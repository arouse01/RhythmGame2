// The code example shows how to implement a metronome that procedurally
// generates the click sounds via the OnAudioFilterRead callback.
// While the game is paused or suspended, this time will not be updated and sounds
// playing will be paused. Therefore developers of music scheduling routines do not have
// to do any rescheduling after the app is unpaused

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class Metronome : MonoBehaviour
{
    //Timer Events based on the beat
    public delegate void BeatTrigger();
    public static event BeatTrigger OnBeat;

    private double beatTime = 0;
    private double lastBeatTime = 0;

    public double bpm = 140.0F;
    public float gain = 0.5F;

    private double nextTick = 0.0F;
    private double sampleRate = 0.0F;
    private bool running = false;

    private int tickNum;
    private int tickCount;
    private List<float> tickList;

    private bool tickFlag;

    void Start()
    {
        double startTick = AudioSettings.dspTime;
        sampleRate = AudioSettings.outputSampleRate;
        Debug.Log("SR:" + AudioSettings.outputSampleRate);
        nextTick = startTick * sampleRate;
        running = true;
        tickNum = 0;
        tickList = new List<float> { 1, 1, 2, 1, 1, 2 };
        tickCount = tickList.Count;
        tickFlag = false;
    }

    private void Update()
    {

        if (tickFlag)
        {
            if (OnBeat != null)
                //OnBeat();
            if (tickNum+1 >= tickCount)
            {
                tickNum = 0;
            }
            else
            {
                tickNum++;
            }
            tickFlag = false;
        }
        beatTime = AudioSettings.dspTime;
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        if (!running)
            return;
        Debug.Log("Time:" + AudioSettings.dspTime);
        double samplesPerTick = sampleRate * 60.0F / bpm * tickList[tickNum];  // update here to pull from array of beat intervals and properly update
        double sample = AudioSettings.dspTime * sampleRate;
        int dataLen = data.Length / channels;
        int n = 0;
        while (n < dataLen)
        {
            while (sample + n >= nextTick)
            {
                nextTick += samplesPerTick;
                lastBeatTime = AudioSettings.dspTime;
                //Debug.Log("Metronome tick");
            }
            tickFlag = lastBeatTime == beatTime;
            n++;
        }
    }

}