using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class UIElementFade : MonoBehaviour
{
    [Header("Text")]
    public List<TMP_Text> textTargets = new List<TMP_Text>();

    [Header("Image")]
    public List<Image> imageTargets = new List<Image>();

    [Header("Settings")]
    [Range(0, 255)] public int textFadeInTargetAlpha = 255;
    [Range(0, 255)] public int imageFadeInTargetAlpha = 255;
    public float fadeInDuration = 1f;
    public float remainVisibleDuration = 1f;
    [Range(0, 255)] public int textFadeOutTargetAlpha = 0;
    [Range(0, 255)] public int imageFadeOutTargetAlpha = 0;
    public float fadeOutDuration = 1f;

    Coroutine fadeRoutine;

    public void UIFadeEvent()
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(UIFadeRoutine());
    }

    IEnumerator UIFadeRoutine()
    {
        float textStartAlpha = GetCurrentTextAlpha01();
        float imageStartAlpha = GetCurrentImageAlpha01();

        float textFadeInTarget01 = textFadeInTargetAlpha / 255f;
        float imageFadeInTarget01 = imageFadeInTargetAlpha / 255f;

        if (fadeInDuration <= 0f)
        {
            SetTextAlpha01(textFadeInTarget01);
            SetImageAlpha01(imageFadeInTarget01);
        }
        else
        {
            float t = 0f;
            while (t < fadeInDuration)
            {
                t += Time.deltaTime;
                float lerp = Mathf.Clamp01(t / fadeInDuration);

                float ta = Mathf.Lerp(textStartAlpha, textFadeInTarget01, lerp);
                float ia = Mathf.Lerp(imageStartAlpha, imageFadeInTarget01, lerp);

                SetTextAlpha01(ta);
                SetImageAlpha01(ia);
                yield return null;
            }

            SetTextAlpha01(textFadeInTarget01);
            SetImageAlpha01(imageFadeInTarget01);
        }

        if (remainVisibleDuration > 0f)
            yield return new WaitForSeconds(remainVisibleDuration);

        float textFadeOutStartAlpha = GetCurrentTextAlpha01();
        float imageFadeOutStartAlpha = GetCurrentImageAlpha01();

        float textFadeOutTarget01 = textFadeOutTargetAlpha / 255f;
        float imageFadeOutTarget01 = imageFadeOutTargetAlpha / 255f;

        if (fadeOutDuration <= 0f)
        {
            SetTextAlpha01(textFadeOutTarget01);
            SetImageAlpha01(imageFadeOutTarget01);
        }
        else
        {
            float t2 = 0f;
            while (t2 < fadeOutDuration)
            {
                t2 += Time.deltaTime;
                float lerp = Mathf.Clamp01(t2 / fadeOutDuration);

                float ta = Mathf.Lerp(textFadeOutStartAlpha, textFadeOutTarget01, lerp);
                float ia = Mathf.Lerp(imageFadeOutStartAlpha, imageFadeOutTarget01, lerp);

                SetTextAlpha01(ta);
                SetImageAlpha01(ia);
                yield return null;
            }

            SetTextAlpha01(textFadeOutTarget01);
            SetImageAlpha01(imageFadeOutTarget01);
        }

        fadeRoutine = null;
    }

    float GetCurrentTextAlpha01()
    {
        for (int i = 0; i < textTargets.Count; i++)
            if (textTargets[i] != null)
                return textTargets[i].color.a;

        return 1f;
    }

    float GetCurrentImageAlpha01()
    {
        for (int i = 0; i < imageTargets.Count; i++)
            if (imageTargets[i] != null)
                return imageTargets[i].color.a;

        return 1f;
    }

    void SetTextAlpha01(float alpha01)
    {
        for (int i = 0; i < textTargets.Count; i++)
        {
            TMP_Text t = textTargets[i];
            if (t == null) continue;
            Color c = t.color;
            c.a = alpha01;
            t.color = c;
        }
    }

    void SetImageAlpha01(float alpha01)
    {
        for (int i = 0; i < imageTargets.Count; i++)
        {
            Image img = imageTargets[i];
            if (img == null) continue;
            Color c = img.color;
            c.a = alpha01;
            img.color = c;
        }
    }
}
