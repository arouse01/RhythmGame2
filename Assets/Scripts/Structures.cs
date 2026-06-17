using UnityEngine;

public class LevelMetadata
{
    public string displayName;
    public string fileName;
    public string gameType;
    public bool hide;
}

public struct FishBeat
{
    public int beatNumber;
    public float beatDuration;
    public bool isRest;
    public int beatLane;

}

public class FishBeatEvent
{
    public FishBeat beat;
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

public struct WheelBeat
{
    public int beatNumber;
    public float beatAngle;
    public float interval;
    //public GameObject eventBox;
}

public class WheelBeatEvent
{
    /* 
     * Set as class so that setting a variable to a specific WheelBeatEvent, e.g. 
     *      WheelBeatEvent currBeat = wheelBeats[nearestIndex];
     * just sets a reference to the WheelBeatEvent so that
     *      currBeat.Bopped = true;
     * affects the original WheelBeatEvent
     */
    public WheelBeat Beat;
    public EventBox EventBox;
    public int BeatIndex;
    public double BoopTime;  // in dsptime, relative to game start time
    public bool Booped;  // Passed boop line and triggered it
    public bool BoopSet; // boop event has been scheduled
    public bool Bopped;  // User hit the beat
    public double SafeZoneStartTime;
    public bool EnteredSafeZone;
    public double SafeZoneEndTime;
    public bool ExitedSafeZone;
    public double BeatZoneStartTime;
    public bool EnteredBeatZone;
    public double BeatZoneEndTime;
    public bool ExitedBeatZone;

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
    public FishBeat[] fishEventList;
    public int beatMax;
    public int targetScore;
    public float colliderSize;
    public float beatZoneSize;
}
