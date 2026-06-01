using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Interfície gràfica i gestor del sistema de Botiga (ShopMenuUI).
/// Permet al jugador comprar consumibles i objectes especials, o vendre recursos acumulats
/// a canvi d'or de forma interactiva mitjançant pestanyes de compra i venda commutables.
/// 
/// DISSENY I CARACTERÍSTIQUES DEL TFG:
/// - **Distribució interactiva asimètrica**: La pantalla es divideix en dues meitats. A l'esquerra es dibuixa
///   el personatge de la botiga (Shopkeeper) amb la seva bombolla gràfica de xerrada, i a la dreta es maqueten
///   les pestanyes adaptatives i la llista d'objectes.
/// - **Temàtiques visuals dinàmiques**: Tota la interfície gràfica de la motxilla s'adapta amb transicions de colors:
///   Blau profund/celest brillant en mode COMPRAR (Buy), i Vermell acerós/taronja fogós en mode VENDRE (Sell).
/// - **Efecte sacsejada del botiguer (Juice)**: El retrat de la criatura es sacseja físicament (Shake animation)
///   en expressar cada frase, lligat a un typewriter amb so de murmuri vocal adaptat per lletres.
/// - **Comportament de reaccions orgànic**: El botiguer utilitza llistes aleatòries de respostes des de l'inventari
///   en cas de transaccions correctes, manca de diners, motxilla plena o comiat, donant molta vida al personatge.
/// - **Graella procedimental sense scroll**: Ajust geomètric instantani en frame inicial compatible amb dispositius tàctils.
/// </summary>
public class ShopMenuUI : MonoBehaviour
{
    public static bool IsOpen { get; set; } // Flag global de bloqueig de moviments

    private Action onClose;
    private bool onCloseInvoked = false;

    private readonly List<ShopEntry> entries = new List<ShopEntry>();
    private int selIdx = -1;  // -1 = EXIT (selecció per defecte)
    private const int EXIT_IDX = -1;
    private const int NCOLS    = 2; // Graella adaptativa de 2 columnes

    private enum ShopMode { Buy, Sell }
    private ShopMode currentMode = ShopMode.Buy; // Mode per defecte

    // Components visuals procedimentals
    private TextMeshProUGUI hpTxt, goldTxt, capTxt;
    private Image buyTabBg, sellTabBg;
    private TextMeshProUGUI buyTabTxt, sellTabTxt;
    private RectTransform tabKeyBtnRT; // Indicador de tecla TAB
    private Image cardImg, statsImg, detImg, frameImg;
    private Outline cardOl, frameOl;
    private TextMeshProUGUI detNameTxt, detDescTxt, detPriceTxt, dialogTxt;
    private Image           detIconImg, npcImg;
    private AudioSource     typeAudioSrc;
    private Coroutine       typeCoroutine;
    private Coroutine       shakeCoroutine;
    private RectTransform   hpRT;
    private Transform       gridParent;
    private Image           exitBg;
    private TextMeshProUGUI exitTxt;
    private GridLayoutGroup glg;
    private RectTransform   glgRT;
    private Image           divImg;
    private RectTransform   tabTopRT_Ref; // Tecla TAB visual
    private RectTransform   escTopRT_Ref; // Tecla ESC visual
    private bool            lastTabPressed;
    private bool            lastEscPressed;

    /// <summary>
    /// Estructura de dades per a cada cel·la d'objecte a la botiga.
    /// </summary>
    private class ShopEntry
    {
        public string idName; 
        public ItemProfile profile; 
        public int count;
        public Image bg; 
        public TextMeshProUGUI txt;
    }

    // ── FACTORY PROCEDIMENTAL (Instanciació i Setup) ──────────────────────
    public static void Show(Action onClose = null)
    {
        // Seguretat: no es permet xop mentre combatem
        if (CombatLoader.IsInCombat)
        {
            Debug.LogWarning("No es pot obrir la botiga mentre s'està en combat.");
            onClose?.Invoke();
            return;
        }
        var canvas = CanvasHelper.GetMainCanvas();
        if (canvas == null)
        {
            canvas = Object.FindFirstObjectByType<Canvas>();
        }
        if (canvas == null)
        {
            var canGO = new GameObject("ShopCanvas_Fallback");
            canvas = canGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 25000;
            canGO.AddComponent<CanvasScaler>();
            canGO.AddComponent<GraphicRaycaster>();
        }
        
        var go = new GameObject("ShopMenuUI");
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(canvas.transform, false);
        rt.anchorMin = rt.offsetMin = Vector2.zero;
        rt.anchorMax = Vector2.one; rt.offsetMax = Vector2.zero;
        
        var ui = go.AddComponent<ShopMenuUI>();
        
        // Forcem canvas d'alta prioritat gràfica
        var cv = go.AddComponent<Canvas>();
        cv.overrideSorting = true;
        cv.sortingOrder = 30000;
        go.AddComponent<GraphicRaycaster>();
        
        ui.onClose = onClose;
        ui.Build(); 
        IsOpen = true;
    }

    // ── CONSTRUCCIÓ PROCEDIMENTAL COMPACTA (SENSE PREFABS) ────────────────
    /// <summary>
    /// Aixeca la botiga completa per codi, dividint la pantalla en dues columnes (35% Shopkeeper, 65% Graella).
    /// </summary>
    private void Build()
    {
        var inv = PlayerInventory.Instance;

        // Fons semitransparent protector que enfosqueix el món de darrere
        var bgImg = gameObject.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.78f);
        bgImg.raycastTarget = true;

        var card = MakeRT("Card", transform);
        cardRT_Ref = card;
        
        card.anchorMin = new Vector2(0.08f, 0.08f);
        card.anchorMax = new Vector2(0.92f, 0.92f); // Centrat amb petits marges visuals
        card.offsetMin = card.offsetMax = Vector2.zero;
        cardImg = card.gameObject.AddComponent<Image>();
        cardImg.color = new Color(0.07f, 0.06f, 0.15f, 1f);
        cardOl = card.gameObject.AddComponent<Outline>();
        cardOl.effectColor = new Color(0.15f, 0.85f, 0.95f, 1f);
        cardOl.effectDistance = new Vector2(8f, -8f);

        // ─── 1. DIVISIÓ HORITZONTAL EN COLUMNES (LeftPanel vs RightPanel) ───
        var leftPanel = MakeRT("LeftPanel", card);
        leftPanel.anchorMin = new Vector2(0f, 0f); leftPanel.anchorMax = new Vector2(0.35f, 1f); // 35% ample
        leftPanel.offsetMin = leftPanel.offsetMax = Vector2.zero;

        // Línia vertical brillant d'unió
        var divider = MakeRT("Divider", card);
        divider.anchorMin = new Vector2(0.35f, 0f); divider.anchorMax = new Vector2(0.35f, 1f);
        divider.sizeDelta = new Vector2(4f, 0f);
        divider.anchoredPosition = Vector2.zero;
        divImg = divider.gameObject.AddComponent<Image>();
        divImg.color = new Color(0.15f, 0.85f, 0.95f, 0.25f);

        var rightPanel = MakeRT("RightPanel", card);
        rightPanel.anchorMin = new Vector2(0.35f, 0f); rightPanel.anchorMax = new Vector2(1f, 1f); // 65% ample
        rightPanel.offsetMin = new Vector2(4f, 0f); rightPanel.offsetMax = Vector2.zero;

        // ─── 2. SECCIÓ DEL BOTIGUER (Shopkeeper & Bombolla) ───
        var npcRT = MakeRT("Shopkeeper", leftPanel);
        npcRT.anchorMin = new Vector2(0.05f, 0.05f);
        npcRT.anchorMax = new Vector2(0.95f, 0.95f);
        npcRT.offsetMin = npcRT.offsetMax = Vector2.zero;

        // Bombolla de diàleg (Bubble)
        var bubbleRT = MakeRT("Bubble", npcRT);
        bubbleRT.anchorMin = new Vector2(0f, 0.65f);
        bubbleRT.anchorMax = new Vector2(1f, 1f);
        bubbleRT.offsetMin = bubbleRT.offsetMax = Vector2.zero;

        // Cua de fons de la bombolla (Triangle girat de fons)
        var tailRT = MakeRT("Tail", bubbleRT);
        tailRT.anchorMin = new Vector2(0.5f, 0f); tailRT.anchorMax = new Vector2(0.5f, 0f);
        tailRT.sizeDelta = new Vector2(46f, 46f);
        tailRT.anchoredPosition = new Vector2(-35f, 8f);
        tailRT.localRotation = Quaternion.Euler(0, 0, 65f);
        var tailImg = tailRT.gameObject.AddComponent<Image>();
        tailImg.color = new Color(1f, 1f, 1f, 0.95f);

        // Fons arrodonit procedimental
        var bBgRT = MakeRT("BG", bubbleRT);
        bBgRT.anchorMin = Vector2.zero; bBgRT.anchorMax = Vector2.one;
        bBgRT.offsetMin = bBgRT.offsetMax = Vector2.zero;
        var bubbleBg = bBgRT.gameObject.AddComponent<Image>();
        bubbleBg.color = new Color(1f, 1f, 1f, 0.95f);
        bubbleBg.sprite = GetRoundedSprite(); // Generat procedimental
        bubbleBg.type = Image.Type.Sliced;
        bubbleBg.pixelsPerUnitMultiplier = 0.5f;

        dialogTxt = TxtFill(bubbleRT, "", 42f, new Color(0.1f, 0.1f, 0.1f), FontStyles.Bold, TextAlignmentOptions.Justified);
        dialogTxt.margin = new Vector4(25f, 25f, 25f, 25f);
        dialogTxt.textWrappingMode = TextWrappingModes.Normal;
        dialogTxt.enableAutoSizing = true;
        dialogTxt.fontSizeMin = 24f;
        dialogTxt.fontSizeMax = 48f;

        typeAudioSrc = gameObject.AddComponent<AudioSource>();
        typeAudioSrc.playOnAwake = false;

        // Imatge física de la criatura botiguera
        var spriteRT = MakeRT("Sprite", npcRT);
        spriteRT.anchorMin = new Vector2(0f, 0f);
        spriteRT.anchorMax = new Vector2(1f, 0.60f);
        spriteRT.offsetMin = spriteRT.offsetMax = Vector2.zero;
        npcImg = spriteRT.gameObject.AddComponent<Image>();
        npcImg.preserveAspect = true;
        
        // Missatge de benvinguda aleatori del botiguer
        ApplyShopVariant(inv.GetRandomMsg(inv.shopWelcomeMsgs));

        // Mesures de les files per a la graella de la dreta
        const float H_TITLE  = 68f;
        const float H_STATS  = 68f;
        const float H_DETAIL = 148f;
        const float H_CAP    = 32f;
        const float H_EXIT   = 56f;
        const float H_HINT   = 40f;

        float fromTop = 0f;

        // ─── Pestanyes Superiors (BUY / SELL) ───
        var titleRT = TopZone(rightPanel, "Title", ref fromTop, H_TITLE);
        
        var buyRT = StretchChild(titleRT, "Buy", 0f, 0f, 0.5f, 1f);
        buyTabBg = buyRT.gameObject.AddComponent<Image>();
        buyTabTxt = TxtFill(buyRT, "BUY", 44f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
        
        var sellRT = StretchChild(titleRT, "Sell", 0.5f, 0f, 1f, 1f);
        sellTabBg = sellRT.gameObject.AddComponent<Image>();
        sellTabTxt = TxtFill(sellRT, "SELL", 44f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);

        // Tecla decorativa visual TAB amb efecte de pressió
        var indGO = new GameObject("TabKey");
        tabKeyBtnRT = indGO.AddComponent<RectTransform>();
        tabKeyBtnRT.pivot = new Vector2(0.5f, 0.5f);
        tabKeyBtnRT.anchorMin = new Vector2(0.5f, 0.5f);
        tabKeyBtnRT.anchorMax = new Vector2(0.5f, 0.5f);
        tabKeyBtnRT.sizeDelta = new Vector2(76f, 36f);

        var tabBaseGO = new GameObject("Base");
        tabBaseGO.transform.SetParent(tabKeyBtnRT, false);
        var tabBaseRT = tabBaseGO.AddComponent<RectTransform>();
        tabBaseRT.anchorMin = Vector2.zero; tabBaseRT.anchorMax = Vector2.one;
        tabBaseRT.offsetMin = tabBaseRT.offsetMax = Vector2.zero;
        var tabBaseImg = tabBaseGO.AddComponent<Image>();
        tabBaseImg.color = new Color(0.02f, 0.02f, 0.02f, 1f);

        var tabTopGO = new GameObject("Top");
        tabTopGO.transform.SetParent(tabKeyBtnRT, false);
        tabTopRT_Ref = tabTopGO.AddComponent<RectTransform>();
        tabTopRT_Ref.anchorMin = Vector2.zero; tabTopRT_Ref.anchorMax = Vector2.one;
        tabTopRT_Ref.offsetMin = tabTopRT_Ref.offsetMax = Vector2.zero;
        tabTopRT_Ref.anchoredPosition = new Vector2(0f, 4f);
        
        var tabTopImg = tabTopGO.AddComponent<Image>();
        tabTopImg.color = new Color(0.12f, 0.12f, 0.12f, 1f);
        var tabTopOl = tabTopGO.AddComponent<Outline>();
        tabTopOl.effectColor = new Color(0.40f, 0.40f, 0.40f, 1f);
        tabTopOl.effectDistance = new Vector2(2f, -2f);

        TxtFill(tabTopRT_Ref, "TAB", 24f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);

        // ─── Estadístiques (Vida, Monedes) ───
        var statsRT = TopZone(rightPanel, "Stats", ref fromTop, H_STATS);
        statsImg = statsRT.gameObject.AddComponent<Image>();
        statsImg.color = new Color(0.04f, 0.03f, 0.11f, 1f);

        hpRT  = StretchChild(statsRT, "HP", 0f, 0f, 0.5f, 1f, 22f, 0f, 0f, 0f);
        hpTxt = hpRT.gameObject.AddComponent<TextMeshProUGUI>();
        SetFont(hpTxt, 44f, new Color(0.35f, 1f, 0.50f), FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        hpTxt.text = $"♥  {inv.CurrentHP} / {inv.MaxHP} HP";

        var goldRT = StretchChild(statsRT, "Gold", 0.5f, 0f, 1f, 1f, 0f, 0f, -56f, 0f);
        goldTxt    = goldRT.gameObject.AddComponent<TextMeshProUGUI>();
        SetFont(goldTxt, 44f, new Color(1f, 0.90f, 0.15f), FontStyles.Bold, TextAlignmentOptions.MidlineRight);
        goldTxt.text = $"{inv.Gold}";

        var coinRT = MakeRT("CoinIcon", statsRT);
        coinRT.anchorMin = new Vector2(1f, 0.5f); coinRT.anchorMax = new Vector2(1f, 0.5f);
        coinRT.sizeDelta = new Vector2(44f, 44f);
        coinRT.anchoredPosition = new Vector2(-28f, 0f);
        var coinImg = coinRT.gameObject.AddComponent<Image>();
        coinImg.preserveAspect = true;
        Sprite coinSp = LoadSprite("Art/Sprites/pixel_coin");
        if (coinSp != null) coinImg.sprite = coinSp;

        // ─── Àrea de detall descriptiu ───
        var detRT = TopZone(rightPanel, "Detail", ref fromTop, H_DETAIL, 2f);
        detImg = detRT.gameObject.AddComponent<Image>();
        detImg.color = new Color(0.08f, 0.10f, 0.20f, 1f);

        var frameRT = PointChild(detRT, "Frame", 0f, 0.5f, 0f, 0.5f, 14f, 0f, 126f, 126f);
        frameImg = frameRT.gameObject.AddComponent<Image>();
        frameImg.color = new Color(0.15f, 0.18f, 0.30f, 1f);
        frameOl = frameRT.gameObject.AddComponent<Outline>();
        frameOl.effectColor = new Color(0.15f, 0.85f, 0.95f, 0.65f);
        frameOl.effectDistance = new Vector2(3f, -3f);

        var iconRT = MakeRT("Icon", frameRT);
        iconRT.anchorMin = new Vector2(0.06f, 0.06f); iconRT.anchorMax = new Vector2(0.94f, 0.94f);
        iconRT.offsetMin = iconRT.offsetMax = Vector2.zero;
        detIconImg = iconRT.gameObject.AddComponent<Image>();
        detIconImg.preserveAspect = true;
        detIconImg.color = new Color(1f, 1f, 1f, 0f);

        var dNameRT = StretchChild(detRT, "DName", 0f, 0.5f, 1f, 1f, 152f, 4f, -150f, -4f);
        detNameTxt  = dNameRT.gameObject.AddComponent<TextMeshProUGUI>();
        SetFont(detNameTxt, 44f, Color.white, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        detNameTxt.text = "";

        // Preu detall (dreta)
        var dPriceRT = StretchChild(detRT, "DPrice", 1f, 0.5f, 1f, 1f, -150f, 4f, -44f, -4f);
        detPriceTxt  = dPriceRT.gameObject.AddComponent<TextMeshProUGUI>();
        SetFont(detPriceTxt, 44f, new Color(1f, 0.9f, 0.15f), FontStyles.Bold, TextAlignmentOptions.MidlineRight);
        detPriceTxt.text = "";

        var detCoinRT = MakeRT("DCoin", detRT);
        detCoinRT.anchorMin = new Vector2(1f, 0.75f); detCoinRT.anchorMax = new Vector2(1f, 0.75f);
        detCoinRT.sizeDelta = new Vector2(36f, 36f);
        detCoinRT.anchoredPosition = new Vector2(-24f, 0f);
        var dcImg = detCoinRT.gameObject.AddComponent<Image>();
        dcImg.sprite = LoadSprite("Art/Sprites/pixel_coin");
        dcImg.preserveAspect = true;

        var dDescRT = StretchChild(detRT, "DDesc", 0f, 0f, 1f, 0.5f, 152f, 4f, -14f, -4f);
        detDescTxt  = dDescRT.gameObject.AddComponent<TextMeshProUGUI>();
        SetFont(detDescTxt, 28f, new Color(0.62f, 0.62f, 0.62f), FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        detDescTxt.text = "";

        // ─── Capacitat ───
        var capRT = TopZone(rightPanel, "Cap", ref fromTop, H_CAP);
        capTxt    = capRT.gameObject.AddComponent<TextMeshProUGUI>();
        SetFont(capTxt, 26f, new Color(0.5f, 0.5f, 0.5f), FontStyles.Normal, TextAlignmentOptions.Center);
        capTxt.text = $"Capacity: {inv.Items.Count} / {inv.maxItemsCapacity}";

        // ─── Àrees inferiors fixes (Bottom-up maquetat) ───
        float fromBottom = 0f;

        var hintRT = BotZone(rightPanel, "Hint", ref fromBottom, H_HINT);
        TxtFill(hintRT, "FLETXES / WASD  moure    |    E / ENTER  confirmar    |    TAB  canviar mode    |    ESC  tancar",
                20f, new Color(0.40f, 0.40f, 0.40f), FontStyles.Normal, TextAlignmentOptions.Center);

        // Botó EXIT flotant interactuable
        var exitRT = BotZone(rightPanel, "Exit", ref fromBottom, H_EXIT, 3f);
        exitBg     = exitRT.gameObject.AddComponent<Image>();
        exitBg.color = new Color(0.12f, 0.22f, 0.38f, 1f);
        exitTxt   = TxtFill(exitRT, "    [ RETORN ]", 38f, new Color(0.2f, 0.92f, 1f), FontStyles.Bold, TextAlignmentOptions.Center);

        // Tecla decorativa ESC amb pressió per codi
        var escGO = new GameObject("EscKey");
        var escBtnRT = escGO.AddComponent<RectTransform>();
        escBtnRT.SetParent(exitRT, false);
        escBtnRT.pivot = new Vector2(0.5f, 0.5f);
        escBtnRT.anchorMin = new Vector2(0.5f, 0.5f);
        escBtnRT.anchorMax = new Vector2(0.5f, 0.5f);
        escBtnRT.sizeDelta = new Vector2(76f, 36f);
        escBtnRT.anchoredPosition = new Vector2(-150f, 0f);

        var escBaseGO = new GameObject("Base");
        escBaseGO.transform.SetParent(escBtnRT, false);
        var escBaseRT = escBaseGO.AddComponent<RectTransform>();
        escBaseRT.anchorMin = Vector2.zero; escBaseRT.anchorMax = Vector2.one;
        escBaseRT.offsetMin = escBaseRT.offsetMax = Vector2.zero;
        var escBaseImg = escBaseGO.AddComponent<Image>();
        escBaseImg.color = new Color(0.02f, 0.02f, 0.02f, 1f);

        var escTopGO = new GameObject("Top");
        escTopGO.transform.SetParent(escBtnRT, false);
        escTopRT_Ref = escTopGO.AddComponent<RectTransform>();
        escTopRT_Ref.anchorMin = Vector2.zero; escTopRT_Ref.anchorMax = Vector2.one;
        escTopRT_Ref.offsetMin = escTopRT_Ref.offsetMax = Vector2.zero;
        escTopRT_Ref.anchoredPosition = new Vector2(0f, 4f);
        
        var escTopImg = escTopGO.AddComponent<Image>();
        escTopImg.color = new Color(0.12f, 0.12f, 0.12f, 1f);
        var escTopOl = escTopGO.AddComponent<Outline>();
        escTopOl.effectColor = new Color(0.40f, 0.40f, 0.40f, 1f);
        escTopOl.effectDistance = new Vector2(2f, -2f);

        TxtFill(escTopRT_Ref, "ESC", 24f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);

        // ─── Graella Central de Cel·les ───
        var gridRT_zone = MakeRT("GridZone", rightPanel);
        gridRT_zone.anchorMin = new Vector2(0f, 0f);
        gridRT_zone.anchorMax = new Vector2(1f, 1f);
        gridRT_zone.offsetMin = new Vector2(6f, fromBottom + 3f);
        gridRT_zone.offsetMax = new Vector2(-6f, -(fromTop + 2f));

        var grid = MakeRT("Grid", gridRT_zone);
        grid.anchorMin = Vector2.zero; grid.anchorMax = Vector2.one;
        grid.offsetMin = Vector2.zero; grid.offsetMax = Vector2.zero;

        glg = grid.gameObject.AddComponent<GridLayoutGroup>();
        glg.cellSize        = new Vector2(200f, 60f);
        glg.spacing         = new Vector2(6f, 6f);
        glg.startCorner     = GridLayoutGroup.Corner.UpperLeft;
        glg.startAxis       = GridLayoutGroup.Axis.Horizontal;
        glg.childAlignment  = TextAnchor.UpperCenter;
        glg.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = NCOLS;
        glg.padding         = new RectOffset(4, 4, 4, 4);

        glgRT     = grid;
        gridParent = grid.transform;

        BuildEntries(inv);
        selIdx = entries.Count > 0 ? 0 : EXIT_IDX;
        RefreshDetail();
        RefreshHighlights();
    }
    
    private Sprite LoadSprite(string path)
    {
#if UNITY_EDITOR
        var sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/{path}.png");
        if (sp != null) return sp;
#endif
        return Resources.Load<Sprite>(path);
    }

    private bool inputBlocked = true;
    private RectTransform cardRT_Ref;
    
    private void Start() => StartCoroutine(IntroRoutine());
    
    /// <summary>
    /// Corrutina d'entrada vertical Slide de la targeta. 
    /// Calcula dinàmicament l'amplada física de les cel·les al primer frame.
    /// </summary>
    private IEnumerator IntroRoutine()
    {
        Vector2 finalOffsetMin = Vector2.zero;
        Vector2 finalOffsetMax = Vector2.zero;
        Vector2 startOffset = new Vector2(0f, -1500f);

        if (cardRT_Ref != null)
        {
            finalOffsetMin = cardRT_Ref.offsetMin;
            finalOffsetMax = cardRT_Ref.offsetMax;
            cardRT_Ref.offsetMin = finalOffsetMin + startOffset;
            cardRT_Ref.offsetMax = finalOffsetMax + startOffset;
        }

        yield return null; // Esperem 1 frame fins que el Canvas estigui representat correctament

        // recalcul geomètric de les cel·les per a evitar overlap en píxel art
        if (glg != null && glgRT != null)
        {
            float w     = glgRT.rect.width;
            float padH  = glg.padding.left + glg.padding.right;
            float cellW = (w - padH - glg.spacing.x * (NCOLS - 1)) / NCOLS;
            glg.cellSize = new Vector2(Mathf.Max(cellW, 60f), glg.cellSize.y);
        }
        
        if (cardRT_Ref != null)
        {
            float elapsed = 0f;
            float dur = 0.35f;
            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                float easeOut = 1f - Mathf.Pow(1f - t, 3f); // OutCubic suavitzat
                
                cardRT_Ref.offsetMin = Vector2.Lerp(finalOffsetMin + startOffset, finalOffsetMin, easeOut);
                cardRT_Ref.offsetMax = Vector2.Lerp(finalOffsetMax + startOffset, finalOffsetMax, easeOut);
                yield return null;
            }
            cardRT_Ref.offsetMin = finalOffsetMin;
            cardRT_Ref.offsetMax = finalOffsetMax;
        }
        
        inputBlocked = false;
    }

    private IEnumerator AdjustGrid()
    {
        yield return null;
        if (glg != null && glgRT != null)
        {
            float w     = glgRT.rect.width;
            float padH  = glg.padding.left + glg.padding.right;
            float cellW = (w - padH - glg.spacing.x * (NCOLS - 1)) / NCOLS;
            glg.cellSize = new Vector2(Mathf.Max(cellW, 60f), glg.cellSize.y);
        }
    }

    /// <summary>
    /// Pobla la botiga segons estiguem a pestanya COMPRAR (llista del botiguer) o VENDRE (llista de motxilla).
    /// </summary>
    private void BuildEntries(PlayerInventory inv)
    {
        entries.Clear();
        foreach (Transform c in gridParent) Destroy(c.gameObject);

        RefreshTabs();

        if (currentMode == ShopMode.Buy)
        {
            if (inv.shopItems != null)
            {
                foreach (var profile in inv.shopItems)
                {
                    if (profile == null) continue;
                    CreateCell(profile.itemName, profile, 1, profile.buyPrice);
                }
            }
        }
        else // ShopMode.Sell (agrupem per recompte per a evitar botons duplicats)
        {
            var counts = new Dictionary<string, int>();
            foreach (var i in inv.Items) { counts.TryGetValue(i, out int n); counts[i] = n + 1; }

            foreach (var kvp in counts)
            {
                ItemProfile p = inv.GetItemProfile(kvp.Key);
                int price = p != null ? p.sellPrice : 0;
                CreateCell(kvp.Key, p, kvp.Value, price);
            }
        }

        if (entries.Count == 0 && selIdx != EXIT_IDX) selIdx = EXIT_IDX;
        else if (entries.Count > 0 && selIdx >= entries.Count) selIdx = entries.Count - 1;
        else if (entries.Count > 0 && selIdx == EXIT_IDX) selIdx = 0;
    }

    /// <summary>
    /// Aplica els canvis de colors i transicions de temàtica gràfica segons el mode (Comprar / Vendre).
    /// </summary>
    private void RefreshTabs()
    {
        bool isBuy = (currentMode == ShopMode.Buy);
        buyTabBg.color = isBuy ? new Color(0.18f, 0.10f, 0.34f, 1f) : new Color(0.09f, 0.05f, 0.17f, 1f);
        buyTabTxt.color = isBuy ? new Color(1f, 0.92f, 0.2f) : new Color(0.4f, 0.4f, 0.4f);
        buyTabTxt.text = isBuy ? "BUY" : "       BUY";
        
        sellTabBg.color = !isBuy ? new Color(0.34f, 0.10f, 0.18f, 1f) : new Color(0.17f, 0.05f, 0.09f, 1f);
        sellTabTxt.color = !isBuy ? new Color(1f, 0.5f, 0.2f) : new Color(0.4f, 0.4f, 0.4f);
        sellTabTxt.text = !isBuy ? "SELL" : "       SELL";

        if (tabKeyBtnRT != null)
        {
            tabKeyBtnRT.SetParent(isBuy ? sellTabBg.transform : buyTabBg.transform, false);
            tabKeyBtnRT.anchoredPosition = new Vector2(isBuy ? -90f : -95f, 0f);
        }

        // ── APLICACIÓ DELS CANVIS CROMÀTICS DINÀMICS DE LA UI ──
        if (isBuy)
        {
            if (cardImg) cardImg.color = new Color(0.07f, 0.06f, 0.15f, 1f); // Fons blau profund
            if (cardOl) cardOl.effectColor = new Color(0.15f, 0.85f, 0.95f, 1f); // Contorn celeste
            if (statsImg) statsImg.color = new Color(0.04f, 0.03f, 0.11f, 1f);
            if (detImg) detImg.color = new Color(0.08f, 0.10f, 0.20f, 1f);
            if (frameImg) frameImg.color = new Color(0.15f, 0.18f, 0.30f, 1f);
            if (frameOl) frameOl.effectColor = new Color(0.15f, 0.85f, 0.95f, 0.65f);
            if (divImg) divImg.color = new Color(0.15f, 0.85f, 0.95f, 0.25f);
        }
        else
        {
            if (cardImg) cardImg.color = new Color(0.15f, 0.06f, 0.06f, 1f); // Fons vermell profund
            if (cardOl) cardOl.effectColor = new Color(0.95f, 0.4f, 0.15f, 1f); // Contorn taronja fogós
            if (statsImg) statsImg.color = new Color(0.11f, 0.03f, 0.03f, 1f);
            if (detImg) detImg.color = new Color(0.20f, 0.08f, 0.08f, 1f);
            if (frameImg) frameImg.color = new Color(0.30f, 0.15f, 0.15f, 1f);
            if (frameOl) frameOl.effectColor = new Color(0.95f, 0.4f, 0.15f, 0.65f);
            if (divImg) divImg.color = new Color(0.95f, 0.4f, 0.15f, 0.25f);
        }
    }

    private void CreateCell(string nameStr, ItemProfile p, int count, int price)
    {
        var cell = new GameObject($"C_{nameStr}");
        cell.transform.SetParent(gridParent, false);
        cell.AddComponent<RectTransform>();
        var bg = cell.AddComponent<Image>();
        bg.color = currentMode == ShopMode.Buy ? new Color(0.12f, 0.15f, 0.25f, 1f) : new Color(0.25f, 0.12f, 0.12f, 1f);
        bg.raycastTarget = false;

        var tGo = new GameObject("T");
        tGo.transform.SetParent(cell.transform, false);
        var tRT = tGo.AddComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
        tRT.offsetMin = new Vector2(6f, 30f); tRT.offsetMax = new Vector2(-6f, -4f); 
        var txt = tGo.AddComponent<TextMeshProUGUI>();
        SetFont(txt, 30f, Color.white, FontStyles.Bold, TextAlignmentOptions.Bottom);
        txt.text         = count > 1 ? $"{nameStr}  <color=#AAAAAA>x{count}</color>" : $"{nameStr}";
        txt.raycastTarget = false;

        var pGo = new GameObject("PGroup");
        pGo.transform.SetParent(cell.transform, false);
        var pRT = pGo.AddComponent<RectTransform>();
        pRT.anchorMin = new Vector2(0f, 0f); pRT.anchorMax = new Vector2(1f, 0f);
        pRT.sizeDelta = new Vector2(0f, 30f);
        pRT.anchoredPosition = new Vector2(0f, 15f);
        
        var hlg = pGo.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlHeight = true; hlg.childControlWidth = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        hlg.spacing = 8f;

        var priceGo = new GameObject("PriceNum");
        priceGo.transform.SetParent(pGo.transform, false);
        var pTxt = priceGo.AddComponent<TextMeshProUGUI>();
        SetFont(pTxt, 28f, new Color(1f, 0.85f, 0f), FontStyles.Bold, TextAlignmentOptions.Center);
        pTxt.text = price.ToString();

        var cGo = new GameObject("CoinIcon");
        cGo.transform.SetParent(pGo.transform, false);
        var cRT = cGo.AddComponent<RectTransform>();
        var cLe = cGo.AddComponent<LayoutElement>();
        cLe.minWidth = 24f; cLe.minHeight = 24f;
        cLe.preferredWidth = 24f; cLe.preferredHeight = 24f;
        var cImg = cGo.AddComponent<Image>();
        cImg.sprite = LoadSprite("Art/Sprites/pixel_coin");
        cImg.preserveAspect = true;

        entries.Add(new ShopEntry { idName = nameStr, profile = p, count = count, bg = bg, txt = txt });
    }

    private void RefreshHighlights()
    {
        bool isBuy = (currentMode == ShopMode.Buy);
        bool exitSel  = (selIdx == EXIT_IDX);
        if (isBuy)
        {
            exitBg.color  = exitSel ? new Color(0.18f, 0.40f, 0.60f) : new Color(0.12f, 0.22f, 0.38f);
            exitTxt.color = exitSel ? new Color(0.2f, 1f, 0.92f) : new Color(0.6f, 0.55f, 0.75f);
        }
        else
        {
            exitBg.color  = exitSel ? new Color(0.60f, 0.18f, 0.18f) : new Color(0.38f, 0.12f, 0.12f);
            exitTxt.color = exitSel ? new Color(1f, 0.4f, 0.2f) : new Color(0.75f, 0.55f, 0.55f);
        }

        for (int i = 0; i < entries.Count; i++)
        {
            bool s = (i == selIdx);
            if (entries[i].bg != null) 
            {
                if (isBuy) entries[i].bg.color  = s ? new Color(0.22f, 0.28f, 0.50f) : new Color(0.12f, 0.15f, 0.25f);
                else       entries[i].bg.color  = s ? new Color(0.50f, 0.22f, 0.22f) : new Color(0.25f, 0.12f, 0.12f);
            }
            if (entries[i].txt != null)
            {
                if (isBuy) entries[i].txt.color = s ? new Color(0.2f, 1f, 0.92f) : Color.white;
                else       entries[i].txt.color = s ? new Color(1f, 0.4f, 0.2f) : Color.white;
            }
        }
    }

    private void RefreshDetail()
    {
        if (selIdx == EXIT_IDX || selIdx < 0 || selIdx >= entries.Count)
        {
            detNameTxt.text  = ""; detDescTxt.text = ""; detIconImg.color = new Color(1f, 1f, 1f, 0f);
            detPriceTxt.text = "";
            return;
        }
        var e = entries[selIdx];
        detNameTxt.text = currentMode == ShopMode.Sell ? $"{e.idName}  <color=#AAAAAA>x{e.count}</color>" : e.idName;
        detDescTxt.text = e.profile != null ? e.profile.itemDescription : "(sense llibre de dades de l'objecte)";
        
        int p = 0;
        if (e.profile != null)
        {
            p = currentMode == ShopMode.Buy ? e.profile.buyPrice : e.profile.sellPrice;
            if (e.profile.itemIcon != null) { detIconImg.sprite = e.profile.itemIcon; detIconImg.color = Color.white; }
            else detIconImg.color = new Color(1f, 1f, 1f, 0f);
        }
        else detIconImg.color = new Color(1f, 1f, 1f, 0f);
        
        detPriceTxt.text = $"{p}";
    }

    // ── NAVEGACIÓ I REPRESSIÓ DE CONTROLS ──
    private void Update()
    {
        // Tecles físiques en 3D decoratives
        if (tabTopRT_Ref != null)
        {
            float cycle = Time.unscaledTime * 1.5f;
            bool isPressed = (cycle % 1f) > 0.7f;
            if (isPressed != lastTabPressed)
            {
                lastTabPressed = isPressed;
                tabTopRT_Ref.anchoredPosition = isPressed ? Vector2.zero : new Vector2(0f, 4f);
            }
        }

        if (escTopRT_Ref != null)
        {
            float cycle = Time.unscaledTime * 1.5f;
            bool isPressed = (cycle % 1f) > 0.7f;
            if (isPressed != lastEscPressed)
            {
                lastEscPressed = isPressed;
                escTopRT_Ref.anchoredPosition = isPressed ? Vector2.zero : new Vector2(0f, 4f);
            }
        }

        if (inputBlocked) return;
        
        bool left  = Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow);
        bool right = Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow);
        bool up    = Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow);
        bool down  = Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow);
        bool use   = Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return);
        bool close = Input.GetKeyDown(KeyCode.Escape);
        bool tab   = Input.GetKeyDown(KeyCode.Tab);

        if (tab)
        {
            currentMode = currentMode == ShopMode.Buy ? ShopMode.Sell : ShopMode.Buy;
            var inv = PlayerInventory.Instance;
            if (inv != null && inv.navSound != null) ItemSoundPlayer.Play(inv.navSound);
            BuildEntries(PlayerInventory.Instance);
            RefreshDetail();
            RefreshHighlights();
            StartCoroutine(AdjustGrid());
            return;
        }

        if (close || (use && selIdx == EXIT_IDX)) 
        { 
            if (use && PlayerInventory.Instance != null && PlayerInventory.Instance.selectSound != null) ItemSoundPlayer.Play(PlayerInventory.Instance.selectSound);
            CloseMenu(); 
            return; 
        }

        if (entries.Count == 0) { if (use) CloseMenu(); return; }

        int totalRows = Mathf.CeilToInt(entries.Count / (float)NCOLS);
        bool moved = false;

        if (selIdx == EXIT_IDX)
        {
            if (up) { selIdx = (totalRows - 1) * NCOLS; moved = true; }
        }
        else
        {
            int row = selIdx / NCOLS;
            int col = selIdx % NCOLS;
            int colsThisRow = Mathf.Min(NCOLS, entries.Count - row * NCOLS);

            if (left  && col > 0)             { selIdx--; moved = true; }
            if (right && col < colsThisRow-1)  { selIdx++; moved = true; }
            if (up)
            {
                if (row > 0) { int nr = row - 1; selIdx = nr * NCOLS + Mathf.Min(col, Mathf.Min(NCOLS, entries.Count - nr * NCOLS) - 1); }
                moved = true;
            }
            if (down)
            {
                if (row < totalRows - 1) { int nr = row + 1; selIdx = nr * NCOLS + Mathf.Min(col, Mathf.Min(NCOLS, entries.Count - nr * NCOLS) - 1); }
                else selIdx = EXIT_IDX;
                moved = true;
            }

            if (selIdx >= entries.Count) selIdx = entries.Count - 1;
        }

        if (moved) 
        { 
            RefreshDetail(); 
            RefreshHighlights(); 
            if (PlayerInventory.Instance != null && PlayerInventory.Instance.navSound != null)
                ItemSoundPlayer.Play(PlayerInventory.Instance.navSound);
        }

        if (use && selIdx != EXIT_IDX && selIdx < entries.Count)
        {
            InteractWithItem(entries[selIdx]);
        }
    }

    private Coroutine errorAnim;
    
    /// <summary>
    /// Corrutina de sacsejada d'errors (Horizontal Shake).
    /// </summary>
    private IEnumerator ShowErrorAnim(string msg)
    {
        detDescTxt.color = new Color(1f, 0.3f, 0.3f);
        detDescTxt.text = msg;
        
        Vector3 startPos = detDescTxt.rectTransform.anchoredPosition;
        float elapsed = 0f;
        while(elapsed < 1.5f)
        {
             elapsed += Time.unscaledDeltaTime;
             if(elapsed < 0.3f) 
             {
                 detDescTxt.rectTransform.anchoredPosition = startPos + new Vector3(Mathf.Sin(elapsed * 50f) * 8f, 0f, 0f);
             }
             else 
             {
                 detDescTxt.rectTransform.anchoredPosition = startPos;
                 detDescTxt.color = Color.Lerp(new Color(1f, 0.3f, 0.3f), new Color(0.62f, 0.62f, 0.62f), (elapsed - 0.3f) / 1.2f);
             }
             yield return null;
        }
        
        detDescTxt.rectTransform.anchoredPosition = startPos;
        RefreshDetail();
    }

    /// <summary>
    /// Aplica una rebregada de diàleg i de retrat del botiguer.
    /// Triarà la veu de murmuri d'esquenes a les expressions gràfiques.
    /// </summary>
    private void ApplyShopVariant(ShopDialogVariant variant)
    {
        if (variant == null) return;
        
        if (typeCoroutine != null) StopCoroutine(typeCoroutine);
        typeCoroutine = StartCoroutine(TypewriteText(variant.text));
        
        if (npcImg != null)
        {
            if (shakeCoroutine != null) StopCoroutine(shakeCoroutine);
            shakeCoroutine = StartCoroutine(ShakeSprite(npcImg.rectTransform));
        }
        
        var inv = PlayerInventory.Instance;
        if (inv == null || npcImg == null) return;
        
        Sprite sp = variant.expressionSprite != null ? variant.expressionSprite : inv.shopkeeperSprite;
        if (sp != null)
        {
            npcImg.sprite = sp;
            npcImg.color = Color.white;
        }
        else
        {
            npcImg.color = new Color(1f, 1f, 1f, 0f);
        }
    }

    private Sprite generatedRoundedSprite;
    
    /// <summary>
    /// Generador procedural gràfic d'una bombolla arrodonida píxel-art tileable de 12x12.
    /// </summary>
    private Sprite GetRoundedSprite()
    {
        if (generatedRoundedSprite != null) return generatedRoundedSprite;
        int size = 12;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        Color w = Color.white;
        Color c = new Color(1f, 1f, 1f, 0f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Eliminem manualment els 3 píxels de cada cantonada de l'escala
                bool corner = false;
                if (x==0 && y<=2) corner = true;
                else if (x==1 && y<=1) corner = true;
                else if (x==2 && y==0) corner = true;
                else if (x==size-1 && y<=2) corner = true;
                else if (x==size-2 && y<=1) corner = true;
                else if (x==size-3 && y==0) corner = true;
                else if (x==0 && y>=size-3) corner = true;
                else if (x==1 && y>=size-2) corner = true;
                else if (x==2 && y>=size-1) corner = true;
                else if (x==size-1 && y>=size-3) corner = true;
                else if (x==size-2 && y>=size-2) corner = true;
                else if (x==size-3 && y>=size-1) corner = true;

                tex.SetPixel(x, y, corner ? c : w);
            }
        }
        tex.Apply();
        
        // Retornem un Sliced Sprite que es manté visualment lluent de 4 cantonades fixes
        generatedRoundedSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(4, 4, 4, 4));
        return generatedRoundedSprite;
    }

    /// <summary>
    /// Corrutina de typewriter per a la bombolla del botiguer.
    /// Emet murmuris cada 2 lletres variant el Pitch de forma orgànica.
    /// </summary>
    private IEnumerator TypewriteText(string text)
    {
        if (dialogTxt != null) dialogTxt.text = "";
        var inv = PlayerInventory.Instance;
        int count = 0;
        
        for (int i = 0; i < text.Length; i++)
        {
            if (dialogTxt != null) dialogTxt.text += text[i];
            
            if (char.IsLetterOrDigit(text[i]) && inv != null && inv.shopVoiceSound != null && typeAudioSrc != null)
            {
                if (count % 2 == 0) 
                {
                    typeAudioSrc.pitch = UnityEngine.Random.Range(0.85f, 1.15f);
                    typeAudioSrc.PlayOneShot(inv.shopVoiceSound, 0.6f);
                }
            }
            count++;
            
            // Pauses dinàmiques narratives
            if (text[i] == '.' || text[i] == '?' || text[i] == '!') yield return new WaitForSecondsRealtime(0.25f);
            else if (text[i] == ',') yield return new WaitForSecondsRealtime(0.15f);
            else yield return new WaitForSecondsRealtime(0.015f);
        }
    }

    /// <summary>
    /// Corrutina de sacsejada física bidimensional (Juicy Shake) de la criatura.
    /// Emet decreixements de força exponencials molt retro.
    /// </summary>
    private IEnumerator ShakeSprite(RectTransform rt)
    {
        if (rt == null) yield break;
        Vector2 startPos = Vector2.zero;
        float elapsed = 0f;
        float duration = 0.25f;
        
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float strength = (1f - (elapsed / duration)) * 8f;
            rt.anchoredPosition = startPos + new Vector2(UnityEngine.Random.Range(-strength, strength), UnityEngine.Random.Range(-strength, strength));
            yield return null;
        }
        rt.anchoredPosition = startPos;
    }

    /// <summary>
    /// Lògica mestra de transaccions. Dissenya reaccions, càlculs d'or i alertes de seguretat.
    /// </summary>
    private void InteractWithItem(ShopEntry entry)
    {
        var inv = PlayerInventory.Instance;
        if (inv == null || entry.profile == null) return;

        // ── MODE COMPRAR (BUY) ──
        if (currentMode == ShopMode.Buy)
        {
            // Alerta: Motxilla plena
            if (inv.Items.Count >= inv.maxItemsCapacity)
            {
                ApplyShopVariant(inv.GetRandomMsg(inv.shopInventoryFullMsgs));
                if (errorAnim != null) StopCoroutine(errorAnim);
                errorAnim = StartCoroutine(ShowErrorAnim("INVENTARI PLÈ!"));
                return;
            }

            int price = entry.profile.buyPrice;
            // Alerta: Falta d'or
            if (!inv.SpendGold(price))
            {
                ApplyShopVariant(inv.GetRandomMsg(inv.shopCantAffordMsgs));
                if (errorAnim != null) StopCoroutine(errorAnim);
                errorAnim = StartCoroutine(ShowErrorAnim("SENSE OR COMPLERT!"));
                return;
            }

            inv.AddItem(entry.profile.itemName);
            ApplyShopVariant(inv.GetRandomMsg(inv.shopBuyMsgs)); // Reacció agraïda
            PlaySuccess(inv);
        }
        // ── MODE VENDRE (SELL) ──
        else 
        {
            int price = entry.profile.sellPrice;
            if (inv.RemoveItem(entry.idName))
            {
                inv.AddGold(price);
                ApplyShopVariant(inv.GetRandomMsg(inv.shopSellMsgs)); // Reacció venedora
                PlaySuccess(inv);
            }
            else
            {
                ApplyShopVariant(inv.GetRandomMsg(inv.shopCantAffordMsgs));
                if (errorAnim != null) StopCoroutine(errorAnim);
                errorAnim = StartCoroutine(ShowErrorAnim("NO S'HA POGUT VENDRE."));
            }
        }

        goldTxt.text  = $"{inv.Gold}";
        capTxt.text   = $"Capacity: {inv.Items.Count} / {inv.maxItemsCapacity}";
        
        BuildEntries(inv);
        StartCoroutine(AdjustGrid());
        RefreshDetail(); 
        RefreshHighlights();
    }
    
    private void PlaySuccess(PlayerInventory inv)
    {
        if (currentMode == ShopMode.Buy && inv.shopBuySound != null)
            ItemSoundPlayer.Play(inv.shopBuySound);
        else if (currentMode == ShopMode.Sell && inv.shopSellSound != null)
            ItemSoundPlayer.Play(inv.shopSellSound);
        else if (inv.selectSound != null)
            ItemSoundPlayer.Play(inv.selectSound);
    }

    private void CloseMenu() 
    { 
        IsOpen = false; 
        if (!onCloseInvoked)
        {
            onCloseInvoked = true;
            onClose?.Invoke();
        }
        Destroy(gameObject); 
    }
    
    private void OnDestroy()  
    { 
        IsOpen = false; 
        if (!onCloseInvoked)
        {
            onCloseInvoked = true;
            onClose?.Invoke();
        }
    }

    // =========================================================================
    // UTILS RT (Generadors Procedimentals RectTransform)
    // =========================================================================
    private RectTransform MakeRT(string n, Transform parent)
    { var go = new GameObject(n); go.transform.SetParent(parent, false); return go.AddComponent<RectTransform>(); }
    private RectTransform MakeRT(string n, RectTransform parent) => MakeRT(n, parent.transform);

    private RectTransform TopZone(RectTransform parent, string n, ref float used, float h, float marginTop = 0f)
    {
        var rt = MakeRT(n, parent);
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = new Vector2(0f, -(used + h));
        rt.offsetMax = new Vector2(0f, -(used + marginTop));
        used += h + marginTop;
        return rt;
    }

    private RectTransform BotZone(RectTransform parent, string n, ref float used, float h, float marginBot = 0f)
    {
        var rt = MakeRT(n, parent);
        rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f);
        rt.offsetMin = new Vector2(0f, used + marginBot);
        rt.offsetMax = new Vector2(0f, used + h + marginBot);
        used += h + marginBot;
        return rt;
    }

    private RectTransform StretchChild(RectTransform parent, string n,
        float ax, float ay, float bx, float by, float oL = 0f, float oB = 0f, float oR = 0f, float oT = 0f)
    {
        var rt = MakeRT(n, parent);
        rt.anchorMin = new Vector2(ax, ay); rt.anchorMax = new Vector2(bx, by);
        rt.offsetMin = new Vector2(oL, oB); rt.offsetMax = new Vector2(oR, oT);
        return rt;
    }

    private RectTransform PointChild(RectTransform parent, string n,
        float ax, float ay, float px, float py, float posX, float posY, float w, float h)
    {
        var rt = MakeRT(n, parent);
        rt.anchorMin = new Vector2(ax, ay); rt.anchorMax = new Vector2(ax, ay);
        rt.pivot = new Vector2(px, py);
        rt.anchoredPosition = new Vector2(posX, posY); rt.sizeDelta = new Vector2(w, h);
        return rt;
    }

    private TextMeshProUGUI TxtFill(RectTransform parent, string text, float size, Color col, FontStyles style, TextAlignmentOptions align)
    {
        var rt = MakeRT("T", parent);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        SetFont(t, size, col, style, align); t.text = text; t.raycastTarget = false;
        return t;
    }

    // ── LOCALITZACIÓ DE FONTS ──
    private void SetFont(TextMeshProUGUI t, float size, Color col, FontStyles style, TextAlignmentOptions align)
    {
        t.fontSize = size; t.color = col; t.fontStyle = style; t.alignment = align;
        var f = LoadFont("determination SDF");
        if (f == null) f = LoadFont("PixelOperator SDF");
        if (f == null) f = LoadFont("8bitoperator_jve SDF");
        if (f != null) t.font = f;
    }
    
    private TMP_FontAsset LoadFont(string n)
    {
#if UNITY_EDITOR
        var f = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"Assets/Fonts/{n}.asset");
        if (f != null) return f;
        f = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            $"Assets/TextMesh Pro/Resources/Fonts & Materials/{n}.asset");
        if (f != null) return f;
#endif
        var r = Resources.Load<TMP_FontAsset>($"Fonts & Materials/{n}"); 
        return r ?? Resources.Load<TMP_FontAsset>($"Fonts/{n}") ?? Resources.Load<TMP_FontAsset>(n);
    }
}
// Final de la línia física
