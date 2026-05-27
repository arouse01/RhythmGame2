using Mono.Cecil;
using UnityEngine;
using UnityEngine.Audio;
using static Unity.VisualScripting.Member;

public class Sound
{
    public string name;
    public AudioClip clip;

    [Range(0f, 1f)]
    public float volume = 0.7f;

    private AudioSource source;
}

public class AudioManager : MonoBehaviour
{

    [SerializeField] private AudioSource[] laneLSources;
    [SerializeField] private AudioSource[] laneHSources;
    [SerializeField] private AudioSource immediateSource;

    private int nextSourceL = 0;
    private int nextSourceH = 0;

    public void ScheduleBeatL(AudioClip clip, double dspTime)
    {

        AudioSource lSource = laneLSources[nextSourceL];
        nextSourceL = (nextSourceL + 1) % laneLSources.Length;

        lSource.clip = clip;
        lSource.PlayScheduled(dspTime);
    }

    public void ScheduleBeatH(AudioClip clip, double dspTime)
    {
        AudioSource hSource = laneHSources[nextSourceH];
        nextSourceH = (nextSourceH + 1) % laneHSources.Length;

        hSource.clip = clip;
        hSource.PlayScheduled(dspTime);
    }
    
    //void PlayScheduled(AudioClip sound, double dspTime)
    //{
    //    source.clip = clip;
    //    source.PlayScheduled(dspTime);
    //}

    public void PlayImmediate(AudioClip clip)
    {
        immediateSource.clip = clip;
        immediateSource.PlayOneShot(clip);
    }

    public void PauseAll()
    {

    }
    
    public void StopAll()
    {
        foreach (AudioSource source in laneLSources)
        {
            source.Stop();
        }
        foreach (AudioSource source in laneHSources)
        {
            source.Stop();
        }
        immediateSource.Stop();
    }
}
