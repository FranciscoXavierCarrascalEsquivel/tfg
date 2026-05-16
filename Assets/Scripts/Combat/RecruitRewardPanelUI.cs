using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Panell que es mostra un cop s'ha completat la barra de reclutament d'un enemic.
/// Mostra l'sprite de recompensa en gran i el text personalitzat.
/// Cridada: RecruitRewardPanelUI.Create(parent, sprite, text, onDone);
/// </summary>
public class RecruitRewardPanelUI : MonoBehaviour
{
    // ─── Factory ────────────────────────────────────────────────────
    public static RecruitRewardPanelUI Create(
        Transform canvasParent,
        Sprite rewardSprite,
        string rewardText,
        string enemyName,
        AudioClip rewardSound,
        Action onDone)
    {
        var go = new GameObject("RecruitRewardPanel");
        go.transform.SetParent(canvasParent, false);
        go.transform.SetAsLastSibling();

        var panel = go.AddComponent<RecruitRewardPanelUI>();
        panel.rewardSprite = rewardSprite;
        panel.rewardText   = rewardText;
        panel.enemyName    = enemyName;
        panel.rewardSound  = rewardSound;
        panel.onDone       = onDone;
        return panel;
    }

    // ── Dades ────────────────────────────────────────────────────────
    private Sprite rewardSprite;
    private string rewardText;
    private string enemyName;
    private AudioClip rewardSound;
    private Action onDone;

    private bool waitingForInput = false;
    private RectTransform cardRect;
    private TMP_FontAsset pixelFont;
    private RectTransform ePromptRT;
    private RectTransform rewardIconRT;

    private void Start()
    {
        // Netegem accents abans de construir per compatibilitat amb la font
        rewardText = RemoveAccents(rewardText);
        enemyName  = RemoveAccents(enemyName);

        Build();
        
        // Reproduir so de recompensa si n'hi ha
        if (rewardSound != null)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.clip = rewardSound;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.Play();
        }

        StartCoroutine(AnimateIn());
    }

    private void Update()
    {
        // Animar el botó E (pulsació visual cap avall, sense rotació lateral)
        if (waitingForInput && ePromptRT != null)
        {
            float cycle = Time.unscaledTime * 4.5f;
            bool isPressed = (cycle % 2f) > 1.4f;
            var top = ePromptRT.Find("Top") as RectTransform;
            if (top != null) top.anchoredPosition = isPressed ? Vector2.zero : new Vector2(0f, 4f);
        }

        // Icona de recompensa estàtica per petició de l'usuari
        if (rewardIconRT != null)
        {
            rewardIconRT.anchoredPosition = new Vector2(0f, -20f);
            rewardIconRT.localScale = Vector3.one;
        }

        if (!waitingForInput) return;
        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
        {
            waitingForInput = false;
            StartCoroutine(AnimateOut());
        }
    }

    // ────────────────────────────────────────────────────────────────
    // Construcció
    // ────────────────────────────────────────────────────────────────
    private void Build()
    {
        pixelFont = LoadFont("determination SDF");
        if (pixelFont == null) pixelFont = LoadFont("PixelOperator SDF");
        if (pixelFont == null) pixelFont = LoadFont("8bitoperator_jve SDF");
        if (pixelFont == null) pixelFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

        // ── Overlay pantalla completa ─────────────────────────────────
        var selfRT = gameObject.AddComponent<RectTransform>();
        selfRT.anchorMin = Vector2.zero;
        selfRT.anchorMax = Vector2.one;
        selfRT.offsetMin = Vector2.zero;
        selfRT.offsetMax = Vector2.zero;

        // Fons fosc semi-transparent (més fosc per destacar la card)
        var bgImg = gameObject.AddComponent<Image>();
        bgImg.color = new Color(0.01f, 0.005f, 0.03f, 0.92f);

        // ── Sombra de la targeta (Shadow Frame) ──────────────────────
        float cardH = 580f;
        var shadow = NewChild("Shadow", transform);
        var shadowRT = shadow.GetComponent<RectTransform>();
        shadowRT.anchorMin = new Vector2(0.15f, 0.5f);
        shadowRT.anchorMax = new Vector2(0.85f, 0.5f);
        shadowRT.offsetMin = new Vector2(12f, -cardH / 2f - 12f);
        shadowRT.offsetMax = new Vector2(12f, cardH / 2f - 12f);
        shadow.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);

        // ── Targeta central ──────────────────────────────────────────
        var card = NewChild("Card", transform);
        cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.15f, 0.5f);
        cardRect.anchorMax = new Vector2(0.85f, 0.5f);
        cardRect.offsetMin = new Vector2(0f, -cardH / 2f);
        cardRect.offsetMax = new Vector2(0f, cardH / 2f);
        cardRect.anchoredPosition = new Vector2(0f, 0f);

        // Fons de la targeta (gradat subtil)
        card.AddComponent<Image>().color = new Color(0.08f, 0.07f, 0.18f, 1f);

        // ── Contorn pixel (groc brillant) ───────────────────────────
        Color goldColor = new Color(1f, 0.90f, 0.15f, 1f);
        AddPixelBorder(card, goldColor, 8f);
        AddCornerSquares(card, goldColor, 16f);

        // ── Capçalera Reclutament ──────────────────────────────────────
        float headerH = 110f;
        var header = NewChild("Header", card.transform);
        var hRT = header.GetComponent<RectTransform>();
        hRT.anchorMin = new Vector2(0f, 1f);
        hRT.anchorMax = new Vector2(1f, 1f);
        hRT.offsetMin = new Vector2(0f, -headerH);
        hRT.offsetMax = Vector2.zero;
        header.AddComponent<Image>().color = new Color(0.28f, 0.12f, 0.55f, 1f);
        
        // NOU: Resseguit per la banda lila de dalt
        AddPixelBorder(header, goldColor, 4f);

        var titleGo = NewChild("Title", header.transform);
        var titleRT = titleGo.GetComponent<RectTransform>();
        titleRT.anchorMin = Vector2.zero;
        titleRT.anchorMax = Vector2.one;
        titleRT.offsetMin = new Vector2(0f, 5f);
        titleRT.offsetMax = Vector2.zero;
        var titleTxt = titleGo.AddComponent<TextMeshProUGUI>();
        SetFont(titleTxt, 54f, new Color(1f, 0.95f, 0.4f), FontStyles.Bold, TextAlignmentOptions.Center);
        titleTxt.text = $"*  COLLECTION COMPLETE!  *";
        
        // Sub-títol (nom enemic)
        var subGo = NewChild("SubTitle", card.transform);
        var subRT = subGo.GetComponent<RectTransform>();
        subRT.anchorMin = new Vector2(0f, 1f); subRT.anchorMax = new Vector2(1f, 1f);
        subRT.offsetMin = new Vector2(20f, -(headerH + 60f));
        subRT.offsetMax = new Vector2(-20f, -headerH);
        var subTxt = subGo.AddComponent<TextMeshProUGUI>();
        SetFont(subTxt, 34f, new Color(0.85f, 0.85f, 0.95f), FontStyles.Normal, TextAlignmentOptions.Center);
        subTxt.text = $"You've obtained {enemyName.ToUpper()}'s reward";

        // ── Backlight Llum (Glow) ───────────────────────────────────
        var glowGo = NewChild("Glow", card.transform);
        var glowRT = glowGo.GetComponent<RectTransform>();
        glowRT.anchorMin = glowRT.anchorMax = new Vector2(0.5f, 0.5f);
        glowRT.sizeDelta = new Vector2(400f, 400f);
        glowRT.anchoredPosition = new Vector2(0f, -20f);
        var glowImg = glowGo.AddComponent<Image>();
        glowImg.color = new Color(1f, 0.9f, 0.45f, 0.35f);
        glowImg.sprite = GetSoftCircleSprite();
        glowImg.raycastTarget = false;

        // ── Sprite de recompensa (GRAN) ──────────────────────────────
        if (rewardSprite != null)
        {
            var iconGo = NewChild("RewardIcon", card.transform);
            rewardIconRT = iconGo.GetComponent<RectTransform>();
            rewardIconRT.anchorMin = rewardIconRT.anchorMax = new Vector2(0.5f, 0.5f);
            rewardIconRT.sizeDelta = new Vector2(220f, 220f);
            rewardIconRT.anchoredPosition = new Vector2(0f, -20f);
            var iconImg = iconGo.AddComponent<Image>();
            iconImg.sprite = rewardSprite;
            iconImg.preserveAspect = true;
        }

        // ── Text de descripció ───────────────────────────────────────
        var descGo = NewChild("Desc", card.transform);
        var descRT = descGo.GetComponent<RectTransform>();
        descRT.anchorMin = new Vector2(0.1f, 0f);
        descRT.anchorMax = new Vector2(0.9f, 0.35f);
        descRT.offsetMin = new Vector2(0f, 40f);
        descRT.offsetMax = new Vector2(0f, 0f);
        var descTxt = descGo.AddComponent<TextMeshProUGUI>();
        SetFont(descTxt, 42f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
        descTxt.enableAutoSizing = true;
        descTxt.fontSizeMin = 24f;
        descTxt.fontSizeMax = 44f;
        descTxt.text = !string.IsNullOrEmpty(rewardText) ? rewardText : "You've gained a permanent upgrade!";

        // ── NOU: Botó Interactiu [E] ───
        var eBase = NewChild("E_Prompt", card.transform);
        ePromptRT = eBase.GetComponent<RectTransform>();
        ePromptRT.anchorMin = ePromptRT.anchorMax = new Vector2(1f, 0f); 
        ePromptRT.pivot = new Vector2(1f, 0f);
        ePromptRT.sizeDelta = new Vector2(50f, 50f); // Mes petit
        ePromptRT.anchoredPosition = new Vector2(-25f, 25f);
        
        // Part inferior (Ombra/Base del botó)
        var pBot = NewChild("Base", eBase.transform);
        var pBotRT = pBot.GetComponent<RectTransform>();
        pBotRT.anchorMin = Vector2.zero; pBotRT.anchorMax = Vector2.one; pBotRT.offsetMin = pBotRT.offsetMax = Vector2.zero;
        pBot.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 1f);
        pBot.GetComponent<Image>().sprite = GetRoundedSprite();
        pBot.GetComponent<Image>().type = Image.Type.Sliced;
        
        // Part superior (El botó en sí)
        var pTop = NewChild("Top", eBase.transform);
        var ptRT = pTop.GetComponent<RectTransform>();
        ptRT.anchorMin = Vector2.zero; ptRT.anchorMax = Vector2.one; ptRT.offsetMin = ptRT.offsetMax = Vector2.zero;
        ptRT.anchoredPosition = new Vector2(0f, 4f);
        var ptImg = pTop.AddComponent<Image>();
        ptImg.color = new Color(0.95f, 0.95f, 0.95f, 1f);
        ptImg.sprite = GetRoundedSprite();
        ptImg.type = Image.Type.Sliced;
        
        // Lletra E
        var etGo = NewChild("T", pTop.transform);
        TxtFill(etGo.GetComponent<RectTransform>(), "E", 32f, Color.black, FontStyles.Bold, TextAlignmentOptions.Center);
        
        // Inicialment ocult fins que acabi l'animació
        ePromptRT.localScale = Vector3.zero;
    }

    private TextMeshProUGUI TxtFill(RectTransform rt, string text, float size, Color col, FontStyles style, TextAlignmentOptions align)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        SetFont(t, size, col, style, align);
        t.text = text;
        return t;
    }

    private Sprite generatedSoftCircle;
    private Sprite GetSoftCircleSprite()
    {
        if (generatedSoftCircle != null) return generatedSoftCircle;
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float center = size / 2f;
        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float alpha = Mathf.Clamp01(1f - (dist / center));
                // Pow per fer-ho més suau i natural (radial fallback)
                alpha = Mathf.Pow(alpha, 2.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        generatedSoftCircle = Sprite.Create(tex, new Rect(0,0,size,size), Vector2.one*0.5f);
        return generatedSoftCircle;
    }

    private Sprite generatedRoundedSprite;
    private Sprite GetRoundedSprite()
    {
        if (generatedRoundedSprite != null) return generatedRoundedSprite;
        int size = 16;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        Color w = Color.white; Color c = new Color(1f, 1f, 1f, 0f);
        for (int y=0; y<size; y++) {
            for (int x=0; x<size; x++) {
                bool corner = (x==0 && y==0) || (x==size-1 && y==0) || (x==0 && y==size-1) || (x==size-1 && y==size-1)
                    || (x==1 && y==0) || (x==0 && y==1) || (x==size-2 && y==0) || (x==size-1 && y==1);
                tex.SetPixel(x, y, corner ? c : w);
            }
        }
        tex.Apply();
        generatedRoundedSprite = Sprite.Create(tex, new Rect(0,0,size,size), Vector2.one*0.5f, 100f, 0, SpriteMeshType.FullRect, new Vector4(4,4,4,4));
        return generatedRoundedSprite;
    }

    // ────────────────────────────────────────────────────────────────
    // Animacions
    // ────────────────────────────────────────────────────────────────
    private IEnumerator AnimateIn()
    {
        Vector2 target = cardRect.anchoredPosition;
        cardRect.localScale = new Vector3(0.5f, 0.5f, 1f);
        cardRect.anchoredPosition = target + new Vector2(0f, -800f);

        float dur = 0.7f;
        float elapsed = 0f;
        Vector2 from = cardRect.anchoredPosition;

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float p = Mathf.Clamp01(elapsed / dur);
            // Elastic Ease Out per donar-li caràcter
            float e = 1f - Mathf.Cos(p * Mathf.PI * 0.5f); // Subtil sin
            if (p < 1f) {
                float s = 1.70158f;
                float p2 = p - 1f;
                e = (p2 * p2 * ((s + 1f) * p2 + s) + 1f);
            }
            cardRect.anchoredPosition = Vector2.LerpUnclamped(from, target, e);
            cardRect.localScale = Vector3.LerpUnclamped(new Vector3(0.5f, 0.5f, 1f), Vector3.one, e);
            yield return null;
        }
        cardRect.anchoredPosition = target;
        cardRect.localScale = Vector3.one;

        // Mostrar el botó E amb un petit "pop"
        yield return new WaitForSeconds(0.4f);
        float eDur = 0.3f; float eElapsed = 0f;
        while(eElapsed < eDur) {
            eElapsed += Time.deltaTime;
            ePromptRT.localScale = Vector3.one * (eElapsed/eDur * 1.25f);
            if (ePromptRT.localScale.x > 1f) ePromptRT.localScale = Vector3.one;
            yield return null;
        }
        ePromptRT.localScale = Vector3.one;

        waitingForInput = true;
    }

    private IEnumerator AnimateOut()
    {
        float dur = 0.25f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            cardRect.localScale = Vector3.Lerp(Vector3.one, new Vector3(1.1f, 1.1f, 1f), p);
            // Fade out opcional del canvas group si m'haguessis demanat
            yield return null;
        }
        
        InvokeDone();
        Destroy(gameObject);
    }

    private void InvokeDone()
    {
        if (onDone != null)
        {
            onDone.Invoke();
            onDone = null; // Evitem que es torni a cridar
        }
    }

    private void OnDestroy()
    {
        // Seguretat: si per algun motiu es destrueix (canvi d'escena, etc), avisar que hem acabat
        InvokeDone();
    }

    // ────────────────────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────────────────────
    private string RemoveAccents(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        string result = text;
        string[] accents = { "à", "á", "è", "é", "ì", "í", "ò", "ó", "ù", "ú", "À", "Á", "È", "É", "Ì", "Í", "Ò", "Ó", "Ù", "Ú", "ç", "Ç", "·" };
        string[] normal  = { "a", "a", "e", "e", "i", "i", "o", "o", "u", "u", "A", "A", "E", "E", "I", "I", "O", "O", "U", "U", "c", "C", "." };
        for (int i = 0; i < accents.Length; i++)
            result = result.Replace(accents[i], normal[i]);
        return result;
    }

    private void SetFont(TMP_Text txt, float size, Color color, FontStyles style, TextAlignmentOptions align)
    {
        if (pixelFont != null) txt.font = pixelFont;
        txt.fontSize = size;
        txt.color = color;
        txt.fontStyle = style;
        txt.alignment = align;
    }


    private GameObject NewChild(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    private void AddHLine(GameObject parent, float y, Color col)
    {
        var go = NewChild("HLine", parent.transform);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.08f, 0.5f);
        rt.anchorMax = new Vector2(0.92f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, 6f);
        rt.anchoredPosition = new Vector2(0f, y);
        go.AddComponent<Image>().color = col;
    }

    private void AddPixelBorder(GameObject parent, Color col, float thick)
    {
        void MakeSide(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var g = NewChild(name, parent.transform);
            var r = g.GetComponent<RectTransform>();
            r.anchorMin = anchorMin; r.anchorMax = anchorMax;
            r.offsetMin = offsetMin; r.offsetMax = offsetMax;
            g.AddComponent<Image>().color = col;
        }
        MakeSide("B_Top",   new Vector2(0,1), new Vector2(1,1), new Vector2(0,-thick), new Vector2(0,0));
        MakeSide("B_Bot",   new Vector2(0,0), new Vector2(1,0), new Vector2(0,0),      new Vector2(0,thick));
        MakeSide("B_Left",  new Vector2(0,0), new Vector2(0,1), new Vector2(0,0),      new Vector2(thick,0));
        MakeSide("B_Right", new Vector2(1,0), new Vector2(1,1), new Vector2(-thick,0), new Vector2(0,0));
    }

    private void AddCornerSquares(GameObject parent, Color col, float size)
    {
        void MakeCorner(string name, Vector2 anchor, Vector2 pivot)
        {
            var g = NewChild(name, parent.transform);
            var r = g.GetComponent<RectTransform>();
            r.anchorMin = anchor; r.anchorMax = anchor;
            r.pivot = pivot;
            r.sizeDelta = new Vector2(size, size);
            r.anchoredPosition = Vector2.zero;
            g.AddComponent<Image>().color = col;
        }
        MakeCorner("C_TL", new Vector2(0,1), new Vector2(0,1));
        MakeCorner("C_TR", new Vector2(1,1), new Vector2(1,1));
        MakeCorner("C_BL", new Vector2(0,0), new Vector2(0,0));
        MakeCorner("C_BR", new Vector2(1,0), new Vector2(1,0));
    }

    private TMP_FontAsset LoadFont(string fontName)
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
