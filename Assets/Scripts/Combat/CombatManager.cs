using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class CombatManager : MonoBehaviour
{
    public enum State
    {
        Enter,
        PlayerTurn,
        EnemyTurn,
        Resolve,
        End
    }

    public enum MenuPhase
    {
        Main,
        Target
    }

    [Header("UI")]
    [SerializeField] private GameObject turnMenu;
    [SerializeField] private Button fightButton;
    [SerializeField] private Button reasonButton;
    [SerializeField] private Button itemButton;
    [SerializeField] private Button fleeButton;
    [SerializeField] private SkillCheckUI skillCheckPrefab;

    private Button[] mainButtons;
    private int selectedIndex = 0;
    private MenuPhase currentPhase = MenuPhase.Main;
    private string originalFightText;

    private State state;
    public bool IsEnded => state == State.End;
    private CombatEncounter encounter;
    private CombatLoader loader;
    
    [Header("Stats")]
    public int playerMaxHP = 100;
    public int enemyMaxHP = 15;
    private int playerCurrentHP;
    private int enemyCurrentHP;
    
    // Variables per controlar el buff de velocitat
    private int speedBuffRoundsLeft = 0;
    private float currentSpeedBuffValue = 0f;

    [Header("UI Stats")]
    [SerializeField] private RectTransform playerUIPanel;
    [SerializeField] private TMPro.TMP_Text playerNameText;
    [SerializeField] private TMPro.TMP_Text playerHPText;
    [SerializeField] private Image playerHPFill;
    [SerializeField] private Image playerPortraitImage; // NOU CAMP: Aquí poses la imatge a la que aniran les partícules
    
    [Space]
    [SerializeField] private RectTransform enemyUIPanel;
    [SerializeField] private TMPro.TMP_Text enemyNameText;
    [SerializeField] private TMPro.TMP_Text enemyHPText;
    [SerializeField] private Image enemyHPFill;
    [SerializeField] private Image enemyPortraitImage; // <- NOU CAMP PER LA FOTO

    [Header("Audio Feedback")]
    [SerializeField] private AudioClip moveMenuSound;
    [SerializeField] private AudioClip confirmMenuSound; // NOU: So al triar opció
    [SerializeField] private AudioClip attackSound;      // So al iniciar l'atac (premem E al minijoc)
    [SerializeField] private AudioClip enemyHitSound;    // So quan l'enemic rep dany
    [SerializeField] private AudioClip takeDamageSound;
    [SerializeField] private AudioClip parrySound;
    [SerializeField] private AudioClip defendParrySound; // NOU: So al fer parry mentre es defensa
    [SerializeField] private AudioClip explosionSound; // NOU: Soroll de la explosio de pixels
    [SerializeField] private AudioClip playerMoveSound;
    [SerializeField] private AudioClip victorySound;    // So de victòria al final del combat
    [SerializeField] private AudioClip playerActionVoice; // NOU: So del narrador en combat (playerActionText)
    private AudioSource audioSource;
    private AudioSource loopAudioSource;
    private AudioSource voiceAudioSource; // NOU: Una font de so dedicada només a veus per no afectar a la resta de sons
    private Coroutine activeEnemySpeakCoroutine; // NOU: Registre per poder cancel·lar diàlegs si canvia la fase (específicament la bombolla)
    private bool isPhaseShiftingThisTurn = false; // NOU: Flag per evitar diàlegs de reacció si hi ha hagut canvi de fase

    [Header("Game Over Settings")]
    [SerializeField] private AudioClip gameOverMusic;
    [SerializeField] private AudioClip gameOverVoice;
    [SerializeField] [TextArea] private string gameOverText = "...";

    [Header("VFX & Limits")]
    [SerializeField] private GameObject parryParticlePrefab;
    [SerializeField] private RectTransform projectileDestroyLimit;

    [Header("Item Animation Settings")]
    [Tooltip("Punt on neix l'objecte (part baixa de la pantalla)")]
    [SerializeField] private RectTransform throwStartPoint;
    [Tooltip("Alçada màxima de la paràbola")]
    [SerializeField] private float throwArcHeight = 400f;
    [Tooltip("Línia de terra on cauen els objectes després d'impactar")]
    [SerializeField] private RectTransform itemGroundLine;

    private HandController[] handControllers;

    // Default positions used for Entrance Animations
    private Vector2 playerUIOriginalPos;
    private Vector2 enemyUIOriginalPos;
    private Vector2 playerNameOriginalPos;
    private Vector2 playerHPTextOriginalPos;
    private Vector2 enemyNameOriginalPos;
    private Vector2 enemyHPTextOriginalPos;
    private Vector2 turnMenuOriginalPos;
    
    private Sprite softCircleSprite; // Procedural
    private Image selectionGlowImage; // The mirror glow

    private RectTransform enemyBubbleRT;
    private TMPro.TMP_Text enemyDialogTxt;
    private RectTransform enemyBubblePromptRT;
    private CanvasGroup enemyBubblePromptCG;
    private bool isEnemySpeaking;
    private Sprite generatedRoundedSprite;

    private int attackReactionIndex = 0;
    private int healReactionIndex = 0;
    private int fleeFailReactionIndex = 0;

    // Social BT
    private SocialBTState socialState;
    private GameObject socialMenuGO;

    // Recompensa de reclutament pendent (es mostra fora del combat)
    private EnemyProfile pendingRecruitReward;

    // Control de fases de combat
    private int currentPhaseIndex = -1;
    private EnemyAttackPattern[] currentPhaseAttacks;

    /// <summary>Retorna i neteja la recompensa pendent (si n'hi ha).</summary>
    public EnemyProfile ConsumeRecruitReward()
    {
        var r = pendingRecruitReward;
        pendingRecruitReward = null;
        return r;
    }

    private void Awake()
    {
        CreateSoftCircle(); // Generem la textura de la cervesa circular difuminada

        if (enemyPortraitImage != null) enemyPortraitImage.enabled = false;

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

        if (turnMenu != null) 
        {
            var rt = turnMenu.GetComponent<RectTransform>();
            turnMenuOriginalPos = rt.anchoredPosition;
            rt.anchoredPosition = turnMenuOriginalPos + new Vector2(0, -500f);
        }
        
        if (playerUIPanel != null) 
        {
            playerUIOriginalPos = playerUIPanel.anchoredPosition;
            playerUIPanel.anchoredPosition = playerUIOriginalPos + new Vector2(0, 300f);
        }
        else if (playerHPText != null) // Fallback al text si et descuides del panel
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

        // Emmagatzemem les posicions originals de tots els textos per fer slide in/out
        if (playerNameText != null) playerNameOriginalPos = playerNameText.rectTransform.anchoredPosition;
        if (playerHPText != null) playerHPTextOriginalPos = playerHPText.rectTransform.anchoredPosition;
        if (enemyNameText != null) enemyNameOriginalPos = enemyNameText.rectTransform.anchoredPosition;
        if (enemyHPText != null) enemyHPTextOriginalPos = enemyHPText.rectTransform.anchoredPosition;
        
        // Inicialment els desplacem fora (cap amunt)
        if (playerNameText != null) playerNameText.rectTransform.anchoredPosition += new Vector2(0, 300f);
        if (playerHPText != null) playerHPText.rectTransform.anchoredPosition += new Vector2(0, 300f);
        if (enemyNameText != null) enemyNameText.rectTransform.anchoredPosition += new Vector2(0, 300f);
        if (enemyHPText != null) enemyHPText.rectTransform.anchoredPosition += new Vector2(0, 300f);
        
        BuildEnemyBubble();
    }
    
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

    private void BuildEnemyBubble()
    {
        if (enemyPortraitImage == null) return;
        
        GameObject go = new GameObject("EnemyBubble");
        go.transform.SetParent(enemyPortraitImage.transform, false);
        enemyBubbleRT = go.AddComponent<RectTransform>();
        
        enemyBubbleRT.anchorMin = new Vector2(-0.25f, 0.85f);
        enemyBubbleRT.anchorMax = new Vector2(1.25f, 1.25f);
        enemyBubbleRT.offsetMin = enemyBubbleRT.offsetMax = Vector2.zero;
        
        var tailGO = new GameObject("Tail");
        tailGO.transform.SetParent(enemyBubbleRT, false);
        var tailRT = tailGO.AddComponent<RectTransform>();
        tailRT.anchorMin = new Vector2(0.5f, 0f); tailRT.anchorMax = new Vector2(0.5f, 0f);
        tailRT.sizeDelta = new Vector2(36f, 36f);
        tailRT.anchoredPosition = new Vector2(-20f, 8f);
        tailRT.localRotation = Quaternion.Euler(0, 0, 65f);
        var tailImg = tailGO.AddComponent<Image>();
        tailImg.color = new Color(1f, 1f, 1f, 0.95f);

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

        // --- NOU: PROMPT DE [E] ---
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

        var pBase = new GameObject("Base");
        pBase.transform.SetParent(enemyBubblePromptRT, false);
        var pbRT = pBase.AddComponent<RectTransform>();
        pbRT.anchorMin = Vector2.zero; pbRT.anchorMax = Vector2.one; pbRT.offsetMin = pbRT.offsetMax = Vector2.zero;
        var pbImg = pBase.AddComponent<Image>();
        pbImg.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        
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
                // Falloff tipus Gaussià per un difuminat de "Premium" real
                float alpha = Mathf.Exp(-4f * d * d) * Mathf.Clamp01(1f - d);
                tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
        }
        tex.Apply();
        softCircleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

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
                enemyPortraitImage.preserveAspect = true; // Manté les proporcions naturals
                enemyPortraitImage.enabled = true;
            }
            else
            {
                enemyPortraitImage.enabled = false;
            }
        }
    }

    public IEnumerator BeginRoutine(CombatEncounter encounter, CombatLoader loader)
    {
        this.encounter = encounter;
        this.loader = loader;

        if (PlayerInventory.Instance != null && encounter != null && encounter.enemyProfile != null)
        {
            PlayerInventory.Instance.EncounterEnemy(encounter.enemyProfile.enemyName);
        }

        // Llegeix HP de l'inventari persistent (si existeix i té vida > 0)
        if (PlayerInventory.Instance != null && PlayerInventory.Instance.CurrentHP > 0)
        {
            playerMaxHP = PlayerInventory.Instance.MaxHP;
            playerCurrentHP = PlayerInventory.Instance.CurrentHP;
        }
        else
        {
            playerCurrentHP = playerMaxHP;
        }

        // Nota: El bonus de vida de reclutament ja està inclòs a PlayerInventory.Instance.MaxHP
        // i es manté persistentment. No cal aplicar-lo manualment cada combat.
        
        // Sobreescriu valors base d'enemic si heu fet algun perfil (ScriptableObject) personalitzat
        string finalEnemyName = "MONSTER";
        Sprite finalEnemySprite = encounter != null ? encounter.enemyPortrait : null;
        
        if (encounter != null && encounter.enemyProfile != null)
        {
            enemyMaxHP = Random.Range(encounter.enemyProfile.minHP, encounter.enemyProfile.maxHP + 1);
            finalEnemyName = encounter.enemyProfile.enemyName.ToUpper();
            if (encounter.enemyProfile.enemyPortrait != null) finalEnemySprite = encounter.enemyProfile.enemyPortrait;
        }

        enemyCurrentHP = enemyMaxHP;
        
        // Inicialitzar fases
        currentPhaseIndex = -1;
        currentPhaseAttacks = (encounter?.enemyProfile != null) ? encounter.enemyProfile.attackPatterns : null;
        CheckPhaseShift(true); 

        UpdateStatsUI(true); // Posa les barres completes de cop a l'inci

        // Aplica l'sprite visual
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

        SetupButtonInteractions();

        // Find and disable hands initially
        handControllers = FindObjectsByType<HandController>(FindObjectsSortMode.None);
        SetHandsActive(false);

        state = State.PlayerTurn;

        // Si l'enemic ha començat parlant (per canvi de fase o inici), esperem que acabi abans de posar el menú
        if (isPhaseShiftingThisTurn)
        {
            while (isEnemySpeaking) yield return null;
            isPhaseShiftingThisTurn = false;
        }

        ShowTurnMenu(true);

        // Configura noms
        if (playerNameText != null) playerNameText.text = "Me";
        if (enemyNameText != null) enemyNameText.text = finalEnemyName;

        // Dispara les animacions d'entrada tipus Slide UI per tota la resta de text/panells
        if (playerUIPanel != null) StartCoroutine(SlideInRect(playerUIPanel, playerUIOriginalPos, new Vector2(0, 300f), 0.7f));
        if (enemyUIPanel != null) StartCoroutine(SlideInRect(enemyUIPanel, enemyUIOriginalPos, new Vector2(0, 300f), 0.7f));
        
        // També els textos individuals si existeixen
        if (playerNameText != null) StartCoroutine(SlideInRect(playerNameText.rectTransform, playerNameOriginalPos, new Vector2(0, 300f), 0.7f));
        if (playerHPText != null) StartCoroutine(SlideInRect(playerHPText.rectTransform, playerHPTextOriginalPos, new Vector2(0, 300f), 0.7f));
        if (enemyNameText != null) StartCoroutine(SlideInRect(enemyNameText.rectTransform, enemyNameOriginalPos, new Vector2(0, 300f), 0.7f));
        if (enemyHPText != null) StartCoroutine(SlideInRect(enemyHPText.rectTransform, enemyHPTextOriginalPos, new Vector2(0, 300f), 0.7f));

        // ── Botons de debug (proves) ────────────────────────────────
        #if UNITY_EDITOR
        SpawnDebugButtons();
        #endif
    }

    #if UNITY_EDITOR
    private void SpawnDebugButtons()
    {
        Transform canvasParent = turnMenu != null ? turnMenu.transform.parent : transform;

        // Botó "PERDONAR" (victòria pacífica)
        CreateDebugButton(canvasParent, "DBG_Peace", "✓ PERDONAR", new Vector2(-100f, -30f),
            new Color(0.15f, 0.5f, 0.15f, 0.85f), () =>
        {
            if (state == State.End) return;
            state = State.End;
            StopAllCoroutines();
            StartCoroutine(FriendVictoryRoutine());
        });

        // Botó "MATAR" (victòria agressiva)
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
            // Cubic Ease Out per un moviment suau i polit cap al final
            float easeT = 1f - Mathf.Pow(1f - t, 3f);
            rect.anchoredPosition = Vector2.Lerp(startPos, targetPos, easeT);
            yield return null;
        }
        
        rect.anchoredPosition = targetPos;
    }

    private IEnumerator SlideOutRect(RectTransform rect, Vector2 originalPos, Vector2 exitOffset, float duration)
    {
        if (rect == null) yield break;
        
        Vector2 targetPos = originalPos + exitOffset;
        float time = 0;
        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;
            // Cubic Ease In per una sortida que s'accelera
            float easeT = t * t * t;
            rect.anchoredPosition = Vector2.Lerp(originalPos, targetPos, easeT);
            yield return null;
        }
        rect.anchoredPosition = targetPos;
    }

    private void Update()
    {
        // Animació del Prompt de [E] a la bombolla de l'enemic
        if (enemyBubblePromptCG != null && enemyBubblePromptRT != null)
        {
            if (enemyBubbleRT != null && enemyBubbleRT.gameObject.activeSelf && enemyBubblePromptCG.alpha > 0.5f)
            {
                float cycle = Time.unscaledTime * 1.5f;
                bool isPressed = (cycle % 1f) > 0.7f;
                // Accedim al "Top" (fill 1) per moure'l com si fos una tecla
                if (enemyBubblePromptRT.childCount > 1)
                {
                    var topRT = enemyBubblePromptRT.GetChild(1).GetComponent<RectTransform>();
                    if (topRT != null) topRT.anchoredPosition = isPressed ? Vector2.zero : new Vector2(0f, 3f);
                }
            }
        }

        // --- Handle Player Movement Sound looping centrally ---
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

        // --- Handle UI Input ---
        if (state != State.PlayerTurn || isPhaseShiftingThisTurn || isEnemySpeaking) return;

        // Bloquejar input del combat mentre l'inventari o el menú de pausa són oberts
        if (InventoryMenuUI.IsOpen || PauseMenuUI.IsOpen) return;

        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
        {
            MoveSelection(-1);
        }
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
        {
            MoveSelection(1);
        }
        else if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return))
        {
            ConfirmSelection();
        }
        else if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift) || Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Escape))
        {
            if (currentPhase == MenuPhase.Target)
            {
                SetMenuPhase(MenuPhase.Main);
            }
        }
        
        // --- DEBUG SHORTCUT ---
        // Prem 'O' en qualsevol moment del teu torn per forçar forçar la victòria i veure l'animació reverse
        if (Input.GetKeyDown(KeyCode.O))
        {
            Debug.Log("DEBUG: Forçant Victòria amb la 'O'");
            state = State.End;
            StartCoroutine(VictoryRoutine());
        }
    }



    // =========================
    // UI Helpers
    // =========================

    private void MoveSelection(int direction)
    {
        int maxOptions = currentPhase == MenuPhase.Main ? mainButtons.Length : 1;
        selectedIndex += direction;
        
        if (selectedIndex < 0) selectedIndex = maxOptions - 1;
        if (selectedIndex >= maxOptions) selectedIndex = 0;
        
        if (moveMenuSound) audioSource.PlayOneShot(moveMenuSound);
        UpdateSelectionVisuals();
    }

    private void ConfirmSelection()
    {
        if (confirmMenuSound && audioSource) audioSource.PlayOneShot(confirmMenuSound);
        
        if (currentPhase == MenuPhase.Main)
        {
            // Saltem el pas de Targejar l'Enemic (TargetPhase) entrant directament a l'Atac (La Ruleta)
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

    private void SetupButtonInteractions()
    {
        for (int i = 0; i < mainButtons.Length; i++)
        {
            Button btn = mainButtons[i];
            if (btn != null)
            {
                // Disable all mouse interaction
                btn.interactable = false;
            }
        }
    }

    private string GetButtonText(Button btn)
    {
        var tmp = btn.GetComponentInChildren<TMPro.TMP_Text>();
        if (tmp != null) return tmp.text;
        var txt = btn.GetComponentInChildren<Text>();
        if (txt != null) return txt.text;
        return "";
    }

    private void SetButtonText(Button btn, string text)
    {
        var tmp = btn.GetComponentInChildren<TMPro.TMP_Text>();
        if (tmp != null) { tmp.text = text; return; }
        var txt = btn.GetComponentInChildren<Text>();
        if (txt != null) { txt.text = text; return; }
    }

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
            GameObject go = new GameObject("SelectionFX_VibrantYellowHighlight");
            selectionCursorFrame = go.AddComponent<RectTransform>();
            selectionGlowImage = go.AddComponent<Image>();
            selectionGlowImage.raycastTarget = false;
            
            StartCoroutine(PremiumGlowRoutine(selectionCursorFrame, selectionGlowImage));
        }

        selectionCursorFrame.gameObject.SetActive(true);

        // El posem com a primer fill del botó (perquè quedi darrere del text i la icona)
        selectionCursorFrame.SetParent(selBtn.transform, false);
        selectionCursorFrame.SetAsFirstSibling();
        
        selectionCursorFrame.anchorMin = Vector2.zero;
        selectionCursorFrame.anchorMax = Vector2.one;
        selectionCursorFrame.pivot = new Vector2(0.5f, 0.5f);
        selectionCursorFrame.anchoredPosition = Vector2.zero;
        
        // El fem créixer un pèl per fora del botó per fer l'efecte de "contorn" (només 4-5px)
        selectionCursorFrame.offsetMin = new Vector2(-5, -5);
        selectionCursorFrame.offsetMax = new Vector2(5, 5);
        selectionCursorFrame.localScale = Vector3.one;

        Image btnImg = selBtn.GetComponent<Image>();
        if (btnImg != null && selectionGlowImage != null)
        {
            selectionGlowImage.sprite = btnImg.sprite;
            selectionGlowImage.type = btnImg.type;
            selectionGlowImage.preserveAspect = btnImg.preserveAspect;
            
            // Assignem el color segons el botó
            Color selectionColor = Color.yellow;
            switch(selectedIndex)
            {
                case 0: selectionColor = new Color(1f, 0.2f, 0.2f, 0.9f); break; // Vermell (Atacar)
                case 1: selectionColor = new Color(1f, 0.9f, 0f, 0.9f);   break; // Groc (Raonar)
                case 2: selectionColor = new Color(0.7f, 0.3f, 1f, 0.9f); break; // Lila (Objectes)
                case 3: selectionColor = new Color(0.2f, 0.6f, 1f, 0.9f); break; // Blau (Fugir)
            }
            selectionGlowImage.color = selectionColor;
        }
    }

    private Color currentHighlightColor = Color.yellow;

    private IEnumerator PremiumGlowRoutine(RectTransform rt, Image glowImg)
    {
        while (true)
        {
            if (rt == null || glowImg == null) yield break;

            // Llegim el color actual del glow per passar-lo a les partícules
            currentHighlightColor = glowImg.color;

            // Un detall fix però amb un alpha que "respira" molt subtilment per donar vida
            float t = (Mathf.Sin(Time.unscaledTime * 3f) + 1f) / 2f;
            Color c = glowImg.color;
            c.a = Mathf.Lerp(0.5f, 0.9f, t);
            glowImg.color = c;

            rt.localScale = Vector3.one;

            // Més quantitat de partícules (frequència augmentada)
            if (rt.gameObject.activeInHierarchy && Random.value < 0.25f)
            {
                SpawnPremiumCircleParticle(rt);
            }
            
            yield return null;
        }
    }

    private void SpawnPremiumCircleParticle(RectTransform parent)
    {
        GameObject p = new GameObject("P_Sparkle");
        p.transform.SetParent(parent, false); 
        p.transform.position = parent.position; 
        
        RectTransform partRT = p.AddComponent<RectTransform>();
        
        // Ara neixen de la bora superior del botó
        float randX = Random.Range(-parent.rect.width / 2.2f, parent.rect.width / 2.2f);
        float topEdgeY = parent.rect.height / 2f; 
        partRT.anchoredPosition = new Vector2(randX, topEdgeY);

        // Més petites, com una ebullició de llum
        float size = Random.Range(1.5f, 4f); 
        partRT.sizeDelta = new Vector2(size, size);
        
        Image img = p.AddComponent<Image>();
        img.sprite = softCircleSprite;
        img.raycastTarget = false;
        img.color = currentHighlightColor;
        
        StartCoroutine(AnimatePremiumParticle(partRT, img));
    }

    private IEnumerator AnimatePremiumParticle(RectTransform rt, Image img)
    {
        // Velocitat gairebé nul·la (com si suressin pràcticament quietes)
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
            
            // Moviment molt més controlat i arran de la vora
            Vector2 pos = rt.anchoredPosition;
            pos += vel * Time.unscaledDeltaTime;
            pos.x += Mathf.Sin(elapsed * waveFreq) * waveAmp * Time.unscaledDeltaTime;
            rt.anchoredPosition = pos;
            
            // "Twinkle" effect ràpid
            float twinkle = Mathf.Sin(elapsed * 15f) * 0.3f + 0.7f;
            Color c = img.color;
            c.a = (1f - t) * 0.7f * twinkle;
            img.color = c;
            
            // Escalat eteri molt petit
            rt.localScale = Vector3.one * (0.9f + Mathf.PingPong(elapsed * 1.5f, 0.3f));
            
            yield return null;
        }
        
        if (rt != null) Destroy(rt.gameObject);
    }

    private Coroutine playerHPAnim;
    private Coroutine enemyHPAnim;

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

        CheckPhaseShift(instant);
    }

    private void CheckPhaseShift(bool immediate = false)
    {
        if (encounter == null || encounter.enemyProfile == null || encounter.enemyProfile.phases == null || encounter.enemyProfile.phases.Length == 0) return;

        float hpPercent = (float)enemyCurrentHP / enemyMaxHP * 100f;
        int bestPhase = -1;

        // Trobem la fase més alta (menor threshold) que encara és activa
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

    private void ApplyPhase(int index, bool immediate)
    {
        // Cancel·lem qualsevol diàleg actiu (bombolla) si canviem de fase
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
            // Si hi ha diàlegs en aquesta fase, marquem el flag perquè l'escena s'esperi (Begin o turns)
            var phase = (index >= 0) ? encounter.enemyProfile.phases[index] : default;
            if (index >= 0 && phase.transitionDialogues != null && phase.transitionDialogues.Length > 0)
                isPhaseShiftingThisTurn = true;

            ShowPhaseDialogue(index);
        }
        else
        {
            // Canvi d'sprite immediat al rebre el dany
            SetPhaseVisuals(index);
            // El diàleg espera a que acabi el shake (0.35s) + 1 segon extra per petició usuari
            isPhaseShiftingThisTurn = true; // Bloquegem reaccions immediatament
            StartCoroutine(ApplyPhaseDialogueRoutine(index));
        }
    }

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
            // Si l'índex és vàlid però no hi havia diàlegs, cal alliberar el flag de bloqueig d'input
            isPhaseShiftingThisTurn = false;
        }
    }

    private IEnumerator ShowMultiplePhaseDialogues(PhaseDialogueLine[] lines)
    {
        foreach (var line in lines)
        {
            yield return EnemySpeakRoutine(line.message, line.typingSpeedMultiplier, line.shakeText);
        }
    }

    private IEnumerator ApplyPhaseDialogueRoutine(int index)
    {
        yield return new WaitForSeconds(1.35f);

        // NOU: Si l'enemic és rendit amistosament, la música ja comença a baixar aquí
        if (index >= 0)
        {
            var phase = encounter.enemyProfile.phases[index];
            if (phase.endFightFriendly && loader != null)
                StartCoroutine(loader.FadeCombatMusic(false, 4f)); // Un fade una mica més llarg perquè dura el temps de diàleg
        }
        
        ShowPhaseDialogue(index);
        
        // Esperem que realment acabi de parlar (si hi ha diàlegs)
        while (isEnemySpeaking) yield return null;
        yield return null; // Frame de seguretat

        // NOU: Friendly End si la fase ho indica
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

    private IEnumerator FriendlyVictoryRoutine()
    {
        // El fade de música ja s'hauria d'haver iniciat a l'ApplyPhaseDialogueRoutine
        // Però per seguretat (si no hi hagués hagut diàleg), mirem de mantenir-lo
        if (loader != null) StartCoroutine(loader.FadeCombatMusic(false, 1.5f));
        
        // So de victòria
        if (victorySound != null && audioSource != null) audioSource.PlayOneShot(victorySound);

        yield return new WaitForSeconds(0.5f);

        ShowTurnMenu(false);

        // Reclutem l'enemic directament per ser final pacífic (si hi ha inventari)
        if (PlayerInventory.Instance != null && encounter?.enemyProfile != null)
        {
            PlayerInventory.Instance.RecruitEnemy(encounter.enemyProfile.enemyName);
        }

        // Slide Out UI
        float outDur = 0.5f;
        Vector2 outOff = new Vector2(0, 400f);
        if (playerUIPanel != null) StartCoroutine(SlideOutRect(playerUIPanel, playerUIOriginalPos, outOff, outDur));
        if (enemyUIPanel != null) StartCoroutine(SlideOutRect(enemyUIPanel, enemyUIOriginalPos, outOff, outDur));
        if (playerNameText != null) StartCoroutine(SlideOutRect(playerNameText.rectTransform, playerNameOriginalPos, outOff, outDur));
        if (playerHPText != null) StartCoroutine(SlideOutRect(playerHPText.rectTransform, playerHPTextOriginalPos, outOff, outDur));
        if (enemyNameText != null) StartCoroutine(SlideOutRect(enemyNameText.rectTransform, enemyNameOriginalPos, outOff, outDur));
        if (enemyHPText != null) StartCoroutine(SlideOutRect(enemyHPText.rectTransform, enemyHPTextOriginalPos, outOff, outDur));

        // Mostrem el panell animat de victòria sense haver matat a ningú (0 or, sense drops matança)
        Transform canvasParent = turnMenu != null ? turnMenu.transform.parent : transform;
        bool done = false;
        VictoryPanelUI.Create(canvasParent, 0, new System.Collections.Generic.List<string>(), 
                              PlayerInventory.Instance != null ? PlayerInventory.Instance.Gold : 0, () => done = true);

        yield return new WaitUntil(() => done);
        loader.EndCombat();
    }

    private IEnumerator AnimateHPBar(Image hpImage, float targetFill, float duration)
    {
        if (hpImage == null) yield break;
        
        float startFill = hpImage.fillAmount;
        float time = 0f;
        while (time < duration)
        {
            time += Time.deltaTime;
            // Moviment Cúbic suau però directe
            float t = time / duration;
            float easeT = 1f - Mathf.Pow(1f - t, 3f);
            hpImage.fillAmount = Mathf.Lerp(startFill, targetFill, easeT);
            yield return null;
        }
        hpImage.fillAmount = targetFill;
    }

    public void PlayerTakeDamage(int damage)
    {
        if (state == State.End) return; // Evitem dany si ja hem mort o acabat

        int finalDamage = damage;

        // Aplica bonus de defensa per reclutament completat
        if (PlayerInventory.Instance != null)
        {
            float defBonus = PlayerInventory.Instance.GetTotalDefenseBonus();
            if (defBonus > 0f)
            {
                int reduction = Mathf.RoundToInt(finalDamage * (defBonus / 100f));
                finalDamage = Mathf.Max(1, finalDamage - reduction); // Mínim 1 de dany
            }
        }

        playerCurrentHP -= finalDamage;
        UpdateStatsUI();

        if (takeDamageSound) audioSource.PlayOneShot(takeDamageSound);

        // Guarda HP actualitzat a l'inventari persistent
        if (PlayerInventory.Instance != null)
            PlayerInventory.Instance.SetHP(playerCurrentHP);

        // Si el jugador mor
        if (playerCurrentHP <= 0)
        {
            playerCurrentHP = 0;
            state = State.End;
            SetHandsActive(false); // Desactivem les mans i el seu soroll immediatament
            
            // Destruïm tots els projectils en pantalla perquè no continuïn fent efectes
            var activeProjs = FindObjectsByType<ProjectileUI>(FindObjectsSortMode.None);
            foreach(var p in activeProjs) 
            {
                if (p != null) Destroy(p.gameObject);
            }
            ProjectileUI.activeProjectiles = 0;

            StartCoroutine(GameOverRoutine());
        }
    }

    private IEnumerator GameOverRoutine()
    {
        // 1. Pantalla negra i silenci
        GameObject blackScreenGO = new GameObject("GameOverScreen");
        Canvas c = blackScreenGO.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 9999;
        
        UnityEngine.UI.Image img = blackScreenGO.AddComponent<UnityEngine.UI.Image>();
        img.color = new Color(0, 0, 0, 0f); 

        // Aturarem el combat loader i els audios de cop
        loader.StartCoroutine(loader.FadeCombatMusic(false, 0f));
        if (audioSource) audioSource.Stop();
        if (loopAudioSource) loopAudioSource.Stop();
        if (voiceAudioSource) voiceAudioSource.Stop();

        // 2. Pantalla negra de cop i espera un breu moment de silenci
        img.color = Color.black;

        yield return new WaitForSecondsRealtime(1.5f);

        // 3. Música de game over amb fade in
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
            yield return new WaitForSecondsRealtime(2f); // Espera si no hi ha música
        }

        // 4. Dialeg (tipus overworld)
        DialogueUI dialogueUI = FindFirstObjectByType<DialogueUI>();
        if (dialogueUI == null)
        {
            var go = new GameObject("DialogueManager");
            dialogueUI = go.AddComponent<DialogueUI>();
        }

        Interactable.DialogueLine gameOverLine = new Interactable.DialogueLine();
        gameOverLine.text = string.IsNullOrEmpty(gameOverText) ? "GAME OVER" : gameOverText;
        gameOverLine.customVoiceSound = gameOverVoice;

        // Desactivem l'animació d'entrada passant un 'false' al nou paràmetre
        dialogueUI.StartDialogue(new Interactable.DialogueLine[] { gameOverLine }, false);

        // Forçar que el diàleg estigui per davant de la pantalla negra
        GameObject dialogPanel = GameObject.Find("DynamicDialoguePanel");
        if (dialogPanel != null)
        {
            Canvas panelCanvas = dialogPanel.GetComponent<Canvas>();
            if (panelCanvas == null) panelCanvas = dialogPanel.AddComponent<Canvas>();
            panelCanvas.overrideSorting = true;
            panelCanvas.sortingOrder = 10000;

            // Retocs visuals per mostrar només text i botó al centre per al Game Over
            UnityEngine.UI.Image bgImg = dialogPanel.GetComponent<UnityEngine.UI.Image>();
            if (bgImg != null) bgImg.enabled = false;
            
            UnityEngine.UI.Outline bgOl = dialogPanel.GetComponent<UnityEngine.UI.Outline>();
            if (bgOl != null) bgOl.enabled = false;

            RectTransform prt = dialogPanel.GetComponent<RectTransform>();
            if (prt != null)
            {
                // Ampliem el requadre a gairebé tota la pantalla per aprofitar millor l'espai
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
                    
                    // Com que ara l'àrea és més gran, l'hi aixequem el límit màxim (abans estava ancorat a 50px de base)
                    tmp.fontSizeMax = 150f;

                    // I força el recàlcul d'autoescalat un altre cop amagant la lletra correctament, doncs el límit inicial l'havia bloquejat.
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
                // Canviem el pare cap al Canvas arrel de la pantalla negra per a posar-ho a la cantonada real inferior-dreta
                eBtn.SetParent(blackScreenGO.transform, false);
                RectTransform ert = eBtn.GetComponent<RectTransform>();
                if (ert != null)
                {
                    ert.anchorMin = new Vector2(1f, 0f);
                    ert.anchorMax = new Vector2(1f, 0f);
                    ert.anchoredPosition = new Vector2(-50f, 50f); // marge des de la vora de la pantalla
                }
            }
        }

        // Esperem fins que el text hagi acabat de mostrar-se, i permetem saltar-lo
        while (dialogueUI.IsTyping)
        {
            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return))
            {
                dialogueUI.AdvanceOrSkip();
            }
            yield return null;
        }

        // Purgar un frame per evitar que la mateixa pulsació compti pel salt i per tancar
        yield return null; 

        while (!Input.GetKeyDown(KeyCode.E) && !Input.GetKeyDown(KeyCode.Return))
        {
            yield return null;
        }
        
        // No cridem "dialogueUI.Hide();" per evitar que el text faci el desplaçament de sortida cap a sota.
        // D'aquesta manera, es quedarà rígid mentre es fa el fade out superior.

        // FADE OUT ABANS DE CANVIAR D'ESCENA (Pintem un llençol negre per damunt de tot)
        GameObject fadeOutGO = new GameObject("FadeOutScreen");
        Canvas fadeCanvas = fadeOutGO.AddComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = 10005; 
        UnityEngine.UI.Image fadeImg = fadeOutGO.AddComponent<UnityEngine.UI.Image>();
        fadeImg.color = new Color(0, 0, 0, 0f); // Comença transparent
        
        float fadeTime = 0f;
        float fadeDuration = 1.0f;
        float startVolume = (audioSource != null) ? audioSource.volume : 0f;

        while(fadeTime < fadeDuration)
        {
            fadeTime += Time.unscaledDeltaTime;
            fadeImg.color = new Color(0, 0, 0, fadeTime / fadeDuration);
            // També fem fade out lent a la música del llop si n'hi ha.
            if (audioSource != null && startVolume > 0)
            {
                audioSource.volume = Mathf.Lerp(startVolume, 0f, fadeTime / fadeDuration);
            }
            yield return null;
        }

        // Esborrar progres al overworld
        if (PlayerInventory.Instance != null)
        {
            Destroy(PlayerInventory.Instance.gameObject);
        }
        
        // Netejar la flag global de combat per no deixar el jugador bloquejat
        CombatLoader.IsInCombat = false;

        // Torna a l'escena d'inici
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }

    public void PlayParrySound()
    {
        if (parrySound && audioSource) audioSource.PlayOneShot(parrySound);
    }

    public void PlayLocalSound(AudioClip clip)
    {
        if (clip && audioSource) audioSource.PlayOneShot(clip);
    }

    public AudioClip DamageSound => takeDamageSound;

    public void PlayExplosionSound()
    {
        if (explosionSound && audioSource) audioSource.PlayOneShot(explosionSound);
    }

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
                    img.color = new Color(0.2f, 1f, 0.2f, 1f); // Verd ara que el parry ens cura sempre
                }
            }

            Destroy(effect, 3f); // Auto-cleanup: desapareixen al cap de 3 segons
        }
    }

    public void OnParrySuccess(Vector3 pos, Sprite projectileSprite)
    {
        PlayParrySound();
        SpawnParryEffect(pos, projectileSprite);

        playerCurrentHP++;
        if (playerCurrentHP > playerMaxHP) playerCurrentHP = playerMaxHP;
        UpdateStatsUI();

        // Mostrem FX de curació sobre la barra de vida per reforçar el feedback
        Transform canvasParent = turnMenu != null ? turnMenu.transform.parent : transform;
        Image targetImg = playerPortraitImage != null ? playerPortraitImage : playerHPFill;
        HealFXUI.ShowAboveBar(canvasParent, targetImg, "+1", new Color(0.25f, 1f, 0.35f), 1f);
    }

    public float GetDestroyLimitY()
    {
        return projectileDestroyLimit != null ? projectileDestroyLimit.anchoredPosition.y : -1200f;
    }

    // =========================
    // Player actions
    // =========================

    private IEnumerator PerformAttackRoutine()
    {
        // Changing state to something else avoids PlayerTurn triggering ConfirmSelection via Space again.
        state = State.Resolve;
        
        // Amaguem el menú amb la seva animació de sortida instantàniament en decidir atacar perque el centre d'atenció sigui la ruleta
        ShowTurnMenu(false);

        int finalDmg = 0;

        // Perform Skill Check if available
        if (skillCheckPrefab != null && turnMenu != null)
        {
            SkillCheckUI skillCheck = Instantiate(skillCheckPrefab, turnMenu.transform.parent);
            skillCheck.SetAttackSound(attackSound);
            skillCheck.gameObject.SetActive(true); 
            skillCheck.transform.SetAsLastSibling(); 
            
            RectTransform rt = skillCheck.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = new Vector2(0, 150); // Més amunt dels botons
            }

            // Wait until skill check finishes and returns damage via callback
            bool checkFinished = false;

            // Passar multiplicador de dany segons els reclutaments completats
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
            
            // Aturam l'execució d'aquest IEnumerator fins que la funcio onDamage callback hagi set cridada.
            yield return new WaitUntil(() => checkFinished);
            
            Destroy(skillCheck.gameObject); 

            // NOU: Si el dany és -1, vol dir que l'usuari ha cancel·lat el minijoc
            if (finalDmg == -1)
            {
                state = State.PlayerTurn;
                ShowTurnMenu(true);
                yield break;
            }
        }
        else 
        {
            // Fallback just in case no UI
            finalDmg = Random.Range(5, 15);
            yield return new WaitForSeconds(1f);
        }

        Debug.Log($"FIGHT! Dealt {finalDmg} damage.");

        // El bonus d'atac ja s'ha aplicat dins el SkillCheckUI per mostrar el número real boostat
        // finalDmg ja inclou el multiplicador del PlayerInventory. GetTotalAttackBonus()
        
        enemyCurrentHP -= finalDmg;
        if (enemyCurrentHP < 0) enemyCurrentHP = 0;
        UpdateStatsUI();

        // So i tremolor de l'enemic en rebre dany
        if (enemyHitSound) audioSource.PlayOneShot(enemyHitSound);
        if (enemyPortraitImage != null) StartCoroutine(ShakeEnemySprite(enemyPortraitImage.rectTransform, 0.35f, 14f));

        // Esperem un petit instant curt fins passar al torn enemic un cop ha donat l'espasada
        yield return new WaitForSeconds(0.6f);

        if (enemyCurrentHP == 0)
        {
            state = State.End;
            Debug.Log("ENEMY DEFEATED");
            StartCoroutine(DefeatAndVictoryRoutine());
            yield break;
        }

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

    // Tremolor de l'sprite de l'enemic en rebre dany
    private IEnumerator ShakeEnemySprite(RectTransform rt, float duration, float magnitude)
    {
        if (rt == null) yield break;
        Vector2 originalPos = rt.anchoredPosition;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float damping = 1f - Mathf.Clamp01(elapsed / duration); // Va decreixent
            float x = Random.Range(-1f, 1f) * magnitude * damping;
            float y = Random.Range(-1f, 1f) * magnitude * damping;
            rt.anchoredPosition = originalPos + new Vector2(x, y);
            yield return null;
        }
        rt.anchoredPosition = originalPos;
    }

    /// Quan l'enemic mor: primer l'escapcem en pixels, despres la pantalla de victoria.
    private IEnumerator DefeatAndVictoryRoutine()
    {
        // Baixem la música lentament en donar el cop definitiu (petició usuari)
        if (loader != null) StartCoroutine(loader.FadeCombatMusic(false, 2.5f));

        // Esperem a que la darrera animació de dany (shake) estigui cap al final
        yield return new WaitForSeconds(0.4f);

        // Els enemics ara poden dir unes últimes paraules abans de morir (E per seguir)
        if (encounter != null && encounter.enemyProfile != null && encounter.enemyProfile.deathReactions != null && encounter.enemyProfile.deathReactions.Length > 0)
        {
            string deathMsg = encounter.enemyProfile.deathReactions[Random.Range(0, encounter.enemyProfile.deathReactions.Length)];
            yield return EnemySpeakRoutine(deathMsg);
        }

        ShowTurnMenu(false);

        // Slide Out de tota la UI de combat cap amunt
        float outDur = 0.5f;
        Vector2 outOff = new Vector2(0, 400f);
        if (playerUIPanel != null) StartCoroutine(SlideOutRect(playerUIPanel, playerUIOriginalPos, outOff, outDur));
        if (enemyUIPanel != null) StartCoroutine(SlideOutRect(enemyUIPanel, enemyUIOriginalPos, outOff, outDur));
        if (playerNameText != null) StartCoroutine(SlideOutRect(playerNameText.rectTransform, playerNameOriginalPos, outOff, outDur));
        if (playerHPText != null) StartCoroutine(SlideOutRect(playerHPText.rectTransform, playerHPTextOriginalPos, outOff, outDur));
        if (enemyNameText != null) StartCoroutine(SlideOutRect(enemyNameText.rectTransform, enemyNameOriginalPos, outOff, outDur));
        if (enemyHPText != null) StartCoroutine(SlideOutRect(enemyHPText.rectTransform, enemyHPTextOriginalPos, outOff, outDur));

        // So de mort de l'enemic (des del seu perfil)
        AudioClip deathClip = encounter?.enemyProfile?.deathSound;
        if (deathClip) audioSource.PlayOneShot(deathClip);

        if (enemyPortraitImage != null && enemyPortraitImage.enabled)
        {
            bool fxDone = false;
            // Determina el canvas pare (el mateix que el panell de victoria usara)
            Transform canvasParent = enemyPortraitImage.transform.parent;
            EnemyDestroyFX.Play(enemyPortraitImage, () => fxDone = true);
            yield return new WaitUntil(() => fxDone);
            yield return new WaitForSeconds(0.25f); // petit respir
        }

        StartCoroutine(VictoryRoutine());
    }

    private IEnumerator VictoryRoutine()
    {
        ShowTurnMenu(false);
        // Baixem la música lentament per la victòria
        if (loader != null) StartCoroutine(loader.FadeCombatMusic(false, 3f));

        // So de victòria
        if (victorySound) audioSource.PlayOneShot(victorySound);

        if (enemyHPText) enemyHPText.text = "";
        if (playerHPText) playerHPText.text = "";

        // Càlcul de premis segons el perfil
        int gold = Random.Range(30, 80);
        System.Collections.Generic.List<string> earnedItems = new System.Collections.Generic.List<string>();

        if (encounter != null && encounter.enemyProfile != null)
        {
            gold = Random.Range(encounter.enemyProfile.goldRewardMin, encounter.enemyProfile.goldRewardMax + 1);
            
            if (encounter.enemyProfile.drops != null)
            {
                foreach (var drop in encounter.enemyProfile.drops)
                {
                    int prob = drop.probability;
                    while (prob >= 100)
                    {
                        earnedItems.Add(drop.itemName);
                        prob -= 100;
                    }
                    if (prob > 0 && Random.Range(0, 100) < prob)
                    {
                        earnedItems.Add(drop.itemName);
                    }
                }
            }
        }

        // Guarda HP restant del jugador i recompenses a l'inventari persistent
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

        // Mostra el panell animat de victòria
        Transform canvasParent = turnMenu != null ? turnMenu.transform.parent : transform;
        bool done = false;
        VictoryPanelUI.Create(canvasParent, gold, earnedItems, totalGold, () => done = true);

        yield return new WaitUntil(() => done);

        loader.EndCombat();
    }

    private void OnReason()
    {
        var bt = encounter?.enemyProfile?.socialBT;
        if (bt == null || bt.playerActions == null || bt.playerActions.Length == 0)
        {
            // NOU: Fallback amb narrador si no hi ha BT configurat
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

    private IEnumerator ReasonFallbackRoutine(string text)
    {
        ShowTurnMenu(false);
        state = State.Resolve;
        
        // Ús del so de veu personalitzat per aquest enemic si està definit
        AudioClip voice = (encounter?.enemyProfile != null) ? encounter.enemyProfile.reasonFallbackSound : null;
        
        // El text de Game Over es mostra molt lentament (x4 de temps, o sigui x0.25 de velocitat)
        yield return ShowPlayerActionDialogue(text, voice, 4.0f);
        EndPlayerTurn(""); // Simplement perdem el torn
    }

    // ─── Menú d'accions socials (node-graph) ─────────────────────────────────

    private IEnumerator SocialActionMenuRoutine(SocialBehaviorTree bt)
    {
        Transform canvasParent = turnMenu != null ? turnMenu.transform.parent : transform;
        TMPro.TextMeshProUGUI headerTxt = null;

        // Mostrem el text d'entrada del node actual (si n'hi ha)
        SocialNode currentNode = bt.GetNode(socialState.currentNodeId);
        if (currentNode != null && !string.IsNullOrEmpty(currentNode.enemyEntryText))
            yield return EnemySpeakRoutine(currentNode.enemyEntryText);

        // Construïm la llista d'accions final (accions BT + possible Perdonar)
        System.Collections.Generic.List<string> displayedActions = new System.Collections.Generic.List<string>(bt.playerActions);
        if (currentNode != null && currentNode.enableApology)
        {
            displayedActions.Add("Apologize");
        }

        // Construïm el panell d'accions (PANEL LATERAL ESQUERRA)
        socialMenuGO = new GameObject("SocialActionMenu");
        socialMenuGO.transform.SetParent(canvasParent, false);

        var panelRT = socialMenuGO.AddComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0f, 0.5f);
        panelRT.anchorMax = new Vector2(0f, 0.5f);
        panelRT.pivot = new Vector2(0f, 0.5f);
        panelRT.sizeDelta = new Vector2(400f, 700f);
        
        // Comença fora de la pantalla per l'esquerra
        Vector2 finalPos = new Vector2(50f, 0f);
        panelRT.anchoredPosition = new Vector2(-500f, 0f);

        var panelBg = socialMenuGO.AddComponent<Image>();
        panelBg.color = new Color(0.08f, 0.08f, 0.12f, 0.96f);
        panelBg.sprite = GetRoundedSprite();
        panelBg.type = Image.Type.Sliced;

        var outline = socialMenuGO.AddComponent<Outline>();
        outline.effectColor = new Color(0.95f, 0.8f, 0.15f, 0.6f);
        outline.effectDistance = new Vector2(5, -5);

        // Header
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

        // Contenidor vertical de botons
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
            
            // Color base depenent de si és "Apologize"
            if (actionName == "Apologize")
                btnImg.color = new Color(0.1f, 0.35f, 0.15f, 1f); // Verd fosc
            else
                btnImg.color = new Color(0.18f, 0.18f, 0.32f, 1f); // Blau fosc estàndard
            
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

        // Animació d'entrada (Slide in de l'esquerra)
        StartCoroutine(AnimateSideMenu(panelRT, finalPos, 0.5f));

        // Navegació per teclat (VERTICAL)
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
                
                if (trans != null && trans.requiredItem != null)
                {
                    if (PlayerInventory.Instance == null || PlayerInventory.Instance.CountItem(trans.requiredItem.itemName) <= 0)
                    {
                        // Mostrem l'error al propi panell d'accions (al header)
                        headerTxt.text = $"No tens: {trans.requiredItem.itemName}!";
                        headerTxt.color = new Color(1f, 0.3f, 0.3f); // Vermell d'error
                        
                        // Petit efecte de vibrat per donar feedback d'error
                        StartCoroutine(ShakeSideMenu(panelRT, finalPos));
                        
                        if (audioSource && takeDamageSound) audioSource.PlayOneShot(takeDamageSound, 0.5f);
                        
                        yield return new WaitForSeconds(1.2f);
                        
                        // Restaurem el header
                        headerTxt.text = bt.menuHeader;
                        headerTxt.color = new Color(1f, 0.92f, 0.2f);
                        continue; 
                    }
                }
                chosenActionIndex = keyboardIndex;
            }
            else if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
            {
                // Animació de sortida i tanca
                yield return StartCoroutine(AnimateSideMenu(panelRT, new Vector2(-400f, 0f), 0.3f));
                Destroy(socialMenuGO);
                state = State.PlayerTurn;
                ShowTurnMenu(true);
                yield break;
            }
            yield return null;
        }

        // Animació de sortida abans d'executar l'acció
        yield return StartCoroutine(AnimateSideMenu(panelRT, new Vector2(-400f, 0f), 0.3f));
        Destroy(socialMenuGO);

        // Processem l'acció
        string chosen = displayedActions[chosenActionIndex];
        if (confirmMenuSound && audioSource) audioSource.PlayOneShot(confirmMenuSound);

        // Busquem la transició al node actual
        SocialTransition transition = bt.GetTransition(currentNode, chosen);

        // NOU: Restar l'objecte si era necessari
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
            // Acció sense transició definida → reacció per defecte, quedem al mateix node
            reactionText = currentNode != null ? currentNode.defaultReactionText : "...";
            nextNodeId   = "";
        }

        // Mostrem diàleg narratiu del jugador (estil overworld) abans de la reacció de l'enemic
        string playerText = transition != null ? transition.playerActionText : null;
        if (!string.IsNullOrEmpty(playerText))
        {
            yield return StartCoroutine(ShowPlayerActionDialogue(playerText));
        }

        // Mostra reacció
        if (!string.IsNullOrEmpty(reactionText))
            yield return EnemySpeakRoutine(reactionText);

        // Moure a nou node
        socialState.MoveTo(nextNodeId);

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

    private IEnumerator AnimateSideMenu(RectTransform rect, Vector2 targetPos, float duration)
    {
        Vector2 startPos = rect.anchoredPosition;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float ease = 1f - Mathf.Pow(1f - t, 3f); // Ease out cubic
            rect.anchoredPosition = Vector2.Lerp(startPos, targetPos, ease);
            yield return null;
        }
        rect.anchoredPosition = targetPos;
    }

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

    private IEnumerator FriendVictoryRoutine()
    {
        ShowTurnMenu(false);

        // Mostrem el diàleg d'amistat (ara a través de la bombolla de l'enemic)
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

        // Càlcul d'or per resolució amistosa
        int friendGold = 0;
        if (bt != null && bt.friendGoldMax > 0)
        {
            friendGold = Random.Range(bt.friendGoldMin, bt.friendGoldMax + 1);
            if (PlayerInventory.Instance != null)
                PlayerInventory.Instance.AddGold(friendGold);
        }

        if (PlayerInventory.Instance != null && encounter?.enemyProfile != null)
        {
            PlayerInventory.Instance.RecruitEnemy(encounter.enemyProfile.enemyName);
        }

        Transform canvasParent = turnMenu != null ? turnMenu.transform.parent : transform;
        bool done = false;
        VictoryPanelUI.Create(canvasParent, friendGold, new System.Collections.Generic.List<string>(),
            PlayerInventory.Instance != null ? PlayerInventory.Instance.Gold : friendGold, () => done = true);

        yield return new WaitUntil(() => done);

        // ── Comprova si s'ha completat la barra de reclutament ──────
        // Guardem les dades per mostrar la recompensa UN COP FORA del combat
        if (PlayerInventory.Instance != null && encounter?.enemyProfile != null)
        {
            var completedProfile = PlayerInventory.Instance.CheckRecruitmentJustCompleted(encounter.enemyProfile.enemyName);
            if (completedProfile != null)
            {
                pendingRecruitReward = completedProfile;
            }
        }

        loader.EndCombat();
    }

    private void OnFlee()
    {
        ShowTurnMenu(false);
        state = State.Resolve; 
        StartCoroutine(FleeRoutine());
    }

    private IEnumerator FleeRoutine()
    {
        float fleeChance = 0.5f;
        if (encounter != null && encounter.enemyProfile != null)
        {
            fleeChance = encounter.enemyProfile.fleeProbability;
        }

        if (Random.value <= fleeChance)
        {
            // Fugida exitosa
            yield return ShowPlayerActionDialogue("You try to run away... and you make it!");
            
            state = State.End;
            loader.EndCombat();
        }
        else
        {
            // Falla
            yield return ShowPlayerActionDialogue("You try to run away... but you can't escape!");
            
            EndPlayerTurn("FLEE_FAIL");
        }
    }

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
            EndPlayerTurn("HEAL");
        }
    }

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

            // Text verd + partícules verdes (barra)
            Image targetImg = playerPortraitImage != null ? playerPortraitImage : playerHPFill;
            HealFXUI.ShowAboveBar(canvasParent, targetImg, $"+{profile.effectValue} HP",
                                  new Color(0.25f, 1f, 0.35f));

            // NOU: Efecte a pantalla completa
            HealFXUI.ShowHealFullscreen(canvasParent);
        }
        else if (profile.effectType == ItemEffectType.DamageEnemy)
        {
            // --- ANIMACIÓ DE TIRAR OBJECTE ---
            // Ara li passem un callback o esperem a que impacti per restar vida
            yield return StartCoroutine(AnimateItemThrow(profile, () => {
                // AQUEST CODI S'EXECUTA JUST EN EL MOMENT DE L'IMPACTE
                enemyCurrentHP -= profile.effectValue;
                if (enemyCurrentHP < 0) enemyCurrentHP = 0;
                UpdateStatsUI();

                if (enemyHitSound != null) audioSource.PlayOneShot(enemyHitSound);
                if (enemyPortraitImage != null) StartCoroutine(ShakeEnemySprite(enemyPortraitImage.rectTransform, 0.35f, 14f));

                // Taronja sobre la barra de l'enemic
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
            HealFXUI.Show(canvasParent, $"VELOC +{profile.effectValue}% ({speedBuffRoundsLeft} TORNS)", new Color(1f, 0.9f, 0.15f));
            
            // NOU: Efecte fletxes a pantalla completa
            HealFXUI.ShowSpeedFullscreen(canvasParent);
        }
        UpdateStatsUI();
        yield return null;
    }

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

        // 1. Punt d'Inici (Des del Inspector o fallback)
        Vector2 startPos = throwStartPoint != null ? throwStartPoint.anchoredPosition : new Vector2(0, -700f); 

        // 2. Punt de Col·lisió (Directament el centre del rival)
        Vector2 impactPos;
        if (enemyPortraitImage != null) impactPos = enemyPortraitImage.rectTransform.anchoredPosition;
        else if (enemyUIPanel != null) impactPos = enemyUIPanel.anchoredPosition;
        else impactPos = new Vector2(0, 300f);

        // 3. Línia de terra (On cau després de col·lisionar)
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

        // --- FASE 1: VOL FINS L'IMPACTE ---
        float duration = 0.5f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            Vector2 currentPos = Vector2.Lerp(startPos, impactPos, t);
            float parabola = 4f * t * (1f - t);
            currentPos.y += parabola * throwArcHeight;
            rt.anchoredPosition = currentPos;
            rt.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, t);
            rt.Rotate(0, 0, -800f * Time.deltaTime);
            yield return null;
        }

        // --- MOMENT DE L'IMPACTE ---
        onImpact?.Invoke();

        // --- FASE 2: CAURE AL TERRA ---
        elapsed = 0f;
        float fallDuration = 0.3f;
        Vector2 posAtImpact = rt.anchoredPosition;
        while (elapsed < fallDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fallDuration;
            // Cau de forma més directa (Ease In)
            rt.anchoredPosition = Vector2.Lerp(posAtImpact, groundPos, t * t);
            rt.Rotate(0, 0, -200f * Time.deltaTime);
            yield return null;
        }

        // Petit rebot al terra
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
    /// Mostra un diàleg estil overworld a la part inferior del combat
    /// per narrar l'acció del jugador. Espera que el jugador l'avanci amb E/Enter.
    /// </summary>
    private IEnumerator ShowPlayerActionDialogue(string text, AudioClip overrideVoice = null, float speedMultiplier = 1f)
    {
        // Creem un DialogueUI temporal per al combat
        var canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) { yield break; }

        var dialogGO = new GameObject("CombatDialogueUI");
        dialogGO.transform.SetParent(canvas.transform, false);
        var dialogUI = dialogGO.AddComponent<DialogueUI>();
        dialogUI.canSkip = false;
        
        // Apliquem la velocitat si és diferent a la normal
        if (speedMultiplier != 1f)
        {
            dialogUI.SetSpeedMultiplier(speedMultiplier); 
        }

        // Personalització de la veu: Prioritat al de l'enemic, fallback al global del combat
        AudioClip voice = (overrideVoice != null) ? overrideVoice : playerActionVoice;
        if (voice != null)
        {
            dialogUI.SetTypingSound(voice);
        }

        bool closed = false;
        dialogUI.OnDialogueClosed += () => closed = true;
        dialogUI.Show(text);

        // Esperem que el jugador avanci/tanqui el diàleg
        while (!closed)
        {
            if (!PauseMenuUI.IsOpen && (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)))
            {
                dialogUI.AdvanceOrSkip();
            }
            yield return null;
        }

        // Petit respir abans de la reacció de l'enemic
        yield return new WaitForSeconds(0.15f);

        if (dialogGO != null) Destroy(dialogGO);
    }

    private IEnumerator EnemySpeakRoutine(string text, float speedMultiplier = 1f, bool shake = false)
    {
        if (enemyBubbleRT == null || enemyDialogTxt == null || string.IsNullOrEmpty(text)) yield break;

        string cleanText = text.Trim();
        isEnemySpeaking = true;
        enemyBubbleRT.gameObject.SetActive(true);
        enemyDialogTxt.text = "";
        if (enemyBubblePromptCG != null) enemyBubblePromptCG.alpha = 0f;

        AudioClip voice = encounter?.enemyProfile?.voiceSound;
        
        // Paràmetres orgànics idèntics a DialogueUI
        float charsPerSecond = 45f;
        float delay = (1f / charsPerSecond) * speedMultiplier;

        for (int i = 0; i < cleanText.Length; i++)
        {
            char c = cleanText[i];
            if (enemyDialogTxt != null) enemyDialogTxt.text += c;
            
            // So de veu (cada 2 caràcters per no saturar)
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

            // Espera amb possibilitat de saltar (skip) immediat
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

        // Mostrem el prompt de [E]
        if (enemyBubblePromptCG != null) enemyBubblePromptCG.alpha = 1f;

        // Esperem input del jugador per tancar
        bool advance = false;
        yield return new WaitForSeconds(0.15f); // Prevenir tancat instantani si estavem pitjant
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

    private void EndPlayerTurn(string reactionType = "")
    {
        ShowTurnMenu(false);
        state = State.EnemyTurn;
        StartCoroutine(EnemyTurnRoutine(reactionType));
    }

    private Coroutine turnMenuAnim;

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
            // Amagar cap avall només si està de fet a l'escena:
            if (turnMenu.activeInHierarchy)
            {
                turnMenuAnim = StartCoroutine(SlideOutAndHide(turnMenu.GetComponent<RectTransform>(), turnMenuOriginalPos + new Vector2(0, -500f), 0.5f));
            }
        }
    }

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

    private IEnumerator SlideOutAndHide(RectTransform rect, Vector2 targetPos, float duration)
    {
        yield return SlideMenuTo(rect, targetPos, duration, false);
        turnMenu.SetActive(false);
    }

    private void SetHandsActive(bool active)
    {
        if (handControllers == null) return;
        foreach (var hand in handControllers)
        {
            if (hand != null) hand.canMove = active;
        }
    }

    // =========================
    // Enemy turn
    // =========================

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

        // Si hi havia un diàleg de fase en curs o pendent, esperem que s'alliberi el flag
        // El flag s'allibera a ApplyPhaseDialogueRoutine un cop s'ha tancat la bombolla
        while (isPhaseShiftingThisTurn) yield return null;

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
                        chosenPattern = currentPhaseAttacks[Random.Range(0, currentPhaseAttacks.Length)];
                    }
                    else if (encounter.enemyProfile.attackPatterns != null && encounter.enemyProfile.attackPatterns.Length > 0)
                    {
                        chosenPattern = encounter.enemyProfile.attackPatterns[Random.Range(0, encounter.enemyProfile.attackPatterns.Length)];
                    }
                }
                else if (encounter.attackPatterns != null && encounter.attackPatterns.Length > 0)
                {
                    chosenPattern = encounter.attackPatterns[Random.Range(0, encounter.attackPatterns.Length)];
                }
            }

            spawner.Configure(prefab, chosenPattern);
            yield return spawner.Run(dur);
            
            // Esperem un instant per assegurar que els últims projectils s'han registrat be
            yield return new WaitForSeconds(0.1f);

            // Wait until all projectiles have finished traveling and are destroyed
            yield return new WaitUntil(() => ProjectileUI.activeProjectiles <= 0);
        }
        else
        {
            yield return new WaitForSeconds(dur);
        }

        Debug.Log("ENEMY TURN ended");

        SetHandsActive(false);

        // Disminuïm i revisem l'estat del buff de velocitat en tornar al torn del jugador
        if (speedBuffRoundsLeft > 0)
        {
            speedBuffRoundsLeft--;
            if (speedBuffRoundsLeft == 0)
            {
                // El buff s'ha esgotat, el traiem!
                var hands = FindObjectsByType<HandController>(FindObjectsSortMode.None);
                foreach (var h in hands) h.speedMultiplier -= currentSpeedBuffValue;
                currentSpeedBuffValue = 0f;
                Debug.Log("Buff de velocitat de mans exhaurit!");
            }
        }

        // Assegurem que qualsevol diàleg de l'enemic es tanca abans de mostrar el menú del jugador
        if (enemyBubbleRT != null && enemyBubbleRT.gameObject.activeSelf)
        {
            enemyBubbleRT.gameObject.SetActive(false);
            isEnemySpeaking = false;
            isPhaseShiftingThisTurn = false;
        }

        // Si el jugador ha mort durant el torn de l'enemic (projectils), aturem-ho tot abans de donar-li el torn.
        if (state == State.End)
        {
            yield break;
        }

        state = State.PlayerTurn;
        ShowTurnMenu(true);
    }
}
