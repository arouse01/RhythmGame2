using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

public class ParameterLoader : MonoBehaviour
{
    // Last modified: 12/20/24 (AR) Adjusted the session parameter file to be in the Session Parameter folder of the data directory
    // Last modified: 12/17/25 (AR) Session parameters relocated to PlayerPrefs variable
    
    [System.Serializable]
    public class TrialParameters
    {
        public int level;
        public float wheelSpeed;
        public float[] eventList;
        public int beatMax;
        public int targetScore;
        public float colliderSize;
        public float beatZoneSize;
    }

    public string AnimalName;
    public string ExperimenterName;
    public string SavePath;
    public string ParamPath;
    public float LRSDuration;
    public int LRSThresh;
    public float targetZoneWidth;

    public TrialParameters[] trials; // Array to hold the parameters for each trial

    private Dictionary<string, int> columnMapping;

    void Start()
    {
        // Call the function to load parameters from the file
        //Debug.Log("Starting file load!");
        //LoadSessionParameters("parameters.txt");
    }

    public void SetPhaseParamPath(string folderPath)
    {
        if (folderPath == null)
        {
            ParamPath = Application.dataPath;
        } else
        {
            ParamPath = folderPath;
        }
    }

    public void LoadTrialParameters(string fileName)
    {

        string filePath;

#if UNITY_EDITOR
        // In the Editor, look for the file in the project root
        filePath = Path.Combine(Application.dataPath, "PhaseParams", fileName);

#else
        // In a built game, look for the file in the build directory
        filePath = Path.Combine(ParamPath, fileName);
#endif

        if (!File.Exists(filePath))
        {
            Debug.LogError("Trial parameter file not found at: " + filePath);
        }

        if (File.Exists(filePath))
        {
            string[] lines = File.ReadAllLines(filePath);
            if (lines.Length > 0)
            {
                ParseHeaders(lines[0]);

                trials = new TrialParameters[lines.Length - 1];

                // start at i=1 because the ParseHeaders function already read the first line
                for (int i = 1; i < lines.Length; i++)
                {
                    ProcessLine(i, lines[i]);
                }
            }
        }
    }

    private void ParseHeaders(string headerLine)
    {
        columnMapping = new Dictionary<string, int>();
        string[] headers = headerLine.Split('\t'); // Assuming tab-delimited file

        for (int i = 0; i < headers.Length; i++)
        {
            string header = headers[i].Trim();
            if (!columnMapping.ContainsKey(header))
            {
                columnMapping[header] = i;
            }
        }
    }

    private void ProcessLine(int i, string line)
    {
        
        string[] splitLine = line.Split('\t'); // Split the line by tabs

        if (columnMapping.TryGetValue("Level", out int levelCol) &&
            columnMapping.TryGetValue("Rate", out int wheelSpeedCol) &&
            columnMapping.TryGetValue("Pattern", out int patternCol) &&
            columnMapping.TryGetValue("MaxBeats", out int beatMaxCol) &&
            columnMapping.TryGetValue("TargetBeats", out int targetScoreCol) &&
            columnMapping.TryGetValue("SafeWidth", out int colliderSizeCol) &&
            columnMapping.TryGetValue("BeatWidth", out int beatZoneSizeCol))
        {
            if (levelCol < splitLine.Length &&
                wheelSpeedCol < splitLine.Length &&
                patternCol < splitLine.Length)
            {
                // Parse and use the values
                int levelOut = int.Parse(splitLine[levelCol]);
                float wheelSpeedOut = float.Parse(splitLine[wheelSpeedCol]);
                float[] eventListValues = Array.ConvertAll(splitLine[patternCol].Split(','), float.Parse);
                int beatMaxOut = int.Parse(splitLine[beatMaxCol]);
                int targetScoreOut = int.Parse(splitLine[targetScoreCol]);
                float colliderSizeOut = float.Parse(splitLine[colliderSizeCol]);
                float beatZoneSizeOut = float.Parse(splitLine[beatZoneSizeCol]);

                trials[i - 1] = new TrialParameters
                {
                    level = levelOut,
                    wheelSpeed = wheelSpeedOut,
                    eventList = eventListValues,
                    beatMax = beatMaxOut,
                    targetScore = targetScoreOut,
                    colliderSize = colliderSizeOut,
                    beatZoneSize = beatZoneSizeOut,
                };
                //Debug.Log(trials[i - 1]);
            }
            else
            {
                Debug.LogError("Invalid data format: Missing required columns in a line.");
            }
        }
        else
        {
            Debug.LogError("Missing required headers in the parameter file.");
        }
    }

    public void LoadSessionParameters(string fileName)
    {
        string filePath;

        #if UNITY_EDITOR
            // In the Editor, look for the file in the project root
            filePath = Path.Combine(Directory.GetCurrentDirectory(), fileName);
#else
            // In a built game, look for the file in the build directory
            filePath = Path.Combine(Application.dataPath, "..", "..", fileName);
#endif


        if (!File.Exists(filePath))
        {
            Debug.LogError("Session parameter file not found at: " + filePath);

            // Let user select new parameter file
            //string path = EditorUtility.OpenFilePanel("Select session parameter file", "", "txt");
            //if (path.Length != 0)
            //{
            //    filePath = path;
            //}
        }

        if (File.Exists(filePath))
        {
            string[] lines = File.ReadAllLines(filePath);

            foreach (var line in lines)
            {
                //Debug.Log(line);  // To show the content of the file (for debugging)

                // Parse each line based on your expected format
                // For example, let's assume each line is in the form of "variableName=value"
                string[] keyValue = line.Split('=');
                if (keyValue.Length == 2)
                {
                    string variableName = keyValue[0].Trim();
                    string value = keyValue[1].Trim();

                    // Assign values to the corresponding starting variables
                    if (variableName == "Animal")
                    {
                        AnimalName = value;
                    }
                    else if (variableName == "Experimenter")
                    {
                        ExperimenterName = value;
                    }
                    else if (variableName == "LRS Duration")
                    {
                        float.TryParse(value, out LRSDuration) ;
                    }
                    else if (variableName == "LRS Thresh")
                    {
                        int.TryParse(value, out LRSThresh);
                    }
                    else if (variableName == "Target Zone Width")
                    {
                        float.TryParse(value, out targetZoneWidth);
                    }
                    else if (variableName == "Save Path")
                    {
                        SavePath = value;
                    }
                }
            }

        }
        else
        {
            Debug.Log("Session Parameter File not found");
        }
    }
}


