using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class DialogueUI : MonoBehaviour
{
    [Header("Typewriter")]
    public float charsPerSecond = 40f;
    [SerializeField] private bool skipSpaces = true;
    [SerializeField] private int soundEveryNChars = 2;

    [Header("Typing Sounds")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] typingClips;
    [Range(0f, 0.4f)][SerializeField] private float pitchRandom = 0.05f;
    [Range(0f, 1f)][SerializeField] private float volume = 0.8f;

    [Header("Panel Animation")]
    [SerializeField] private float animDuration = 0.4f; 
    [SerializeField] private bool animateOnShow = true;  
    public bool canSkip = true;
    public bool canAdvance = true;
    public static bool ForceDisableSkipGlobals { get; set; } = false;

    private Coroutine typingRoutine;
    private Coroutine animRoutine;
    private Coroutine autoAdvanceRoutine;

    private string fullText;
    private bool isOpen;
    public bool IsOpen => isOpen;
    private bool isTyping;
    private int typedCount;

    public bool WasSkipped { get; private set; }
    public System.Action OnDialogueClosed;

    [Header("UI References (Manual or Dynamic)")]
    [SerializeField] private GameObject currentPanelGO;
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private CanvasGroup panelGroup;
    public TextMeshProUGUI dialogueText;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Image portraitImage;
    [SerializeField] private Animator portraitAnimator;
    [SerializeField] private GameObject nameBoxGO;
    [SerializeField] private CanvasGroup eBtnGroup;
    [SerializeField] private RectTransform eTextRT;
    [SerializeField] private CanvasGroup fBtnGroup;
    [SerializeField] private RectTransform fTextRT;
    [SerializeField] private RectTransform fContainerRT;
    private float skipHoldTime;
    private const float SkipHoldRequired = 0.5f;
    private Vector3[] panelCorners = new Vector3[4]; // reused buffer

    private bool lastEPressed;
    private bool lastFPressed;
    private RectTransform dividerRT;
    private RectTransform portRT;
    private Image panelBgImg;
    private Outline panelBgOl;

    private Vector2 shownPos = Vector2.zero;
    private bool isHidingForCombat = false;
    private bool eventAlreadyFired = false;

    private float currentSpeedMultiplier = 1f;

    public void SetSpeedMultiplier(float multiplier)
    {
        // Guardem el multiplicador temporalment en comptes de modificar permanentment la base charsPerSecond
        if (multiplier > 0)
            currentSpeedMultiplier = multiplier;
    }

    private Interactable.DialogueLine[] sequence;
    private int seqIndex;
    private bool inSequence;
    private bool isReopening;
    private bool isCurrentOnTop;
    private bool isHiding;
    
    private AudioClip currentLineVoice;

    // Branches
    private Interactable.DialogueLine currentLine;
    private Interactable.DialogueChoice[] currentLineChoices;
    private bool isSelectingChoice;
    private int selectedChoiceIdx;
    private RectTransform choicePanelRT;
    private System.Collections.Generic.List<TextMeshProUGUI> choiceTexts = new System.Collections.Generic.List<TextMeshProUGUI>();
    private System.Collections.Generic.List<Interactable.DialogueChoice> visibleChoices = new System.Collections.Generic.List<Interactable.DialogueChoice>();

    private void Awake()
    {
        if (!audioSource) audioSource = GetComponent<AudioSource>();
        // S'ha eliminat la necessitat d'assignar res per inspector, el construim quan fem Show()
    }

    private void Update()
    {
        if (PauseMenuUI.IsOpen) return;

        if (isOpen && !isHiding && !isReopening && currentPanelGO != null && currentPanelGO.activeSelf)
        {
            // F Button (Skip) Logic
            bool lineAllowsSkip = canSkip && !ForceDisableSkipGlobals && (currentLine == null || (!currentLine.cannotSkip && (currentLine.owner == null || !currentLine.owner.CannotSkipDialogue)));

            if (lineAllowsSkip)
            {
                if (Input.GetKey(KeyCode.F))
                {
                    skipHoldTime += Time.unscaledDeltaTime;
                    if (fTextRT != null) fTextRT.anchoredPosition = Vector2.zero; // Press down visually
                    
                    if (skipHoldTime >= SkipHoldRequired)
                    {
                        skipHoldTime = 0f;
                        WasSkipped = true;
                        Hide();
                        return;
                    }
                }
                else
                {
                    skipHoldTime = 0f;
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

            // E Button Logic
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
                        eTextRT.anchoredPosition = isPressed ? Vector2.zero : new Vector2(0f, 4f);
                    }
                }
                else
                {
                    eBtnGroup.alpha = 0f;
                }
            }

            // Track F container to follow the panel position (including animations)
            if (fContainerRT != null && panelRect != null)
            {
                panelRect.GetWorldCorners(panelCorners); // 0=bottom-left, 1=top-left, 2=top-right, 3=bottom-right
                // Position below the bottom-left corner of the panel
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

        if (isSelectingChoice && choiceTexts.Count > 0)
        {
            bool left = Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow);
            bool right = Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow);
            bool up = Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow);
            bool down = Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow);
            bool moved = false;

            int cols = 4;

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
                HighlightChoice();
                if (PlayerInventory.Instance != null && PlayerInventory.Instance.navSound != null)
                {
                    ItemSoundPlayer.Play(PlayerInventory.Instance.navSound);
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

    public void StartDialogue(Interactable.DialogueLine[] lines, bool animateIn = true)
    {
        WasSkipped = false;
        if (CombatLoader.IsInCombat)
        {
            var cm = FindFirstObjectByType<CombatManager>();
            if (cm == null || !cm.IsEnded) return; // Permetem el diàleg si és el de Game Over
        }
        
        if (lines == null || lines.Length == 0)
        {
            Hide(); 
            return;
        }

        BuildDynamicUI();

        sequence = lines;
        seqIndex = 0;
        inSequence = true;

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

    public void Show(string text, Sprite portrait = null, RuntimeAnimatorController portraitAnim = null)
    {
        WasSkipped = false;
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

    private void ShowInternal(Interactable.DialogueLine line, bool playInAnim)
    {
        if (autoAdvanceRoutine != null) { StopCoroutine(autoAdvanceRoutine); autoAdvanceRoutine = null; }

        isOpen = true;
        currentLine = line;
        fullText = line?.text ?? "";
        typedCount = 0;

        // Invoca els esdeveniments assignats només si no s'han disparat prèviament en el tancament previ
        if (!eventAlreadyFired)
        {
            line?.onLineReached?.Invoke();
        }
        eventAlreadyFired = false; // Resetegem el flag per a les properes línies

        // Canvia l'sprite de l'Interactable o del target específic si està configurat
        if (line != null)
        {
            if (line.interactableSpriteChange != null)
            {
                SpriteRenderer sr = line.targetSpriteRenderer;
                
                // Si no hi ha target específic, fem servir el de l'Interactable original (Legacy)
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

        LayoutUI(line != null && line.isRightSide, isCurrentOnTop, hasPortrait, line != null && line.isThought);

        // Nom
        if (!string.IsNullOrEmpty(line?.speakerName))
        {
            nameBoxGO.SetActive(true);
            nameText.text = line.speakerName;
        }
        else
        {
            nameBoxGO.SetActive(false);
        }

        // Retrat (sprite)
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

        // Mostrar text
        if (dialogueText != null)
        {
            dialogueText.enableAutoSizing = true;
            dialogueText.text = fullText;
            dialogueText.ForceMeshUpdate();
            dialogueText.enableAutoSizing = false;
            dialogueText.maxVisibleCharacters = 0;
        }

        if (typingRoutine != null) StopCoroutine(typingRoutine);
        typingRoutine = StartCoroutine(TypeRoutine(fullText));
    }

    public void Hide()
    {
        currentSpeedMultiplier = 1f; // Reseteja la velocitat per evitar bugs després de combats

        if (isHiding) return;
        isHiding = true;

        inSequence = false;
        sequence = null;
        seqIndex = 0;

        canAdvance = true; // Reset per evitar bloquejos perpetus
        isTyping = false;
        isSelectingChoice = false;
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

    public void AdvanceOrSkip()
    {
        if (!isOpen || isReopening || isHiding) return;
        if (autoAdvanceRoutine != null) { StopCoroutine(autoAdvanceRoutine); autoAdvanceRoutine = null; }

        if (isTyping)
        {
            // Eliminada la comprovació de cannotSkip aquí per permetre que la E sempre acabi el text ràpid.
            // La restricció ara s'aplica només al botó F (saltar seqüència completa).
            
            if (typingRoutine != null) StopCoroutine(typingRoutine);
            typingRoutine = null;

            isTyping = false;
            if (dialogueText)
            {
                dialogueText.text = fullText;
                dialogueText.maxVisibleCharacters = fullText.Length;
            }
            
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

        if (isSelectingChoice)
        {
            var choice = visibleChoices[selectedChoiceIdx];

            if (choice.customSelectSound != null)
            {
                if (audioSource != null) audioSource.PlayOneShot(choice.customSelectSound);
            }
            else if (PlayerInventory.Instance != null && PlayerInventory.Instance.selectSound != null)
            {
                ItemSoundPlayer.Play(PlayerInventory.Instance.selectSound);
            }

            // Marcar com a vist si l'interactable ho demana i no és repetible
            var currentLine = sequence != null && inSequence && seqIndex < sequence.Length ? sequence[seqIndex] : null;
            bool shouldHideSeen = currentLine != null && currentLine.owner != null && currentLine.owner.HideSeenChoices;
            
            if (shouldHideSeen)
            {
                if (!choice.repeatable && PlayerInventory.Instance != null)
                {
                    PlayerInventory.Instance.MarkChoiceSeen(choice.text);
                }
            }

            choice.onChoiceSelected?.Invoke();
            
            isSelectingChoice = false;
            foreach(Transform child in choicePanelRT) Destroy(child.gameObject);
            choiceTexts.Clear();
            visibleChoices.Clear();
            
            int nextIdx = choice.jumpToLineIndex >= 0 ? choice.jumpToLineIndex : seqIndex + 1;

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

        if (inSequence && sequence != null)
        {
            if (currentLineChoices != null && currentLineChoices.Length > 0) return; // Esperem selecció
            
            var currentLine = sequence[seqIndex];
            if (currentLine.isEndNode)
            {
                Hide();
                return;
            }

            int nextIdx = currentLine.jumpToLineIndex >= 0 ? currentLine.jumpToLineIndex : seqIndex + 1;

            if (nextIdx >= 0 && nextIdx < sequence.Length)
            {
                var nextLine = sequence[nextIdx];
                
                // NOU: Si la SEGÜENT línia té esdeveniments (com iniciar combat),
                // primer tanquem el diàleg visualment i després l'executem.
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

    private IEnumerator CloseExecuteAndPause(Interactable.DialogueLine line, int nextIdx)
    {
        isHidingForCombat = true;
        seqIndex = nextIdx; // Mantenim l'índex correcte per poder mostrar el text d'aquesta línia en tornar

        // 1. Tanquem amb animació (esperem la durada de l'animació de PlayOut)
        PlayOut();
        yield return new WaitForSecondsRealtime(animDuration + 0.05f);
        
        if (currentPanelGO != null) currentPanelGO.SetActive(false);
        isOpen = false;

        // 2. Iniciem l'esdeveniment (combat o el que sigui)
        line.onLineReached?.Invoke();
        eventAlreadyFired = true; // Marquem perquè quan tornem a ShowInternal no es torni a disparar

        // 3. Si l'esdeveniment no ha iniciat cap combat, tornem a obrir el diàleg immediatament
        if (!CombatLoader.IsInCombat)
        {
            ResumeAfterCombat();
        }
    }

    public void ResumeAfterCombat()
    {
        if (!isHidingForCombat) return;
        isHidingForCombat = false;

        if (inSequence && sequence != null && seqIndex < sequence.Length)
        {
            // Tornem a mostrar el diàleg amb animació d'entrada
            if (currentPanelGO != null) currentPanelGO.SetActive(true);
            ShowInternal(sequence[seqIndex], true);
        }
        else
        {
            // Si no queden línies, tanquem del tot
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
        if (currentPanelGO != null) currentPanelGO.SetActive(false); // Ocultem temporalment

        // Esperem fins que tanquis o tornis el rellotge del món a temps real
        while (ShopMenuUI.IsOpen || Time.timeScale == 0f)
        {
            yield return null;
        }
        AdvanceToLine(nextIdx); // El mostrarà i reproduirà de nou l'animació visual d'aparèixer
    }

    private IEnumerator ReopenRoutine(Interactable.DialogueLine nextLine)
    {
        isReopening = true;
        PlayOutForReopen(); // Playout sense llençar el event de tancat total
        yield return new WaitForSecondsRealtime(animDuration + 0.05f);
        isReopening = false;
        ShowInternal(nextLine, playInAnim: true);
    }

    private IEnumerator TypeRoutine(string text)
    {
        isTyping = true;

        string cleanText = text.Trim();
        
        // Apliquem el multiplicador temporal a la velocitat base
        float currentSpeed = Mathf.Max(1f, charsPerSecond / currentSpeedMultiplier);
        float delay = 1f / currentSpeed;

        // Usem maxVisibleCharacters per evitar crear strings nous cada frame (zero GC)
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
            
            // Ritme de lectura orgànic
            if (c == '.' || c == '?' || c == '!')
            {
                currentDelay = delay * 10f; // Pausa forta
            }
            else if (c == ',' || c == ';' || c == ':' || c == '-')
            {
                currentDelay = delay * 5f; // Pausa breu
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

    // -----------------------
    // UI Builder & Layout
    // -----------------------
    private void BuildDynamicUI()
    {
        // Si l'usuari ja ha assignat manualment el panell i els textos, respectem-ho
        if (currentPanelGO != null && dialogueText != null && nameText != null) return;
        
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        Canvas targetCanvas = null;
        foreach (var c in canvases)
        {
            if (c.name != "EndCanvas" && c.name != "AlertCanvas")
            {
                targetCanvas = c;
                break; // Found a valid canvas
            }
        }
        if (targetCanvas == null && canvases.Length > 0) targetCanvas = canvases[0]; // fallback
        if (targetCanvas == null) return;
        
        var canvas = targetCanvas;

        currentPanelGO = new GameObject("DynamicDialoguePanel");
        panelRect = currentPanelGO.AddComponent<RectTransform>();
        panelRect.SetParent(canvas.transform, false);

        // NOU: Forcem que el panell tingui un Canvas propi per sobre de tot
        Canvas diagCanvas = currentPanelGO.AddComponent<Canvas>();
        diagCanvas.overrideSorting = true;
        diagCanvas.sortingOrder = 9999;
        currentPanelGO.AddComponent<GraphicRaycaster>();

        panelRect.anchorMin = new Vector2(0.12f, 0.05f);
        panelRect.anchorMax = new Vector2(0.88f, 0.35f); // Increased panel height max (from 0.28)
        panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
        shownPos = panelRect.anchoredPosition; // Guardant la posició 0 default com a shown

        panelGroup = currentPanelGO.AddComponent<CanvasGroup>();
        panelGroup.alpha = 0f;

        panelBgImg = currentPanelGO.AddComponent<Image>();
        panelBgImg.color = new Color(0.08f, 0.08f, 0.16f, 0.95f);
        panelBgOl = currentPanelGO.AddComponent<Outline>();
        panelBgOl.effectColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        panelBgOl.effectDistance = new Vector2(6f, -6f);

        // Portrait container
        var portGO = new GameObject("PortraitBox");
        portGO.transform.SetParent(panelRect, false);
        portRT = portGO.AddComponent<RectTransform>();

        // Portrait Image (Fill)
        var portImgGO = new GameObject("Img");
        portImgGO.transform.SetParent(portRT, false);
        var piRT = portImgGO.AddComponent<RectTransform>();
        piRT.anchorMin = Vector2.zero; piRT.anchorMax = Vector2.one;
        piRT.offsetMin = piRT.offsetMax = Vector2.zero;
        portraitImage = portImgGO.AddComponent<Image>();
        portraitImage.preserveAspect = true;
        portraitImage.color = new Color(1, 1, 1, 0); 
        portraitAnimator = portImgGO.AddComponent<Animator>();

        // Text Box
        var txtGO = new GameObject("TextBox");
        txtGO.transform.SetParent(panelRect, false);
        var tRT = txtGO.AddComponent<RectTransform>();
        
        dialogueText = txtGO.AddComponent<TextMeshProUGUI>();
        dialogueText.margin = new Vector4(35f, 30f, 35f, 30f);
        dialogueText.textWrappingMode = TextWrappingModes.Normal;
        dialogueText.fontSizeMax = 80f; dialogueText.fontSizeMin = 40f; // Increased text limits
        dialogueText.enableAutoSizing = true;
        SetFont(dialogueText, 60f, Color.white, FontStyles.Normal, TextAlignmentOptions.TopLeft); // Increased initial font size

        // Name Box (Background)
        nameBoxGO = new GameObject("NameBox");
        nameBoxGO.transform.SetParent(panelRect, false);
        var nbRT = nameBoxGO.AddComponent<RectTransform>();
        var nbImg = nameBoxGO.AddComponent<Image>();
        nbImg.color = new Color(0.15f, 0.25f, 0.4f, 1f);
        var nbOl = nameBoxGO.AddComponent<Outline>();
        nbOl.effectColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        nbOl.effectDistance = new Vector2(6f, -6f);

        // Name Text
        var nameTextGO = new GameObject("NameText");
        nameTextGO.transform.SetParent(nbRT, false);
        var ntRT = nameTextGO.AddComponent<RectTransform>();
        ntRT.anchorMin = Vector2.zero; ntRT.anchorMax = Vector2.one;
        ntRT.offsetMin = ntRT.offsetMax = Vector2.zero;

        nameText = nameTextGO.AddComponent<TextMeshProUGUI>();
        nameText.margin = new Vector4(10f, 5f, 10f, 5f);
        SetFont(nameText, 48f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center); // Increased text base size
        nameText.enableAutoSizing = true; nameText.fontSizeMin = 30f; nameText.fontSizeMax = 54f; // Increased test limits

        // Divider Line
        var divGO = new GameObject("DividerLine");
        divGO.transform.SetParent(panelRect, false);
        dividerRT = divGO.AddComponent<RectTransform>();
        var divImg = divGO.AddComponent<Image>();
        divImg.color = new Color(0.8f, 0.8f, 0.8f, 1f);

        // Button E Indicator (Físic)
        var eBoxGO = new GameObject("E_Button");
        eBoxGO.transform.SetParent(panelRect, false);
        var eRT = eBoxGO.AddComponent<RectTransform>();
        eRT.anchorMin = new Vector2(1f, 0f); eRT.anchorMax = new Vector2(1f, 0f);
        eRT.sizeDelta = new Vector2(48f, 48f); // Increased key size (from 36x36)
        eRT.pivot = new Vector2(1f, 0f);
        eRT.anchoredPosition = new Vector2(-20f, 15f);

        eBtnGroup = eBoxGO.AddComponent<CanvasGroup>();
        eBtnGroup.alpha = 0f;

        // Base Fosca (Ombra inferior tecla 3D)
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

        // Cara Superior de la Tecla (Fons blanc)
        var eTop = new GameObject("Top");
        eTop.transform.SetParent(eRT, false);
        eTextRT = eTop.AddComponent<RectTransform>(); 
        eTextRT.anchorMin = Vector2.zero; eTextRT.anchorMax = Vector2.one;
        eTextRT.offsetMin = eTextRT.offsetMax = Vector2.zero;
        eTextRT.anchoredPosition = new Vector2(0f, 4f); // 4px aixecada

        var topImg = eTop.AddComponent<Image>();
        topImg.color = new Color(0.85f, 0.85f, 0.85f, 1f);
        var topOl = eTop.AddComponent<Outline>();
        topOl.effectColor = new Color(0.05f, 0.05f, 0.05f, 1f);
        topOl.effectDistance = new Vector2(2f, -2f);

        // Lletra "E" separada perquè text i Imatge no col·lisionin al mateix GameObject
        var eLetterGO = new GameObject("Letter");
        eLetterGO.transform.SetParent(eTextRT, false);
        var elRT = eLetterGO.AddComponent<RectTransform>();
        elRT.anchorMin = Vector2.zero; elRT.anchorMax = Vector2.one;
        elRT.offsetMin = elRT.offsetMax = Vector2.zero;

        var eTextComp = eLetterGO.AddComponent<TextMeshProUGUI>();
        SetFont(eTextComp, 36f, new Color(0.05f, 0.05f, 0.05f, 1f), FontStyles.Bold, TextAlignmentOptions.Center); // Increased text
        eTextComp.text = "E";
        eTextComp.margin = new Vector4(2f, 2f, 0f, 0f);

        // Button F Indicator (Skip) — parented to canvas, not panel, so it sits outside the box
        var fContainerGO = new GameObject("F_Container");
        fContainerGO.transform.SetParent(canvas.transform, false);
        fContainerRT = fContainerGO.AddComponent<RectTransform>();
        fContainerRT.sizeDelta = new Vector2(250f, 48f);
        fContainerRT.pivot = new Vector2(0f, 1f); // top-left pivot so we can place it just below the panel
        // Position will be set dynamically in LayoutUI
        var fcRT = fContainerRT;

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

        // Branching Choices Box
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

    private void ShowChoices()
    {
        isSelectingChoice = true;
        selectedChoiceIdx = 0;
        
        // Reactivar el GridLayoutGroup (pot estar desactivat per l'animació anterior)
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
            
            // Si l'opció ja s'ha vist i no és repetible, i l'interactable ho demana, la ignorem
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

            // CanvasGroup per controlar l'opacitat de l'animació
            var cg = btnGO.AddComponent<CanvasGroup>();
            cg.alpha = 0f; // Comença invisible

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

        // Si per alguna raó no hi ha cap opció visible (error de disseny), tanquem el diàleg
        if (visibleChoices.Count == 0)
        {
            Hide();
            return;
        }
        
        HighlightChoice();
        StartCoroutine(AnimateChoicesIn());
    }

    private IEnumerator AnimateChoicesIn()
    {
        float slideDistance = 120f;
        float staggerDelay = 0.08f;
        float animTime = 0.25f;

        // Esperar un frame perquè el GridLayoutGroup calculi les posicions correctes
        yield return null;

        // Guardar les posicions i tamanys finals calculades pel layout
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

        // Desactivar el GridLayoutGroup per poder animar lliurement
        var glg = choicePanelRT.GetComponent<GridLayoutGroup>();
        if (glg != null) glg.enabled = false;

        // Aplicar tamanys explícits i preparar animació
        for (int i = 0; i < choiceRTs.Count; i++)
        {
            choiceRTs[i].sizeDelta = targetSizes[i];
            choiceRTs[i].anchoredPosition = targetPositions[i] + new Vector2(-slideDistance, 0f);
            choiceCGs[i].alpha = 0f;
        }

        // Animar escalonadament
        for (int i = 0; i < choiceRTs.Count; i++)
        {
            StartCoroutine(AnimateSingleChoice(choiceRTs[i], choiceCGs[i], targetPositions[i], slideDistance, animTime));
            yield return new WaitForSecondsRealtime(staggerDelay);
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
            // Ease out cubic
            float eased = 1f - Mathf.Pow(1f - u, 3f);

            rt.anchoredPosition = Vector2.Lerp(startPos, targetPos, eased);
            cg.alpha = eased;
            yield return null;
        }

        rt.anchoredPosition = targetPos;
        cg.alpha = 1f;
    }

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

    private void LayoutUI(bool rightSide, bool onTop, bool hasPortrait, bool isThought)
    {
        if (currentPanelGO == null) return;

        if (onTop)
        {
            panelRect.anchorMin = new Vector2(0.12f, 0.70f);
            panelRect.anchorMax = new Vector2(0.88f, 0.92f);
            if (choicePanelRT != null)
            {
                choicePanelRT.anchorMin = new Vector2(0f, 0f);
                choicePanelRT.anchorMax = new Vector2(1f, 0f);
                choicePanelRT.pivot = new Vector2(0.5f, 1f); // Grow downwards
                var glg = choicePanelRT.GetComponent<GridLayoutGroup>();
                if (glg != null) glg.childAlignment = TextAnchor.UpperCenter;
                choicePanelRT.anchoredPosition = new Vector2(0f, -20f); // Just below
                choicePanelRT.sizeDelta = new Vector2(0f, 20f); // Stretch width
            }

        }
        else
        {
            panelRect.anchorMin = new Vector2(0.12f, 0.05f);
            panelRect.anchorMax = new Vector2(0.88f, 0.34f); // Increased panel proportion (from 0.27f)
            if (choicePanelRT != null)
            {
                choicePanelRT.anchorMin = new Vector2(0f, 1f);
                choicePanelRT.anchorMax = new Vector2(1f, 1f);
                choicePanelRT.pivot = new Vector2(0.5f, 0f); // Grow upwards
                var glg = choicePanelRT.GetComponent<GridLayoutGroup>();
                if (glg != null) glg.childAlignment = TextAnchor.LowerCenter;
                choicePanelRT.anchoredPosition = new Vector2(0f, 20f); // Just above
                choicePanelRT.sizeDelta = new Vector2(0f, 20f); // Stretch width
            }

        }
        
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        
        shownPos = panelRect.anchoredPosition;

        if (panelBgImg != null)
        {
            if (isThought)
            {
                panelBgImg.sprite = GetCloudSprite();
                panelBgImg.type = Image.Type.Tiled;
                panelBgImg.pixelsPerUnitMultiplier = 0.12f; // Incrementem la distància de tile per reduir vèrtex general < 65k
                panelBgOl.effectDistance = new Vector2(4f, -4f); 
            }
            else
            {
                panelBgImg.sprite = null;
                panelBgImg.type = Image.Type.Sliced; // Super important per a quad simples sense Tilear
                panelBgOl.effectDistance = new Vector2(6f, -6f);
            }
        }

        var tRT = dialogueText.GetComponent<RectTransform>();
        var nbRT = nameBoxGO.GetComponent<RectTransform>();

        if (portRT != null) portRT.gameObject.SetActive(hasPortrait);
        if (dividerRT != null) dividerRT.gameObject.SetActive(hasPortrait);

        if (!hasPortrait)
        {
            // Ample Complert sense foto
            tRT.anchorMin = new Vector2(0f, 0f); tRT.anchorMax = new Vector2(1f, 1f);
            tRT.offsetMin = tRT.offsetMax = Vector2.zero;

            nbRT.anchorMin = new Vector2(0f, 1f); nbRT.anchorMax = new Vector2(0f, 1f);
            nbRT.sizeDelta = new Vector2(320f, 75f); // Expanded name box
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
            nbRT.sizeDelta = new Vector2(320f, 75f); // Expanded name box
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
            nbRT.sizeDelta = new Vector2(320f, 75f); // Expanded name box
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

    // -----------------------
    // Animació del panell
    // -----------------------
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
            float eased = 1f - Mathf.Pow(1f - u, 3f);

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
            float eased = u * u; // Eased de 0 a 1

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
