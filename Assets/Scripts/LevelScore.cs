using System.Collections;
using UnityEngine;
using TMPro;

public class LevelScore : MonoBehaviour
{
    // Inspired by https://www.youtube.com/watch?v=7p7qWDxuADs
    [SerializeField] Star[] Stars;

    [SerializeField] float EnlargeScale = 1.5f;
    [SerializeField] float ShrinkScale = 1f;
    [SerializeField] float EnlargeDuration = 0.25f;
    [SerializeField] float ShrinkDuration = 0.25f;

    public TextMeshProUGUI ScoreText;
    public string scoreString1;
    public string scoreString2;
    public string scoreString3;

    //void Start()
    //{
    //    //ShowStars(5);
    //}

    public void HideStars()
    {
        foreach (Star star in Stars)
        {
            star.LevelStar.transform.localScale = Vector3.zero;
        }
        ScoreText.transform.localScale = Vector3.zero;
        ScoreText.enabled = false;
    }
    public void ShowStars(int numberOfStars)
    {
        StartCoroutine(ShowStarsRoutine(numberOfStars));
    }

    private IEnumerator ShowStarsRoutine(int numberOfStars)
    {
        foreach (Star star in Stars)
        {
            star.LevelStar.transform.localScale = Vector3.zero;
        }
        ScoreText.transform.localScale = Vector3.zero;

        for (int i = 0; i < numberOfStars; i++)
        {
            yield return StartCoroutine(EnlargeAndShrinkStar(Stars[i]));
        }
        switch (numberOfStars)
        {
            case 3:
                ScoreText.SetText(scoreString3);
                break;
            case 2:
                ScoreText.SetText(scoreString2);
                break;
            case 1:
                ScoreText.SetText(scoreString1);
                break;
        }
        ScoreText.enabled = true;
        yield return StartCoroutine(EnlargeAndShrinkText());
    }

    private IEnumerator EnlargeAndShrinkStar(Star star)
    {
        yield return StartCoroutine(ChangeStarScale(star, EnlargeScale, EnlargeDuration));
        yield return StartCoroutine(ChangeStarScale(star, ShrinkScale, ShrinkDuration));
    }

    private IEnumerator ChangeStarScale(Star star, float targetScale, float duration)
    {
        Vector3 initialScale = star.LevelStar.transform.localScale;
        Vector3 finalScale = new(targetScale, targetScale, targetScale);

        float elapsedTime = 0;

        while (elapsedTime < duration)
        {
            star.LevelStar.transform.localScale = Vector3.Lerp(initialScale, finalScale, (elapsedTime / duration));
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        star.LevelStar.transform.localScale = finalScale;
    }

    private IEnumerator EnlargeAndShrinkText()
    {
        yield return StartCoroutine(ChangeTextScale(EnlargeScale, EnlargeDuration));
        yield return StartCoroutine(ChangeTextScale(ShrinkScale, ShrinkDuration));
    }

    private IEnumerator ChangeTextScale(float targetScale, float duration)
    {
        Vector3 initialScale = ScoreText.transform.localScale;
        Vector3 finalScale = new(targetScale, targetScale, targetScale);

        float elapsedTime = 0;

        while (elapsedTime < duration)
        {
            ScoreText.transform.localScale = Vector3.Lerp(initialScale, finalScale, (elapsedTime / duration));
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        ScoreText.transform.localScale = finalScale;
    }

}
