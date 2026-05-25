using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sistema d'Efectes Visuals i Partícules Dinàmiques en el Canvas (HealFXUI).
/// Aquest component és el responsable de renderitzar feedbacks visuals fluids de tipus
/// curació (+HP) o dany tant a la pantalla completa del combat com a sobre de la barra de vida de la interfície.
/// 
/// DISSENY I INTEGRACIÓ DE VISUALS PROCEDIMENTALS DEL TFG:
/// - **Doble enquadrament adaptatiu**:
///   1. `Show()`: Text flotant amb pop d'escala al mig de la pantalla en danyar o esquivar.
///   2. `ShowAboveBar()`: Acobla textos de curació verds i brolla de partícules procedimentals just a sobre
///      d'un objecte RectTransform (com la barra de vida).
/// - **Partícules en pantalla completa**:
///   - `ShowHealFullscreen()`: Brolla de punts verds que pugen des de la part inferior.
///   - `ShowSpeedFullscreen()`: Brolla de fletxes triangulars grogues `▲` de velocitat.
/// - **Animació Pop-Up de Dany**:
///   - `AnimateFloat()`: Aplica una corba asíncrona de tipus Arrel Quadrada (Sqrt) per a un lliscament vertical,
///     combinada amb una escalabilitat Pop-Up (0.3 -> 1.2 -> 1.0) estil retro RPG.
/// - **Física de partícules procedimental**:
///   - `SpawnDot()`: Instancia dinàmicament objectes gràfics (amb imatge o lletra) i calcula forces lineals
///     i gravetat decreixent a temps real per no dependre de components complexos de ParticleSystem de Unity.
/// </summary>
public class HealFXUI : MonoBehaviour
{
    // =========================================================================
    // MODES DE PRESENTACIÓ DIRECTA (FACTORY METHODS)
    // =========================================================================
    /// <summary>
    /// Mostra un text de dany o estat flotant al mig de la pantalla que puja i s'esvaeix de forma suau.
    /// </summary>
    public static void Show(Transform canvasParent, string text, Color color, float duration = 1.8f)
    {
        var go = new GameObject("HealFX");
        go.transform.SetParent(canvasParent, false);
        go.transform.SetAsLastSibling(); // Força que es dibuixi per sobre de qualsevol element

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(560f, 110f);
        rt.anchoredPosition = new Vector2(-320f, -80f); // Posicionament adaptat al disseny general del combat

        var txt = AddText(go, text, 88f, color); // Generem text procedimental amb contorn

        var fx = go.AddComponent<HealFXUI>();
        fx.StartCoroutine(fx.AnimateFloat(rt, txt, duration));
    }

    /// <summary>
    /// Dispara una pluja de partícules verdes que s'eleven per tota la pantalla completa.
    /// </summary>
    public static void ShowHealFullscreen(Transform canvasParent)
    {
        var go = new GameObject("HealFX_Fullscreen");
        go.transform.SetParent(canvasParent, false);
        var fx = go.AddComponent<HealFXUI>();
        fx.StartCoroutine(fx.SpawnFullscreenParticles(canvasParent, new Color(0.25f, 1f, 0.35f), false));
    }

    /// <summary>
    /// Dispara una pluja de fletxes triangulars grogues a pantalla completa per indicar un augment de velocitat.
    /// </summary>
    public static void ShowSpeedFullscreen(Transform canvasParent)
    {
        var go = new GameObject("SpeedFX_Fullscreen");
        go.transform.SetParent(canvasParent, false);
        var fx = go.AddComponent<HealFXUI>();
        fx.StartCoroutine(fx.SpawnFullscreenParticles(canvasParent, new Color(1f, 0.9f, 0.15f), true));
    }

    /// <summary>
    /// Dibuixa el text flotant amb pop d'escala i una brolla de partícules exactament a sobre de la barra de vida.
    /// </summary>
    /// <param name="barImage">La imatge de referència visual de la barra de vida sobre la qual flotarem.</param>
    public static void ShowAboveBar(Transform canvasParent, Image barImage, string text, Color color, float duration = 1.8f)
    {
        if (barImage == null) { Show(canvasParent, text, color, duration); return; }

        var go = new GameObject("HealFX_Bar");
        go.transform.SetParent(canvasParent, false);
        go.transform.SetAsLastSibling();

        // Repliquem de forma dinàmica les àncores de la barra d'origen per garantir compatibilitat multiresolució
        var barRt  = barImage.GetComponent<RectTransform>();
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = barRt.anchorMin;
        rt.anchorMax = barRt.anchorMax;
        rt.offsetMin = barRt.offsetMin;
        rt.offsetMax = barRt.offsetMax;
        
        // El desplacem lleugerament cap amunt de la barra per no col·lisionar visualment amb la vida
        rt.anchoredPosition = barRt.anchoredPosition + new Vector2(0f, 80f);
        rt.sizeDelta = new Vector2(barRt.rect.width, 110f);

        var txt = AddText(go, text, 80f, color);

        var fx = go.AddComponent<HealFXUI>();
        // Spawnegem la brolla de partícules en la mateixa coordenada
        fx.StartCoroutine(fx.SpawnParticles(canvasParent, rt.anchoredPosition, rt.anchorMin, rt.anchorMax, color));
        fx.StartCoroutine(fx.AnimateFloat(rt, txt, duration));
    }

    // =========================================================================
    // LÒGICA D'ANIMACIÓ FLOTANT RPG (POP-UP EFFECT)
    // =========================================================================
    /// <summary>
    /// Corrutina d'elevació del text. Aplica lliscament vertical, sacsejada d'escala RPG
    /// i esvaïment gradual (fade-out) en el tram final de vida del feedback.
    /// </summary>
    private IEnumerator AnimateFloat(RectTransform rt, TextMeshProUGUI txt, float duration)
    {
        Vector2 startPos = rt.anchoredPosition;
        Vector2 endPos   = startPos + new Vector2(0f, 160f); // Es desplaçarà 160 píxels cap amunt
        float   elapsed  = 0f;
        rt.localScale    = new Vector3(0.3f, 0.3f, 1f); // Comença molt petita

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Lliscament amb corba d'arrel quadrada per desplaçar-se més ràpid al principi i frenar suau al final
            rt.anchoredPosition = Vector2.Lerp(startPos, endPos, Mathf.Sqrt(t));

            // ── SACSEJADA D'ESCALA POP-UP (0.3 -> 1.2 -> 1.0) ──
            if      (t < 0.15f) rt.localScale = Vector3.one * Mathf.Lerp(0.3f, 1.2f, t / 0.15f); // Pop explosiu inicial
            else if (t < 0.28f) rt.localScale = Vector3.one * Mathf.Lerp(1.2f, 1.0f, (t - 0.15f) / 0.13f); // Assentament suau
            else                rt.localScale = Vector3.one;

            // Fade out de transparència a partir del 60% del temps de vida de la corrutina
            float alpha = t < 0.60f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.60f) / 0.40f);
            var c = txt.color; c.a = alpha; txt.color = c;

            yield return null;
        }

        Destroy(gameObject); // Alliberament de memòria higiènic
    }

    // =========================================================================
    // CONTROL PROCEDIMENTAL D'SPAWNEIX DE PARTÍCULES
    // =========================================================================
    /// <summary>
    /// Spawneja una brolla asíncrona de 14 partícules a sobre de la barra de vida amb intervals curts.
    /// </summary>
    private IEnumerator SpawnParticles(Transform canvasParent, Vector2 centerPos, Vector2 anchorMin, Vector2 anchorMax, Color col)
    {
        int count = 14;
        for (int i = 0; i < count; i++)
        {
            SpawnDot(canvasParent, centerPos, anchorMin, anchorMax, col, false, true);
            yield return new WaitForSeconds(0.04f); // Espaiat dinàmic
        }
    }

    /// <summary>
    /// Spawneja partícules asíncronament dispersades per tota l'amplitud horitzontal de la pantalla.
    /// </summary>
    private IEnumerator SpawnFullscreenParticles(Transform canvasParent, Color col, bool isArrow)
    {
        int count = 25; 
        for (int i = 0; i < count; i++)
        {
            // Coordenades aleatòries horitzontals sortint des d'una alçada de -650f (fora per baix)
            Vector2 randomPos = new Vector2(Random.Range(-1100f, 1100f), -650f);
            SpawnDot(canvasParent, randomPos, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), col, isArrow, false);
            yield return new WaitForSeconds(0.03f); 
        }
        Destroy(gameObject);
    }

    /// <summary>
    /// Instancia procedimentalment una partícula en forma d'imatge dinàmica (Dot) o lletra tipogràfica (Arrow).
    /// </summary>
    private void SpawnDot(Transform canvasParent, Vector2 center, Vector2 anchorMin, Vector2 anchorMax, Color col, bool isArrow, bool useGravity)
    {
        var dGo = new GameObject(isArrow ? "Arrow" : "Dot");
        dGo.transform.SetParent(canvasParent, false);
        dGo.transform.SetAsLastSibling();

        var dRt = dGo.AddComponent<RectTransform>();
        dRt.anchorMin = anchorMin;
        dRt.anchorMax = anchorMax;
        
        // ── PARTÍCULES TIPOGRÀFIQUES (Símbols del teclat) ──
        if (isArrow)
        {
            dRt.sizeDelta = new Vector2(60f, 60f);
            var txt = dGo.AddComponent<TextMeshProUGUI>();
            txt.text = "▲"; // Triangles cap amunt de velocitat
            txt.fontSize = Random.Range(40f, 60f); 
            txt.color = new Color(col.r, col.g, col.b, 0.85f);
            txt.alignment = TextAlignmentOptions.Center;
            
            var fx = dGo.AddComponent<HealFXUI>();
            fx.StartCoroutine(fx.AnimateDot(dRt, null, txt, useGravity));
        }
        // ── PARTÍCULES GRÀFIQUES (Quadres / Punts) ──
        else
        {
            float size = Random.Range(15f, 30f); 
            dRt.sizeDelta = new Vector2(size, size);
            var dImg = dGo.AddComponent<Image>();
            dImg.color = new Color(col.r, col.g, col.b, 0.85f);
            
            var fx = dGo.AddComponent<HealFXUI>();
            fx.StartCoroutine(fx.AnimateDot(dRt, dImg, null, useGravity));
        }

        // Variabilitat de spawn horitzontal aleatòria perquè no brollin exactament al mateix píxel
        dRt.anchoredPosition = center + new Vector2(Random.Range(-40f, 40f), 0);
    }

    /// <summary>
    /// Aplica forces visuals reals, gravetat, acceleració i fricció sobre la partícula asíncronament.
    /// </summary>
    private IEnumerator AnimateDot(RectTransform rt, Image img, TextMeshProUGUI txt, bool useGravity)
    {
        float speed = Random.Range(900f, 1300f); 
        Vector2 vel = new Vector2(Random.Range(-40f, 40f), speed); // Velocitat d'impuls inicial cap amunt
        float life = Random.Range(1.0f, 1.5f); // Temps de vida asíncron
        float elapsed = 0f;

        while (elapsed < life)
        {
            elapsed += Time.deltaTime;
            rt.anchoredPosition += vel * Time.deltaTime; // Actualització de posició real
            
            // ── COMPORTAMENT FÍSIC ADAPTATIU ──
            if (useGravity) 
            {
                // Si s'aplica gravetat, la partícula és frenada i cau cap avall de forma parabòlica
                vel.y -= 400f * Time.deltaTime; 
            }
            else 
            {
                // Si no, accelera cap amunt de forma constant com una bombolla o gas
                vel.y += 100f * Time.deltaTime; 
            }

            // Desvaniment transparent gradual asíncron
            float alpha = Mathf.Lerp(0.9f, 0f, elapsed / life);
            if (img != null) { var c = img.color; c.a = alpha; img.color = c; }
            if (txt != null) { var c = txt.color; c.a = alpha; txt.color = c; }
            
            yield return null;
        }

        Destroy(gameObject);
    }

    // =========================================================================
    // UTILS TIPOGRÀFICS (Generació de Text amb Outline de Alta Visibilitat)
    // =========================================================================
    /// <summary>
    /// Genera procedimentalment el component TextMeshPro, li assigna la font tipogràfica
    /// del TFG i li col·loca un contorn negre doble (Outline) per garantir visibilitat perfecta
    /// sobre qualsevol mena de fons de joc (clar o fosc).
    /// </summary>
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

        // ── CONFIGURACIÓ DE CONTORN DE SEGURETAT DE ALTA VISIBILITAT ──
        txt.fontSharedMaterial = Instantiate(txt.fontSharedMaterial); // Duplicat de material per a no embrutar altres textos
        txt.fontSharedMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.28f); // Contorn de gran gruix (0.28)
        txt.fontSharedMaterial.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.04f, 0.04f, 0.04f, 1f));

        var font = LoadFont("determination SDF");
        if (font == null) font = LoadFont("PixelOperator SDF");
        if (font == null) font = LoadFont("8bitoperator_jve SDF");
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
