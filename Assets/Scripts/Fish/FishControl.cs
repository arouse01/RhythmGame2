using TMPro;
using UnityEngine;
using System;

public class BeatObject : MonoBehaviour
{
    public FishBeatEvent beat;
    private int beatType;
    [HideInInspector] public int beatLane;

    public event Action<FishBeatEvent> Beep;
    public event Action<FishBeatEvent> Boop;

    private bool pause;

    // Support ASCII text for body
    [SerializeField] private TMP_Text text;

    private string[] fishFrames =
    {
        "<◉◃",
        "<◉◅",
        "<◉◃",
        "<◉◁"
    };

    private string[] birdFrames =
    {
        "◅◉Є",
        "◅◉E",
        "◅◉Є",
        "◅◉E"
    };
    private string[] frames;
    private int index;
    private int displayIndex;

    void Start()
    {
        displayIndex = 0;
        //beatType = "bird";

        if (beatType == 0)
        {
            frames = fishFrames;
        }
        else
        {
            frames = birdFrames;
            
        }
        text.text = frames[0];
    }

    // Update is called once per frame
    void Update()
    {
        if (!pause)
        {
            index = (index + 1) % 25;
            if (index % 5 == 0)
            {
                displayIndex = (displayIndex + 1) % frames.Length;
                text.text = frames[displayIndex];
            }
        }
        
    }

    public void Initialize(FishBeatEvent beatEvent)
    {
        beat = beatEvent;
        beatType = beat.beatType;
        beatLane = beat.beat.beatLane;
        if (beatType == 0)
        {
            frames = fishFrames;
        }
        else
        {
            frames = birdFrames;
        }
        pause = false;
    }

    public void UpdatePosition(double currentTime)
    {
        float y;
        if (beat.beatLane == 0) 
        {
            y = -2;
        }
        else
        {
            y = 2;
        }
        float newX = (float)(beat.spawnX - (currentTime - beat.spawnTime) * beat.speed);

        transform.position = new Vector2(newX, y);
    }

    public void CheckTriggers(double currentTime)
    {
        if (beat.beepTime <= currentTime && !beat.beeped)
        {
            Beep?.Invoke(beat);
            beat.beeped = true;
        }

        if (beat.boopTime <= currentTime && !beat.booped)
        {
            Boop?.Invoke(beat);
            beat.booped = true;
        }
    }

    public bool IsExpired(double currentTime)
    {
        if (beat.destroyTime <= currentTime)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    public void PlayTick()
    {

    }

    public void Eaten()
    {
        Color c = text.color;
        c.a = 0f;
        text.color = c;
    }
}
