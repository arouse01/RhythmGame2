//using System.Collections;
//using System.Collections.Generic;
using UnityEngine;
using System;

public class BeatTicker : MonoBehaviour
{
    public static event Action OnBeatContact;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Beat"))
        {
            OnBeatContact?.Invoke();
        }
    }
}
