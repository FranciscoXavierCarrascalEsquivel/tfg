using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class SplitSnapshot : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private RawImage left;
    [SerializeField] private RawImage right;

    [Header("Animation")]
    [SerializeField] private float offscreen = 2500f; // prou gran per qualsevol resolució

    private Texture2D snapshot;

    // Guardem amplada de cada meitat per fer clamp (evitar buits)
    private float halfWidthPx;

    // =========================
    // PUBLIC API
    // =========================

    public void SetSnapshot(Texture2D tex)
    {
        snapshot = tex;

        left.texture = snapshot;
        right.texture = snapshot;

        // Cada meitat mostra mitja textura
        left.uvRect  = new Rect(0f, 0f, 0.5f, 1f);
        right.uvRect = new Rect(0.5f, 0f, 0.5f, 1f);

        // Posició inicial
        var lrt = (RectTransform)left.transform;
        var rrt = (RectTransform)right.transform;
        lrt.anchoredPosition = Vector2.zero;
        rrt.anchoredPosition = Vector2.zero;

        // Amplada en píxels de cada meitat a pantalla (per clamp)
        halfWidthPx = lrt.rect.width; // hauria de ser Screen.width/2 si anchors estan bé
        if (halfWidthPx <= 0f) halfWidthPx = Screen.width * 0.5f; // fallback
    }

    public IEnumerator PlayOpen()
    {
        var lrt = (RectTransform)left.transform;
        var rrt = (RectTransform)right.transform;

        // Posicions base
        Vector2 lBase = Vector2.zero;
        Vector2 rBase = Vector2.zero;

        // ===== CONFIG =====
        float preOpenDistance = 120f;  // obertura inicial forta
        float preOpenTime = 0.36f;     // velocitat del primer cop
        float holdTime = 0.75f;        // temps de resistència
        float snapTime = 0.8f;         // obertura final

        float shakeStrength = 10f;     // força del tembleque
        float shakeSpeed = 20f;        // velocitat del tembleque

        // Destins
        Vector2 lPre   = new Vector2(-preOpenDistance, 0);
        Vector2 rPre   = new Vector2( preOpenDistance, 0);

        Vector2 lFinal = new Vector2(-offscreen, 0);
        Vector2 rFinal = new Vector2( offscreen, 0);

        // Límit de shake per no crear “gaps”:
        // - A la fase pre/hold, la pantalla ja està oberta "preOpenDistance".
        // - Si shakeX supera aquesta obertura, al centre pot aparèixer un buit.
        // → clamp a una fracció segura de preOpenDistance.
        float maxSafeShakeX = Mathf.Max(0f, preOpenDistance * 0.45f); // 45% és segur
        // També evitem que el shake sigui tan gran que arrossegui mitja textura fora.
        maxSafeShakeX = Mathf.Min(maxSafeShakeX, halfWidthPx * 0.25f); // extra seguretat

        // =========================
        // 1) COP INICIAL (ANTICIPATION)
        // =========================
        float t = 0f;
        while (t < preOpenTime)
        {
            float a = EaseOutCubic(t / preOpenTime);

            float shakeX = GetShakeX(Time.unscaledTime, shakeStrength, shakeSpeed, maxSafeShakeX);
            Vector2 shake = new Vector2(shakeX, 0f);

            lrt.anchoredPosition = Vector2.LerpUnclamped(lBase, lPre, a) + shake;
            rrt.anchoredPosition = Vector2.LerpUnclamped(rBase, rPre, a) + shake;

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        // =========================
        // 2) HOLD + TEMBLEQUE (FORÇA)
        // =========================
        float holdT = 0f;
        while (holdT < holdTime)
        {
            float shakeX = GetShakeX(Time.unscaledTime, shakeStrength * 1.25f, shakeSpeed, maxSafeShakeX);
            Vector2 shake = new Vector2(shakeX, 0f);

            lrt.anchoredPosition = lPre + shake;
            rrt.anchoredPosition = rPre + shake;

            holdT += Time.unscaledDeltaTime;
            yield return null;
        }

        // =========================
        // 3) SNAP OPEN (EXPLOSIÓ)
        // =========================
        t = 0f;
        while (t < snapTime)
        {
            float a = EaseInCubic(t / snapTime);

            lrt.anchoredPosition = Vector2.LerpUnclamped(lPre, lFinal, a);
            rrt.anchoredPosition = Vector2.LerpUnclamped(rPre, rFinal, a);

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        lrt.anchoredPosition = lFinal;
        rrt.anchoredPosition = rFinal;

        Cleanup();
        Destroy(gameObject);
    }

    // =========================
    // HELPERS
    // =========================

    private static float EaseOutCubic(float x)
    {
        x = Mathf.Clamp01(x);
        float p = 1f - x;
        return 1f - p * p * p;
    }

    private static float EaseInCubic(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * x;
    }

    // Tremolor NOMÉS en X i amb clamp per evitar buits
    private static float GetShakeX(float time, float strength, float speed, float maxAbs)
    {
        float sx = (Mathf.PerlinNoise(time * speed, 0.123f) - 0.5f) * 2f;
        float v = sx * strength;
        if (maxAbs > 0f) v = Mathf.Clamp(v, -maxAbs, maxAbs);
        return v;
    }

    private void Cleanup()
    {
        if (snapshot != null)
        {
            Destroy(snapshot);
            snapshot = null;
        }
    }
}
