using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.VisualScripting;
using UnityEditor.Timeline.Actions;
using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEngine.GraphicsBuffer;
using TimeUtil = UnityEngine.Time;

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
    private float screenRightEdge;
    private float spawnLocation;
    private float destroyLocation;

    // audio timing vars
    private double startDSPtime;
    private double currDSPtime;
    private double beepBoopInterval;
    private int nextBeatIndex;
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
    private bool boopedWater;  // whether the current eventBox has been hit
    private bool boopedAir;  // whether the current eventBox has been hit
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
    private bool safeZoneContactWater; // whether target is touching an eventBox
    private bool beatZoneContactWater; // whether target is touching center of eventBox
    //private bool safeZoneContactAir; // whether target is touching an eventBox
    //private bool beatZoneContactAir; // whether target is touching center of eventBox

    private Beat[] fishEventListRaw;
    private List<BeatEvent> fishBeats;  // beat onsets for current trial
    private List<BeatObject> activeBeats;
    private List<BeatEvent> remainingBeats;
    private List<BeatEvent> birdBeats;  // beat onsets for current trial
    private List<double> tickTimes = new();
    private List<double> tapTimes = new();
    private List<double> tapAngles = new();
    private int lastEventNum = 0;

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

    

    void Start()
    {
        GameType = "Fish";
        
        gameOver = false;
        gameOverStarted = false;

        trialIsRunning = false;
        score = 0;
        eventCount = 0;
        boopedWater = false;
        boopedAir = false;
        pause = false;
        
        float distance = Camera.main.orthographicSize;
        screenRightEdge = Camera.main.ViewportToWorldPoint(new Vector3(1, 1, distance)).x;  // Get exact location of screen edge
        spawnLocation = screenRightEdge + 1.5f;  // 1.5 is extra buffer so you don't see the beat appear 
        destroyLocation = -spawnLocation; 
        //Debug.Log("edge is " + screenRightEdge);

        audioSource = GetComponent<AudioSource>();
        //Wheel.gameObject.SetActive(false);
        //Target.gameObject.SetActive(false);
        fishBeats = new();
        activeBeats = new();

    }

    
    void Update()
    {
        
        if (trialIsRunning && eventCount >= eventMax)
        {
            EndTrial(false);
        }
        if (trialIsRunning)
        {
            currDSPtime = AudioSettings.dspTime - startDSPtime;

            SpawnNewBeats(currDSPtime);

            UpdateActiveBeats(currDSPtime);

            RemoveExpiredBeats(currDSPtime);
            

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

        //Target.targetZoneWidth = targetZoneWidth;

        //Wheel.gameObject.SetActive(true);
        //Target.gameObject.SetActive(true);
        cfg.ShowLRS(false);

        // read parameter file
        trials = ParameterLoader.LoadFishTrialParameters(phaseParamPath, sessionFile);

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
        EventLogger.LogEvent("Game", "App", Application.productName);
        EventLogger.LogEvent("Game", "Game", GameType);
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
        boopedWater = false;
        boopedAir = false;
        pause = false;

        defaultLives = 3;

        beepPos = BeepLine.transform.position.x;
        boopPos = BoopLine.transform.position.x;
        beepBoopDist = System.Math.Abs(boopPos - beepPos);

        cfg.ShowLevelScore(false);
        cfg.ShowLifeMarkers(false);

        cfg.UpdateMessage("Click to start<br>Phase " + sessionNumber);
        cfg.UpdateStats(""); // clear statsText because no trials have been run yet

        ActivateInputs();



    }

    void StartTrial()
    {
        // store trial info in data file
        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        EventLogger.LogEvent("Trial", "Trial " + (currTrial + 1) + " started", timestamp);

        EventLogger.LogEvent("Trial Param", "Level", trials[currTrial].level.ToString());
        EventLogger.LogEvent("Trial Param", "Swim Speed", trials[currTrial].tempo.ToString());
        string eventList = string.Join(", ", trials[currTrial].fishEventList);  //TODO: figure out this calculation to properly log the beat timings
        EventLogger.LogEvent("Trial Param", "Fish Event List", eventList);
        EventLogger.LogEvent("Trial Param", "Max Beats", trials[currTrial].beatMax.ToString());
        EventLogger.LogEvent("Trial Param", "Target Score", trials[currTrial].targetScore.ToString());
        EventLogger.LogEvent("Trial Param", "Safe Zone Size", trials[currTrial].colliderSize.ToString());
        EventLogger.LogEvent("Trial Param", "Beat Zone Size", trials[currTrial].beatZoneSize.ToString());

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
        lastEventNum = 0;
        currLives = defaultLives;
        GameController.Instance.ShowLifeMarkers(true);
        UpdateLives(currLives);
        GameController.Instance.UpdateMessage("");
        GameController.Instance.UpdateStats("");
        GameController.Instance.UpdateScore(score);
        GameController.Instance.ShowLevelScore(false);
        trialIsRunning = true;
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

        // initialize dsp timing tracking
        startDSPtime = AudioSettings.dspTime;
        UpdateEventList();

        activeBeats = new();

      
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
        EventLogger.LogEvent("Feedback", "LRS initiated");
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
        EventLogger.LogEvent("Feedback", "LRS ended");
        pause = false;
        //LRSImage.enabled = false; // Disable the blackout image
        AudioListener.pause = false;
        //controller.InGameText.SetActive(true);
        //scoreText.enabled = true;

        //fishManager.StartSwim();

    }

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
            double tapTime = AudioSettings.dspTime;
            tapTimes.Add(tapTime);

            // calculate phase angle of tap
            // Problematic if tapping before first tick - no known time point to determine beat onset
            // But we could get current wheel angle and calculate angle of next beat...
            // On the other hand, can you really argue for the angle of taps before the first tick being meaningful in relation to the beat construct in any way?
            // Maybe if they're ahead of the first tick but close? Then it's a question of accuracy, but still likely before any construct of beat is created 
            
            // first, get index of next beat and previous beat
            
            double tapPhase;
            if (lastEventNum > 0)
            {
                // if tap is after first tick
                tapPhase = GetAngle(tapTime);
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
            if (beatZoneContactWater && !boopedWater)  // hit in beat zone, score point
            {
                
                EventLogger.LogEvent("Response", "Hit");
                boopedWater = true;
                if (beatZoneObject != null)
                {
                    //beatZoneObject.GetComponent<Renderer>().material.color = beatZoneColorFlash;
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
            else if (safeZoneContactWater && !boopedWater)  // hit in safe zone, no score change but no penalty
            {
                
                EventLogger.LogEvent("Response", "Safe");
                boopedWater = true;
                currLives = defaultLives;
                UpdateLives(currLives);  // reset lives to max
                //safeZoneObject.transform.Find("BeatZone").GetComponent<Renderer>().material.color = beatZoneColorFade;



                //audioSource.PlayOneShot(goodHitSound);

            }
            else
            {
                if (safeZoneContactWater || beatZoneContactWater)
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
        //    //EventLogger.LogEvent("Debug", "Tap Phase", tapPhase.ToString());

        //    // Classify the tap
        //    if (beatZoneContactAir && !boopedAir)
        //    {
        //        // hit in beat zone, score point
        //        EventLogger.LogEvent("Response", "Hit");
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
        //        audioSource.PlayOneShot(goodHitSound);

        //        if (score >= targetScore)
        //        {
        //            audioSource.PlayOneShot(bridgeSound);
        //            EndTrial();
        //        }

        //    }
        //    else if (safeZoneContactAir && !boopedAir)
        //    {
        //        // hit in safe zone, no score change
        //        EventLogger.LogEvent("Response", "Safe");
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
        //            EventLogger.LogEvent("Response", "Miss (already hit)");
        //        }
        //        else
        //        {
        //            EventLogger.LogEvent("Response", "Miss");
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

    void EndTrial(bool success = true)
    {
        //fishManager.StopSwim();
        TimeUtil.fixedDeltaTime = GameController.Instance.timeStepSlow;
        TimeUtil.maximumDeltaTime = GameController.Instance.timeStepSlow * 3;
        trialIsRunning = false;
        EventLogger.LogEvent("Trial", "Trial " + (currTrial + 1) + " ended");
        ClearFish();
        //fishManager.Clear();
        //Wheel.Resize();
        beatZoneContactWater = false;
        safeZoneContactWater = false;
        //beatZoneContactAir = false;
        //safeZoneContactAir = false;
        boopedWater = false;
        boopedAir = false;

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

        player.gameObject.SetActive(false);
        FishParent.SetActive(false);
        GameController.Instance.GameOver();
    }

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
            Beat beatObject = fishEventListRaw[j];
            beatObject.beatNumber = j;
            double currDuration = beatObject.beatDuration * secPerBeat;
            spawnOnset += currDuration;  // increment onset time 

            // calculate remaining times from beepOnset and known parameters
            double beepOnset = spawnOnset + spawnToBeepTime;
            double boopOnset = beepOnset + beepBoopTimePrec;  // boop time is fixed number of beats behind beep time
            double destroyTime = boopOnset + boopToDestroyTime;
            


            if (!beatObject.isRest)
            {
                // only add the beat to the list if it's an actual beat instead of a rest
                BeatEvent currBeat = new()
                {
                    beat = beatObject,
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
    }

    void SpawnNewBeats(double currentTime)
    {
        // Spawn any beats that have reached their time and increment the spawned index so we don't have to loop through the whole list every time
        while ( nextBeatIndex < fishBeats.Count && fishBeats[nextBeatIndex].spawnTime <= currentTime)
        {
            BeatObject newObject = Instantiate(fishPrefab, FishParent.transform);
            //newObject.name = "EventBox_" + nextBeatIndex.ToString();
            newObject.Initialize(fishBeats[nextBeatIndex]);
            double boopTime = newObject.beat.boopTime + startDSPtime;
            ScheduleBoopAudio(newObject.beatLane, boopTime);
            //newObject.Be
            activeBeats.Add(newObject);

            nextBeatIndex++;
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
                //activeBeats[i].beat.
                Destroy(activeBeats[i].gameObject);
                activeBeats.RemoveAt(i);
            }
        }
    }
    
    public void ClearFish()
    {

    }

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
        // Important to note this is approximate - we're guessing when the next beat will happen based on math, but can't be 100% certain due to many points of variability
        // TODO: revise for getting next fishManager position in terms of angle
        // Since we'll know the exact onset times of all beats, we can calculate the next and previous beat times from the raw data instead of relying on an additional parameter

        double nextTick = fishBeats[lastEventNum + 1].boopTime;
        double prevTick;
        if (tapTime < fishBeats[0].boopTime)
        {
            //// Special case when the tap occurs before any tick, so we have to estimate both the previous tick time and the next tick time
            //// Calculate next tick time using wheel angle and speed (first tick is always at 0 degrees)
            prevTick = fishBeats[lastEventNum].boopTime;
            //double wheelAngle = fishManager.GetNextBeat();
            //// To start the wheel is usually rotated a bit before the first tick, so rotation just below 360. Convert to 
            //if (wheelAngle > 270.0)
            //{
            //    wheelAngle = 360.0 - wheelAngle;
            //}
            //nextTick = tapTime + wheelAngle / (fishManager.fishRate * 360.0);
            ////EventLogger.LogEvent("Debug", "Next Tick", nextTick.ToString());
            //prevTick = nextTick - fishManager.fishEventList[lastEventNum] / (fishManager.fishRate * fishManager.SumArray(fishManager.fishEventList));
        }
        else
        {
            //// Get which interval we're on - we can tell which eventBox was the most recent since it's stored in lastEventNum
            //// For taps after the first tick, tested with following parameters: timestep = 0.004, tempo = 0.25, pattern 1,1,1,1, beat zone size = 2
            //// All predicted phases were smaller than 1x10E-6
            //nextTick = prevTick + fishManager.fishEventList[lastEventNum - 1] / (fishManager.fishRate * fishManager.SumArray(fishManager.fishEventList));
            prevTick = fishBeats[lastEventNum].boopTime;
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

        var gameplayActions = inputActions.FindActionMap(GameType);
        diveAction = gameplayActions.FindAction("Dive");
        jumpAction = gameplayActions.FindAction("Jump");
        cancelAction = gameplayActions.FindAction("Cancel");

        //triggerAction.performed += OnClick;
        //triggerAction.Enable();

        diveAction.performed += OnDivePress;
        diveAction.performed += OnClick;
        diveAction.Enable();

        jumpAction.performed += OnJumpPress;
        jumpAction.performed += OnClick;
        jumpAction.Enable();

        cancelAction.performed += OnEscape;
        cancelAction.Enable();

        //beep
    }

    void DeactivateInputs()
    {
        //Debug.Log("Trigger off");
        TargetControl.OnContactStart -= WindowContactOn;
        TargetControl.OnContactEnd -= WindowContactOff;
        TargetControl.OnBeatZoneStart -= BeatZoneContactOn;
        TargetControl.OnBeatZoneEnd -= BeatZoneContactOff;
        BeatTicker.OnBeatContact -= BeatContact;

        //if (clickAction != null)
        //{
        //    clickAction.performed -= OnDivePress;
        //    clickAction.Disable();
        //}

        if (diveAction != null)
        {
            diveAction.performed -= OnDivePress;
            diveAction.performed -= OnClick;
            diveAction.Disable();
        }

        if (jumpAction != null)
        {
            jumpAction.performed -= OnJumpPress;
            jumpAction.performed -= OnClick;
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

    private void WindowContactOn()
    {
        EventLogger.LogEvent("Beat", "Beat safe window start");
        eventCount++;

        safeZoneObject = player.safeZone;
        safeZoneContactWater = true;
        boopedWater = false;

    }

    private void WindowContactOff()
    {
        EventLogger.LogEvent("Beat", "Beat safe window end");
        safeZoneContactWater = false;
        if (!boopedWater)  // If beat passes without a tap, reset score
        {
            if (score > 0)
            {
                score = 0;
                GameController.Instance.UpdateScore(score);
            }
        }
        //Wheel.ResetBoxColors();  // Reset all EventBox pieces to their default colors, just in case one got colored weird for some reason
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
        beatZoneContactWater = true;
        beatZoneObject = player.beatZone;
    }

    private void BeatZoneContactOff()
    {
        EventLogger.LogEvent("Beat", "Beat zone end");
        beatZoneContactWater = false;
    }
}
