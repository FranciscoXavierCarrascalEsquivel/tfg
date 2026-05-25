using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Component encarregat de realitzar efectes de fosa gràfica a negre de la pantalla (ScreenFader).
/// S'utilitza de forma generalitzada per a suavitzar transicions, canvis de zona o càrregues de combats.
/// Incorpora de manera segura el bloqueig/desbloqueig de control de moviment del jugador
/// per a evitar desplaçaments accidentals mentre la pantalla roman a les fosques.
/// </summary>
public class ScreenFader : MonoBehaviour
{
    [Header("Elements Visuals")]
    [SerializeField] private Image fadeImage;      // Referència a la imatge plana UI (negra) que cobrirà la pantalla
    [SerializeField] private float fadeDuration = 0.25f; // Durada preestablerta de la fosa en segons

    private void Awake()
    {
        if (fadeImage != null)
        {
            // Ens assegurem que en arrencar el joc la pantalla sigui completament visible (opacitat a 0)
            Color c = fadeImage.color;
            c.a = 0f;
            fadeImage.color = c;
        }
    }

    /// <summary>
    /// Corrutina per realitzar una fosa gradual des de visible cap a negre absolut (FadeOut).
    /// Congela preventivament els controls del personatge.
    /// </summary>
    public IEnumerator FadeOutToBlack()
    {
        var player = FindFirstObjectByType<PlayerController2D>();
        if (player != null) player.LockMovement();
        
        yield return Fade(0f, 1f);
    }

    /// <summary>
    /// Corrutina per realitzar una fosa gradual des de negre absolut cap a visible (FadeIn).
    /// Desbloqueja els controls del personatge en finalitzar.
    /// </summary>
    public IEnumerator FadeInFromBlack()
    {
        var player = FindFirstObjectByType<PlayerController2D>();
        if (player != null) player.UnlockMovement();
        
        yield return Fade(1f, 0f);
    }

    /// <summary>
    /// Corrutina genèrica que realitza la interpolació del canal Alfa (opacitat) de la imatge de fosa.
    /// </summary>
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
}
