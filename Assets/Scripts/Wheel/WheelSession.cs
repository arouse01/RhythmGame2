using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using TimeUtil = UnityEngine.Time;

public class WheelSession : MonoBehaviour
{
    //public GameController controller;
    private string GameType;
    public WheelControl Wheel;
    public TargetControl Target;
    //public Image LRSImage;

    [SerializeField] private AudioManager audioManager;  // more centralized audio manager

    //private GameObject LRSDurationField;
    //private GameObject TargetWidthField;
    double startDSPtime;
    double currDSPtime;
    //private GameObject levelScoreObject;
    //private GameObject lifeMarkers;

    private bool gameOver;  // game is over, move to user input
    private bool gameOverStarted;  // gameover process started

    public AudioClip bridgeSound;
    public AudioClip tickSound;
    public AudioClip goodHitSound;
    public AudioClip badHitSound;
    public bool pause;

    public Color safeZoneColorDefault;
    public Color beatZoneColorDefault;
    public Color beatZoneColorFlash;
    private Color beatZoneColorFade; // fade beat zone on boop

    private bool safeZoneContact; // whether target is touching an eventBox
    private bool beatZoneContact; // whether target is touching center of eventBox
    private string sessionNumber;  // store session number so we can disable some events for specific phases (i.e. phase 0 where we don't need a score and don't want a sound on beat contact)
    private int score;  // current score
    private int mistakeCount; // number of errors
    private int currLives; // number of lives left
    private int defaultLives; // number of errors allowed
    private int level;  // current level
    private bool bopped;  // whether the current eventBox has been hit
    //private int eventCount;  // total number of contact events (beats)
    private int prevBeatIndex;
    private int nextBeatIndex;
    //private AudioSource audioSource;
    private int numTrials;  // number of trials
    private int currTrial; // index of current trial
    private int eventMax; // max beats to present
    private int targetScore;  // target score to pass
    private bool trialIsRunning; // whether trial is running or not
    private float LRSDuration = 3; // how long the LRS should be visible
    //private int LRSThresh = 3; // how long the LRS should be visible
    private float targetZoneWidth = 0.25f; // width of the target zone around the avatar
    private float colliderSize;  // width of the eventBox collider
    private float beatZoneSize; // width of the beatZone collider
    //private Collider beatZoneObject;
    //private Collider safeZoneObject;
    public EventBox currEventBox;

    private List<WheelBeatEvent> wheelBeats;
    private List<double> tickTimes = new();
    private List<double> tapTimes = new();
    private List<double> tapAngles = new();
    private int lastEventNum = 0;

    public ParameterLoader parameters;
    private List<WheelTrialParameters> trials;

    private static string logFilePath = Application.dataPath + "/Data/EventLog.txt";

    public InputActionAsset inputActions;
    private InputAction triggerAction;
    private InputAction cancelAction;

    void Start()
    {
        //TODO: should the gameType variable be hardcoded like this?
        GameType = "Wheel";
        // get refs to objects from GameController
        gameOver = false;
        gameOverStarted = false;

        trialIsRunning = false;
        score = 0;
        //eventCount = 0;
        bopped = false;
        pause = false;

        beatZoneColorFade = beatZoneColorDefault;
        beatZoneColorFade.a = .5f;

        //audioSource = GetComponent<AudioSource>();
        Wheel.gameObject.SetActive(false);
        Target.gameObject.SetActive(false);

        wheelBeats = new();
    }

    void Update()
    {
        if (trialIsRunning)
        {
            currDSPtime = (AudioSettings.dspTime - startDSPtime);
            double displayTime = currDSPtime + Time.smoothDeltaTime * 0.5; // We're inflating currDSPtime a tiny bit (based on current actual framerate) to account for visual lag introduced by only updating at game framerate. 

            if (nextBeatIndex >= eventMax)
            {
                EndTrial(false);
            }

            RotateWheel(displayTime);

            UpdateBeats(displayTime);

        }

    }

    public void StartSession()
    {
        // Triggered session start, assuming that GameController has already verified the prefs and settings
        var cfg = GameController.Instance;

        string sessionFile = cfg.levelParameterFile;
        string AnimalName = cfg.animalName;

        string phaseParamPath = PlayerPrefs.GetString("PhaseParamFolder");
        string savePath = PlayerPrefs.GetString("SaveFolder");

        LRSDuration = PlayerPrefs.GetFloat("LRSDuration");

        targetZoneWidth = PlayerPrefs.GetFloat("TargetWidth");
        
        Target.targetZoneWidth = targetZoneWidth;

        //preGamePanel.SetActive(false);
        //UserInputObject.SetActive(false);
        //InGameText.SetActive(true);

        Wheel.gameObject.SetActive(true);
        Target.gameObject.SetActive(true);
        cfg.ShowLRS(false);

        // read parameter file
        trials = ParameterLoader.LoadWheelTrialParameters(phaseParamPath, sessionFile);
        
        // Get number of trials
        numTrials = trials.Count;

        currTrial = 0;

        sessionNumber = System.Text.RegularExpressions.Regex.Replace(sessionFile, "[^0-9]", "");
        //Target.InitializeTarget();

        //// create log file
        System.DateTime currentTime = System.DateTime.Now;
        string currDate = currentTime.ToString("yyyyMMddHHmmss");

        //// Format the date and time to include milliseconds
        //string timeWithMilliseconds = currentTime.ToString("yyyy-MM-dd HH:mm:ss.fff");

        string logFileName = AnimalName + "_" + currDate + ".txt";

        string logFileFolder = Path.Combine(savePath, AnimalName);
        logFilePath = Path.Combine(logFileFolder, logFileName);
        if (!Directory.Exists(logFileFolder))
        {
            Directory.CreateDirectory(logFileFolder);
        }
        EventLogger.SetLogFilePath(logFilePath);
        EventLogger.StartLog();
        double currTime = AudioSettings.dspTime;
        EventLogger.StartSession(currTime);
        EventLogger.Log(LogItem.GameData(currTime, "Version", Application.version));
        EventLogger.Log(LogItem.GameData(currTime, "App", Application.productName));
        EventLogger.Log(LogItem.GameData(currTime, "Game Type", GameType));
        EventLogger.Log(LogItem.GameData(currTime, "Fixed Timestep Precise", cfg.timeStepPrecise.ToString()));
        float fixedTimestep = TimeUtil.fixedDeltaTime;
        EventLogger.Log(LogItem.GameData(currTime, "Fixed Timestep Slow", fixedTimestep.ToString()));
        EventLogger.Log(LogItem.SessionData(currTime, "Animal", AnimalName));
        //EventLogger.LogData("Session", "Attention", attentionText);
        EventLogger.Log(LogItem.SessionData(currTime, "Presession Notes", cfg.preNotesText));
        EventLogger.Log(LogItem.SessionData(currTime, "LRS Duration", LRSDuration.ToString()));
        EventLogger.Log(LogItem.SessionData(currTime, "Target Width", targetZoneWidth.ToString()));

        EventLogger.Log(LogItem.SessionData(currTime, "Phase", sessionNumber));
        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        EventLogger.Log(LogItem.SessionData(currTime, "Session Start", timestamp)); 
        

        score = 0;
        trialIsRunning = false;
        //eventCount = 0;
        bopped = false;
        pause = false;

        defaultLives = 3;

        cfg.ShowLevelScore(false);
        cfg.ShowLifeMarkers(false);

        cfg.UpdateMessage("Click to start<br>Phase " + sessionNumber);
        cfg.UpdateStats(""); // clear statsText because no trials have been run yet
        Wheel.StopSpin();

        ActivateInputs();
        


    }

    void StartTrial()
    {
        
        //EventLogger.LogData("Trial", "Trial " + (currTrial + 1) + " started", timestamp);

        //EventLogger.LogData("Trial Param", "Level", trials[currTrial].level.ToString());
        //EventLogger.LogData("Trial Param", "Wheel Tempo", trials[currTrial].wheelSpeed.ToString());
        //string eventList = string.Join(", ", trials[currTrial].eventList);
        //EventLogger.LogData("Trial Param", "Event List", eventList);
        //EventLogger.LogData("Trial Param", "Max Beats", trials[currTrial].beatMax.ToString());
        //EventLogger.LogData("Trial Param", "Target Score", trials[currTrial].targetScore.ToString());
        //EventLogger.LogData("Trial Param", "Safe Zone Size", trials[currTrial].colliderSize.ToString());
        //EventLogger.LogData("Trial Param", "Beat Zone Size", trials[currTrial].beatZoneSize.ToString());

        // initiate wheel and eventBoxes
        Wheel.wheelTempo = trials[currTrial].wheelSpeed;
        Wheel.eventList = trials[currTrial].eventList;
        eventMax = trials[currTrial].beatMax;
        targetScore = trials[currTrial].targetScore;
        colliderSize = trials[currTrial].colliderSize;
        beatZoneSize = trials[currTrial].beatZoneSize;
        level = trials[currTrial].level;
        //eventCount = 0;
        score = 0;
        mistakeCount = 0;
        lastEventNum = 0;
        currLives = defaultLives;
        prevBeatIndex = 0;
        nextBeatIndex = 0;
        GameController.Instance.ShowLifeMarkers(true);
        UpdateLives(currLives);
        GameController.Instance.UpdateMessage("");
        GameController.Instance.UpdateStats("");
        GameController.Instance.UpdateScore(score);
        GameController.Instance.ShowLevelScore(false);
        trialIsRunning = true;
        Wheel.colliderSize = colliderSize;
        Wheel.beatZoneSize = beatZoneSize;
        Wheel.safeZoneColorDefault = safeZoneColorDefault;
        Wheel.beatZoneColorDefault = beatZoneColorDefault;
        Target.beatZoneColorDefault = beatZoneColorDefault;
        Wheel.gameLevel = level;
        Wheel.ResetWheel();
        CreateWheelEvents();
        //Debug.Break();
        TimeUtil.fixedDeltaTime = GameController.Instance.timeStepPrecise;
        TimeUtil.maximumDeltaTime = GameController.Instance.timeStepPrecise * 3;


        tickTimes.Clear();
        tapTimes.Clear();
        tapAngles.Clear();

        // store trial info in data file
        startDSPtime = AudioSettings.dspTime;
        EventLogger.StartTrial(startDSPtime);
        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        EventLogger.Log(LogItem.TrialData(startDSPtime, currTrial, "Trial Started", timestamp));
        EventLogger.Log(LogItem.TrialData(startDSPtime, currTrial, "Level", trials[currTrial].level.ToString()));
        EventLogger.Log(LogItem.TrialData(startDSPtime, currTrial, "Wheel Tempo", trials[currTrial].wheelSpeed.ToString()));

        string eventList = string.Join(", ", trials[currTrial].eventList);
        EventLogger.Log(LogItem.TrialData(startDSPtime, currTrial, "Wheel Event List", eventList));
        EventLogger.Log(LogItem.TrialData(startDSPtime, currTrial, "Max Beats", trials[currTrial].beatMax.ToString()));
        EventLogger.Log(LogItem.TrialData(startDSPtime, currTrial, "Target Score", trials[currTrial].targetScore.ToString()));
        EventLogger.Log(LogItem.TrialData(startDSPtime, currTrial, "Safe Zone Size", trials[currTrial].colliderSize.ToString()));
        EventLogger.Log(LogItem.TrialData(startDSPtime, currTrial, "Beat Zone Size", trials[currTrial].beatZoneSize.ToString()));

        Wheel.StartSpin();


    }

    public void PauseGame()
    {
        // LRS has been triggered
        if (pause) return;
        double currTime = AudioSettings.dspTime;
        EventLogger.Log(LogItem.Feedback(currTime, currTrial, "LRS initiated"));
        //EventLogger.LogData("Feedback", "LRS initiated");

        pause = true;
        //LRSImage.enabled = true; // Enable the blackout image
        Wheel.StopSpin();
        AudioListener.pause = true;  // this pauses the DSPtime as well

        //controller.TriggerLRS(duration);
        //scoreText.enabled = false;
        //Invoke(nameof(DisableLRS), duration); // Disable after the duration

    }

    public void ResumeGame()
    {
        // resume after pausing
        if (!pause) return;
        double currTime = AudioSettings.dspTime;
        EventLogger.Log(LogItem.Feedback(currTime, currTrial, "LRS ended"));
        //EventLogger.LogData("Feedback", "LRS ended");
        pause = false;
        //LRSImage.enabled = false; // Disable the blackout image

        //controller.InGameText.SetActive(true);
        //scoreText.enabled = true;
        Wheel.StartSpin();
        AudioListener.pause = false;  // this pauses the DSPtime as well

    }

    private void OnClick(InputAction.CallbackContext context)
    {
        //Debug.Log("Clicked!");
        if (gameOver & !gameOverStarted)
        {
            GameOver();
        }
        else if (gameOver)
        {

        }
        else if (!trialIsRunning & !gameOver)
        {
            if (!pause)
            {
                StartTrial();
            }

        }
        else
        {
            if (!pause)
            {
                //double tapTime = TimeUtil.fixedTimeAsDouble;
                double tapTimeRaw = AudioSettings.dspTime;
                double tapTime = tapTimeRaw - startDSPtime;
                tapTimes.Add(tapTime);

                Target.Bounce();

                // calculate phase angle of tap
                // Problematic if tapping before first tick - no known time point to determine beat onset
                // But we could get current wheel angle and calculate angle of next beat...
                // On the other hand, can you really argue for the angle of taps before the first tick being meaningful in relation to the beat construct in any way?
                // Maybe if they're ahead of the first tick but close? Then it's a question of accuracy, but still likely before any construct of beat is created 
                double tapPhase = GetPhaseAngle(tapTime);
                tapAngles.Add(tapPhase);
                

                // Classify the tap
                if (beatZoneContact && !bopped)
                {
                    // hit in beat zone, score point
                    EventLogger.Log(LogItem.Response(tapTimeRaw, currTrial, 0, "Hit", tapPhase));
                    int closestIndex = (tapPhase > 0) ? prevBeatIndex : nextBeatIndex;
                    wheelBeats[closestIndex].Bopped = true;
                    bopped = true;
                    currEventBox.Bop(beatZoneColorFlash);
                    
                    //if (beatZoneObject != null)
                    //{
                    //    currEventBox.Bop(beatZoneColorFlash);
                    //    //beatZoneObject.GetComponent<Renderer>().material.color = beatZoneColorFlash;
                    //}

                    score++;
                    currLives = defaultLives;
                    UpdateLives(currLives);  // reset lives to max

                    // TODO: placeholder for calculating accuracy

                    GameController.Instance.UpdateScore(score);
                    PlayPlayerSound(goodHitSound);

                    if (score >= targetScore)
                    {
                        PlayPlayerSound(bridgeSound);
                        EndTrial();
                    }

                }
                else if (safeZoneContact && !bopped)
                {
                    // hit in safe zone, no score change
                    EventLogger.Log(LogItem.Response(tapTimeRaw, currTrial, 0, "Safe", tapPhase));
                    //EventLogger.LogData("Response", "Safe");
                    int closestIndex = (tapPhase > 0) ? prevBeatIndex : nextBeatIndex;
                    wheelBeats[closestIndex].Bopped = true;
                    bopped = true;
                    currLives = defaultLives;
                    UpdateLives(currLives);  // reset lives to max

                    currEventBox.BopSafe();
                    //safeZoneObject.transform.Find("BeatZone").GetComponent<Renderer>().material.color = beatZoneColorFade;



                    //PlayPlayerSound(goodHitSound);

                }
                else
                {
                    if (safeZoneContact || beatZoneContact)
                    {
                        // in the safeZone or beatZone but not counted as hit
                        EventLogger.Log(LogItem.Response(tapTimeRaw, currTrial, 0, "Miss (already hit)", tapPhase));
                    }
                    else
                    {
                        EventLogger.Log(LogItem.Response(tapTimeRaw, currTrial, 0, "Miss", tapPhase));
                    }

                    if (score > 0)
                    {
                        score = 0;
                    }

                    mistakeCount++;
                    GameController.Instance.UpdateScore(score);

                    currLives--;
                    UpdateLives(currLives);

                    // TODO: placeholder for accuracy update

                    //PlayPlayerSound(badHitSound, 0.5f);
                }

                //if (currLives <= 0)
                //{

                //    TriggerLRS(LRSDuration);
                //    score = 0;
                //    scoreText.SetText(score.ToString());
                //}



            }
        }

    }

    private void OnEscape(InputAction.CallbackContext context)
    {
        EndTrial();
        GameOver();
    }
    
    void EndTrial(bool success = true)
    {
        Wheel.StopSpin();
        audioManager.StopAll();
        TimeUtil.fixedDeltaTime = GameController.Instance.timeStepSlow;
        TimeUtil.maximumDeltaTime = GameController.Instance.timeStepSlow * 3;
        trialIsRunning = false;
        double currTime = AudioSettings.dspTime;
        EventLogger.Log(LogItem.TrialData(currTime, currTrial, "Trial Ended"));
        //EventLogger.LogData("Trial", "Trial " + (currTrial + 1) + " ended");
        Wheel.Clear();
        Wheel.Resize();
        beatZoneContact = false;
        safeZoneContact = false;
        bopped = false;

        // pause so the score screen doesn't get skipped
        pause = true;

        GameController.Instance.ShowLifeMarkers(false);

        if (success)
        {

            // TODO: calculate level score
            if (int.Parse(sessionNumber) > 0)
            {
                UpdateLevelScore();
            }
        }
        else
        {

        }


        // wait before allowing to go on
        StartCoroutine(TrialEndPause(2f));

    }

    private IEnumerator TrialEndPause(float duration)
    {
        pause = true;
        yield return new WaitForSeconds(duration); // Wait for the specified time
        //float counter = 0;
        //while (counter < duration) 
        //{
        //    counter += Time.deltaTime;
        //}
        pause = false; // Turn off the blackout
        // if not max trial, start next trial
        if (currTrial < (numTrials - 1))
        {
            currTrial++;
            GameController.Instance.UpdateMessage("Click to start<br>Trial " + (currTrial + 1).ToString());
        }
        else
        {
            GameController.Instance.UpdateMessage("Game Over");
            gameOver = true;
        }
    }

    void RotateWheel(double currTime)
    {
        // Rotate at rotSpeed degrees per second
        //float currAngle = Wheel.transform.eulerAngles.z;
        //if (currAngle % 360 < 1)
        //{
        //    Debug.Log($"Time: {currTime}, Angle: {currAngle}");
        //}
        float newAngle = (float)((currTime) * Wheel.wheelTempo * 360f - Wheel.startAngle);
        //Wheel.transform.RotateAround(Wheel.transform.position, Vector3.forward, newAngle);
        Wheel.transform.rotation = Quaternion.Euler(0, 0, newAngle);
        
    }

    void CreateWheelEvents()
    {
        // take the EventBoxes and create a list of beat events with full times
        // Wheel has just been reset and rotated to its starting position, so we can use the current rotation to calculate onset times

        wheelBeats.Clear();
        wheelBeats = new();

        double rotSpeed = Wheel.wheelTempo * 360.0f; // rotation in degrees per second
        float currAngle = Wheel.startAngle;
        //double startDelay = Wheel.startAngle / rotSpeed;

        int j;
        for (int i = 0; i < eventMax; i++)
        {
            j = i % Wheel.boxes.Count;
            WheelBeat beat = Wheel.boxes[j].wheelBeat;
            
            double boopTime = (currAngle / rotSpeed);

            double safeStart = boopTime - (colliderSize / 2) / rotSpeed;
            double safeEnd = boopTime + (colliderSize / 2) / rotSpeed;
            double beatStart = boopTime - (beatZoneSize / 2) / rotSpeed;
            double beatEnd = boopTime + (beatZoneSize / 2) / rotSpeed;

            WheelBeatEvent currBeatEvent = new()
            {
                EventBox = Wheel.boxes[j],
                BeatIndex = i,
                BoopTime = boopTime,
                SafeZoneStartTime = safeStart,
                SafeZoneEndTime = safeEnd,
                BeatZoneStartTime = beatStart,
                BeatZoneEndTime = beatEnd,
            };
            wheelBeats.Add(currBeatEvent);
            
            currAngle += beat.interval;  // total angle turned, regardless of direction (since there's no direction change)
        }
    }
    
    void UpdateBeats(double currTime)
    {
        int beatBuffer = 3;  // How far away is considered "nearby" when looking at adjacent beats - that way we only process the nearest instead of all and still retain enough in the past to capture leaving the safe zone
        int iStart = prevBeatIndex > beatBuffer ? prevBeatIndex - beatBuffer : 0;
        int iEnd = prevBeatIndex + beatBuffer > wheelBeats.Count ? wheelBeats.Count : prevBeatIndex + beatBuffer;
        for (int i = iStart; i <= iEnd; i++) 
        {
            WheelBeatEvent currBeat = wheelBeats[i];

            if (currBeat.SafeZoneStartTime <= currTime && !currBeat.EnteredSafeZone)
            {
                WindowContactOn(currBeat);
                currBeat.EnteredSafeZone = true;
            }
            
            if (currBeat.BeatZoneStartTime <= currTime && !currBeat.EnteredBeatZone)
            {
                BeatZoneContactOn(currBeat);
                currBeat.EnteredBeatZone = true;
            }
            
            if (currBeat.BoopTime <= currTime && !currBeat.Booped)
            {
                BeatContact(currBeat);
                currBeat.Booped = true;
            }

            if (currBeat.BeatZoneEndTime <= currTime && !currBeat.ExitedBeatZone)
            {
                BeatZoneContactOff(currBeat);
                currBeat.ExitedBeatZone = true;
            }

            if (currBeat.SafeZoneEndTime <= currTime && !currBeat.ExitedSafeZone)
            {
                WindowContactOff(currBeat);
                currBeat.ExitedSafeZone = true;
            }

            if ((currBeat.BoopTime - 2) <= currTime && !currBeat.BoopSet)
            {
                currBeat.BoopSet = true;
                double boopTime = currBeat.BoopTime + startDSPtime;
                Debug.Log($"Scheduling boop at {currBeat.BoopTime}");
                ScheduleBoopAudio(boopTime);
            }

        }
    }
    
    void UpdateLevelScore(bool success = true)
    {
        double? meanAngle;  //? makes the double nullable
        double vecLength;
        if (tapAngles.Count > 0)
        {
            meanAngle = Stats.CircMean(tapAngles, returnRad: false);
            vecLength = Stats.CircVectorLength(tapAngles);
        }
        else
        {
            // No angles means we can't calculate any of this
            meanAngle = null;
            vecLength = 0;
        }

        int numStars;
        if (success)
        {
            // success means the user met criteria, which means at least one tap


            // Formula for numStars: 
            if (meanAngle != null)
            {
                if (mistakeCount <= 1)
                {
                    numStars = 3;
                }
                else if (mistakeCount <= 4)
                {
                    numStars = 2;
                }
                else
                {
                    numStars = 1;
                }
            }
            else
            {
                if (mistakeCount <= 1)
                {
                    numStars = 3;
                }
                else if (mistakeCount <= 4)
                {
                    numStars = 2;
                }
                else
                {
                    numStars = 1;
                }
            }

        }
        else
        {
            // Trial timed out
            numStars = 1;
            GameController.Instance.UpdateMessage("Maximum beats exceeded");
        }

        //statsText.SetText($"θ = {meanAngle:0.00}<br>r = {vecLength:0.00}");

        GameController.Instance.UpdateMessage("Mistakes: " + (mistakeCount).ToString());
        GameController.Instance.ShowLevelScore(true);
        GameController.Instance.UpdateStars(numStars);

    }

    public void UpdateLives(int currLives)
    {
        // Logic for handling lives, which is echoed to GameController for display updates
        if (currLives == 0)
        {
            // reached zero lives
            GameController.Instance.TriggerBlackout(LRSDuration);
            UpdateLives(defaultLives);
        } 
        else
        {
            GameController.Instance.UpdateLives(currLives);
        }
          
    }
    
    void GameOver()
    {
        DeactivateInputs();
        //InGameText.SetActive(false);
        //gameOverStarted = true;
        //UserInputObject.SetActive(true);
        //gameOverPanel.SetActive(true);
        //prefsButton.SetActive(false);
        
        Wheel.gameObject.SetActive(false);
        Target.gameObject.SetActive(false);
        GameController.Instance.GameOver();
    }

    public double GetPhaseAngle(double tapTime)
    {
        // Since we'll know the exact onset times of all beats, we can calculate the next and previous beat times from the raw data instead of relying on an additional parameter

        double nextTick = wheelBeats[nextBeatIndex].BoopTime;
        double prevTick;
        if (nextBeatIndex == 0)  // taps that happen before first tick
        {
            // Special case when the tap occurs before any tick, so we have to estimate both the previous tick time and the next tick time
            //// Calculate next tick time using wheel angle and speed (first tick is always at 0 degrees)
            //double wheelAngle = Wheel.GetRotation();
            //// To start the wheel is usually rotated a bit before the first tick, so rotation just below 360. Convert to 
            //if (wheelAngle > 270.0)
            //{
            //    wheelAngle = 360.0 - wheelAngle;
            //}
            //nextTick = tapTime + wheelAngle / (Wheel.wheelTempo * 360.0);
            //EventLogger.LogData("Debug", "Next Tick", nextTick.ToString());
            prevTick = nextTick - Wheel.eventList[lastEventNum] / (Wheel.wheelTempo * Stats.SumArray(Wheel.eventList));
        }
        else
        {
            // Get which interval we're on - we can tell which eventBox was the most recent since it's stored in lastEventNum
            // For taps after the first tick, tested with following parameters: timestep = 0.004, tempo = 0.25, pattern 1,1,1,1, beat zone size = 2
            // All predicted phases were smaller than 1x10E-6
            prevTick = wheelBeats[prevBeatIndex].BoopTime;
                //nextTick = prevTick + Wheel.eventList[lastEventNum - 1] / (Wheel.wheelTempo * Stats.SumArray(Wheel.eventList));
        }
        double closest = (System.Math.Abs(prevTick - tapTime) <= System.Math.Abs(nextTick - tapTime)) ? prevTick : nextTick;
        double interval = nextTick - prevTick;

        double currAngle = 2 * System.Math.PI * (tapTime - closest) / interval;

        return currAngle;
    }

    //private void OnEnable()
    //{
    //    //Debug.Log("Trigger triggered!");
    //    TargetControl.OnContactStart += WindowContactOn;
    //    TargetControl.OnContactEnd += WindowContactOff;
    //    TargetControl.OnBeatZoneStart += BeatZoneContactOn;
    //    TargetControl.OnBeatZoneEnd += BeatZoneContactOff;
    //    BeatTicker.OnBeatContact += BeatContact;

    //    var gameplayActions = inputActions.FindActionMap("Rhythm");
    //    triggerAction = gameplayActions.FindAction("Click");

    //    triggerAction.performed += OnClick;
    //    triggerAction.Enable();

    //}

    void CheckTriggers(int beatIndex, double currTime)
    {
        WheelBeatEvent currBeat = wheelBeats[beatIndex];

    }

    void ActivateInputs()
    {
        //Debug.Log("Trigger triggered!");
        //TargetControl.OnContactStart += WindowContactOn;
        //TargetControl.OnContactEnd += WindowContactOff;
        //TargetControl.OnBeatZoneStart += BeatZoneContactOn;
        //TargetControl.OnBeatZoneEnd += BeatZoneContactOff;
        //BeatTicker.OnBeatContact += BeatContact;

        var gameplayActions = inputActions.FindActionMap("Wheel");
        triggerAction = gameplayActions.FindAction("Click");
        cancelAction = gameplayActions.FindAction("Cancel");

        triggerAction.performed += OnClick;
        triggerAction.Enable();

        cancelAction.performed += OnEscape;
        cancelAction.Enable();
    }

    void DeactivateInputs()
    {
        //Debug.Log("Trigger off");
        //TargetControl.OnContactStart -= WindowContactOn;
        //TargetControl.OnContactEnd -= WindowContactOff;
        //TargetControl.OnBeatZoneStart -= BeatZoneContactOn;
        //TargetControl.OnBeatZoneEnd -= BeatZoneContactOff;
        //BeatTicker.OnBeatContact -= BeatContact;

        if (triggerAction != null)
        {
            triggerAction.performed -= OnClick;
            triggerAction.Disable();
        }

        if (cancelAction != null)
        {
            cancelAction.performed -= OnEscape;
            cancelAction.Disable();
        }
    }
    void OnEnable()
    {
        //Debug.Log($"Subscribing: {gameObject.name}");
        GameController.Instance.OnGameStart += StartSession;
        GameController.Instance.OnGamePause += PauseGame;
        GameController.Instance.OnGameResume += ResumeGame;
    }

    void OnDisable()
    {
        //Debug.Log($"Unsubscribing: {gameObject.name}");
        GameController.Instance.OnGameStart -= StartSession;
        GameController.Instance.OnGamePause -= PauseGame;
        GameController.Instance.OnGameResume -= ResumeGame;
    }

    void ScheduleBoopAudio(double dspTime)
    {
        
        audioManager.ScheduleBeat(tickSound, dspTime);
        
    }

    void PlayPlayerSound(AudioClip clip)
    {
        audioManager.PlayImmediate(clip);
    }

    //private void OnDisable()
    //{
    //    //Debug.Log("Trigger off");
    //    TargetControl.OnContactStart -= WindowContactOn;
    //    TargetControl.OnContactEnd -= WindowContactOff;
    //    TargetControl.OnBeatZoneStart -= BeatZoneContactOn;
    //    TargetControl.OnBeatZoneEnd -= BeatZoneContactOff;
    //    BeatTicker.OnBeatContact -= BeatContact;

    //    if (triggerAction != null)
    //    {
    //        triggerAction.performed -= OnClick;
    //        triggerAction.Disable();
    //    }

    //    if (cancelAction != null)
    //    {
    //        cancelAction.performed -= OnEscape;
    //        cancelAction.Disable();
    //    }

    //}

    private void WindowContactOn(WheelBeatEvent beat)
    {
        double currTime = AudioSettings.dspTime;
        EventLogger.Log(LogItem.Beat(currTime, beat.SafeZoneStartTime, currTrial, beat.BeatIndex, -1, "Safe window start"));
        //EventLogger.LogData("Beat", "Beat safe window start");
        //eventCount++;

        //safeZoneObject = Target.safeZone;
        currEventBox = beat.EventBox;
        safeZoneContact = true;
        bopped = false;

    }

    private void WindowContactOff(WheelBeatEvent beat)
    {
        double currTime = AudioSettings.dspTime;
        EventLogger.Log(LogItem.Beat(currTime, beat.SafeZoneEndTime, currTrial, beat.BeatIndex, -1, "Safe window end"));
        //EventLogger.LogData("Beat", "Beat safe window end");
        safeZoneContact = false;
        if (!beat.Bopped)  // If beat passes without a tap, reset score
        {
            if (score > 0)
            {
                score = 0;
                GameController.Instance.UpdateScore(score);
            }
        }
        beat.EventBox.ResetColors();  // Reset all EventBox pieces to their default colors, just in case one got colored weird for some reason
        currEventBox = null;
    }

    private void BeatContact(WheelBeatEvent beat)
    {
        //EventLogger.LogData("Beat", "Beat tick");
        double timeRaw = AudioSettings.dspTime;
        EventLogger.Log(LogItem.Beat(timeRaw, beat.BoopTime, currTrial, beat.BeatIndex, -1, "Beat tick"));
        double wheelAngle = Wheel.transform.eulerAngles.z;
        //Debug.Log($"Beat {beat.BeatIndex}, Wheel angle is {wheelAngle}");
        prevBeatIndex++;
        nextBeatIndex++;
        //if (int.Parse(sessionNumber) > 0)
        //{
        //    audioSource.PlayOneShot(tickSound);
        //}
        //double time = TimeUtil.fixedTimeAsDouble;
        //tickTimes.Add(time);
        //string lastEventStr = safeZoneObject.name[^2..];
        //lastEventStr = char.IsDigit(lastEventStr[^2]) ? lastEventStr[^2..] : lastEventStr[^1..];  // If only one character is number, just grab that one character
        //lastEventNum = int.Parse(lastEventStr);



    }

    private void BeatZoneContactOn(WheelBeatEvent beat)
    {
        //EventLogger.LogData("Beat", "Beat zone start");
        double currTime = AudioSettings.dspTime;
        EventLogger.Log(LogItem.Beat(currTime, beat.BeatZoneStartTime, currTrial, beat.BeatIndex, -1, "Beat window start"));

        beatZoneContact = true;
        //beatZoneObject = Target.beatZone;
    }

    private void BeatZoneContactOff(WheelBeatEvent beat)
    {
        //EventLogger.LogData("Beat", "Beat zone end");
        double currTime = AudioSettings.dspTime;
        EventLogger.Log(LogItem.Beat(currTime, beat.BeatZoneEndTime, currTrial, beat.BeatIndex, -1, "Beat window end"));

        beatZoneContact = false;
    }
}
