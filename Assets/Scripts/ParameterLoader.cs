using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

public class ParameterLoader
{
    // Last modified: 12/20/24 (AR) Adjusted the session parameter file to be in the Session Parameter folder of the data directory
    // Last modified: 12/17/25 (AR) Session parameters relocated to PlayerPrefs variable


    public static List<LevelMetadata> GetAvailableLevels(string gameType, string paramPath)
    {
        List<LevelMetadata> availableLevels = new();

        string[] levelFiles = Directory.GetFiles(paramPath, "*.txt");

        foreach (string levelFile in levelFiles)
        {
            // extract the filename itself first 
            string fileName = Path.GetFileName(levelFile);
            //open selected file
            
            LevelMetadata currMetaData = LoadTrialMetadata(paramPath, fileName);
            if (currMetaData.gameType.ToLower() == gameType.ToLower())
            {
                if (!currMetaData.hide)
                {
                    availableLevels.Add(currMetaData);
                }
            }
        }
        return availableLevels;
    }
    
    static LevelMetadata LoadTrialMetadata(string paramPath, string fileName)
    {

        string filePath;

#if UNITY_EDITOR
        // In the Editor, look for the file in the project root
        filePath = Path.Combine(Application.dataPath, "PhaseParams", fileName);

#else
        // In a built game, look for the file in the build directory
        filePath = Path.Combine(paramPath, fileName);
#endif

        if (!File.Exists(filePath))
        {
            Debug.LogError("Trial parameter file not found at: " + filePath);
            return null;
        }
        else
        {
            Dictionary<string, string> metadata = new();

            foreach (string line in File.ReadLines(filePath))
            {
                // quit if we've reached the trial data
                if (line.StartsWith("#")) 
                {
                    if (line.Contains("TRIALS"))
                    {
                        break;
                    }
                    else if(line.Contains("METADATA"))
                    {
                        continue;
                    }

                } 
                
                // skip blank lines, if any
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                
                string[] parts = line.Split('=');
                if (parts.Length == 2)
                {
                    string key = parts[0].Trim();
                    string value = parts[1].Trim();
                    metadata[key] = value;
                }
                else
                {
                    Debug.LogWarning($"Invalid metadata line: {line} in {fileName}");
                }
                
            }
                        

            LevelMetadata meta = new()
            {
                displayName = metadata.GetValueOrDefault("name", fileName),
                fileName = fileName,
                gameType = metadata.GetValueOrDefault("game", "Wheel") // since wheel was first and some parameter files might not have metadata, default to wheel
            };

            // since the hide value is bool, it needs special processing
            if (metadata.TryGetValue("hide", out string boolValue))
            {
                if (bool.TryParse(boolValue, out bool parsedBool))
                {
                    meta.hide = parsedBool;
                }
            }

            return meta;
        }
    }

    private static Dictionary<string, int> ParseHeaders(string headerLine)
    {
        Dictionary<string, int> columnMapping = new();
        string[] headers = headerLine.Split('\t'); // Assuming tab-delimited file

        for (int i = 0; i < headers.Length; i++)
        {
            string header = headers[i].Trim();
            if (!columnMapping.ContainsKey(header))
            {
                columnMapping[header] = i;
            }
        }
        return columnMapping;
    }

    public static List<WheelTrialParameters> LoadWheelTrialParameters(string paramPath, string fileName)
    {

        string filePath;
        Dictionary<string, int> columnMapping = null;

#if UNITY_EDITOR
        // In the Editor, look for the file in the project root
        filePath = Path.Combine(Application.dataPath, "PhaseParams", fileName);

#else
        // In a built game, look for the file in the build directory
        filePath = Path.Combine(paramPath, fileName);
#endif

        if (!File.Exists(filePath))
        {
            Debug.LogError("Trial parameter file not found at: " + filePath);
            return null;
        }
        
        List<WheelTrialParameters> trialList = new();

        // read through, exclude metadata, get just trial info
        bool inTrialSection = false;

        foreach (string rawLine in File.ReadLines(filePath))
        {
            string line = rawLine.Trim();

            if (line.StartsWith("#") && line.Contains("TRIALS"))
            {
                inTrialSection = true;
                continue;
            }

            // skip blank lines, if any
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (!inTrialSection)
            {
                continue;
            }

            // we can only get here if the code knows we've hit the trial list
            if (columnMapping == null)  // get the column order - this should be the first thing that happens in the TRIALS section
            {
                columnMapping = ParseHeaders(line);
                continue;
            }

            WheelTrialParameters newTrial = ProcessWheelLine(columnMapping, line);
            
            
            trialList.Add(newTrial);
            
        }
        return trialList;
    }

    private static WheelTrialParameters ProcessWheelLine(Dictionary<string, int> columnMapping, string line)
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

                WheelTrialParameters currTrial = new()
                {
                    level = levelOut,
                    wheelSpeed = wheelSpeedOut,
                    eventList = eventListValues,
                    beatMax = beatMaxOut,
                    targetScore = targetScoreOut,
                    colliderSize = colliderSizeOut,
                    beatZoneSize = beatZoneSizeOut,
                };
                return currTrial;
                //Debug.Log(trials[i - 1]);
            }
            else
            {
                Debug.LogError("Invalid data format: Missing required columns in a line.");
                return null;
            }
        }
        else
        {
            Debug.LogError("Missing required headers in the parameter file.");
            return null;
        }
    }

    public static List<FishTrialParameters> LoadFishTrialParameters(string paramPath, string fileName)
    {

        string filePath;
        Dictionary<string, int> columnMapping = null;

#if UNITY_EDITOR
        // In the Editor, look for the file in the project root
        filePath = Path.Combine(Application.dataPath, "PhaseParams", fileName);

#else
        // In a built game, look for the file in the build directory
        filePath = Path.Combine(paramPath, fileName);
#endif

        if (!File.Exists(filePath))
        {
            Debug.LogError("Trial parameter file not found at: " + filePath);
            return null;
        }

        List<FishTrialParameters> trialList = new();

        // read through, exclude metadata, get just trial info
        bool inTrialSection = false;

        foreach (string rawLine in File.ReadLines(filePath))
        {
            string line = rawLine.Trim();

            if (line.StartsWith("#") && line.Contains("TRIALS"))
            {
                inTrialSection = true;
                continue;
            }

            // skip blank lines, if any
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (!inTrialSection)
            {
                continue;
            }

            // we can only get here if the code knows we've hit the trial list
            if (columnMapping == null)  // get the column order - this should be the first thing that happens in the TRIALS section
            {
                columnMapping = ParseHeaders(line);
                continue;
            }

            FishTrialParameters newTrial = ProcessFishLine(columnMapping, line);


            trialList.Add(newTrial);

        }
        return trialList;
    }

    private static FishTrialParameters ProcessFishLine(Dictionary<string, int> columnMapping, string line)
    {
        // TODO: update for fish game
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

                FishTrialParameters currTrial = new()
                {
                    level = levelOut,
                    travelSpeed = wheelSpeedOut,
                    fishEventList = eventListValues,
                    beatMax = beatMaxOut,
                    targetScore = targetScoreOut,
                    colliderSize = colliderSizeOut,
                    beatZoneSize = beatZoneSizeOut,
                };
                return currTrial;
                //Debug.Log(trials[i - 1]);
            }
            else
            {
                Debug.LogError("Invalid data format: Missing required columns in a line.");
                return null;
            }
        }
        else
        {
            Debug.LogError("Missing required headers in the parameter file.");
            return null;
        }
    }



}


