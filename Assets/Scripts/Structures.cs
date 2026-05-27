using UnityEngine;

public class LevelMetadata
{
    public string displayName;
    public string fileName;
    public string gameType;
    public bool hide;
}

public struct Beat
{
    public int beatNumber;
    public float beatDuration;
    public bool isRest;

}

public struct BeatEvent
{
    public Beat beat;
    public int beatType;  // image - fish (0) or bird (1)
    public int beatLane;  // bottom (0) or top (1)
    public double beepTime; // in dsptime, relative to game start time
    public bool beeped;  // Passed the beep line and triggered it
    public double boopTime;  // in dsptime, relative to game start time
    public bool booped;  // Passed boop line and triggered it
    public bool bopped;
    public float speed;
    public double spawnTime;  // in dsptime, relative to game start time
    public double destroyTime;  // in dsptime, relative to game start time
    public float spawnX;
}

[System.Serializable]
public class WheelTrialParameters
{
    public int level;
    public float wheelSpeed;
    public float[] eventList;
    public int beatMax;
    public int targetScore;
    public float colliderSize;
    public float beatZoneSize;
}

public class FishTrialParameters
{
    public int level;
    public float tempo;
    public double beepBoopTime;
    public bool beepActive;
    public Beat[] fishEventList;
    public int beatMax;
    public int targetScore;
    public float colliderSize;
    public float beatZoneSize;
}
