using System.Collections;
using UnityEngine;

public class SplitTransition : MonoBehaviour
{
    [SerializeField] RectTransform left;
    [SerializeField] RectTransform right;
    [SerializeField] float closeDuration = 0.35f;
    [SerializeField] float openDuration = 0.35f;

    // distància fora de pantalla (px). Com més gran, més segur.
    [SerializeField] float offscreen = 2500f;

    void Reset()
    {
        // Intenta auto-assignar si els noms coincideixen
        var t = transform;
        if (t.childCount >= 2)
        {
            left = t.Find("Left") as RectTransform;
            right = t.Find("Right") as RectTransform;
        }
    }

    public IEnumerator Close()
    {
        // Left ve des de fora esquerra -> centre, Right des de fora dreta -> centre
        Vector2 leftFrom = new Vector2(-offscreen, 0);
        Vector2 leftTo   = Vector2.zero;

        Vector2 rightFrom = new Vector2(offscreen, 0);
        Vector2 rightTo   = Vector2.zero;

        // Posa'ls al punt inicial
        left.anchoredPosition = leftFrom;
        right.anchoredPosition = rightFrom;

        yield return Move(leftFrom, leftTo, rightFrom, rightTo, closeDuration);
    }

    public IEnumerator Open()
    {
        Vector2 leftFrom = Vector2.zero;
        Vector2 leftTo   = new Vector2(-offscreen, 0);

        Vector2 rightFrom = Vector2.zero;
        Vector2 rightTo   = new Vector2(offscreen, 0);

        yield return Move(leftFrom, leftTo, rightFrom, rightTo, openDuration);

        Destroy(gameObject); // neteja
    }

    IEnumerator Move(Vector2 l0, Vector2 l1, Vector2 r0, Vector2 r1, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            float a = t / duration;
            // easing suau (smoothstep)
            a = a * a * (3f - 2f * a);

            left.anchoredPosition = Vector2.LerpUnclamped(l0, l1, a);
            right.anchoredPosition = Vector2.LerpUnclamped(r0, r1, a);

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        left.anchoredPosition = l1;
        right.anchoredPosition = r1;
    }
}
