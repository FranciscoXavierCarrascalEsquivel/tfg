using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gestor Principal del Sistema de Combat (CombatManager).
/// Aquest script és el nucli ("core") de tota la lògica de la batalla de tipus RPG del nostre TFG.
/// S'encarrega d'orquestrar els diferents torns (Torn de Jugador, Torn d'Enemic, Resolució i Fi),
/// gestionar les estadístiques dels personatges de forma persistent enllaçant amb l'inventari global (PlayerInventory),
/// respondre als atacs mitjançant la invocació de minijocs de precisió (SkillCheckUI), i oferir
/// interaccions avançades a través de l'arbre de comportament social per a la resolució pacífica (SocialBehaviorTree).
/// A més, incorpora reaccions asíncrones de l'enemic en forma de diàlegs tipus "typewriter", canvis de fase
/// per punts de vida dinàmics, i efectes visuals premium com ara partícules, batuts de pantalla ("shake") i count-ups d'or.
/// </summary>
public class CombatManager : MonoBehaviour
{
    // ─── ENUMERATS DE FLUX I CONTROL ────────────────────────────────
    /// <summary>
    /// Representa els estats principals de la màquina d'estats del combat.
    /// </summary>
    public enum State
    {
        Enter,       // Fase d'animació d'entrada i càrrega de dades
        PlayerTurn,  // El jugador tria la seva opció de menú (Atacar, Raonar, Objecte, Fugir)
        EnemyTurn,   // L'enemic inicia les coreografies d'esquivar (Bullet Hell)
        Resolve,     // Fase de càlculs o minijocs on es pausen les tecles bàsiques
        End          // Finalització del combat (derrota o victòria)
    }

    /// <summary>
    /// Representa les sub-fases de navegació per teclat a dins del menú.
    /// </summary>
    public enum MenuPhase
    {
        Main,        // Menú principal (Fight, Reason, Item, Flee)
        Target       // Menú de selecció d'objectiu (per defecte, el rival enemic)
    }

    // ─── CONFIGURACIÓ DE REFERÈNCIES DE LA UI ───────────────────────
    [Header("UI")]
    [SerializeField] private GameObject turnMenu;          // Marc o panell de fons que conté els botons d'acció
    [SerializeField] private Button fightButton;           // Botó d'acció "Fight" (Atacar)
    [SerializeField] private Button reasonButton;          // Botó d'acció "Reason" (Raonar/Negociar)
    [SerializeField] private Button itemButton;            // Botó d'acció "Item" (Utilitzar objecte)
    [SerializeField] private Button fleeButton;            // Botó d'acció "Flee" (Intentar fugir)
    [SerializeField] private SkillCheckUI skillCheckPrefab;// Prefab del minijoc de precisió per calcular els atacs

    private Button[] mainButtons;                          // Col·lecció de botons per facilitar la navegació per index
    private int selectedIndex = 0;                         // Index del botó que actualment està seleccionat
    private MenuPhase currentPhase = MenuPhase.Main;       // La fase actual del menú de selecció
    private string originalFightText;                      // Desa el text inicial del botó d'atacar per a restauracions

    private State state;                                   // L'estat actiu actual de la màquina d'estats
    /// <summary> Retorna si el combat s'ha donat per finalitzat. </summary>
    public bool IsEnded => state == State.End;
    private CombatEncounter encounter;                     // Dades del combat actiu (enemic, projectils...)
    private CombatLoader loader;                           // Enllaç al carregador per acabar i restaurar l'overworld
    
    // ─── ESTADÍSTIQUES DE COMBAT ─────────────────────────────────────
    [Header("Stats")]
    public int playerMaxHP = 100;                          // Vida màxima del jugador
    public int enemyMaxHP = 15;                            // Vida màxima de l'enemic
    private int playerCurrentHP;                           // Vida actual de l'avatar
    private int enemyCurrentHP;                            // Vida actual de la criatura rival
    
    // Variables de gestió per a les pocions de buff de velocitat de mans
    private int speedBuffRoundsLeft = 0;                   // Rondes restants de la poti activa
    private float currentSpeedBuffValue = 0f;              // Percentatge addicional de velocitat aplicat

    // ─── PANELLS DE RETRAT I INFORMACIÓ ──────────────────────────────
    [Header("UI Stats")]
    [SerializeField] private RectTransform playerUIPanel;  // Transformador del panell d'informació del jugador
    [SerializeField] private TMPro.TMP_Text playerNameText;// Nom del jugador a pantalla
    [SerializeField] private TMPro.TMP_Text playerHPText;  // Text de vida del jugador (HP actual / HP max)
    [SerializeField] private Image playerHPFill;           // Barra de vida lliscant del jugador
    [SerializeField] private Image playerPortraitImage;    // Retrat del jugador (rebrà el pop de parry/heals)
    
    [Space]
    [SerializeField] private RectTransform enemyUIPanel;    // Transformador del panell d'informació de l'enemic
    [SerializeField] private TMPro.TMP_Text enemyNameText;  // Nom del rival a pantalla
    [SerializeField] private TMPro.TMP_Text enemyHPText;    // Text de vida de l'enemic
    [SerializeField] private Image enemyHPFill;             // Barra de vida lliscant de l'enemic
    [SerializeField] private Image enemyPortraitImage;      // Retrat visual del monstre (rebrà l'efecte de trencament a pixels)

    // ─── COMPONETS I REFERÈNCIES D'AUDIO ──────────────────────────────
    [Header("Audio Feedback")]
    [SerializeField] private AudioClip moveMenuSound;      // So de canvi de botó en navegar
    [SerializeField] private AudioClip confirmMenuSound;   // So en clicar una de les opcions principals
    [SerializeField] private AudioClip attackSound;        // So d'inici de l'atac (ruleta)
    [SerializeField] private AudioClip enemyHitSound;      // So quan l'enemic pateix un cop d'espasa
    [SerializeField] private AudioClip takeDamageSound;     // So quan el jugador perd punts de vida
    [SerializeField] private AudioClip parrySound;          // So quan és bloca un projectil amb èxit (Parry)
    [SerializeField] private AudioClip defendParrySound;   // So per al bloqueig simple d'escut
    [SerializeField] private AudioClip explosionSound;     // So per a l'efecte visual d'explosió de partícules
    [SerializeField] private AudioClip playerMoveSound;    // Bucle de moviment de les mans en l'arena
    [SerializeField] private AudioClip victorySound;       // Música o fanfàrria de victòria final
    [SerializeField] private AudioClip playerActionVoice;   // Veu de narració per a accions descriptives
    private AudioSource audioSource;                       // Font de so per a efectes transitoris
    private AudioSource loopAudioSource;                   // Font de so exclusiva per a bucles de moviment de mans
    private AudioSource voiceAudioSource;                  // Font per a les veus de personatges (evita solapaments brutals)
    private Coroutine activeEnemySpeakCoroutine;           // Registre per controlar i poder aturar els diàlegs de l'enemic
    private bool isPhaseShiftingThisTurn = false;           // Bandera que immunitza contra diàlegs redundants durant transicions de fase

    // ─── CONFIGURACIÓ DE DERROTA (GAME OVER) ─────────────────────────
    [Header("Game Over Settings")]
    [SerializeField] private AudioClip gameOverMusic;      // So melancòlic en morir
    [SerializeField] private AudioClip gameOverVoice;      // Narrador final del Game Over
    [SerializeField] [TextArea] private string gameOverText = "..."; // Text explicatiu de la derrota

    // ─── EFECTES VISUALS I FRAGMENTACIÓ ──────────────────────────────
    [Header("VFX & Limits")]
    [SerializeField] private GameObject parryParticlePrefab;// Prefab d'ones de xoc que apareixen en blocar
    [SerializeField] private RectTransform projectileDestroyLimit; // Línia invisible on es destrueixen els projectils

    // ─── ANIMACIONS DE LLANÇAMENT D'OBJECTES ─────────────────────────
    [Header("Item Animation Settings")]
    [Tooltip("El punt físic de la pantalla (UI) on s'instancia l'objecte abans de ser llançat.")]
    [SerializeField] private RectTransform throwStartPoint;
    [Tooltip("L'alçada màxima de la trajectòria parabòlica del llançament.")]
    [SerializeField] private float throwArcHeight = 400f;
    [Tooltip("La línia Y de terra on reposarà l'objecte un cop ha tocat l'enemic.")]
    [SerializeField] private RectTransform itemGroundLine;

    private HandController[] handControllers;              // Referències del parell de mans de joc actives

    // Desa de seguretat de les posicions de disseny de la UI per realitzar desplaçaments (slides) nets
    private Vector2 playerUIOriginalPos;
    private Vector2 enemyUIOriginalPos;
    private Vector2 playerNameOriginalPos;
    private Vector2 playerHPTextOriginalPos;
    private Vector2 enemyNameOriginalPos;
    private Vector2 enemyHPTextOriginalPos;
    private Vector2 turnMenuOriginalPos;
    
    private Sprite softCircleSprite;                       // Textura radial difuminada generada per codi per a aures
    private Image selectionGlowImage;                      // Aura luminosa groga/vermella de selecció activa

    private RectTransform enemyBubbleRT;                   // Transform de la bombolla de diàleg procedural de l'enemic
    private TMPro.TMP_Text enemyDialogTxt;                 // Text dins de la bombolla de l'enemic
    private RectTransform enemyBubblePromptRT;             // Marc per al prompt [E] a la bombolla de text
    private CanvasGroup enemyBubblePromptCG;               // Control de transparència per al prompt de la bombolla
    private bool isEnemySpeaking;                          // Bandera que bloqueja interaccions mentre l'enemic parla
    private Sprite generatedRoundedSprite;                 // Sprite de cantonades tallades en 9-slice generat per codi

    // Indexadors per controlar les respostes orals lògiques seqüencials de l'enemic en cada circumstància
    private int attackReactionIndex = 0;
    private int healReactionIndex = 0;
    private int fleeFailReactionIndex = 0;

    // Estat actiu del graf de diàleg social (Raonar)
    private SocialBTState socialState;
    private GameObject socialMenuGO;                       // Objecte contenidor del menú d'opcions de conversa

    // Desa el perfil del reclutat per disparar el panell de premi en finalitzar de debò la pantalla
    private EnemyProfile pendingRecruitReward;

    // Control de progressió de fases per dany
    private int currentPhaseIndex = -1;
    private EnemyAttackPattern[] currentPhaseAttacks;      // Conjunt d'atacs assignats a la fase activa
    private EnemyAttackPattern? lastUsedPattern = null;    // Evita la repetició idèntica consecutiva de patrons

    /// <summary>
    /// Entrega la recompensa pendent de reclutament i neteja el camp per evitar dobles instàncies.
    /// </summary>
    public EnemyProfile ConsumeRecruitReward()
    {
        var r = pendingRecruitReward;
        pendingRecruitReward = null;
        return r;
    }

    // Inicialització primerenca
    private void Awake()
    {
        CreateSoftCircle(); // Genera de manera dinàmica la textura circular suavitzada per a partícules i aures de selecció

        if (enemyPortraitImage != null) enemyPortraitImage.enabled = false;

        // Instanciació segura de fonts d'àudio dedicades
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        loopAudioSource = gameObject.AddComponent<AudioSource>();
        loopAudioSource.playOnAwake = false;
        loopAudioSource.loop = true;
        loopAudioSource.spatialBlend = 0f;

        voiceAudioSource = gameObject.AddComponent<AudioSource>();
        voiceAudioSource.playOnAwake = false;
        voiceAudioSource.spatialBlend = 0f;

        // Emmagatzemem totes les posicions de disseny per poder fer-les lliscar correctament des de fora dels marges visibles
        if (turnMenu != null) 
        {
            var rt = turnMenu.GetComponent<RectTransform>();
            turnMenuOriginalPos = rt.anchoredPosition;
            rt.anchoredPosition = turnMenuOriginalPos + new Vector2(0, -500f); // Neix amagat cap avall
        }
        
        if (playerUIPanel != null) 
        {
            playerUIOriginalPos = playerUIPanel.anchoredPosition;
            playerUIPanel.anchoredPosition = playerUIOriginalPos + new Vector2(0, 300f); // Neix amagat cap amunt
        }
        else if (playerHPText != null)
        {
            var rt = playerHPText.GetComponent<RectTransform>();
            playerUIOriginalPos = rt.anchoredPosition;
            rt.anchoredPosition = playerUIOriginalPos + new Vector2(0, 300f);
        }
        
        if (enemyUIPanel != null) 
        {
            enemyUIOriginalPos = enemyUIPanel.anchoredPosition;
            enemyUIPanel.anchoredPosition = enemyUIOriginalPos + new Vector2(0, 300f);
        }

        // Posicions per a textos individuals del combat
        if (playerNameText != null) playerNameOriginalPos = playerNameText.rectTransform.anchoredPosition;
        if (playerHPText != null) playerHPTextOriginalPos = playerHPText.rectTransform.anchoredPosition;
        if (enemyNameText != null) enemyNameOriginalPos = enemyNameText.rectTransform.anchoredPosition;
        if (enemyHPText != null) enemyHPTextOriginalPos = enemyHPText.rectTransform.anchoredPosition;
        
        if (playerNameText != null) playerNameText.rectTransform.anchoredPosition += new Vector2(0, 300f);
        if (playerHPText != null) playerHPText.rectTransform.anchoredPosition += new Vector2(0, 300f);
        if (enemyNameText != null) enemyNameText.rectTransform.anchoredPosition += new Vector2(0, 300f);
        if (enemyHPText != null) enemyHPText.rectTransform.anchoredPosition += new Vector2(0, 300f);
        
        // Construeix de manera completament procedural la bombolla del rival sobre el seu cap
        BuildEnemyBubble();
    }
    
    /// <summary>
    /// Genera per programació un sprite de cantonades pixlades tallades en 9-slice.
    /// Ideal per donar format RPG retro a la bombolla del monstre.
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
                // Tallem els extrems per simular una arrodoniment pixel-art clàssic
                bool corner = false;
                if (x==0 && y<=2) corner = true;
                else if (x==1 && y<=1) corner = true;
                else if (x==2 && y==0) corner = true;
                else if (x==size-1 && y<=2) corner = true;
                else if (x==size-2 && y<=1) corner = true;
                else if (x==size-3 && y==0) corner = true;
                else if (x==0 && y>=size-3) corner = true;
                else if (x==1 && y>=size-2) corner = true;
                else if (x==2 && y==size-1) corner = true;
                else if (x==size-1 && y>=size-3) corner = true;
                else if (x==size-2 && y>=size-2) corner = true;
                else if (x==size-3 && y==size-1) corner = true;

                tex.SetPixel(x, y, corner ? c : w);
            }
        }
        tex.Apply();
        
        generatedRoundedSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(4, 4, 4, 4));
        return generatedRoundedSprite;
    }

    /// <summary>
    /// Construeix de forma procedural els components visuals de la bombolla de diàleg de l'enemic
    /// (cua de direcció, caixa de fons 9-sliced, text auto-ajustable i la tecla [E] premible).
    /// </summary>
    private void BuildEnemyBubble()
    {
        if (enemyPortraitImage == null) return;
        
        GameObject go = new GameObject("EnemyBubble");
        go.transform.SetParent(enemyPortraitImage.transform, false);
        enemyBubbleRT = go.AddComponent<RectTransform>();
        
        enemyBubbleRT.anchorMin = new Vector2(-0.25f, 0.85f);
        enemyBubbleRT.anchorMax = new Vector2(1.25f, 1.25f);
        enemyBubbleRT.offsetMin = enemyBubbleRT.offsetMax = Vector2.zero;
        
        // Cua de la bombolla (la punta que senyala la boca de la imatge)
        var tailGO = new GameObject("Tail");
        tailGO.transform.SetParent(enemyBubbleRT, false);
        var tailRT = tailGO.AddComponent<RectTransform>();
        tailRT.anchorMin = new Vector2(0.5f, 0f); tailRT.anchorMax = new Vector2(0.5f, 0f);
        tailRT.sizeDelta = new Vector2(36f, 36f);
        tailRT.anchoredPosition = new Vector2(-20f, 8f);
        tailRT.localRotation = Quaternion.Euler(0, 0, 65f);
        var tailImg = tailGO.AddComponent<Image>();
        tailImg.color = new Color(1f, 1f, 1f, 0.95f);

        // Fons blanc semi-transparent
        var bgGO = new GameObject("BG");
        bgGO.transform.SetParent(enemyBubbleRT, false);
        var bgRT = bgGO.AddComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(1f, 1f, 1f, 0.95f);
        bgImg.sprite = GetRoundedSprite();
        bgImg.type = Image.Type.Sliced;
        bgImg.pixelsPerUnitMultiplier = 0.5f;
        
        // Text del diàleg en negreta i autoescalable preventiu
        var txtGO = new GameObject("DialogText");
        txtGO.transform.SetParent(enemyBubbleRT, false);
        var tRT = txtGO.AddComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
        tRT.offsetMin = tRT.offsetMax = Vector2.zero;
        
        enemyDialogTxt = txtGO.AddComponent<TMPro.TextMeshProUGUI>();
        enemyDialogTxt.color = new Color(0.1f, 0.1f, 0.1f);
        enemyDialogTxt.fontStyle = TMPro.FontStyles.Bold;
        enemyDialogTxt.alignment = TMPro.TextAlignmentOptions.Center;
        enemyDialogTxt.margin = new Vector4(20f, 20f, 20f, 20f);
        enemyDialogTxt.textWrappingMode = TMPro.TextWrappingModes.Normal;
        enemyDialogTxt.enableAutoSizing = true;
        enemyDialogTxt.fontSizeMin = 18f;
        enemyDialogTxt.fontSizeMax = 32f;
        
        if (playerNameText != null && playerNameText.font != null)
        {
            enemyDialogTxt.font = playerNameText.font;
        }

        // Tecla E de prompt de continuació de text
        var promptGO = new GameObject("EPrompt");
        promptGO.transform.SetParent(enemyBubbleRT, false);
        enemyBubblePromptRT = promptGO.AddComponent<RectTransform>();
        enemyBubblePromptRT.anchorMin = new Vector2(1f, 0f);
        enemyBubblePromptRT.anchorMax = new Vector2(1f, 0f);
        enemyBubblePromptRT.pivot = new Vector2(1f, 0f);
        enemyBubblePromptRT.sizeDelta = new Vector2(32f, 32f);
        enemyBubblePromptRT.anchoredPosition = new Vector2(-10f, 10f);

        enemyBubblePromptCG = promptGO.AddComponent<CanvasGroup>();
        enemyBubblePromptCG.alpha = 0f;

        // Ombra inferior del botonet
        var pBase = new GameObject("Base");
        pBase.transform.SetParent(enemyBubblePromptRT, false);
        var pbRT = pBase.AddComponent<RectTransform>();
        pbRT.anchorMin = Vector2.zero; pbRT.anchorMax = Vector2.one; pbRT.offsetMin = pbRT.offsetMax = Vector2.zero;
        var pbImg = pBase.AddComponent<Image>();
        pbImg.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        
        // Cos premible del botó
        var pTop = new GameObject("Top");
        pTop.transform.SetParent(enemyBubblePromptRT, false);
        var ptRT = pTop.AddComponent<RectTransform>();
        ptRT.anchorMin = Vector2.zero; ptRT.anchorMax = Vector2.one; ptRT.offsetMin = ptRT.offsetMax = Vector2.zero;
        ptRT.anchoredPosition = new Vector2(0f, 3f);
        var ptImg = pTop.AddComponent<Image>();
        ptImg.color = new Color(0.9f, 0.9f, 0.9f, 1f);

        var pTxtGO = new GameObject("T");
        pTxtGO.transform.SetParent(ptRT, false);
        var ptxRT = pTxtGO.AddComponent<RectTransform>();
        ptxRT.anchorMin = Vector2.zero; ptxRT.anchorMax = Vector2.one; ptxRT.offsetMin = ptxRT.offsetMax = Vector2.zero;
        var pTxt = pTxtGO.AddComponent<TMPro.TextMeshProUGUI>();
        pTxt.text = "E"; pTxt.fontSize = 20f; pTxt.color = Color.black; pTxt.alignment = TMPro.TextAlignmentOptions.Center;
        pTxt.fontStyle = TMPro.FontStyles.Bold;
        if (playerNameText != null && playerNameText.font != null) pTxt.font = playerNameText.font;

        enemyBubbleRT.gameObject.SetActive(false);
    }

    /// <summary>
    /// Genera en memòria una textura de cercle difuminat (Gaussiana) 
    /// per a ser utilitzada proceduralment com a partícula o glow de selecció de la UI.
    /// </summary>
    private void CreateSoftCircle()
    {
        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - size / 2f) / (size / 2f);
                float dy = (y - size / 2f) / (size / 2f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Exp(-4f * d * d) * Mathf.Clamp01(1f - d);
                tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
        }
        tex.Apply();
        softCircleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    /// <summary>
    /// Càrrega ràpida de la imatge de l'enemic abans d'iniciar el combat per evitar flaixos de fons blancs buits.
    /// </summary>
    public void PreSetup(CombatEncounter encounter)
    {
        Sprite finalEnemySprite = encounter != null ? encounter.enemyPortrait : null;
        if (encounter != null && encounter.enemyProfile != null && encounter.enemyProfile.enemyPortrait != null)
        {
            finalEnemySprite = encounter.enemyProfile.enemyPortrait;
        }

        if (enemyPortraitImage != null)
        {
            if (finalEnemySprite != null)
            {
                enemyPortraitImage.sprite = finalEnemySprite;
                enemyPortraitImage.preserveAspect = true;
                enemyPortraitImage.enabled = true;
            }
            else
            {
                enemyPortraitImage.enabled = false;
            }
        }
    }

    /// <summary>
    /// Corrutina inicializadora de la batalla. Restaura punts de vida, enllaça de forma asíncrona
    /// els perfils enemics, inicialitza els nodes del graf social de diàleg (BT),
    /// bloqueja la interacció del ratolí dels botons, i comença les animacions d'entrada tipus Slide.
    /// </summary>
    public IEnumerator BeginRoutine(CombatEncounter encounter, CombatLoader loader)
    {
        this.encounter = encounter;
        this.loader = loader;

        // Registrem que ens hem enfrontat a aquest enemic en l'inventari permanent de cara al TFG
        if (PlayerInventory.Instance != null && encounter != null && encounter.enemyProfile != null)
        {
            PlayerInventory.Instance.EncounterEnemy(encounter.enemyProfile.enemyName);
        }

        // Recuperació persistent dels HP de la partida de cara a l'overworld
        if (PlayerInventory.Instance != null && PlayerInventory.Instance.CurrentHP > 0)
        {
            playerMaxHP = PlayerInventory.Instance.MaxHP;
            playerCurrentHP = PlayerInventory.Instance.CurrentHP;
        }
        else
        {
            playerCurrentHP = playerMaxHP;
        }
        
        string finalEnemyName = "MONSTER";
        Sprite finalEnemySprite = encounter != null ? encounter.enemyPortrait : null;
        
        // Configurem els punts de vida i sprite reals extrets del perfil (ScriptableObject)
        if (encounter != null && encounter.enemyProfile != null)
        {
            enemyMaxHP = Random.Range(encounter.enemyProfile.minHP, encounter.enemyProfile.maxHP + 1);
            finalEnemyName = encounter.enemyProfile.enemyName.ToUpper();
            if (encounter.enemyProfile.enemyPortrait != null) finalEnemySprite = encounter.enemyProfile.enemyPortrait;
        }

        enemyCurrentHP = enemyMaxHP;
        
        // Inicialització dels atacs associats a la fase 1 (per defecte)
        currentPhaseIndex = -1;
        currentPhaseAttacks = (encounter?.enemyProfile != null) ? encounter.enemyProfile.attackPatterns : null;
        CheckPhaseShift(true); 

        // Inicialitzem l'Arbre de Comportament Social (BT)
        if (encounter != null && encounter.enemyProfile != null && encounter.enemyProfile.socialBT != null)
        {
            socialState = new SocialBTState(encounter.enemyProfile.socialBT.startNodeId);
        }
        else
        {
            socialState = null;
        }

        lastUsedPattern = null;
        UpdateStatsUI(true); // Actualitza barres de HP instantàniament a l'inici

        if (enemyPortraitImage != null && finalEnemySprite != null)
        {
            enemyPortraitImage.sprite = finalEnemySprite;
            enemyPortraitImage.enabled = true;
        }
        else if (enemyPortraitImage != null)
        {
            enemyPortraitImage.enabled = false;
        }

        mainButtons = new Button[] { fightButton, reasonButton, itemButton, fleeButton };
        
        if (fightButton != null)
        {
            originalFightText = GetButtonText(fightButton);
            if (string.IsNullOrEmpty(originalFightText)) originalFightText = "FIGHT";
        }

        // Bloquegem el ratolí dels botons ja que el disseny obliga a control exclusiu de teclat retro (A/D/E)
        SetupButtonInteractions();

        // Enllaç de les mans de combat
        handControllers = FindObjectsByType<HandController>(FindObjectsSortMode.None);
        SetHandsActive(false);

        state = State.PlayerTurn;

        // Esperem fins que acabi de parlar si s'ha disparat una transició de diàleg de fase al Begin
        if (isPhaseShiftingThisTurn)
        {
            while (isEnemySpeaking) yield return null;
            isPhaseShiftingThisTurn = false;
        }

        ShowTurnMenu(true);

        if (playerNameText != null) playerNameText.text = "Me";
        if (enemyNameText != null) enemyNameText.text = finalEnemyName;

        // Llancem les animacions d'entrada (Slide-in de tipus EaseOut) de tots els panells visuals del combat
        if (playerUIPanel != null) StartCoroutine(SlideInRect(playerUIPanel, playerUIOriginalPos, new Vector2(0, 300f), 0.7f));
        if (enemyUIPanel != null) StartCoroutine(SlideInRect(enemyUIPanel, enemyUIOriginalPos, new Vector2(0, 300f), 0.7f));
        
        if (playerNameText != null) StartCoroutine(SlideInRect(playerNameText.rectTransform, playerNameOriginalPos, new Vector2(0, 300f), 0.7f));
        if (playerHPText != null) StartCoroutine(SlideInRect(playerHPText.rectTransform, playerHPTextOriginalPos, new Vector2(0, 300f), 0.7f));
        if (enemyNameText != null) StartCoroutine(SlideInRect(enemyNameText.rectTransform, enemyNameOriginalPos, new Vector2(0, 300f), 0.7f));
        if (enemyHPText != null) StartCoroutine(SlideInRect(enemyHPText.rectTransform, enemyHPTextOriginalPos, new Vector2(0, 300f), 0.7f));

        // ── BOTONS DE CHEATS D'EDITOR (Només sota #if UNITY_EDITOR per evitar fallades de Build) ──
        #if UNITY_EDITOR
        SpawnDebugButtons();
        #endif
    }

    #if UNITY_EDITOR
    /// <summary>
    /// Genera botons flotants de depuració a l'Editor que permeten finalitzar el combat
    /// immediatament en mode pacífic (Perdonar) o violant (Matar), facilitant el test del TFG.
    /// </summary>
    private void SpawnDebugButtons()
    {
        Transform canvasParent = turnMenu != null ? turnMenu.transform.parent : transform;

        CreateDebugButton(canvasParent, "DBG_Peace", "✓ PERDONAR", new Vector2(-100f, -30f),
            new Color(0.15f, 0.5f, 0.15f, 0.85f), () =>
        {
            if (state == State.End) return;
            state = State.End;
            StopAllCoroutines();
            StartCoroutine(FriendVictoryRoutine());
        });

        CreateDebugButton(canvasParent, "DBG_Kill", "✗ MATAR", new Vector2(100f, -30f),
            new Color(0.5f, 0.15f, 0.15f, 0.85f), () =>
        {
            if (state == State.End) return;
            state = State.End;
            enemyCurrentHP = 0;
            StopAllCoroutines();
            StartCoroutine(VictoryRoutine());
        });
    }

    /// <summary> Instancia proceduralment cadascun dels botons de depuració de l'editor. </summary>
    private void CreateDebugButton(Transform parent, string name, string label, Vector2 pos, Color bgColor, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(170f, 40f);
        rt.anchoredPosition = pos;

        var img = go.AddComponent<Image>();
        img.color = bgColor;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        var txtGo = new GameObject("Text");
        txtGo.transform.SetParent(go.transform, false);
        var txtRT = txtGo.AddComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero; txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = Vector2.zero; txtRT.offsetMax = Vector2.zero;
        var txt = txtGo.AddComponent<TMPro.TextMeshProUGUI>();
        txt.text = label;
        txt.fontSize = 20f;
        txt.alignment = TMPro.TextAlignmentOptions.Center;
        txt.color = Color.white;
        txt.fontStyle = TMPro.FontStyles.Bold;
    }
    #endif

    /// <summary>
    /// Corrutina de desplaçament d'entrada (Slide-in) de RectTransforms basat en la corba Cubic Ease Out.
    /// </summary>
    private IEnumerator SlideInRect(RectTransform rect, Vector2 targetPos, Vector2 startOffset, float duration)
    {
        if (rect == null) yield break;
        
        Vector2 startPos = targetPos + startOffset;
        rect.anchoredPosition = startPos;
        
        float time = 0;
        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;
            float easeT = 1f - Mathf.Pow(1f - t, 3f);
            rect.anchoredPosition = Vector2.Lerp(startPos, targetPos, easeT);
            yield return null;
        }
        
        rect.anchoredPosition = targetPos;
    }

    /// <summary>
    /// Corrutina de desplaçament de sortida (Slide-out) de RectTransforms basada en la corba Cubic Ease In.
    /// </summary>
    private IEnumerator SlideOutRect(RectTransform rect, Vector2 originalPos, Vector2 exitOffset, float duration)
    {
        if (rect == null) yield break;
        
        Vector2 targetPos = originalPos + exitOffset;
        float time = 0;
        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;
            float easeT = t * t * t;
            rect.anchoredPosition = Vector2.Lerp(originalPos, targetPos, easeT);
            yield return null;
        }
        rect.anchoredPosition = targetPos;
    }

    // Comprovació lògica en cada fotograma
    private void Update()
    {
        // Anima el botó de continuació [E] de la bombolla tipus pulsació 3D
        if (enemyBubblePromptCG != null && enemyBubblePromptRT != null)
        {
            if (enemyBubbleRT != null && enemyBubbleRT.gameObject.activeSelf && enemyBubblePromptCG.alpha > 0.5f)
            {
                float cycle = Time.unscaledTime * 1.5f;
                bool isPressed = (cycle % 1f) > 0.7f;
                if (enemyBubblePromptRT.childCount > 1)
                {
                    var topRT = enemyBubblePromptRT.GetChild(1).GetComponent<RectTransform>();
                    if (topRT != null) topRT.anchoredPosition = isPressed ? Vector2.zero : new Vector2(0f, 3f);
                }
            }
        }

        // Gestiona el bucle de so quan les mans es mouen per l'arena
        bool anyHandMoving = false;
        if (handControllers != null)
        {
            foreach (var h in handControllers)
            {
                if (h != null && h.IsMoving)
                {
                    anyHandMoving = true;
                    break;
                }
            }
        }

        if (anyHandMoving && playerMoveSound)
        {
            if (!loopAudioSource.isPlaying)
            {
                loopAudioSource.clip = playerMoveSound;
                loopAudioSource.Play();
            }
        }
        else
        {
            if (loopAudioSource != null && loopAudioSource.isPlaying)
            {
                loopAudioSource.Stop();
            }
        }

        // Si no és el torn del jugador o hi ha diàlegs actius de transició, ignorem inputs
        if (state != State.PlayerTurn || isPhaseShiftingThisTurn || isEnemySpeaking) return;

        // Bloquegem tecles si tenim el menú de pausa o d'inventari obert per sobre de la Canvas
        if (InventoryMenuUI.IsOpen || PauseMenuUI.IsOpen) return;

        // Navegació de menú clàssica retro (horitzontal mitjançant A/D o fletxes)
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
        {
            MoveSelection(-1);
        }
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
        {
            MoveSelection(1);
        }
        else if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return))
        {
            ConfirmSelection();
        }
        else if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift) || Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Escape))
        {
            // Permet desfer la selecció d'objectiu i tornar al panell principal
            if (currentPhase == MenuPhase.Target)
            {
                SetMenuPhase(MenuPhase.Main);
            }
        }
    }

    // ─── DESPLAÇAMENT I NAVEGACIÓ EN ELS BOTONS DE COMBAT ─────────────
    /// <summary>
    /// Canvia l'opció seleccionada actual, reprodueix el so de desplaçament i actualitza el contorn lluminós.
    /// </summary>
    private void MoveSelection(int direction)
    {
        int maxOptions = currentPhase == MenuPhase.Main ? mainButtons.Length : 1;
        selectedIndex += direction;
        
        if (selectedIndex < 0) selectedIndex = maxOptions - 1;
        if (selectedIndex >= maxOptions) selectedIndex = 0;
        
        if (moveMenuSound) audioSource.PlayOneShot(moveMenuSound);
        UpdateSelectionVisuals();
    }

    /// <summary>
    /// Confirma l'opció de menú triada i redirigeix l'execució al mètode d'acció corresponent.
    /// </summary>
    private void ConfirmSelection()
    {
        if (confirmMenuSound && audioSource) audioSource.PlayOneShot(confirmMenuSound);
        
        if (currentPhase == MenuPhase.Main)
        {
            // Saltem la fase d'objectiu innecessària quan només hi ha un sol enemic, accelerant el gameplay
            if (selectedIndex == 0) StartCoroutine(PerformAttackRoutine());
            else if (selectedIndex == 1) OnReason();
            else if (selectedIndex == 2) OnItem();
            else if (selectedIndex == 3) OnFlee();
        }
        else if (currentPhase == MenuPhase.Target)
        {
            if (selectedIndex == 0)
            {
                StartCoroutine(PerformAttackRoutine());
            }
        }
    }

    /// <summary> Bloqueja interaccions de ratolí per als botons per obligar al control estricte de teclat. </summary>
    private void SetupButtonInteractions()
    {
        for (int i = 0; i < mainButtons.Length; i++)
        {
            Button btn = mainButtons[i];
            if (btn != null)
            {
                btn.interactable = false;
            }
        }
    }

    /// <summary> Retorna el text interior d'un component botó de forma segura independentment de la font o TMPro. </summary>
    private string GetButtonText(Button btn)
    {
        var tmp = btn.GetComponentInChildren<TMPro.TMP_Text>();
        if (tmp != null) return tmp.text;
        var txt = btn.GetComponentInChildren<Text>();
        if (txt != null) return txt.text;
        return "";
    }

    /// <summary> Modifica de manera dinàmica el contingut d'un botó. </summary>
    private void SetButtonText(Button btn, string text)
    {
        var tmp = btn.GetComponentInChildren<TMPro.TMP_Text>();
        if (tmp != null) { tmp.text = text; return; }
        var txt = btn.GetComponentInChildren<Text>();
        if (txt != null) { txt.text = text; return; }
    }

    /// <summary> Alterna entre les fases de selecció principal i selecció d'objectius rivals. </summary>
    private void SetMenuPhase(MenuPhase newPhase)
    {
        currentPhase = newPhase;
        selectedIndex = 0;

        if (currentPhase == MenuPhase.Main)
        {
            if (fightButton != null) SetButtonText(fightButton, originalFightText);
        }
        else if (currentPhase == MenuPhase.Target)
        {
            if (fightButton != null) SetButtonText(fightButton, $"Enemy ({enemyCurrentHP} HP)");
        }

        UpdateSelectionVisuals();
    }

    private RectTransform selectionCursorFrame;

    /// <summary>
    /// Actualitza la presentació gràfica premium de selecció (Highlights / Aures de colors).
    /// Instancia una aura especial de fons dinàmica que hereta el color temàtic del botó triat
    /// (Vermell = Atacar, Groc = Conversa, Lila = Objectes, Blau = Fugir) i llança un bucle de partícules de fons.
    /// </summary>
    private void UpdateSelectionVisuals()
    {
        for (int i = 0; i < mainButtons.Length; i++)
        {
            Button btn = mainButtons[i];
            if (btn == null) continue;

            if (currentPhase == MenuPhase.Target && i > 0)
            {
                btn.gameObject.SetActive(false);
                continue;
            }
            else
            {
                btn.gameObject.SetActive(true);
            }
        }

        if (mainButtons.Length == 0 || selectedIndex < 0 || selectedIndex >= mainButtons.Length) return;

        Button selBtn = mainButtons[selectedIndex];
        if (selBtn == null || !selBtn.gameObject.activeInHierarchy) return;

        if (selectionCursorFrame == null)
        {
            // Creació procedural del component de ressaltat de disseny
            GameObject go = new GameObject("SelectionFX_VibrantYellowHighlight");
            selectionCursorFrame = go.AddComponent<RectTransform>();
            selectionGlowImage = go.AddComponent<Image>();
            selectionGlowImage.raycastTarget = false;
            
            StartCoroutine(PremiumGlowRoutine(selectionCursorFrame, selectionGlowImage));
        }

        selectionCursorFrame.gameObject.SetActive(true);
        selectionCursorFrame.SetParent(selBtn.transform, false);
        selectionCursorFrame.SetAsFirstSibling(); // Es dibuixa darrere dels textos/icones
        
        selectionCursorFrame.anchorMin = Vector2.zero;
        selectionCursorFrame.anchorMax = Vector2.one;
        selectionCursorFrame.pivot = new Vector2(0.5f, 0.5f);
        selectionCursorFrame.anchoredPosition = Vector2.zero;
        
        // Escalat de 5 píxels per sobresortir netament a les vores
        selectionCursorFrame.offsetMin = new Vector2(-5, -5);
        selectionCursorFrame.offsetMax = new Vector2(5, 5);
        selectionCursorFrame.localScale = Vector3.one;

        Image btnImg = selBtn.GetComponent<Image>();
        if (btnImg != null && selectionGlowImage != null)
        {
            selectionGlowImage.sprite = btnImg.sprite;
            selectionGlowImage.type = btnImg.type;
            selectionGlowImage.preserveAspect = btnImg.preserveAspect;
            
            // Assignem una paleta de colors HSL premium per a cada acció per donar un gran acabat gràfic al TFG
            Color selectionColor = Color.yellow;
            switch(selectedIndex)
            {
                case 0: selectionColor = new Color(1f, 0.2f, 0.2f, 0.9f); break; // Atacar (Vermell)
                case 1: selectionColor = new Color(1f, 0.9f, 0f, 0.9f);   break; // Raonar (Groc)
                case 2: selectionColor = new Color(0.7f, 0.3f, 1f, 0.9f); break; // Objectes (Lila)
                case 3: selectionColor = new Color(0.2f, 0.6f, 1f, 0.9f); break; // Fugir (Blau)
            }
            selectionGlowImage.color = selectionColor;
        }
    }

    private Color currentHighlightColor = Color.yellow;

    /// <summary>
    /// Corrutina de respiració ("breathing") de l'opacitat (Alpha) de l'aura
    /// i instanciació aleatòria de partícules de brillantor (sparkles) per sobre del botó.
    /// </summary>
    private IEnumerator PremiumGlowRoutine(RectTransform rt, Image glowImg)
    {
        while (true)
        {
            if (rt == null || glowImg == null) yield break;

            currentHighlightColor = glowImg.color;

            // Oscil·lació harmònica del canal Alpha (breathing glow)
            float t = (Mathf.Sin(Time.unscaledTime * 3f) + 1f) / 2f;
            Color c = glowImg.color;
            c.a = Mathf.Lerp(0.5f, 0.9f, t);
            glowImg.color = c;

            rt.localScale = Vector3.one;

            // Llançament de guspires (sparkles) de tant en tant
            if (rt.gameObject.activeInHierarchy && Random.value < 0.25f)
            {
                SpawnPremiumCircleParticle(rt);
            }
            
            yield return null;
        }
    }

    /// <summary>
    /// Genera una guspira a sobre del botó seleccionat. Neix a la vora superior 
    /// del panell i es desplaça lentament cap amunt.
    /// </summary>
    private void SpawnPremiumCircleParticle(RectTransform parent)
    {
        GameObject p = new GameObject("P_Sparkle");
        p.transform.SetParent(parent, false); 
        p.transform.position = parent.position; 
        
        RectTransform partRT = p.AddComponent<RectTransform>();
        
        float randX = Random.Range(-parent.rect.width / 2.2f, parent.rect.width / 2.2f);
        float topEdgeY = parent.rect.height / 2f; 
        partRT.anchoredPosition = new Vector2(randX, topEdgeY);

        float size = Random.Range(1.5f, 4f); 
        partRT.sizeDelta = new Vector2(size, size);
        
        Image img = p.AddComponent<Image>();
        img.sprite = softCircleSprite;
        img.raycastTarget = false;
        img.color = currentHighlightColor;
        
        StartCoroutine(AnimatePremiumParticle(partRT, img));
    }

    /// <summary>
    /// Corrutina de moviment de la guspira: flota en zig-zag (funció sinusoide) 
    /// cap amunt i va pampalluguejant ("twinkle") fins a esvair-se completament de la memòria.
    /// </summary>
    private IEnumerator AnimatePremiumParticle(RectTransform rt, Image img)
    {
        Vector2 vel = new Vector2(Random.Range(-1f, 1f), Random.Range(1f, 5f)); 
        float life = Random.Range(1.8f, 2.8f);
        float elapsed = 0f;
        float waveFreq = Random.Range(3f, 6f);
        float waveAmp = Random.Range(2f, 6f);
        
        while(elapsed < life)
        {
            if (rt == null || img == null) yield break;
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / life;
            
            Vector2 pos = rt.anchoredPosition;
            pos += vel * Time.unscaledDeltaTime;
            pos.x += Mathf.Sin(elapsed * waveFreq) * waveAmp * Time.unscaledDeltaTime;
            rt.anchoredPosition = pos;
            
            float twinkle = Mathf.Sin(elapsed * 15f) * 0.3f + 0.7f;
            Color c = img.color;
            c.a = (1f - t) * 0.7f * twinkle;
            img.color = c;
            
            rt.localScale = Vector3.one * (0.9f + Mathf.PingPong(elapsed * 1.5f, 0.3f));
            
            yield return null;
        }
        
        if (rt != null) Destroy(rt.gameObject);
    }

    private Coroutine playerHPAnim;
    private Coroutine enemyHPAnim;

    /// <summary>
    /// Actualitza la interfície de HP (barres de vida lliscants d'amortiment i textos informatius).
    /// </summary>
    private void UpdateStatsUI(bool instant = false)
    {
        if (playerHPText) playerHPText.text = $"HP {playerCurrentHP} / {playerMaxHP}";
        if (enemyHPText) enemyHPText.text = $"HP {enemyCurrentHP} / {enemyMaxHP}";

        float targetPlayerFill = (float)playerCurrentHP / playerMaxHP;
        if (playerHPFill) 
        {
            if (instant) playerHPFill.fillAmount = targetPlayerFill;
            else 
            {
                if (playerHPAnim != null) StopCoroutine(playerHPAnim);
                playerHPAnim = StartCoroutine(AnimateHPBar(playerHPFill, targetPlayerFill, 0.4f));
            }
        }

        float targetEnemyFill = (float)enemyCurrentHP / enemyMaxHP;
        if (enemyHPFill) 
        {
            if (instant) enemyHPFill.fillAmount = targetEnemyFill;
            else 
            {
                if (enemyHPAnim != null) StopCoroutine(enemyHPAnim);
                enemyHPAnim = StartCoroutine(AnimateHPBar(enemyHPFill, targetEnemyFill, 0.4f));
            }
        }
        
        if (currentPhase == MenuPhase.Target && fightButton != null)
        {
            SetButtonText(fightButton, $"Enemy ({enemyCurrentHP} HP)");
        }

        // Comprova si la reducció de vida de l'enemic ha desencadenat una transició de fase (boss phase)
        CheckPhaseShift(instant);
    }

    /// <summary>
    /// Comprova si el percentatge de vida de l'enemic és inferior a algun dels llindars (thresholds)
    /// definits a les seves fases del ScriptableObject. En cas afirmatiu, dispara el canvi de fase.
    /// </summary>
    private void CheckPhaseShift(bool immediate = false)
    {
        if (encounter == null || encounter.enemyProfile == null || encounter.enemyProfile.phases == null || encounter.enemyProfile.phases.Length == 0) return;

        float hpPercent = (float)enemyCurrentHP / enemyMaxHP * 100f;
        int bestPhase = -1;

        // Triem el llindar més limitant que compleixi la condició
        for (int i = 0; i < encounter.enemyProfile.phases.Length; i++)
        {
            if (hpPercent <= encounter.enemyProfile.phases[i].hpThresholdPercent)
            {
                bestPhase = i;
            }
        }

        if (bestPhase != currentPhaseIndex)
        {
            currentPhaseIndex = bestPhase;
            ApplyPhase(bestPhase, immediate);
        }
    }

    /// <summary>
    /// Activa la fase de boss seleccionada, canviant el sprite actiu de l'enemic,
    /// reproduint un efecte sonor i disparant els respectius diàlegs asíncrons.
    /// </summary>
    private void ApplyPhase(int index, bool immediate)
    {
        // Netegem qualsevol diàleg d'enemic que s'estigués escrivint a mitges per evitar barreges visuals
        if (activeEnemySpeakCoroutine != null)
        {
            StopCoroutine(activeEnemySpeakCoroutine);
            activeEnemySpeakCoroutine = null;
            if (enemyBubbleRT != null) enemyBubbleRT.gameObject.SetActive(false);
            isEnemySpeaking = false;
        }

        if (immediate)
        {
            SetPhaseVisuals(index);
            var phase = (index >= 0) ? encounter.enemyProfile.phases[index] : default;
            if (index >= 0 && phase.transitionDialogues != null && phase.transitionDialogues.Length > 0)
                isPhaseShiftingThisTurn = true;

            ShowPhaseDialogue(index);
        }
        else
        {
            SetPhaseVisuals(index);
            isPhaseShiftingThisTurn = true; // Bloquegem l'input immediatament
            StartCoroutine(ApplyPhaseDialogueRoutine(index));
        }
    }

    /// <summary> Assigna els valors de sprite i coreografies d'atac associades a la fase activa. </summary>
    private void SetPhaseVisuals(int index)
    {
        if (index < 0) 
        {
            currentPhaseAttacks = (encounter?.enemyProfile != null) ? encounter.enemyProfile.attackPatterns : null;
            return;
        }

        var phase = encounter.enemyProfile.phases[index];
        currentPhaseAttacks = phase.phaseAttacks;

        if (phase.transitionSound != null && audioSource != null)
            audioSource.PlayOneShot(phase.transitionSound);

        if (enemyPortraitImage != null && phase.phaseSprite != null)
        {
            enemyPortraitImage.sprite = phase.phaseSprite;
            enemyPortraitImage.enabled = true;
        }
    }

    /// <summary> Inicia l'escriptura dels diàlegs propis del canvi de fase de l'enemic. </summary>
    private void ShowPhaseDialogue(int index)
    {
        if (index < 0) return;
        var phase = encounter.enemyProfile.phases[index];
        
        if (phase.transitionDialogues != null && phase.transitionDialogues.Length > 0)
        {
            activeEnemySpeakCoroutine = StartCoroutine(ShowMultiplePhaseDialogues(phase.transitionDialogues));
        }
        else
        {
            isPhaseShiftingThisTurn = false;
        }
    }

    /// <summary> Recorre seqüencialment les línies de diàleg de la fase, esperant el retorn de cadascuna. </summary>
    private IEnumerator ShowMultiplePhaseDialogues(PhaseDialogueLine[] lines)
    {
        foreach (var line in lines)
        {
            yield return EnemySpeakRoutine(line.message, line.typingSpeedMultiplier, line.shakeText);
        }
    }

    /// <summary>
    /// Rutina asíncrona de fase: espera que el batut de dany finalitzi i reprodueix els diàlegs de boss.
    /// Si la fase de destí finalitza pacíficament (endFightFriendly), inicia un fade-out musical i acaba el combat.
    /// </summary>
    private IEnumerator ApplyPhaseDialogueRoutine(int index)
    {
        yield return new WaitForSeconds(1.35f);

        if (index >= 0)
        {
            var phase = encounter.enemyProfile.phases[index];
            if (phase.endFightFriendly && loader != null)
                StartCoroutine(loader.FadeCombatMusic(false, 4f));
        }
        
        ShowPhaseDialogue(index);
        
        while (isEnemySpeaking) yield return null;
        yield return null;

        if (index >= 0)
        {
            var phase = encounter.enemyProfile.phases[index];
            if (phase.endFightFriendly)
            {
                StartCoroutine(FriendlyVictoryRoutine());
                yield break;
            }
        }

        isPhaseShiftingThisTurn = false;
    }

    /// <summary>
    /// Gestiona el final amistós o pacífic de la batalla (resolució per diàleg/negociació).
    /// Incrementa els valors de reclutament de la criatura, atura la música, llança un efecte visual
    /// de lliscament (slide out) de tota la UI de combat i presenta el VictoryPanel procedimental.
    /// </summary>
    private IEnumerator FriendlyVictoryRoutine()
    {
        if (loader != null) StartCoroutine(loader.FadeCombatMusic(false, 1.5f));
        
        if (victorySound != null && audioSource != null) audioSource.PlayOneShot(victorySound);

        yield return new WaitForSeconds(0.5f);

        ShowTurnMenu(false);

        if (PlayerInventory.Instance != null && encounter?.enemyProfile != null)
        {
            PlayerInventory.Instance.RecruitEnemy(encounter.enemyProfile.enemyName);
        }

        float outDur = 0.5f;
        Vector2 outOff = new Vector2(0, 400f);
        if (playerUIPanel != null) StartCoroutine(SlideOutRect(playerUIPanel, playerUIOriginalPos, outOff, outDur));
        if (enemyUIPanel != null) StartCoroutine(SlideOutRect(enemyUIPanel, enemyUIOriginalPos, outOff, outDur));
        if (playerNameText != null) StartCoroutine(SlideOutRect(playerNameText.rectTransform, playerNameOriginalPos, outOff, outDur));
        if (playerHPText != null) StartCoroutine(SlideOutRect(playerHPText.rectTransform, playerHPTextOriginalPos, outOff, outDur));
        if (enemyNameText != null) StartCoroutine(SlideOutRect(enemyNameText.rectTransform, enemyNameOriginalPos, outOff, outDur));
        if (enemyHPText != null) StartCoroutine(SlideOutRect(enemyHPText.rectTransform, enemyHPTextOriginalPos, outOff, outDur));

        Transform canvasParent = turnMenu != null ? turnMenu.transform.parent : transform;
        bool done = false;
        VictoryPanelUI.Create(canvasParent, 0, new System.Collections.Generic.List<string>(), 
                               PlayerInventory.Instance != null ? PlayerInventory.Instance.Gold : 0, () => done = true);

        yield return new WaitUntil(() => done);
        loader.EndCombat();
    }

    /// <summary> Animació d'amortiment suau (Cúbica EaseOut) de la variació de vida de les barres. </summary>
    private IEnumerator AnimateHPBar(Image hpImage, float targetFill, float duration)
    {
        if (hpImage == null) yield break;
        
        float startFill = hpImage.fillAmount;
        float time = 0f;
        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;
            float easeT = 1f - Mathf.Pow(1f - t, 3f);
            hpImage.fillAmount = Mathf.Lerp(startFill, targetFill, easeT);
            yield return null;
        }
        hpImage.fillAmount = targetFill;
    }

    /// <summary> Truc de depuració o pocions que regenera completament la vida del jugador. </summary>
    public void DebugHealPlayerToMax()
    {
        playerCurrentHP = playerMaxHP;
        UpdateStatsUI();
        if (PlayerInventory.Instance != null)
            PlayerInventory.Instance.SetHP(playerCurrentHP);
    }

    /// <summary>
    /// Resta vida al jugador considerant els perfils de defensa o resistència del jugador (TFG).
    /// Si els punts de vida són menors o iguals a zero, dispara la corrutina de Game Over de debò.
    /// </summary>
    public void PlayerTakeDamage(int damage)
    {
        if (state == State.End) return;

        int finalDamage = damage;

        // Comprovem si el jugador posseeix bonificacions permanents de defensa degut a enemics reclutats
        if (PlayerInventory.Instance != null)
        {
            float defBonus = PlayerInventory.Instance.GetTotalDefenseBonus();
            if (defBonus > 0f)
            {
                int reduction = Mathf.RoundToInt(finalDamage * (defBonus / 100f));
                finalDamage = Mathf.Max(1, finalDamage - reduction);
            }
        }

        playerCurrentHP -= finalDamage;
        UpdateStatsUI();

        if (takeDamageSound) audioSource.PlayOneShot(takeDamageSound);

        if (PlayerInventory.Instance != null)
            PlayerInventory.Instance.SetHP(playerCurrentHP);

        // CONDICIÓ DE DERROTA
        if (playerCurrentHP <= 0)
        {
            playerCurrentHP = 0;
            state = State.End;
            SetHandsActive(false); // Atura moviments de mans immediatament
            
            // Destrucció preventiva de projectils que quedin flotant per l'arena
            var activeProjs = FindObjectsByType<ProjectileUI>(FindObjectsSortMode.None);
            foreach(var p in activeProjs) 
            {
                if (p != null) Destroy(p.gameObject);
            }
            ProjectileUI.activeProjectiles = 0;

            StartCoroutine(GameOverRoutine());
        }
    }

    /// <summary>
    /// Corrutina de Game Over Premium. Esvaeix la pantalla a negre sòlid, reprodueix música
    /// orquestral i melancòlica de Game Over, invoca un diàleg personalitzat amb veu,
    /// adapta les dimensions del text a format massiu (150px) i esborra el progrés persistent
    /// de la partida abans de redirigir l'usuari cap al menú principal del TFG.
    /// </summary>
    private IEnumerator GameOverRoutine()
    {
        GameObject blackScreenGO = new GameObject("GameOverScreen");
        Canvas c = blackScreenGO.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 9999;
        
        UnityEngine.UI.Image img = blackScreenGO.AddComponent<UnityEngine.UI.Image>();
        img.color = new Color(0, 0, 0, 0f); 

        loader.StartCoroutine(loader.FadeCombatMusic(false, 0f));
        if (audioSource) audioSource.Stop();
        if (loopAudioSource) loopAudioSource.Stop();
        if (voiceAudioSource) voiceAudioSource.Stop();

        img.color = Color.black;

        yield return new WaitForSecondsRealtime(1.5f);

        if (gameOverMusic != null)
        {
            audioSource.clip = gameOverMusic;
            audioSource.loop = true;
            audioSource.volume = 0f;
            audioSource.Play();

            float musicFadeDuration = 3f;
            float goMusicFadeElapsed = 0f;
            while (goMusicFadeElapsed < musicFadeDuration)
            {
                goMusicFadeElapsed += Time.unscaledDeltaTime;
                audioSource.volume = Mathf.Lerp(0f, 1f, goMusicFadeElapsed / musicFadeDuration);
                yield return null;
            }
            audioSource.volume = 1f;
        }
        else
        {
            yield return new WaitForSecondsRealtime(2f);
        }

        // Recuperem el component de diàleg de l'escena
        DialogueUI dialogueUI = null;
        DialogueUI[] allDUI = FindObjectsByType<DialogueUI>(FindObjectsSortMode.None);
        foreach (var ui in allDUI)
        {
            if (ui != null && ui.gameObject.scene == gameObject.scene)
            {
                dialogueUI = ui;
                break;
            }
        }
        if (dialogueUI == null)
        {
            var go = new GameObject("DialogueManager");
            dialogueUI = go.AddComponent<DialogueUI>();
        }

        Interactable.DialogueLine gameOverLine = new Interactable.DialogueLine();
        gameOverLine.text = string.IsNullOrEmpty(gameOverText) ? "GAME OVER" : gameOverText;
        gameOverLine.customVoiceSound = gameOverVoice;

        dialogueUI.SetSpeedMultiplier(0.15f); // Text super lent pel drama

        dialogueUI.StartDialogue(new Interactable.DialogueLine[] { gameOverLine }, false);

        GameObject dialogPanel = GameObject.Find("DynamicDialoguePanel");
        if (dialogPanel != null)
        {
            Canvas panelCanvas = dialogPanel.GetComponent<Canvas>();
            if (panelCanvas == null) panelCanvas = dialogPanel.AddComponent<Canvas>();
            panelCanvas.overrideSorting = true;
            panelCanvas.sortingOrder = 10000;

            UnityEngine.UI.Image bgImg = dialogPanel.GetComponent<UnityEngine.UI.Image>();
            if (bgImg != null) bgImg.enabled = false;
            
            UnityEngine.UI.Outline bgOl = dialogPanel.GetComponent<UnityEngine.UI.Outline>();
            if (bgOl != null) bgOl.enabled = false;

            RectTransform prt = dialogPanel.GetComponent<RectTransform>();
            if (prt != null)
            {
                prt.anchorMin = new Vector2(0.1f, 0.1f);
                prt.anchorMax = new Vector2(0.9f, 0.9f);
            }

            Transform txtBox = dialogPanel.transform.Find("TextBox");
            if (txtBox != null)
            {
                TMPro.TextMeshProUGUI tmp = txtBox.GetComponent<TMPro.TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.alignment = TMPro.TextAlignmentOptions.Top;
                    tmp.fontSizeMax = 150f;

                    string typedSoFar = tmp.text;
                    tmp.enableAutoSizing = true;
                    tmp.text = gameOverLine.text;
                    tmp.ForceMeshUpdate();
                    tmp.enableAutoSizing = false;
                    tmp.text = typedSoFar;
                }
            }
            
            Transform eBtn = dialogPanel.transform.Find("E_Button");
            if (eBtn != null)
            {
                eBtn.SetParent(blackScreenGO.transform, false);
                RectTransform ert = eBtn.GetComponent<RectTransform>();
                if (ert != null)
                {
                    ert.anchorMin = new Vector2(1f, 0f);
                    ert.anchorMax = new Vector2(1f, 0f);
                    ert.anchoredPosition = new Vector2(-50f, 50f);
                }
            }
        }

        while (dialogueUI.IsTyping)
        {
            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return))
            {
                dialogueUI.AdvanceOrSkip();
            }
            yield return null;
        }

        yield return null; 

        while (!Input.GetKeyDown(KeyCode.E) && !Input.GetKeyDown(KeyCode.Return))
        {
            yield return null;
        }

        // FADE OUT final abans de tornar al menú principal del TFG
        GameObject fadeOutGO = new GameObject("FadeOutScreen");
        Canvas fadeCanvas = fadeOutGO.AddComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = 10005; 
        UnityEngine.UI.Image fadeImg = fadeOutGO.AddComponent<UnityEngine.UI.Image>();
        fadeImg.color = new Color(0, 0, 0, 0f);
        
        float fadeTime = 0f;
        float fadeDuration = 1.0f;
        float startVolume = (audioSource != null) ? audioSource.volume : 0f;

        while(fadeTime < fadeDuration)
        {
            fadeTime += Time.unscaledDeltaTime;
            fadeImg.color = new Color(0, 0, 0, fadeTime / fadeDuration);
            if (audioSource != null && startVolume > 0)
            {
                audioSource.volume = Mathf.Lerp(startVolume, 0f, fadeTime / fadeDuration);
            }
            yield return null;
        }

        // Purga d'inventari per no heretar punts de vida o estat de mort a la propera partida
        if (PlayerInventory.Instance != null)
        {
            Destroy(PlayerInventory.Instance.gameObject);
        }
        
        CombatLoader.IsInCombat = false;
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }

    /// <summary> Reprodueix el so de bloqueig crític de tipus Parry. </summary>
    public void PlayParrySound()
    {
        if (parrySound && audioSource) audioSource.PlayOneShot(parrySound);
    }

    /// <summary> Reprodueix un efecte sonor qualsevol de forma local a l'arena. </summary>
    public void PlayLocalSound(AudioClip clip)
    {
        if (clip && audioSource) audioSource.PlayOneShot(clip);
    }

    /// <summary> Retorna l'efecte de so de recepció de dany. </summary>
    public AudioClip DamageSound => takeDamageSound;

    /// <summary> Reprodueix so d'explosió gràfica. </summary>
    public void PlayExplosionSound()
    {
        if (explosionSound && audioSource) audioSource.PlayOneShot(explosionSound);
    }

    /// <summary> Instancia un efecte visual de bloqueig d'escut (partícula parry). </summary>
    public void SpawnParryEffect(Vector3 position, Sprite projectileSprite = null)
    {
        if (parryParticlePrefab)
        {
            var effect = Instantiate(parryParticlePrefab, position, Quaternion.identity, transform);
            
            if (projectileSprite != null)
            {
                var img = effect.GetComponent<UnityEngine.UI.Image>();
                if (img) 
                {
                    img.sprite = projectileSprite;
                    img.color = new Color(0.2f, 1f, 0.2f, 1f); // Verd: en aquest joc fer parry ens cura!
                }
            }

            Destroy(effect, 3f);
        }
    }

    /// <summary>
    /// S'executa quan el jugador atura un projectil vermell amb èxit gràcies a l'escut.
    /// Recupera un punt de vida del jugador (cura) i llança efectes flotants ("+1").
    /// </summary>
    public void OnParrySuccess(Vector3 pos, Sprite projectileSprite)
    {
        PlayParrySound();
        SpawnParryEffect(pos, projectileSprite);

        playerCurrentHP++;
        if (playerCurrentHP > playerMaxHP) playerCurrentHP = playerMaxHP;
        UpdateStatsUI();

        Transform canvasParent = turnMenu != null ? turnMenu.transform.parent : transform;
        Image targetImg = playerPortraitImage != null ? playerPortraitImage : playerHPFill;
        HealFXUI.ShowAboveBar(canvasParent, targetImg, "+1", new Color(0.25f, 1f, 0.35f), 1f);
    }

    /// <summary> Retorna la línia de límit Y inferior on moren els projectils que cauen. </summary>
    public float GetDestroyLimitY()
    {
        return projectileDestroyLimit != null ? projectileDestroyLimit.anchoredPosition.y : -1200f;
    }

    // ─── LÒGICA I ACCIONS DEL JUGADOR (PLAYER ACTIONS) ───────────────
    /// <summary>
    /// Corrutina d'acció d'atac de tipus Fight: oculta el menú, instancia el minijoc de la ruleta,
    /// llegeix les bonificacions permanents de dany del jugador acumulades a l'inventari persistent,
    /// rep la puntuació calculada, aplica batut d'enemic i passa al torn de l'enemic.
    /// </summary>
    private IEnumerator PerformAttackRoutine()
    {
        state = State.Resolve;
        ShowTurnMenu(false);

        int finalDmg = 0;

        if (skillCheckPrefab != null && turnMenu != null)
        {
            SkillCheckUI skillCheck = Instantiate(skillCheckPrefab, turnMenu.transform.parent);
            skillCheck.SetAttackSound(attackSound);
            skillCheck.gameObject.SetActive(true); 
            skillCheck.transform.SetAsLastSibling(); 
            
            RectTransform rt = skillCheck.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = new Vector2(0, 150);
            }

            bool checkFinished = false;

            // Transmetem el multiplicador acumulat pel jugador basat en enemics reclutats
            if (PlayerInventory.Instance != null)
            {
                float atkBonus = PlayerInventory.Instance.GetTotalAttackBonus();
                skillCheck.SetDamageMultiplier(1f + (atkBonus / 100f));
            }

            skillCheck.StartSkillCheck((calcDmg) => 
            {
                finalDmg = calcDmg;
                checkFinished = true;
            });
            
            yield return new WaitUntil(() => checkFinished);
            Destroy(skillCheck.gameObject); 

            // Codi especial -1 indica cancel·lació del minijoc amb Escape/Backspace
            if (finalDmg == -1)
            {
                state = State.PlayerTurn;
                ShowTurnMenu(true);
                yield break;
            }
        }
        else 
        {
            finalDmg = Random.Range(5, 15);
            yield return new WaitForSeconds(1f);
        }

        Debug.Log($"FIGHT! Dealt {finalDmg} damage.");
        
        enemyCurrentHP -= finalDmg;
        if (enemyCurrentHP < 0) enemyCurrentHP = 0;
        UpdateStatsUI();

        if (enemyHitSound) audioSource.PlayOneShot(enemyHitSound);
        if (enemyPortraitImage != null) StartCoroutine(ShakeEnemySprite(enemyPortraitImage.rectTransform, 0.35f, 14f));

        yield return new WaitForSeconds(0.6f);

        // Si la vida de l'enemic és zero, el combat ha acabat!
        if (enemyCurrentHP == 0)
        {
            state = State.End;
            Debug.Log("ENEMY DEFEATED");
            StartCoroutine(DefeatAndVictoryRoutine());
            yield break;
        }

        // Canvis de nodes socials paral·lels per respostes mecàniques
        if (socialState != null)
        {
            var bt = encounter?.enemyProfile?.socialBT;
            if (bt != null)
            {
                var currentNode = bt.GetNode(socialState.currentNodeId);
                if (currentNode != null && !string.IsNullOrEmpty(currentNode.attackNextNodeId))
                {
                    socialState.MoveTo(currentNode.attackNextNodeId);
                    Debug.Log($"Attack triggered node transition to: {currentNode.attackNextNodeId}");
                }
            }
        }

        EndPlayerTurn("ATTACK");
    }

    /// <summary>
    /// Corrutina que tremola l'sprite de l'enemic per donar feedback orgànic d'impacte i dany rebut.
    /// </summary>
    private IEnumerator ShakeEnemySprite(RectTransform rt, float duration, float magnitude)
    {
        if (rt == null) yield break;
        Vector2 originalPos = rt.anchoredPosition;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float damping = 1f - Mathf.Clamp01(elapsed / duration);
            float x = Random.Range(-1f, 1f) * magnitude * damping;
            float y = Random.Range(-1f, 1f) * magnitude * damping;
            rt.anchoredPosition = originalPos + new Vector2(x, y);
            yield return null;
        }
        rt.anchoredPosition = originalPos;
    }

    /// <summary>
    /// Executa la seqüència de derrota completa de l'enemic. Llegeix diàlegs finals orals,
    /// llisca la UI cap a dalt, reprodueix el so de mort, i executa l'explosió procedural
    /// desintegrant la imatge de l'enemic en fragments de píxels.
    /// </summary>
    private IEnumerator DefeatAndVictoryRoutine()
    {
        if (loader != null) StartCoroutine(loader.FadeCombatMusic(false, 2.5f));

        yield return new WaitForSeconds(0.4f);

        // Diàlegs finals de mort orals
        if (encounter != null && encounter.enemyProfile != null && encounter.enemyProfile.deathReactions != null && encounter.enemyProfile.deathReactions.Length > 0)
        {
            string deathMsg = encounter.enemyProfile.deathReactions[Random.Range(0, encounter.enemyProfile.deathReactions.Length)];
            yield return EnemySpeakRoutine(deathMsg);
        }

        ShowTurnMenu(false);

        float outDur = 0.5f;
        Vector2 outOff = new Vector2(0, 400f);
        if (playerUIPanel != null) StartCoroutine(SlideOutRect(playerUIPanel, playerUIOriginalPos, outOff, outDur));
        if (enemyUIPanel != null) StartCoroutine(SlideOutRect(enemyUIPanel, enemyUIOriginalPos, outOff, outDur));
        if (playerNameText != null) StartCoroutine(SlideOutRect(playerNameText.rectTransform, playerNameOriginalPos, outOff, outDur));
        if (playerHPText != null) StartCoroutine(SlideOutRect(playerHPText.rectTransform, playerHPTextOriginalPos, outOff, outDur));
        if (enemyNameText != null) StartCoroutine(SlideOutRect(enemyNameText.rectTransform, enemyNameOriginalPos, outOff, outDur));
        if (enemyHPText != null) StartCoroutine(SlideOutRect(enemyHPText.rectTransform, enemyHPTextOriginalPos, outOff, outDur));

        AudioClip deathClip = encounter?.enemyProfile?.deathSound;
        if (deathClip) audioSource.PlayOneShot(deathClip);

        if (enemyPortraitImage != null && enemyPortraitImage.enabled)
        {
            bool fxDone = false;
            EnemyDestroyFX.Play(enemyPortraitImage, () => fxDone = true);
            yield return new WaitUntil(() => fxDone);
            yield return new WaitForSeconds(0.25f);
        }

        StartCoroutine(VictoryRoutine());
    }

    /// <summary>
    /// Corrutina de victòria (violenta). Calcula l'or guanyat de forma aleatòria
    /// basat en els valors mínim i màxim definits pel disseny, obté la llista de drops
    /// aleatoris de l'enemic, desa les dades en l'inventari persistent i llança el VictoryPanel.
    /// </summary>
    private IEnumerator VictoryRoutine()
    {
        ShowTurnMenu(false);
        if (loader != null) StartCoroutine(loader.FadeCombatMusic(false, 3f));

        if (victorySound) audioSource.PlayOneShot(victorySound);

        if (enemyHPText) enemyHPText.text = "";
        if (playerHPText) playerHPText.text = "";

        int gold = 0;
        System.Collections.Generic.List<string> earnedItems = new System.Collections.Generic.List<string>();

        if (encounter != null && encounter.enemyProfile != null)
        {
            var p = encounter.enemyProfile;
            gold = Random.Range(p.goldRewardMin, p.goldRewardMax + 1);
            earnedItems = CalculateDrops(p.drops);
        }
        else
        {
            gold = Random.Range(30, 80);
        }

        if (PlayerInventory.Instance != null)
        {
            if (encounter != null && encounter.enemyProfile != null)
            {
                PlayerInventory.Instance.KillEnemy(encounter.enemyProfile.enemyName);
            }

            PlayerInventory.Instance.SetHP(playerCurrentHP);
            PlayerInventory.Instance.AddGold(gold);
            foreach (var item in earnedItems)
            {
                if (!string.IsNullOrEmpty(item) && item != "none" && item != "—")
                    PlayerInventory.Instance.AddItem(item);
            }
        }

        int totalGold = PlayerInventory.Instance != null ? PlayerInventory.Instance.Gold : gold;

        Transform canvasParent = turnMenu != null ? turnMenu.transform.parent : transform;
        bool done = false;
        VictoryPanelUI.Create(canvasParent, gold, earnedItems, totalGold, () => done = true);

        yield return new WaitUntil(() => done);

        loader.EndCombat();
    }

    /// <summary>
    /// Acció de Raonar: llança el diàleg social or des de l'arbre si és disponible.
    /// En cas contrari, reprodueix diàlegs orals preventius de fallback per evitar softlocks gràfics.
    /// </summary>
    private void OnReason()
    {
        var bt = encounter?.enemyProfile?.socialBT;
        if (bt == null || bt.playerActions == null || bt.playerActions.Length == 0)
        {
            string fallback = (encounter?.enemyProfile != null) ? encounter.enemyProfile.reasonFallbackDialogue : "";
            if (string.IsNullOrEmpty(fallback)) fallback = "Tractes de parlar amb l'enemic, però no sembla haver-hi resposta.";
            
            StartCoroutine(ReasonFallbackRoutine(fallback));
            return;
        }

        if (socialState == null)
            socialState = new SocialBTState(bt.startNodeId);

        ShowTurnMenu(false);
        state = State.Resolve;
        StartCoroutine(SocialActionMenuRoutine(bt));
    }

    /// <summary> Rutina de fallback asíncrona per a l'acció de Raonar en cas de no tenir graf associat. </summary>
    private IEnumerator ReasonFallbackRoutine(string text)
    {
        ShowTurnMenu(false);
        state = State.Resolve;
        
        AudioClip voice = (encounter?.enemyProfile != null) ? encounter.enemyProfile.reasonFallbackSound : null;
        
        // Multiplicador 4.0f per mostrar la línia de diàleg de forma dramàtica/lenta
        yield return ShowPlayerActionDialogue(text, voice, 4.0f);
        EndPlayerTurn("");
    }

    // ─── GESTIÓ DEL DIÀLEG SOCIAL PROCEDURAL DEL TFG (NODE-GRAPH) ──────
    /// <summary>
    /// Corrutina del menú d'accions socials: genera de manera completament procedural a l'esquerra de la pantalla
    /// un panell retro vertical, col·loca els botons d'acció del graf del node actual, implementa navegació per teclat,
    /// valida si es tenen els objectes de l'inventari exigits com a precondició (mostrant vibracions d'error en vermell en cas negatiu),
    /// i finalment consumeix l'objecte de la transició abans de reproduir els diàlegs dels personatges.
    /// </summary>
    private IEnumerator SocialActionMenuRoutine(SocialBehaviorTree bt)
    {
        Transform canvasParent = turnMenu != null ? turnMenu.transform.parent : transform;
        TMPro.TextMeshProUGUI headerTxt = null;

        SocialNode currentNode = bt.GetNode(socialState.currentNodeId);
        if (currentNode != null && !string.IsNullOrEmpty(currentNode.enemyEntryText))
            yield return EnemySpeakRoutine(currentNode.enemyEntryText);

        System.Collections.Generic.List<string> displayedActions = new System.Collections.Generic.List<string>(bt.playerActions);
        if (currentNode != null && currentNode.enableApology)
        {
            displayedActions.Add("Apologize");
        }

        // Construcció procedural del panell esquerra
        socialMenuGO = new GameObject("SocialActionMenu");
        socialMenuGO.transform.SetParent(canvasParent, false);

        var panelRT = socialMenuGO.AddComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0f, 0.5f);
        panelRT.anchorMax = new Vector2(0f, 0.5f);
        panelRT.pivot = new Vector2(0f, 0.5f);
        panelRT.sizeDelta = new Vector2(400f, 700f);
        
        Vector2 finalPos = new Vector2(50f, 0f);
        panelRT.anchoredPosition = new Vector2(-500f, 0f); // Llançament des de l'esquerra

        var panelBg = socialMenuGO.AddComponent<Image>();
        panelBg.color = new Color(0.08f, 0.08f, 0.12f, 0.96f);
        panelBg.sprite = GetRoundedSprite();
        panelBg.type = Image.Type.Sliced;

        var outline = socialMenuGO.AddComponent<Outline>();
        outline.effectColor = new Color(0.95f, 0.8f, 0.15f, 0.6f);
        outline.effectDistance = new Vector2(5, -5);

        // Capçalera
        var headerGO = new GameObject("Header");
        headerGO.transform.SetParent(panelRT, false);
        var headerRT = headerGO.AddComponent<RectTransform>();
        headerRT.anchorMin = new Vector2(0f, 0.85f); headerRT.anchorMax = new Vector2(1f, 1f);
        headerRT.offsetMin = new Vector2(10, 0); headerRT.offsetMax = new Vector2(-10, 0);
        headerTxt = headerGO.AddComponent<TMPro.TextMeshProUGUI>();
        headerTxt.text = bt.menuHeader;
        headerTxt.fontSize = 36f;
        headerTxt.fontStyle = TMPro.FontStyles.Bold;
        headerTxt.alignment = TMPro.TextAlignmentOptions.Center;
        headerTxt.color = new Color(1f, 0.92f, 0.2f);
        if (playerNameText != null && playerNameText.font != null) headerTxt.font = playerNameText.font;

        // Contenidor per al llistat vertical de botons d'acció
        var buttonsContainer = new GameObject("ButtonsContainer");
        buttonsContainer.transform.SetParent(panelRT, false);
        var bcRT = buttonsContainer.AddComponent<RectTransform>();
        bcRT.anchorMin = new Vector2(0, 0); bcRT.anchorMax = new Vector2(1, 0.82f);
        bcRT.offsetMin = new Vector2(20, 20); bcRT.offsetMax = new Vector2(-20, 0);

        var vlg = buttonsContainer.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
        vlg.padding = new RectOffset(10, 10, 10, 10);
        vlg.spacing = 15;
        vlg.childControlHeight = true;
        vlg.childForceExpandHeight = false;

        int actionCount = displayedActions.Count;
        int chosenActionIndex = -1;
        Image[] btnImages = new Image[actionCount];

        for (int i = 0; i < actionCount; i++)
        {
            string actionName = displayedActions[i];
            int capturedI = i;

            var btnGO = new GameObject($"ActionBtn_{i}");
            btnGO.transform.SetParent(bcRT.transform, false);
            var le = btnGO.AddComponent<UnityEngine.UI.LayoutElement>();
            le.minHeight = 80f;

            var btnImg = btnGO.AddComponent<Image>();
            btnImg.sprite = GetRoundedSprite();
            btnImg.type = Image.Type.Sliced;
            
            if (actionName == "Apologize")
                btnImg.color = new Color(0.1f, 0.35f, 0.15f, 1f);
            else
                btnImg.color = new Color(0.18f, 0.18f, 0.32f, 1f);
            
            btnImages[i] = btnImg;

            var btnComp = btnGO.AddComponent<UnityEngine.UI.Button>();
            btnComp.targetGraphic = btnImg;

            var btnTxtGO = new GameObject("Label");
            btnTxtGO.transform.SetParent(btnGO.transform, false);
            var btnTxtRT = btnTxtGO.AddComponent<RectTransform>();
            btnTxtRT.anchorMin = Vector2.zero; btnTxtRT.anchorMax = Vector2.one;
            btnTxtRT.offsetMin = new Vector2(15, 0); btnTxtRT.offsetMax = new Vector2(-15, 0);
            var btnTxt = btnTxtGO.AddComponent<TMPro.TextMeshProUGUI>();
            btnTxt.text = actionName;
            btnTxt.fontSize = 28f;
            btnTxt.fontStyle = TMPro.FontStyles.Bold;
            btnTxt.alignment = TMPro.TextAlignmentOptions.Center;
            btnTxt.color = Color.white;
            if (playerNameText != null && playerNameText.font != null) btnTxt.font = playerNameText.font;

            btnComp.onClick.AddListener(() => { chosenActionIndex = capturedI; });
        }

        // Moviment de desplaçament d'entrada de la llista
        StartCoroutine(AnimateSideMenu(panelRT, finalPos, 0.5f));

        int keyboardIndex = 0;
        void UpdateHighlight()
        {
            for (int i = 0; i < actionCount; i++)
            {
                bool isSelected = (i == keyboardIndex);
                if (displayedActions[i] == "Apologize")
                {
                    btnImages[i].color = isSelected ? new Color(0.2f, 0.6f, 0.25f, 1f) : new Color(0.1f, 0.35f, 0.15f, 1f);
                }
                else
                {
                    btnImages[i].color = isSelected ? new Color(0.4f, 0.35f, 0.7f, 1f) : new Color(0.18f, 0.18f, 0.32f, 1f);
                }
            }
        }
        UpdateHighlight();

        while (chosenActionIndex < 0)
        {
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
            {
                keyboardIndex = (keyboardIndex - 1 + actionCount) % actionCount;
                if (moveMenuSound) audioSource.PlayOneShot(moveMenuSound);
                UpdateHighlight();
            }
            else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                keyboardIndex = (keyboardIndex + 1) % actionCount;
                if (moveMenuSound) audioSource.PlayOneShot(moveMenuSound);
                UpdateHighlight();
            }
            else if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return))
            {
                string actionName = displayedActions[keyboardIndex];
                var trans = bt.GetTransition(currentNode, actionName);
                
                // VALIDACIÓ DE L'OBJECTE REQUERIT PER L'ACCIÓ
                if (trans != null && trans.requiredItem != null)
                {
                    if (PlayerInventory.Instance == null || PlayerInventory.Instance.CountItem(trans.requiredItem.itemName) <= 0)
                    {
                        // Text de capçalera en vermell indicant error
                        headerTxt.text = $"No tens: {trans.requiredItem.itemName}!";
                        headerTxt.color = new Color(1f, 0.3f, 0.3f);
                        
                        // Vibració lateral d'avís
                        StartCoroutine(ShakeSideMenu(panelRT, finalPos));
                        
                        if (audioSource && takeDamageSound) audioSource.PlayOneShot(takeDamageSound, 0.5f);
                        
                        yield return new WaitForSeconds(1.2f);
                        
                        headerTxt.text = bt.menuHeader;
                        headerTxt.color = new Color(1f, 0.92f, 0.2f);
                        continue; 
                    }
                }
                chosenActionIndex = keyboardIndex;
            }
            else if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
            {
                // Sortida instantània amb lliscament
                yield return StartCoroutine(AnimateSideMenu(panelRT, new Vector2(-400f, 0f), 0.3f));
                Destroy(socialMenuGO);
                state = State.PlayerTurn;
                ShowTurnMenu(true);
                yield break;
            }
            yield return null;
        }

        yield return StartCoroutine(AnimateSideMenu(panelRT, new Vector2(-400f, 0f), 0.3f));
        Destroy(socialMenuGO);

        string chosen = displayedActions[chosenActionIndex];
        if (confirmMenuSound && audioSource) audioSource.PlayOneShot(confirmMenuSound);

        SocialTransition transition = bt.GetTransition(currentNode, chosen);

        // Descomptem preventivament l'objecte utilitzat de l'inventari persistent
        if (transition != null && transition.requiredItem != null)
        {
            if (PlayerInventory.Instance != null)
            {
                PlayerInventory.Instance.RemoveItem(transition.requiredItem.itemName);
                Debug.Log($"Objecte {transition.requiredItem.itemName} restat de l'inventari per acció social.");
            }
        }

        string reactionText;
        string nextNodeId;

        if (transition != null)
        {
            reactionText = transition.enemyReactionText;
            nextNodeId   = transition.nextNodeId;
        }
        else
        {
            reactionText = currentNode != null ? currentNode.defaultReactionText : "...";
            nextNodeId   = "";
        }

        // 1. Narra a la part baixa l'acció descriptiva del jugador (ShowPlayerActionDialogue)
        string playerText = transition != null ? transition.playerActionText : null;
        if (!string.IsNullOrEmpty(playerText))
        {
            yield return StartCoroutine(ShowPlayerActionDialogue(playerText));
        }

        // 2. Dispara la resposta parlada o reacció de l'enemic a la bombolla del seu retrat
        if (!string.IsNullOrEmpty(reactionText))
            yield return EnemySpeakRoutine(reactionText);

        socialState.MoveTo(nextNodeId);

        // Si hem aconseguit entablar amistat permanent
        if (socialState.IsFriend)
        {
            state = State.End;
            StartCoroutine(FriendVictoryRoutine());
        }
        else
        {
            EndPlayerTurn("");
        }
    }

    /// <summary>
    /// Corrutina de desplaçament suau d'un menú lateral per posició Lerp.
    /// </summary>
    private IEnumerator AnimateSideMenu(RectTransform rect, Vector2 targetPos, float duration)
    {
        Vector2 startPos = rect.anchoredPosition;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float ease = 1f - Mathf.Pow(1f - t, 3f);
            rect.anchoredPosition = Vector2.Lerp(startPos, targetPos, ease);
            yield return null;
        }
        rect.anchoredPosition = targetPos;
    }

    /// <summary>
    /// Corrutina que fa vibrar lateralment un RectTransform per aportar feedback de retroacció d'errors.
    /// </summary>
    private IEnumerator ShakeSideMenu(RectTransform rect, Vector2 basePos)
    {
        float elapsed = 0f;
        float duration = 0.25f;
        float intensity = 10f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledTime;
            float offX = Random.Range(-intensity, intensity);
            float offY = Random.Range(-intensity, intensity);
            rect.anchoredPosition = basePos + new Vector2(offX, offY);
            yield return null;
        }
        rect.anchoredPosition = basePos;
    }

    /// <summary>
    /// Gestiona la victòria pacífica en finalitzar l'arbre social.
    /// </summary>
    private IEnumerator FriendVictoryRoutine()
    {
        ShowTurnMenu(false);

        var bt = encounter?.enemyProfile?.socialBT;
        if (bt != null && !string.IsNullOrEmpty(bt.friendshipText))
        {
            yield return EnemySpeakRoutine(bt.friendshipText);
        }

        float outDur = 0.5f;
        Vector2 outOff = new Vector2(0, 400f);
        if (playerUIPanel != null)   StartCoroutine(SlideOutRect(playerUIPanel, playerUIOriginalPos, outOff, outDur));
        if (enemyUIPanel != null)    StartCoroutine(SlideOutRect(enemyUIPanel, enemyUIOriginalPos, outOff, outDur));
        if (playerNameText != null)  StartCoroutine(SlideOutRect(playerNameText.rectTransform, playerNameOriginalPos, outOff, outDur));
        if (playerHPText != null)    StartCoroutine(SlideOutRect(playerHPText.rectTransform, playerHPTextOriginalPos, outOff, outDur));
        if (enemyNameText != null)   StartCoroutine(SlideOutRect(enemyNameText.rectTransform, enemyNameOriginalPos, outOff, outDur));
        if (enemyHPText != null)     StartCoroutine(SlideOutRect(enemyHPText.rectTransform, enemyHPTextOriginalPos, outOff, outDur));

        if (victorySound) audioSource.PlayOneShot(victorySound);

        yield return new WaitForSeconds(outDur + 0.3f);

        int friendGold = 0;
        System.Collections.Generic.List<string> earnedItems = new System.Collections.Generic.List<string>();
        
        if (encounter?.enemyProfile != null)
        {
            var p = encounter.enemyProfile;
            if (p.amicableGoldRewardMax > 0)
            {
                friendGold = Random.Range(p.amicableGoldRewardMin, p.amicableGoldRewardMax + 1);
            }
            else if (bt != null && bt.friendGoldMax > 0)
            {
                friendGold = Random.Range(bt.friendGoldMin, bt.friendGoldMax + 1);
            }
            
            earnedItems = CalculateDrops(p.amicableDrops);
        }

        if (PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.AddGold(friendGold);
            foreach (var item in earnedItems)
            {
                if (!string.IsNullOrEmpty(item) && item != "none" && item != "—")
                    PlayerInventory.Instance.AddItem(item);
            }
        }

        if (PlayerInventory.Instance != null && encounter?.enemyProfile != null)
        {
            PlayerInventory.Instance.RecruitEnemy(encounter.enemyProfile.enemyName);
        }

        Transform canvasParent = turnMenu != null ? turnMenu.transform.parent : transform;
        bool done = false;
        VictoryPanelUI.Create(canvasParent, friendGold, earnedItems,
            PlayerInventory.Instance != null ? PlayerInventory.Instance.Gold : friendGold, () => done = true);

        yield return new WaitUntil(() => done);

        // Control de reclutament finalitzat per a bonificacions permanents
        if (PlayerInventory.Instance != null && encounter?.enemyProfile != null)
        {
            var completedProfile = PlayerInventory.Instance.CheckRecruitmentJustCompleted(encounter.enemyProfile.enemyName);
            if (completedProfile != null)
            {
                Debug.Log($"[RECRUIT] Recruitment reward triggered for {encounter.enemyProfile.enemyName}!");
                pendingRecruitReward = completedProfile;
            }
            else
            {
                Debug.Log($"[RECRUIT] Recruitment not completed yet for {encounter.enemyProfile.enemyName} (Count: {PlayerInventory.Instance.GetRecruitedCount(encounter.enemyProfile.enemyName)})");
            }
        }

        // Desem la vida exacta actual a l'inventari persistent abans de sortir del combat
        if (PlayerInventory.Instance != null)
            PlayerInventory.Instance.SetHP(playerCurrentHP);

        loader.EndCombat();
    }

    /// <summary> Obre les accions associades a intentar escapar del combat. </summary>
    private void OnFlee()
    {
        ShowTurnMenu(false);
        state = State.Resolve; 
        StartCoroutine(FleeRoutine());
    }

    /// <summary>
    /// Corrutina de fugida: calcula dinàmicament la probabilitat d'escapar.
    /// En cas d'èxit finalitza el combat immediatament; en cas contrari narrar l'error i perdem el torn.
    /// </summary>
    private IEnumerator FleeRoutine()
    {
        float fleeChance = 0.5f;
        if (encounter != null && encounter.enemyProfile != null)
        {
            fleeChance = encounter.enemyProfile.fleeProbability;
        }

        if (Random.value <= fleeChance)
        {
            yield return ShowPlayerActionDialogue("You try to run away... and you make it!");
            state = State.End;
            // Desem la vida exacta actual a l'inventari persistent abans de sortir del combat
            if (PlayerInventory.Instance != null)
                PlayerInventory.Instance.SetHP(playerCurrentHP);
            loader.EndCombat();
        }
        else
        {
            yield return ShowPlayerActionDialogue("You try to run away... but you can't escape!");
            EndPlayerTurn("FLEE_FAIL");
        }
    }

    /// <summary>
    /// Acció d'inventari: obre el menú general d'inventari en format combat,
    /// intercepta l'objecte triat i executa els seus efectes interactius a la batalla.
    /// </summary>
    private void OnItem()
    {
        ShowTurnMenu(false);
        state = State.Resolve;
        InventoryMenuUI.Show(isCombat: true, onItemSelected: (profile) =>
        {
            StartCoroutine(ProcessItemSequence(profile));
        },
        onClose: () =>
        {
            state = State.PlayerTurn;
            ShowTurnMenu(true);
        });
    }

    /// <summary> Corrutina seqüencial que resol l'ús d'un objecte i canvia al torn de l'enemic. </summary>
    private IEnumerator ProcessItemSequence(ItemProfile profile)
    {
        yield return StartCoroutine(ApplyItemEffect(profile));
        
        if (enemyCurrentHP <= 0)
        {
            state = State.End;
            StartCoroutine(DefeatAndVictoryRoutine());
        }
        else
        {
            string reaction = (profile.effectType == ItemEffectType.DamageEnemy) ? "ATTACK" : "HEAL";
            EndPlayerTurn(reaction);
        }
    }

    /// <summary>
    /// Aplica els efectes directes dels objectes en el combat: pocions de curació (HealPlayer) amb fullscreen pop,
    /// ampolles de dany (DamageEnemy) disparant trajectòries parabòliques, o pocions de boost de velocitat (SpeedUpHands).
    /// </summary>
    private IEnumerator ApplyItemEffect(ItemProfile profile)
    {
        Debug.Log($"Utilitzant objecte en combat: {profile.itemName}");
        Transform canvasParent = turnMenu != null ? turnMenu.transform.parent : transform;

        if (profile.effectType == ItemEffectType.HealPlayer)
        {
            if (profile.useSound != null) audioSource.PlayOneShot(profile.useSound);
            if (profile.additionalUseSounds != null)
            {
                foreach (var clip in profile.additionalUseSounds)
                {
                    if (clip != null) audioSource.PlayOneShot(clip);
                }
            }
            playerCurrentHP += profile.effectValue;
            if (playerCurrentHP > playerMaxHP) playerCurrentHP = playerMaxHP;

            Image targetImg = playerPortraitImage != null ? playerPortraitImage : playerHPFill;
            HealFXUI.ShowAboveBar(canvasParent, targetImg, $"+{profile.effectValue} HP",
                                  new Color(0.25f, 1f, 0.35f));

            HealFXUI.ShowHealFullscreen(canvasParent);
        }
        else if (profile.effectType == ItemEffectType.DamageEnemy)
        {
            // Llançament parabòlic de l'objecte de dany
            yield return StartCoroutine(AnimateItemThrow(profile, () => {
                // EXECUTAT AL MOMENT EXACTE DE L'IMPACTE DE LA MONEDA/OBJECTE
                enemyCurrentHP -= profile.effectValue;
                if (enemyCurrentHP < 0) enemyCurrentHP = 0;
                UpdateStatsUI();

                if (enemyHitSound != null) audioSource.PlayOneShot(enemyHitSound);
                if (enemyPortraitImage != null) StartCoroutine(ShakeEnemySprite(enemyPortraitImage.rectTransform, 0.35f, 14f));

                HealFXUI.ShowAboveBar(canvasParent, enemyHPFill, $"-{profile.effectValue} HP",
                                      new Color(1f, 0.45f, 0.1f));
            }));
        }
        else if (profile.effectType == ItemEffectType.SpeedUpHands)
        {
            if (profile.useSound != null) audioSource.PlayOneShot(profile.useSound);
            if (profile.additionalUseSounds != null)
            {
                foreach (var clip in profile.additionalUseSounds)
                {
                    if (clip != null) audioSource.PlayOneShot(clip);
                }
            }
            var hands = FindObjectsByType<HandController>(FindObjectsSortMode.None);
            
            if (speedBuffRoundsLeft <= 0)
            {
                currentSpeedBuffValue = (profile.effectValue / 100f);
                foreach (var h in hands) h.speedMultiplier += currentSpeedBuffValue;
            }
            
            speedBuffRoundsLeft = profile.buffDurationRounds;
            HealFXUI.Show(canvasParent, $"SPEED +{profile.effectValue}% ({speedBuffRoundsLeft} TURNS)", new Color(1f, 0.9f, 0.15f));
            
            HealFXUI.ShowSpeedFullscreen(canvasParent);
        }
        UpdateStatsUI();
        yield return null;
    }

    /// <summary>
    /// Corrutina premium que anima la trajectòria en 2D d'un objecte llançat.
    /// Realitza un moviment Lerp acompanyat d'una paràbola en Y (funció quadràtica),
    /// rota l'objecte ràpidament durant el vol, crida el callback d'impacte,
    /// i simula la caiguda física fins a terra acabant amb petits rebots elàstics de gravetat.
    /// </summary>
    private IEnumerator AnimateItemThrow(ItemProfile profile, System.Action onImpact)
    {
        Transform canvasParent = turnMenu != null ? turnMenu.transform.parent : transform;
        
        GameObject go = new GameObject("ThrownItem");
        go.transform.SetParent(canvasParent, false);
        Image img = go.AddComponent<Image>();
        img.sprite = profile.itemIcon;
        img.preserveAspect = true;
        img.raycastTarget = false;
        
        RectTransform rt = go.GetComponent<RectTransform>();

        Vector2 startPos = throwStartPoint != null ? throwStartPoint.anchoredPosition : new Vector2(0, -700f); 

        Vector2 impactPos;
        if (enemyPortraitImage != null) impactPos = enemyPortraitImage.rectTransform.anchoredPosition;
        else if (enemyUIPanel != null) impactPos = enemyUIPanel.anchoredPosition;
        else impactPos = new Vector2(0, 300f);

        float groundY = itemGroundLine != null ? itemGroundLine.anchoredPosition.y : impactPos.y - 150f;
        Vector2 groundPos = new Vector2(impactPos.x + Random.Range(-100f, 100f), groundY);

        rt.anchoredPosition = startPos;
        float startScale = 3.5f;
        float endScale = 0.8f;
        rt.localScale = Vector3.one * startScale;

        if (profile.useSound != null) audioSource.PlayOneShot(profile.useSound);
        if (profile.additionalUseSounds != null)
        {
            foreach (var clip in profile.additionalUseSounds)
            {
                if (clip != null) audioSource.PlayOneShot(clip);
            }
        }

        // ── FASE 1: VOL EN PARÀBOLA ──
        float duration = 0.5f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            Vector2 currentPos = Vector2.Lerp(startPos, impactPos, t);
            float parabola = 4f * t * (1f - t); // Paràbola invertida
            currentPos.y += parabola * throwArcHeight;
            rt.anchoredPosition = currentPos;
            rt.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, t);
            rt.Rotate(0, 0, -800f * Time.deltaTime);
            yield return null;
        }

        // IMPACTE DEL CÀLCUL DE DANY
        onImpact?.Invoke();

        // ── FASE 2: CAIGUDA DESPRÉS DE COL·LIDIR ──
        elapsed = 0f;
        float fallDuration = 0.3f;
        Vector2 posAtImpact = rt.anchoredPosition;
        while (elapsed < fallDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fallDuration;
            rt.anchoredPosition = Vector2.Lerp(posAtImpact, groundPos, t * t);
            rt.Rotate(0, 0, -200f * Time.deltaTime);
            yield return null;
        }

        // Rebot elàstic en xocar a terra
        float bounce = 0f;
        while(bounce < 0.2f)
        {
            bounce += Time.deltaTime;
            float b = Mathf.Abs(Mathf.Sin(bounce * Mathf.PI / 0.2f)) * 15f;
            rt.anchoredPosition = groundPos + new Vector2(0, b);
            yield return null;
        }
        rt.anchoredPosition = groundPos;

        yield return new WaitForSeconds(1f);
        
        // Desaparició suau (fade out) de l'objecte de la pantalla
        float fade = 1f;
        while(fade > 0f)
        {
            fade -= Time.deltaTime * 2f;
            img.color = new Color(1,1,1,fade);
            yield return null;
        }
        Destroy(go);
    }

    /// <summary>
    /// Mostra un diàleg a la part inferior en format overworld de combat,
    /// ideal per narrar accions. Suporta avançar lletra a lletra asíncronament.
    /// </summary>
    private IEnumerator ShowPlayerActionDialogue(string text, AudioClip overrideVoice = null, float speedMultiplier = 1f)
    {
        DialogueUI dialogUI = null;
        DialogueUI[] allDUI = FindObjectsByType<DialogueUI>(FindObjectsSortMode.None);
        foreach (var ui in allDUI)
        {
            if (ui != null && ui.gameObject.scene == gameObject.scene)
            {
                dialogUI = ui;
                break;
            }
        }
        GameObject dialogGO = null;

        if (dialogUI == null)
        {
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) { yield break; }

            dialogGO = new GameObject("CombatDialogueUI");
            dialogGO.transform.SetParent(canvas.transform, false);
            dialogUI = dialogGO.AddComponent<DialogueUI>();
        }
        
        dialogUI.canSkip = false;
        
        if (speedMultiplier != 1f)
        {
            dialogUI.SetSpeedMultiplier(speedMultiplier); 
        }

        AudioClip voice = (overrideVoice != null) ? overrideVoice : playerActionVoice;
        if (voice != null)
        {
            dialogUI.SetTypingSound(voice);
        }

        bool closed = false;
        dialogUI.OnDialogueClosed += () => closed = true;
        dialogUI.Show(text);

        yield return null;

        while (!closed)
        {
            if (!PauseMenuUI.IsOpen && (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)))
            {
                dialogUI.AdvanceOrSkip();
            }
            yield return null;
        }

        yield return new WaitForSeconds(0.15f);

        if (dialogGO != null) Destroy(dialogGO);
    }

    /// <summary>
    /// Corrutina de Typewriter or de la bombolla del rival: escriu lletra a lletra el text,
    /// reprodueix sons de veu asíncrons cada dos caràcters per evitar solapaments sonors,
    /// suporta retards especials segons signes de puntuació (. , ! ?),
    /// permet al jugador saltar (skip) completament l'escriptura clicant E/Intro,
    /// i finalment mostra un prompt inferior abans de tancar.
    /// </summary>
    private IEnumerator EnemySpeakRoutine(string text, float speedMultiplier = 1f, bool shake = false)
    {
        if (enemyBubbleRT == null || enemyDialogTxt == null || string.IsNullOrEmpty(text)) yield break;

        string cleanText = text.Trim();
        isEnemySpeaking = true;
        enemyBubbleRT.gameObject.SetActive(true);
        enemyDialogTxt.text = "";
        if (enemyBubblePromptCG != null) enemyBubblePromptCG.alpha = 0f;

        AudioClip voice = encounter?.enemyProfile?.voiceSound;
        
        float charsPerSecond = 45f;
        float delay = (1f / charsPerSecond) * speedMultiplier;

        yield return null;

        for (int i = 0; i < cleanText.Length; i++)
        {
            char c = cleanText[i];
            if (enemyDialogTxt != null) enemyDialogTxt.text += c;
            
            // So de veu seqüencial
            if (!char.IsWhiteSpace(c) && voice != null && voiceAudioSource != null)
            {
                if (i % 2 == 0)
                {
                    voiceAudioSource.pitch = 1f + Random.Range(-0.06f, 0.06f);
                    voiceAudioSource.PlayOneShot(voice, 0.8f); 
                }
            }
            
            float currentDelay = delay;
            if (c == '.' || c == '?' || c == '!') currentDelay = delay * 10f;
            else if (c == ',' || c == ';' || c == ':') currentDelay = delay * 5f;

            float elapsed = 0f;
            bool skipped = false;
            while (elapsed < currentDelay)
            {
                elapsed += Time.unscaledDeltaTime;
                if (!PauseMenuUI.IsOpen && (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)))
                {
                    skipped = true;
                    break;
                }
                yield return null;
            }

            if (skipped)
            {
                enemyDialogTxt.text = cleanText;
                break;
            }
        }

        if (voiceAudioSource != null) voiceAudioSource.pitch = 1f;

        if (enemyBubblePromptCG != null) enemyBubblePromptCG.alpha = 1f;

        bool advance = false;
        yield return new WaitForSeconds(0.15f);
        while (!advance)
        {
            if (!PauseMenuUI.IsOpen && (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)))
            {
                advance = true;
                if (confirmMenuSound && audioSource) audioSource.PlayOneShot(confirmMenuSound);
            }
            yield return null;
        }

        if (enemyBubblePromptCG != null) enemyBubblePromptCG.alpha = 0f;
        enemyBubbleRT.gameObject.SetActive(false);
        isEnemySpeaking = false;
    }

    /// <summary> Tanca el torn del jugador i inicia la corrutina del torn de l'enemic. </summary>
    private void EndPlayerTurn(string reactionType = "")
    {
        ShowTurnMenu(false);
        state = State.EnemyTurn;
        StartCoroutine(EnemyTurnRoutine(reactionType));
    }

    private Coroutine turnMenuAnim;

    /// <summary> Alterna la visibilitat del menú general amb lliscaments dinàmics (Cubic Ease Out). </summary>
    private void ShowTurnMenu(bool show)
    {
        if (turnMenu == null) return;
        
        if (turnMenuAnim != null) StopCoroutine(turnMenuAnim);
        
        if (show) 
        {
            turnMenu.SetActive(true);
            SetMenuPhase(MenuPhase.Main);
            turnMenuAnim = StartCoroutine(SlideMenuTo(turnMenu.GetComponent<RectTransform>(), turnMenuOriginalPos, 0.6f, true));
        }
        else
        {
            if (turnMenu.activeInHierarchy)
            {
                turnMenuAnim = StartCoroutine(SlideOutAndHide(turnMenu.GetComponent<RectTransform>(), turnMenuOriginalPos + new Vector2(0, -500f), 0.5f));
            }
        }
    }

    /// <summary> Corrutina per lliscar el menú de forma controlada per Lerp. </summary>
    private IEnumerator SlideMenuTo(RectTransform rect, Vector2 targetPos, float duration, bool easeOut)
    {
        if (rect == null) yield break;
        
        Vector2 startPos = rect.anchoredPosition;
        float time = 0;
        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;
            float easeT = easeOut ? (1f - Mathf.Pow(1f - t, 3f)) : (t * t * t);
            
            rect.anchoredPosition = Vector2.Lerp(startPos, targetPos, easeT);
            yield return null;
        }
        rect.anchoredPosition = targetPos;
    }

    /// <summary> Lliscament de sortida que en finalitzar desactiva completament el GameObject de la UI. </summary>
    private IEnumerator SlideOutAndHide(RectTransform rect, Vector2 targetPos, float duration)
    {
        yield return SlideMenuTo(rect, targetPos, duration, false);
        turnMenu.SetActive(false);
    }

    /// <summary> Activa/Desactiva la capacitat de control de les mans. </summary>
    private void SetHandsActive(bool active)
    {
        if (handControllers == null) return;
        foreach (var hand in handControllers)
        {
            if (hand != null) hand.canMove = active;
        }
    }

    /// <summary> Retorna si alguna de les mans gaudeix actualment d'immunitat transitòria. </summary>
    public bool IsPlayerImmune()
    {
        if (handControllers == null) return false;
        foreach (var hand in handControllers)
        {
            if (hand != null && hand.IsImmune) return true;
        }
        return false;
    }

    /// <summary> Concedeix immunitat temporal general a totes les mans. </summary>
    public void TriggerGlobalImmunity(float duration)
    {
        if (handControllers == null) return;
        foreach (var hand in handControllers)
        {
            if (hand != null) hand.TriggerImmunity(duration);
        }
    }

    // ─── TORN DE L'ENEMIC (ENEMY TURN ROUTINE) ───────────────────────
    /// <summary>
    /// Corrutina del torn enemic: reprodueix diàlegs orals orals segons si el jugador ha atacat o s'ha curat,
    /// activa el moviment de les mans dins l'arena de combat, tria de forma pseudoaleatòria 
    /// un dels patrons de projectils de la fase activa evitant la repetició idèntica de l'anterior torn,
    /// espera que s'executi la coreografia del Bullet Hell (EnemyAttackSpawner),
    /// i un cop destruïts tots els projectils, dedueix els buffs de velocitat transitoris i retorna al torn del jugador.
    /// </summary>
    private IEnumerator EnemyTurnRoutine(string reactionType)
    {
        Debug.Log("ENEMY TURN started");

        string comment = "";
        if (encounter != null && encounter.enemyProfile != null)
        {
            var p = encounter.enemyProfile;
            if (reactionType == "ATTACK" && p.attackReactions != null && attackReactionIndex < p.attackReactions.Length)
            {
                comment = p.attackReactions[attackReactionIndex];
                attackReactionIndex++;
            }
            else if (reactionType == "HEAL" && p.healReactions != null && healReactionIndex < p.healReactions.Length)
            {
                comment = p.healReactions[healReactionIndex];
                healReactionIndex++;
            }
            else if (reactionType == "FLEE_FAIL" && p.fleeFailReactions != null && fleeFailReactionIndex < p.fleeFailReactions.Length)
            {
                comment = p.fleeFailReactions[fleeFailReactionIndex];
                fleeFailReactionIndex++;
            }
        }

        if (!string.IsNullOrEmpty(comment) && !isPhaseShiftingThisTurn)
        {
            yield return EnemySpeakRoutine(comment);
        }

        // Si teníem un canvi de fase pendent de resoldre la seva bombolla, s'espera fins a quedar lliure
        while (isPhaseShiftingThisTurn) yield return null;

        // Reactivem el moviment de les mans per esquivar bales a l'arena de combat
        SetHandsActive(true);

        float dur = 2f;
        if (encounter != null) dur = encounter.enemyProfile != null ? encounter.enemyProfile.attackDuration : encounter.enemyAttackDuration;

        var spawner = FindFirstObjectByType<EnemyAttackSpawner>();
        if (spawner != null)
        {
            EnemyAttackPattern chosenPattern = EnemyAttackPattern.RandomDrop;
            GameObject prefab = encounter != null ? encounter.projectilePrefab : null;

            if (encounter != null)
            {
                if (encounter.enemyProfile != null)
                {
                    prefab = encounter.enemyProfile.projectilePrefab;
                    if (currentPhaseAttacks != null && currentPhaseAttacks.Length > 0)
                    {
                        chosenPattern = GetRandomPattern(currentPhaseAttacks);
                    }
                    else if (encounter.enemyProfile.attackPatterns != null && encounter.enemyProfile.attackPatterns.Length > 0)
                    {
                        chosenPattern = GetRandomPattern(encounter.enemyProfile.attackPatterns);
                    }
                }
                else if (encounter.attackPatterns != null && encounter.attackPatterns.Length > 0)
                {
                    chosenPattern = GetRandomPattern(encounter.attackPatterns);
                }
            }

            spawner.Configure(prefab, chosenPattern);
            yield return spawner.Run(dur);
            
            yield return new WaitForSeconds(0.1f);

            // Wait: el torn no canvia fins que s'ha esvaït fins a l'últim projectil de l'arena de combat
            yield return new WaitUntil(() => ProjectileUI.activeProjectiles <= 0);
        }
        else
        {
            yield return new WaitForSeconds(dur);
        }

        Debug.Log("ENEMY TURN ended");

        SetHandsActive(false); // Bloquegem el moviment de mans en el torn del jugador

        // Decrementem la durada de buffs de poció
        if (speedBuffRoundsLeft > 0)
        {
            speedBuffRoundsLeft--;
            if (speedBuffRoundsLeft == 0)
            {
                var hands = FindObjectsByType<HandController>(FindObjectsSortMode.None);
                foreach (var h in hands) h.speedMultiplier -= currentSpeedBuffValue;
                currentSpeedBuffValue = 0f;
                Debug.Log("Buff de velocitat de mans exhaurit!");
            }
        }

        if (enemyBubbleRT != null && enemyBubbleRT.gameObject.activeSelf)
        {
            enemyBubbleRT.gameObject.SetActive(false);
            isEnemySpeaking = false;
            isPhaseShiftingThisTurn = false;
        }

        // Si el jugador ha mort degut als projectils rebuts, ens aturem
        if (state == State.End)
        {
            yield break;
        }

        state = State.PlayerTurn;
        ShowTurnMenu(true);
    }

    /// <summary>
    /// Selecciona de manera aleatòria un dels patrons d'atac de la llista 
    /// assegurant-se de no repetir el mateix d'abans si hi ha prou ventall d'opcions.
    /// </summary>
    private EnemyAttackPattern GetRandomPattern(EnemyAttackPattern[] patterns)
    {
        if (patterns == null || patterns.Length == 0) return EnemyAttackPattern.RandomDrop;
        if (patterns.Length == 1) return patterns[0];

        EnemyAttackPattern chosen;
        int maxAttempts = 10;
        int attempts = 0;
        
        do
        {
            chosen = patterns[Random.Range(0, patterns.Length)];
            attempts++;
        } 
        while (lastUsedPattern.HasValue && chosen == lastUsedPattern.Value && attempts < maxAttempts);

        lastUsedPattern = chosen;
        return chosen;
    }

    /// <summary>
    /// Processa les llistes de probabilitats d'obtenició de drops
    /// per decidir quins objectes reals s'agreguen a l'inventari.
    /// </summary>
    private System.Collections.Generic.List<string> CalculateDrops(DropItemProbability[] drops)
    {
        var earned = new System.Collections.Generic.List<string>();
        if (drops != null)
        {
            foreach (var drop in drops)
            {
                int prob = drop.probability;
                while (prob >= 100)
                {
                    earned.Add(drop.itemName);
                    prob -= 100;
                }
                if (prob > 0 && Random.Range(0, 100) < prob)
                {
                    earned.Add(drop.itemName);
                }
            }
        }
        return earned;
    }
}
