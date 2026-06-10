//using System.Collections;
//using System.Collections.Generic;
using UnityEngine;
//using UnityEngine.U2D;

public class EventBox : MonoBehaviour
{
    public float startAngle = 0;
    public int beatIndex;
    public WheelBeat wheelBeat;
    public float colliderSize = 5;
    public float beatZoneSize = 2;
    public float outerRadius = 2f;
    public float innerRadius = 1f;
    public int segments = 40;
    //public GameObject parentObject; // The GameObject to which the segment will be attached
    public Material material; // The material to apply to the segment

    [SerializeField] private MeshRenderer SafeSpaceRenderer;
    [SerializeField] private SpriteRenderer BeatMarkerRenderer;
    [SerializeField] private MeshRenderer BeatZoneRenderer;

    private GameObject wheel;  // parent wheel object, for getting radius
    private float circRadius;
    private Transform beatZone;
    private Transform beatMarker;

    private Color safeSpaceColor;
    private Color beatZoneColor;
    private Color beatZoneColorFade;

    // Start is called before the first frame update
    void Start()
    {
        wheel = transform.parent.gameObject;
        circRadius = wheel.transform.localScale.x / 2f;

        beatZone = transform.Find("BeatZone");
        beatMarker = transform.Find("BeatMarker");

        float h = 2f + 1.0f * 2 / circRadius; // height of the wedge

        // create safe zone
        CreateWedge(colliderSize, h + 0.1f / circRadius, innerRadius, gameObject);

        // create beat zone
        CreateWedge(beatZoneSize, h, innerRadius, beatZone.gameObject);

        //// update BeatMarker
        float parentScaleX = transform.parent != null ? transform.parent.localScale.x : 1;
        float beatMarkerWidth = 0.1f / parentScaleX;  // Make the beat marker independent of wheel size
        float beatMarkerHeight = h - innerRadius;
        float beatMarkerY = (h + innerRadius) / 2f;
        beatMarker.transform.localScale = new Vector3(beatMarkerWidth, beatMarkerHeight, 0.0f);
        beatMarker.transform.localPosition = new Vector3(0f, beatMarkerY, 0.0f);

        // set angle
        //float angleRad = angle * Mathf.Deg2Rad;
        //transform.Rotate(0.0f, 0.0f, startAngle, Space.Self);
        transform.localRotation = Quaternion.Euler(0, 0, startAngle);

        // the localScale gets set to 0.02 after the first round (maybe because of the wheel resizing between rounds?)
        transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
    }


    void CreateWedge(float wedgeWidth, float top, float bottom, GameObject targetObject)
    {
        // Create and assign mesh
        Mesh mesh = new();

        float angleStep = wedgeWidth / segments;
        float baseAngle = 0.0f;
        // Vertices array
        Vector3[] vertices = new Vector3[(segments + 1) * 2]; // +2 for the center points


        for (int i = 0; i <= segments; i++)
        {
            float currentAngle = Mathf.Deg2Rad * (baseAngle + (i * angleStep) - wedgeWidth / 2);
            float x = Mathf.Sin(currentAngle);
            float y = Mathf.Cos(currentAngle);

            // Outer arc vertex
            vertices[i] = new Vector3(x * top, y * top, 0);

            // Inner arc vertex
            vertices[i + segments + 1] = new Vector3(x * bottom, y * bottom, 0);
        }

        // Triangles
        int[] triangles = new int[segments * 6]; // 2 triangles per segment
        int ti = 0;
        for (int i = 0; i < segments; i++)
        {
            int outer0 = i;
            int outer1 = i + 1;
            int inner0 = i + segments + 1;
            int inner1 = i + segments + 2;

            // First triangle
            triangles[ti++] = outer0;
            triangles[ti++] = inner0;
            triangles[ti++] = outer1;

            // Second triangle
            triangles[ti++] = outer1;
            triangles[ti++] = inner0;
            triangles[ti++] = inner1;
        }
        //// The center and outer center point for mesh creation
        //vertices[0] = Vector3.zero;
        //vertices[vertexCount + 1] = new Vector3(0f, 0f, 0f);

        //// Calculate angle in radians
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        MeshFilter meshFilter = targetObject.GetComponent<MeshFilter>();
        meshFilter.mesh = mesh;

        //MeshCollider meshCollider = targetObject.GetComponent<MeshCollider>();
        //meshCollider.sharedMesh = meshFilter.mesh; // Assign the same mesh

        //MeshRenderer meshRenderer = targetObject.GetComponent<MeshRenderer>();
        //meshRenderer.material = material;

    }

    public void Initialize(WheelBeat beat)
    {
        wheelBeat = beat;
        startAngle = beat.beatAngle;
        beatIndex = beat.beatNumber;

        beatZone = transform.Find("BeatZone");
        beatMarker = transform.Find("BeatMarker");

    }
    
    //void AttachToParent()
    //{
    //    // If a parent is specified, attach the segment to it and set the position and rotation.
    //    if (parentObject != null)
    //    {
    //        transform.SetParent(parentObject.transform);
    //        transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
    //    }
    //}


    public void SelfDestruct()
    {
        Destroy(gameObject);
    }

    public void InitializeColors(Color safeColor, Color beatColor)
    {
        safeSpaceColor = safeColor;
        beatZoneColor = beatColor;
        beatZoneColorFade = beatZoneColor;
        beatZoneColorFade.a = .5f;
    }

    public void ResetColors()
    {
        //beatZone = transform.Find("BeatZone");
        SafeSpaceRenderer.material.color = safeSpaceColor;
        BeatZoneRenderer.material.color = beatZoneColor;
    }
    
    public void Bop(Color bopColor)
    {
        BeatZoneRenderer.material.color = bopColor;
    }

    public void BopSafe()
    {
        BeatZoneRenderer.material.color = beatZoneColorFade;
    }


}