using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Motor central de renderitzat i control de diàlegs convencionals (DialogueUI).
/// Aquesta classe actua com a "front-end" de l'Overworld, gestionant la maquetació procedimental
/// del panell de text, els retrats facials animats, les bombolles de pensament, les bifurcacions
/// de decisions (Choices) i l'efecte de text teclejat progressiu (Typewriter).
/// 
/// DISSENY I IMPLEMENTACIÓ PER AL TFG:
/// - **Dinamisme retro**: Animació per codi de les tecles interactives físiques 'E' i 'F', simulant
///   pressió tridimensional (3D keypress).
/// - **Graelles dinàmiques animades**: Les respostes ramificades s'animen de forma asíncrona lliscant
///   des de l'esquerra de forma escalonada, acompanyades d'un so amb Pitch incremental (escala musical).
/// - **Fons de núvol procedimental**: Genera píxel a píxel a la memòria una textura tileable en mode pensament.
/// - **Gestió de prioritat i represa de combat**: Interromp visualment la conversa de forma suau per carregar batalles
///   i la reprèn automàticament en el punt exacte en finalitzar la contesa.
/// </summary>
public class DialogueUI : MonoBehaviour
{
    [Header("Typewriter (Efecte Tecleig)")]
    public float charsPerSecond = 40f; // Velocitat estàndard de redacció en lletres/segon
    [SerializeField] private bool skipSpaces = true; // Si és cert, no reprodueix el so de clic en espais buits
    [SerializeField] private int soundEveryNChars = 2; // Freqüència amb què sona el so de veu per estalviar soroll

    [Header("Sons de Tecleig")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] typingClips; // Col·lecció de sons de murmuri retro
    [Range(0f, 0.4f)][SerializeField] private float pitchRandom = 0.05f; // Micro-modulació de veu
    [Range(0f, 1f)][SerializeField] private float volume = 0.8f;

    [Header("Sons de Ramificacions (Choices)")]
    [SerializeField] private AudioClip choiceAppearSound; // So al dibuixar-se cada botó d'elecció
    [SerializeField] private float choiceBasePitch = 1.0f; // Pitch inicial per a la primera opció
    [SerializeField] private float choicePitchIncrement = 0.15f; // Increment de pitch per crear escala musical en les opcions

    [Header("Animació del Panell")]
    [SerializeField] private float animDuration = 0.4f; // Temps en segons de les foses
    [SerializeField] private bool animateOnShow = true; // Lliscament d'obertura actiu
    public bool canSkip = true;
    public bool canAdvance = true;
    public static bool ForceDisableSkipGlobals { get; set; } = false; // Flag global de depuració per a cinemàtiques

    private Coroutine typingRoutine;
    private Coroutine animRoutine;
    private Coroutine autoAdvanceRoutine; // Rutina d'avanç automàtic temporitzat

    private string fullText; // Text net final a escriure
    private bool isOpen;
    public bool IsOpen => isOpen;
    private bool isTyping; // Flag que indica si s'està escrivint caràcters actualment
    private int typedCount;

    public bool WasSkipped { get; private set; } // Si s'ha cancel·lat la conversa prement Skip
    public System.Action OnDialogueClosed; // Notificador per alliberar el personatge en el mapa

    [Header("Referències de la UI (Generades Procedimentalment)")]
    [SerializeField] private GameObject currentPanelGO;
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private CanvasGroup panelGroup;
    public TextMeshProUGUI dialogueText;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Image portraitImage;
    [SerializeField] private Animator portraitAnimator;
    [SerializeField] private GameObject nameBoxGO;
    [SerializeField] private CanvasGroup eBtnGroup;   // Control d'opacitat indicador "E"
    [SerializeField] private RectTransform eTextRT;  // Tecla física animada "E"
    [SerializeField] private CanvasGroup fBtnGroup;   // Control d'opacitat de l'indicador "F"
    [SerializeField] private RectTransform fTextRT;  // Tecla física animada "F"
    [SerializeField] private RectTransform fContainerRT; // Contenidor de l'indicador Skip
    private float skipHoldTime;
    private const float SkipHoldRequired = 0.5f; // Temps en segons necessari per saltar
    private Vector3[] panelCorners = new Vector3[4]; // Buffer reusable per evitar escombraries (Zero Garbage Collector)

    private bool lastEPressed;
    private bool lastFPressed;
    private RectTransform dividerRT;
    private RectTransform portRT;
    private Image panelBgImg;
    private Outline panelBgOl;

    private Vector2 shownPos = Vector2.zero;
    private bool isHidingForCombat = false; // S'activa si tanquem el diàleg temporalment per a una batalla
    private bool eventAlreadyFired = false; // Evita la re-execució doble d'esdeveniments de Unity en saltar

    private float currentSpeedMultiplier = 1f;

    /// <summary>
    /// Permet alterar de forma temporal la velocitat del typewriter (ex: per a pressa en cinemàtiques).
    /// </summary>
    public void SetSpeedMultiplier(float multiplier)
    {
        if (multiplier > 0)
            currentSpeedMultiplier = multiplier;
    }

    private Interactable.DialogueLine[] sequence; // Llista ordenada de línies
    private int seqIndex;
    private bool inSequence;
    private bool isReopening; // Evita conflictes en foses de tancament-reobertura consecutives
    private bool isCurrentOnTop; // S'ha de mostrar a la vora superior?
    private bool isHiding;
    
    private AudioClip currentLineVoice;

    // --- Branques de diàleg (Choices) ---
    private Interactable.DialogueLine currentLine;
    private Interactable.DialogueChoice[] currentLineChoices;
    private bool isSelectingChoice; // Cert si estem esperant la decisió de l'usuari
    private int selectedChoiceIdx; // Índex seleccionat actualment
    private RectTransform choicePanelRT; // Panell contenidor de respostes
    private System.Collections.Generic.List<TextMeshProUGUI> choiceTexts = new System.Collections.Generic.List<TextMeshProUGUI>();
    private System.Collections.Generic.List<Interactable.DialogueChoice> visibleChoices = new System.Collections.Generic.List<Interactable.DialogueChoice>();

    private void Awake()
    {
        if (!audioSource) audioSource = GetComponent<AudioSource>();
    }

    private void Update()
    {
        if (PauseMenuUI.IsOpen) return;

        if (isOpen && !isHiding && !isReopening && !isHidingForCombat && currentPanelGO != null && currentPanelGO.activeSelf)
        {
            // ── LÒGICA VISUAL DE LA TECLA F (SALT DE CONVERSA) ──
            bool lineAllowsSkip = canSkip && !ForceDisableSkipGlobals && 
                (currentLine == null || (!currentLine.cannotSkip && (currentLine.owner == null || !currentLine.owner.CannotSkipDialogue)));

            if (lineAllowsSkip)
            {
                if (Input.GetKey(KeyCode.F))
                {
                    skipHoldTime += Time.unscaledDeltaTime;
                    if (fTextRT != null) fTextRT.anchoredPosition = Vector2.zero; // Tecla visualment enfonsada
                    
                    if (skipHoldTime >= SkipHoldRequired)
                    {
                        // S'ha completat el temps de pressió: cancel·lem la conversa
                        skipHoldTime = 0f;
                        WasSkipped = true;
                        Hide();
                        return;
                    }
                }
                else
                {
                    skipHoldTime = 0f;
                    // Efecte pampallugues polsat/no-polsat retro
                    float fCycle = Time.unscaledTime * 1.5f + 0.5f; 
                    bool fIsPressed = (fCycle % 1f) > 0.7f;
                    if (fIsPressed != lastFPressed)
                    {
                        lastFPressed = fIsPressed;
                        if (fTextRT != null) fTextRT.anchoredPosition = fIsPressed ? Vector2.zero : new Vector2(0f, 4f);
                    }
                }

                if (fBtnGroup != null) fBtnGroup.alpha = 1f;
            }
            else
            {
                if (fBtnGroup != null) fBtnGroup.alpha = 0f;
                skipHoldTime = 0f;
            }

            // ── LÒGICA VISUAL DE LA TECLA E (AVANÇAR TEXT) ──
            if (eBtnGroup != null && eTextRT != null)
            {
                if (!isTyping && !isSelectingChoice && canAdvance)
                {
                    eBtnGroup.alpha = 1f;
                    float cycle = Time.unscaledTime * 1.5f;
                    bool isPressed = (cycle % 1f) > 0.7f;
                    if (isPressed != lastEPressed)
                    {
                        lastEPressed = isPressed;
                        eTextRT.anchoredPosition = isPressed ? Vector2.zero : new Vector2(0f, 4f); // Simula el botó pujant/baixant
                    }
                }
                else
                {
                    eBtnGroup.alpha = 0f;
                }
            }

            // Reposicionament dinàmic de la barra de Skip perquè segueixi sempre la cantonada inferior del diàleg
            if (fContainerRT != null && panelRect != null)
            {
                panelRect.GetWorldCorners(panelCorners); 
                Vector3 bottomLeft = panelCorners[0];
                fContainerRT.position = new Vector3(bottomLeft.x, bottomLeft.y - 10f, bottomLeft.z);
            }
        }
        else
        {
            if (eBtnGroup != null) eBtnGroup.alpha = 0f;
            if (fBtnGroup != null) fBtnGroup.alpha = 0f;
            skipHoldTime = 0f;
        }

        // ── NAVEGACIÓ DELS MENÚS DE SELECCIÓ (CHOICES) ──
        if (isSelectingChoice && choiceTexts.Count > 0)
        {
            bool left = Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow);
            bool right = Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow);
            bool up = Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow);
            bool down = Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow);
            bool moved = false;

            int cols = 4; // Distribució en graella de 4 columnes màxim

            if (left) { selectedChoiceIdx--; if (selectedChoiceIdx < 0) selectedChoiceIdx = choiceTexts.Count - 1; moved = true; }
            if (right) { selectedChoiceIdx++; if (selectedChoiceIdx >= choiceTexts.Count) selectedChoiceIdx = 0; moved = true; }

            if (up) 
            { 
                if (selectedChoiceIdx >= cols) selectedChoiceIdx -= cols; 
                else selectedChoiceIdx = (choiceTexts.Count - 1) - ((choiceTexts.Count - 1) % cols) + selectedChoiceIdx;
                if (selectedChoiceIdx >= choiceTexts.Count) selectedChoiceIdx = choiceTexts.Count - 1;
                moved = true; 
            }
            if (down) 
            { 
                selectedChoiceIdx += cols; 
                if (selectedChoiceIdx >= choiceTexts.Count) selectedChoiceIdx %= cols; 
                moved = true; 
            }

            if (moved) 
            {
                HighlightChoice(); // Brillantor contorn opció seleccionada
                if (PlayerInventory.Instance != null && PlayerInventory.Instance.navSound != null)
                {
                    ItemSoundPlayer.Play(PlayerInventory.Instance.navSound); // So de moviment de cursor
                }
            }
        }
    }

    public bool IsTyping => isTyping;

    public void SetTypingSound(AudioClip clip, int every = 2)
    {
        if (clip != null) typingClips = new AudioClip[] { clip };
        soundEveryNChars = every;
    }

    /// <summary>
    /// Activa la seqüència ordenada de línies de diàleg de forma lineal.
    /// </summary>
    public void StartDialogue(Interactable.DialogueLine[] lines, bool animateIn = true)
    {
        WasSkipped = false;
        isHidingForCombat = false;
        
        // Bloqueig de seguretat en combats actius excepte si la baralla ja s'ha donat per completada
        if (CombatLoader.IsInCombat)
        {
            var cm = FindFirstObjectByType<CombatManager>();
            if (cm == null || !cm.IsEnded) return; 
        }
        
        if (lines == null || lines.Length == 0)
        {
            Hide(); 
            return;
        }

        BuildDynamicUI(); // Construcció dynamic procedimental

        sequence = lines;
        seqIndex = 0;
        inSequence = true;

        // Si hi ha un delay inicial, desnativem la caixa i programem l'espera
        if (sequence[0].delayBeforeLine > 0f)
        {
            StartCoroutine(FirstLineDelayRoutine(sequence[0], animateIn));
        }
        else
        {
            ShowInternal(sequence[0], playInAnim: animateIn);
        }
    }

    private IEnumerator FirstLineDelayRoutine(Interactable.DialogueLine firstLine, bool animateIn)
    {
        isReopening = true; 
        if (currentPanelGO != null) currentPanelGO.SetActive(false);
        yield return new WaitForSecondsRealtime(firstLine.delayBeforeLine);
        isReopening = false;
        if (currentPanelGO != null) currentPanelGO.SetActive(true);
        ShowInternal(firstLine, playInAnim: animateIn);
    }

    /// <summary>
    /// Mètode simple legacy per llançar missatges estàtics ràpids.
    /// </summary>
    public void Show(string text, Sprite portrait = null, RuntimeAnimatorController portraitAnim = null)
    {
        WasSkipped = false;
        isHidingForCombat = false;
        inSequence = false;
        sequence = null;
        seqIndex = 0;

        BuildDynamicUI();

        ShowInternal(new Interactable.DialogueLine
        {
            text = text,
            portrait = portrait,
            portraitAnimator = portraitAnim
        }, playInAnim: true);
    }

    /// <summary>
    /// Mètode intern d'engegada visual de la caixa de diàleg.
    /// Actualitza textos, noms, posicions d'avatar i efectua els canvis físics gràfics configurats.
    /// </summary>
    private void ShowInternal(Interactable.DialogueLine line, bool playInAnim)
    {
        if (autoAdvanceRoutine != null) { StopCoroutine(autoAdvanceRoutine); autoAdvanceRoutine = null; }

        isOpen = true;
        currentLine = line;
        fullText = line?.text ?? "";
        typedCount = 0;

        // Executem els UnityEvents si no ho hem fet en el pas previ de preparació
        if (!eventAlreadyFired)
        {
            line?.onLineReached?.Invoke();
        }
        eventAlreadyFired = false; 

        // ── CANVI DE SPRITE EMERGENT (INTERACTABLES ACTIUS) ──
        if (line != null)
        {
            if (line.interactableSpriteChange != null)
            {
                SpriteRenderer sr = line.targetSpriteRenderer;
                if (sr == null && line.owner != null)
                {
                    sr = line.owner.GetComponent<SpriteRenderer>();
                }

                if (sr != null) 
                {
                    sr.sprite = line.interactableSpriteChange;
                    if (line.interactableSpriteChangeSound != null)
                    {
                        ItemSoundPlayer.Play(line.interactableSpriteChangeSound);
                    }
                }
            }
            
            // Re-assigna els estats de memòria dels diàlegs
            if (line.setNextInteractionVersion >= 0 && line.owner != null)
            {
                line.owner.SetNextInteractionVersion(line.setNextInteractionVersion);
            }
        }

        isCurrentOnTop = (line != null && line.showOnTop);
        currentLineVoice = line != null ? line.customVoiceSound : null;
        currentLineChoices = line?.choices;
        isSelectingChoice = false;

        bool hasPortrait = (line != null && line.portrait != null && !line.isThought);

        // Apliquem la distribució dinàmica del panell (esquerra, dreta, núvol, etc.)
        LayoutUI(line != null && line.isRightSide, isCurrentOnTop, hasPortrait, line != null && line.isThought);

        // Dibuixat de caixes de nom
        if (!string.IsNullOrEmpty(line?.speakerName))
        {
            nameBoxGO.SetActive(true);
            nameText.text = line.speakerName;
        }
        else
        {
            nameBoxGO.SetActive(false);
        }

        // Imatge d'avatar
        if (portraitImage != null)
        {
            if (line != null && line.portrait != null)
                portraitImage.sprite = line.portrait;

            portraitImage.color = (portraitImage.sprite != null) ? Color.white : new Color(1,1,1,0);
        }

        if (portraitAnimator != null)
        {
            if (line != null && line.portraitAnimator != null)
                portraitAnimator.runtimeAnimatorController = line.portraitAnimator;
            portraitAnimator.enabled = (portraitAnimator.runtimeAnimatorController != null);
        }

        if (currentPanelGO != null) currentPanelGO.SetActive(true);

        if (playInAnim && animateOnShow) PlayIn();
        else ForceShown();

        // ── AUTOSIZING DE TEXT PREVI (Evita problemes d'escala amb TextMeshPro) ──
        if (dialogueText != null)
        {
            dialogueText.enableAutoSizing = true;
            dialogueText.text = fullText;
            dialogueText.ForceMeshUpdate();
            dialogueText.enableAutoSizing = false; // Desactivem després de trobar la mida perfecta
            dialogueText.maxVisibleCharacters = 0;
        }

        if (typingRoutine != null) StopCoroutine(typingRoutine);
        typingRoutine = StartCoroutine(TypeRoutine(fullText));
    }

    /// <summary>
    /// Tanca completament la interfície, neteja els subpanells i restableix els multiplicadors.
    /// </summary>
    public void Hide()
    {
        currentSpeedMultiplier = 1f; // IMPORTANT: Resetejem el multiplicador temporal

        if (isHiding) return;
        isHiding = true;

        inSequence = false;
        sequence = null;
        seqIndex = 0;

        canAdvance = true; 
        isTyping = false;
        isSelectingChoice = false;
        isHidingForCombat = false;
        if (autoAdvanceRoutine != null) { StopCoroutine(autoAdvanceRoutine); autoAdvanceRoutine = null; }

        if (choicePanelRT != null) 
        {
            foreach(Transform child in choicePanelRT) Destroy(child.gameObject);
        }
        choiceTexts.Clear();

        if (typingRoutine != null) StopCoroutine(typingRoutine);
        typingRoutine = null;

        PlayOut();
    }

    /// <summary>
    /// Avança a la següent línia de diàleg o escriu de cop el text actiu si estava a mig teclejar.
    /// </summary>
    public void AdvanceOrSkip()
    {
        if (!isOpen || isReopening || isHiding || isHidingForCombat) return;
        if (autoAdvanceRoutine != null) { StopCoroutine(autoAdvanceRoutine); autoAdvanceRoutine = null; }

        // Si està en mig del typewriter, completem el text immediatament
        if (isTyping)
        {
            if (typingRoutine != null) StopCoroutine(typingRoutine);
            typingRoutine = null;

            isTyping = false;
            if (dialogueText)
            {
                dialogueText.text = fullText;
                dialogueText.maxVisibleCharacters = fullText.Length;
            }
            
            // Dibuixem eleccions directament
            if (currentLineChoices != null && currentLineChoices.Length > 0)
            {
                ShowChoices();
            }
            else
            {
                var currentLine = sequence != null && inSequence ? sequence[seqIndex] : null;
                if (currentLine != null && currentLine.autoAdvanceTime > 0f)
                {
                    autoAdvanceRoutine = StartCoroutine(AutoAdvanceRoutine(currentLine.autoAdvanceTime));
                }
            }
            return;
        }

        // Si estem seleccionant branques de resposta
        if (isSelectingChoice)
        {
            var choice = visibleChoices[selectedChoiceIdx];

            // Reproducció so de selecció
            if (choice.customSelectSound != null)
            {
                if (audioSource != null) audioSource.PlayOneShot(choice.customSelectSound);
            }
            else if (PlayerInventory.Instance != null && PlayerInventory.Instance.selectSound != null)
            {
                ItemSoundPlayer.Play(PlayerInventory.Instance.selectSound);
            }

            // Si s'han d'ocultar opcions completes i no és repetible, la guardem a la motxilla
            var currentLine = sequence != null && inSequence && seqIndex < sequence.Length ? sequence[seqIndex] : null;
            bool shouldHideSeen = currentLine != null && currentLine.owner != null && currentLine.owner.HideSeenChoices;
            
            if (shouldHideSeen)
            {
                if (!choice.repeatable && PlayerInventory.Instance != null)
                {
                    PlayerInventory.Instance.MarkChoiceSeen(choice.text);
                }
            }

            choice.onChoiceSelected?.Invoke(); // Execució de l'elecció (ex: iniciar combat)
            
            isSelectingChoice = false;
            foreach(Transform child in choicePanelRT) Destroy(child.gameObject);
            choiceTexts.Clear();
            visibleChoices.Clear();
            
            int nextIdx = choice.jumpToLineIndex >= 0 ? choice.jumpToLineIndex : seqIndex + 1;

            // Si hem obert una botiga o un menú congelador de temps, esperem que es tanqui per seguir
            if (ShopMenuUI.IsOpen || Time.timeScale == 0f)
            {
                StartCoroutine(WaitMenuCloseAndAdvance(nextIdx));
            }
            else
            {
                AdvanceToLine(nextIdx);
            }
            return;
        }

        // Navegació lineal estàndard
        if (inSequence && sequence != null)
        {
            if (currentLineChoices != null && currentLineChoices.Length > 0) return; // Forcem triar opció
            
            var currentLine = sequence[seqIndex];
            if (currentLine.isEndNode)
            {
                Hide();
                return;
            }

            int nextIdx = currentLine.jumpToLineIndex >= 0 ? currentLine.jumpToLineIndex : seqIndex + 1;

            // ── PRIORITAT DE TANCAMENT PER COMBATS INMEDIATS ──
            if (nextIdx >= 0 && nextIdx < sequence.Length)
            {
                var nextLine = sequence[nextIdx];
                
                // Si la següent línia conté UnityEvents (iniciadors de batalles), 
                // tanquem primer de forma elegant i després processem
                if (nextLine.onLineReached != null && nextLine.onLineReached.GetPersistentEventCount() > 0)
                {
                    StartCoroutine(CloseExecuteAndPause(nextLine, nextIdx));
                    return;
                }
            }

            AdvanceToLine(nextIdx);
            return;
        }

        Hide();
    }

    /// <summary>
    /// Corrutina utilitzada per amagar de cop la visual de diàlegs, disparar esdeveniments
    /// que congelen el temps del món (ex. batalles) i esperar asíncronament a reprendre.
    /// </summary>
    private IEnumerator CloseExecuteAndPause(Interactable.DialogueLine line, int nextIdx)
    {
        Debug.Log($"[DialogueUI] CloseExecuteAndPause started. nextIdx: {nextIdx}, line: {line?.text}");
        isHidingForCombat = true;
        seqIndex = nextIdx; // Deixem l'índex llest

        // Tanquem el panell elegantment de cara a la transició de combat SENSE disparar l'esdeveniment de tancament final!
        PlayOutForReopen();
        yield return new WaitForSecondsRealtime(animDuration + 0.05f);
        
        if (currentPanelGO != null) currentPanelGO.SetActive(false);
        isOpen = false;

        // Disparem el combat o animació cinemàtica
        Debug.Log("[DialogueUI] CloseExecuteAndPause invoking line.onLineReached...");
        line.onLineReached?.Invoke();
        eventAlreadyFired = true; 

        // Si l'esdeveniment no era un combat (i per tant no hem perdut el focus ni s'està carregant un), reactivem dinàmicament
        Debug.Log($"[DialogueUI] CloseExecuteAndPause checked. IsInCombat: {CombatLoader.IsInCombat}, IsCombatLoading: {CombatLoader.IsCombatLoading}");
        if (!CombatLoader.IsInCombat && !CombatLoader.IsCombatLoading)
        {
            Debug.Log("[DialogueUI] Not a combat, calling ResumeAfterCombat immediately.");
            ResumeAfterCombat();
        }
    }

    /// <summary>
    /// Cridat pel CombatLoader al descarregar l'escena de combat per rependre de forma transparent la conversa.
    /// </summary>
    public void ResumeAfterCombat()
    {
        Debug.Log($"[DialogueUI] ResumeAfterCombat called. isHidingForCombat: {isHidingForCombat}, inSequence: {inSequence}, seqIndex: {seqIndex}");
        isHidingForCombat = false;

        if (inSequence && sequence != null && seqIndex < sequence.Length)
        {
            Debug.Log($"[DialogueUI] Resuming sequence line at {seqIndex}: {sequence[seqIndex]?.text}");
            if (currentPanelGO != null) currentPanelGO.SetActive(true);
            eventAlreadyFired = true; // Forcem que no es torni a disparar recursivament l'esdeveniment del combat
            
            // Congelem el moviment del jugador per si de cas s'havia desbloquejat
            var player = FindFirstObjectByType<PlayerController2D>();
            if (player != null) player.LockMovement();

            ShowInternal(sequence[seqIndex], true);
        }
        else
        {
            Debug.Log($"[DialogueUI] Resume failed, sequence/seqIndex bounds check failed. Hiding.");
            Hide();
        }
    }

    private void AdvanceToLine(int nextIdx)
    {
        if (sequence == null) { Hide(); return; }
        
        seqIndex = nextIdx;
        if (seqIndex >= 0 && seqIndex < sequence.Length)
        {
            var nextLine = sequence[seqIndex];
            if (nextLine.delayBeforeLine > 0f) StartCoroutine(DelayedShowRoutine(nextLine));
            else if (nextLine.forceReopen) StartCoroutine(ReopenRoutine(nextLine));
            else ShowInternal(nextLine, false);
            return;
        }
        Hide();
    }

    private IEnumerator DelayedShowRoutine(Interactable.DialogueLine nextLine)
    {
        isReopening = true; 
        PlayOutForReopen(); 
        yield return new WaitForSecondsRealtime(animDuration); 
        yield return new WaitForSecondsRealtime(nextLine.delayBeforeLine); 
        isReopening = false;
        ShowInternal(nextLine, playInAnim: true); 
    }

    private System.Collections.IEnumerator WaitMenuCloseAndAdvance(int nextIdx)
    {
        if (currentPanelGO != null) currentPanelGO.SetActive(false); // Amaguem mentre comprem

        while (ShopMenuUI.IsOpen || Time.timeScale == 0f)
        {
            yield return null;
        }
        AdvanceToLine(nextIdx); // Reprenem quan es tanqui la botiga
    }

    private IEnumerator ReopenRoutine(Interactable.DialogueLine nextLine)
    {
        isReopening = true;
        PlayOutForReopen(); 
        yield return new WaitForSecondsRealtime(animDuration + 0.05f);
        isReopening = false;
        ShowInternal(nextLine, playInAnim: true);
    }

    /// <summary>
    /// Corrutina del typewriter orgànic de lletres.
    /// Executa salts i esperes més llargues de forma adaptativa en punts o comes per simular ritme de veu.
    /// </summary>
    private IEnumerator TypeRoutine(string text)
    {
        isTyping = true;
        string cleanText = text.Trim();
        
        // Calculem velocitat escalada pel multiplicador
        float currentSpeed = Mathf.Max(1f, charsPerSecond * currentSpeedMultiplier);
        float delay = 1f / currentSpeed;

        if (dialogueText)
        {
            dialogueText.text = cleanText;
            dialogueText.maxVisibleCharacters = 0;
        }

        for (int i = 0; i < cleanText.Length; i++)
        {
            char c = cleanText[i];
            if (dialogueText) dialogueText.maxVisibleCharacters = i + 1;

            bool shouldSound = true;
            if (skipSpaces && char.IsWhiteSpace(c)) shouldSound = false;

            if (shouldSound)
            {
                typedCount++;
                if (soundEveryNChars <= 1 || (typedCount % soundEveryNChars == 0))
                    PlayRandomTypingSound();
            }

            float currentDelay = delay;
            
            // ── RITME I ENFASI DE VEU RPG (PAUSES ORGÀNIQUES) ──
            if (c == '.' || c == '?' || c == '!')
            {
                currentDelay = delay * 10f; // Pausa gran en fi de frase
            }
            else if (c == ',' || c == ';' || c == ':' || c == '-')
            {
                currentDelay = delay * 5f; // Pausa suau
            }

            yield return new WaitForSecondsRealtime(currentDelay);
        }

        isTyping = false;
        typingRoutine = null;

        if (audioSource != null) audioSource.pitch = 1f;

        if (currentLineChoices != null && currentLineChoices.Length > 0)
        {
            ShowChoices();
        }
        else
        {
            var currentLine = sequence != null && inSequence ? sequence[seqIndex] : null;
            if (currentLine != null && currentLine.autoAdvanceTime > 0f)
            {
                autoAdvanceRoutine = StartCoroutine(AutoAdvanceRoutine(currentLine.autoAdvanceTime));
            }
        }
    }

    private IEnumerator AutoAdvanceRoutine(float time)
    {
        yield return new WaitForSecondsRealtime(time);
        if (isOpen && !isHiding && !isReopening && !isTyping && !isSelectingChoice)
        {
            AdvanceOrSkip();
        }
    }

    /// <summary>
    /// Emet un murmuri al teclejar. Selecciona un so a l'atzar de les llistes per donar varietat orgànica.
    /// </summary>
    private void PlayRandomTypingSound()
    {
        if (currentLineVoice != null)
        {
            if (!audioSource) 
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 0f;
            }
            audioSource.pitch = 1f + Random.Range(-pitchRandom, pitchRandom);
            audioSource.clip = currentLineVoice;
            audioSource.volume = volume;
            audioSource.Play();
            return;
        }

        if (typingClips != null && typingClips.Length > 0)
        {
            if (!audioSource) 
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 0f;
            }
            audioSource.clip = typingClips[Random.Range(0, typingClips.Length)];
            audioSource.pitch = 1f + Random.Range(-pitchRandom * 0.5f, pitchRandom * 0.5f);
            audioSource.volume = volume;
            audioSource.Play();
            return;
        }

        var inv = PlayerInventory.Instance;
        if (inv != null && inv.shopVoiceSound != null)
        {
            if (!audioSource) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.clip = inv.shopVoiceSound;
            audioSource.pitch = 1f + Random.Range(-0.1f, 0.1f);
            audioSource.volume = 0.5f;
            audioSource.Play();
        }
    }

    // =========================================================================
    // UI Builder Procedimental (Creació Gràfica dels Elements de Diàleg)
    // =========================================================================
    private void BuildDynamicUI()
    {
        if (currentPanelGO != null && dialogueText != null && nameText != null) return;
        
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        Canvas targetCanvas = null;
        foreach (var c in canvases)
        {
            if (c.name != "EndCanvas" && c.name != "AlertCanvas")
            {
                targetCanvas = c;
                break;
            }
        }
        if (targetCanvas == null && canvases.Length > 0) targetCanvas = canvases[0];
        if (targetCanvas == null) return;
        
        var canvas = targetCanvas;

        currentPanelGO = new GameObject("DynamicDialoguePanel");
        panelRect = currentPanelGO.AddComponent<RectTransform>();
        panelRect.SetParent(canvas.transform, false);

        Canvas diagCanvas = currentPanelGO.AddComponent<Canvas>();
        diagCanvas.overrideSorting = true;
        diagCanvas.sortingOrder = 9999;
        currentPanelGO.AddComponent<GraphicRaycaster>();

        panelRect.anchorMin = new Vector2(0.12f, 0.05f);
        panelRect.anchorMax = new Vector2(0.88f, 0.35f); 
        panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
        shownPos = panelRect.anchoredPosition;

        panelGroup = currentPanelGO.AddComponent<CanvasGroup>();
        panelGroup.alpha = 0f;

        panelBgImg = currentPanelGO.AddComponent<Image>();
        panelBgImg.color = new Color(0.08f, 0.08f, 0.16f, 0.95f);
        panelBgOl = currentPanelGO.AddComponent<Outline>();
        panelBgOl.effectColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        panelBgOl.effectDistance = new Vector2(6f, -6f);

        // Retrat caixa
        var portGO = new GameObject("PortraitBox");
        portGO.transform.SetParent(panelRect, false);
        portRT = portGO.AddComponent<RectTransform>();

        // Imatge retrat
        var portImgGO = new GameObject("Img");
        portImgGO.transform.SetParent(portRT, false);
        var piRT = portImgGO.AddComponent<RectTransform>();
        piRT.anchorMin = Vector2.zero; piRT.anchorMax = Vector2.one;
        piRT.offsetMin = piRT.offsetMax = Vector2.zero;
        portraitImage = portImgGO.AddComponent<Image>();
        portraitImage.preserveAspect = true;
        portraitImage.color = new Color(1, 1, 1, 0); 
        portraitAnimator = portImgGO.AddComponent<Animator>();

        // Text caixa
        var txtGO = new GameObject("TextBox");
        txtGO.transform.SetParent(panelRect, false);
        var tRT = txtGO.AddComponent<RectTransform>();
        
        dialogueText = txtGO.AddComponent<TextMeshProUGUI>();
        dialogueText.margin = new Vector4(35f, 30f, 35f, 30f);
        dialogueText.textWrappingMode = TextWrappingModes.Normal;
        dialogueText.fontSizeMax = 80f; dialogueText.fontSizeMin = 40f; 
        dialogueText.enableAutoSizing = true;
        SetFont(dialogueText, 60f, Color.white, FontStyles.Normal, TextAlignmentOptions.TopLeft);

        // Caixa Nom
        nameBoxGO = new GameObject("NameBox");
        nameBoxGO.transform.SetParent(panelRect, false);
        var nbRT = nameBoxGO.AddComponent<RectTransform>();
        var nbImg = nameBoxGO.AddComponent<Image>();
        nbImg.color = new Color(0.15f, 0.25f, 0.4f, 1f);
        var nbOl = nameBoxGO.AddComponent<Outline>();
        nbOl.effectColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        nbOl.effectDistance = new Vector2(6f, -6f);

        // Text Nom
        var nameTextGO = new GameObject("NameText");
        nameTextGO.transform.SetParent(nbRT, false);
        var ntRT = nameTextGO.AddComponent<RectTransform>();
        ntRT.anchorMin = Vector2.zero; ntRT.anchorMax = Vector2.one;
        ntRT.offsetMin = ntRT.offsetMax = Vector2.zero;

        nameText = nameTextGO.AddComponent<TextMeshProUGUI>();
        nameText.margin = new Vector4(10f, 5f, 10f, 5f);
        SetFont(nameText, 48f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
        nameText.enableAutoSizing = true; nameText.fontSizeMin = 30f; nameText.fontSizeMax = 54f;

        // Línia separadora
        var divGO = new GameObject("DividerLine");
        divGO.transform.SetParent(panelRect, false);
        dividerRT = divGO.AddComponent<RectTransform>();
        var divImg = divGO.AddComponent<Image>();
        divImg.color = new Color(0.8f, 0.8f, 0.8f, 1f);

        // Indicador de tecla E visual
        var eBoxGO = new GameObject("E_Button");
        eBoxGO.transform.SetParent(panelRect, false);
        var eRT = eBoxGO.AddComponent<RectTransform>();
        eRT.anchorMin = new Vector2(1f, 0f); eRT.anchorMax = new Vector2(1f, 0f);
        eRT.sizeDelta = new Vector2(48f, 48f); 
        eRT.pivot = new Vector2(1f, 0f);
        eRT.anchoredPosition = new Vector2(-20f, 15f);

        eBtnGroup = eBoxGO.AddComponent<CanvasGroup>();
        eBtnGroup.alpha = 0f;

        var eBase = new GameObject("Base");
        eBase.transform.SetParent(eRT, false);
        var eBaseRT = eBase.AddComponent<RectTransform>();
        eBaseRT.anchorMin = Vector2.zero; eBaseRT.anchorMax = Vector2.one;
        eBaseRT.offsetMin = eBaseRT.offsetMax = Vector2.zero;
        var baseImg = eBase.AddComponent<Image>();
        baseImg.color = new Color(0.2f, 0.2f, 0.2f, 1f); 
        var baseOl = eBase.AddComponent<Outline>();
        baseOl.effectColor = new Color(0.05f, 0.05f, 0.05f, 1f);
        baseOl.effectDistance = new Vector2(2f, -2f);

        var eTop = new GameObject("Top");
        eTop.transform.SetParent(eRT, false);
        eTextRT = eTop.AddComponent<RectTransform>(); 
        eTextRT.anchorMin = Vector2.zero; eTextRT.anchorMax = Vector2.one;
        eTextRT.offsetMin = eTextRT.offsetMax = Vector2.zero;
        eTextRT.anchoredPosition = new Vector2(0f, 4f); 

        var topImg = eTop.AddComponent<Image>();
        topImg.color = new Color(0.85f, 0.85f, 0.85f, 1f);
        var topOl = eTop.AddComponent<Outline>();
        topOl.effectColor = new Color(0.05f, 0.05f, 0.05f, 1f);
        topOl.effectDistance = new Vector2(2f, -2f);

        var eLetterGO = new GameObject("Letter");
        eLetterGO.transform.SetParent(eTextRT, false);
        var elRT = eLetterGO.AddComponent<RectTransform>();
        elRT.anchorMin = Vector2.zero; elRT.anchorMax = Vector2.one;
        elRT.offsetMin = elRT.offsetMax = Vector2.zero;

        var eTextComp = eLetterGO.AddComponent<TextMeshProUGUI>();
        SetFont(eTextComp, 36f, new Color(0.05f, 0.05f, 0.05f, 1f), FontStyles.Bold, TextAlignmentOptions.Center); 
        eTextComp.text = "E";
        eTextComp.margin = new Vector4(2f, 2f, 0f, 0f);

        // Indicador de Skip visual (tecla F) - Vinculat al Canvas per sortir del diàleg
        var fContainerGO = new GameObject("F_Container");
        fContainerGO.transform.SetParent(canvas.transform, false);
        fContainerRT = fContainerGO.AddComponent<RectTransform>();
        fContainerRT.sizeDelta = new Vector2(250f, 48f);
        fContainerRT.pivot = new Vector2(0f, 1f); 

        fBtnGroup = fContainerGO.AddComponent<CanvasGroup>();
        fBtnGroup.alpha = 0f;

        var fBoxGO = new GameObject("F_Button");
        fBoxGO.transform.SetParent(fContainerGO.transform, false);
        var fRT = fBoxGO.AddComponent<RectTransform>();
        fRT.anchorMin = new Vector2(0f, 0.5f); fRT.anchorMax = new Vector2(0f, 0.5f);
        fRT.sizeDelta = new Vector2(48f, 48f);
        fRT.pivot = new Vector2(0f, 0.5f);
        fRT.anchoredPosition = new Vector2(0f, 0f);

        var fBase = new GameObject("Base");
        fBase.transform.SetParent(fRT, false);
        var fBaseRT = fBase.AddComponent<RectTransform>();
        fBaseRT.anchorMin = Vector2.zero; fBaseRT.anchorMax = Vector2.one;
        fBaseRT.offsetMin = fBaseRT.offsetMax = Vector2.zero;
        var fbImg = fBase.AddComponent<Image>();
        fbImg.color = new Color(0.2f, 0.2f, 0.2f, 1f); 
        var fbOl = fBase.AddComponent<Outline>();
        fbOl.effectColor = new Color(0.05f, 0.05f, 0.05f, 1f);
        fbOl.effectDistance = new Vector2(2f, -2f);

        var fTop = new GameObject("Top");
        fTop.transform.SetParent(fRT, false);
        fTextRT = fTop.AddComponent<RectTransform>(); 
        fTextRT.anchorMin = Vector2.zero; fTextRT.anchorMax = Vector2.one;
        fTextRT.offsetMin = fTextRT.offsetMax = Vector2.zero;
        fTextRT.anchoredPosition = new Vector2(0f, 4f); 

        var ftImg = fTop.AddComponent<Image>();
        ftImg.color = new Color(0.85f, 0.85f, 0.85f, 1f);
        var ftOl = fTop.AddComponent<Outline>();
        ftOl.effectColor = new Color(0.05f, 0.05f, 0.05f, 1f);
        ftOl.effectDistance = new Vector2(2f, -2f);

        var fLetterGO = new GameObject("Letter");
        fLetterGO.transform.SetParent(fTextRT, false);
        var flRT = fLetterGO.AddComponent<RectTransform>();
        flRT.anchorMin = Vector2.zero; flRT.anchorMax = Vector2.one;
        flRT.offsetMin = flRT.offsetMax = Vector2.zero;

        var fTextComp = fLetterGO.AddComponent<TextMeshProUGUI>();
        SetFont(fTextComp, 36f, new Color(0.05f, 0.05f, 0.05f, 1f), FontStyles.Bold, TextAlignmentOptions.Center); 
        fTextComp.text = "F";
        fTextComp.margin = new Vector4(2f, 2f, 0f, 0f);

        var skipTextGO = new GameObject("SkipText");
        skipTextGO.transform.SetParent(fContainerGO.transform, false);
        var stRT = skipTextGO.AddComponent<RectTransform>();
        stRT.anchorMin = new Vector2(0f, 0.5f); stRT.anchorMax = new Vector2(0f, 0.5f);
        stRT.sizeDelta = new Vector2(200f, 48f);
        stRT.pivot = new Vector2(0f, 0.5f);
        stRT.anchoredPosition = new Vector2(60f, 0f); 

        var stComp = skipTextGO.AddComponent<TextMeshProUGUI>();
        SetFont(stComp, 26f, new Color(0.8f, 0.8f, 0.8f, 1f), FontStyles.Bold, TextAlignmentOptions.Left);
        stComp.text = "Hold to skip";

        // Panell de seleccions
        var chGO = new GameObject("ChoicePanel");
        chGO.transform.SetParent(panelRect, false);
        choicePanelRT = chGO.AddComponent<RectTransform>();
        choicePanelRT.pivot = new Vector2(0.5f, 0f);
        var chGLG = chGO.AddComponent<GridLayoutGroup>();
        chGLG.childAlignment = TextAnchor.LowerCenter;
        chGLG.spacing = new Vector2(10f, 10f);
        chGLG.cellSize = new Vector2(350f, 85f);
        chGLG.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        chGLG.constraintCount = 4;

        currentPanelGO.SetActive(false);
    }

    /// <summary>
    /// Genera la graella dinàmica d'opcions a escollir per a ramificar diàlegs.
    /// </summary>
    private void ShowChoices()
    {
        isSelectingChoice = true;
        selectedChoiceIdx = 0;
        
        if (choicePanelRT != null)
        {
            var glg = choicePanelRT.GetComponent<GridLayoutGroup>();
            if (glg != null) glg.enabled = true;
        }

        foreach(Transform child in choicePanelRT) Destroy(child.gameObject);
        choiceTexts.Clear();
        visibleChoices.Clear();
        
        var inv = PlayerInventory.Instance;
        var currentLine = sequence != null && inSequence && seqIndex < sequence.Length ? sequence[seqIndex] : null;
        bool shouldHideSeen = currentLine != null && currentLine.owner != null && currentLine.owner.HideSeenChoices;

        for (int i = 0; i < currentLineChoices.Length; i++)
        {
            var choice = currentLineChoices[i];
            
            // Oculteu les opcions de diàlegs completats prèviament per a evitar redundància narrativa
            if (shouldHideSeen && inv != null && !choice.repeatable && inv.IsChoiceSeen(choice.text))
            {
                continue;
            }

            visibleChoices.Add(choice);

            var btnGO = new GameObject("ChoiceBtn");
            btnGO.transform.SetParent(choicePanelRT, false);
            var bRT = btnGO.AddComponent<RectTransform>();
            var le = btnGO.AddComponent<LayoutElement>();
            le.minHeight = 85f; 

            var img = btnGO.AddComponent<Image>();
            img.color = new Color(0.1f, 0.1f, 0.2f, 0.95f);
            var ol = btnGO.AddComponent<Outline>();
            ol.effectColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            ol.effectDistance = new Vector2(3f, -3f);

            var cg = btnGO.AddComponent<CanvasGroup>();
            cg.alpha = 0f; // S'inicia invisible per poder aplicar l'animació asíncrona

            var txtGO = new GameObject("Txt");
            txtGO.transform.SetParent(btnGO.transform, false);
            var tRT = txtGO.AddComponent<RectTransform>();
            tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
            tRT.offsetMin = new Vector2(10f, 5f); tRT.offsetMax = new Vector2(-10f, -5f);
            
            var txt = txtGO.AddComponent<TextMeshProUGUI>();
            SetFont(txt, 45f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center); 
            txt.enableAutoSizing = true;
            txt.fontSizeMin = 24f;
            txt.fontSizeMax = 55f;
            txt.text = choice.text;
            
            choiceTexts.Add(txt);
        }

        if (visibleChoices.Count == 0)
        {
            Hide();
            return;
        }
        
        HighlightChoice();
        StartCoroutine(AnimateChoicesIn());
    }

    /// <summary>
    /// Corrutina mestra d'escala musical d'opcions.
    /// Llissa individualment cada botó amb un lleuger retard dinàmic (Staggered animation)
    /// i emet sons d'aparició amb Pitch dinàmic per augmentar el feedback visual del TFG.
    /// </summary>
    private IEnumerator AnimateChoicesIn()
    {
        float slideDistance = 120f;
        float staggerDelay = 0.08f;
        float animTime = 0.25f;

        yield return null; // Un frame d'espera perquè el GridLayoutGroup s'hagi calculat a la GPU

        var choiceRTs = new System.Collections.Generic.List<RectTransform>();
        var choiceCGs = new System.Collections.Generic.List<CanvasGroup>();
        var targetPositions = new System.Collections.Generic.List<Vector2>();
        var targetSizes = new System.Collections.Generic.List<Vector2>();

        for (int i = 0; i < choiceTexts.Count; i++)
        {
            var btnGO = choiceTexts[i].transform.parent.gameObject;
            var rt = btnGO.GetComponent<RectTransform>();
            var cg = btnGO.GetComponent<CanvasGroup>();
            choiceRTs.Add(rt);
            choiceCGs.Add(cg);
            targetPositions.Add(rt.anchoredPosition);
            targetSizes.Add(rt.sizeDelta);
        }

        // Desactivem temporalment el GridLayout per a no tenir restriccions visuals en les coordenades
        var glg = choicePanelRT.GetComponent<GridLayoutGroup>();
        if (glg != null) glg.enabled = false;

        for (int i = 0; i < choiceRTs.Count; i++)
        {
            choiceRTs[i].sizeDelta = targetSizes[i];
            choiceRTs[i].anchoredPosition = targetPositions[i] + new Vector2(-slideDistance, 0f);
            choiceCGs[i].alpha = 0f;
        }

        // Llançament dinàmic de les sub-rutines de desplaçament
        for (int i = 0; i < choiceRTs.Count; i++)
        {
            if (choiceAppearSound != null)
            {
                if (!audioSource)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.spatialBlend = 0f;
                }
                // Pitch incremental per crear una escala ascendent a mesura que es dibuixen
                audioSource.pitch = choiceBasePitch + (i * choicePitchIncrement);
                audioSource.PlayOneShot(choiceAppearSound);
            }

            StartCoroutine(AnimateSingleChoice(choiceRTs[i], choiceCGs[i], targetPositions[i], slideDistance, animTime));
            yield return new WaitForSecondsRealtime(staggerDelay);
        }

        if (audioSource != null)
        {
            audioSource.pitch = 1f;
        }
    }

    private IEnumerator AnimateSingleChoice(RectTransform rt, CanvasGroup cg, Vector2 targetPos, float slideDistance, float duration)
    {
        Vector2 startPos = targetPos + new Vector2(-slideDistance, 0f);
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / duration);
            float eased = 1f - Mathf.Pow(1f - u, 3f); // OutCubic

            rt.anchoredPosition = Vector2.Lerp(startPos, targetPos, eased);
            cg.alpha = eased;
            yield return null;
        }

        rt.anchoredPosition = targetPos;
        cg.alpha = 1f;
    }

    /// <summary>
    /// Brillantor visual de l'opció seleccionada de l'arbre.
    /// </summary>
    private void HighlightChoice()
    {
        for (int i = 0; i < choiceTexts.Count; i++)
        {
            bool sel = (i == selectedChoiceIdx);
            choiceTexts[i].color = sel ? new Color(0.2f, 1f, 0.9f) : Color.white;
            var parImg = choiceTexts[i].transform.parent.GetComponent<Image>();
            if (parImg) parImg.color = sel ? new Color(0.2f, 0.2f, 0.4f, 1f) : new Color(0.1f, 0.1f, 0.2f, 0.95f);
            var parOl = choiceTexts[i].transform.parent.GetComponent<Outline>();
            if (parOl) parOl.effectColor = sel ? new Color(0.2f, 1f, 0.9f, 1f) : new Color(0.8f, 0.8f, 0.8f, 1f);
        }
    }

    private Sprite generatedCloudSprite;
    
    /// <summary>
    /// Generador procedural d'imatges de núvol (tileables) a nivell de píxel.
    /// Dissenyat per a diàlegs de tipus pensament reduint el consum de vèrtexs (< 65k).
    /// </summary>
    private Sprite GetCloudSprite()
    {
        if (generatedCloudSprite != null) return generatedCloudSprite;
        int size = 24;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        Color c = new Color(1f, 1f, 1f, 0f);
        Color w = Color.white;
        
        Vector2[] centers = new Vector2[]
        {
            new Vector2(3.5f, 3.5f), new Vector2(11.5f, 3.5f), new Vector2(19.5f, 3.5f),
            new Vector2(3.5f, 11.5f),                          new Vector2(19.5f, 11.5f),
            new Vector2(3.5f, 19.5f), new Vector2(11.5f, 19.5f), new Vector2(19.5f, 19.5f)
        };

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                if (x >= 4 && x <= 19 && y >= 4 && y <= 19)
                {
                    tex.SetPixel(x, y, w);
                    continue;
                }
                
                bool insideNode = false;
                foreach(var center in centers)
                {
                    if (Vector2.Distance(new Vector2(x, y), center) <= 4.2f)
                    {
                        insideNode = true;
                        break;
                    }
                }
                tex.SetPixel(x, y, insideNode ? w : c);
            }
        }
        tex.Apply();
        
        generatedCloudSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(8, 8, 8, 8));
        return generatedCloudSprite;
    }

    /// <summary>
    /// Organització de mides, proporcions, retrats i divisions de la caixa de text de forma adaptativa.
    /// </summary>
    private void LayoutUI(bool rightSide, bool onTop, bool hasPortrait, bool isThought)
    {
        if (currentPanelGO == null) return;

        // ── ALTURA DEL DIÀLEG (DALT O BAIX) ──
        if (onTop)
        {
            panelRect.anchorMin = new Vector2(0.12f, 0.70f);
            panelRect.anchorMax = new Vector2(0.88f, 0.92f);
            if (choicePanelRT != null)
            {
                choicePanelRT.anchorMin = new Vector2(0f, 0f);
                choicePanelRT.anchorMax = new Vector2(1f, 0f);
                choicePanelRT.pivot = new Vector2(0.5f, 1f); 
                var glg = choicePanelRT.GetComponent<GridLayoutGroup>();
                if (glg != null) glg.childAlignment = TextAnchor.UpperCenter;
                choicePanelRT.anchoredPosition = new Vector2(0f, -20f); 
                choicePanelRT.sizeDelta = new Vector2(0f, 20f); 
            }
        }
        else
        {
            panelRect.anchorMin = new Vector2(0.12f, 0.05f);
            panelRect.anchorMax = new Vector2(0.88f, 0.34f); 
            if (choicePanelRT != null)
            {
                choicePanelRT.anchorMin = new Vector2(0f, 1f);
                choicePanelRT.anchorMax = new Vector2(1f, 1f);
                choicePanelRT.pivot = new Vector2(0.5f, 0f); 
                var glg = choicePanelRT.GetComponent<GridLayoutGroup>();
                if (glg != null) glg.childAlignment = TextAnchor.LowerCenter;
                choicePanelRT.anchoredPosition = new Vector2(0f, 20f); 
                choicePanelRT.sizeDelta = new Vector2(0f, 20f); 
            }
        }
        
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        
        shownPos = panelRect.anchoredPosition;

        // Estètica de fons segons tipus
        if (panelBgImg != null)
        {
            if (isThought)
            {
                panelBgImg.sprite = GetCloudSprite();
                panelBgImg.type = Image.Type.Tiled;
                panelBgImg.pixelsPerUnitMultiplier = 0.12f; 
                panelBgOl.effectDistance = new Vector2(4f, -4f); 
            }
            else
            {
                panelBgImg.sprite = null;
                panelBgImg.type = Image.Type.Sliced; 
                panelBgOl.effectDistance = new Vector2(6f, -6f);
            }
        }

        var tRT = dialogueText.GetComponent<RectTransform>();
        var nbRT = nameBoxGO.GetComponent<RectTransform>();

        if (portRT != null) portRT.gameObject.SetActive(hasPortrait);
        if (dividerRT != null) dividerRT.gameObject.SetActive(hasPortrait);

        // ── ENQUADRAMENT D'AVATARS I AMPLES (ESQUERRA / DRETA /Complert) ──
        if (!hasPortrait)
        {
            tRT.anchorMin = new Vector2(0f, 0f); tRT.anchorMax = new Vector2(1f, 1f);
            tRT.offsetMin = tRT.offsetMax = Vector2.zero;

            nbRT.anchorMin = new Vector2(0f, 1f); nbRT.anchorMax = new Vector2(0f, 1f);
            nbRT.sizeDelta = new Vector2(320f, 75f); 
            nbRT.pivot = new Vector2(0f, 0f);
            nbRT.anchoredPosition = new Vector2(15f, 8f);
        }
        else if (rightSide)
        {
            portRT.anchorMin = new Vector2(0.82f, 0f); portRT.anchorMax = new Vector2(1f, 1f);
            portRT.offsetMin = portRT.offsetMax = Vector2.zero;
            
            tRT.anchorMin = new Vector2(0f, 0f); tRT.anchorMax = new Vector2(0.82f, 1f);
            tRT.offsetMin = tRT.offsetMax = Vector2.zero;

            nbRT.anchorMin = new Vector2(1f, 1f); nbRT.anchorMax = new Vector2(1f, 1f);
            nbRT.sizeDelta = new Vector2(320f, 75f); 
            nbRT.pivot = new Vector2(1f, 0f);
            nbRT.anchoredPosition = new Vector2(-10f, 8f);

            if (dividerRT != null)
            {
                dividerRT.anchorMin = new Vector2(0.82f, 0f);
                dividerRT.anchorMax = new Vector2(0.82f, 1f);
                dividerRT.sizeDelta = new Vector2(6f, 0f);
                dividerRT.anchoredPosition = Vector2.zero;
            }
        }
        else
        {
            portRT.anchorMin = new Vector2(0f, 0f); portRT.anchorMax = new Vector2(0.18f, 1f);
            portRT.offsetMin = portRT.offsetMax = Vector2.zero;
            
            tRT.anchorMin = new Vector2(0.18f, 0f); tRT.anchorMax = new Vector2(1f, 1f);
            tRT.offsetMin = tRT.offsetMax = Vector2.zero;

            nbRT.anchorMin = new Vector2(0f, 1f); nbRT.anchorMax = new Vector2(0f, 1f);
            nbRT.sizeDelta = new Vector2(320f, 75f); 
            nbRT.pivot = new Vector2(0f, 0f);
            nbRT.anchoredPosition = new Vector2(10f, 8f);

            if (dividerRT != null)
            {
                dividerRT.anchorMin = new Vector2(0.18f, 0f);
                dividerRT.anchorMax = new Vector2(0.18f, 1f);
                dividerRT.sizeDelta = new Vector2(6f, 0f);
                dividerRT.anchoredPosition = Vector2.zero;
            }
        }
    }

    private static TMP_FontAsset cachedFont;

    private void SetFont(TextMeshProUGUI t, float size, Color col, FontStyles style, TextAlignmentOptions align)
    {
        t.fontSize = size; t.color = col; t.fontStyle = style; t.alignment = align;
        
        if (cachedFont == null)
        {
#if UNITY_EDITOR
            cachedFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/determination SDF.asset")
                 ?? UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/PixelOperator SDF.asset") 
                 ?? UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/PixelOperator SDF.asset");
#endif
            if (cachedFont == null)
            {
                cachedFont = Resources.Load<TMP_FontAsset>("Fonts/determination SDF") 
                    ?? Resources.Load<TMP_FontAsset>("determination SDF")
                    ?? Resources.Load<TMP_FontAsset>("Fonts/PixelOperator SDF") 
                    ?? Resources.Load<TMP_FontAsset>("PixelOperator SDF");
            }
        }

        if (cachedFont != null) t.font = cachedFont;
    }

    // =========================================================================
    // Animacions del Panell (Lliscaments RPG verticals suaus)
    // =========================================================================
    private void PlayIn()
    {
        if (panelRect == null || panelGroup == null) return;
        if (animRoutine != null) StopCoroutine(animRoutine);
        animRoutine = StartCoroutine(AnimateIn());
    }

    private void PlayOut()
    {
        if (panelRect == null || panelGroup == null)
        {
            if (currentPanelGO) currentPanelGO.SetActive(false);
            OnDialogueClosed?.Invoke();
            return;
        }

        if (animRoutine != null) StopCoroutine(animRoutine);
        animRoutine = StartCoroutine(AnimateOut(true));
    }

    private void PlayOutForReopen()
    {
        if (panelRect == null || panelGroup == null) return;
        if (animRoutine != null) StopCoroutine(animRoutine);
        animRoutine = StartCoroutine(AnimateOut(false));
    }

    private IEnumerator AnimateIn()
    {
        panelGroup.alpha = 1f;
        float slideDist = 800f;
        Vector2 offset = new Vector2(0f, isCurrentOnTop ? slideDist : -slideDist);
        panelRect.anchoredPosition = shownPos + offset;
        panelRect.localScale = Vector3.one;

        float t = 0f;
        while (t < animDuration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / Mathf.Max(0.0001f, animDuration));
            float eased = 1f - Mathf.Pow(1f - u, 3f); // OutCubic

            panelRect.anchoredPosition = Vector2.Lerp(shownPos + offset, shownPos, eased);
            yield return null;
        }

        panelGroup.alpha = 1f;
        panelRect.anchoredPosition = shownPos;
        animRoutine = null;
    }

    private IEnumerator AnimateOut(bool invokeCloseEvent)
    {
        panelGroup.alpha = 1f;
        Vector2 startPos = panelRect.anchoredPosition;
        float slideDist = 800f;
        Vector2 offset = new Vector2(0f, isCurrentOnTop ? slideDist : -slideDist);
        Vector2 endPos = shownPos + offset;

        float t = 0f;
        while (t < animDuration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / Mathf.Max(0.0001f, animDuration));
            float eased = u * u; // InQuad

            panelRect.anchoredPosition = Vector2.Lerp(startPos, endPos, eased);
            yield return null;
        }

        panelGroup.alpha = 0f;
        panelRect.anchoredPosition = endPos;

        if (currentPanelGO) currentPanelGO.SetActive(false);
        if (dialogueText) { dialogueText.text = ""; dialogueText.maxVisibleCharacters = int.MaxValue; }

        animRoutine = null;
        if (invokeCloseEvent) 
        {
            isOpen = false;
            isHiding = false;
            OnDialogueClosed?.Invoke();
        }
    }

    private void ForceHidden()
    {
        if (panelRect != null) 
        {
            float slideDist = 800f;
            Vector2 offset = new Vector2(0f, isCurrentOnTop ? slideDist : -slideDist);
            panelRect.anchoredPosition = shownPos + offset;
            panelRect.localScale = Vector3.one;
        }
        if (panelGroup != null) panelGroup.alpha = 0f;
        if (currentPanelGO != null) currentPanelGO.SetActive(false);

        if (dialogueText) { dialogueText.text = ""; dialogueText.maxVisibleCharacters = int.MaxValue; }
        isOpen = false;
        isTyping = false;
        inSequence = false;
    }

    private void ForceShown()
    {
        if (panelGroup != null) panelGroup.alpha = 1f;
        if (panelRect != null) 
        {
            panelRect.anchoredPosition = shownPos;
            panelRect.localScale = Vector3.one;
        }
    }
}
