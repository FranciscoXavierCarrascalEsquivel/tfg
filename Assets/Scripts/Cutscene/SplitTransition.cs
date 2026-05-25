using System.Collections;
using UnityEngine;

/// <summary>
/// Gestiona l'animació de transició gràfica de tall o tancament lateral de pantalla (Split Transition).
/// És una alternativa simplificada en dues meitats (esquerra/dreta RectTransform) utilitzant
/// funcions de suavitzat (smoothstep) natiu, i temporitzat basat en unscaledDeltaTime per a funcionar
/// independentment de si el temps del joc està pausat (Time.timeScale = 0).
/// </summary>
public class SplitTransition : MonoBehaviour
{
    [Header("Panells de la Transició (UI)")]
    [SerializeField] private RectTransform left;  // RectTransform esquerre
    [SerializeField] private RectTransform right; // RectTransform dret
    
    [Header("Temps de Transició")]
    [SerializeField] private float closeDuration = 0.35f; // Temps de tancament lateral
    [SerializeField] private float openDuration = 0.35f;  // Temps d'obertura cap als costats

    [Tooltip("Distància en píxels fora de la pantalla per col·locar els panells invisibles.")]
    [SerializeField] private float offscreen = 2500f;

    private void Reset()
    {
        // Provem d'auto-assignar dinàmicament els fills per nom per a facilitar la creació de prefabs
        var t = transform;
        if (t.childCount >= 2)
        {
            left = t.Find("Left") as RectTransform;
            right = t.Find("Right") as RectTransform;
        }
    }

    /// <summary>
    /// Corrutina per a reproduir el tancament (els panells llisquen cap al centre fins a col·lidir).
    /// </summary>
    public IEnumerator Close()
    {
        Vector2 leftFrom = new Vector2(-offscreen, 0);
        Vector2 leftTo   = Vector2.zero;

        Vector2 rightFrom = new Vector2(offscreen, 0);
        Vector2 rightTo   = Vector2.zero;

        // Situem els elements als extrems
        left.anchoredPosition = leftFrom;
        right.anchoredPosition = rightFrom;

        yield return Move(leftFrom, leftTo, rightFrom, rightTo, closeDuration);
    }

    /// <summary>
    /// Corrutina per a reproduir l'obertura (els panells es separen del centre cap als extrems) i destruir el canvas temporal.
    /// </summary>
    public IEnumerator Open()
    {
        Vector2 leftFrom = Vector2.zero;
        Vector2 leftTo   = new Vector2(-offscreen, 0);

        Vector2 rightFrom = Vector2.zero;
        Vector2 rightTo   = new Vector2(offscreen, 0);

        yield return Move(leftFrom, leftTo, rightFrom, rightTo, openDuration);

        // Alliberem l'objecte en acabar l'obertura
        Destroy(gameObject); 
    }

    /// <summary>
    /// Corrutina genèrica de moviment d'interpolació basat en una corba smoothstep de suavitzat.
    /// </summary>
    private IEnumerator Move(Vector2 l0, Vector2 l1, Vector2 r0, Vector2 r1, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            float a = t / duration;
            // Funció smoothstep matemàtica per a suavitzar acceleració i desacceleració gràfica
            a = a * a * (3f - 2f * a);

            left.anchoredPosition = Vector2.LerpUnclamped(l0, l1, a);
            right.anchoredPosition = Vector2.LerpUnclamped(r0, r1, a);

            t += Time.unscaledDeltaTime; // Unscaled per funcionar en pantalles de Pausa o càrrega
            yield return null;
        }

        left.anchoredPosition = l1;
        right.anchoredPosition = r1;
    }
}
