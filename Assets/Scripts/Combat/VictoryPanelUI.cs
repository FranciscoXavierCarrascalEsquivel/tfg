using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Panell de victòria animat estil pixel-art.
/// Cridada: VictoryPanelUI.Create(canvasParent, gold, items, totalGold, onDone)
/// Els sprites pixel_coin i pixel_bag han d'estar a Assets/Art/Sprites/ 
/// i configurats com a Sprite (Point filter, no compression).
/// </summary>
public class VictoryPanelUI : MonoBehaviour
{
    // ─── Sprite paths (relatius a Assets/) ─────────────────────────
    private const string COIN_PATH = "Art/Sprites/pixel_coin";
    private const string BAG_PATH  = "Art/Sprites/pixel_bag";

    // ─── Factory ────────────────────────────────────────────────────
    /// <param name="items">Llista d'objectes guanyats (pot ser buida)</param>
    public static VictoryPanelUI Create(
        Transform canvasParent,
        int goldEarned,
        System.Collections.Generic.List<string> items,
        int totalGold,
        Action onDone)
    {
        var go = new GameObject("VictoryPanel");
        go.transform.SetParent(canvasParent, false);
        go.transform.SetAsLastSibling();

        var panel = go.AddComponent<VictoryPanelUI>();
        panel.goldEarned = goldEarned;
        panel.items      = items ?? new System.Collections.Generic.List<string>();
        panel.totalGold  = totalGold;
        panel.onDone     = onDone;
        return panel;
    }

    // ── Sobrecàrrega de compatibilitat (un sol string d'item) ────────
    public static VictoryPanelUI Create(Transform canvasParent, int goldEarned, string itemName, int totalGold, Action onDone)
    {
        var list = new System.Collections.Generic.List<string>();
        if (!string.IsNullOrEmpty(itemName) && itemName != "none" && itemName != "—")
            list.Add(itemName);
        return Create(canvasParent, goldEarned, list, totalGold, onDone);
    }

    // ── Dades ────────────────────────────────────────────────────────
    private int    goldEarned;
    private System.Collections.Generic.List<string> items;
    private int    totalGold;
    private Action onDone;

    // ── Estat ────────────────────────────────────────────────────────
    private bool waitingForInput = false;
    private TMP_Text promptText;

    // ────────────────────────────────────────────────────────────────
    private void Start()
    {
        Build();
        StartCoroutine(AnimateIn());
    }

    private void Update()
    {
        if (!waitingForInput) return;
        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
        {
            waitingForInput = false;
            onDone?.Invoke();
        }
    }

    // ────────────────────────────────────────────────────────────────
    // Construcció
    // ────────────────────────────────────────────────────────────────
    private CanvasGroup   bgCG;
    private RectTransform titleRect;
    private RectTransform cardRect;
    private TMP_Text      goldValueText;

    private TMP_FontAsset pixelFont;
    private Sprite        coinSprite;
    private Sprite        bagSprite;

    private void Build()
    {
        // Carrega font 8-bit (ja inclosa al projecte)
        pixelFont = LoadFont("determination SDF");
        if (pixelFont == null) pixelFont = LoadFont("PixelOperator SDF");
        if (pixelFont == null) pixelFont = LoadFont("8bitoperator_jve SDF");
        if (pixelFont == null) pixelFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

        // Sprites de moneda i bossa (veure instruccions a sota)
        coinSprite = LoadSprite(COIN_PATH);
        bagSprite  = LoadSprite(BAG_PATH);

        // ── Overlay pantalla completa ─────────────────────────────────
        var selfRT = gameObject.AddComponent<RectTransform>();
        selfRT.anchorMin = Vector2.zero;
        selfRT.anchorMax = Vector2.one;
        selfRT.offsetMin = Vector2.zero;
        selfRT.offsetMax = Vector2.zero;

        bgCG = gameObject.AddComponent<CanvasGroup>();
        bgCG.alpha = 1f;

        // ELIMINAT: El fons fosc (perquè es vegi l'escenari)
        // gameObject.AddComponent<Image>().color = new Color(0.03f, 0.02f, 0.06f, 0.88f);

        // ── Targeta principal (pixel-art RPG window) ──────────────────
        // Altura mes compacta si no hi ha objectes
        int extraRows = 0;
        if (items.Count > 0)
        {
            var distinctItems = new System.Collections.Generic.HashSet<string>(items);
            extraRows = (distinctItems.Count + 1) / 2; // Arrodoniment cap amunt per columnes
        }
        float cardBaseH = (items.Count == 0) ? 420f : 500f;
        float cardH     = cardBaseH + extraRows * 100f;

        var card = NewChild("Card", transform);
        cardRect = card.GetComponent<RectTransform>();
        // Àncores adaptatives, però amb MÉS MARGE a esquerra i dreta (20% lliure a cada costat, la card ocupa el centre 60%)
        cardRect.anchorMin        = new Vector2(0.2f, 0.5f);
        cardRect.anchorMax        = new Vector2(0.8f, 0.5f);
        cardRect.offsetMin        = new Vector2(0f, -cardH / 2f);
        cardRect.offsetMax        = new Vector2(0f, cardH / 2f);
        // Inicialment a sota del tot pel lliscament
        cardRect.anchoredPosition = new Vector2(0f, -50f);

        // Fons de la targeta — blau fosc sòlid
        card.AddComponent<Image>().color = new Color(0.08f, 0.07f, 0.16f, 1f);

        // ── Franja de capçalera (header block) ───────────────────────
        float headerH = 140f;
        var header = NewChild("Header", card.transform);
        var hRT = header.GetComponent<RectTransform>();
        hRT.anchorMin = new Vector2(0f, 1f);
        hRT.anchorMax = new Vector2(1f, 1f);
        hRT.offsetMin = new Vector2(0f, -headerH);
        hRT.offsetMax = Vector2.zero;
        header.AddComponent<Image>().color = new Color(0.18f, 0.10f, 0.34f, 1f);

        // Títol dins la capçalera (no es mou, la targeta sencera llisca)
        var titleGo = NewChild("Title", header.transform);
        titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.anchorMin = Vector2.zero;
        titleRect.anchorMax = Vector2.one;
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;
        var titleTxt = titleGo.AddComponent<TextMeshProUGUI>();
        SetFont(titleTxt, 80f, new Color(1f, 0.92f, 0.2f), FontStyles.Bold, TextAlignmentOptions.Center);
        // Assegurem que s'ajusti automàticament si cal
        titleTxt.enableAutoSizing = true;
        titleTxt.fontSizeMin = 40f;
        titleTxt.fontSizeMax = 90f;
        titleTxt.text = "*  VICTORY!  *";

        // ── Contorn pixel (4 costats) + cantonades quadrades ─────────
        float brd = 8f;
        AddPixelBorder(card, new Color(0.95f, 0.80f, 0.15f, 1f), brd);
        AddCornerSquares(card, new Color(0.95f, 0.80f, 0.15f, 1f), 16f);

        // ── Files de dades ────────────────────────────────────────────
        // Partim de sota la capçalera amb espaiat més compacte
        float currentY = cardH / 2f - headerH - 70f;

        goldValueText = MakeIconRow(card, coinSprite, "Gold", $"+0 G ({totalGold - goldEarned} G)",
                                    new Color(1f, 0.90f, 0.15f), currentY);
        currentY -= 100f;

        AddHLine(card, currentY + 30f, new Color(0.95f, 0.80f, 0.15f, 0.35f));
        currentY -= 20f;

        if (items.Count == 0)
        {
            MakeIconRow(card, bagSprite, "Items acquired", "-",
                        new Color(0.6f, 0.6f, 0.6f), currentY);
            currentY -= 100f;
        }
        else
        {
            // Capçalera separada pels objectes aconseguits
            MakeIconRow(card, bagSprite, "Items acquired", "", Color.white, currentY);
            currentY -= 100f;

            var itemCounts = new System.Collections.Generic.Dictionary<string, int>();
            foreach(var item in items)
            {
                if(itemCounts.ContainsKey(item)) itemCounts[item]++;
                else itemCounts[item] = 1;
            }

            BuildItemGrid(card, itemCounts, ref currentY);
        }

        AddHLine(card, currentY + 36f, new Color(0.95f, 0.80f, 0.15f, 0.35f));
        currentY -= 20f;

        // Prompt
        var promptGo = NewChild("Prompt", transform);
        var promptRt = promptGo.GetComponent<RectTransform>();
        promptRt.anchorMin        = new Vector2(0.5f, 0f);
        promptRt.anchorMax        = new Vector2(0.5f, 0f);
        promptRt.sizeDelta        = new Vector2(1000f, 60f);
        promptRt.anchoredPosition = new Vector2(0f, 50f); // Més a baix, quasi a prop del límit de la pantalla
        promptText = promptGo.AddComponent<TextMeshProUGUI>();
        // Transparència inicial a 0 perquè no es vegi d'entrada, el Blink ho animarà a 1
        SetFont(promptText, 52f, new Color(1f, 1f, 1f, 0f), 
                FontStyles.Normal, TextAlignmentOptions.Center);
        promptText.text = "[ PRESS E OR ENTER TO CONTINUE ]";
        
        // Contorn perfecte Natiu al TextMeshPro creant una nova instància del material per no enguarrar l'asset principal
        promptText.fontSharedMaterial = Instantiate(promptText.fontSharedMaterial);
        promptText.fontSharedMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.25f);
        promptText.fontSharedMaterial.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.04f, 0.04f, 0.04f, 1f));
    }

    // ────────────────────────────────────────────────────────────────
    // Helpers de construcció UI
    // ────────────────────────────────────────────────────────────────
    
    // Construeix un Grid intern de 2 columnes amb les icones personalitzades de l'inventari
    private void BuildItemGrid(GameObject parent, System.Collections.Generic.Dictionary<string, int> itemCounts, ref float currentY)
    {
        int count = 0;
        int maxCols = 2; // Dues columnes
        float colWidth = 330f; // Mida de la pastilla
        float startX = (-colWidth * 0.5f) - 30f; // Espaiat al centre
        
        foreach (var kvp in itemCounts)
        {
            float colOffset = (count % maxCols == 0) ? startX : startX + colWidth + 60f; 
            
            var profile = PlayerInventory.Instance != null ? PlayerInventory.Instance.GetItemProfile(kvp.Key) : null;
            Sprite itemSprite = profile != null ? profile.itemIcon : bagSprite;
            string itemName = profile != null ? profile.itemName : kvp.Key;
            
            // Sempre mostra la quantitat obtinguda (ex. x1, x2...) per clarificar-ho a l'usuari
            string val = $"{itemName} x{kvp.Value}";
            
            var row = NewChild($"Item_{kvp.Key}", parent.transform);
            var rowRt = row.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0.5f, 0.5f);
            rowRt.anchorMax = new Vector2(0.5f, 0.5f);
            rowRt.sizeDelta = new Vector2(colWidth, 100f);
            rowRt.anchoredPosition = new Vector2(colOffset, currentY);

            // Icona
            if (itemSprite != null)
            {
                var iconGo = NewChild("Icon", row.transform);
                var iconRt = iconGo.GetComponent<RectTransform>();
                iconRt.anchorMin = new Vector2(0f, 0.5f);
                iconRt.anchorMax = new Vector2(0f, 0.5f);
                iconRt.sizeDelta = new Vector2(70f, 70f);
                iconRt.anchoredPosition = new Vector2(35f, 0f);
                var imgComp = iconGo.AddComponent<Image>();
                imgComp.sprite = itemSprite;
                imgComp.preserveAspect = true;
                imgComp.material = null;
            }

            // Name + Value
            var lblGo = NewChild("Value", row.transform);
            var lblRt = lblGo.GetComponent<RectTransform>();
            lblRt.anchorMin = new Vector2(0f, 0f);
            lblRt.anchorMax = new Vector2(1f, 1f);
            lblRt.offsetMin = new Vector2(85f, 0f);
            lblRt.offsetMax = Vector2.zero;

            var lblTxt = lblGo.AddComponent<TextMeshProUGUI>();
            SetFont(lblTxt, 34f, Color.white, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            lblTxt.enableAutoSizing = true;
            lblTxt.fontSizeMin = 20f;
            lblTxt.fontSizeMax = 38f;
            lblTxt.text = val;
            
            count++;
            if (count % maxCols == 0) currentY -= 90f;
        }
        
        if (count % maxCols != 0) currentY -= 90f;
    }

    /// Crea una fila amb icona sprite + label + valor
    private TMP_Text MakeIconRow(GameObject parent, Sprite icon, string label, string value,
                                  Color valueColor, float yOffset)
    {
        var row = NewChild($"Row_{label}", parent.transform);
        var rowRt = row.GetComponent<RectTransform>();
        // Els posem directament ancorats al 15% esquerra i 85% dreta (30% d'espai buit en total)
        rowRt.anchorMin = new Vector2(0.15f, 0.5f);
        rowRt.anchorMax = new Vector2(0.85f, 0.5f);
        rowRt.offsetMin = Vector2.zero;
        rowRt.offsetMax = Vector2.zero;
        rowRt.sizeDelta = new Vector2(0f, 120f); // Més alt encara
        rowRt.anchoredPosition = new Vector2(0f, yOffset);

        // Icona
        if (icon != null)
        {
            var iconGo = NewChild("Icon", row.transform);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0f, 0.5f);
            iconRt.anchorMax = new Vector2(0f, 0.5f);
            iconRt.sizeDelta = new Vector2(100f, 100f);
            iconRt.anchoredPosition = new Vector2(50f, 0f);
            var imgComp = iconGo.AddComponent<Image>();
            imgComp.sprite = icon;
            imgComp.preserveAspect = true;
            // Crisp pixel rendering
            imgComp.material = null;
        }

        // Label (Or guanyat / objecte)
        var lblGo = NewChild("Label", row.transform);
        var lblRt = lblGo.GetComponent<RectTransform>();
        lblRt.anchorMin = new Vector2(0f, 0f);
        // Donem un 70% de l'espai al label per evitar que "aconseguits" baixi de fila
        lblRt.anchorMax = (string.IsNullOrEmpty(value) || value == "-") ? new Vector2(1f, 1f) : new Vector2(0.7f, 1f);
        lblRt.offsetMin = new Vector2(140f, 0f);
        lblRt.offsetMax = Vector2.zero;
        var lblTxt = lblGo.AddComponent<TextMeshProUGUI>();
        SetFont(lblTxt, 64f, new Color(0.65f, 0.65f, 0.65f),
                FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        lblTxt.enableAutoSizing = true;
        lblTxt.fontSizeMin = 30f;
        lblTxt.fontSizeMax = 64f;
        lblTxt.textWrappingMode = TextWrappingModes.NoWrap;
        lblTxt.text = label;

        // Valor NUMÈRIC / NOM OBJECTE
        var valGo = NewChild("Value", row.transform);
        var valRt = valGo.GetComponent<RectTransform>();
        valRt.anchorMin = new Vector2(0.5f, 0f); // Meitat dreta
        valRt.anchorMax = new Vector2(1f, 1f);
        valRt.offsetMin = Vector2.zero;
        valRt.offsetMax = Vector2.zero; // Sense pixel offset
        var valTxt = valGo.AddComponent<TextMeshProUGUI>();
        // Mateix color ressaltat, mateix tamany MAIXM 64.
        SetFont(valTxt, 64f, valueColor, FontStyles.Bold, TextAlignmentOptions.MidlineRight);
        valTxt.enableAutoSizing = true;
        valTxt.fontSizeMin = 30f;
        valTxt.fontSizeMax = 64f;
        valTxt.textWrappingMode = TextWrappingModes.NoWrap;
        valTxt.text = value;
        return valTxt;
    }

    private void AddHLine(GameObject parent, float y, Color col)
    {
        var go = NewChild("HLine", parent.transform);
        var rt = go.GetComponent<RectTransform>();
        // Les línies prenen un 8% fins al 92% (dinàmic també)
        rt.anchorMin = new Vector2(0.08f, 0.5f);
        rt.anchorMax = new Vector2(0.92f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, 6f); // Línia més gruixuda
        rt.anchoredPosition = new Vector2(0f, y);
        go.AddComponent<Image>().color = col;
    }

    /// Afegeix 4 barres pixel per simular un contorn net
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

    /// Afegeix 4 quadrats a les cantonades (estil RPG clàssic)
    private void AddCornerSquares(GameObject parent, Color col, float size)
    {
        void MakeCorner(string name, Vector2 anchor, Vector2 pivot)
        {
            var g = NewChild(name, parent.transform);
            var r = g.GetComponent<RectTransform>();
            r.anchorMin = anchor; r.anchorMax = anchor;
            r.pivot     = pivot;
            r.sizeDelta = new Vector2(size, size);
            r.anchoredPosition = Vector2.zero;
            g.AddComponent<Image>().color = col;
        }
        MakeCorner("C_TL", new Vector2(0,1), new Vector2(0,1));
        MakeCorner("C_TR", new Vector2(1,1), new Vector2(1,1));
        MakeCorner("C_BL", new Vector2(0,0), new Vector2(0,0));
        MakeCorner("C_BR", new Vector2(1,0), new Vector2(1,0));
    }

    private void SetFont(TMP_Text txt, float size, Color color, FontStyles style, TextAlignmentOptions align)
    {
        if (pixelFont != null) txt.font = pixelFont;
        txt.fontSize  = size;
        txt.color     = color;
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

    private Sprite LoadSprite(string assetsRelativePath)
    {
#if UNITY_EDITOR
        var sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/{assetsRelativePath}.png");
        if (sp != null) return sp;
#endif
        return Resources.Load<Sprite>(assetsRelativePath);
    }

    private TMP_FontAsset LoadFont(string fontName)
    {
#if UNITY_EDITOR
        // Busca a la carpeta Fonts del projecte directament
        var f = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"Assets/Fonts/{fontName}.asset");
        if (f != null) return f;
        // Intent alternatiu: TextMesh Pro Resources
        f = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            $"Assets/TextMesh Pro/Resources/Fonts & Materials/{fontName}.asset");
        if (f != null) return f;
#endif
        // Runtime: ha d'estar dins una carpeta Resources
        var loaded = Resources.Load<TMP_FontAsset>($"Fonts & Materials/{fontName}");
        if (loaded != null) return loaded;
        return Resources.Load<TMP_FontAsset>($"Fonts/{fontName}") ?? Resources.Load<TMP_FontAsset>(fontName);
    }

    // ────────────────────────────────────────────────────────────────
    // Animació d'entrada
    // ────────────────────────────────────────────────────────────────
    private IEnumerator AnimateIn()
    {
        // Targeta sencera llisca des de baix amb efecte pop
        Vector2 cardTarget = cardRect.anchoredPosition;
        yield return SlideRect(cardRect, cardTarget + new Vector2(0f, -800f), cardTarget, 0.65f);

        // Comptador d'or animat i combinat
        yield return new WaitForSeconds(0.1f);
        yield return CountUp(goldValueText, goldEarned, totalGold, 0.7f);

        // Prompt parpellejant de color blanc
        yield return new WaitForSeconds(0.2f);
        // CRÍTIC: Assignar waitingForInput = true ABANS de la corrutina Blink, perquè hi pugui entrar
        waitingForInput = true;
        StartCoroutine(BlinkText(promptText));
    }

    private IEnumerator Fade(CanvasGroup cg, float from, float to, float dur)
    {
        float t = 0f; cg.alpha = from;
        while (t < dur) { t += Time.deltaTime; cg.alpha = Mathf.Lerp(from, to, t/dur); yield return null; }
        cg.alpha = to;
    }

    private IEnumerator SlideRect(RectTransform rt, Vector2 from, Vector2 to, float dur)
    {
        float t = 0f; 
        rt.anchoredPosition = from;
        rt.localScale = new Vector3(0.3f, 0.3f, 1f);

        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            
            // Fórmula Back Ease Out (overshoot / rebot)
            float s = 1.70158f;
            p = p - 1f;
            float e = (p * p * ((s + 1f) * p + s) + 1f); 

            rt.anchoredPosition = Vector2.LerpUnclamped(from, to, e);
            rt.localScale = Vector3.LerpUnclamped(new Vector3(0.3f, 0.3f, 1f), Vector3.one, e);
            
            yield return null;
        }
        
        rt.anchoredPosition = to;
        rt.localScale = Vector3.one;
    }

    private IEnumerator CountUp(TMP_Text txt, int gained, int finalTotal, float dur)
    {
        float t = 0f;
        int prevTotal = finalTotal - gained;
        while (t < dur)
        {
            t += Time.deltaTime;
            int currentGained = Mathf.RoundToInt(Mathf.Lerp(0, gained, t / dur));
            int currentTotal = prevTotal + currentGained;
            txt.text = $"+{currentGained} G ({currentTotal} G)";
            yield return null;
        }
        txt.text = $"+{gained} G ({finalTotal} G)";
    }

    private IEnumerator BlinkText(TMP_Text txt)
    {
        while (waitingForInput)
        {
            yield return FadeText(txt, 0f, 1f, 0.45f);
            yield return new WaitForSeconds(0.2f);
            yield return FadeText(txt, 1f, 0f, 0.45f);
            yield return new WaitForSeconds(0.05f);
        }
    }

    private IEnumerator FadeText(TMP_Text txt, float from, float to, float dur)
    {
        float t = 0f;
        Color c = txt.color;
        c.a = from; txt.color = c;
        while (t < dur)
        {
            t += Time.deltaTime;
            c.a = Mathf.Lerp(from, to, t/dur);
            txt.color = c;
            yield return null;
        }
        c.a = to; txt.color = c;
    }
}
