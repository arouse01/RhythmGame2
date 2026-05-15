using System.Collections;
//using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LRSControl : MonoBehaviour
{

    public Image LRSImage;
    
        
    void Start()
    {
        // LRS shouldn't be visible at first
        LRSImage.enabled = false;
    }

    public void TriggerLRS(float duration)
    {
        StartCoroutine(LRSCoroutine(duration));
    }

    private IEnumerator LRSCoroutine(float duration)
    {
        LRSImage.enabled = true;
        yield return new WaitForSeconds(duration); // Wait for the specified time
        LRSImage.enabled = false; // Turn off the blackout
    }
}
