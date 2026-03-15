using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventoryMenuUI : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    private bool inCombat;
    private Action<ItemProfile> onItemSelected;
    private Action onClose;

    private readonly List<InventoryEntry> entries = new List<InventoryEntry>();
    private int selIdx = -1;  // -1 = EXIT (defecte)
    private const int EXIT_IDX = -1;
    private const int NCOLS    = 2;

    private TextMeshProUGUI hpTxt, goldTxt, capTxt;
    private TextMeshProUGUI detNameTxt, detDescTxt;
    private Image           detIconImg;
    private RectTransform   hpRT;
    private Transform       gridParent;
    private Image           exitBg;
    private TextMeshProUGUI exitTxt;
    private GridLayoutGroup glg;
    private RectTransform   glgRT;

    private class InventoryEntry
    {
        public string name; public ItemProfile profile; public int count; public bool canUse;
        public Image bg; public TextMeshProUGUI txt;
    }

    // ── Factory ──────────────────────────────────────────────────────
    public static void Show(bool isCombat, Action<ItemProfile> onItemSelected, Action onClose = null)
    {
        var canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;
        var go = new GameObject("InventoryMenuUI");
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(canvas.transform, false);
        rt.anchorMin = rt.offsetMin = Vector2.zero;
        rt.anchorMax = Vector2.one; rt.offsetMax = Vector2.zero;
        var ui = go.AddComponent<InventoryMenuUI>();
        ui.inCombat = isCombat; ui.onItemSelected = onItemSelected; ui.onClose = onClose;
        ui.Build(); IsOpen = true;
    }

    // ── Construcció (posicionat per àncores, sense VLG) ───────────────
    private void Build()
    {
        var inv = PlayerInventory.Instance;

        // Overlay intercepta clics
        var bgImg = gameObject.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.78f);
        bgImg.raycastTarget = true;

        // ─── Targeta ─────────────────────────────────────────────────
        var card = MakeRT("Card", transform);
        cardRT_Ref = card;
        
        card.anchorMin = new Vector2(0.12f, 0.08f);
        card.anchorMax = new Vector2(0.88f, 0.92f);
        card.offsetMin = card.offsetMax = Vector2.zero;
        card.gameObject.AddComponent<Image>().color = new Color(0.07f, 0.06f, 0.15f, 1f);
        var ol = card.gameObject.AddComponent<Outline>();
        ol.effectColor = new Color(0.95f, 0.80f, 0.15f, 1f);
        ol.effectDistance = new Vector2(8f, -8f);

        // Alçades de cada zona en píxels
        const float H_TITLE  = 68f;
        const float H_STATS  = 68f;
        const float H_DETAIL = 148f;
        const float H_CAP    = 32f;
        const float H_EXIT   = 56f;
        const float H_HINT   = 40f;

        float fromTop = 0f;   // píxels ja usats des del top

        // ─── Títol ───────────────────────────────────────────────────
        var titleRT = TopZone(card, "Title", ref fromTop, H_TITLE);
        titleRT.gameObject.AddComponent<Image>().color = new Color(0.18f, 0.10f, 0.34f, 1f);
        TxtFill(titleRT, "*  INVENTARI  *", 58f, new Color(1f, 0.92f, 0.2f), FontStyles.Bold, TextAlignmentOptions.Center);

        // ─── Stats HP / Or ───────────────────────────────────────────
        var statsRT = TopZone(card, "Stats", ref fromTop, H_STATS);
        statsRT.gameObject.AddComponent<Image>().color = new Color(0.04f, 0.03f, 0.11f, 1f);

        hpRT  = StretchChild(statsRT, "HP", 0f, 0f, 0.5f, 1f, 22f, 0f, 0f, 0f);
        hpTxt = hpRT.gameObject.AddComponent<TextMeshProUGUI>();
        SetFont(hpTxt, 44f, new Color(0.35f, 1f, 0.50f), FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        hpTxt.text = $"♥  {inv.CurrentHP} / {inv.MaxHP} HP";

        var goldRT = StretchChild(statsRT, "Gold", 0.5f, 0f, 1f, 1f, 0f, 0f, -22f, 0f);
        goldTxt    = goldRT.gameObject.AddComponent<TextMeshProUGUI>();
        SetFont(goldTxt, 44f, new Color(1f, 0.90f, 0.15f), FontStyles.Bold, TextAlignmentOptions.MidlineRight);
        goldTxt.text = $"{inv.Gold} G       ";

        var coinRT = MakeRT("CoinIcon", goldRT);
        // Ancorada completament a la dreta del panell al mig (Midline Right)
        coinRT.anchorMin = new Vector2(1f, 0.5f); coinRT.anchorMax = new Vector2(1f, 0.5f);
        coinRT.sizeDelta = new Vector2(44f, 44f);
        coinRT.anchoredPosition = new Vector2(-12f, 0f);
        var coinImg = coinRT.gameObject.AddComponent<Image>();
        coinImg.preserveAspect = true;
        Sprite coinSp = LoadSprite("Art/Sprites/pixel_coin");
        if (coinSp != null) coinImg.sprite = coinSp;

        // ─── Panell detall ────────────────────────────────────────────
        var detRT = TopZone(card, "Detail", ref fromTop, H_DETAIL, 2f);
        detRT.gameObject.AddComponent<Image>().color = new Color(0.10f, 0.08f, 0.20f, 1f);

        // Marc icona (quadrat esquerra amb contorn)
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

        // Text nom (meitat superior dreta del panell)
        var dNameRT = StretchChild(detRT, "DName", 0f, 0.5f, 1f, 1f, 152f, 4f, -14f, -4f);
        detNameTxt  = dNameRT.gameObject.AddComponent<TextMeshProUGUI>();
        SetFont(detNameTxt, 44f, Color.white, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        detNameTxt.text = "";

        // Text desc (meitat inferior dreta del panell)
        var dDescRT = StretchChild(detRT, "DDesc", 0f, 0f, 1f, 0.5f, 152f, 4f, -14f, -4f);
        detDescTxt  = dDescRT.gameObject.AddComponent<TextMeshProUGUI>();
        SetFont(detDescTxt, 28f, new Color(0.62f, 0.62f, 0.62f), FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        detDescTxt.text = "";

        // ─── Capacitat ───────────────────────────────────────────────
        var capRT = TopZone(card, "Cap", ref fromTop, H_CAP);
        capTxt    = capRT.gameObject.AddComponent<TextMeshProUGUI>();
        SetFont(capTxt, 26f, new Color(0.5f, 0.5f, 0.5f), FontStyles.Normal, TextAlignmentOptions.Center);
        capTxt.text = $"Espai: {inv.Items.Count} / {inv.maxItemsCapacity}";

        // ─── Zones fixes des del BOTTOM ──────────────────────────────
        float fromBottom = 0f;

        // Hint
        var hintRT = BotZone(card, "Hint", ref fromBottom, H_HINT);
        TxtFill(hintRT, "FLETXES / WASD  navegar    |    E / INTRO  usar    |    ESC  tancar",
                24f, new Color(0.40f, 0.40f, 0.40f), FontStyles.Normal, TextAlignmentOptions.Center);

        // EXIT
        var exitRT = BotZone(card, "Exit", ref fromBottom, H_EXIT, 3f);
        exitBg     = exitRT.gameObject.AddComponent<Image>();
        exitBg.color = new Color(0.22f, 0.12f, 0.38f, 1f);
        exitTxt   = TxtFill(exitRT, "[ SORTIR ]", 38f, new Color(1f, 0.92f, 0.2f), FontStyles.Bold, TextAlignmentOptions.Center);

        // ─── Graella (zona que queda entre capZone i exitZone) ────────
        var gridRT_zone = MakeRT("GridZone", card);
        gridRT_zone.anchorMin = new Vector2(0f, 0f);
        gridRT_zone.anchorMax = new Vector2(1f, 1f);
        gridRT_zone.offsetMin = new Vector2(6f, fromBottom + 3f);    // from bottom
        gridRT_zone.offsetMax = new Vector2(-6f, -(fromTop + 2f));   // from top

        var grid = MakeRT("Grid", gridRT_zone);
        grid.anchorMin = Vector2.zero; grid.anchorMax = Vector2.one;
        grid.offsetMin = Vector2.zero; grid.offsetMax = Vector2.zero;

        glg = grid.gameObject.AddComponent<GridLayoutGroup>();
        glg.cellSize        = new Vector2(200f, 60f);   // amplada real calculada al 1r frame
        glg.spacing         = new Vector2(6f, 6f);
        glg.startCorner     = GridLayoutGroup.Corner.UpperLeft;
        glg.startAxis       = GridLayoutGroup.Axis.Horizontal;
        glg.childAlignment  = TextAnchor.UpperCenter;
        glg.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = NCOLS;
        glg.padding         = new RectOffset(4, 4, 4, 4);

        glgRT     = grid;
        gridParent = grid.transform;

        // ─── Pobla i inicialitza ─────────────────────────────────────
        BuildEntries(inv);
        selIdx = entries.Count > 0 ? 0 : EXIT_IDX; // Selecciona el 1r objecte si n'hi ha
        RefreshDetail();
        RefreshHighlights();
    }
    
    // Utilitat per carregar imatges fora d'asset bundles ràpid
    private Sprite LoadSprite(string path)
    {
#if UNITY_EDITOR
        var sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/{path}.png");
        if (sp != null) return sp;
#endif
        return Resources.Load<Sprite>(path);
    }

    // ── Ajusta cell width al 1r frame (quan rect és real) i Animació Intro
    private bool inputBlocked = true;
    private RectTransform cardRT_Ref; // Ref a la targeta per animar
    
    private void Start() => StartCoroutine(IntroRoutine());
    
    private IEnumerator IntroRoutine()
    {
        Vector2 finalOffsetMin = Vector2.zero;
        Vector2 finalOffsetMax = Vector2.zero;
        Vector2 startOffset = new Vector2(0f, -1500f); // Empença des de baix l'alçada de pantalla

        // 1. Configuració inicial animació
        if (cardRT_Ref != null)
        {
            finalOffsetMin = cardRT_Ref.offsetMin;
            finalOffsetMax = cardRT_Ref.offsetMax;
            cardRT_Ref.offsetMin = finalOffsetMin + startOffset;
            cardRT_Ref.offsetMax = finalOffsetMax + startOffset;
        }

        yield return null; // Espera 1 frame per tenir l'amplada real de la graella

        // 2. Ajust cell width
        if (glg != null && glgRT != null)
        {
            float w     = glgRT.rect.width;
            float padH  = glg.padding.left + glg.padding.right;
            float cellW = (w - padH - glg.spacing.x * (NCOLS - 1)) / NCOLS;
            glg.cellSize = new Vector2(Mathf.Max(cellW, 60f), glg.cellSize.y);
        }
        
        // 3. Animació d'entrada (deslligada de Time.timeScale per funcionar pausat)
        if (cardRT_Ref != null)
        {
            float elapsed = 0f;
            float dur = 0.35f; // Durada entrada slide
            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                // Ease out cubic
                float easeOut = 1f - Mathf.Pow(1f - t, 3f);
                
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

    // ── Poblar graella ─────────────────────────────────────────────────
    private void BuildEntries(PlayerInventory inv)
    {
        entries.Clear();
        foreach (Transform c in gridParent) Destroy(c.gameObject);

        var counts = new Dictionary<string, int>();
        foreach (var i in inv.Items) { counts.TryGetValue(i, out int n); counts[i] = n + 1; }

        if (counts.Count == 0) return;

        foreach (var kvp in counts)
        {
            ItemProfile p = inv.GetItemProfile(kvp.Key);
            bool canUse   = p != null && (inCombat || p.CanUseInOverworld());

            // Crea la cel·la — GridLayoutGroup la dimensionarà automàticament
            var cell = new GameObject($"C_{kvp.Key}");
            cell.transform.SetParent(gridParent, false);
            cell.AddComponent<RectTransform>();          // GridLayoutGroup necessita RT
            var bg = cell.AddComponent<Image>();
            bg.color         = new Color(0.15f, 0.12f, 0.25f, 1f);
            bg.raycastTarget = false;

            var tGo = new GameObject("T");
            tGo.transform.SetParent(cell.transform, false);
            var tRT = tGo.AddComponent<RectTransform>();
            tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
            tRT.offsetMin = new Vector2(6f, 3f); tRT.offsetMax = new Vector2(-6f, -3f);
            var txt = tGo.AddComponent<TextMeshProUGUI>();
            SetFont(txt, 34f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
            txt.text         = $"{kvp.Key}  x{kvp.Value}";
            txt.raycastTarget = false;

            entries.Add(new InventoryEntry { name = kvp.Key, profile = p, count = kvp.Value, canUse = canUse, bg = bg, txt = txt });
        }
    }

    // ── Selecció visual ────────────────────────────────────────────────
    private void RefreshHighlights()
    {
        bool exitSel  = (selIdx == EXIT_IDX);
        exitBg.color  = exitSel ? new Color(0.40f, 0.18f, 0.60f) : new Color(0.22f, 0.12f, 0.38f);
        exitTxt.color = exitSel ? new Color(1f, 0.92f, 0.2f) : new Color(0.6f, 0.55f, 0.75f);

        for (int i = 0; i < entries.Count; i++)
        {
            bool s = (i == selIdx);
            if (entries[i].bg  != null) entries[i].bg.color  = s ? new Color(0.28f, 0.22f, 0.50f) : new Color(0.15f, 0.12f, 0.25f);
            if (entries[i].txt != null) entries[i].txt.color = s ? new Color(1f, 0.92f, 0.2f) : Color.white;
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
        detDescTxt.text = e.profile != null
            ? e.profile.itemDescription
            : "(sense ItemProfile)";
        if (e.profile?.itemIcon != null) { detIconImg.sprite = e.profile.itemIcon; detIconImg.color = Color.white; }
        else detIconImg.color = new Color(1f, 1f, 1f, 0f);
    }

    // ── Navegació ──────────────────────────────────────────────────────
    private void Update()
    {
        if (inputBlocked) return;
        bool left  = Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow);
        bool right = Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow);
        bool up    = Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow);
        bool down  = Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow);
        bool use   = Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return);
        bool close = Input.GetKeyDown(KeyCode.Escape);

        if (close || (use && selIdx == EXIT_IDX)) 
        { 
            if (use && PlayerInventory.Instance != null) ItemSoundPlayer.Play(PlayerInventory.Instance.selectSound);
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

            // Seguretat: no superar el darrer element
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
            if (!entries[selIdx].canUse)
            {
                if (errorAnim != null) StopCoroutine(errorAnim);
                errorAnim = StartCoroutine(ShowErrorAnim());
            }
            else
            {
                if (PlayerInventory.Instance != null) ItemSoundPlayer.Play(PlayerInventory.Instance.selectSound);
                UseItem(entries[selIdx]);
            }
        }
    }

    private Coroutine errorAnim;
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

    // ── Usar ───────────────────────────────────────────────────────────
    private void UseItem(InventoryEntry entry)
    {
        var inv = PlayerInventory.Instance;

        // Comprovació: si és un objecte curatiu i tenim la vida plena, no deixis usar-lo
        if (entry.profile != null && entry.profile.effectType == ItemEffectType.HealPlayer)
        {
            if (inv.CurrentHP >= inv.MaxHP)
            {
                if (errorAnim != null) StopCoroutine(errorAnim);
                errorAnim = StartCoroutine(ShowErrorAnim("JA TENS LA VIDA PLENA."));
                return;
            }
        }

        if (!inv.RemoveItem(entry.name)) return;

        // Reproduir el so d'ús de l'objecte
        if (entry.profile != null)
            ItemSoundPlayer.Play(entry.profile.useSound);

        onItemSelected?.Invoke(entry.profile);

        if (inCombat) { CloseMenu(); return; }

        if (entry.profile != null && entry.profile.effectType == ItemEffectType.HealPlayer)
            StartCoroutine(ShowHealAnim($"+{entry.profile.effectValue} HP"));

        hpTxt.text   = $"♥  {inv.CurrentHP} / {inv.MaxHP} HP";
        goldTxt.text  = $"{inv.Gold} G       ";
        capTxt.text   = $"Espai: {inv.Items.Count} / {inv.maxItemsCapacity}";
        BuildEntries(inv);
        selIdx = Mathf.Clamp(selIdx, -1, entries.Count - 1);
        StartCoroutine(AdjustGrid());
        RefreshDetail(); RefreshHighlights();
    }

    private IEnumerator ShowHealAnim(string text)
    {
        var go = new GameObject("HA"); go.transform.SetParent(hpRT.parent, false);
        var rt = go.AddComponent<RectTransform>();
        
        // Espai lliure massiu sense ancoratges relatius per evitar talls quan l'escala canvia
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(20f, 40f);
        rt.sizeDelta = new Vector2(1000f, 200f); // Super gran
        
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
            el += Time.unscaledDeltaTime; float p = el / dur;
            rt.anchoredPosition = Vector2.Lerp(s, e, Mathf.Sqrt(p));
            rt.localScale = Vector3.Lerp(Vector3.one * 0.5f, Vector3.one, Mathf.Clamp01(p * 5f));
            var c = t.color; c.a = p < 0.65f ? 1f : Mathf.Lerp(1f, 0f, (p - 0.65f) / 0.35f);
            t.color = c; yield return null;
        }
        Destroy(go);
    }

    private void CloseMenu() { IsOpen = false; onClose?.Invoke(); Destroy(gameObject); }
    private void OnDestroy()  { IsOpen = false; }

    // ── Helpers RT ───────────────────────────────────────────────────
    private RectTransform MakeRT(string n, Transform parent)
    { var go = new GameObject(n); go.transform.SetParent(parent, false); return go.AddComponent<RectTransform>(); }
    private RectTransform MakeRT(string n, RectTransform parent) => MakeRT(n, parent.transform);

    /// Zona ancorada al TOP, ocupa [from top usedTop] -> [usedTop + height]
    private RectTransform TopZone(RectTransform parent, string n, ref float used, float h, float marginTop = 0f)
    {
        var rt = MakeRT(n, parent);
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = new Vector2(0f, -(used + h));
        rt.offsetMax = new Vector2(0f, -(used + marginTop));
        used += h + marginTop;
        return rt;
    }

    /// Zona ancorada al BOTTOM
    private RectTransform BotZone(RectTransform parent, string n, ref float used, float h, float marginBot = 0f)
    {
        var rt = MakeRT(n, parent);
        rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f);
        rt.offsetMin = new Vector2(0f, used + marginBot);
        rt.offsetMax = new Vector2(0f, used + h + marginBot);
        used += h + marginBot;
        return rt;
    }

    /// Fill: ocupa tot el parent
    private RectTransform StretchChild(RectTransform parent, string n,
        float ax, float ay, float bx, float by, float oL = 0f, float oB = 0f, float oR = 0f, float oT = 0f)
    {
        var rt = MakeRT(n, parent);
        rt.anchorMin = new Vector2(ax, ay); rt.anchorMax = new Vector2(bx, by);
        rt.offsetMin = new Vector2(oL, oB); rt.offsetMax = new Vector2(oR, oT);
        return rt;
    }

    /// Punt ancle + sizeDelta
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

    // ── Font ─────────────────────────────────────────────────────────
    private void SetFont(TextMeshProUGUI t, float size, Color col, FontStyles style, TextAlignmentOptions align)
    {
        t.fontSize = size; t.color = col; t.fontStyle = style; t.alignment = align;
        var f = LoadFont("8bitoperator_jve SDF"); if (f == null) f = LoadFont("PixelOperator SDF");
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
        var r = Resources.Load<TMP_FontAsset>($"Fonts & Materials/{n}"); return r ?? Resources.Load<TMP_FontAsset>(n);
    }
}
