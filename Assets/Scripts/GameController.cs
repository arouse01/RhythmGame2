using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using UnityEngine.InputSystem;
using TimeUtil = UnityEngine.Time;

/*
Sounds:
    goodHitSound - ding.wav, Audacity
        Track 1: Generate>Risset Drum, 1760.0 Hz Frequency, Decay 0.2s, Center freq 100 Hz, Width 100 Hz, Noise mix 0, Amplitude 0.8
        Track 2: Generate>Tone, 880 Hz, amplitude 0.2, duration 0.2s
        Select all, Effect>Fade Out
        Effect>Amplify, New Peak Amplitude of -15 dB
    tickSound - tick.wav, Audacity
        Track 1: Generate>Tone, 2000 Hz sine wave, amplitude 1.0, duration 0.025s
        Track 2: Generate>Tone, 3200 Hz sine wave, amplitude 0.1, duration 0.025s long
        Track 3: Generate>Tone, 4400 Hz sine wave, amplitude 0.1, duration 0.025s long
        Track 4: Generate>Tone, 4600 Hz sine wave, amplitude 0.1, duration 0.025s long
        Mix tracks 2-4 and Effect>Amplify, New Peak Amplitude of -15 dB
        Select all tracks from 0.007s to end and Effect>Fade Out
    bridgeSound - Xilo_1_a.wav, received from Kelley Winship as preexisting bridge sound

*/

public class GameController : MonoBehaviour
{
    public float timeStepPrecise;
    public float timeStepSlow = 0.02f;
    
    public WheelControl Wheel;
    public TargetControl Target;
    public Image LRSImage;
    public LevelScore LevelScore;

    public TextMeshProUGUI versionText;

    public GameObject UserInputObject;
    public GameObject gameOverPanel;
    public GameObject playerInfoField;
    public GameObject attentionField;
    public GameObject postNotesField;

    public GameObject preGamePanel;
    public GameObject AnimalField;
    public GameObject preNotesField;
    public GameObject LRSDurationField;
    public GameObject TargetWidthField;

    public GameObject InGameText;
    private TextMeshProUGUI scoreText;
    private TextMeshProUGUI messageText;
    private TextMeshProUGUI statsText;
    private GameObject levelScoreObject;
    private GameObject lifeMarkers;
    public ParameterLoader parameters;

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
    private bool gameOver;  // game is over, move to user input
    private bool gameOverStarted;  // gameover process started

    private List<double> tickTimes = new();
    private List<double> tapTimes = new();
    private List<double> tapAngles = new();
    private int lastEventNum = 0;

    private TextMeshProUGUI Life1Marker;
    private TextMeshProUGUI Life2Marker;
    private TextMeshProUGUI Life3Marker;

    private static string logFilePath = Application.dataPath + "/Data/EventLog.txt";

    public InputActionAsset inputActions;
    private InputAction triggerAction;
    private InputAction cancelAction;

    void Start()
    {
        TimeUtil.fixedDeltaTime = timeStepSlow;
        TimeUtil.maximumDeltaTime = timeStepSlow * 3;
        Application.targetFrameRate = 60;
        LRSImage.enabled = false; // Disable the LRS image to start
        versionText.text = "Version " + Application.version;  // Update the version number on the home screen
        //gameOver = false;
        //gameOverStarted = false;
        //gameOverPanel.SetActive(false);
        //trialIsRunning = false;
        //score = 0;
        //eventCount = 0;
        //booped = false;
        //pause = false;
        scoreText = InGameText.transform.Find("Score Text").GetComponent<TextMeshProUGUI>();
        messageText = InGameText.transform.Find("Message Text").GetComponent<TextMeshProUGUI>();
        statsText = InGameText.transform.Find("LevelStats Text").GetComponent<TextMeshProUGUI>();
        lifeMarkers = InGameText.transform.Find("Life Markers").gameObject;
        Life1Marker = lifeMarkers.transform.Find("Life 1").GetComponent<TextMeshProUGUI>();
        Life2Marker = lifeMarkers.transform.Find("Life 2").GetComponent<TextMeshProUGUI>();
        Life3Marker = lifeMarkers.transform.Find("Life 3").GetComponent<TextMeshProUGUI>();
        levelScoreObject = InGameText.transform.Find("Level Score").gameObject;
        audioSource = GetComponent<AudioSource>();
        parameters.LoadSessionParameters("parameters.txt");
        LRSDurationField.GetComponent<TMPro.TMP_InputField>().text = parameters.LRSDuration.ToString();
        TargetWidthField.GetComponent<TMPro.TMP_InputField>().text = parameters.targetZoneWidth.ToString();
        Wheel.gameObject.SetActive(false);
        Target.gameObject.SetActive(false);

        beatZoneColorFade = beatZoneColorDefault;
        beatZoneColorFade.a = .5f;


        GameStart();

        
    }

    // Update is called once per frame
    void Update()
    {
        if (trialIsRunning && eventCount >= eventMax)
        {
            EndTrial(false);
        }
        
    }

    void GameStart()
    {
        gameOver = false;
        gameOverStarted = false;
        gameOverPanel.SetActive(false);
        UserInputObject.SetActive(true);
        preGamePanel.SetActive(true);
        InGameText.SetActive(false);
    }

    public void StartSession(string sessionFile)
    {
        string AnimalName = AnimalField.GetComponent<TMP_InputField>().text;
        //string attentionText = attentionField.GetComponent<TMP_InputField>().text;
        string preNotesText = preNotesField.GetComponent<TMP_InputField>().text;

        //string AnimalName = parameters.AnimalName;

        preGamePanel.SetActive(false);
        UserInputObject.SetActive(false);
        InGameText.SetActive(true);

        Wheel.gameObject.SetActive(true);
        Target.gameObject.SetActive(true);

        // read parameter file
        parameters.LoadTrialParameters(sessionFile);
        parameters.AnimalName = AnimalName;
        // Get number of trials
        numTrials = parameters.trials.Length;

        currTrial = 0;

        // LRSDuration = parameters.LRSDuration;
        LRSDuration = float.Parse(LRSDurationField.GetComponent<TMPro.TMP_InputField>().text);
        //LRSThresh = parameters.LRSThresh;
        //targetZoneWidth = float.Parse(TargetWidthField.GetComponent<TMPro.TMP_InputField>().text);
        targetZoneWidth = parameters.targetZoneWidth;

        Target.targetZoneWidth = targetZoneWidth;

        sessionNumber = System.Text.RegularExpressions.Regex.Replace(sessionFile, "[^0-9]", "");
        //Target.InitializeTarget();

        //// create log file
        System.DateTime currentTime = System.DateTime.Now;
        string currDate = currentTime.ToString("yyyyMMddHHmmss");

        //// Format the date and time to include milliseconds
        //string timeWithMilliseconds = currentTime.ToString("yyyy-MM-dd HH:mm:ss.fff");

        string logFileName = parameters.AnimalName + "_" + currDate + ".txt";
        string logFileFolder = Path.Combine(Application.dataPath, "SessionData", parameters.AnimalName);
        logFilePath = Path.Combine(logFileFolder, logFileName);
        if (!Directory.Exists(logFileFolder))
        {
            Directory.CreateDirectory(logFileFolder);
        }
        EventLogger.SetLogFilePath(logFilePath);
        EventLogger.StartLog();
        EventLogger.LogEvent("Game", "Version", Application.version);
        EventLogger.LogEvent("Game", "Fixed Timestep Precise", timeStepPrecise.ToString());
        float fixedTimestep = TimeUtil.fixedDeltaTime;
        EventLogger.LogEvent("Game", "Fixed Timestep Slow", fixedTimestep.ToString());
        EventLogger.LogEvent("Session", "Animal", AnimalName);
        //EventLogger.LogEvent("Session", "Attention", attentionText);
        EventLogger.LogEvent("Session", "Presession Notes", preNotesText);
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

        levelScoreObject.SetActive(false);
        lifeMarkers.SetActive(false);

        messageText.SetText("Click to start<br>Phase " + sessionNumber);
        statsText.SetText(""); // clear statsText because no trials have been run yet
        Wheel.StopSpin();

        ActivateInputs();
    }

    void StartTrial()
    {
        // store trial info in data file
        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        EventLogger.LogEvent("Trial", "Trial " + (currTrial + 1) + " started", timestamp);

        EventLogger.LogEvent("Trial Param", "Level", parameters.trials[currTrial].level.ToString());
        EventLogger.LogEvent("Trial Param", "Wheel Tempo", parameters.trials[currTrial].wheelSpeed.ToString());
        string eventList = string.Join(", ", parameters.trials[currTrial].eventList);
        EventLogger.LogEvent("Trial Param", "Event List", eventList);
        EventLogger.LogEvent("Trial Param", "Max Beats", parameters.trials[currTrial].beatMax.ToString());
        EventLogger.LogEvent("Trial Param", "Target Score", parameters.trials[currTrial].targetScore.ToString());
        EventLogger.LogEvent("Trial Param", "Safe Zone Size", parameters.trials[currTrial].colliderSize.ToString());
        EventLogger.LogEvent("Trial Param", "Beat Zone Size", parameters.trials[currTrial].beatZoneSize.ToString());
        
        // initiate wheel and eventBoxes
        Wheel.wheelTempo = parameters.trials[currTrial].wheelSpeed;
        Wheel.eventList = parameters.trials[currTrial].eventList;
        eventMax = parameters.trials[currTrial].beatMax;
        targetScore = parameters.trials[currTrial].targetScore;
        colliderSize = parameters.trials[currTrial].colliderSize;
        beatZoneSize = parameters.trials[currTrial].beatZoneSize;
        level = parameters.trials[currTrial].level;
        eventCount = 0;
        score = 0;
        mistakeCount = 0;
        lastEventNum = 0;
        currLives = defaultLives;
        lifeMarkers.SetActive(true);
        UpdateLives();
        messageText.SetText("");
        statsText.SetText("");
        UpdateScore();
        levelScoreObject.SetActive(false);
        trialIsRunning = true;
        Wheel.colliderSize = colliderSize;
        Wheel.beatZoneSize = beatZoneSize;
        Wheel.safeZoneColorDefault = safeZoneColorDefault;
        Wheel.beatZoneColorDefault = beatZoneColorDefault;
        Target.beatZoneColorDefault = beatZoneColorDefault;
        Wheel.gameLevel = level;
        Wheel.Reset();
        //Debug.Break();
        TimeUtil.fixedDeltaTime = timeStepPrecise;
        TimeUtil.maximumDeltaTime = timeStepPrecise * 3;

        tickTimes.Clear();
        tapTimes.Clear();
        tapAngles.Clear();
        
        Wheel.StartSpin();


    }

    void EndTrial(bool success=true)
    {
        Wheel.StopSpin();
        TimeUtil.fixedDeltaTime = timeStepSlow;
        TimeUtil.maximumDeltaTime = timeStepSlow * 3;
        trialIsRunning = false;
        EventLogger.LogEvent("Trial", "Trial " + (currTrial + 1) + " ended");
        Wheel.Clear();
        Wheel.Resize();
        beatZoneContact = false;
        safeZoneContact = false;
        booped = false;
        
        // pause so the score screen doesn't get skipped
        pause = true;
        
        lifeMarkers.SetActive(false);
        
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
    
    void GameOver()
    {
        DeactivateInputs();
        InGameText.SetActive(false);
        gameOverStarted = true;
        UserInputObject.SetActive(true);
        gameOverPanel.SetActive(true);
        Wheel.gameObject.SetActive(false);
        Target.gameObject.SetActive(false);
    }

    public void GameOverFinish()
    {
        //GameObject playerInfoField = gameOverPanel.transform.Find("PlayerInfoField").gameObject;
        //GameObject attentionField = gameOverPanel.transform.Find("AttentionField").gameObject;
        //GameObject postNotesField = gameOverPanel.transform.Find("postNotesField").gameObject;

        string playerInfoText = playerInfoField.GetComponent<TMP_InputField>().text;
        string attentionText = attentionField.GetComponent<TMP_InputField>().text;
        string generalNotesText = postNotesField.GetComponent<TMP_InputField>().text;
        EventLogger.LogEvent("Session", "Player Information", playerInfoText);
        EventLogger.LogEvent("Session", "Attention", attentionText);
        EventLogger.LogEvent("Session", "Postsession Notes", generalNotesText);

        gameOverPanel.SetActive(false);
        EventLogger.StopLog();
        GameStart();
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
            messageText.SetText("Click to start<br>Trial " + (currTrial + 1).ToString());
        }
        else
        {
            messageText.SetText("Game Over");
            gameOver = true;
        }
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
                    UpdateLives();

                    // TODO: placeholder for calculating accuracy

                    UpdateScore();
                    audioSource.PlayOneShot(goodHitSound);
                    
                    if (score >= targetScore)
                    {
                        audioSource.PlayOneShot(bridgeSound);
                        EndTrial();
                    }

                }
                else if(safeZoneContact && !booped)
                {
                    // hit in safe zone, no score change
                    EventLogger.LogEvent("Response", "Safe");
                    booped = true;
                    currLives = defaultLives;
                    UpdateLives();  // reset lives to max
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
                    UpdateScore();

                    currLives--;
                    UpdateLives();
                    
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


    public void TriggerLRS(float duration)
    {
        if (LRSImage != null)
        {
            EventLogger.LogEvent("Feedback", "LRS initiated");
            pause = true;
            LRSImage.enabled = true; // Enable the blackout image
            Wheel.StopSpin();
            InGameText.SetActive(false);
            //scoreText.enabled = false;
            Invoke(nameof(DisableLRS), duration); // Disable after the duration
        }
    }

    void DisableLRS()
    {
        if (LRSImage != null)
        {
            EventLogger.LogEvent("Feedback", "LRS ended");
            pause = false;
            LRSImage.enabled = false; // Disable the blackout image

            InGameText.SetActive(true);
            //scoreText.enabled = true;
            Wheel.StartSpin();
        }
    }

    void UpdateLives()
    {
        switch (currLives)
        {
            case 0:
                TriggerLRS(LRSDuration);
                currLives = defaultLives;
                UpdateLives();
                break;
            case 1:
                Life3Marker.color = new Color(0, 0, 0, 255);
                Life2Marker.color = new Color(0, 0, 0, 255);
                Life1Marker.color = new Color(255, 255, 255, 255);
                break;
            case 2:
                Life3Marker.color = new Color(0, 0, 0, 255);
                Life2Marker.color = new Color(255, 255, 255, 255);
                Life1Marker.color = new Color(255, 255, 255, 255);
                break;
            case 3:
                Life3Marker.color = new Color(255, 255, 255, 255); 
                Life2Marker.color = new Color(255, 255, 255, 255);
                Life1Marker.color = new Color(255, 255, 255, 255);
                break;
            default:
                break;
        }
    }

    void UpdateScore()
    {
        scoreText.SetText(score.ToString());
    }
    
    void UpdateLevelScore(bool success=true)
    {
        double? meanAngle;  //? makes the double nullable
        double vecLength;
        if (tapAngles.Count > 0)
        {
            meanAngle = CircMean(tapAngles, returnRad: false); 
            vecLength = CircVectorLength(tapAngles);
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
            messageText.SetText("Maximum beats exceeded");
        }

        //statsText.SetText($"θ = {meanAngle:0.00}<br>r = {vecLength:0.00}");

        scoreText.SetText("Mistakes: " + (mistakeCount).ToString());
        levelScoreObject.SetActive(true);
        LevelScore.ShowStars(numStars);

    }

    private List<double> GetAngles(List<double> tapTimes, List<double> tickTimes)
    {
        List<double> nearestTicks = new(tapTimes.Count);
        List<double> angles = new(tapTimes.Count);

        tickTimes.Sort();

        //// Add another tick to the end in case the last tap is after the last tick
        double lastTick = tickTimes[^1];
        // We can tell which eventBox was the most recent since it's stored in lastEventNum
        double bonusTick = lastTick + Wheel.eventList[lastEventNum] / (Wheel.wheelTempo * Wheel.SumArray(Wheel.eventList));
        // Add the bonus tick to a copy of tickTimes so we don't mess with the actual data
        List<double> tempTicks = new(tickTimes);
        tempTicks.Add(bonusTick);

        // Now we get the closest tick times for each tap
        foreach (double tap in tapTimes)
        {
            // BinarySearch returns either exact index or bitwise complement of index of the closest larger element
            int index = tempTicks.BinarySearch(tap);

            if (index >= 0)
            {
                // BinarySearch returns a value >=0 if an exact match is found
                nearestTicks.Add(tempTicks[index]);
                angles.Add(0.0);  // if the tap exactly coincides with the tick, phase is 0
            }
            else
            {
                index = ~index;  // ~ undoes the "bitwise complement" to give us index of closest larger element
                double closest;
                double interval;
                if (index == 0)  // First element is closest
                {
                    closest = tempTicks[0]; 
                    interval = tempTicks[1] - tempTicks[0];  // assume interval before first tick is the same as the first actual IOI
                }
                else if (index == tempTicks.Count)  // Last element is closest
                {
                    // NOTE: This may be incorrect if somehow the last tap is way after the last tick. Shouldn't happen in the context of the game because taps can only happen during a trial when ticks are occurring...
                    closest = tempTicks[^1]; 
                    interval = tempTicks[^1] - tempTicks[^2];  // assume interval after last tick is the same as the last actual IOI
                }
                else
                {
                    // Compare the two nearest values
                    double prev = tempTicks[index - 1];
                    double next = tempTicks[index];
                    closest = (System.Math.Abs(prev - tap) <= System.Math.Abs(next - tap)) ? prev : next;
                    interval = next - prev;
                }
                double currAngle = 2 * System.Math.PI * (tap - closest) / interval;
                angles.Add(currAngle);
            }
        }
        return angles;
    }

    private double GetAngle(double tapTime, double prevTick)
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
            nextTick = prevTick + Wheel.eventList[lastEventNum-1] / (Wheel.wheelTempo * Wheel.SumArray(Wheel.eventList));
        }
        double closest = (System.Math.Abs(prevTick - tapTime) <= System.Math.Abs(nextTick - tapTime)) ? prevTick : nextTick;
        double interval = nextTick - prevTick;

        double currAngle = 2 * System.Math.PI * (tapTime - closest) / interval;
        
        return currAngle;
    }

    private double CircMean(List<double> angleList, bool returnRad=true)
    {
        double sinSum = 0.0;
        double cosSum = 0.0;

        foreach (double angle in angleList)
        {
            sinSum += System.Math.Sin(angle);
            cosSum += System.Math.Cos(angle);
        }

        if (returnRad)
        {
            return System.Math.Atan2(sinSum, cosSum);
        }
        else
        {
            return System.Math.Atan2(sinSum, cosSum) * (180.0 / System.Math.PI); // Converted to degrees
        }
        
    }

    private double CircVectorLength(List<double> angleList)
    {
        double sinSum = 0.0;
        double cosSum = 0.0;

        foreach (double angle in angleList)
        {
            sinSum += System.Math.Sin(angle);
            cosSum += System.Math.Cos(angle);
        }

        return System.Math.Sqrt(sinSum * sinSum+ cosSum * cosSum) / angleList.Count;
    }


    //private void OnEnable()
    //{
    //    ////Debug.Log("Trigger triggered!");
    //    //TargetControl.OnContactStart += WindowContactOn;
    //    //TargetControl.OnContactEnd += WindowContactOff;
    //    //TargetControl.OnBeatZoneStart += BeatZoneContactOn;
    //    //TargetControl.OnBeatZoneEnd += BeatZoneContactOff;
    //    //BeatTicker.OnBeatContact += BeatContact;

    //    //var gameplayActions = inputActions.FindActionMap("Rhythm");
    //    //triggerAction = gameplayActions.FindAction("Click");

    //    //triggerAction.performed += OnClick;
    //    //triggerAction.Enable();

    //}

    void ActivateInputs()
    {
        //Debug.Log("Trigger triggered!");
        TargetControl.OnContactStart += WindowContactOn;
        TargetControl.OnContactEnd += WindowContactOff;
        TargetControl.OnBeatZoneStart += BeatZoneContactOn;
        TargetControl.OnBeatZoneEnd += BeatZoneContactOff;
        BeatTicker.OnBeatContact += BeatContact;

        var gameplayActions = inputActions.FindActionMap("Rhythm");
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

    public void QuitGame()
    {
        Application.Quit();
    }

    private void OnDisable()
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
                UpdateScore();
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

    void OnApplicationQuit()
    {
        EventLogger.StopLog();
    }

}


