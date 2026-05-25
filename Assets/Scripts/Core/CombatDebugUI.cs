using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

/// <summary>
/// Panell de depuració de combats, trucs i consola de proves del desenvolupador (CombatDebugUI).
/// Dissenyat de forma que s'instancia automàticament al carregar el joc gràcies a mètodes de retrocàrrega.
/// Aquest component permet provar ràpidament el comportament físic dels enemics, simular els finals,
/// i concedir objectes o diners de forma instantània (Cheats) durant les fases de test del TFG.
/// 
/// CARACTERÍSTIQUES CLAU DEL TFG:
/// - **Auto-instanciació silenciosa**: Engegament automàtic sense embrutar la jerarquia de les escenes de disseny.
/// - **Compatibilitat de punter i clics directes**: Força la creació del sistema d'esdeveniments si manca en l'escena.
/// - **Interfície dinàmica sense scroll**: Tota la informació i generació de botons es dibuixa directament a pantalla
///   mitjançant distribucions lineals (Layouts) per a un ús òptim en dispositius mòbils/tàctils o amb punter.
/// - **Predicció dinàmica del final**: Analitza l'inventari per predir en temps real si l'usuari obtindrà el final Pacifista, Genocida o Mixte.
/// </summary>
public class CombatDebugUI : MonoBehaviour
{
    private PlayerController2D player;
    private CombatLoader combatLoader;

    private bool showDebug = false; // Flag que determina la visibilitat activa del menú
    private GameObject debugCanvasObj; // Contenidor pare de tota la UI
    
    /// <summary>
    /// Mètode d'inicialització automàtica executat immediatament després de carregar la primera escena.
    /// Això garanteix que la consola de debug estigui sempre disponible, fins i tot si provem escenes aïllades.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoSpawn()
    {
        if (GameObject.Find("CombatDebugUI") == null)
        {
            GameObject debugGO = new GameObject("CombatDebugUI");
            debugGO.AddComponent<CombatDebugUI>();
        }
    }

    // Elements gràfics de text actualitzables
    private TextMeshProUGUI statsText;
    private TextMeshProUGUI endingText;
    
    // Contenidors lineals de botons
    private Transform itemContentContainer;
    private Transform enemyContentContainer;
    private bool itemsPopulated = false; // Control de càrrega d'objectes per no duplicar accessos a disc

    private void Start()
    {
        // DontDestroyOnLoad només es pot aplicar sobre GameObjects que es trobin a l'arrel de la jerarquia.
        // Assegurem que l'objecte sigui orfe per evitar errors de Unity.
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
        BuildUI();
    }

    private void Update()
    {
        // Permitir alternar el menú utilitzant dreceres típiques de depuració (F12, F10 o F8)
        if (Input.GetKeyDown(KeyCode.F12) || Input.GetKeyDown(KeyCode.F10) || Input.GetKeyDown(KeyCode.F8))
        {
            showDebug = !showDebug;
            
            // Re-generem els elements si l'objecte del Canvas ha estat destruït accidentalment
            if (debugCanvasObj == null) 
            {
                BuildUI();
                itemsPopulated = false;
            }

            if (debugCanvasObj != null)
            {
                debugCanvasObj.SetActive(showDebug);
                if (showDebug) 
                {
                    // Si el menú s'obre, ens assegurem que el cursor del ratolí sigui visible i es pugui interactuar
                    Cursor.visible = true;
                    Cursor.lockState = CursorLockMode.None;
                    
                    EnsureEventSystem();   // Verificació d'Events de Unity actius
                    RefreshDynamicData();  // Actualització del nivell de vida, or, etc.
                    PopulateContent();     // Re-ompliment del llistat de botons de combat i items
                }
            }
        }
    }

    /// <summary>
    /// Comprova que existeixi un mòdul de control de ratolí (EventSystem) a l'escena activa.
    /// Si no n'hi ha cap, l'instancia procedimentalment per garantir que els botons rebin els clics o gestos tàctils.
    /// </summary>
    private void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject esObj = new GameObject("EventSystem_Debug");
            esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            DontDestroyOnLoad(esObj);
        }
    }

    /// <summary>
    /// Recull les dades actuals del jugador i actualitza les cadenes de text en pantalla.
    /// Afegeix la lògica predictiva d'avaluació de finals a partir dels assassinats o reclutaments.
    /// </summary>
    private void RefreshDynamicData()
    {
        if (player == null) player = FindFirstObjectByType<PlayerController2D>();
        if (combatLoader == null) combatLoader = FindFirstObjectByType<CombatLoader>();

        // Actualització de vida i monedes
        if (statsText != null)
        {
            if (PlayerInventory.Instance != null)
            {
                statsText.text = $"Vida actual: <color=#55FF55>{PlayerInventory.Instance.CurrentHP}</color> / <color=#55FF55>{PlayerInventory.Instance.MaxHP}</color>\n" +
                                 $"Or: <color=#FFDD55>{PlayerInventory.Instance.Gold}</color> G";
            }
            else
            {
                statsText.text = "No s'ha trobat l'inventari del jugador.";
            }
        }

        // ── PREDICCIÓ DE FINALS (ANÀLISI D'INVENTARI) ──
        if (endingText != null)
        {
            int totalKills = 0;
            int totalRecruits = 0;
            int totalMaxPopulation = 0;

            // Llegim la totalitat dels enemics creats al joc a la carpeta Resources
            EnemyProfile[] allEnemies = Resources.LoadAll<EnemyProfile>("Enemies");
            foreach (var p in allEnemies)
            {
                if (p != null) totalMaxPopulation += p.maxRecruitLimit;
            }
            // Evitem divisions per zero o índexs buits
            totalMaxPopulation = Mathf.Max(1, totalMaxPopulation - 1);

            if (PlayerInventory.Instance != null)
            {
                foreach (var kv in PlayerInventory.Instance.KilledEnemies) totalKills += kv.Value;
                foreach (var kv in PlayerInventory.Instance.RecruitedEnemies) totalRecruits += kv.Value;
            }

            string endingType = "Mixte";
            string colorHex = "#FFFF55"; 
            
            // Heurística de predicció del TFG
            if (totalKills == 0 && totalRecruits == 0)
            {
                endingType = "Ignorant / Observador";
                colorHex = "#55FFFF";
            }
            else if (totalRecruits == 0 && totalKills >= totalMaxPopulation && totalMaxPopulation > 0)
            {
                endingType = "Genocida";
                colorHex = "#FF5555"; 
            }
            else if (totalKills == 0 && totalRecruits >= totalMaxPopulation && totalMaxPopulation > 0)
            {
                endingType = "Reclutador / Pacifista";
                colorHex = "#55FF55"; 
            }
            
            endingText.text = $"Enemics Totals: {totalMaxPopulation}\n" +
                              $"Morts Totals: {totalKills}\n" +
                              $"Reclutaments Totals: {totalRecruits}\n\n" +
                              $"Final Predit: <color={colorHex}>{endingType.ToUpper()}</color>";
        }
    }

    /// <summary>
    /// Escaneja la base de dades d'objectes (ScriptableObjects) del joc i genera els botons dinàmics per demanar-los.
    /// També escaneja els enemics de la zona activa del jugador per permetre batalles forçades a l'acte.
    /// </summary>
    private void PopulateContent()
    {
        // ── GENERACIÓ DINÀMICA DE BOTONS D'ITEMS ──
        if (!itemsPopulated)
        {
            List<ItemProfile> availableItems = new List<ItemProfile>();
            HashSet<string> itemNames = new HashSet<string>();

            // Cerca prioritària d'assets dins de l'Editor de Unity
#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:ItemProfile");
            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                ItemProfile item = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemProfile>(path);
                if (item != null && !itemNames.Contains(item.itemName)) 
                {
                    availableItems.Add(item);
                    itemNames.Add(item.itemName);
                }
            }
#endif

            // Cerca a Resources (imprescindible perquè funcioni a les Builds finals del TFG)
            ItemProfile[] resourcesItems = Resources.LoadAll<ItemProfile>("");
            foreach (var item in resourcesItems)
            {
                if (item != null && !itemNames.Contains(item.itemName))
                {
                    availableItems.Add(item);
                    itemNames.Add(item.itemName);
                }
            }
            
            // Cerca addicional a les llistes locals de seguretat de l'inventari
            if (PlayerInventory.Instance != null && PlayerInventory.Instance.itemDatabase != null)
            {
                foreach (var item in PlayerInventory.Instance.itemDatabase)
                {
                    if (item != null && !itemNames.Contains(item.itemName))
                    {
                        availableItems.Add(item);
                        itemNames.Add(item.itemName);
                    }
                }
            }

            // Neteja higiènica dels botons creats anteriorment per evitar pèrdues de memòria
            foreach (Transform child in itemContentContainer)
            {
                Destroy(child.gameObject);
            }

            // Generem un botó dinàmic per a cada objecte de la base de dades
            foreach (var item in availableItems)
            {
                if (item == null) continue;
                CreateButton(itemContentContainer, $"Afegir: {item.itemName}", () => {
                    if (PlayerInventory.Instance != null) {
                        PlayerInventory.Instance.AddItem(item.itemName);
                        Debug.Log($"[DEBUG] Added: {item.itemName}");
                        RefreshDynamicData();
                    }
                });
            }
            itemsPopulated = true;
        }

        // ── GENERACIÓ DE BOTONS DE LLUITA CONTRA ENEMICS WILD ──
        foreach (Transform child in enemyContentContainer)
        {
            Destroy(child.gameObject);
        }

        // Si el jugador està a sobre d'una zona d'enemics amb criatures assignades, les mostrem
        if (player != null && player.wildEnemies != null && player.wildEnemies.Length > 0)
        {
            foreach (var enemy in player.wildEnemies)
            {
                if (enemy == null) continue;
                CreateButton(enemyContentContainer, $"Lluitar {enemy.enemyName}", () => {
                    StartFight(enemy);
                });
            }
        }
        else
        {
            CreateText(enemyContentContainer, "No hi ha enemics a prop.");
        }
    }

    /// <summary>
    /// Dibuixa procedimentalment tota la finestra de trucs des de zero.
    /// Organitza tres columnes adaptatives amb fons semitransparents, botons estilitzats
    /// i controls de tancament per ratolí o gestos tàctils.
    /// </summary>
    private void BuildUI()
    {
        // 1. Instanciació del Canvas d'alta prioritat
        debugCanvasObj = new GameObject("CombatDebugCanvas");
        DontDestroyOnLoad(debugCanvasObj);
        Canvas canvas = debugCanvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000; // Valors molt alts per assegurar-nos que és el menú més visible
        
        CanvasScaler scaler = debugCanvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        GraphicRaycaster gr = debugCanvasObj.AddComponent<GraphicRaycaster>();
        gr.blockingObjects = GraphicRaycaster.BlockingObjects.None;
        gr.ignoreReversedGraphics = true;

        // 2. Fons de pantalla complet de la consola
        GameObject panelObj = new GameObject("BackgroundPanel");
        panelObj.transform.SetParent(debugCanvasObj.transform, false);
        RectTransform panelRT = panelObj.AddComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.02f, 0.02f);
        panelRT.anchorMax = new Vector2(0.98f, 0.98f);
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;
        
        Image panelImg = panelObj.AddComponent<Image>();
        panelImg.color = new Color(0.12f, 0.12f, 0.16f, 0.98f); 
        panelImg.raycastTarget = false; // No bloquegem clics de fons
        
        Outline outline = panelObj.AddComponent<Outline>();
        outline.effectColor = Color.white;
        outline.effectDistance = new Vector2(4, -4);

        // 3. Títol principal
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(panelRT, false);
        RectTransform titleRT = titleObj.AddComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0, 0.94f);
        titleRT.anchorMax = new Vector2(1, 1);
        titleRT.offsetMin = Vector2.zero;
        titleRT.offsetMax = Vector2.zero;
        
        TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "EINES DE DEPURACIÓ DEL TFG (F12)";
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.fontSize = 32;
        titleText.color = Color.white;
        titleText.raycastTarget = false;
        SetFont(titleText);

        // 4. Contenidor horitzontal de columnes
        GameObject colsObj = new GameObject("Columns");
        colsObj.transform.SetParent(panelRT, false);
        RectTransform colsRT = colsObj.AddComponent<RectTransform>();
        colsRT.anchorMin = new Vector2(0.01f, 0.01f);
        colsRT.anchorMax = new Vector2(0.99f, 0.92f);
        colsRT.offsetMin = Vector2.zero;
        colsRT.offsetMax = Vector2.zero;
        
        HorizontalLayoutGroup hlg = colsObj.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 15;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        // ── Columna 1: Trucs d'Estadístiques ──
        GameObject col1 = CreateColumn(colsObj.transform, "ESTADÍSTIQUES I TRUCS");
        statsText = CreateText(col1.transform, "Carregant...");
        
        CreateButton(col1.transform, "Restaurar Vida al Màxim", () => {
            if (PlayerInventory.Instance != null)
            {
                PlayerInventory.Instance.SetHP(PlayerInventory.Instance.MaxHP);
                var cm = FindFirstObjectByType<CombatManager>();
                if (cm != null) cm.DebugHealPlayerToMax();
                RefreshDynamicData();
            }
        });
        
        CreateButton(col1.transform, "Obtenir +500 Or", () => {
            if (PlayerInventory.Instance != null)
            {
                PlayerInventory.Instance.AddGold(500);
                RefreshDynamicData();
            }
        });

        CreateText(col1.transform, "\n<size=28>ESTAT DEL FINAL</size>");
        endingText = CreateText(col1.transform, "Carregant...");

        // ── Columna 2: Generador d'Objectes (SENSE SCROLL, botons directes) ──
        GameObject col2 = CreateColumn(colsObj.transform, "GENERADOR D'OBJECTES");
        itemContentContainer = CreateDirectContainer(col2.transform);
        
        // ── Columna 3: Forçar Combats de Prova (SENSE SCROLL) ──
        GameObject col3 = CreateColumn(colsObj.transform, "FORÇAR BOMBARDERS (FIGHT)");
        enemyContentContainer = CreateDirectContainer(col3.transform);
        
        // ── Botó Flotant de Tancament ──
        GameObject closeBtnObj = new GameObject("CloseButton");
        closeBtnObj.transform.SetParent(panelRT, false);
        RectTransform closeBtnRT = closeBtnObj.AddComponent<RectTransform>();
        closeBtnRT.anchorMin = new Vector2(1, 1);
        closeBtnRT.anchorMax = new Vector2(1, 1);
        closeBtnRT.pivot = new Vector2(1, 1);
        closeBtnRT.sizeDelta = new Vector2(200, 50);
        closeBtnRT.anchoredPosition = new Vector2(-20, -20);
        
        Image cImg = closeBtnObj.AddComponent<Image>();
        cImg.color = new Color(0.15f, 0.15f, 0.2f, 1f);
        cImg.raycastTarget = true;
        
        Outline cOutl = closeBtnObj.AddComponent<Outline>();
        cOutl.effectColor = Color.white;
        cOutl.effectDistance = new Vector2(2, -2);
        
        Button cBtn = closeBtnObj.AddComponent<Button>();
        cBtn.targetGraphic = cImg;
        cBtn.onClick.AddListener(() => {
            showDebug = false;
            debugCanvasObj.SetActive(false);
        });
        
        TextMeshProUGUI cTxt = CreateText(closeBtnObj.transform, "<color=#FF5555>TANCAR</color>");
        cTxt.alignment = TextAlignmentOptions.Center;
        cTxt.rectTransform.anchorMin = Vector2.zero;
        cTxt.rectTransform.anchorMax = Vector2.one;
        cTxt.rectTransform.sizeDelta = Vector2.zero;
        
        ColorBlock cCb = cBtn.colors;
        cCb.normalColor = Color.white;
        cCb.highlightedColor = new Color(0.6f, 0.6f, 0.6f);
        cCb.pressedColor = new Color(0.4f, 0.4f, 0.4f);
        cCb.selectedColor = Color.white;
        cCb.colorMultiplier = 1;
        cBtn.colors = cCb;

        debugCanvasObj.SetActive(false);
    }
    
    private GameObject CreateColumn(Transform parent, string title)
    {
        GameObject col = new GameObject("Column");
        col.transform.SetParent(parent, false);
        
        Image img = col.AddComponent<Image>();
        img.color = new Color(0, 0, 0, 0.4f);
        img.raycastTarget = false;
        
        VerticalLayoutGroup vlg = col.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(10, 10, 10, 10);
        vlg.spacing = 10;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        TextMeshProUGUI titleTxt = CreateText(col.transform, title);
        titleTxt.alignment = TextAlignmentOptions.Center;
        titleTxt.fontSize = 24;
        
        return col;
    }

    /// <summary>
    /// Genera un contenidor procedimental vertical adaptatiu de mida variable.
    /// Això elimina la necessitat d'utilitzar barres de desplaçament incòmodes per a pantalles tàctils.
    /// </summary>
    private Transform CreateDirectContainer(Transform parent)
    {
        GameObject container = new GameObject("DirectContent");
        container.transform.SetParent(parent, false);
        
        LayoutElement le = container.AddComponent<LayoutElement>();
        le.flexibleHeight = 1f; 
        
        VerticalLayoutGroup vlg = container.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 10;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        
        return container.transform;
    }

    private TextMeshProUGUI CreateText(Transform parent, string textStr)
    {
        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(parent, false);
        TextMeshProUGUI txt = txtObj.AddComponent<TextMeshProUGUI>();
        txt.text = textStr;
        txt.fontSize = 20;
        txt.color = Color.white;
        txt.raycastTarget = false;
        SetFont(txt);
        return txt;
    }

    /// <summary>
    /// Utility procedimental per instanciar botons interactius amb estètica retro.
    /// </summary>
    private void CreateButton(Transform parent, string textStr, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnObj = new GameObject("Button");
        btnObj.transform.SetParent(parent, false);
        
        LayoutElement le = btnObj.AddComponent<LayoutElement>();
        le.minHeight = 45;
        
        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(0.15f, 0.15f, 0.2f, 1f);
        img.raycastTarget = true; // Actiu per rebre gestos de punter
        
        Outline outl = btnObj.AddComponent<Outline>();
        outl.effectColor = Color.white;
        outl.effectDistance = new Vector2(2, -2);
        
        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = img; // IMPORTANT: Detecta esdeveniments sobre la imatge del botó
        btn.onClick.AddListener(onClick);
        
        TextMeshProUGUI txt = CreateText(btnObj.transform, textStr);
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = Color.white;
        txt.raycastTarget = false; // IMPORTANT: Desactivar Raycast al text per no tapar el clic del fons
        
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.6f, 0.6f, 0.6f);
        cb.pressedColor = new Color(0.4f, 0.4f, 0.4f);
        cb.selectedColor = Color.white;
        cb.colorMultiplier = 1;
        btn.colors = cb;
    }

    private void SetFont(TextMeshProUGUI t)
    {
        TMP_FontAsset f = null;
#if UNITY_EDITOR
        f = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/determination SDF.asset")
            ?? UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/PixelOperator SDF.asset") 
            ?? UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/PixelOperator SDF.asset");
#endif
        if (f == null)
        {
            f = Resources.Load<TMP_FontAsset>("Fonts/determination SDF") 
                ?? Resources.Load<TMP_FontAsset>("determination SDF")
                ?? Resources.Load<TMP_FontAsset>("Fonts/PixelOperator SDF") 
                ?? Resources.Load<TMP_FontAsset>("PixelOperator SDF");
        }
        if (f != null) t.font = f;
    }

    /// <summary>
    /// Congela l'entorn i desencadena l'inici asíncron d'una batalla contra una criatura concreta.
    /// </summary>
    private void StartFight(EnemyProfile enemy)
    {
        if (combatLoader == null) combatLoader = FindFirstObjectByType<CombatLoader>();
        if (combatLoader == null)
        {
            Debug.LogError("CombatDebugUI: CombatLoader not found!");
            return;
        }

        if (player != null) player.LockMovement();

        CombatEncounter enc = new CombatEncounter();
        enc.enemyProfile = enemy;
        
        combatLoader.StartCombat(enc);
        
        showDebug = false;
        if (debugCanvasObj != null) debugCanvasObj.SetActive(false);
    }
}
