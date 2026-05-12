using SimpleFileBrowser;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.IO;
using System.Security.Cryptography;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SocialPlatforms.Impl;
using UnityEngine.UI;
using static UnityEngine.GraphicsBuffer;
using TimeUtil = UnityEngine.Time;

public class WheelSession : MonoBehaviour
{
    //public GameController controller;
    public WheelControl Wheel;
    public TargetControl Target;
    //public Image LRSImage;

    //private GameObject LRSDurationField;
    //private GameObject TargetWidthField;

    private GameObject levelScoreObject;
    private GameObject lifeMarkers;

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
    private string sessionNumber;
    private int score;  // current score
    private int mistakeCount; // number of errors
    private int currLives; // number of lives left
    private int defaultLives; // number of errors allowed
    private int level;  // current level
    private bool booped;  // whether the current eventBox has been hit
    private int eventCount;  // total number of contact events (beats)
    private AudioSource audioSource;
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
    private Collider beatZoneObject;
    private Collider safeZoneObject;

    private List<double> tickTimes = new();
    private List<double> tapTimes = new();
    private List<double> tapAngles = new();
    private int lastEventNum = 0;

    public ParameterLoader parameters;
    private List<TrialParameters> trials;

    private static string logFilePath = Application.dataPath + "/Data/EventLog.txt";

    public InputActionAsset inputActions;
    private InputAction triggerAction;
    private InputAction cancelAction;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // get refs to objects from GameController
        gameOver = false;
        gameOverStarted = false;

        trialIsRunning = false;
        score = 0;
        eventCount = 0;
        booped = false;
        pause = false;

        beatZoneColorFade = beatZoneColorDefault;
        beatZoneColorFade.a = .5f;

        audioSource = GetComponent<AudioSource>();
        Wheel.gameObject.SetActive(false);
        Target.gameObject.SetActive(false);

    }

    void Update()
    {
        if (trialIsRunning && eventCount >= eventMax)
        {
            EndTrial(false);
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
        EventLogger.LogEvent("Game", "Version", Application.version);
        EventLogger.LogEvent("Game", "Game", Application.productName);
        EventLogger.LogEvent("Game", "Fixed Timestep Precise", cfg.timeStepPrecise.ToString());
        float fixedTimestep = TimeUtil.fixedDeltaTime;
        EventLogger.LogEvent("Game", "Fixed Timestep Slow", fixedTimestep.ToString());
        EventLogger.LogEvent("Session", "Animal", AnimalName);
        //EventLogger.LogEvent("Session", "Attention", attentionText);
        EventLogger.LogEvent("Session", "Presession Notes", cfg.preNotesText);
        EventLogger.LogEvent("Session", "LRS Duration", LRSDuration.ToString());
        EventLogger.LogEvent("Session", "Target Width", targetZoneWidth.ToString());

        EventLogger.LogEvent("Session", "Phase", sessionNumber);
        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        EventLogger.LogEvent("Session", "Session Start", timestamp);

        score = 0;
        trialIsRunning = false;
        eventCount = 0;
        booped = false;
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
        // store trial info in data file
        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        EventLogger.LogEvent("Trial", "Trial " + (currTrial + 1) + " started", timestamp);

        EventLogger.LogEvent("Trial Param", "Level", trials[currTrial].level.ToString());
        EventLogger.LogEvent("Trial Param", "Wheel Tempo", trials[currTrial].wheelSpeed.ToString());
        string eventList = string.Join(", ", trials[currTrial].eventList);
        EventLogger.LogEvent("Trial Param", "Event List", eventList);
        EventLogger.LogEvent("Trial Param", "Max Beats", trials[currTrial].beatMax.ToString());
        EventLogger.LogEvent("Trial Param", "Target Score", trials[currTrial].targetScore.ToString());
        EventLogger.LogEvent("Trial Param", "Safe Zone Size", trials[currTrial].colliderSize.ToString());
        EventLogger.LogEvent("Trial Param", "Beat Zone Size", trials[currTrial].beatZoneSize.ToString());

        // initiate wheel and eventBoxes
        Wheel.wheelTempo = trials[currTrial].wheelSpeed;
        Wheel.eventList = trials[currTrial].eventList;
        eventMax = trials[currTrial].beatMax;
        targetScore = trials[currTrial].targetScore;
        colliderSize = trials[currTrial].colliderSize;
        beatZoneSize = trials[currTrial].beatZoneSize;
        level = trials[currTrial].level;
        eventCount = 0;
        score = 0;
        mistakeCount = 0;
        lastEventNum = 0;
        currLives = defaultLives;
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
        Wheel.Reset();
        //Debug.Break();
        TimeUtil.fixedDeltaTime = GameController.Instance.timeStepPrecise;
        TimeUtil.maximumDeltaTime = GameController.Instance.timeStepPrecise * 3;


        tickTimes.Clear();
        tapTimes.Clear();
        tapAngles.Clear();

        Wheel.StartSpin();


    }

    public void PauseGame()
    {
        // LRS has been triggered
        if (pause) return;
        EventLogger.LogEvent("Feedback", "LRS initiated");
        pause = true;
        //LRSImage.enabled = true; // Enable the blackout image
        Wheel.StopSpin();
        //controller.TriggerLRS(duration);
        //scoreText.enabled = false;
        //Invoke(nameof(DisableLRS), duration); // Disable after the duration
        
    }

    public void ResumeGame()
    {
        // resume after pausing
        if (!pause) return;
        EventLogger.LogEvent("Feedback", "LRS ended");
        pause = false;
        //LRSImage.enabled = false; // Disable the blackout image

        //controller.InGameText.SetActive(true);
        //scoreText.enabled = true;
        Wheel.StartSpin();
        
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
                Target.Bounce();
                double tapTime = TimeUtil.fixedTimeAsDouble;
                tapTimes.Add(tapTime);

                // calculate phase angle of tap
                // Problematic if tapping before first tick - no known time point to determine beat onset
                // But we could get current wheel angle and calculate angle of next beat...
                // On the other hand, can you really argue for the angle of taps before the first tick being meaningful in relation to the beat construct in any way?
                // Maybe if they're ahead of the first tick but close? Then it's a question of accuracy, but still likely before any construct of beat is created 
                double tapPhase;
                if (lastEventNum > 0)
                {
                    // if tap is after first tick
                    tapPhase = GetAngle(tapTime, tickTimes[^1]);
                    tapAngles.Add(tapPhase);
                }
                else
                {
                    // Tap is before first tick, so count as error
                    //tapPhase = GetAngle(tapTime, -1d);

                }
                //tapAngles.Add(tapPhase);
                //EventLogger.LogEvent("Debug", "Tap Phase", tapPhase.ToString());

                // Classify the tap
                if (beatZoneContact && !booped)
                {
                    // hit in beat zone, score point
                    EventLogger.LogEvent("Response", "Hit");
                    booped = true;
                    if (beatZoneObject != null)
                    {
                        beatZoneObject.GetComponent<Renderer>().material.color = beatZoneColorFlash;
                    }

                    score++;
                    currLives = defaultLives;
                    UpdateLives(currLives);  // reset lives to max

                    // TODO: placeholder for calculating accuracy

                    GameController.Instance.UpdateScore(score);
                    audioSource.PlayOneShot(goodHitSound);

                    if (score >= targetScore)
                    {
                        audioSource.PlayOneShot(bridgeSound);
                        EndTrial();
                    }

                }
                else if (safeZoneContact && !booped)
                {
                    // hit in safe zone, no score change
                    EventLogger.LogEvent("Response", "Safe");
                    booped = true;
                    currLives = defaultLives;
                    UpdateLives(currLives);  // reset lives to max
                    safeZoneObject.transform.Find("BeatZone").GetComponent<Renderer>().material.color = beatZoneColorFade;



                    //audioSource.PlayOneShot(goodHitSound);

                }
                else
                {
                    if (safeZoneContact || beatZoneContact)
                    {
                        // in the safeZone or beatZone but not counted as hit
                        EventLogger.LogEvent("Response", "Miss (already hit)");
                    }
                    else
                    {
                        EventLogger.LogEvent("Response", "Miss");
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

                    //audioSource.PlayOneShot(badHitSound, 0.5f);
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
        TimeUtil.fixedDeltaTime = GameController.Instance.timeStepSlow;
        TimeUtil.maximumDeltaTime = GameController.Instance.timeStepSlow * 3;
        trialIsRunning = false;
        EventLogger.LogEvent("Trial", "Trial " + (currTrial + 1) + " ended");
        Wheel.Clear();
        Wheel.Resize();
        beatZoneContact = false;
        safeZoneContact = false;
        booped = false;

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

    public double GetAngle(double tapTime, double prevTick)
    {
        // Important to note this is approximate - we're guessing when the next beat will happen based on math, but can't be 100% certain due to many points of variability

        double nextTick;
        if (prevTick < 0)
        {
            // Special case when the tap occurs before any tick, so we have to estimate both the previous tick time and the next tick time
            // Calculate next tick time using wheel angle and speed (first tick is always at 0 degrees)
            double wheelAngle = Wheel.GetRotation();
            // To start the wheel is usually rotated a bit before the first tick, so rotation just below 360. Convert to 
            if (wheelAngle > 270.0)
            {
                wheelAngle = 360.0 - wheelAngle;
            }
            nextTick = tapTime + wheelAngle / (Wheel.wheelTempo * 360.0);
            //EventLogger.LogEvent("Debug", "Next Tick", nextTick.ToString());
            prevTick = nextTick - Wheel.eventList[lastEventNum] / (Wheel.wheelTempo * Wheel.SumArray(Wheel.eventList));
        }
        else
        {
            // Get which interval we're on - we can tell which eventBox was the most recent since it's stored in lastEventNum
            // For taps after the first tick, tested with following parameters: timestep = 0.004, tempo = 0.25, pattern 1,1,1,1, beat zone size = 2
            // All predicted phases were smaller than 1x10E-6
            nextTick = prevTick + Wheel.eventList[lastEventNum - 1] / (Wheel.wheelTempo * Wheel.SumArray(Wheel.eventList));
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

    void ActivateInputs()
    {
        //Debug.Log("Trigger triggered!");
        TargetControl.OnContactStart += WindowContactOn;
        TargetControl.OnContactEnd += WindowContactOff;
        TargetControl.OnBeatZoneStart += BeatZoneContactOn;
        TargetControl.OnBeatZoneEnd += BeatZoneContactOff;
        BeatTicker.OnBeatContact += BeatContact;

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
        TargetControl.OnContactStart -= WindowContactOn;
        TargetControl.OnContactEnd -= WindowContactOff;
        TargetControl.OnBeatZoneStart -= BeatZoneContactOn;
        TargetControl.OnBeatZoneEnd -= BeatZoneContactOff;
        BeatTicker.OnBeatContact -= BeatContact;

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

    private void WindowContactOn()
    {
        EventLogger.LogEvent("Beat", "Beat safe window start");
        eventCount++;

        safeZoneObject = Target.safeZone;
        safeZoneContact = true;
        booped = false;

    }

    private void WindowContactOff()
    {
        EventLogger.LogEvent("Beat", "Beat safe window end");
        safeZoneContact = false;
        if (!booped)  // If beat passes without a tap, reset score
        {
            if (score > 0)
            {
                score = 0;
                GameController.Instance.UpdateScore(score);
            }
        }
        Wheel.ResetBoxColors();  // Reset all EventBox pieces to their default colors, just in case one got colored weird for some reason
    }

    private void BeatContact()
    {
        EventLogger.LogEvent("Beat", "Beat tick");
        if (int.Parse(sessionNumber) > 0)
        {
            audioSource.PlayOneShot(tickSound);
        }
        double time = TimeUtil.fixedTimeAsDouble;
        tickTimes.Add(time);
        string lastEventStr = safeZoneObject.name[^2..];
        lastEventStr = char.IsDigit(lastEventStr[^2]) ? lastEventStr[^2..] : lastEventStr[^1..];  // If only one character is number, just grab that one character
        lastEventNum = int.Parse(lastEventStr);



    }

    private void BeatZoneContactOn()
    {
        EventLogger.LogEvent("Beat", "Beat zone start");
        beatZoneContact = true;
        beatZoneObject = Target.beatZone;
    }

    private void BeatZoneContactOff()
    {
        EventLogger.LogEvent("Beat", "Beat zone end");
        beatZoneContact = false;
    }
}
