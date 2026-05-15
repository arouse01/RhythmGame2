using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class FishControl : MonoBehaviour
{
    public float fishRate;
    public float[] fishEventList;

    public float colliderSize;
    public float beatZoneSize;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
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
