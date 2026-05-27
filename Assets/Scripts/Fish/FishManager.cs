using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;

public class FishManager : MonoBehaviour
{
    // controls the beat (fish/bird) objects - spawning, moving, disappearing
    
    public float fishRate;
    public Beat[] fishEventListRaw;  // Beat durations

    public string beatType;

    public float colliderSize;
    public float beatZoneSize;

    

    void Start()
    {
        
    }

   
    void Update()
    {
       
        
    }
    public void StartSwim()
    {

    }

    public void StopSwim()
    {

    }

    public void Clear()
    {

    }

    public void SpawnFish()
    {

    }

    public double GetNextBeat()
    {
        double temp = 0;
        // TODO: Get next beat onset time
        return temp;
    }

    public float SumArray(float[] toBeSummed)
    {
        float sum = 0;
        foreach (float i in toBeSummed)
        {
            sum += i;
        }
        return sum;
    }
}
