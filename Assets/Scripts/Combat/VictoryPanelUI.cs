using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Gestiona la interfície gràfica i el comportament del panell de victòria en acabar un combat.
/// Aquest panell es genera de manera 100% procedural (UI per codi) en finalitzar la batalla,
/// mostrant les recompenses d'or obtingudes mitjançant un comptador visual animat (count-up),
/// la llista dels objectes d'inventari recol·lectats disposats en format de graella a dues columnes
/// amb les seves respectives quantitats, i un text inferior parpellejant amb efecte de contorn (outline)
/// que convida el jugador a prémer una tecla d'interacció ('E', 'Intro' o 'Espai') per tornar a l'overworld.
/// </summary>
public class VictoryPanelUI : MonoBehaviour
{
    // ─── RUTES DE RECURSOS D'IMATGES (SPRITES RETRO) ──────────────────
    private const string COIN_PATH = "Art/Sprites/pixel_coin"; // Ruta per a l'sprite de moneda pixelada
    private const string BAG_PATH  = "Art/Sprites/pixel_bag";  // Ruta per a l'sprite de bossa pixelada

    // ─── PATRÓ FACTORY PER A LA CREACIÓ DINÀMICA ─────────────────────
    /// <summary>
    /// Crea instància única del panell de victòria a sobre del Canvas general.
    /// Això estalvia haver de mantenir prefabs de UI complexos enllaçats en escenes separades.
    /// </summary>
    /// <param name="canvasParent">El component Transform on es penjarà la interfície (pare del Canvas).</param>
    /// <param name="goldEarned">Quantitat d'or guanyat en la batalla.</param>
    /// <param name="items">Llista d'objectes de recompensa guanyats.</param>
    /// <param name="totalGold">L'or total que té l'inventari del jugador un cop sumat el guanyat.</param>
    /// <param name="onDone">Callback a cridar quan l'usuari prem per acceptar i tancar.</param>
    public static VictoryPanelUI Create(
        Transform canvasParent,
        int goldEarned,
        System.Collections.Generic.List<string> items,
        int totalGold,
        Action onDone)
    {
        // Instanciem un objecte buit que contindrà la lògica de la victòria
        var go = new GameObject("VictoryPanel");
        go.transform.SetParent(canvasParent, false);
        go.transform.SetAsLastSibling(); // Ens col·loquem per sobre de qualsevol altra pantalla o element

        var panel = go.AddComponent<VictoryPanelUI>();
        panel.goldEarned = goldEarned;
        panel.items      = items ?? new System.Collections.Generic.List<string>();
        panel.totalGold  = totalGold;
        panel.onDone     = onDone;
        return panel;
    }

    // ── SOBRECÀRREGA DE COMPATIBILITAT (PER A UN SOL OBJECTE) ─────────
    /// <summary>
    /// Sobrecàrrega del factory de creació adaptat per si només es rep un únic nom d'objecte en format text.
    /// S'encarrega d'encapsular-lo en una llista per poder utilitzar el mateix mètode de graella unificat.
    /// </summary>
    public static VictoryPanelUI Create(Transform canvasParent, int goldEarned, string itemName, int totalGold, Action onDone)
    {
        var list = new System.Collections.Generic.List<string>();
        if (!string.IsNullOrEmpty(itemName) && itemName != "none" && itemName != "—")
            list.Add(itemName);
        return Create(canvasParent, goldEarned, list, totalGold, onDone);
    }

    // ── VARIABLES DE CONTEXT I DADES ─────────────────────────────────
    private int    goldEarned;                        // Or guanyat en aquesta ronda de combat
    private System.Collections.Generic.List<string> items; // Llista completa de cadenes identificatives dels premis
    private int    totalGold;                         // L'or actual a l'inventari sumat ja el dany
    private Action onDone;                            // Callback de retorn en acabar el minijoc

    // ── VARIABLES D'ESTAT INTERN ─────────────────────────────────────
    private bool waitingForInput = false;             // Defineix si ja hem acabat l'animació i podem acceptar input
    private TMP_Text promptText;                      // El text del prompt d'acció inferior que parpelleja

    private void Start()
    {
        // Construeix i dibuixa tots els elements visuals de la UI de forma procedural
        Build();
        // Inicia el lliscament elàstic i els comptadors visuals de premis
        StartCoroutine(AnimateIn());
    }

    private void Update()
    {
        // Si encara estem comptant l'or o fent l'animació elàstica de la targeta, no acceptem tancaments
        if (!waitingForInput) return;
        
        // Acceptem tancaments en prémer: E, Retorn o la barra d'espai
        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
        {
            waitingForInput = false;
            onDone?.Invoke();
        }
    }

    // ── COMPONENTS D'INTERFÍCIE GENERATS ──────────────────────────────
    private CanvasGroup   bgCG;           // Gestiona la transparència del conjunt d'elements
    private RectTransform titleRect;      // Referència per al títol "VICTORY!"
    private RectTransform cardRect;       // Referència per a la targeta central elàstica
    private TMP_Text      goldValueText;  // Text on s'imprimeix l'animació incremental d'or

    private TMP_FontAsset pixelFont;      // Referència a la font retro del joc
    private Sprite        coinSprite;     // Recurs de gràfic de la moneda
    private Sprite        bagSprite;      // Recurs de gràfic del sac/bossa

    // ── CONSTRUCCIÓ PROCEDURAL (DISSENY RETRO PREMIUM) ───────────────
    /// <summary>
    /// Bucle de construcció totalment dinàmic que dissenya el panell des de zero.
    /// Ajusta la mida vertical (alçada) de la targeta informativa segons la quantitat d'objectes
    /// a llistar, per evitar que quedi espai buit o es desbordin els continguts.
    /// </summary>
    private void Build()
    {
        // Carrega dinàmica de fonts de forma progressiva per evitar fallades en el Build
        pixelFont = LoadFont("determination SDF");
        if (pixelFont == null) pixelFont = LoadFont("PixelOperator SDF");
        if (pixelFont == null) pixelFont = LoadFont("8bitoperator_jve SDF");
        if (pixelFont == null) pixelFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

        // Carrega d'imatges pixel-art d'origen
        coinSprite = LoadSprite(COIN_PATH);
        bagSprite  = LoadSprite(BAG_PATH);

        // ── OVERLAY COMPLET DE PANTALLA ───────────────────────────────
        var selfRT = gameObject.AddComponent<RectTransform>();
        selfRT.anchorMin = Vector2.zero;
        selfRT.anchorMax = Vector2.one;
        selfRT.offsetMin = Vector2.zero;
        selfRT.offsetMax = Vector2.zero;

        bgCG = gameObject.AddComponent<CanvasGroup>();
        bgCG.alpha = 1f;

        // ── CALCULAR ALÇADA DINÀMICA DE LA CARD DE PREMIS ─────────────
        // Calculem les files addicionals necessàries per distribuir els objectes en un format de graella a 2 columnes
        int extraRows = 0;
        if (items.Count > 0)
        {
            var distinctItems = new System.Collections.Generic.HashSet<string>(items);
            extraRows = (distinctItems.Count + 1) / 2; // Arrodoniment cap amunt en dividint per dues columnes
        }
        float cardBaseH = (items.Count == 0) ? 420f : 500f;
        float cardH     = cardBaseH + extraRows * 100f; // Més alçada proporcional per cada fila extra d'inventari

        // ── TARGETA INFORMATIVA CENTRAL ──────────────────────────────
        var card = NewChild("Card", transform);
        cardRect = card.GetComponent<RectTransform>();
        // Ancorada horitzontalment al centre de la pantalla ocupant el 60% d'espai (deixant 20% lliure a cada costat)
        cardRect.anchorMin        = new Vector2(0.2f, 0.5f);
        cardRect.anchorMax        = new Vector2(0.8f, 0.5f);
        cardRect.offsetMin        = new Vector2(0f, -cardH / 2f);
        cardRect.offsetMax        = new Vector2(0f, cardH / 2f);
        // Inicialment un xic a sota per al lliscament fluid d'entrada
        cardRect.anchoredPosition = new Vector2(0f, -50f);

        // Color de fons de la targeta (blau fosc estil RPG retro)
        card.AddComponent<Image>().color = new Color(0.08f, 0.07f, 0.16f, 1f);

        // ── FRANJA DE CAPÇALERA LILA (HEADER) ──────────────────────────
        float headerH = 140f;
        var header = NewChild("Header", card.transform);
        var hRT = header.GetComponent<RectTransform>();
        hRT.anchorMin = new Vector2(0f, 1f);
        hRT.anchorMax = new Vector2(1f, 1f);
        hRT.offsetMin = new Vector2(0f, -headerH);
        hRT.offsetMax = Vector2.zero;
        header.AddComponent<Image>().color = new Color(0.18f, 0.10f, 0.34f, 1f);

        // Títol central gegant "VICTORY!" en groc brillant
        var titleGo = NewChild("Title", header.transform);
        titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.anchorMin = Vector2.zero;
        titleRect.anchorMax = Vector2.one;
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;
        var titleTxt = titleGo.AddComponent<TextMeshProUGUI>();
        SetFont(titleTxt, 80f, new Color(1f, 0.92f, 0.2f), FontStyles.Bold, TextAlignmentOptions.Center);
        titleTxt.enableAutoSizing = true;
        titleTxt.fontSizeMin = 40f;
        titleTxt.fontSizeMax = 90f;
        titleTxt.text = "*  VICTORY!  *";

        // ── CONTORN PIXEL DAURAT I CANTONADES DECORATIVES ─────────────
        float brd = 8f;
        AddPixelBorder(card, new Color(0.95f, 0.80f, 0.15f, 1f), brd);
        AddCornerSquares(card, new Color(0.95f, 0.80f, 0.15f, 1f), 16f);

        // ── FILA D'INFORMACIÓ DE L'OR GUANYAT ─────────────────────────
        float currentY = cardH / 2f - headerH - 70f;

        // Instanciem la fila de l'or amb la moneda, s'inicialitza amb +0 i el seu valor previ per l'efecte count-up
        goldValueText = MakeIconRow(card, coinSprite, "Gold", $"+0 G ({totalGold - goldEarned} G)",
                                    new Color(1f, 0.90f, 0.15f), currentY);
        currentY -= 100f;

        // Línia horitzontal divisòria decorativa
        AddHLine(card, currentY + 30f, new Color(0.95f, 0.80f, 0.15f, 0.35f));
        currentY -= 20f;

        // ── LLISTAT D'OBJECTES (ITEMS) ADQUIRITS ───────────────────────
        if (items.Count == 0)
        {
            // Failsafe visual si no s'ha obtingut cap objecte en el combat
            MakeIconRow(card, bagSprite, "Items acquired", "-",
                        new Color(0.6f, 0.6f, 0.6f), currentY);
            currentY -= 100f;
        }
        else
        {
            // Capçalera per a la secció dels objectes
            MakeIconRow(card, bagSprite, "Items acquired", "", Color.white, currentY);
            currentY -= 100f;

            // Agrupem els objectes guanyats per calcular-ne la quantitat de cadascun de forma òptima
            var itemCounts = new System.Collections.Generic.Dictionary<string, int>();
            foreach(var item in items)
            {
                if(itemCounts.ContainsKey(item)) itemCounts[item]++;
                else itemCounts[item] = 1;
            }

            // Dibuixem la graella dinàmica a dues columnes amb els sprites reals de l'inventari
            BuildItemGrid(card, itemCounts, ref currentY);
        }

        AddHLine(card, currentY + 36f, new Color(0.95f, 0.80f, 0.15f, 0.35f));
        currentY -= 20f;

        // ── PROMPT INFERIOR DE TECLA DE CONTINUACIÓ ───────────────────
        var promptGo = NewChild("Prompt", transform);
        var promptRt = promptGo.GetComponent<RectTransform>();
        promptRt.anchorMin        = new Vector2(0.5f, 0f);
        promptRt.anchorMax        = new Vector2(0.5f, 0f);
        promptRt.sizeDelta        = new Vector2(1000f, 60f);
        promptRt.anchoredPosition = new Vector2(0f, 50f); // A prop de la part inferior de la pantalla per a equilibri visual
        promptText = promptGo.AddComponent<TextMeshProUGUI>();
        
        // Transparència a 0 inicialment, la corrutina de Blink el farà aparèixer gradualment
        SetFont(promptText, 52f, new Color(1f, 1f, 1f, 0f), 
                FontStyles.Normal, TextAlignmentOptions.Center);
        promptText.text = "[ PRESS E OR ENTER TO CONTINUE ]";
        
        // Apliquem un contorn natiu de TextMeshPro de color fosc molt precís a sobre d'un material instanciat
        promptText.fontSharedMaterial = Instantiate(promptText.fontSharedMaterial);
        promptText.fontSharedMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.25f);
        promptText.fontSharedMaterial.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.04f, 0.04f, 0.04f, 1f));
    }

    // ── HELPERS CONSTRUCTORS DE LA GRAELLA D'OBJECTES ──────────────────
    /// <summary>
    /// Genera de forma dinàmica una quadrícula de dues columnes simètriques.
    /// Localitza els perfils de cada objecte a través de l'inventari del jugador per recuperar-ne la icona original,
    /// i hi afegeix la quantitat acumulada en format 'x1', 'x2'...
    /// </summary>
    private void BuildItemGrid(GameObject parent, System.Collections.Generic.Dictionary<string, int> itemCounts, ref float currentY)
    {
        int count = 0;
        int maxCols = 2;
        float colWidth = 330f;
        float startX = (-colWidth * 0.5f) - 30f; // Coordenada inicial X per a la primera columna
        
        foreach (var kvp in itemCounts)
        {
            // Calculem si col·loquem el bloc a l'esquerra o a la dreta del centre en funció de l'índex de recompensa
            float colOffset = (count % maxCols == 0) ? startX : startX + colWidth + 60f; 
            
            // Provem de recuperar el fitxer ScriptableObject d'aquest objecte per obtenir la seva icona real
            var profile = PlayerInventory.Instance != null ? PlayerInventory.Instance.GetItemProfile(kvp.Key) : null;
            Sprite itemSprite = profile != null ? profile.itemIcon : bagSprite;
            string itemName = profile != null ? profile.itemName : kvp.Key;
            
            string val = $"{itemName} x{kvp.Value}";
            
            var row = NewChild($"Item_{kvp.Key}", parent.transform);
            var rowRt = row.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0.5f, 0.5f);
            rowRt.anchorMax = new Vector2(0.5f, 0.5f);
            rowRt.sizeDelta = new Vector2(colWidth, 100f);
            rowRt.anchoredPosition = new Vector2(colOffset, currentY);

            // Icona pixel-art de l'objecte
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
                imgComp.material = null; // Assegura renderitzat sense distorsió
            }

            // Descripció de l'objecte i recompensa
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
            // Cada dues columnes inserides, baixem verticalment una línia de pastilles per a la següent parella
            if (count % maxCols == 0) currentY -= 90f;
        }
        
        // Seguretat per al disseny: si l'últim element ha quedat senar, s'ha de reduir igualment la coordenada Y
        if (count % maxCols != 0) currentY -= 90f;
    }

    /// <summary>
    /// Construeix una fila decorada que conté una icona alineada a l'esquerra, un títol i el valor numèric.
    /// </summary>
    private TMP_Text MakeIconRow(GameObject parent, Sprite icon, string label, string value,
                                  Color valueColor, float yOffset)
    {
        var row = NewChild($"Row_{label}", parent.transform);
        var rowRt = row.GetComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0.15f, 0.5f);
        rowRt.anchorMax = new Vector2(0.85f, 0.5f);
        rowRt.offsetMin = Vector2.zero;
        rowRt.offsetMax = Vector2.zero;
        rowRt.sizeDelta = new Vector2(0f, 120f);
        rowRt.anchoredPosition = new Vector2(0f, yOffset);

        // Icona visual representativa de la línia
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
            imgComp.material = null;
        }

        // Títol o etiqueta a l'esquerra de la fila
        var lblGo = NewChild("Label", row.transform);
        var lblRt = lblGo.GetComponent<RectTransform>();
        lblRt.anchorMin = new Vector2(0f, 0f);
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

        // Valor a la dreta de la fila (en color especial, per ex. groc per a l'or)
        var valGo = NewChild("Value", row.transform);
        var valRt = valGo.GetComponent<RectTransform>();
        valRt.anchorMin = new Vector2(0.5f, 0f);
        valRt.anchorMax = new Vector2(1f, 1f);
        valRt.offsetMin = Vector2.zero;
        valRt.offsetMax = Vector2.zero;
        var valTxt = valGo.AddComponent<TextMeshProUGUI>();
        SetFont(valTxt, 64f, valueColor, FontStyles.Bold, TextAlignmentOptions.MidlineRight);
        valTxt.enableAutoSizing = true;
        valTxt.fontSizeMin = 30f;
        valTxt.fontSizeMax = 64f;
        valTxt.textWrappingMode = TextWrappingModes.NoWrap;
        valTxt.text = value;
        return valTxt;
    }

    /// <summary>
    /// Afegeix una línia horitzontal divisòria estilitzada.
    /// </summary>
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

    /// <summary>
    /// Dibuixa quatre franges d'imatge pixelades a les vores per crear un marc o silueta elegant de gruix ajustable.
    /// </summary>
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

    /// <summary>
    /// Col·loca petits quadrats a les quatre cantonades per accentuar l'estil retro arcade de la UI.
    /// </summary>
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

    /// <summary>
    /// Configura les propietats gràfiques de la lletra del component de text passat per paràmetre.
    /// </summary>
    private void SetFont(TMP_Text txt, float size, Color color, FontStyles style, TextAlignmentOptions align)
    {
        if (pixelFont != null) txt.font = pixelFont;
        txt.fontSize  = size;
        txt.color     = color;
        txt.fontStyle = style;
        txt.alignment = align;
    }

    /// <summary>
    /// Crea un fill de UI buit llest per a ser posicionat.
    /// </summary>
    private GameObject NewChild(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    /// <summary>
    /// Mètode segur per localitzar sprites a l'editor de Unity o recursos a la build,
    /// protegit amb compilar condicional.
    /// </summary>
    private Sprite LoadSprite(string assetsRelativePath)
    {
#if UNITY_EDITOR
        var sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/{assetsRelativePath}.png");
        if (sp != null) return sp;
#endif
        return Resources.Load<Sprite>(assetsRelativePath);
    }

    /// <summary>
    /// Mètode segur per carregar fonts de tipus TMP_FontAsset a l'editor de Unity o a Resources.
    /// </summary>
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

    // ── CORRUTINES D'ANIMACIÓ D'ENTRADA I EFECTE COUNT-UP ──────────────
    /// <summary>
    /// Corrutina d'entrada: llisca la targeta de premis cap al centre amb un efecte de pop,
    /// a continuació fa un recompte dinàmic de les monedes d'or fins al valor acumulat,
    /// i finalment activa el prompt de tecla inferior amb el seu bucle de parpelleig suau.
    /// </summary>
    private IEnumerator AnimateIn()
    {
        Vector2 cardTarget = cardRect.anchoredPosition;
        // Lliscament elàstic de la targeta informativa
        yield return SlideRect(cardRect, cardTarget + new Vector2(0f, -800f), cardTarget, 0.65f);

        yield return new WaitForSeconds(0.1f);
        // Iniciem l'efecte count-up de l'or sumat gradualment en el rellotge de temps
        yield return CountUp(goldValueText, goldEarned, totalGold, 0.7f);

        yield return new WaitForSeconds(0.2f);
        // Habilitem la recollida de tecles d'acció
        waitingForInput = true;
        // Iniciem el bucle infinit de parpelleig lumínic del prompt
        StartCoroutine(BlinkText(promptText));
    }

    /// <summary>
    /// Corrutina per esvair o enfosquir components tipus CanvasGroup de la interfície.
    /// </summary>
    private IEnumerator Fade(CanvasGroup cg, float from, float to, float dur)
    {
        float t = 0f; cg.alpha = from;
        while (t < dur) { t += Time.deltaTime; cg.alpha = Mathf.Lerp(from, to, t/dur); yield return null; }
        cg.alpha = to;
    }

    /// <summary>
    /// Efectua un lliscament físic de RectTransform acompanyat d'un escalat elàstic (Back Ease Out).
    /// El panell neix d'una escala reduïda 0.3 per donar-li un fort impuls orgànic d'esclat.
    /// </summary>
    private IEnumerator SlideRect(RectTransform rt, Vector2 from, Vector2 to, float dur)
    {
        float t = 0f; 
        rt.anchoredPosition = from;
        rt.localScale = new Vector3(0.3f, 0.3f, 1f);

        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            
            // Corba de descàrrega Back Ease Out
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

    /// <summary>
    /// Augmenta de forma progressiva i lineal el text d'or des de 0 fins a la xifra obtinguda,
    /// actualitzant alhora el recompte global al fons, generant un efecte mecànic d'alta qualitat.
    /// </summary>
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

    /// <summary>
    /// Corrutina infinita de parpelleig: realitza cicles continuats de faders d'opacitat del text
    /// de dany inferior mentre estiguem esperant la confirmació del teclat.
    /// </summary>
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

    /// <summary>
    /// Realitza un fader lineal d'entrada o sortida modificant exclusivament el canal Alpha.
    /// </summary>
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
