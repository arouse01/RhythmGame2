using UnityEngine;
using System;

public class PlayerControl : MonoBehaviour
{
    public Collider beatZone;
    public Collider safeZone;

    Animation anim;

    // Events to trigger GameController
    public static event Action OnContactStart;
    public static event Action OnContactEnd;
    public static event Action OnBeatZoneStart;
    public static event Action OnBeatZoneEnd;

    private Transform avatar;

    public float targetZoneWidth = .25f;
    public float targetZoneHeight = 3f;
    public float targetZoneY = 3f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        avatar = transform.Find("Body");

        anim = avatar.GetComponent<Animation>();

        InitializeTarget();
    }

    void BuildTargetZone()
    {
        MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();


        // Create the mesh
        Mesh mesh = new();

        // Define the vertices (corners of the rectangle)
        Vector3[] vertices = new Vector3[]
        {
            new(-targetZoneWidth / 2, targetZoneY-targetZoneHeight / 2, 0), // Bottom-left
            new(targetZoneWidth / 2, targetZoneY-targetZoneHeight / 2, 0),  // Bottom-right
            new(-targetZoneWidth / 2, targetZoneY+targetZoneHeight / 2, 0),  // Top-left
            new(targetZoneWidth / 2, targetZoneY+targetZoneHeight / 2, 0)    // Top-right
        };

        // Define the triangles (two triangles make the rectangle)
        int[] triangles = new int[]
        {
            0, 2, 1, // First triangle (Bottom-left, Top-left, Bottom-right)
            1, 2, 3  // Second triangle (Bottom-right, Top-left, Top-right)
        };

        // Assign to the mesh
        mesh.vertices = vertices;
        mesh.triangles = triangles;

        // Recalculate bounds and normals for rendering
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        // Assign the mesh to the MeshFilter
        meshFilter.mesh = mesh;

        MeshCollider meshCollider = gameObject.GetComponent<MeshCollider>();
        meshCollider.sharedMesh = meshFilter.mesh; // Assign the same mesh
    }
    
    public void InitializeTarget()
    {
        BuildTargetZone();
        BuildAvatar();
    }

    void BuildAvatar()
    {

    }

    void Update()
    {
        
    }

    public void Dive()
    {

    }

    // This method is called when another collider enters the trigger zone
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("EventBox"))
        {
            safeZone = other;
            OnContactStart?.Invoke();
            //GetComponent<Renderer>().material.color = Color.blue;
            //triangle.GetComponent<Renderer>().material.color = Color.white;
            //other.GetComponent<Renderer>().material.color = Color.yellow;
        }
        else if (other.CompareTag("BeatZone"))
        {
            beatZone = other;
            OnBeatZoneStart?.Invoke();

        }


    }

    // This method is called when another collider exits the trigger zone
    private void OnTriggerExit(Collider other)
    {
        //GetComponent<Renderer>().material.color = Color.white;
        if (other.CompareTag("EventBox"))
        {
            OnContactEnd?.Invoke();
            //triangle.GetComponent<Renderer>().material.color = Color.blue;
            //other.GetComponent<SpriteRenderer>().SetColor("_Color", Color.black);
        }
        else if (other.CompareTag("BeatZone"))
        {
            OnBeatZoneEnd?.Invoke();
            //other.GetComponent<Renderer>().material.color = beatZoneColorDefault;
        }

    }

}
