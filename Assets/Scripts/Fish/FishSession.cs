using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.VisualScripting;
using UnityEditor.Timeline.Actions;
using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEngine.GraphicsBuffer;
using TimeUtil = UnityEngine.Time;


/* 
 
I am not artsy. Here are the guides I used to create most of the assets!
Clouds: https://2dgameartguru.com/create-clouds-using-circles-in-inkscape/
 
 
 */
public class FishSession : MonoBehaviour
{
    private string GameType;

    public PlayerControl player;
    public GameObject FishParent;
    //public FishManager fishManager;
    [SerializeField] private BeatObject fishPrefab;  // fish prefab

    [SerializeField] private GameObject BeepLine;
    [SerializeField] private GameObject BoopLine;

    [SerializeField] private AudioManager audioManager;  // more centralized audio manager

    private bool gameOver;  // game is over, move to user input
    private bool gameOverStarted;  // gameover process started
    private bool pause;

    private float beepPos; // x position of the beepLine
    private float boopPos;  // x position of the boopLine
    private float beepBoopDist;
    private bool beepActive = false;
    private float screenRightEdge;
    private float spawnLocation;
    private float destroyLocation;

    // audio timing vars
    private double startDSPtime;
    private double currDSPtime;
    private double beepBoopInterval;
    private int nextBeatCreationIndex;  // next beat to be created
    //private double nextTick = 0.0f;
    //private double downBeatTime = 0;
    //private double lastDownBeatTime = 0;
    //private double beatTime = 0;
    //private double lastBeatTime = 0;
    //public delegate void BeatTrigger();
    //public static event BeatTrigger OnBeat;

    private string sessionNumber; 
    private int score;  // current score
    private int mistakeCount; // number of errors
    private int currLives; // number of lives left
    private int defaultLives; // number of errors allowed
    private int level;  // current level
    public float tempo;  // trial tempo (in bpm)
    //private bool boopedWater;  // whether the current eventBox has been hit
    private bool boopedAir;  // whether the current eventBox has been hit
    private int eventCount;  // total number of contact events (beats)
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
    public GameObject beatZoneObject;
    public GameObject safeZoneObject;


    //private bool safeZoneContactWater; // whether target is touching an eventBox
    //private bool beatZoneContactWater; // whether target is touching center of eventBox
    //private bool safeZoneContactAir; // whether target is touching an eventBox
    //private bool beatZoneContactAir; // whether target is touching center of eventBox
    private float beatZoneStartX;
    private float beatZoneEndX;
    private float safeZoneStartX;
    private float safeZoneEndX;

    private Beat[] fishEventListRaw;
    private List<BeatEvent> fishBeats;  // beat onsets for current trial
    private List<BeatObject> activeBeats;
    private List<BeatEvent> remainingBeats;
    private List<BeatEvent> birdBeats;  // beat onsets for current trial
    private List<double> tickTimes = new();
    private List<double> tapTimes = new();
    private List<double> tapAngles = new();
    //private int lastEventNum = 0;
    private int prevBeatIndex;
    private int nextBeatIndex;

    public ParameterLoader parameters;
    private List<FishTrialParameters> trials;

    private static string logFilePath = Application.dataPath + "/Data/EventLog.txt";

    public AudioClip bridgeSound;
    public AudioClip tickSound;
    public AudioClip goodHitSound;
    public AudioClip badHitSound;

    public InputActionAsset inputActions;
    private InputAction diveAction;
    private InputAction jumpAction;
    private InputAction cancelAction;
    private InputAction clickAction;

    [SerializeField] private OceanControl OceanWaves;

    

    void Start()
    {
        GameType = "Fish";
        
        gameOver = false;
        gameOverStarted = false;

        trialIsRunning = false;
        score = 0;
        eventCount = 0;

        pause = false;
        
        float distance = Camera.main.orthographicSize;
        screenRightEdge = Camera.main.ViewportToWorldPoint(new Vector3(1, 1, distance)).x;  // Get exact location of screen edge
        spawnLocation = screenRightEdge + 1.5f;  // 1.5 is extra buffer so you don't see the beat appear 
        destroyLocation = -spawnLocation; 
        //Debug.Log("edge is " + screenRightEdge);

        //audioSource = GetComponent<AudioSource>();
        //Wheel.gameObject.SetActive(false);
        //Target.gameObject.SetActive(false);
        fishBeats = new();
        activeBeats = new();

        var gameplayActions = inputActions.FindActionMap(GameType);
        diveAction = gameplayActions.FindAction("Dive");
        jumpAction = gameplayActions.FindAction("Jump");
        cancelAction = gameplayActions.FindAction("Cancel");
        clickAction = gameplayActions.FindAction("Click");

        Color c = BeepLine.GetComponent<SpriteRenderer>().color;
        c.a = beepActive ? 1f : 0f;
        BeepLine.GetComponent<SpriteRenderer>().color = c;
    }

    
    void Update()
    {
        
        if (trialIsRunning)
        {
            currDSPtime = AudioSettings.dspTime - startDSPtime;

            if (nextBeatIndex >= eventMax) 
            {
                EndTrial(false);
            }
            
            SpawnNewBeats(currDSPtime);

            UpdateActiveBeats(currDSPtime);

            RemoveExpiredBeats(currDSPtime);

        }
    }

    #region Session/Trial control

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

        Bounds beatZoneBounds = beatZoneObject.GetComponent<SpriteRenderer>().bounds;
        beatZoneStartX = (beatZoneBounds.center + new Vector3(beatZoneBounds.extents.x, 0, 0)).x;
        beatZoneEndX = (beatZoneBounds.center - new Vector3(beatZoneBounds.extents.x, 0, 0)).x;
        Bounds safeZoneBounds = safeZoneObject.GetComponent<SpriteRenderer>().bounds;
        safeZoneStartX = (safeZoneBounds.center + new Vector3(safeZoneBounds.extents.x, 0, 0)).x;
        safeZoneEndX = (safeZoneBounds.center - new Vector3(safeZoneBounds.extents.x, 0, 0)).x;

        //Target.targetZoneWidth = targetZoneWidth;

        //Wheel.gameObject.SetActive(true);
        //Target.gameObject.SetActive(true);
        cfg.ShowLRS(false);

        // read parameter file
        trials = ParameterLoader.LoadFishTrialParameters(phaseParamPath, sessionFile);

        // Get number of trials
        numTrials = trials.Count;

        currTrial = 0;

        sessionNumber = System.Text.RegularExpressions.Regex.Replace(sessionFile, "[^0-9]", "");  // TODO: Turn into logging the session file name
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
        EventLogger.LogStruct(EventLogItem.GameData(currTime, "Version", Application.version));
        EventLogger.LogStruct(EventLogItem.GameData(currTime, "App", Application.productName));
        EventLogger.LogStruct(EventLogItem.GameData(currTime, "Game Type", GameType));
        EventLogger.LogStruct(EventLogItem.GameData(currTime, "Fixed Timestep Precise", cfg.timeStepPrecise.ToString()));
        float fixedTimestep = TimeUtil.fixedDeltaTime;
        EventLogger.LogStruct(EventLogItem.GameData(currTime, "Fixed Timestep Slow", fixedTimestep.ToString()));
        EventLogger.LogStruct(EventLogItem.SessionData(currTime, "Animal", AnimalName));
        //EventLogger.LogData("Session", "Attention", attentionText);
        EventLogger.LogStruct(EventLogItem.SessionData(currTime, "Presession Notes", cfg.preNotesText));
        EventLogger.LogStruct(EventLogItem.SessionData(currTime, "LRS Duration", LRSDuration.ToString()));
        EventLogger.LogStruct(EventLogItem.SessionData(currTime, "Target Width", targetZoneWidth.ToString()));

        EventLogger.LogStruct(EventLogItem.SessionData(currTime, "Phase", sessionNumber));
        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        EventLogger.LogStruct(EventLogItem.SessionData(currTime, "Session Start", timestamp));

        score = 0;
        trialIsRunning = false;
        eventCount = 0;

        pause = false;

        defaultLives = 3;

        beepPos = BeepLine.transform.position.x;
        boopPos = BoopLine.transform.position.x;
        beepBoopDist = System.Math.Abs(boopPos - beepPos);

        cfg.ShowLevelScore(false);
        cfg.ShowLifeMarkers(false);

        cfg.UpdateMessage("Click to start<br>Phase " + sessionNumber);
        cfg.UpdateStats(""); // clear statsText because no trials have been run yet

        diveAction.performed += OnDivePress;
        jumpAction.performed += OnJumpPress;
        cancelAction.performed += OnEscape;
        clickAction.performed += OnClick;

        //diveAction.Disable();
        //jumpAction.Disable();
        //cancelAction.Enable();

        ActivateInterTrialInputs();

    }

    void StartTrial()
    {
        DeactivateInterTrialInputs();
        //ActivateInputs();
        
        // initiate wheel and eventBoxes
        tempo = trials[currTrial].tempo;
        fishEventListRaw = trials[currTrial].fishEventList;
        eventMax = trials[currTrial].beatMax;
        beepBoopInterval = trials[currTrial].beepBoopTime;
        targetScore = trials[currTrial].targetScore;
        colliderSize = trials[currTrial].colliderSize;
        beatZoneSize = trials[currTrial].beatZoneSize;
        level = trials[currTrial].level;
        eventCount = 0;
        score = 0;
        mistakeCount = 0;
        //lastEventNum = 0;
        prevBeatIndex = -1;
        nextBeatIndex = 0;
        currLives = defaultLives;
        GameController.Instance.ShowLifeMarkers(true);
        UpdateLives(currLives);
        GameController.Instance.UpdateMessage("");
        GameController.Instance.UpdateStats("");
        GameController.Instance.UpdateScore(score);
        GameController.Instance.ShowLevelScore(false);

        //fishManager.colliderSize = colliderSize;
        //fishManager.beatZoneSize = beatZoneSize;

        //fishManager.safeZoneColorDefault = safeZoneColorDefault;
        //fishManager.beatZoneColorDefault = beatZoneColorDefault;
        //player.beatZoneColorDefault = beatZoneColorDefault;
        //Wheel.gameLevel = level;
        //Wheel.Reset();
        //Debug.Break();
        TimeUtil.fixedDeltaTime = GameController.Instance.timeStepPrecise;
        TimeUtil.maximumDeltaTime = GameController.Instance.timeStepPrecise * 3;


        tickTimes.Clear();
        tapTimes.Clear();
        tapAngles.Clear();

        UpdateEventList();

        activeBeats = new();

        // initialize dsp timing tracking and add trial info to log file
        startDSPtime = AudioSettings.dspTime;
        EventLogger.StartTrial(startDSPtime);
        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        EventLogger.LogStruct(EventLogItem.TrialData(startDSPtime, currTrial, "Trial Started", timestamp));

        EventLogger.LogStruct(EventLogItem.TrialData(startDSPtime, currTrial, "Level", trials[currTrial].level.ToString()));
        EventLogger.LogStruct(EventLogItem.TrialData(startDSPtime, currTrial, "Swim Speed", trials[currTrial].tempo.ToString()));
        string eventList = string.Join(", ", trials[currTrial].fishEventList);  //TODO: figure out this calculation to properly log the beat timings
        EventLogger.LogStruct(EventLogItem.TrialData(startDSPtime, currTrial, "Fish Event List", eventList));
        EventLogger.LogStruct(EventLogItem.TrialData(startDSPtime, currTrial, "Max Beats", trials[currTrial].beatMax.ToString()));
        EventLogger.LogStruct(EventLogItem.TrialData(startDSPtime, currTrial, "Target Score", trials[currTrial].targetScore.ToString()));
        EventLogger.LogStruct(EventLogItem.TrialData(startDSPtime, currTrial, "Safe Zone Size", trials[currTrial].colliderSize.ToString()));
        EventLogger.LogStruct(EventLogItem.TrialData(startDSPtime, currTrial, "Beat Zone Size", trials[currTrial].beatZoneSize.ToString()));       

        trialIsRunning = true;

        OceanWaves.PauseWaves(false);

    }

    //void OnAudioFilterRead(float[] data, int channels)
    //{
    //    if (!trialIsRunning)
    //        return;

    //    double samplesPerTick = sampleRate * 60.0F / tempo; // * 4.0F / signatureLo;
    //    double sample = AudioSettings.dspTime * sampleRate;

    //    int dataLen = data.Length / channels;
    //    int n = 0;

    //    while (n < dataLen)
    //    {
    //        //float x = gain * amp * Mathf.Sin(phase);
    //        //int i = 0;
    //        //while (i < channels)
    //        //{
    //        //    data[n * channels + i] += x;
    //        //    i++;
    //        //}
    //        while (sample + n >= nextTick)
    //        {
    //            nextTick += samplesPerTick;
    //            //amp = 1.0F;
    //            //if (++accent > signatureHi)
    //            //{
    //            //    accent = 1;
    //            //    amp *= 2.0F;
    //            //    lastDownBeatTime = AudioSettings.dspTime;

    //            //}

    //            lastBeatTime = AudioSettings.dspTime;

    //            // Debug.Log("Tick: " + accent + "/" + signatureHi);
    //        }
    //        //phase += amp * 0.3F;
    //        //amp *= 0.993F;
            
    //        n++;
    //    }
    //}

    public void PauseGame()
    {
        // LRS has been triggered
        if (pause) return;
        EventLogger.LogStruct(EventLogItem.Feedback(startDSPtime, currTrial, "LRS initiated"));
        pause = true;
        AudioListener.pause = true;  // this pauses the DSPtime as well
        //LRSImage.enabled = true; // Enable the blackout image
        //fishManager.StopSwim();
        //controller.TriggerLRS(duration);
        //scoreText.enabled = false;
        //Invoke(nameof(DisableLRS), duration); // Disable after the duration

    }

    public void ResumeGame()
    {
        // resume after pausing
        if (!pause) return;
        EventLogger.LogStruct(EventLogItem.Feedback(startDSPtime, currTrial, "LRS ended"));
        pause = false;
        //LRSImage.enabled = false; // Disable the blackout image
        AudioListener.pause = false;
        //controller.InGameText.SetActive(true);
        //scoreText.enabled = true;

        //fishManager.StartSwim();

    }

    void EndTrial(bool success = true)
    {
        ActivateInterTrialInputs();

        OceanWaves.PauseWaves(true);

        TimeUtil.fixedDeltaTime = GameController.Instance.timeStepSlow;
        TimeUtil.maximumDeltaTime = GameController.Instance.timeStepSlow * 3;
        trialIsRunning = false;
        EventLogger.LogStruct(EventLogItem.TrialData(AudioSettings.dspTime, currTrial, "Trial Ended"));
        ClearFish();

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

#endregion

    #region Player Actions
    private void OnClick(InputAction.CallbackContext context)
    {
        // Any button pressed - for moving between trials or moving to game over section
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
    }

    private void OnDivePress(InputAction.CallbackContext context)
    {
        
        if (!pause)
        {
            player.Dive();
            double tapTimeRaw = AudioSettings.dspTime;
            double tapTime = tapTimeRaw - startDSPtime;
            tapTimes.Add(tapTime);

            // calculate phase angle of tap
            // Problematic if tapping before first tick - no known time point to determine beat onset
            // But we could get current wheel angle and calculate angle of next beat...
            // On the other hand, can you really argue for the angle of taps before the first tick being meaningful in relation to the beat construct in any way?
            // Maybe if they're ahead of the first tick but close? Then it's a question of accuracy, but still likely before any construct of beat is created 
            
            // first, get index of next beat and previous beat
            
            double tapPhase = GetAngle(tapTime);
            tapAngles.Add(tapPhase);
            
            //tapAngles.Add(tapPhase);
            //EventLogger.LogData("Debug", "Tap Phase", tapPhase.ToString());
            
            int nearestIndex = (tapPhase <= 0) ? nextBeatIndex : prevBeatIndex;
            
            if (nearestIndex < 0)
            {
                EventLogger.LogStruct(EventLogItem.Response(tapTimeRaw, currTrial, 0, "Miss (early)", tapPhase));
            }
            else
            {

            
                // Classify the tap
                BeatEvent nearestBeat = fishBeats[nearestIndex];
                BeatObject nearestBeatObj = activeBeats.Find(beat => beat.beat == nearestBeat);

                float beatLocation = (float)(nearestBeat.spawnX - (tapTime - nearestBeat.spawnTime) * nearestBeat.speed);
                if (beatZoneStartX >= beatLocation && beatZoneEndX <= beatLocation && !nearestBeat.bopped)  // hit in beat zone, score point
                {

                    EventLogger.LogStruct(EventLogItem.Response(tapTimeRaw, currTrial, 0, "Hit", tapPhase));
                    nearestBeat.bopped = true;
                    nearestBeatObj.Eaten();

                    score++;
                    currLives = defaultLives;
                    UpdateLives(currLives);  // reset lives to max

                    // TODO: placeholder for calculating accuracy

                    GameController.Instance.UpdateScore(score);
                

                    if (score < targetScore)
                    {
                        PlayPlayerSound(goodHitSound);
                    }
                    else
                    {
                        PlayPlayerSound(goodHitSound);
                        EndTrial();

                    }

                }
                else if (safeZoneStartX >= beatLocation && safeZoneEndX <= beatLocation && !nearestBeat.bopped)  // hit in safe zone, no score change but no penalty
                {

                    EventLogger.LogStruct(EventLogItem.Response(tapTimeRaw, currTrial, 0, "Safe", tapPhase));
                    nearestBeat.bopped = true;
                    currLives = defaultLives;
                    UpdateLives(currLives);  // reset lives to max
                    //safeZoneObject.transform.Find("BeatZone").GetComponent<Renderer>().material.color = beatZoneColorFade;



                    //PlayPlayerSound(goodHitSound);

                }
                else
                {
                    if ((safeZoneStartX <= beatLocation && safeZoneEndX >= beatLocation) ||
                        beatZoneStartX <= beatLocation && beatZoneEndX >= beatLocation)
                    {
                        // in the safeZone or beatZone but not counted as hit
                        EventLogger.LogStruct(EventLogItem.Response(tapTimeRaw, currTrial, 0, "Miss (already hit)", tapPhase));
                    }
                    else
                    {
                        EventLogger.LogStruct(EventLogItem.Response(tapTimeRaw, currTrial, 0, "Miss", tapPhase));
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
            }
            //if (currLives <= 0)
            //{

            //    TriggerLRS(LRSDuration);
            //    score = 0;
            //    scoreText.SetText(score.ToString());
            //}




        }

    }

    private void OnJumpPress(InputAction.CallbackContext context)
    {
        if (!pause)
        {
            player.Jump();
            //    double tapTime = TimeUtil.fixedTimeAsDouble;
            //    tapTimes.Add(tapTime);

            //    // calculate phase angle of tap
            //    // Problematic if tapping before first tick - no known time point to determine beat onset
            //    // But we could get current wheel angle and calculate angle of next beat...
            //    // On the other hand, can you really argue for the angle of taps before the first tick being meaningful in relation to the beat construct in any way?
            //    // Maybe if they're ahead of the first tick but close? Then it's a question of accuracy, but still likely before any construct of beat is created 
            //    double tapPhase;
            //    if (lastEventNum > 0)
            //    {
            //        // if tap is after first tick
            //        tapPhase = GetAngle(tapTime, tickTimes[^1]);
            //        tapAngles.Add(tapPhase);
            //    }
            //    else
            //    {
            //        // Tap is before first tick, so count as error
            //        //tapPhase = GetAngle(tapTime, -1d);

            //    }
            //    //tapAngles.Add(tapPhase);
            //    //EventLogger.LogData("Debug", "Tap Phase", tapPhase.ToString());

            //    // Classify the tap
            //    if (beatZoneContactAir && !boopedAir)
            //    {
            //        // hit in beat zone, score point
            //        EventLogger.LogData("Response", "Hit");
            //        boopedAir = true;
            //        if (beatZoneObject != null)
            //        {
            //            //beatZoneObject.GetComponent<Renderer>().material.color = beatZoneColorFlash;
            //        }

            //        score++;
            //        currLives = defaultLives;
            //        UpdateLives(currLives);  // reset lives to max

            //        // TODO: placeholder for calculating accuracy

            //        GameController.Instance.UpdateScore(score);
            //        PlayPlayerSound(goodHitSound);

            //        if (score >= targetScore)
            //        {
            //            PlayPlayerSound(bridgeSound);
            //            EndTrial();
            //        }

            //    }
            //    else if (safeZoneContactAir && !boopedAir)
            //    {
            //        // hit in safe zone, no score change
            //        EventLogger.LogData("Response", "Safe");
            //        boopedAir = true;
            //        currLives = defaultLives;
            //        UpdateLives(currLives);  // reset lives to max
            //        //safeZoneObject.transform.Find("BeatZone").GetComponent<Renderer>().material.color = beatZoneColorFade;



            //        //audioSource.PlayOneShot(goodHitSound);

            //    }
            //    else
            //    {
            //        if (safeZoneContactAir || beatZoneContactAir)
            //        {
            //            // in the safeZone or beatZone but not counted as hit
            //            EventLogger.LogData("Response", "Miss (already hit)");
            //        }
            //        else
            //        {
            //            EventLogger.LogData("Response", "Miss");
            //        }

            //        if (score > 0)
            //        {
            //            score = 0;
            //        }

            //        mistakeCount++;
            //        GameController.Instance.UpdateScore(score);

            //        currLives--;
            //        UpdateLives(currLives);

            //        // TODO: placeholder for accuracy update

            //        //audioSource.PlayOneShot(badHitSound, 0.5f);
            //    }

            //    //if (currLives <= 0)
            //    //{

            //    //    TriggerLRS(LRSDuration);
            //    //    score = 0;
            //    //    scoreText.SetText(score.ToString());
            //    //}




        }

    }

    private void OnEscape(InputAction.CallbackContext context)
    {
        EndTrial();
        GameOver();
    }

    #endregion

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
        diveAction.performed -= OnDivePress;
        jumpAction.performed -= OnJumpPress;
        cancelAction.performed -= OnEscape;
        clickAction.performed -= OnClick;
        //ActivateInterTrialInputs();
        player.gameObject.SetActive(false);
        FishParent.SetActive(false);
        GameController.Instance.GameOver();
    }

    #region Beat Control
    public void UpdateEventList()
    {
        fishBeats?.Clear();
        fishBeats = new();
        double secPerBeat = 60f / tempo;
        
        double startDelay = secPerBeat * 1;  // How much to delay the first beat so that time to beep line is not 0. TODO: turn into parameter in session file so we can adjust if needed
        double beepBoopTimePrec = secPerBeat * beepBoopInterval;
        float beatSpeed = (float)(beepBoopDist / beepBoopTimePrec);  // parameter file specifies number of beats between beep and boop lines, and we know the distance
        double beepToSpawnDist = System.Math.Abs(beepPos - spawnLocation);  // 1.5 is extra buffer so you don't see the beat appear 
        double spawnToBeepTime = beepToSpawnDist / beatSpeed;
        double boopToDestroyDist = System.Math.Abs(boopPos - destroyLocation);
        double boopToDestroyTime = boopToDestroyDist / beatSpeed;

        double spawnOnset = 0 + startDelay;  // first spawn time

        int j;
        for (int i = 0; i < eventMax; i++)
        {
            j = i % fishEventListRaw.Length;  // so we can cycle through the raw list repeatedly
            Beat beat = fishEventListRaw[j];
            beat.beatNumber = i;
            double currDuration = beat.beatDuration * secPerBeat;
            spawnOnset += currDuration;  // increment onset time 

            // calculate remaining times from beepOnset and known parameters
            double beepOnset = spawnOnset + spawnToBeepTime;
            double boopOnset = beepOnset + beepBoopTimePrec;  // boop time is fixed number of beats behind beep time
            double destroyTime = boopOnset + boopToDestroyTime;
            


            if (!beat.isRest)
            {
                // only add the beat to the list if it's an actual beat instead of a rest
                BeatEvent currBeat = new()
                {
                    beat = beat,
                    beatType = 0,
                    beatLane = 0,
                    beepTime = beepOnset,
                    boopTime = beepOnset + beepBoopTimePrec,
                    speed = beatSpeed,
                    spawnTime = spawnOnset,
                    destroyTime = destroyTime,
                    spawnX = spawnLocation,

                };
                fishBeats.Add(currBeat);
            }            
        }
        //remainingBeats = fishBeats;
        // Add actual beat duration (IOI) if needed

        //OceanWaves.SetOceanSpeed(beatSpeed);  // if we want ocean speed to vary with fish speed

    }

    void SpawnNewBeats(double currentTime)
    {
        // Spawn any beats that have reached their time and increment the spawned index so we don't have to loop through the whole list every time
        while ( nextBeatCreationIndex < fishBeats.Count && fishBeats[nextBeatCreationIndex].spawnTime <= currentTime)
        {
            BeatObject newObject = Instantiate(fishPrefab, FishParent.transform);
            //newObject.name = "EventBox_" + nextBeatCreationIndex.ToString();
            newObject.Initialize(fishBeats[nextBeatCreationIndex]);
            newObject.Boop += BeatContact;
            
            if (beepActive)
            {
                double beepTime = newObject.beat.beepTime + startDSPtime; 
                ScheduleBoopAudio(newObject.beatLane, beepTime);
                newObject.Beep += BeepContact;
            }
            double boopTime = newObject.beat.boopTime + startDSPtime;
            ScheduleBoopAudio(newObject.beatLane, boopTime);

            activeBeats.Add(newObject);

            nextBeatCreationIndex++;
        }

    }

    void UpdateActiveBeats(double currentTime)
    {
        foreach (var activeBeat in activeBeats)
        {
            activeBeat.UpdatePosition(currentTime);

            activeBeat.CheckTriggers(currentTime);  // convert into a function that returns state (beep, boop, none, etc)
        }
    }

    void RemoveExpiredBeats(double currentTime)
    {
        for (int i = activeBeats.Count - 1; i >= 0; i--)  // iterate backwards so we don't have problems with deleting elements while reading the list and messing with indices
        { 
            if (activeBeats[i].IsExpired(currentTime))
            {
                activeBeats[i].Boop -= BeatContact;
                activeBeats[i].Beep -= BeepContact;
                Destroy(activeBeats[i].gameObject);
                activeBeats.RemoveAt(i);
            }
        }
    }
    
    public void ClearFish()
    {
        audioManager.StopAll();
        
        for (int i = activeBeats.Count - 1; i >= 0; i--)  // iterate backwards so we don't have problems with deleting elements while reading the list and messing with indices
        {
            activeBeats[i].Boop -= BeatContact;
            activeBeats[i].Beep -= BeepContact;
            Destroy(activeBeats[i].gameObject);
            activeBeats.RemoveAt(i);
        }
    }

    #endregion

    void ScheduleBoopAudio(int beatLane, double dspTime)
    {

        if (beatLane == 0)
        {
            audioManager.ScheduleBeatL(tickSound, dspTime);
        } 
        else if (beatLane == 1)
        {
            audioManager.ScheduleBeatH(tickSound, dspTime);
        }
    }

    void PlayPlayerSound(AudioClip clip)
    {
        audioManager.PlayImmediate(clip);
    }

    public double GetAngle(double tapTime)
    {
        // Since we'll know the exact onset times of all beats, we can calculate the next and previous beat times from the raw data instead of relying on an additional parameter

        double nextTick = fishBeats[nextBeatIndex].boopTime;
        double prevTick;
        if (prevBeatIndex < 0)  // for taps that happen before the first beat boops
        {
            double secPerBeat = 60f / tempo;
            double currDuration = fishBeats[0].beat.beatDuration * secPerBeat;
            prevTick = nextTick - currDuration;
        }
        else
        {
            prevTick = fishBeats[prevBeatIndex].boopTime;
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

    #region Inputs and triggers
    
    void ActivateInterTrialInputs()
    {
        diveAction.Disable();
        jumpAction.Disable();
        clickAction.Enable();
    }

    void DeactivateInterTrialInputs()
    {
        diveAction.Enable();
        jumpAction.Enable();
        clickAction.Disable();
    }

    void ActivateInputs()
    {
        //Debug.Log("Trigger triggered!");
        //TargetControl.OnContactStart += WindowContactOn;
        //TargetControl.OnContactEnd += WindowContactOff;
        //TargetControl.OnBeatZoneStart += BeatZoneContactOn;
        //TargetControl.OnBeatZoneEnd += BeatZoneContactOff;


        

        //triggerAction.performed += OnClick;
        //triggerAction.Enable();

        //diveAction.performed += OnDivePress;
        //diveAction.performed += OnClick;
        diveAction.Enable();

        //jumpAction.performed += OnJumpPress;
        //jumpAction.performed += OnClick;
        jumpAction.Enable();

        //cancelAction.performed += OnEscape;
        cancelAction.Enable();

        //beep
    }

    void DeactivateInputs()
    {
        //Debug.Log("Trigger off");
        //TargetControl.OnContactStart -= WindowContactOn;
        //TargetControl.OnContactEnd -= WindowContactOff;
        //TargetControl.OnBeatZoneStart -= BeatZoneContactOn;
        //TargetControl.OnBeatZoneEnd -= BeatZoneContactOff;
        //BeatTicker.OnBeatContact -= BeatContact;

        //if (clickAction != null)
        //{
        //    clickAction.performed -= OnDivePress;
        //    clickAction.Disable();
        //}

        if (diveAction != null)
        {
            diveAction.performed -= OnDivePress;
            //diveAction.performed -= OnClick;
            diveAction.Disable();
        }

        if (jumpAction != null)
        {
            jumpAction.performed -= OnJumpPress;
            //jumpAction.performed -= OnClick;
            jumpAction.Disable();
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

    //private void WindowContactOn()
    //{
    //    EventLogger.LogData("Beat", "Beat safe window start");
    //    eventCount++;

    //    //safeZoneObject = player.safeZone;
    //    //safeZoneContactWater = true;
    //    boopedWater = false;

    //}

    //private void WindowContactOff()
    //{
    //    EventLogger.LogData("Beat", "Beat safe window end");
    //    //safeZoneContactWater = false;
    //    if (!boopedWater)  // If beat passes without a tap, reset score
    //    {
    //        if (score > 0)
    //        {
    //            score = 0;
    //            GameController.Instance.UpdateScore(score);
    //        }
    //    }
    //    //Wheel.ResetBoxColors();  // Reset all EventBox pieces to their default colors, just in case one got colored weird for some reason
    //}

    private void BeatContact(BeatEvent beat)
    {
        double timeRaw = AudioSettings.dspTime;
        EventLogger.LogStruct(EventLogItem.Beat(timeRaw, beat.boopTime, currTrial, beat.beat.beatNumber, beat.beatLane, "Boop tick"));
        
        prevBeatIndex++;
        nextBeatIndex++;
        
    }

    private void BeepContact(BeatEvent beat)
    {
        double timeRaw = AudioSettings.dspTime;
        EventLogger.LogStruct(EventLogItem.Beat(timeRaw, beat.beepTime, currTrial, beat.beat.beatNumber, beat.beatLane, "Beep tick"));

    }

    //private void BeatZoneContactOn()
    //{
    //    EventLogger.LogData("Beat", "Beat zone start");
    //    beatZoneContactWater = true;
    //    //beatZoneObject = player.beatZone;
    //}

    //private void BeatZoneContactOff()
    //{
    //    EventLogger.LogData("Beat", "Beat zone end");
    //    beatZoneContactWater = false;
    //}
    #endregion
}
