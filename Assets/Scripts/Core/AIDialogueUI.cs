using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Gestiona la interfície del mode IA: mostra respostes del NPC,
/// permet al jugador escriure text i enviar-lo a Ollama.
/// Reutilitza visualment el quadre de diàleg existent del joc.
/// </summary>
public class AIDialogueUI : MonoBehaviour
{
    // --- Singleton lleuger ---
    private static AIDialogueUI _instance;
    public static AIDialogueUI Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("AIDialogueUI");
                _instance = go.AddComponent<AIDialogueUI>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    public bool IsOpen { get; private set; }
    public System.Action OnAIDialogueClosed;

    // --- Referències UI construïdes dinàmicament ---
    private Canvas rootCanvas;
    private GameObject panelGO;
    private RectTransform panelRT;
    private CanvasGroup panelGroup;
    private TextMeshProUGUI npcResponseText;
    private TextMeshProUGUI npcNameText;
    private GameObject npcNameBoxGO;
    private TMP_InputField playerInputField;
    private TextMeshProUGUI hintText;
    private Image panelBgImg;
    private Image aiPortraitImage;
    private GameObject thinkingBubbleGO;
    private RectTransform[] bubbleRTs;
    private Sprite circleSprite;
    private RectTransform inputContainerRT;

    // --- Audio ---
    private AudioSource audioSource;
    private AudioClip currentPlayerTypingSound;
    private AudioClip currentAITypingSound;

    // --- Estat ---
    private Interactable currentNPC;
    private bool isWaitingForResponse;
    private bool isBuilt;
    private Coroutine typingRoutine;

    // --- Animació ---
    private float animDuration = 0.4f;
    private Coroutine animRoutine;
    private Vector2 shownPos;

    // --- Font cache (reutilitza la mateixa font del joc) ---
    private static TMP_FontAsset cachedFont;

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (!IsOpen) return;
        if (PauseMenuUI.IsOpen) return;

        // Sortir amb Escape
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
            return;
        }

        // Nota: L'Enter es gestiona via l'event onSubmit del TMP_InputField,
        // perquè el InputField consumeix la tecla Return abans que arribi a Update().
    }

    /// <summary>
    /// Obre el mode IA per a un NPC concret.
    /// </summary>
    public void Open(Interactable npc)
    {
        if (IsOpen) Close();

        currentNPC = npc;
        IsOpen = true;
        isWaitingForResponse = false;

        // Configurar sons d'aquest NPC
        currentPlayerTypingSound = npc.playerTypingSound;
        currentAITypingSound = npc.aiTypingSound;

        // Assegurar AudioSource
        if (audioSource == null)
        {
            audioSource = gameObject.GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        BuildUI();

        // Netejar tot abans de començar
        if (npcResponseText != null) 
        {
            npcResponseText.text = "";
            npcResponseText.maxVisibleCharacters = 0;
        }
        if (thinkingBubbleGO != null) thinkingBubbleGO.SetActive(false);

        // Configurar portrait si en té
        if (aiPortraitImage != null)
        {
            if (npc.aiPortrait != null)
            {
                aiPortraitImage.sprite = npc.aiPortrait;
                aiPortraitImage.enabled = true;
            }
            else
            {
                aiPortraitImage.enabled = false;
            }
        }

        // Mostrar missatge inicial si en té (amb un mini-retard per seguretat)
        if (!string.IsNullOrEmpty(npc.aiFirstMessage))
        {
            if (typingRoutine != null) StopCoroutine(typingRoutine);
            typingRoutine = StartCoroutine(TypeInitialMessageDelayed(npc.aiFirstMessage));
        }
        else if (npcResponseText != null)
        {
            npcResponseText.text = "";
        }
        else
        {
            // Si no hi ha missatge inicial, almenys buidem el camp
            if (npcResponseText != null) npcResponseText.text = "";
        }

        // Configurar nom del NPC
        if (npcNameBoxGO != null && npcNameText != null)
        {
            if (!string.IsNullOrEmpty(npc.aiCharacterName))
            {
                npcNameBoxGO.SetActive(true);
                npcNameText.text = npc.aiCharacterName;
            }
            else
            {
                npcNameBoxGO.SetActive(false);
            }
        }

        // Missatge inicial
        if (npcResponseText != null)
        {
            npcResponseText.text = "...";
        }

        // Mostrar panell
        if (panelGO != null) panelGO.SetActive(true);
        PlayIn();

        // Focus a l'input
        if (playerInputField != null)
        {
            playerInputField.text = "";
            playerInputField.ActivateInputField();
            playerInputField.Select();
        }
    }

    /// <summary>
    /// Tanca el mode IA.
    /// </summary>
    public void Close()
    {
        if (!IsOpen) return;

        IsOpen = false;
        isWaitingForResponse = false;

        if (typingRoutine != null) { StopCoroutine(typingRoutine); typingRoutine = null; }

        PlayOut();
    }

    private IEnumerator TypeInitialMessageDelayed(string message)
    {
        yield return new WaitForSecondsRealtime(0.1f);
        yield return TypeResponseRoutine(message);
    }

    /// <summary>
    /// Cridat pel event onValueChanged del TMP_InputField quan el jugador escriu.
    /// </summary>
    private void OnPlayerTyping(string newValue)
    {
        if (currentPlayerTypingSound != null && audioSource != null && !string.IsNullOrEmpty(newValue))
        {
            audioSource.pitch = Random.Range(0.9f, 1.1f);
            audioSource.PlayOneShot(currentPlayerTypingSound, 0.6f);
        }
    }

    /// <summary>
    /// Cridat pel event onSubmit del TMP_InputField quan el jugador prem Enter.
    /// </summary>
    private void OnInputSubmit(string text)
    {
        if (!IsOpen || isWaitingForResponse) return;
        if (string.IsNullOrWhiteSpace(text)) return;
        SendPlayerMessage();
    }

    private void SendPlayerMessage()
    {
        string message = playerInputField.text.Trim();
        if (string.IsNullOrEmpty(message)) return;

        Debug.Log($"[AIDialogueUI] Enviant missatge a Ollama: '{message}'");

        playerInputField.text = "";
        isWaitingForResponse = true;

        // Mostrar "Pensant..." mentre espera
        if (npcResponseText != null)
        {
            if (typingRoutine != null) { StopCoroutine(typingRoutine); typingRoutine = null; }
            typingRoutine = StartCoroutine(ThinkingAnimation());
        }

        // Desactivar input temporalment
        if (playerInputField != null)
        {
            playerInputField.interactable = false;
        }

        // Enviar a Ollama
        OllamaDialogueClient.Instance.SendMessage(currentNPC, message, OnResponseReceived);
    }

    private void OnResponseReceived(string response)
    {
        try
        {
            Debug.Log($"[AIDialogueUI] Resposta rebuda: '{(response != null ? response.Substring(0, Mathf.Min(80, response.Length)) : "NULL")}'");

            isWaitingForResponse = false;

            if (typingRoutine != null) { StopCoroutine(typingRoutine); typingRoutine = null; }

            // Mostrar resposta
            if (npcResponseText != null)
            {
                npcResponseText.maxVisibleCharacters = int.MaxValue; // Reset per seguretat
                typingRoutine = StartCoroutine(TypeResponseRoutine(response ?? OllamaDialogueClient.FallbackMessage));
            }

            // Reactivar input
            if (playerInputField != null && IsOpen)
            {
                playerInputField.interactable = true;
                StartCoroutine(RefocusInputNextFrame());
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AIDialogueUI] ERROR a OnResponseReceived: {e.Message}\n{e.StackTrace}");
            isWaitingForResponse = false;
            // Intentem mostrar el text directament com a últim recurs
            if (npcResponseText != null)
            {
                npcResponseText.text = response ?? OllamaDialogueClient.FallbackMessage;
                npcResponseText.maxVisibleCharacters = int.MaxValue;
            }
            if (playerInputField != null)
            {
                playerInputField.interactable = true;
            }
        }
    }

    private IEnumerator RefocusInputNextFrame()
    {
        yield return null; // Esperar un frame
        if (playerInputField != null && IsOpen)
        {
            playerInputField.ActivateInputField();
            playerInputField.Select();
        }
    }

    private IEnumerator ThinkingAnimation()
    {
        if (thinkingBubbleGO == null) yield break;
        
        thinkingBubbleGO.SetActive(true);
        npcResponseText.text = ""; // Netegem el text mentre pensa
        
        float timer = 0f;
        while (isWaitingForResponse)
        {
            timer += Time.unscaledDeltaTime;
            for (int i = 0; i < bubbleRTs.Length; i++)
            {
                float offset = Mathf.Sin(timer * 5f + (i * 0.8f)) * 10f;
                bubbleRTs[i].anchoredPosition = new Vector2(bubbleRTs[i].anchoredPosition.x, offset);
                
                // Efecte de escala també
                float scale = 1f + Mathf.Sin(timer * 5f + (i * 0.8f)) * 0.2f;
                bubbleRTs[i].localScale = new Vector3(scale, scale, 1f);
            }
            yield return null;
        }
        
        thinkingBubbleGO.SetActive(false);
    }

    private IEnumerator TypeResponseRoutine(string text)
    {
        if (npcResponseText == null) yield break;

        // Amagar l'animació de càrrega quan comencem a escriure la resposta
        if (thinkingBubbleGO != null) thinkingBubbleGO.SetActive(false);

        npcResponseText.text = text;
        npcResponseText.maxVisibleCharacters = 0;

        float charsPerSecond = 50f;
        float delay = 1f / charsPerSecond;

        for (int i = 0; i < text.Length; i++)
        {
            npcResponseText.maxVisibleCharacters = i + 1;

            // So de tecleig IA per cada caràcter visible (no espais)
            char c = text[i];
            if (currentAITypingSound != null && c != ' ' && c != '\n')
            {
                audioSource.pitch = Random.Range(0.95f, 1.05f);
                audioSource.PlayOneShot(currentAITypingSound, 0.5f);
            }

            float currentDelay = delay;
            if (c == '.' || c == '?' || c == '!') currentDelay = delay * 8f;
            else if (c == ',' || c == ';' || c == ':') currentDelay = delay * 4f;

            yield return new WaitForSecondsRealtime(currentDelay);
        }

        npcResponseText.maxVisibleCharacters = text.Length;
        typingRoutine = null;
    }

    // =========================================
    // UI Builder
    // =========================================
    private void BuildUI()
    {
        if (isBuilt && panelGO != null) return;

        // Trobar el Canvas principal
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

        rootCanvas = targetCanvas;

        // --- Panell principal ---
        panelGO = new GameObject("AIDialoguePanel");
        panelRT = panelGO.AddComponent<RectTransform>();
        panelRT.SetParent(rootCanvas.transform, false);

        // Canvas propi amb sorting alt
        Canvas diagCanvas = panelGO.AddComponent<Canvas>();
        diagCanvas.overrideSorting = true;
        diagCanvas.sortingOrder = 10000;
        panelGO.AddComponent<GraphicRaycaster>();

        panelRT.anchorMin = new Vector2(0.12f, 0.05f);
        panelRT.anchorMax = new Vector2(0.88f, 0.40f);
        panelRT.offsetMin = panelRT.offsetMax = Vector2.zero;
        shownPos = panelRT.anchoredPosition;

        panelGroup = panelGO.AddComponent<CanvasGroup>();
        panelGroup.alpha = 0f;

        panelBgImg = panelGO.AddComponent<Image>();
        panelBgImg.color = new Color(0.08f, 0.08f, 0.16f, 0.95f);
        var panelOl = panelGO.AddComponent<Outline>();
        panelOl.effectColor = new Color(0.4f, 0.8f, 1f, 0.8f);
        panelOl.effectDistance = new Vector2(6f, -6f);

        // --- NPC Name Box ---
        npcNameBoxGO = new GameObject("AINameBox");
        npcNameBoxGO.transform.SetParent(panelRT, false);
        var nbRT = npcNameBoxGO.AddComponent<RectTransform>();
        nbRT.anchorMin = new Vector2(0f, 1f);
        nbRT.anchorMax = new Vector2(0f, 1f);
        nbRT.sizeDelta = new Vector2(320f, 75f);
        nbRT.pivot = new Vector2(0f, 0f);
        nbRT.anchoredPosition = new Vector2(15f, 8f);

        var nbImg = npcNameBoxGO.AddComponent<Image>();
        nbImg.color = new Color(0.15f, 0.25f, 0.4f, 1f);
        var nbOl = npcNameBoxGO.AddComponent<Outline>();
        nbOl.effectColor = new Color(0.4f, 0.8f, 1f, 0.8f);
        nbOl.effectDistance = new Vector2(6f, -6f);

        var nameTextGO = new GameObject("AINameText");
        nameTextGO.transform.SetParent(nbRT, false);
        var ntRT = nameTextGO.AddComponent<RectTransform>();
        ntRT.anchorMin = Vector2.zero; ntRT.anchorMax = Vector2.one;
        ntRT.offsetMin = ntRT.offsetMax = Vector2.zero;
        npcNameText = nameTextGO.AddComponent<TextMeshProUGUI>();
        npcNameText.margin = new Vector4(10f, 5f, 10f, 5f);
        SetFont(npcNameText, 48f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
        npcNameText.enableAutoSizing = true;
        npcNameText.fontSizeMin = 30f;
        npcNameText.fontSizeMax = 54f;

        // --- Zona de resposta del NPC (part superior del panell) ---
        var responseGO = new GameObject("AIResponseBox");
        responseGO.transform.SetParent(panelRT, false);
        var rRT = responseGO.AddComponent<RectTransform>();
        rRT.anchorMin = new Vector2(0.30f, 0.28f); // Comença després de l'espai de la foto
        rRT.anchorMax = new Vector2(0.98f, 0.95f);
        rRT.offsetMin = rRT.offsetMax = Vector2.zero;

        npcResponseText = responseGO.AddComponent<TextMeshProUGUI>();
        npcResponseText.margin = new Vector4(10f, 10f, 10f, 10f); // Marges interns més nets
        npcResponseText.textWrappingMode = TextWrappingModes.Normal;
        npcResponseText.fontSizeMax = 70f;
        npcResponseText.fontSizeMin = 32f;
        npcResponseText.enableAutoSizing = true;
        SetFont(npcResponseText, 55f, Color.white, FontStyles.Normal, TextAlignmentOptions.TopLeft);

        // --- Portrait (Cantonada esquerra dedicada) ---
        var portraitBoxGO = new GameObject("AIPortraitBox");
        portraitBoxGO.transform.SetParent(panelRT, false);
        var pbRT = portraitBoxGO.AddComponent<RectTransform>();
        pbRT.anchorMin = new Vector2(0.02f, 0.05f); // Una mica de marge des de la vora del panell
        pbRT.anchorMax = new Vector2(0.28f, 0.95f); // Ocupa el primer 28% de l'ample i gairebé tot l'alt
        pbRT.offsetMin = pbRT.offsetMax = Vector2.zero;

        var portraitGO = new GameObject("AIPortraitImage");
        portraitGO.transform.SetParent(pbRT, false);
        var pRT = portraitGO.AddComponent<RectTransform>();
        pRT.anchorMin = Vector2.zero; pRT.anchorMax = Vector2.one;
        pRT.offsetMin = Vector2.zero; pRT.offsetMax = Vector2.zero;
        
        aiPortraitImage = portraitGO.AddComponent<Image>();
        aiPortraitImage.preserveAspect = true;
        aiPortraitImage.enabled = false;

        // --- Thinking Bubbles ---
        thinkingBubbleGO = new GameObject("AIThinkingBubbles");
        thinkingBubbleGO.transform.SetParent(panelRT, false);
        var tbRT = thinkingBubbleGO.AddComponent<RectTransform>();
        tbRT.anchorMin = new Vector2(0.5f, 0.65f);
        tbRT.anchorMax = new Vector2(0.5f, 0.65f);
        tbRT.sizeDelta = new Vector2(200f, 100f);
        tbRT.anchoredPosition = Vector2.zero;
        
        if (circleSprite == null) circleSprite = CreateCircleSprite();
        
        bubbleRTs = new RectTransform[3];
        for (int i = 0; i < 3; i++)
        {
            var dot = new GameObject("Dot" + i);
            dot.transform.SetParent(tbRT, false);
            var dRT = dot.AddComponent<RectTransform>();
            dRT.sizeDelta = new Vector2(22f, 22f);
            dRT.anchoredPosition = new Vector2(-50f + (i * 50f), 0f);
            var dImg = dot.AddComponent<Image>();
            dImg.sprite = circleSprite;
            dImg.color = new Color(0.4f, 0.8f, 1f, 0.8f);
            bubbleRTs[i] = dRT;
        }
        thinkingBubbleGO.SetActive(false);

        // --- Separador visual ---
        var separatorGO = new GameObject("AISeparator");
        separatorGO.transform.SetParent(panelRT, false);
        var sepRT = separatorGO.AddComponent<RectTransform>();
        sepRT.anchorMin = new Vector2(0.30f, 0.27f); // Alineat amb el text per no tapar la foto
        sepRT.anchorMax = new Vector2(0.95f, 0.27f);
        sepRT.sizeDelta = new Vector2(0f, 3f);
        sepRT.anchoredPosition = Vector2.zero;
        var sepImg = separatorGO.AddComponent<Image>();
        sepImg.color = new Color(0.4f, 0.8f, 1f, 0.5f);

        // --- Camp d'entrada de text del jugador (part inferior) ---
        var inputContainerGO = new GameObject("AIInputContainer");
        inputContainerGO.transform.SetParent(panelRT, false);
        inputContainerRT = inputContainerGO.AddComponent<RectTransform>();
        inputContainerRT.anchorMin = new Vector2(0.30f, 0.04f); // Comença després de la foto
        inputContainerRT.anchorMax = new Vector2(0.98f, 0.25f);
        inputContainerRT.offsetMin = inputContainerRT.offsetMax = Vector2.zero;

        var icImg = inputContainerGO.AddComponent<Image>();
        icImg.color = new Color(0.05f, 0.05f, 0.12f, 0.9f);
        var icOl = inputContainerGO.AddComponent<Outline>();
        icOl.effectColor = new Color(0.3f, 0.6f, 0.8f, 0.6f);
        icOl.effectDistance = new Vector2(3f, -3f);

        // Input Field (TMP)
        var inputGO = new GameObject("AIInputField");
        inputGO.transform.SetParent(inputContainerRT, false);
        var inputRT = inputGO.AddComponent<RectTransform>();
        inputRT.anchorMin = Vector2.zero; inputRT.anchorMax = Vector2.one;
        inputRT.offsetMin = new Vector2(15f, 5f); inputRT.offsetMax = new Vector2(-15f, -5f);

        // TextArea (necessari per a TMP_InputField)
        var textAreaGO = new GameObject("Text Area");
        textAreaGO.transform.SetParent(inputRT, false);
        var taRT = textAreaGO.AddComponent<RectTransform>();
        taRT.anchorMin = Vector2.zero; taRT.anchorMax = Vector2.one;
        taRT.offsetMin = Vector2.zero; taRT.offsetMax = Vector2.zero;
        var taMask = textAreaGO.AddComponent<RectMask2D>();

        // Text del jugador
        var playerTextGO = new GameObject("Text");
        playerTextGO.transform.SetParent(taRT, false);
        var ptRT = playerTextGO.AddComponent<RectTransform>();
        ptRT.anchorMin = Vector2.zero; ptRT.anchorMax = Vector2.one;
        ptRT.offsetMin = Vector2.zero; ptRT.offsetMax = Vector2.zero;
        var playerText = playerTextGO.AddComponent<TextMeshProUGUI>();
        SetFont(playerText, 44f, new Color(0.9f, 0.95f, 1f, 1f), FontStyles.Normal, TextAlignmentOptions.Left);
        playerText.enableAutoSizing = true;
        playerText.fontSizeMin = 28f;
        playerText.fontSizeMax = 50f;

        // Placeholder
        var placeholderGO = new GameObject("Placeholder");
        placeholderGO.transform.SetParent(taRT, false);
        var phRT = placeholderGO.AddComponent<RectTransform>();
        phRT.anchorMin = Vector2.zero; phRT.anchorMax = Vector2.one;
        phRT.offsetMin = Vector2.zero; phRT.offsetMax = Vector2.zero;
        var placeholderText = placeholderGO.AddComponent<TextMeshProUGUI>();
        SetFont(placeholderText, 44f, new Color(0.5f, 0.55f, 0.65f, 0.7f), FontStyles.Italic, TextAlignmentOptions.Left);
        placeholderText.text = "Type here...  (Enter to send, Esc to exit)";
        placeholderText.enableAutoSizing = true;
        placeholderText.fontSizeMin = 22f;
        placeholderText.fontSizeMax = 44f;

        // Configurar InputField
        playerInputField = inputGO.AddComponent<TMP_InputField>();
        playerInputField.textViewport = taRT;
        playerInputField.textComponent = playerText;
        playerInputField.placeholder = placeholderText;
        playerInputField.fontAsset = playerText.font;
        playerInputField.pointSize = 44f;
        playerInputField.characterLimit = 300;
        playerInputField.lineType = TMP_InputField.LineType.SingleLine;

        // IMPORTANT: Connectar l'event onSubmit per capturar Enter
        // (TMP_InputField consumeix la tecla Return, Input.GetKeyDown no la detecta)
        playerInputField.onSubmit.AddListener(OnInputSubmit);

        // So de tecleig del jugador per cada caràcter escrit
        playerInputField.onValueChanged.AddListener(OnPlayerTyping);

        // Colors de l'InputField
        var inputColors = playerInputField.colors;
        inputColors.normalColor = Color.white;
        inputColors.highlightedColor = new Color(0.4f, 0.8f, 1f, 1f);
        inputColors.selectedColor = Color.white;
        playerInputField.colors = inputColors;

        // Caret (cursor) color
        playerInputField.caretColor = new Color(0.4f, 0.8f, 1f, 1f);
        playerInputField.caretWidth = 3;
        playerInputField.customCaretColor = true;
        playerInputField.selectionColor = new Color(0.2f, 0.5f, 0.8f, 0.4f);

        // --- Hint text (Escape per sortir) a baix a la dreta ---
        var hintGO = new GameObject("AIHintText");
        hintGO.transform.SetParent(rootCanvas.transform, false);
        var hintRT = hintGO.AddComponent<RectTransform>();
        hintRT.SetParent(panelRT, false);
        hintRT.anchorMin = new Vector2(1f, 0f);
        hintRT.anchorMax = new Vector2(1f, 0f);
        hintRT.sizeDelta = new Vector2(300f, 40f);
        hintRT.pivot = new Vector2(1f, 1f);
        hintRT.anchoredPosition = new Vector2(-10f, -8f);

        hintText = hintGO.AddComponent<TextMeshProUGUI>();
        SetFont(hintText, 24f, new Color(0.5f, 0.6f, 0.7f, 0.6f), FontStyles.Italic, TextAlignmentOptions.Right);
        hintText.text = "[Esc] Exit";

        panelGO.SetActive(false);
        isBuilt = true;
    }

    // =========================================
    // Font Helper (reutilitza la font 8-bit del joc)
    // =========================================
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

    // =========================================
    // Animació (mateixa estètica que DialogueUI)
    // =========================================
    private void PlayIn()
    {
        if (panelRT == null || panelGroup == null) return;
        if (animRoutine != null) StopCoroutine(animRoutine);
        animRoutine = StartCoroutine(AnimateIn());
    }

    private void PlayOut()
    {
        if (panelRT == null || panelGroup == null)
        {
            if (panelGO) panelGO.SetActive(false);
            OnAIDialogueClosed?.Invoke();
            return;
        }
        if (animRoutine != null) StopCoroutine(animRoutine);
        animRoutine = StartCoroutine(AnimateOut());
    }

    private IEnumerator AnimateIn()
    {
        panelGroup.alpha = 1f;
        float slideDist = 800f;
        Vector2 offset = new Vector2(0f, -slideDist);
        panelRT.anchoredPosition = shownPos + offset;
        panelRT.localScale = Vector3.one;

        float t = 0f;
        while (t < animDuration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / Mathf.Max(0.0001f, animDuration));
            float eased = 1f - Mathf.Pow(1f - u, 3f);
            panelRT.anchoredPosition = Vector2.Lerp(shownPos + offset, shownPos, eased);
            yield return null;
        }

        panelGroup.alpha = 1f;
        panelRT.anchoredPosition = shownPos;
        animRoutine = null;
    }

    private IEnumerator AnimateOut()
    {
        panelGroup.alpha = 1f;
        Vector2 startPos = panelRT.anchoredPosition;
        float slideDist = 800f;
        Vector2 endPos = shownPos + new Vector2(0f, -slideDist);

        float t = 0f;
        while (t < animDuration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / Mathf.Max(0.0001f, animDuration));
            float eased = u * u;
            panelRT.anchoredPosition = Vector2.Lerp(startPos, endPos, eased);
            yield return null;
        }

        panelGroup.alpha = 0f;
        panelRT.anchoredPosition = endPos;
        if (panelGO) panelGO.SetActive(false);

        animRoutine = null;
        OnAIDialogueClosed?.Invoke();
    }
    private Sprite CreateCircleSprite()
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
                float alpha = d <= 1f ? 1f : 0f;
                // Soft edge
                if (d > 0.8f && d <= 1f) alpha = 1f - (d - 0.8f) / 0.2f;
                tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
