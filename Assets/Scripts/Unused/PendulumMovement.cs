 using System.Collections;
using System.Collections.Generic;
using UnityEngine;



//public class PendulumMovement : MonoBehaviour
//{
//    // Start is called before the first frame update
//    void Start()
//    {
//        Debug.Log("test");
//    }

//    public bool isInBox;

//    //void Update()
//    //{
//    //    if (isInBox)
//    //    {
//    //        Debug.Log("Found in box!");
//    //    }
//    //    //else
//    //    //{
//    //    //    Debug.Log("Not in box!");
//    //    //}
//    //}

//    void OnCollisionEnter(Collision collision)
//    {
//        if (collision.gameObject.layer == LayerMask.NameToLayer("Target"))
//        {
//            Debug.Log("pendulum has touched line");
//            // Implement your custom logic here
//        }
//        Debug.Log("pendulum touches line");
//        //isInBox = true;

//    }

//    void OnTriggerEnter(Collider other)
//    {
//        Debug.Log("trigger: pendulum touches line");

//    }

//    void OnTriggerExit(Collider other)
//    {
//        isInBox = false;

//    }
//}

public class LineCrossingDetector : MonoBehaviour
{
    // This method is called when another collider enters the trigger zone
    private void OnTriggerEnter2D(Collider2D other)
    {
        
            Debug.Log("Trigger triggered!");

    }
}
