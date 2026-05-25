using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Interfície gràfica i gestor del sistema de diàleg amb intel·ligència artificial (mode IA).
/// Aquest component és el responsable de renderitzar el quadre de diàleg avançat que permet
/// a l'usuari interaccionar de manera oberta escrivint text, el qual s'envia a un model local
/// de llenguatge (Ollama) per obtenir respostes dinàmiques generades en temps real pel NPC.
/// 
/// DISSENY I ESTRUCTURA:
/// - S'ha optat per una construcció 100% procedimental (per codi) de la interfície gràfica. D'aquesta
///   manera, evitem dependències rígides de prefabs de Unity, permetent que el sistema s'acobli
///   perfectament a qualsevol Canvas actiu en l'escena.
/// - Implementa un patró de disseny Singleton per garantir un únic punt d'accés global.
/// - Reutilitza la font tipogràfica retro (8-bit) per mantenir la coherència visual de la interfície.
/// - Ofereix animacions de transició (entrada/sortida de tipus lliscament suau) i efectes de tecleig de lletres.
/// </summary>
public class AIDialogueUI : MonoBehaviour
{
    // --- Singleton de la Interfície ---
    // Permet accedir a la UI des de qualsevol script de control o interacció del jugador
    private static AIDialogueUI _instance;
    public static AIDialogueUI Instance
    {
        get
        {
            if (_instance == null)
            {
                // Si per algun motiu no s'ha instanciat previament, la creem sota un GameObject buit persistent
                var go = new GameObject("AIDialogueUI");
                _instance = go.AddComponent<AIDialogueUI>();
                DontDestroyOnLoad(go); // Assegura la supervivència entre canvis d'escena
            }
            return _instance;
        }
    }

    public bool IsOpen { get; private set; } // Flag que indica si la finestra de xat IA està activa
    public System.Action OnAIDialogueClosed; // Callback per notificar al món que s'ha tancat el xat (ex. per alliberar el moviment del jugador)

    // --- Referències a components de la UI generats per codi ---
    private Canvas rootCanvas;
    private GameObject panelGO;
    private RectTransform panelRT;
    private CanvasGroup panelGroup;
    private TextMeshProUGUI npcResponseText; // Text on es bolca la resposta generada per la IA
    private TextMeshProUGUI npcNameText;     // Etiqueta del nom de l'enemic/NPC
    private GameObject npcNameBoxGO;         // Caixa visual de fons del nom
    private TMP_InputField playerInputField; // Camp de text editable on escriu el jugador
    private TextMeshProUGUI hintText;         // Indicadors ràpids per al teclat (ex: Esc per sortir)
    private Image panelBgImg;
    private Image aiPortraitImage;           // Imatge d'avatar o retrat de la criatura
    private GameObject thinkingBubbleGO;     // Contenidor dels punts suspensius de "Pensant..."
    private RectTransform[] bubbleRTs;       // Punts físics de la bombolla per animar-los individualment
    private Sprite circleSprite;             // Sprite circular generat dinàmicament per procediment
    private RectTransform inputContainerRT;

    // --- Control d'Àudio i Feedbacks ---
    private AudioSource audioSource;
    private AudioClip currentPlayerTypingSound; // So al teclejar el jugador
    private AudioClip currentAITypingSound;     // So de l'efecte de tecleig retro quan parla el NPC

    // --- Control d'Estat del Flux ---
    private Interactable currentNPC;         // Referència de la base de dades de l'entitat interaccionada
    private bool isWaitingForResponse;       // Flag per evitar re-enviaments de prompts mentre la IA pensa
    private bool isBuilt;                    // Control per no recrear la UI procedimental cada vegada que s'obre
    private Coroutine typingRoutine;         // Referència a la corrutina d'escriptura activa (per poder tallar-la)

    // --- Retrats d'Expressió Emocional ---
    private Sprite cachedResponsePortrait;   // Imatge estàndard del personatge parlant
    private Sprite cachedThinkingPortrait;   // Imatge del personatge en actitud reflexiva/processant

    // --- Configuració de les Animacions ---
    private float animDuration = 0.4f;       // Temps de la fosa i desplaçament vertical
    private Coroutine animRoutine;
    private Vector2 shownPos;                // Posició final desitjada en pantalla (calculada dinàmicament)

    // --- Font Cache ---
    // Desa la referència a la font tipogràfica per estalviar accessos a disc repetits
    private static TMP_FontAsset cachedFont;

    private void Awake()
    {
        // Control de duplicats en cas de persistència extrema
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (!IsOpen) return;
        if (PauseMenuUI.IsOpen) return; // Si està el joc pausat, no processem lògica del xat

        // Tancament ràpid prement la tecla d'escapament
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
            return;
        }

        // Nota didàctica: L'Enter per enviar el prompt es processa directament mitjançant
        // l'esdeveniment onSubmit de la classe TMP_InputField, evitant possibles problemes
        // de consum de tecles duplicades en el motor d'Update principal de Unity.
    }

    /// <summary>
    /// Obre la finestra de xat intel·ligent configurada per a un personatge en concret.
    /// </summary>
    /// <param name="npc">El component interactuable que conté els prompts del personatge.</param>
    public void Open(Interactable npc)
    {
        if (IsOpen) Close(); // Neteja de diàlegs previs no tancats correctament

        currentNPC = npc;
        IsOpen = true;
        isWaitingForResponse = false;

        // Recuperem els efectes sonors configurats de forma individualitzada per a aquest personatge
        currentPlayerTypingSound = npc.playerTypingSound;
        currentAITypingSound = npc.aiTypingSound;

        // Ens assegurem de tenir un component emissor de so en el mateix objecte
        if (audioSource == null)
        {
            audioSource = gameObject.GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        // Construïm la interfície si és la primera vegada que s'executa el sistema
        BuildUI();

        // Reiniciem i netegem els elements visuals de qualsevol diàleg anterior
        if (npcResponseText != null) 
        {
            npcResponseText.text = "";
            npcResponseText.maxVisibleCharacters = 0;
        }
        if (thinkingBubbleGO != null) thinkingBubbleGO.SetActive(false);

        // Emmagatzemem les expressions facials definides a la base de dades
        cachedResponsePortrait = npc.aiPortrait;
        cachedThinkingPortrait = npc.aiThinkingPortrait != null ? npc.aiThinkingPortrait : npc.aiPortrait;

        // Establim el retrat inicial (normal)
        if (aiPortraitImage != null)
        {
            if (cachedResponsePortrait != null)
            {
                aiPortraitImage.sprite = cachedResponsePortrait;
                aiPortraitImage.enabled = true;
            }
            else
            {
                aiPortraitImage.enabled = false; // Desactivar si el personatge no té imatge gràfica
            }
        }

        // Executem el missatge de benvinguda pre-configurat del NPC (si en té cap)
        string aiMsg = npc.GetAIMessage();
        if (!string.IsNullOrEmpty(aiMsg))
        {
            if (typingRoutine != null) StopCoroutine(typingRoutine);
            // Iniciem l'escriptura del text inicial amb un lleuger desfasament per fer-ho més orgànic
            typingRoutine = StartCoroutine(TypeInitialMessageDelayed(aiMsg));
        }
        else if (npcResponseText != null)
        {
            npcResponseText.text = "";
        }
        else
        {
            if (npcResponseText != null) npcResponseText.text = "";
        }

        // Mostrem i actualitzem el nom de l'enemic en la caixa dedicada
        if (npcNameBoxGO != null && npcNameText != null)
        {
            if (!string.IsNullOrEmpty(npc.aiCharacterName))
            {
                npcNameBoxGO.SetActive(true);
                npcNameText.text = npc.aiCharacterName;
            }
            else
            {
                npcNameBoxGO.SetActive(false); // Amagar si no hi ha nom especificat
            }
        }

        // Indicador provisional mentre s'engeguen les rutines
        if (npcResponseText != null && string.IsNullOrEmpty(aiMsg))
        {
            npcResponseText.text = "...";
        }

        // Activem el panell principal i executem l'animació d'entrada
        if (panelGO != null) panelGO.SetActive(true);
        PlayIn();

        // Donem focus de forma directa al camp d'escriptura perquè l'usuari pugui teclejar a l'acte
        if (playerInputField != null)
        {
            playerInputField.text = "";
            playerInputField.ActivateInputField();
            playerInputField.Select();
        }
    }

    /// <summary>
    /// Tanca de forma neta la interfície del diàleg intel·ligent.
    /// </summary>
    public void Close()
    {
        if (!IsOpen) return;

        IsOpen = false;
        isWaitingForResponse = false;

        // Parem qualsevol tasca d'escriptura de text pendent per evitar fuites de rendiment
        if (typingRoutine != null) { StopCoroutine(typingRoutine); typingRoutine = null; }

        // Iniciem l'animació de lliscament de sortida
        PlayOut();
    }

    /// <summary>
    /// Corrutina per retardar lleument la fosa del diàleg de benvinguda inicial per evitar pampallugues.
    /// </summary>
    private IEnumerator TypeInitialMessageDelayed(string message)
    {
        yield return new WaitForSecondsRealtime(0.1f);
        yield return TypeResponseRoutine(message);
    }

    /// <summary>
    /// Listener associat a l'esdeveniment onValueChanged de la caixa d'escriptura del jugador.
    /// Afegeix el feedback auditiu clàssic de teclat mecànic a mesura que es prem cada tecla.
    /// </summary>
    private void OnPlayerTyping(string newValue)
    {
        if (currentPlayerTypingSound != null && audioSource != null && !string.IsNullOrEmpty(newValue))
        {
            // Variem lleugerament el to de reproducció (Pitch) perquè no soni repetitiu i resulti més natural
            audioSource.pitch = Random.Range(0.9f, 1.1f);
            audioSource.PlayOneShot(currentPlayerTypingSound, 0.6f);
        }
    }

    /// <summary>
    /// Callback per a la detecció d'enviament de missatge mitjançant la tecla Enter.
    /// </summary>
    private void OnInputSubmit(string text)
    {
        if (!IsOpen || isWaitingForResponse) return;
        if (string.IsNullOrWhiteSpace(text)) return;
        SendPlayerMessage();
    }

    /// <summary>
    /// Prepara, valida i envia el text de l'usuari cap a la connexió del servei Ollama.
    /// </summary>
    private void SendPlayerMessage()
    {
        string message = playerInputField.text.Trim();
        if (string.IsNullOrEmpty(message)) return;

        Debug.Log($"[AIDialogueUI] Enviant missatge a Ollama: '{message}'");

        // Netegem el camp d'entrada perquè quedi llest per a la propera intervenció
        playerInputField.text = "";
        isWaitingForResponse = true;

        // Executem l'animació d'espera "Pensant..." per donar feedback de processament a l'usuari
        if (npcResponseText != null)
        {
            if (typingRoutine != null) { StopCoroutine(typingRoutine); typingRoutine = null; }
            typingRoutine = StartCoroutine(ThinkingAnimation());
        }

        // Desactivem temporalment la interacció de la caixa de text per evitar sobrecàrregues
        if (playerInputField != null)
        {
            playerInputField.interactable = false;
        }

        // Enviem el prompt mitjançant el client de xarxa asíncron
        OllamaDialogueClient.Instance.SendMessage(currentNPC, message, OnResponseReceived);
    }

    /// <summary>
    /// Callback disparat en el moment que rebem la cadena de text de resposta de l'API FastAPI/Ollama.
    /// </summary>
    /// <param name="response">El contingut de text enviat per la IA.</param>
    private void OnResponseReceived(string response)
    {
        try
        {
            Debug.Log($"[AIDialogueUI] Resposta rebuda: '{(response != null ? response.Substring(0, Mathf.Min(80, response.Length)) : "NULL")}'");

            isWaitingForResponse = false;

            // Aturem immediatament la corrutina de l'animació dels punts de pensament
            if (typingRoutine != null) { StopCoroutine(typingRoutine); typingRoutine = null; }

            // Procedim a escriure la resposta lletra per lletra
            if (npcResponseText != null)
            {
                npcResponseText.maxVisibleCharacters = int.MaxValue; // Reset de caràcters visibles actius
                typingRoutine = StartCoroutine(TypeResponseRoutine(response ?? OllamaDialogueClient.FallbackMessage));
            }

            // Reactivem el canal d'entrada per permetre la continuació del diàleg
            if (playerInputField != null && IsOpen)
            {
                playerInputField.interactable = true;
                // Forcem el focus de tornada a l'input en el frame següent per evitar problemes d'esdeveniments
                StartCoroutine(RefocusInputNextFrame());
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AIDialogueUI] ERROR a OnResponseReceived: {e.Message}\n{e.StackTrace}");
            isWaitingForResponse = false;
            
            // Si hi ha cap error inesperat de xarxa o deserialització, restaures el retrat normal
            if (aiPortraitImage != null && cachedResponsePortrait != null)
            {
                aiPortraitImage.sprite = cachedResponsePortrait;
            }
            
            // Bolquem el missatge d'error de rescat per no trencar l'experiència del jugador
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
        yield return null; // Un frame d'espera és crucial per assegurar que el mòdul d'Events de Unity ha refrescat
        if (playerInputField != null && IsOpen)
        {
            playerInputField.ActivateInputField();
            playerInputField.Select();
        }
    }

    /// <summary>
    /// Corrutina que gestiona el feedback gràfic de reflexió de la criatura quan processa el prompt.
    /// Canvia l'avatar a l'expressió reflexiva i anima els punts suspesos de forma sinusoidal.
    /// </summary>
    private IEnumerator ThinkingAnimation()
    {
        if (thinkingBubbleGO == null) yield break;
        
        // Canviem al retrat facial de reflexió (Thinking)
        if (aiPortraitImage != null && cachedThinkingPortrait != null)
        {
            aiPortraitImage.sprite = cachedThinkingPortrait;
        }

        thinkingBubbleGO.SetActive(true);
        npcResponseText.text = ""; // Netegem la resposta anterior durant el temps d'espera
        
        float timer = 0f;
        while (isWaitingForResponse)
        {
            timer += Time.unscaledDeltaTime; // Utilitzem unscaledDeltaTime per no dependre del TimeScale del joc
            for (int i = 0; i < bubbleRTs.Length; i++)
            {
                // Calculem un moviment sinusoidal desfasat per a cada punt, simulant un efecte d'ona
                float offset = Mathf.Sin(timer * 5f + (i * 0.8f)) * 10f;
                bubbleRTs[i].anchoredPosition = new Vector2(bubbleRTs[i].anchoredPosition.x, offset);
                
                // Variem l'escala dinàmicament per donar-li més volum i dinamisme tridimensional
                float scale = 1f + Mathf.Sin(timer * 5f + (i * 0.8f)) * 0.2f;
                bubbleRTs[i].localScale = new Vector3(scale, scale, 1f);
            }
            yield return null;
        }
        
        thinkingBubbleGO.SetActive(false);
    }

    /// <summary>
    /// Corrutina d'escriptura retro (Typewriter).
    /// Mostra la resposta de la IA de manera progressiva i sincronitzada amb sons de tecleig.
    /// </summary>
    private IEnumerator TypeResponseRoutine(string text)
    {
        if (npcResponseText == null) yield break;

        // Ens assegurem de tancar la visual de pensament si continuava activa
        if (thinkingBubbleGO != null) thinkingBubbleGO.SetActive(false);

        // Restablim el retrat normal de xat (Response)
        if (aiPortraitImage != null && cachedResponsePortrait != null)
        {
            aiPortraitImage.sprite = cachedResponsePortrait;
        }

        npcResponseText.text = text;
        npcResponseText.maxVisibleCharacters = 0;

        float charsPerSecond = 50f; // Velocitat base de tecleig
        float delay = 1f / charsPerSecond;

        for (int i = 0; i < text.Length; i++)
        {
            npcResponseText.maxVisibleCharacters = i + 1;

            char c = text[i];
            // Emetem el so de parla de la criatura evitant espais o salts de línia
            if (currentAITypingSound != null && c != ' ' && c != '\n')
            {
                audioSource.pitch = Random.Range(0.95f, 1.05f); // Micro-modulació de freqüència de veu
                audioSource.PlayOneShot(currentAITypingSound, 0.5f);
            }

            // Dinamisme de puntuació: fem pauses lleugerament més llargues en comes, punts o exclamacions
            float currentDelay = delay;
            if (c == '.' || c == '?' || c == '!') currentDelay = delay * 8f;
            else if (c == ',' || c == ';' || c == ':') currentDelay = delay * 4f;

            yield return new WaitForSecondsRealtime(currentDelay);
        }

        // Assegurem que s'activi la visualització del final exacte per evitar pèrdues de paraules
        npcResponseText.maxVisibleCharacters = text.Length;
        typingRoutine = null;
    }

    // =========================================================================
    // UI Builder Procedimental (Creació Dinàmica dels Elements de la UI)
    // =========================================================================
    /// <summary>
    /// Mètode encarregat d'instanciar i organitzar tota la jerarquia del Canvas per codi.
    /// Configura les caixes, marges, colors, components de xarxa i efectes de contorn de la interfície.
    /// </summary>
    private void BuildUI()
    {
        if (isBuilt && panelGO != null) return; // Si ja està construïda, no cal tornar-hi

        // Localitzem el Canvas principal de l'escena on incrustarem el nostre panell
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        Canvas targetCanvas = null;
        foreach (var c in canvases)
        {
            // Evitem muntar-lo a sobre dels Canvas d'escena final o d'alertes específiques
            if (c.name != "EndCanvas" && c.name != "AlertCanvas")
            {
                targetCanvas = c;
                break;
            }
        }
        if (targetCanvas == null && canvases.Length > 0) targetCanvas = canvases[0];
        if (targetCanvas == null) return;

        rootCanvas = targetCanvas;

        // ── Creació del Panell Contenidor Principal ──
        panelGO = new GameObject("AIDialoguePanel");
        panelRT = panelGO.AddComponent<RectTransform>();
        panelRT.SetParent(rootCanvas.transform, false);

        // Ens assegurem que el panell tingui el seu propi Canvas d'ordenament superior
        Canvas diagCanvas = panelGO.AddComponent<Canvas>();
        diagCanvas.overrideSorting = true;
        diagCanvas.sortingOrder = 10000; // Prioritat màxima sobre la UI estàndard
        panelGO.AddComponent<GraphicRaycaster>(); // Permet rebre clics de ratolí en l'input

        // Col·loquem el panell ocupant la part inferior de la pantalla (zona de diàlegs)
        panelRT.anchorMin = new Vector2(0.12f, 0.05f);
        panelRT.anchorMax = new Vector2(0.88f, 0.40f);
        panelRT.offsetMin = panelRT.offsetMax = Vector2.zero;
        shownPos = panelRT.anchoredPosition; // Guardem la posició per a les animacions de desplaçament

        panelGroup = panelGO.AddComponent<CanvasGroup>();
        panelGroup.alpha = 0f;

        // Estètica del fons: blau fosc molt profund, estil cyberpunk/místic
        panelBgImg = panelGO.AddComponent<Image>();
        panelBgImg.color = new Color(0.08f, 0.08f, 0.16f, 0.95f);
        
        // Afegim un contorn brillant d'estil "glow" celest
        var panelOl = panelGO.AddComponent<Outline>();
        panelOl.effectColor = new Color(0.4f, 0.8f, 1f, 0.8f);
        panelOl.effectDistance = new Vector2(6f, -6f);

        // ── Caixa del Nom del Personatge (Etiqueta superior esquerra) ──
        npcNameBoxGO = new GameObject("AINameBox");
        npcNameBoxGO.transform.SetParent(panelRT, false);
        var nbRT = npcNameBoxGO.AddComponent<RectTransform>();
        nbRT.anchorMin = new Vector2(0f, 1f);
        nbRT.anchorMax = new Vector2(0f, 1f);
        nbRT.sizeDelta = new Vector2(320f, 75f);
        nbRT.pivot = new Vector2(0f, 0f);
        nbRT.anchoredPosition = new Vector2(15f, 8f);

        var nbImg = npcNameBoxGO.AddComponent<Image>();
        nbImg.color = new Color(0.15f, 0.25f, 0.4f, 1f); // Blau acer per diferenciar el nom
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

        // ── Zona del Text de Diàleg de la IA (Part superior de la caixa) ──
        var responseGO = new GameObject("AIResponseBox");
        responseGO.transform.SetParent(panelRT, false);
        var rRT = responseGO.AddComponent<RectTransform>();
        rRT.anchorMin = new Vector2(0.30f, 0.28f); // Situada després del retrat per evitar solapaments
        rRT.anchorMax = new Vector2(0.98f, 0.95f);
        rRT.offsetMin = rRT.offsetMax = Vector2.zero;

        npcResponseText = responseGO.AddComponent<TextMeshProUGUI>();
        npcResponseText.margin = new Vector4(10f, 10f, 10f, 10f);
        npcResponseText.textWrappingMode = TextWrappingModes.Normal;
        npcResponseText.fontSizeMax = 70f;
        npcResponseText.fontSizeMin = 32f;
        npcResponseText.enableAutoSizing = true;
        SetFont(npcResponseText, 55f, Color.white, FontStyles.Normal, TextAlignmentOptions.TopLeft);

        // ── Retrat de la Criatura ──
        var portraitBoxGO = new GameObject("AIPortraitBox");
        portraitBoxGO.transform.SetParent(panelRT, false);
        var pbRT = portraitBoxGO.AddComponent<RectTransform>();
        pbRT.anchorMin = new Vector2(0.02f, 0.05f); 
        pbRT.anchorMax = new Vector2(0.28f, 0.95f); // Ocupa el primer terç esquerre del panell
        pbRT.offsetMin = pbRT.offsetMax = Vector2.zero;

        var portraitGO = new GameObject("AIPortraitImage");
        portraitGO.transform.SetParent(pbRT, false);
        var pRT = portraitGO.AddComponent<RectTransform>();
        pRT.anchorMin = Vector2.zero; pRT.anchorMax = Vector2.one;
        pRT.offsetMin = Vector2.zero; pRT.offsetMax = Vector2.zero;
        
        aiPortraitImage = portraitGO.AddComponent<Image>();
        aiPortraitImage.preserveAspect = true; // IMPORTANT: Manté la proporció de píxels dels sprites retro
        aiPortraitImage.enabled = false;

        // ── Bombolles Animades d'Esperà (Thinking Dots) ──
        thinkingBubbleGO = new GameObject("AIThinkingBubbles");
        thinkingBubbleGO.transform.SetParent(panelRT, false);
        var tbRT = thinkingBubbleGO.AddComponent<RectTransform>();
        tbRT.anchorMin = new Vector2(0.5f, 0.65f);
        tbRT.anchorMax = new Vector2(0.5f, 0.65f);
        tbRT.sizeDelta = new Vector2(200f, 100f);
        tbRT.anchoredPosition = Vector2.zero;
        
        if (circleSprite == null) circleSprite = CreateCircleSprite(); // Generem la textura de punt procedural
        
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

        // ── Línia Separadora Estètica ──
        var separatorGO = new GameObject("AISeparator");
        separatorGO.transform.SetParent(panelRT, false);
        var sepRT = separatorGO.AddComponent<RectTransform>();
        sepRT.anchorMin = new Vector2(0.30f, 0.27f);
        sepRT.anchorMax = new Vector2(0.95f, 0.27f);
        sepRT.sizeDelta = new Vector2(0f, 3f);
        sepRT.anchoredPosition = Vector2.zero;
        var sepImg = separatorGO.AddComponent<Image>();
        sepImg.color = new Color(0.4f, 0.8f, 1f, 0.5f);

        // ── Contenidor i Camp de Text del Jugador (Part Inferior) ──
        var inputContainerGO = new GameObject("AIInputContainer");
        inputContainerGO.transform.SetParent(panelRT, false);
        inputContainerRT = inputContainerGO.AddComponent<RectTransform>();
        inputContainerRT.anchorMin = new Vector2(0.30f, 0.04f);
        inputContainerRT.anchorMax = new Vector2(0.98f, 0.25f);
        inputContainerRT.offsetMin = inputContainerRT.offsetMax = Vector2.zero;

        var icImg = inputContainerGO.AddComponent<Image>();
        icImg.color = new Color(0.05f, 0.05f, 0.12f, 0.9f); // Més fosc per contrastar el text que entra
        var icOl = inputContainerGO.AddComponent<Outline>();
        icOl.effectColor = new Color(0.3f, 0.6f, 0.8f, 0.6f);
        icOl.effectDistance = new Vector2(3f, -3f);

        // Input Field principal de TextMeshPro
        var inputGO = new GameObject("AIInputField");
        inputGO.transform.SetParent(inputContainerRT, false);
        var inputRT = inputGO.AddComponent<RectTransform>();
        inputRT.anchorMin = Vector2.zero; inputRT.anchorMax = Vector2.one;
        inputRT.offsetMin = new Vector2(15f, 5f); inputRT.offsetMax = new Vector2(-15f, -5f);

        // Zona de tall (Viewport)
        var textAreaGO = new GameObject("Text Area");
        textAreaGO.transform.SetParent(inputRT, false);
        var taRT = textAreaGO.AddComponent<RectTransform>();
        taRT.anchorMin = Vector2.zero; taRT.anchorMax = Vector2.one;
        taRT.offsetMin = Vector2.zero; taRT.offsetMax = Vector2.zero;
        var taMask = textAreaGO.AddComponent<RectMask2D>();

        // Text editable visible
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

        // Text de suggeriment (Placeholder)
        var placeholderGO = new GameObject("Placeholder");
        placeholderGO.transform.SetParent(taRT, false);
        var phRT = placeholderGO.AddComponent<RectTransform>();
        phRT.anchorMin = Vector2.zero; phRT.anchorMax = Vector2.one;
        phRT.offsetMin = Vector2.zero; phRT.offsetMax = Vector2.zero;
        var placeholderText = placeholderGO.AddComponent<TextMeshProUGUI>();
        SetFont(placeholderText, 44f, new Color(0.5f, 0.55f, 0.65f, 0.7f), FontStyles.Italic, TextAlignmentOptions.Left);
        placeholderText.text = "Escriu aquí... (Enter per enviar, Esc per sortir)";
        placeholderText.enableAutoSizing = true;
        placeholderText.fontSizeMin = 22f;
        placeholderText.fontSizeMax = 44f;

        // Muntem i connectem el component TMP_InputField
        playerInputField = inputGO.AddComponent<TMP_InputField>();
        playerInputField.textViewport = taRT;
        playerInputField.textComponent = playerText;
        playerInputField.placeholder = placeholderText;
        playerInputField.fontAsset = playerText.font;
        playerInputField.pointSize = 44f;
        playerInputField.characterLimit = 300; // Limitem el text per evitar abusos o desbordaments de memòria de la IA
        playerInputField.lineType = TMP_InputField.LineType.SingleLine;

        // Enllacem els nostres mètodes d'esdeveniments
        playerInputField.onSubmit.AddListener(OnInputSubmit);
        playerInputField.onValueChanged.AddListener(OnPlayerTyping);

        // Estètica de colors al seleccionar
        var inputColors = playerInputField.colors;
        inputColors.normalColor = Color.white;
        inputColors.highlightedColor = new Color(0.4f, 0.8f, 1f, 1f);
        inputColors.selectedColor = Color.white;
        playerInputField.colors = inputColors;

        playerInputField.caretColor = new Color(0.4f, 0.8f, 1f, 1f);
        playerInputField.caretWidth = 3;
        playerInputField.customCaretColor = true;
        playerInputField.selectionColor = new Color(0.2f, 0.5f, 0.8f, 0.4f);

        // ── Indicador de tecla d'escapament per a sortida ràpida (Hint) ──
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
        hintText.text = "[Esc] Sortir";

        panelGO.SetActive(false);
        isBuilt = true; // Protecció per evitar futures reconstruccions d'elements redundants
    }

    // =========================================================================
    // Font Helper - Localització Dinàmica i Mètodes de Càrrega de Recursos
    // =========================================================================
    /// <summary>
    /// Configura les propietats tipogràfiques d'un element de text.
    /// Realitza una cerca recursiva de la font retro als directoris del projecte, funcionant tant
    /// a l'editor com en la versió compilada (Build).
    /// </summary>
    private void SetFont(TextMeshProUGUI t, float size, Color col, FontStyles style, TextAlignmentOptions align)
    {
        t.fontSize = size; t.color = col; t.fontStyle = style; t.alignment = align;

        if (cachedFont == null)
        {
            // Cerca prioritària mitjançant AssetDatabase en cas d'estar executant-se a l'Editor de Unity
#if UNITY_EDITOR
            cachedFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/determination SDF.asset")
                 ?? UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/PixelOperator SDF.asset")
                 ?? UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/PixelOperator SDF.asset");
#endif
            // Si estem en una Build compilada o la cerca anterior ha fallat, utilitzem Resources.Load estàndard
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
    // Animacions de Entrada i Sortida (Efecte Lliscament Vertical RPG)
    // =========================================================================
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

    /// <summary>
    /// Corrutina d'entrada: desplaça el panell de diàleg de forma vertical (d'avall cap a dalt)
    /// utilitzant una suavització cúbica per donar-li un aspecte visual fluid i molt professional.
    /// </summary>
    private IEnumerator AnimateIn()
    {
        panelGroup.alpha = 1f;
        float slideDist = 800f; // Distància de desplaçament d'inici (fora de la pantalla)
        Vector2 offset = new Vector2(0f, -slideDist);
        panelRT.anchoredPosition = shownPos + offset;
        panelRT.localScale = Vector3.one;

        float t = 0f;
        while (t < animDuration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / Mathf.Max(0.0001f, animDuration));
            // Aplicació d'una corba de suavització de tipus "OutCubic" per frenar al final de la transició
            float eased = 1f - Mathf.Pow(1f - u, 3f);
            panelRT.anchoredPosition = Vector2.Lerp(shownPos + offset, shownPos, eased);
            yield return null;
        }

        panelGroup.alpha = 1f;
        panelRT.anchoredPosition = shownPos;
        animRoutine = null;
    }

    /// <summary>
    /// Corrutina de sortida: llisca el panell cap avall fins a ocultar-lo completament per sota de la vista.
    /// </summary>
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
            // Aplicació d'una corba de tipus "InQuad" (acceleració gradual cap avall)
            float eased = u * u;
            panelRT.anchoredPosition = Vector2.Lerp(startPos, endPos, eased);
            yield return null;
        }

        panelGroup.alpha = 0f;
        panelRT.anchoredPosition = endPos;
        if (panelGO) panelGO.SetActive(false); // Apaguem el panell per estalviar recursos de dibuix del motor

        animRoutine = null;
        OnAIDialogueClosed?.Invoke(); // Notifiquem al jugador per alliberar la seva interacció amb el món
    }

    /// <summary>
    /// Generador gràfic procedural d'un sprite circular amb suavitzat de vores (Anti-aliasing bàsic).
    /// Ens permet tenir imatges circulars perfectes sense necessitat d'afegir assets en el disc.
    /// </summary>
    private Sprite CreateCircleSprite()
    {
        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Calculem la distància de cada píxel respecte al centre de la textura
                float dx = (x - size / 2f) / (size / 2f);
                float dy = (y - size / 2f) / (size / 2f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = d <= 1f ? 1f : 0f;
                
                // Apliquem una petita fosa gradual en el límit exterior per crear un suavitzat natural
                if (d > 0.8f && d <= 1f) alpha = 1f - (d - 0.8f) / 0.2f;
                
                tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
        }
        tex.Apply(); // Envia els píxels a la GPU de manera eficient
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
