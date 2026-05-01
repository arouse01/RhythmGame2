using UnityEngine;
using UnityEngine.UI;

public class Star : MonoBehaviour
{
    // Inspired by https://www.youtube.com/watch?v=7p7qWDxuADs
    public Image LevelStar;

    private void Awake()
    {
        LevelStar = GetComponent<Image>();
        LevelStar.transform.localScale = Vector3.zero;
    }
}