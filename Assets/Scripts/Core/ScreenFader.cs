using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ScreenFader : MonoBehaviour
{
    [SerializeField] private Image fadeImage;
    [SerializeField] private float fadeDuration = 0.25f;

    private void Awake()
    {
        if (fadeImage != null)
        {
            // Assegura que comencem transparents
            Color c = fadeImage.color;
            c.a = 0f;
            fadeImage.color = c;
        }
    }

    public IEnumerator FadeOutToBlack()
    {
        var player = FindObjectOfType<PlayerController2D>();
        if (player != null) player.LockMovement();
        yield return Fade(0f, 1f);
    }

    public IEnumerator FadeInFromBlack()
    {
        var player = FindObjectOfType<PlayerController2D>();
        if (player != null) player.UnlockMovement();
        yield return Fade(1f, 0f);
    }

    private IEnumerator Fade(float from, float to)
    {
        if (fadeImage == null) yield break;

        float t = 0f;
        Color c = fadeImage.color;

        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(from, to, t / fadeDuration);
            c.a = a;
            fadeImage.color = c;
            yield return null;
        }

        c.a = to;
        fadeImage.color = c;
    }
    private void Update()
{
    if (Input.GetKeyDown(KeyCode.F))
        StartCoroutine(FadeOutToBlack());

    if (Input.GetKeyDown(KeyCode.G))
        StartCoroutine(FadeInFromBlack());
}

}
