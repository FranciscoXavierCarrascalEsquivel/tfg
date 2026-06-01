using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Interfície gràfica de l'Inventari i la Motxilla del Jugador (InventoryMenuUI).
/// Aquest component és el responsable de maquetar completament per codi (procedimentalment)
/// el menú d'inventari on s'agrupen tant els objectes curatius/clau (ITEMS) com les criatures aliades (RECRUITED).
/// 
/// IMPLEMENTACIÓ DE DISSENY DEL TFG:
/// - **Tabs dinàmiques**: Commutació de pestanyes mitjançant la tecla TAB d'esquenes a la simulació del món.
/// - **Càlcul geomètric de graelles**: Determina a temps real l'amplada física de les cel·les (GridLayoutGroup)
///   segons la resolució activa de pantalla per evitar overlapings i talls de textures de tipus píxel-art.
/// - **Animació Slide unscaled**: Animacions d'entrada i sortida verticals (lliscament cubic suau) deslligades del
///   TimeScale per garantir el seu funcionament correcte durant el congelament de la pausa.
/// - **Feedback de curació procedimental**: Llança un text de curació verd flotant que puja i s'esvaeix dinàmicament (+HP).
/// - **Shake d'errors**: Sacseja horitzontalment els textos descriptius amb color vermell d'alerta si es cometen accions invàlides.
/// - **Barres de reclutament segmentades**: Dibuixa procedimentalment l'estat d'amistat de cada criatura mitjançant barres grogues
///   amb subdivisions negres segons el límit de reclutament configurable de cadascuna d'elles.
/// </summary>
public class InventoryMenuUI : MonoBehaviour
{
    public static bool IsOpen { get; set; } // Flag global d'estat per congelar controls de mapa

    private bool inCombat; // Cert si estem obrint l'inventari enmig d'un combat (només es permetrà usar certs ítems)
    private Action<ItemProfile> onItemSelected; // Callback en utilitzar un objecte curatiu
    private Action onClose;

    private readonly List<InventoryEntry> entries = new List<InventoryEntry>(); // Elements visualitzats actuals
    private int selIdx = -1;  // Índex de selecció actiu (-1 = botó de sortida EXIT)
    private const int EXIT_IDX = -1;
    private const int NCOLS    = 2; // Graella de dues columnes perfectes

    private enum InventoryMode { Items, Recruits }
    private InventoryMode currentMode = InventoryMode.Items; // Pestanya activa per defecte

    // Referències tipogràfiques i visuals de zones
    private TextMeshProUGUI hpTxt, goldTxt, capTxt;
    private Image itemsTabBg, recruitsTabBg;
    private TextMeshProUGUI itemsTabTxt, recruitsTabTxt;
    private RectTransform tabKeyBtnRT; // Indicador de tecla TAB
    private GameObject      detZoneGO;  // Panell de detall d'objectes
    private GameObject      statsZoneGO; // Barra d'estat (Vida, Or)
    private GameObject      capZoneGO;   // Indicador de capacitat de motxilla
    private RectTransform   gridZoneRT;  // Contenidor físic de la graella de cel·les
    private float           topOffsetWithDetAndStats = 0f; // Espai superior lliure en mode Items
    private float           topOffsetNoDetNoStats = 0f;    // Espai superior lliure en mode Recruits (més ampli)
    private TextMeshProUGUI detNameTxt, detDescTxt;
    private Image           detIconImg;
    private RectTransform   hpRT;
    private Transform       gridParent;
    private Image           exitBg;
    private TextMeshProUGUI exitTxt;
    private GridLayoutGroup glg;
    private RectTransform   glgRT;
    private RectTransform   escTopRT;      // Tecla visual "ESC" animada
    private RectTransform   tabTopRT_Ref;  // Tecla visual "TAB" animada
    private bool            lastEscPressed;
    private bool            lastTabPressed;

    /// <summary>
    /// Model de dades utilitari per a cada cel·la dibuixada a la graella.
    /// </summary>
    private class InventoryEntry
    {
        public string name; 
        public ItemProfile profile; 
        public EnemyProfile enemyProfile; 
        public int count; 
        public bool canUse;
        public Image bg; 
        public TextMeshProUGUI txt;
    }

    // ── FACTORY SINGLETON (Mètode d'Instanciació i Setup) ──────────────────
    /// <summary>
    /// Obre la motxilla instanciant el Canvas asíncronament i preparant els nodes de depuració visual.
    /// </summary>
    public static void Show(bool isCombat, Action<ItemProfile> onItemSelected, Action onClose = null)
    {
        var canvas = CanvasHelper.GetMainCanvas();
        if (canvas == null) return;
        
        // Creació del GameObject pare procedimental
        var go = new GameObject("InventoryMenuUI");
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(canvas.transform, false);
        rt.anchorMin = rt.offsetMin = Vector2.zero;
        rt.anchorMax = Vector2.one; rt.offsetMax = Vector2.zero;
        
        var ui = go.AddComponent<InventoryMenuUI>();
        
        // Forcem ordre d'ordenament gràfic superior
        var cv = go.AddComponent<Canvas>();
        cv.overrideSorting = true;
        cv.sortingOrder = 30000; // Col·locat immediatament a sota de la consola de depuració
        go.AddComponent<GraphicRaycaster>(); // Imprescindible per rebre els Raycasts del ratolí/punters
        
        ui.inCombat = isCombat; 
        ui.onItemSelected = onItemSelected; 
        ui.onClose = onClose;
        
        ui.Build(); 
        IsOpen = true;
    }

    // ── CONSTRUCCIÓ PROCEDIMENTAL COMPACTA (SENSE PREFABS) ────────────────
    /// <summary>
    /// Aixeca dinàmicament totes les àrees de la targeta (Títol, Pestanyes, Detall, Exit, Tecles i Graella).
    /// </summary>
    private void Build()
    {
        var inv = PlayerInventory.Instance;

        // Fons semitransparent que enfosqueix la visualització del joc
        var bgImg = gameObject.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.78f);
        bgImg.raycastTarget = true;

        // ─── Targeta Contenidora Principal ───
        var card = MakeRT("Card", transform);
        cardRT_Ref = card;
        
        card.anchorMin = new Vector2(0.12f, 0.08f);
        card.anchorMax = new Vector2(0.88f, 0.92f); // Marges perfectes en pantalla de 16:9
        card.offsetMin = card.offsetMax = Vector2.zero;
        card.gameObject.AddComponent<Image>().color = new Color(0.07f, 0.06f, 0.15f, 1f); // Blau nits profund
        
        // Contorn metal·litzat de color or brillant
        var ol = card.gameObject.AddComponent<Outline>();
        ol.effectColor = new Color(0.95f, 0.80f, 0.15f, 1f);
        ol.effectDistance = new Vector2(8f, -8f);

        // Mides definides per a les franges horitzontals
        const float H_TITLE  = 68f;
        const float H_STATS  = 68f;
        const float H_DETAIL = 148f;
        const float H_CAP    = 32f;
        const float H_EXIT   = 56f;
        const float H_HINT   = 40f;

        float fromTop = 0f; // Comptador de píxels consumits per les zones superiors

        // ─── Pestanyes Superiors (ITEMS / RECRUITED) ───
        var titleRT = TopZone(card, "Title", ref fromTop, H_TITLE);
        
        var itemsRT = StretchChild(titleRT, "Items", 0f, 0f, 0.5f, 1f);
        itemsTabBg = itemsRT.gameObject.AddComponent<Image>();
        itemsTabTxt = TxtFill(itemsRT, "ITEMS", 44f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
        
        var recruitsRT = StretchChild(titleRT, "Recruits", 0.5f, 0f, 1f, 1f);
        recruitsTabBg = recruitsRT.gameObject.AddComponent<Image>();
        recruitsTabTxt = TxtFill(recruitsRT, "RECRUITED", 44f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);

        // Indicador gràfic de tecla "TAB" procedimental
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
        var tabTopRT = tabTopGO.AddComponent<RectTransform>();
        tabTopRT.anchorMin = Vector2.zero; tabTopRT.anchorMax = Vector2.one;
        tabTopRT.offsetMin = tabTopRT.offsetMax = Vector2.zero;
        tabTopRT.anchoredPosition = new Vector2(0f, 4f);
        
        var tabTopImg = tabTopGO.AddComponent<Image>();
        tabTopImg.color = new Color(0.12f, 0.12f, 0.12f, 1f);
        var tabTopOl = tabTopGO.AddComponent<Outline>();
        tabTopOl.effectColor = new Color(0.40f, 0.40f, 0.40f, 1f);
        tabTopOl.effectDistance = new Vector2(2f, -2f);

        TxtFill(tabTopRT, "TAB", 24f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);

        // ─── Àrea d'Estadístiques (Vida, Monedes) ───
        var statsRT = TopZone(card, "Stats", ref fromTop, H_STATS);
        statsZoneGO = statsRT.gameObject;
        statsRT.gameObject.AddComponent<Image>().color = new Color(0.04f, 0.03f, 0.11f, 1f);

        hpRT  = StretchChild(statsRT, "HP", 0f, 0f, 0.5f, 1f, 22f, 0f, 0f, 0f);
        hpTxt = hpRT.gameObject.AddComponent<TextMeshProUGUI>();
        SetFont(hpTxt, 44f, new Color(0.35f, 1f, 0.50f), FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        hpTxt.text = $"♥  {inv.CurrentHP} / {inv.MaxHP} HP";

        var goldRT = StretchChild(statsRT, "Gold", 0.5f, 0f, 1f, 1f, 0f, 0f, -22f, 0f);
        goldTxt    = goldRT.gameObject.AddComponent<TextMeshProUGUI>();
        SetFont(goldTxt, 44f, new Color(1f, 0.90f, 0.15f), FontStyles.Bold, TextAlignmentOptions.MidlineRight);
        goldTxt.text = $"{inv.Gold} G       ";

        // Icona de moneda píxel art dinàmica
        var coinRT = MakeRT("CoinIcon", goldRT);
        coinRT.anchorMin = new Vector2(1f, 0.5f); coinRT.anchorMax = new Vector2(1f, 0.5f);
        coinRT.sizeDelta = new Vector2(44f, 44f);
        coinRT.anchoredPosition = new Vector2(-12f, 0f);
        var coinImg = coinRT.gameObject.AddComponent<Image>();
        coinImg.preserveAspect = true;
        Sprite coinSp = LoadSprite("Art/Sprites/pixel_coin");
        if (coinSp != null) coinImg.sprite = coinSp;

        // ─── Àrea de Detalls i Descipció ───
        var detRT = TopZone(card, "Detail", ref fromTop, H_DETAIL, 2f);
        detZoneGO = detRT.gameObject;
        detRT.gameObject.AddComponent<Image>().color = new Color(0.10f, 0.08f, 0.20f, 1f);

        // Caixa per allotjar l'avatar del dibuix
        var frameRT = PointChild(detRT, "Frame", 0f, 0.5f, 0f, 0.5f, 14f, 0f, 126f, 126f);
        frameRT.gameObject.AddComponent<Image>().color = new Color(0.18f, 0.15f, 0.30f, 1f);
        var fOl = frameRT.gameObject.AddComponent<Outline>();
        fOl.effectColor = new Color(0.95f, 0.80f, 0.15f, 0.65f);
        fOl.effectDistance = new Vector2(3f, -3f);

        var iconRT = MakeRT("Icon", frameRT);
        iconRT.anchorMin = new Vector2(0.06f, 0.06f); iconRT.anchorMax = new Vector2(0.94f, 0.94f);
        iconRT.offsetMin = iconRT.offsetMax = Vector2.zero;
        detIconImg = iconRT.gameObject.AddComponent<Image>();
        detIconImg.preserveAspect = true;
        detIconImg.color = new Color(1f, 1f, 1f, 0f);

        var dNameRT = StretchChild(detRT, "DName", 0f, 0.5f, 1f, 1f, 152f, 4f, -14f, -4f);
        detNameTxt  = dNameRT.gameObject.AddComponent<TextMeshProUGUI>();
        SetFont(detNameTxt, 44f, Color.white, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        detNameTxt.text = "";

        var dDescRT = StretchChild(detRT, "DDesc", 0f, 0f, 1f, 0.5f, 152f, 4f, -14f, -4f);
        detDescTxt  = dDescRT.gameObject.AddComponent<TextMeshProUGUI>();
        SetFont(detDescTxt, 28f, new Color(0.62f, 0.62f, 0.62f), FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        detDescTxt.text = "";

        // ─── Àrea de Capacitat ───
        var capRT = TopZone(card, "Cap", ref fromTop, H_CAP);
        capZoneGO = capRT.gameObject;
        capTxt    = capRT.gameObject.AddComponent<TextMeshProUGUI>();
        topOffsetWithDetAndStats = fromTop;
        SetFont(capTxt, 26f, new Color(0.5f, 0.5f, 0.5f), FontStyles.Normal, TextAlignmentOptions.Center);
        capTxt.text = $"Capacity: {inv.Items.Count} / {inv.maxItemsCapacity}";

        // ─── Àrees fixes inferiors (Bottom-up maquetat) ───
        float fromBottom = 0f;

        var hintRT = BotZone(card, "Hint", ref fromBottom, H_HINT);
        TxtFill(hintRT, "FLETXES / WASD moure  |  E / ENTER utilitzar  |  TAB pestanya  |  ESC tancar",
                20f, new Color(0.40f, 0.40f, 0.40f), FontStyles.Normal, TextAlignmentOptions.Center);

        // Botó EXIT interactuable
        var exitRT = BotZone(card, "Exit", ref fromBottom, H_EXIT, 3f);
        exitBg     = exitRT.gameObject.AddComponent<Image>();
        exitBg.color = new Color(0.22f, 0.12f, 0.38f, 1f);
        exitTxt   = TxtFill(exitRT, "    [ SORTIR ]", 38f, new Color(1f, 0.92f, 0.2f), FontStyles.Bold, TextAlignmentOptions.Center);

        // Tecla física "ESC" decorativa amb pressió per codi
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
        escTopRT = escTopGO.AddComponent<RectTransform>();
        escTopRT.anchorMin = Vector2.zero; escTopRT.anchorMax = Vector2.one;
        escTopRT.offsetMin = escTopRT.offsetMax = Vector2.zero;
        escTopRT.anchoredPosition = new Vector2(0f, 4f);
        
        var escTopImg = escTopGO.AddComponent<Image>();
        escTopImg.color = new Color(0.12f, 0.12f, 0.12f, 1f);
        var escTopOl = escTopGO.AddComponent<Outline>();
        escTopOl.effectColor = new Color(0.40f, 0.40f, 0.40f, 1f);
        escTopOl.effectDistance = new Vector2(2f, -2f);

        TxtFill(escTopRT, "ESC", 24f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);

        // ─── Àrea de la Graella Dinàmica (Espai central resultant) ───
        var gridRT_zone = MakeRT("GridZone", card);
        gridZoneRT = gridRT_zone;
        topOffsetNoDetNoStats = H_TITLE;
        gridRT_zone.anchorMin = new Vector2(0f, 0f);
        gridRT_zone.anchorMax = new Vector2(1f, 1f);
        gridRT_zone.offsetMin = new Vector2(6f, fromBottom + 3f);
        gridRT_zone.offsetMax = new Vector2(-6f, -(fromTop + 2f));

        var grid = MakeRT("Grid", gridRT_zone);
        grid.anchorMin = Vector2.zero; grid.anchorMax = Vector2.one;
        grid.offsetMin = Vector2.zero; grid.offsetMax = Vector2.zero;

        glg = grid.gameObject.AddComponent<GridLayoutGroup>();
        glg.cellSize        = new Vector2(200f, 60f); // Es recalcularà en el primer frame actiu!
        glg.spacing         = new Vector2(6f, 6f);
        glg.startCorner     = GridLayoutGroup.Corner.UpperLeft;
        glg.startAxis       = GridLayoutGroup.Axis.Horizontal;
        glg.childAlignment  = TextAnchor.UpperCenter;
        glg.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = NCOLS;
        glg.padding         = new RectOffset(4, 4, 4, 4);

        glgRT     = grid;
        gridParent = grid.transform;

        // ─── Poblem el contingut ───
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
    /// Corrutina d'animació d'entrada Slide des de sota.
    /// També aprofita el primer frame per calcular l'amplada real de les cel·les al GridLayout.
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

        yield return null; // Un frame d'espera crucial: el motor calcula la matriu de disseny del Canvas

        // ── CÀLCUL DE LA GEOMETRIA DE LES CEL·LES ──
        if (glg != null && glgRT != null)
        {
            float w     = glgRT.rect.width;
            float padH  = glg.padding.left + glg.padding.right;
            float cellW = (w - padH - glg.spacing.x * (NCOLS - 1)) / NCOLS;
            glg.cellSize = new Vector2(Mathf.Max(cellW, 60f), glg.cellSize.y);
        }
        
        // Entrada lliscant en segons de temps real
        if (cardRT_Ref != null)
        {
            float elapsed = 0f;
            float dur = 0.35f;
            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                float easeOut = 1f - Mathf.Pow(1f - t, 3f); // Ease out cubic
                
                cardRT_Ref.offsetMin = Vector2.Lerp(finalOffsetMin + startOffset, finalOffsetMin, easeOut);
                cardRT_Ref.offsetMax = Vector2.Lerp(finalOffsetMax + startOffset, finalOffsetMax, easeOut);
                yield return null;
            }
            cardRT_Ref.offsetMin = finalOffsetMin;
            cardRT_Ref.offsetMax = finalOffsetMax;
        }
        
        inputBlocked = false; // Desbloquegem controls
    }

    /// <summary>
    /// Corrutina utilitària que re-estructura el disseny vertical i horitzontal de les cel·les.
    /// Molt important per canviar el format de files grans de monstres en pestanya Recruits.
    /// </summary>
    private IEnumerator AdjustGrid()
    {
        yield return null;
        if (glg != null && glgRT != null)
        {
            float w     = glgRT.rect.width;
            float padH  = glg.padding.left + glg.padding.right;
            float cellW = (w - padH - glg.spacing.x * (NCOLS - 1)) / NCOLS;
            if (currentMode == InventoryMode.Items)
            {
                glg.cellSize = new Vector2(Mathf.Max(cellW, 60f), 60f);
            }
            else
            {
                float h = glgRT.rect.height;
                float padV = glg.padding.top + glg.padding.bottom;
                float cellH = (h - padV - glg.spacing.y) / 2f; // Obliguem exactament a dibuixar 2 files
                glg.cellSize = new Vector2(Mathf.Max(cellW, 60f), Mathf.Max(cellH, 60f));
            }
        }
    }

    /// <summary>
    /// Neteja i reconstrueix dinàmicament cadascuna de les cel·les interiors de la motxilla.
    /// </summary>
    private void BuildEntries(PlayerInventory inv)
    {
        entries.Clear();
        foreach (Transform c in gridParent) Destroy(c.gameObject);

        RefreshTabs(); // Canvis estètics de pestanyes

        // ── PESTANYA 1: ITEMS ──
        if (currentMode == InventoryMode.Items)
        {
            var counts = new Dictionary<string, int>();
            // Agrupem els objectes de l'inventari per la seva quantitat per a no tenir botons repetits
            foreach (var i in inv.Items) { counts.TryGetValue(i, out int n); counts[i] = n + 1; }

            if (counts.Count > 0)
            {
                foreach (var kvp in counts)
                {
                    ItemProfile p = inv.GetItemProfile(kvp.Key);
                    bool canUse   = p != null && (inCombat ? p.CanUseInCombat() : p.CanUseInOverworld());
                    CreateCell(kvp.Key, p, null, kvp.Value, canUse);
                }
            }
        }
        // ── PESTANYA 2: RECRUITS (CRIATURES PACTADES) ──
        else
        {
            HashSet<string> seenEnemies = new HashSet<string>();

            // Llegim en primer lloc la col·lecció física serialitzada a l'inspector
            if (inv.enemyDatabase != null)
            {
                foreach (var enemy in inv.enemyDatabase)
                {
                    if (enemy == null) continue;
                    seenEnemies.Add(enemy.enemyName);
                    int count = inv.GetRecruitedCount(enemy.enemyName);
                    bool encountered = inv.HasEncounteredEnemy(enemy.enemyName);
                    CreateCell(enemy.enemyName, null, enemy, count, false, encountered);
                }
            }

            // Fallbacks de seguretat per enemics addicionals registrats de forma volàtil a la memòria
            if (inv.RecruitedEnemies != null)
            {
                foreach (var kvp in inv.RecruitedEnemies)
                {
                    if (seenEnemies.Contains(kvp.Key)) continue;
                    seenEnemies.Add(kvp.Key);
                    CreateCell(kvp.Key, null, null, kvp.Value, false, true);
                }
            }
            if (inv.EncounteredEnemies != null)
            {
                foreach (var enc in inv.EncounteredEnemies)
                {
                    if (seenEnemies.Contains(enc)) continue;
                    seenEnemies.Add(enc);
                    int count = inv.GetRecruitedCount(enc);
                    CreateCell(enc, null, null, count, false, true);
                }
            }
        }

        // Correcció automàtica de selecció fora de marges
        if (entries.Count == 0 && selIdx != EXIT_IDX) selIdx = EXIT_IDX;
        else if (entries.Count > 0 && selIdx >= entries.Count) selIdx = entries.Count - 1;
        else if (entries.Count > 0 && selIdx == EXIT_IDX) selIdx = 0;
    }

    private void RefreshTabs()
    {
        bool isItems = (currentMode == InventoryMode.Items);
        itemsTabBg.color = isItems ? new Color(0.18f, 0.10f, 0.34f, 1f) : new Color(0.09f, 0.05f, 0.17f, 1f);
        itemsTabTxt.color = isItems ? new Color(1f, 0.92f, 0.2f) : new Color(0.4f, 0.4f, 0.4f);
        itemsTabTxt.text = isItems ? "ITEMS" : "           ITEMS"; 
        
        recruitsTabBg.color = !isItems ? new Color(0.34f, 0.10f, 0.18f, 1f) : new Color(0.17f, 0.05f, 0.09f, 1f);
        recruitsTabTxt.color = !isItems ? new Color(1f, 0.5f, 0.2f) : new Color(0.4f, 0.4f, 0.4f);
        recruitsTabTxt.text = !isItems ? "RECRUITED" : "           RECRUITED"; 

        if (tabKeyBtnRT != null)
        {
            tabKeyBtnRT.SetParent(isItems ? recruitsTabBg.transform : itemsTabBg.transform, false);
            tabKeyBtnRT.anchoredPosition = new Vector2(-120f, 0f); 
        }

        // Amaguem el detall de descripció en el mode Recruits, ja que cadascun disposa
        // de la seva pròpia descripció i barra dins de la mateixa cel·la
        if (detZoneGO != null) detZoneGO.SetActive(isItems);
        if (capZoneGO != null) capZoneGO.SetActive(isItems);
        if (statsZoneGO != null) statsZoneGO.SetActive(isItems);
        
        if (gridZoneRT != null)
        {
            gridZoneRT.offsetMax = new Vector2(-6f, -(isItems ? (topOffsetWithDetAndStats + 2f) : (topOffsetNoDetNoStats + 2f)));
        }
    }

    /// <summary>
    /// Generador procedimental d'una sola cel·la amb subdivisions, barres de vida, recompenses i textos de seguretat.
    /// </summary>
    private void CreateCell(string nameStr, ItemProfile itemP, EnemyProfile enemyP, int count, bool canUse, bool encountered = true)
    {
        var cell = new GameObject($"C_{nameStr}");
        cell.transform.SetParent(gridParent, false);
        cell.AddComponent<RectTransform>();
        var bg = cell.AddComponent<Image>();
        bg.color = currentMode == InventoryMode.Items ? new Color(0.15f, 0.12f, 0.25f, 1f) : new Color(0.15f, 0.12f, 0.15f, 1f);
        bg.raycastTarget = false;

        // ── DIBUIX DE CEL·LES D'ITEMS ──
        if (currentMode == InventoryMode.Items)
        {
            var tGo = new GameObject("T");
            tGo.transform.SetParent(cell.transform, false);
            var tRT = tGo.AddComponent<RectTransform>();
            tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
            tRT.offsetMin = new Vector2(6f, 3f); tRT.offsetMax = new Vector2(-6f, -3f);
            var txt = tGo.AddComponent<TextMeshProUGUI>();
            SetFont(txt, 34f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
            txt.text = $"{nameStr}  x{count}";
            txt.raycastTarget = false;

            entries.Add(new InventoryEntry { name = nameStr, profile = itemP, enemyProfile = enemyP, count = count, canUse = canUse, bg = bg, txt = txt });
        }
        // ── DIBUIX DE CEL·LES DE CRIATURES (AMISTAT SEGMENTADA) ──
        else
        {
            // Retrat de l'enemic
            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(cell.transform, false);
            var iconRT = iconGO.AddComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0f, 0.25f); iconRT.anchorMax = new Vector2(1f, 1f);
            iconRT.offsetMin = new Vector2(10f, 10f); iconRT.offsetMax = new Vector2(-10f, -10f);
            var img = iconGO.AddComponent<Image>();
            img.preserveAspect = true;
            if (enemyP != null && enemyP.enemyPortrait != null) img.sprite = enemyP.enemyPortrait;
            else img.color = new Color(1f, 1f, 1f, 0f);

            // Si mai no l'hem interaccionat, es dibuixa com a silueta negra mistèria (???)
            if (!encountered && img.sprite != null)
            {
                img.color = new Color(0f, 0f, 0f, 1f);
            }

            int limit = 1;
            if (enemyP != null)
                limit = Mathf.Max(1, enemyP.maxRecruitLimit);

            string dispName = encountered ? nameStr : "???";

            var tGo = new GameObject("T");
            tGo.transform.SetParent(cell.transform, false);
            var tRT = tGo.AddComponent<RectTransform>();
            tRT.anchorMin = new Vector2(0f, 0.22f); tRT.anchorMax = new Vector2(1f, 0.32f);
            tRT.offsetMin = new Vector2(6f, 0f); tRT.offsetMax = new Vector2(-6f, 0f);
            var txt = tGo.AddComponent<TextMeshProUGUI>();
            SetFont(txt, 26f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
            txt.text = $"{dispName}  <color=#FFFF00>{count} / {limit}</color>";
            txt.raycastTarget = false;

            // Fons fosc de la barra d'amistat
            var barBgGO = new GameObject("BarBG");
            barBgGO.transform.SetParent(cell.transform, false);
            var barBgRT = barBgGO.AddComponent<RectTransform>();
            barBgRT.anchorMin = new Vector2(0.25f, 0.12f); barBgRT.anchorMax = new Vector2(0.75f, 0.22f);
            barBgRT.offsetMin = new Vector2(0f, 0f); barBgRT.offsetMax = new Vector2(-40f, 0f); 
            var barBgImg = barBgGO.AddComponent<Image>();
            barBgImg.color = new Color(0.12f, 0.10f, 0.20f, 1f);
            
            var barBgOutline = barBgGO.AddComponent<Outline>();
            barBgOutline.effectColor = new Color(0f, 0f, 0f, 1f);
            barBgOutline.effectDistance = new Vector2(2f, -2f);

            // Barra groga de progrés
            var barFillGO = new GameObject("BarFill");
            barFillGO.transform.SetParent(barBgGO.transform, false);
            var barFillRT = barFillGO.AddComponent<RectTransform>();
            float fillPct = Mathf.Clamp01((float)count / limit);
            barFillRT.anchorMin = new Vector2(0f, 0f); barFillRT.anchorMax = new Vector2(fillPct, 1f);
            barFillRT.offsetMin = Vector2.zero; barFillRT.offsetMax = Vector2.zero;
            var barFillImg = barFillGO.AddComponent<Image>();
            barFillImg.color = new Color(1f, 0.85f, 0.15f, 1f);

            // ── DIBUIX DE LES LÍNIES DIVISÒRIES SEGONS ELS LIMITS ──
            for (int i = 1; i < limit; i++)
            {
                var sepGO = new GameObject($"Sep_{i}");
                sepGO.transform.SetParent(barBgGO.transform, false);
                var sepRT = sepGO.AddComponent<RectTransform>();
                float p = (float)i / limit;
                sepRT.anchorMin = new Vector2(p, 0f); sepRT.anchorMax = new Vector2(p, 1f);
                sepRT.offsetMin = new Vector2(-2f, 0f); sepRT.offsetMax = new Vector2(2f, 0f); 
                var sepImg = sepGO.AddComponent<Image>();
                sepImg.color = new Color(0.05f, 0.02f, 0.08f, 1f); 
            }

            // Sprite de la recompensa desbloquejable (a l'extrem dret de la barra)
            var rewardGO = new GameObject("Reward");
            rewardGO.transform.SetParent(cell.transform, false);
            var rewardRT = rewardGO.AddComponent<RectTransform>();
            rewardRT.anchorMin = new Vector2(0.75f, 0.17f); rewardRT.anchorMax = new Vector2(0.75f, 0.17f);
            rewardRT.pivot = new Vector2(0f, 0.5f);
            rewardRT.sizeDelta = new Vector2(70f, 70f); 
            rewardRT.anchoredPosition = new Vector2(-25f, 0f); 
            var rewardImg = rewardGO.AddComponent<Image>();
            rewardImg.preserveAspect = true;
            if (enemyP != null && enemyP.recruitmentRewardSprite != null)
            {
                rewardImg.sprite = enemyP.recruitmentRewardSprite;
                rewardImg.color = Color.white;
            }
            else
            {
                rewardImg.color = new Color(1f, 1f, 1f, 0f);
            }

            // Text del benefici o habilitat passiva
            var descGO = new GameObject("Desc");
            descGO.transform.SetParent(cell.transform, false);
            var descRT = descGO.AddComponent<RectTransform>();
            descRT.anchorMin = new Vector2(0.05f, 0.0f); descRT.anchorMax = new Vector2(0.95f, 0.08f);
            descRT.offsetMin = new Vector2(0f, 0f); descRT.offsetMax = new Vector2(0f, 0f);
            var descTxt = descGO.AddComponent<TextMeshProUGUI>();
            SetFont(descTxt, 24f, new Color(0.75f, 0.75f, 0.75f, 1f), FontStyles.Normal, TextAlignmentOptions.Top);
            
            if (encountered && enemyP != null && !string.IsNullOrEmpty(enemyP.recruitmentRewardDescription))
            {
                descTxt.text = enemyP.recruitmentRewardDescription;
            }
            else
            {
                descTxt.text = "???";
            }
            descTxt.raycastTarget = false;

            entries.Add(new InventoryEntry { name = dispName, profile = itemP, enemyProfile = enemyP, count = count, canUse = false, bg = bg, txt = txt });
        }
    }

    private void RefreshHighlights()
    {
        bool isItems = (currentMode == InventoryMode.Items);
        bool exitSel  = (selIdx == EXIT_IDX);

        if (isItems)
        {
            exitBg.color  = exitSel ? new Color(0.40f, 0.18f, 0.60f) : new Color(0.22f, 0.12f, 0.38f);
            exitTxt.color = exitSel ? new Color(1f, 0.92f, 0.2f) : new Color(0.6f, 0.55f, 0.75f);
        }
        else
        {
            exitBg.color  = exitSel ? new Color(0.60f, 0.18f, 0.25f) : new Color(0.38f, 0.12f, 0.18f);
            exitTxt.color = exitSel ? new Color(1f, 0.5f, 0.2f) : new Color(0.75f, 0.55f, 0.6f);
        }

        for (int i = 0; i < entries.Count; i++)
        {
            bool s = (i == selIdx);
            if (entries[i].bg  != null) 
            {
                if (isItems) entries[i].bg.color  = s ? new Color(0.28f, 0.22f, 0.50f) : new Color(0.15f, 0.12f, 0.25f);
                else         entries[i].bg.color  = s ? new Color(0.50f, 0.22f, 0.28f) : new Color(0.25f, 0.12f, 0.15f);
            }
            if (entries[i].txt != null) 
            {
                if (isItems) entries[i].txt.color = s ? new Color(1f, 0.92f, 0.2f) : Color.white;
                else         entries[i].txt.color = s ? new Color(1f, 0.5f, 0.2f) : Color.white;
            }
        }
    }

    private void RefreshDetail()
    {
        if (selIdx == EXIT_IDX || selIdx < 0 || selIdx >= entries.Count)
        {
            detNameTxt.text  = ""; detDescTxt.text = ""; detIconImg.color = new Color(1f, 1f, 1f, 0f);
            return;
        }
        var e = entries[selIdx];
        detNameTxt.text = $"{e.name}  x{e.count}";
        
        if (currentMode == InventoryMode.Items)
        {
            detDescTxt.text = e.profile != null ? e.profile.itemDescription : "(sense llibre de dades de l'objecte)";
            if (e.profile?.itemIcon != null) { detIconImg.sprite = e.profile.itemIcon; detIconImg.color = Color.white; }
            else detIconImg.color = new Color(1f, 1f, 1f, 0f);
        }
        else
        {
            detDescTxt.text = "Enemic reclutat pacíficament.\nAmistat consolidada.";
            if (e.enemyProfile != null)
            {
                detDescTxt.text += $"\n({e.count} / {e.enemyProfile.maxRecruitLimit} reclutats)";
                if (e.enemyProfile.enemyPortrait != null) { detIconImg.sprite = e.enemyProfile.enemyPortrait; detIconImg.color = Color.white; }
                else detIconImg.color = new Color(1f, 1f, 1f, 0f);
            }
            else detIconImg.color = new Color(1f, 1f, 1f, 0f);
        }
    }

    // ── NAVEGACIÓ I CONTROL DE TECLAT ─────────────────────────────────────
    private void Update()
    {
        // Petites micro-animacions d'efecte pressió 3D en les tecles d'escapament
        if (escTopRT != null)
        {
            float cycle = Time.unscaledTime * 1.5f;
            bool isPressed = (cycle % 1f) > 0.7f;
            if (isPressed != lastEscPressed)
            {
                lastEscPressed = isPressed;
                escTopRT.anchoredPosition = isPressed ? Vector2.zero : new Vector2(0f, 4f);
            }
        }

        if (tabKeyBtnRT != null)
        {
            float cycle = Time.unscaledTime * 1.5f;
            bool isPressed = (cycle % 1f) > 0.7f;
            if (isPressed != lastTabPressed)
            {
                lastTabPressed = isPressed;
                if (tabTopRT_Ref == null) tabTopRT_Ref = tabKeyBtnRT.Find("Top") as RectTransform;
                if (tabTopRT_Ref != null) tabTopRT_Ref.anchoredPosition = isPressed ? Vector2.zero : new Vector2(0f, 4f);
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

        // Commutació de pestanyes per tecla
        if (tab)
        {
            currentMode = currentMode == InventoryMode.Items ? InventoryMode.Recruits : InventoryMode.Items;
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
            if (PlayerInventory.Instance != null) ItemSoundPlayer.Play(PlayerInventory.Instance.selectSound);
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
                else selIdx = EXIT_IDX; // Saltem al botó de sortir si baixem de l'última fila
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

        // Activació de l'ús de l'objecte
        if (use && selIdx != EXIT_IDX && selIdx < entries.Count)
        {
            if (!entries[selIdx].canUse)
            {
                if (errorAnim != null) StopCoroutine(errorAnim);
                if (currentMode == InventoryMode.Items)
                {
                    if (entries[selIdx].profile != null && entries[selIdx].profile.effectType == ItemEffectType.KeyItem)
                        errorAnim = StartCoroutine(ShowErrorAnim("AQUEST OBJECTE ÉS CLAU, S'UTILITZA AUTOMÀTICAMENT."));
                    else
                        errorAnim = StartCoroutine(ShowErrorAnim("AQUEST OBJECTE NOMÉS ES POT USAR EN COMBAT."));
                }
                else
                {
                    errorAnim = StartCoroutine(ShowErrorAnim("ELS ENEMICS RECLUTATS NO ES PODEN CONSUMIR."));
                }
            }
            else
            {
                if (PlayerInventory.Instance != null) ItemSoundPlayer.Play(PlayerInventory.Instance.selectSound);
                UseItem(entries[selIdx]);
            }
        }
    }

    private Coroutine errorAnim;
    
    /// <summary>
    /// Corrutina de sacsejada d'errors (Horizontal Shake).
    /// Mou horitzontalment el text de descripció vermell amb una velocitat sinusoidal molt ràpida.
    /// </summary>
    private IEnumerator ShowErrorAnim(string msg = "AQUEST OBJECTE NOMÉS ES POT USAR EN COMBAT.")
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
                 // Sacsejada forta
                 detDescTxt.rectTransform.anchoredPosition = startPos + new Vector3(Mathf.Sin(elapsed * 50f) * 8f, 0f, 0f);
             }
             else 
             {
                 // Fosa i aturada gradual de tornada a l'estat gris estàndard
                 detDescTxt.rectTransform.anchoredPosition = startPos;
                 detDescTxt.color = Color.Lerp(new Color(1f, 0.3f, 0.3f), new Color(0.62f, 0.62f, 0.62f), (elapsed - 0.3f) / 1.2f);
             }
             yield return null;
        }
        
        detDescTxt.rectTransform.anchoredPosition = startPos;
        RefreshDetail();
    }

    // ── USAR OBJECTES CURATIUS ───────────────────────────────────────────
    private void UseItem(InventoryEntry entry)
    {
        var inv = PlayerInventory.Instance;

        // Comprovació lògica preventival: vida ja completada
        if (entry.profile != null && entry.profile.effectType == ItemEffectType.HealPlayer)
        {
            if (inv.CurrentHP >= inv.MaxHP)
            {
                if (errorAnim != null) StopCoroutine(errorAnim);
                errorAnim = StartCoroutine(ShowErrorAnim("JA TENS LA VIDA AL MÀXIM."));
                return;
            }
        }

        if (!inv.RemoveItem(entry.name)) return;

        // Sons d'aplicació
        if (entry.profile != null)
        {
            ItemSoundPlayer.Play(entry.profile.useSound);
            if (entry.profile.additionalUseSounds != null)
            {
                foreach (var clip in entry.profile.additionalUseSounds)
                {
                    ItemSoundPlayer.Play(clip);
                }
            }
        }

        onItemSelected?.Invoke(entry.profile);

        // Si som en combat, tanquem immediatament el menú per a no congelar l'acció
        if (inCombat) { IsOpen = false; Destroy(gameObject); return; }

        if (entry.profile != null && entry.profile.effectType == ItemEffectType.HealPlayer)
            StartCoroutine(ShowHealAnim($"+{entry.profile.effectValue} HP"));

        hpTxt.text   = $"♥  {inv.CurrentHP} / {inv.MaxHP} HP";
        goldTxt.text  = $"{inv.Gold} G       ";
        capTxt.text   = $"Capacity: {inv.Items.Count} / {inv.maxItemsCapacity}";
        
        BuildEntries(inv);
        selIdx = Mathf.Clamp(selIdx, -1, entries.Count - 1);
        StartCoroutine(AdjustGrid());
        RefreshDetail(); 
        RefreshHighlights();
    }

    /// <summary>
    /// Corrutina d'animació d'indicació de curació (+HP).
    /// Instancia procedimentalment un text verd que s'eleva verticalment amb suavitzat quadràtic.
    /// </summary>
    private IEnumerator ShowHealAnim(string text)
    {
        var go = new GameObject("HA"); go.transform.SetParent(hpRT.parent, false);
        var rt = go.AddComponent<RectTransform>();
        
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(20f, 40f);
        rt.sizeDelta = new Vector2(1000f, 200f); 
        
        rt.localScale = Vector3.one * 0.5f;
        var t = go.AddComponent<TextMeshProUGUI>();
        
        t.enableAutoSizing = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        
        SetFont(t, 48f, new Color(0.3f, 1f, 0.4f), FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        t.text = text; 
        t.raycastTarget = false;
        
        float el = 0f, dur = 1.4f;
        Vector2 s = rt.anchoredPosition, e = s + new Vector2(0f, 70f);
        while (el < dur)
        {
            el += Time.unscaledDeltaTime; 
            float p = el / dur;
            
            // Lliscament cubic
            rt.anchoredPosition = Vector2.Lerp(s, e, Mathf.Sqrt(p));
            // Creixement progressiu
            rt.localScale = Vector3.Lerp(Vector3.one * 0.5f, Vector3.one, Mathf.Clamp01(p * 5f));
            
            // Fosa final asíncrona cap al buit
            var c = t.color; c.a = p < 0.65f ? 1f : Mathf.Lerp(1f, 0f, (p - 0.65f) / 0.35f);
            t.color = c; 
            yield return null;
        }
        Destroy(go);
    }

    private void CloseMenu() 
    { 
        if (inputBlocked) return;
        inputBlocked = true; 
        StartCoroutine(OutroRoutine()); 
    }

    private IEnumerator OutroRoutine()
    {
        Vector2 finalOffsetMin = cardRT_Ref.offsetMin;
        Vector2 finalOffsetMax = cardRT_Ref.offsetMax;
        Vector2 targetOffset = new Vector2(0f, -1500f);

        float elapsed = 0f;
        float dur = 0.3f;
        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / dur);
            float easeIn = t * t * t; // Ease in cubic per a un tancament molt ràpid
            
            cardRT_Ref.offsetMin = Vector2.Lerp(finalOffsetMin, finalOffsetMin + targetOffset, easeIn);
            cardRT_Ref.offsetMax = Vector2.Lerp(finalOffsetMax, finalOffsetMax + targetOffset, easeIn);
            yield return null;
        }

        IsOpen = false; 
        onClose?.Invoke(); 
        Destroy(gameObject);
    }

    private void OnDestroy()  { IsOpen = false; }

    // =========================================================================
    // UTILS RT (Generadors Dinàmics de RectTransform per Coordenades)
    // =========================================================================
    private RectTransform MakeRT(string n, Transform parent)
    { 
        var go = new GameObject(n); 
        go.transform.SetParent(parent, false); 
        return go.AddComponent<RectTransform>(); 
    }
    
    private RectTransform MakeRT(string n, RectTransform parent) => MakeRT(n, parent.transform);

    /// <summary>
    /// Ancoratge automàtic a la vora superior de la targeta.
    /// </summary>
    private RectTransform TopZone(RectTransform parent, string n, ref float used, float h, float marginTop = 0f)
    {
        var rt = MakeRT(n, parent);
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = new Vector2(0f, -(used + h));
        rt.offsetMax = new Vector2(0f, -(used + marginTop));
        used += h + marginTop;
        return rt;
    }

    /// <summary>
    /// Ancoratge automàtic a la vora inferior de la targeta.
    /// </summary>
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

    // ── LOCALITZACIÓ DE FONTS MULTI-PLATAFORMA ──
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
