// The code example shows how to implement a metronome that procedurally
// generates the click sounds via the OnAudioFilterRead callback.
// While the game is paused or suspended, this time will not be updated and sounds
// playing will be paused. Therefore developers of music scheduling routines do not have
// to do any rescheduling after the app is unpaused

using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class test : MonoBehaviour
{
    private void OnEnable()
    {
        Metronome.OnBeat += Beat;
        Debug.Log("Tick enabled");

    }

    private void OnDisable()
    {
        Metronome.OnBeat -= Beat;

    }

    void Beat()
    {
        Debug.Log("Tick at " + AudioSettings.dspTime);
        //Do something here
    }

    void DownBeat()
    {
        //Do something else here
    }
}