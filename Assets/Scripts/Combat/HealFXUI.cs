using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Efecte visual de curar/danyar. Dos modes:
///   - Show()        → text flotant al centre del canvas
///   - ShowAboveBar()→ text + partícules verdes ancorades sobre una imatge (barra HP)
/// </summary>
public class HealFXUI : MonoBehaviour
{
    // ── Mode genèric (centre-esquerra) ───────────────────────────────
    public static void Show(Transform canvasParent, string text, Color color, float duration = 1.8f)
    {
        var go = new GameObject("HealFX");
        go.transform.SetParent(canvasParent, false);
        go.transform.SetAsLastSibling();

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(560f, 110f);
        rt.anchoredPosition = new Vector2(-320f, -80f);

        var txt = AddText(go, text, 88f, color);

        var fx = go.AddComponent<HealFXUI>();
        fx.StartCoroutine(fx.AnimateFloat(rt, txt, duration));
    }

    public static void ShowHealFullscreen(Transform canvasParent)
    {
        var go = new GameObject("HealFX_Fullscreen");
        go.transform.SetParent(canvasParent, false);
        var fx = go.AddComponent<HealFXUI>();
        fx.StartCoroutine(fx.SpawnFullscreenParticles(canvasParent, new Color(0.25f, 1f, 0.35f), false));
    }

    public static void ShowSpeedFullscreen(Transform canvasParent)
    {
        var go = new GameObject("SpeedFX_Fullscreen");
        go.transform.SetParent(canvasParent, false);
        var fx = go.AddComponent<HealFXUI>();
        fx.StartCoroutine(fx.SpawnFullscreenParticles(canvasParent, new Color(1f, 0.9f, 0.15f), true));
    }

    // ── Mode barra HP → sobre la Image passada ──────────────────────
    public static void ShowAboveBar(Transform canvasParent, Image barImage, string text, Color color,
                                    float duration = 1.8f)
    {
        if (barImage == null) { Show(canvasParent, text, color, duration); return; }

        var go = new GameObject("HealFX_Bar");
        go.transform.SetParent(canvasParent, false);
        go.transform.SetAsLastSibling();

        // Copiem les àncores de la barra, però la col·loquem una mica per sobre
        var barRt  = barImage.GetComponent<RectTransform>();
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = barRt.anchorMin;
        rt.anchorMax = barRt.anchorMax;
        rt.offsetMin = barRt.offsetMin;
        rt.offsetMax = barRt.offsetMax;
        rt.anchoredPosition = barRt.anchoredPosition + new Vector2(0f, 80f);
        rt.sizeDelta = new Vector2(barRt.rect.width, 110f);

        var txt = AddText(go, text, 80f, color);

        var fx = go.AddComponent<HealFXUI>();
        // Partícules verdes
        fx.StartCoroutine(fx.SpawnParticles(canvasParent, rt.anchoredPosition, rt.anchorMin, rt.anchorMax, color));
        fx.StartCoroutine(fx.AnimateFloat(rt, txt, duration));
    }

    // ─── Animació ─────────────────────────────────────────────────────
    private IEnumerator AnimateFloat(RectTransform rt, TextMeshProUGUI txt, float duration)
    {
        Vector2 startPos = rt.anchoredPosition;
        Vector2 endPos   = startPos + new Vector2(0f, 160f);
        float   elapsed  = 0f;
        rt.localScale    = new Vector3(0.3f, 0.3f, 1f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            rt.anchoredPosition = Vector2.Lerp(startPos, endPos, Mathf.Sqrt(t));

            // Pop d'escala
            if      (t < 0.15f) rt.localScale = Vector3.one * Mathf.Lerp(0.3f, 1.2f, t / 0.15f);
            else if (t < 0.28f) rt.localScale = Vector3.one * Mathf.Lerp(1.2f, 1.0f, (t - 0.15f) / 0.13f);
            else                rt.localScale = Vector3.one;

            // Fade out a partir del 60%
            float alpha = t < 0.60f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.60f) / 0.40f);
            var c = txt.color; c.a = alpha; txt.color = c;

            yield return null;
        }

        Destroy(gameObject);
    }

    // ─── Partícules verdes ────────────────────────────────────────────
    private IEnumerator SpawnParticles(Transform canvasParent, Vector2 centerPos,
                                       Vector2 anchorMin, Vector2 anchorMax, Color col)
    {
        int count = 14;
        for (int i = 0; i < count; i++)
        {
            SpawnDot(canvasParent, centerPos, anchorMin, anchorMax, col, false, true);
            yield return new WaitForSeconds(0.04f);
        }
    }

    private IEnumerator SpawnFullscreenParticles(Transform canvasParent, Color col, bool isArrow)
    {
        int count = 25; // Menys partícules per escurçar la durada
        for (int i = 0; i < count; i++)
        {
            Vector2 randomPos = new Vector2(Random.Range(-1100f, 1100f), -650f);
            SpawnDot(canvasParent, randomPos, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), col, isArrow, false);
            yield return new WaitForSeconds(0.03f); 
        }
        Destroy(gameObject);
    }

    private void SpawnDot(Transform canvasParent, Vector2 center,
                           Vector2 anchorMin, Vector2 anchorMax, Color col, bool isArrow, bool useGravity)
    {
        var dGo = new GameObject(isArrow ? "Arrow" : "Dot");
        dGo.transform.SetParent(canvasParent, false);
        dGo.transform.SetAsLastSibling();

        var dRt = dGo.AddComponent<RectTransform>();
        dRt.anchorMin = anchorMin;
        dRt.anchorMax = anchorMax;
        
        if (isArrow)
        {
            dRt.sizeDelta = new Vector2(60f, 60f); // Mida petita
            var txt = dGo.AddComponent<TextMeshProUGUI>();
            txt.text = "▲";
            txt.fontSize = Random.Range(40f, 60f); 
            txt.color = new Color(col.r, col.g, col.b, 0.85f);
            txt.alignment = TextAlignmentOptions.Center;
            
            var fx = dGo.AddComponent<HealFXUI>();
            fx.StartCoroutine(fx.AnimateDot(dRt, null, txt, useGravity));
        }
        else
        {
            float size = Random.Range(15f, 30f); // Mida petita original
            dRt.sizeDelta = new Vector2(size, size);
            var dImg = dGo.AddComponent<Image>();
            dImg.color = new Color(col.r, col.g, col.b, 0.85f);
            
            var fx = dGo.AddComponent<HealFXUI>();
            fx.StartCoroutine(fx.AnimateDot(dRt, dImg, null, useGravity));
        }

        dRt.anchoredPosition = center + new Vector2(Random.Range(-40f, 40f), 0);
    }

    private IEnumerator AnimateDot(RectTransform rt, Image img, TextMeshProUGUI txt, bool useGravity)
    {
        float speed = Random.Range(900f, 1300f); // Molt ràpides
        Vector2 vel = new Vector2(Random.Range(-40f, 40f), speed);
        float life = Random.Range(1.0f, 1.5f); // Animació més curta
        float elapsed = 0f;

        while (elapsed < life)
        {
            elapsed += Time.deltaTime;
            rt.anchoredPosition += vel * Time.deltaTime;
            
            if (useGravity) vel.y -= 400f * Time.deltaTime;
            else vel.y += 100f * Time.deltaTime; // Acceleració cap amunt si no hi ha gravetat

            float alpha = Mathf.Lerp(0.9f, 0f, elapsed / life);
            if (img != null) { var c = img.color; c.a = alpha; img.color = c; }
            if (txt != null) { var c = txt.color; c.a = alpha; txt.color = c; }
            
            yield return null;
        }

        Destroy(gameObject);
    }

    // ─── Utils ────────────────────────────────────────────────────────
    private static TextMeshProUGUI AddText(GameObject go, string text, float size, Color color)
    {
        var txt = go.AddComponent<TextMeshProUGUI>();
        txt.text      = text;
        txt.fontSize  = size;
        txt.fontStyle = FontStyles.Bold;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color     = new Color(color.r, color.g, color.b, 1f);
        txt.raycastTarget = false;
        txt.textWrappingMode = TextWrappingModes.NoWrap;
        txt.overflowMode = TextOverflowModes.Overflow;

        // Contorn negre
        txt.fontSharedMaterial = Instantiate(txt.fontSharedMaterial);
        txt.fontSharedMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.28f);
        txt.fontSharedMaterial.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.04f, 0.04f, 0.04f, 1f));

        var font = LoadFont("8bitoperator_jve SDF");
        if (font == null) font = LoadFont("PixelOperator SDF");
        if (font != null) txt.font = font;

        return txt;
    }

    private static TMP_FontAsset LoadFont(string fontName)
    {
#if UNITY_EDITOR
        var f = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"Assets/Fonts/{fontName}.asset");
        if (f != null) return f;
        f = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            $"Assets/TextMesh Pro/Resources/Fonts & Materials/{fontName}.asset");
        if (f != null) return f;
#endif
        var loaded = Resources.Load<TMP_FontAsset>($"Fonts & Materials/{fontName}");
        if (loaded != null) return loaded;
        return Resources.Load<TMP_FontAsset>($"Fonts/{fontName}") ?? Resources.Load<TMP_FontAsset>(fontName);
    }
}
