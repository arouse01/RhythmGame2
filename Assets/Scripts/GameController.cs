using SimpleFileBrowser;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
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
    public static GameController Instance { get; private set; }

    public bool debugMode;
    
    public event System.Action OnGameStart;
    public event System.Action OnGamePause;
    public event System.Action OnGameResume;

    public float timeStepPrecise;
    public float timeStepSlow = 0.02f;
    
    public Image LRSImage;
    private Coroutine blackoutRoutine;

    public LevelScore LevelScore;  // LevelScore script for managing display of level stats

    public TextMeshProUGUI versionText;

    public GameObject UserInputObject;
    private GameObject preGamePanel; 
    private GameObject AnimalField;
    private GameObject preNotesField;

    private GameObject prefsPanel;
    private GameObject PhaseParamFolderField;
    //private string PhaseParamFolder;
    private GameObject SaveFolderField;
    //private string SaveFolder;
    private GameObject LRSDurationField; 
    private GameObject TargetWidthField;
    private GameObject prefsButton;
    private GameObject prefWarningText;

    private GameObject gameOverPanel;
    private GameObject playerInfoField;
    private GameObject attentionField;
    private GameObject postNotesField;

    [SerializeField] private TMP_Dropdown gameDropdown;
    [SerializeField] private TMP_Dropdown levelDropdown;
    private string GameType;
    public string levelParameterFile;
    private List<LevelMetadata> availableLevels;

    public GameObject InGameText;
    private TextMeshProUGUI scoreText;
    private TextMeshProUGUI messageText;
    private TextMeshProUGUI statsText;
    private GameObject levelScoreObject;
    private GameObject lifeMarkers;

    public ParameterLoader parameters;

    public bool pause;

    private TextMeshProUGUI Life1Marker;
    private TextMeshProUGUI Life2Marker;
    private TextMeshProUGUI Life3Marker;

    //private static string logFilePath = Application.dataPath + "/Data/EventLog.txt";

    public InputActionAsset inputActions;
    //private InputAction triggerAction;
    //private InputAction cancelAction;

    public string animalName;
    public string preNotesText;

    void Start()
    {
        // Unload any other scenes, just in case
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);

            if (scene.name != "Menu")
            {
                SceneManager.UnloadSceneAsync(scene);
            }
        }

        debugMode = true;

        TimeUtil.fixedDeltaTime = timeStepSlow;
        TimeUtil.maximumDeltaTime = timeStepSlow * 3;
        Application.targetFrameRate = 60;
        ShowLRS(false); // Disable the LRS image to start
        versionText.text = "Version " + Application.version;  // Update the version number on the home screen

        //// Get layout objects
        // preGamePanel
        preGamePanel = UserInputObject.transform.Find("InputPanels/SetupPanel").gameObject;
        AnimalField = preGamePanel.transform.Find("UserInputPre/AnimalNameRow/AnimalNameField").gameObject;
        preNotesField = preGamePanel.transform.Find("UserInputPre/PreNotesRow/PreNotesField").gameObject;
        //gameDropdown = preGamePanel.transform.Find("UserInputPre/GameSelectRow/GameSelectDropdown").gameObject;
        //levelDropdown = preGamePanel.transform.Find("UserInputPre/GameSelectRow/LevelSelectDropdown").gameObject;

        // prefsPanel
        prefsPanel = UserInputObject.transform.Find("InputPanels/PrefsPanel").gameObject;
        PhaseParamFolderField = prefsPanel.transform.Find("UserInputPrefs/PhaseParamFolderRow/PhaseParamFolderField").gameObject;
        SaveFolderField = prefsPanel.transform.Find("UserInputPrefs/SaveFolderRow/SaveFolderField").gameObject;
        LRSDurationField = prefsPanel.transform.Find("UserInputPrefs/OtherFieldsRow/LRSDurField").gameObject;
        TargetWidthField = prefsPanel.transform.Find("UserInputPrefs/OtherFieldsRow/TargetSizeField").gameObject;
        prefsButton = UserInputObject.transform.Find("BottomText/PrefsButton").gameObject;
        prefWarningText = prefsPanel.transform.Find("UserInputPrefs/WarningTextRow/PrefWarningText").gameObject;

        // 
        gameOverPanel = UserInputObject.transform.Find("InputPanels/EndPanel").gameObject;
        playerInfoField = gameOverPanel.transform.Find("UserInputEnd/PlayerInfoRow/PlayerInfoField").gameObject;
        attentionField = gameOverPanel.transform.Find("UserInputEnd/AttentionRow/AttentionField").gameObject;
        postNotesField = gameOverPanel.transform.Find("UserInputEnd/PostNotesRow/PostNotesField").gameObject;

        // in-game text
        scoreText = InGameText.transform.Find("Score Text").GetComponent<TextMeshProUGUI>();
        messageText = InGameText.transform.Find("Message Text").GetComponent<TextMeshProUGUI>();
        statsText = InGameText.transform.Find("LevelStats Text").GetComponent<TextMeshProUGUI>();
        lifeMarkers = InGameText.transform.Find("Life Markers").gameObject;
        Life1Marker = lifeMarkers.transform.Find("Life 1").GetComponent<TextMeshProUGUI>();
        Life2Marker = lifeMarkers.transform.Find("Life 2").GetComponent<TextMeshProUGUI>();
        Life3Marker = lifeMarkers.transform.Find("Life 3").GetComponent<TextMeshProUGUI>();
        levelScoreObject = InGameText.transform.Find("Level Score").gameObject;

        // load game and level choices from last selection
        
        //parameters.SetPhaseParamPath(phaseParamPath);
        //OnGameDropdownChanged(PlayerPrefs.GetInt("GameTypeIndex"));
        OnGameDropdownChanged(0);
        OnLevelDropdownChanged(0);
        gameDropdown.onValueChanged.AddListener(OnGameDropdownChanged);
        levelDropdown.onValueChanged.AddListener(OnLevelDropdownChanged);


        MainMenuStart();

        
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void MainMenuStart()
    {
        //gameOver = false;
        //gameOverStarted = false;
        gameOverPanel.SetActive(false);
        UserInputObject.SetActive(true);
        preGamePanel.SetActive(true);
        prefsButton.SetActive(true);
        prefsPanel.SetActive(false);
        InGameText.SetActive(false);

        if (debugMode)
        {
            DebugModeStart(1, 1);
        }
    }

    public void OpenPrefs()
    {
        //parameters.LoadSessionParameters("parameters.txt");
        PhaseParamFolderField.GetComponent<TMPro.TMP_InputField>().text = PlayerPrefs.GetString("PhaseParamFolder");
        SaveFolderField.GetComponent<TMPro.TMP_InputField>().text = PlayerPrefs.GetString("SaveFolder");
        LRSDurationField.GetComponent<TMPro.TMP_InputField>().text = PlayerPrefs.GetFloat("LRSDuration").ToString();
        TargetWidthField.GetComponent<TMPro.TMP_InputField>().text = PlayerPrefs.GetFloat("TargetWidth").ToString();

        // clear any highlighting
        PhaseParamFolderField.GetComponent<Image>().color = new Color(1f, 1f, 1f, 1f);
        SaveFolderField.GetComponent<Image>().color = new Color(1f, 1f, 1f, 1f);
        LRSDurationField.GetComponent<Image>().color = new Color(1f, 1f, 1f, 1f);
        TargetWidthField.GetComponent<Image>().color = new Color(1f, 1f, 1f, 1f);

        preGamePanel.SetActive(false); 
        prefsPanel.SetActive(true);
        prefsButton.SetActive(false);

        prefWarningText.GetComponent<TMPro.TMP_Text>().text = "";

    }

    public void SavePrefs()
    {
        PlayerPrefs.SetString("PhaseParamFolder", PhaseParamFolderField.GetComponent<TMPro.TMP_InputField>().text);
        PlayerPrefs.SetString("SaveFolder", SaveFolderField.GetComponent<TMPro.TMP_InputField>().text);
        PlayerPrefs.SetFloat("LRSDuration", float.Parse(LRSDurationField.GetComponent<TMPro.TMP_InputField>().text));
        PlayerPrefs.SetFloat("TargetWidth", float.Parse(TargetWidthField.GetComponent<TMPro.TMP_InputField>().text));

        // check prefs
        if (CheckPrefs())
        {
            prefsPanel.SetActive(false);
            preGamePanel.SetActive(true);
            prefsButton.SetActive(true);
        }
        
    }

    public void ClosePrefs()
    {
        prefsPanel.SetActive(false);
        preGamePanel.SetActive(true);
        prefsButton.SetActive(true);
    }

    public void SetPhaseParamFolder()
    {
        StartCoroutine(ShowLoadDialogCoroutine("phase"));
        
    }

    public void SetSaveFolder()
    {
        StartCoroutine(ShowLoadDialogCoroutine("save"));
    }

    IEnumerator ShowLoadDialogCoroutine(string whichField)
    {
        // Show a load file dialog and wait for a response from user
        // Load file/folder: file, Allow multiple selection: true
        // Initial path: default (Documents), Initial filename: empty
        // Title: "Load File", Submit button text: "Load"
        yield return FileBrowser.WaitForLoadDialog(FileBrowser.PickMode.Folders, false, null, null, "Select Files", "Load");

        // Dialog is closed

        if (FileBrowser.Success)
            if (whichField == "phase")
            {
                PhaseParamFolderField.GetComponent<TMPro.TMP_InputField>().text = FileBrowser.Result[0];
            } else if (whichField == "save")
            {
                SaveFolderField.GetComponent<TMPro.TMP_InputField>().text = FileBrowser.Result[0];
            }
    }

    public bool CheckPrefs()
    {
        // Validate all required preferences, return false if at least one is unset or invalid
        bool Success = true;
        string warningText;
        // if LRSDuration is 0 or unset
        // if targetZoneWidth is 0 or unset
        // if sessionFile is not found
        // if savePath is unset or not found

        // First check if any fields are invalid overall (so we can go to prefs layout first)
        if (!System.IO.Directory.Exists(PlayerPrefs.GetString("PhaseParamFolder")) || 
            !System.IO.Directory.Exists(PlayerPrefs.GetString("SaveFolder")) ||
            PlayerPrefs.GetFloat("LRSDuration") <= 0 ||
            PlayerPrefs.GetFloat("TargetWidth") <= 0)
        {
            Success = false;

            if (!prefsPanel.activeSelf)
            {
                // if prefs panel is not active, activate it
                OpenPrefs();
            }
            warningText = "Warning: Missing/Invalid preferences";
            prefWarningText.GetComponent<TMPro.TMP_Text>().text = warningText;
        }

        if (!Success)
        {
            // highlight the erroneous fields
            if (!System.IO.Directory.Exists(PlayerPrefs.GetString("PhaseParamFolder")))
            {
                PhaseParamFolderField.GetComponent<Image>().color = new Color(1f, 1f, 0.5f, 1f);
            }
            else
            {
                PhaseParamFolderField.GetComponent<Image>().color = new Color(1f, 1f, 1f, 1f);
            }
            
            if (!System.IO.Directory.Exists(PlayerPrefs.GetString("SaveFolder")))
            {
                SaveFolderField.GetComponent<Image>().color = new Color(1f, 1f, 0.5f, 1f);
            }
            else
            {
                SaveFolderField.GetComponent<Image>().color = new Color(1f, 1f, 1f, 1f);
            }
            
            if (PlayerPrefs.GetFloat("LRSDuration") <= 0)
            {
                LRSDurationField.GetComponent<Image>().color = new Color(1f, 1f, 0.5f, 1f);
            }
            else
            {
                LRSDurationField.GetComponent<Image>().color = new Color(1f, 1f, 1f, 1f);
            }
            if (PlayerPrefs.GetFloat("TargetWidth") <= 0)
            {
                TargetWidthField.GetComponent<Image>().color = new Color(1f, 1f, 0.5f, 1f);
            }
            else
            {
                TargetWidthField.GetComponent<Image>().color = new Color(1f, 1f, 1f, 1f);
            }
        }
        
        if (!Success)
        {
            // go to prefsPanel
            preGamePanel.SetActive(false);
            prefsPanel.SetActive(true);
            prefsButton.SetActive(false);
        }

        return Success;
    }

    void OnGameDropdownChanged(int index)
    {
        // load parameter files for selected game
        GameType = gameDropdown.options[gameDropdown.value].text;
        PlayerPrefs.SetInt("GameTypeIndex", gameDropdown.value);
        //string phaseFolder = PlayerPrefs.GetString("PhaseParamFolder");

        // update level selection based on game selection

        levelDropdown.ClearOptions();
        string phaseParamPath = PlayerPrefs.GetString("PhaseParamFolder");
        availableLevels = ParameterLoader.GetAvailableLevels(GameType, phaseParamPath);

        // from availableLevels build the list of names
        List<string> levelNames = new();

        foreach (var level in availableLevels)
        {
            levelNames.Add(level.displayName);
        }

        
        levelDropdown.AddOptions(levelNames);

        levelParameterFile = availableLevels[index].fileName;
        //Debug.Log("Selected index: " + index);
    }

    void OnLevelDropdownChanged(int index)
    {
        //Debug.Log("Selected index: " + index);
        if (availableLevels.Count > 0)
        {
            levelParameterFile = availableLevels[index].fileName;
        }
        
        //levelParameterFile = levelDropdown.options[levelDropdown.value].text;
        //PlayerPrefs.SetInt("LevelIndex", levelDropdown.value);
    }

    //IEnumerator ShowSelectLevelDialogCoroutine()
    //{
    //    // Show a load file dialog and wait for a response from user
    //    // Load file/folder: file, Allow multiple selection: true
    //    // Initial path: default (Documents), Initial filename: empty
    //    // Title: "Load File", Submit button text: "Load"
    //    yield return FileBrowser.WaitForLoadDialog(FileBrowser.PickMode.Folders, false, null, null, "Select Files", "Load");

    //    // Dialog is closed

    //    if (FileBrowser.Success)
    //        if (whichField == "phase")
    //        {
    //            PhaseParamFolderField.GetComponent<TMPro.TMP_InputField>().text = FileBrowser.Result[0];
    //        }
    //        else if (whichField == "save")
    //        {
    //            SaveFolderField.GetComponent<TMPro.TMP_InputField>().text = FileBrowser.Result[0];
    //        }
    //}

    void DebugModeStart(int gameType, int fileNumber)
    {
        gameDropdown.value = gameType;
        levelDropdown.value = fileNumber;
        StartSession();
    }

    public void StartSession()
    {
        // start session of selected type

        if (CheckPrefs())
        {
            //Target.targetZoneWidth = targetZoneWidth;

            preGamePanel.SetActive(false);
            UserInputObject.SetActive(false);
            InGameText.SetActive(true);

            animalName = AnimalField.GetComponent<TMP_InputField>().text;
            preNotesText = preNotesField.GetComponent<TMP_InputField>().text;

            //GameType = "Wheel";
            SceneManager.LoadScene(GameType, LoadSceneMode.Additive);
            StartCoroutine(InvokeStartNextFrame());

            
        }

        
    }

    private IEnumerator InvokeStartNextFrame()
    {
        // trigger game start with an event
        yield return null; // wait one frame so scene is loaded
        OnGameStart?.Invoke();
    }

    public void GameOver()
    {
        SceneManager.UnloadSceneAsync(GameType);
        InGameText.SetActive(false);
        //gameOverStarted = true;
        UserInputObject.SetActive(true);
        gameOverPanel.SetActive(true);
        prefsButton.SetActive(false);
        //Wheel.gameObject.SetActive(false);
        //Target.gameObject.SetActive(false);
    }

    public void GameOverFinish()
    {
        double currTime = AudioSettings.dspTime;
        string playerInfoText = playerInfoField.GetComponent<TMP_InputField>().text;
        string attentionText = attentionField.GetComponent<TMP_InputField>().text;
        string generalNotesText = postNotesField.GetComponent<TMP_InputField>().text;
        EventLogger.LogStruct(EventLogItem.SessionData(currTime, "Player Information", playerInfoText));
        EventLogger.LogStruct(EventLogItem.SessionData(currTime, "Attention", attentionText));
        EventLogger.LogStruct(EventLogItem.SessionData(currTime, "Postsession Notes", generalNotesText));

        gameOverPanel.SetActive(false);
        EventLogger.StopLog();
        MainMenuStart();
    }


    public void UpdateMessage(string message)
    {
        messageText.SetText(message);
    }

    public void UpdateStats(string message)
    {
        statsText.SetText(message);
    }


    public void ShowLevelScore(bool show)
    {
        levelScoreObject.SetActive(show);
    }

    public void ShowLifeMarkers(bool show)
    {
        lifeMarkers.SetActive(show);
    }

    public void TriggerBlackout(float duration)
    {
        // Stop existing blackout if one is already running
        if (blackoutRoutine != null)
        {
            StopCoroutine(blackoutRoutine);
        }
        
        blackoutRoutine = StartCoroutine(BlackoutRoutine(duration));
        //LRSImage.enabled = true; // Enable the blackout image
        //scoreText.enabled = false;
        
    }

    private IEnumerator BlackoutRoutine(float duration)
    {
        SetBlackout(true);
        //AudioListener.pause = true;

        yield return new WaitForSeconds(duration);

        SetBlackout(false);
        //AudioListener.pause = false;

        blackoutRoutine = null;
    }

    public void SetBlackout(bool visible)
    {
        ShowLRS(visible);
        
        if (visible)
        {
            pause = true;
            InGameText.SetActive(false);
            OnGamePause?.Invoke();
        }
        else
        {
            pause = false;
            InGameText.SetActive(true);
            OnGameResume?.Invoke();
        }

    }

    public void ShowLRS(bool show)
    {
        if (LRSImage != null)
        {
            // update the alpha channel to make it not transparent any more
            Color c = LRSImage.color;
            c.a = show ? 1f : 0f;
            LRSImage.color = c;

            // block UI clicks just in case
            LRSImage.raycastTarget = show;

        }
    }

    public void UpdateLives(int currLives)
    {
        // Two-part function: this one just controls the display of lives, but is triggered by a partner function in the game
        // itself that calculates number of lives and triggers LRS, if necessary

        switch (currLives)
        {
            case 0:
                // reached zero lives
                //TriggerLRS(LRSDuration);
                //int lives = defaultLives;
                //UpdateLives(lives);
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

    public void UpdateScore(int score)
    {
        scoreText.SetText(score.ToString());
    }
    
    public void UpdateStars(int numStars)
    {
        LevelScore.ShowStars(numStars);
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    void OnApplicationQuit()
    {
        EventLogger.StopLog();
    }

}


