using UnityEngine;

public class LevelMetadata
{
    public string displayName;
    public string fileName;
    public string gameType;
    public bool hide;
}

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
