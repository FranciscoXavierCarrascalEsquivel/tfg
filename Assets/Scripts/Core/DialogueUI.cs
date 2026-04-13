using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class DialogueUI : MonoBehaviour
{
    [Header("Typewriter")]
    [SerializeField] private float charsPerSecond = 40f;
    [SerializeField] private bool skipSpaces = true;
    [SerializeField] private int soundEveryNChars = 2;

    [Header("Typing Sounds")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] typingClips;
    [Range(0f, 0.4f)][SerializeField] private float pitchRandom = 0.05f;
    [Range(0f, 1f)][SerializeField] private float volume = 0.8f;

    [Header("Panel Animation")]
    [SerializeField] private float animDuration = 0.15f; 
    [SerializeField] private float slidePixels = 40f;    
    [SerializeField] private bool animateOnShow = true;  

    private Coroutine typingRoutine;
    private Coroutine animRoutine;

    private string fullText;
    private bool isOpen;
    private bool isTyping;
    private int typedCount;

    public System.Action OnDialogueClosed;

    // Generated UI refs
    private GameObject currentPanelGO;
    private RectTransform panelRect;
    private CanvasGroup panelGroup;
    private TextMeshProUGUI dialogueText;
    private TextMeshProUGUI nameText;
    private Image portraitImage;
    private Animator portraitAnimator;
    private GameObject nameBoxGO;
    private CanvasGroup eBtnGroup;
    private RectTransform eTextRT;
    private RectTransform dividerRT;
    private RectTransform portRT;
    private Image panelBgImg;
    private Outline panelBgOl;

    private Vector2 shownPos = Vector2.zero;

    private Interactable.DialogueLine[] sequence;
    private int seqIndex;
    private bool inSequence;
    private bool isReopening;
    private bool isCurrentOnTop;
    private bool isHiding;
    
    private AudioClip currentLineVoice;

    // Branches
    private Interactable.DialogueChoice[] currentLineChoices;
    private bool isSelectingChoice;
    private int selectedChoiceIdx;
    private RectTransform choicePanelRT;
    private System.Collections.Generic.List<TextMeshProUGUI> choiceTexts = new System.Collections.Generic.List<TextMeshProUGUI>();

    private void Awake()
    {
        if (!audioSource) audioSource = GetComponent<AudioSource>();
        // S'ha eliminat la necessitat d'assignar res per inspector, el construim quan fem Show()
    }

    private void Update()
    {
        if (eBtnGroup != null && eTextRT != null)
        {
            if (isOpen && !isHiding && !isReopening && currentPanelGO.activeSelf && !isTyping)
            {
                if (isSelectingChoice) { eBtnGroup.alpha = 0f; }
                else
                {
                    eBtnGroup.alpha = 1f;
                    float cycle = Time.unscaledTime * 1.5f;
                    bool isPressed = (cycle % 1f) > 0.7f;
                    eTextRT.anchoredPosition = isPressed ? Vector2.zero : new Vector2(0f, 4f);
                }
            }
            else
            {
                eBtnGroup.alpha = 0f;
            }
        }

        if (isSelectingChoice && choiceTexts.Count > 0)
        {
            bool up = Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow);
            bool down = Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow);
            bool moved = false;

            if (up) { selectedChoiceIdx--; if (selectedChoiceIdx < 0) selectedChoiceIdx = choiceTexts.Count - 1; moved = true; }
            if (down) { selectedChoiceIdx++; if (selectedChoiceIdx >= choiceTexts.Count) selectedChoiceIdx = 0; moved = true; }

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

    public bool IsOpen => isOpen;
    public bool IsTyping => isTyping;

    public void SetTypingSound(AudioClip clip, int every = 2)
    {
        if (clip != null) typingClips = new AudioClip[] { clip };
        soundEveryNChars = every;
    }

    public void StartDialogue(Interactable.DialogueLine[] lines, bool animateIn = true)
    {
        if (lines == null || lines.Length == 0)
        {
            Hide(); 
            return;
        }

        BuildDynamicUI();

        sequence = lines;
        seqIndex = 0;
        inSequence = true;

        ShowInternal(sequence[0], playInAnim: animateIn);
    }

    public void Show(string text, Sprite portrait = null, RuntimeAnimatorController portraitAnim = null)
    {
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
        isOpen = true;
        fullText = line?.text ?? "";
        typedCount = 0;

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

        // NOU: Força el càlcul de la mida ideal de la lletra només amb el text complet
        if (dialogueText != null)
        {
            dialogueText.enableAutoSizing = true;
            dialogueText.text = fullText;
            dialogueText.ForceMeshUpdate();
            dialogueText.enableAutoSizing = false;
        }

        if (typingRoutine != null) StopCoroutine(typingRoutine);
        typingRoutine = StartCoroutine(TypeRoutine(fullText));
    }

    public void Hide()
    {
        if (isHiding) return;
        isHiding = true;

        inSequence = false;
        sequence = null;
        seqIndex = 0;

        isTyping = false;
        isSelectingChoice = false;
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

        if (isTyping)
        {
            if (typingRoutine != null) StopCoroutine(typingRoutine);
            typingRoutine = null;

            isTyping = false;
            if (dialogueText) dialogueText.text = fullText;
            
            if (currentLineChoices != null && currentLineChoices.Length > 0)
            {
                ShowChoices();
            }
            return;
        }

        if (isSelectingChoice)
        {
            if (PlayerInventory.Instance != null && PlayerInventory.Instance.selectSound != null)
            {
                ItemSoundPlayer.Play(PlayerInventory.Instance.selectSound);
            }

            var choice = currentLineChoices[selectedChoiceIdx];
            choice.onChoiceSelected?.Invoke();
            
            isSelectingChoice = false;
            foreach(Transform child in choicePanelRT) Destroy(child.gameObject);
            choiceTexts.Clear();
            
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
            AdvanceToLine(nextIdx);
            return;
        }

        Hide();
    }

    private void AdvanceToLine(int nextIdx)
    {
        if (sequence == null) { Hide(); return; }
        
        seqIndex = nextIdx;
        if (seqIndex >= 0 && seqIndex < sequence.Length)
        {
            var nextLine = sequence[seqIndex];
            if (nextLine.forceReopen) StartCoroutine(ReopenRoutine(nextLine));
            else ShowInternal(nextLine, false);
            return;
        }
        Hide();
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
        if (dialogueText) dialogueText.text = "";

        string cleanText = text.Trim();
        float delay = 1f / Mathf.Max(1f, charsPerSecond);

        for (int i = 0; i < cleanText.Length; i++)
        {
            char c = cleanText[i];
            if (dialogueText) dialogueText.text += c;

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
        if (currentPanelGO != null) return;
        
        var canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        currentPanelGO = new GameObject("DynamicDialoguePanel");
        panelRect = currentPanelGO.AddComponent<RectTransform>();
        panelRect.SetParent(canvas.transform, false);

        panelRect.anchorMin = new Vector2(0.12f, 0.05f);
        panelRect.anchorMax = new Vector2(0.88f, 0.28f);
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
        dialogueText.enableWordWrapping = true;
        dialogueText.fontSizeMax = 50f; dialogueText.fontSizeMin = 24f;
        dialogueText.enableAutoSizing = true;
        SetFont(dialogueText, 42f, Color.white, FontStyles.Normal, TextAlignmentOptions.TopLeft);

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
        SetFont(nameText, 32f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
        nameText.enableAutoSizing = true; nameText.fontSizeMin = 18f; nameText.fontSizeMax = 36f;

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
        eRT.sizeDelta = new Vector2(36f, 36f);
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
        SetFont(eTextComp, 26f, new Color(0.05f, 0.05f, 0.05f, 1f), FontStyles.Bold, TextAlignmentOptions.Center);
        eTextComp.text = "E";
        eTextComp.margin = new Vector4(2f, 2f, 0f, 0f);

        // Branching Choices Box
        var chGO = new GameObject("ChoicePanel");
        chGO.transform.SetParent(panelRect, false);
        choicePanelRT = chGO.AddComponent<RectTransform>();
        choicePanelRT.pivot = new Vector2(1f, 0f); // Baix Dreta
        var chVLG = chGO.AddComponent<VerticalLayoutGroup>();
        chVLG.childAlignment = TextAnchor.LowerRight;
        chVLG.spacing = 10f;
        chVLG.childForceExpandHeight = false;
        chVLG.childForceExpandWidth = true;

        currentPanelGO.SetActive(false);
    }

    private void ShowChoices()
    {
        isSelectingChoice = true;
        selectedChoiceIdx = 0;
        
        foreach(Transform child in choicePanelRT) Destroy(child.gameObject);
        choiceTexts.Clear();
        
        for (int i = 0; i < currentLineChoices.Length; i++)
        {
            var btnGO = new GameObject("ChoiceBtn");
            btnGO.transform.SetParent(choicePanelRT, false);
            var bRT = btnGO.AddComponent<RectTransform>();
            var le = btnGO.AddComponent<LayoutElement>();
            le.minHeight = 64f;

            var img = btnGO.AddComponent<Image>();
            img.color = new Color(0.1f, 0.1f, 0.2f, 0.95f);
            var ol = btnGO.AddComponent<Outline>();
            ol.effectColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            ol.effectDistance = new Vector2(3f, -3f);

            var txtGO = new GameObject("Txt");
            txtGO.transform.SetParent(btnGO.transform, false);
            var tRT = txtGO.AddComponent<RectTransform>();
            tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
            tRT.offsetMin = new Vector2(10f, 5f); tRT.offsetMax = new Vector2(-10f, -5f);
            
            var txt = txtGO.AddComponent<TextMeshProUGUI>();
            SetFont(txt, 40f, Color.white, FontStyles.Bold, TextAlignmentOptions.Right);
            txt.text = currentLineChoices[i].text;
            
            choiceTexts.Add(txt);
        }
        
        HighlightChoice();
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
                choicePanelRT.anchorMin = new Vector2(1f, 0f);
                choicePanelRT.anchorMax = new Vector2(1f, 0f);
                choicePanelRT.anchoredPosition = new Vector2(0f, -60f); // Salten per sota de la conversa si està dalt
                choicePanelRT.sizeDelta = new Vector2(400f, 20f);
            }
        }
        else
        {
            panelRect.anchorMin = new Vector2(0.12f, 0.05f);
            panelRect.anchorMax = new Vector2(0.88f, 0.27f);
            if (choicePanelRT != null)
            {
                choicePanelRT.anchorMin = new Vector2(1f, 1f);
                choicePanelRT.anchorMax = new Vector2(1f, 1f);
                choicePanelRT.anchoredPosition = new Vector2(0f, 60f); // Salten per dalt de la conversa si està baix
                choicePanelRT.sizeDelta = new Vector2(400f, 20f);
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
            nbRT.sizeDelta = new Vector2(240f, 56f);
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
            nbRT.sizeDelta = new Vector2(240f, 56f);
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
            nbRT.sizeDelta = new Vector2(240f, 56f);
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

    private void SetFont(TextMeshProUGUI t, float size, Color col, FontStyles style, TextAlignmentOptions align)
    {
        t.fontSize = size; t.color = col; t.fontStyle = style; t.alignment = align;
#if UNITY_EDITOR
        var f = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/8bitoperator_jve SDF.asset") 
             ?? UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/8bitoperator_jve SDF.asset");
        if (f != null) { t.font = f; return; }
#endif
        var r = Resources.Load<TMP_FontAsset>("Fonts & Materials/8bitoperator_jve SDF") ?? Resources.Load<TMP_FontAsset>("8bitoperator_jve SDF");
        if (r != null) t.font = r;
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
        float slideDist = 350f;
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
        float slideDist = 350f;
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
        if (dialogueText) dialogueText.text = "";

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
            float slideDist = 350f;
            Vector2 offset = new Vector2(0f, isCurrentOnTop ? slideDist : -slideDist);
            panelRect.anchoredPosition = shownPos + offset;
            panelRect.localScale = Vector3.one;
        }
        if (panelGroup != null) panelGroup.alpha = 0f;
        if (currentPanelGO != null) currentPanelGO.SetActive(false);

        if (dialogueText) dialogueText.text = "";
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
