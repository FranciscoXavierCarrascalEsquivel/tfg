using System;
using UnityEngine;
using UnityEngine.Events;
using System.Collections;

/// <summary>
/// Component base de dades i interacció de l'Overworld (Interactable).
/// Defineix el comportament de qualsevol entitat amb la qual el jugador pot parlar o actuar
/// (NPCs, cartells informatius, cofres, enemics, etc.).
/// Suporta arbres de diàlegs lineals, diàlegs ramificats (amb camins i decisions),
/// requeriments temporals d'inventari, versions progressives d'interacció,
/// i la totalitat de paràmetres de prompts asíncrons per a la Intel·ligència Artificial.
/// </summary>
public class Interactable : MonoBehaviour
{
    /// <summary>
    /// Representació estructurada d'un únic enunciat o línia de diàleg (DialogueLine).
    /// </summary>
    [Serializable]
    public class DialogueLine
    {
        [TextArea(2, 6)]
        [Tooltip("El contingut de text literal que es renderitzarà en el panell.")]
        public string text = "…";

        [Header("Retrat (Opcional)")]
        [Tooltip("L'avatar retro bidimensional d'expressió facial associat al parlant.")]
        public Sprite portrait;

        [Tooltip("El nom textual del personatge que s'imprimirà a sobre de la caixa de text.")]
        public string speakerName = "";

        [Tooltip("Si és cert, l'avatar es dibuixarà a la banda dreta de la caixa en comptes de l'esquerra.")]
        public bool isRightSide = false;

        [Tooltip("Si vols que el panell de diàleg surti al capdamunt de la pantalla per no tapar visualment l'acció inferior.")]
        public bool showOnTop = false;

        [Tooltip("Si és cert, la caixa canviarà a tipus núvol (Pensament) i s'aplicarà una font i comportament especials.")]
        public bool isThought = false;

        [Tooltip("Si és cert, el Canvas es tancarà i es tornarà a obrir per donar un feedback visual de tall dramàtic.")]
        public bool forceReopen = false;

        [Tooltip("So exclusiu de murmuri o so de veu per a aquesta frase en concret.")]
        public AudioClip customVoiceSound;

        [Tooltip("Controlador d'animació opcional en cas de voler que el retrat sigui dinàmic/animat.")]
        public RuntimeAnimatorController portraitAnimator;

        [Tooltip("Permet canviar dinàmicament l'aspecte visual d'un objecte del món (ex. activar un interruptor) en aquesta línia.")]
        public Sprite interactableSpriteChange;

        [Tooltip("El Renderer a sobre del qual s'aplicarà la transformació de l'sprite visual anterior. Si es deixa buit, s'aplicarà a aquest mateix element.")]
        public SpriteRenderer targetSpriteRenderer;

        [Tooltip("Feedback sonor que s'emetrà en el moment de realitzar la transformació gràfica de l'objecte.")]
        public AudioClip interactableSpriteChangeSound;

        [HideInInspector]
        public Interactable owner; // Vinculació d'instància amb el script contenidor original

        [Header("Behavior Tree & Branching")]
        [Tooltip("Si és cert, el diàleg finalitza immediatament un cop completada aquesta frase, sense continuar llegint la llista.")]
        public bool isEndNode = false;

        [Header("Ritme i Temporitzadors")]
        [Tooltip("Espera un temps en segons (amb pantalla buida) abans de començar a teclejar aquesta línia.")]
        public float delayBeforeLine = 0f;

        [Tooltip("Si és major a 0, la línia passarà a la següent automàticament sense esperar que el jugador premi el botó d'interacció (E).")]
        public float autoAdvanceTime = 0f;

        [Tooltip("Si és cert, el jugador tindrà prohibit saltar-se l'animació de tecleig prement el botó d'avanç.")]
        public bool cannotSkip = false;

        [Header("Xarxa de Nodes")]
        [Tooltip("Força que la propera vegada que el jugador interaccioni amb aquest objecte, s'activi una versió de diàleg concreta. (-1 ignora).")]
        public int setNextInteractionVersion = -1;

        [Tooltip("Índex de salt directe dins de la llista de frases per a fer bucles o salts condicionals. (-1 per a comportament lineal continu).")]
        public int jumpToLineIndex = -1;

        [Tooltip("Llista d'opcions de resposta interactives que s'oferiran al jugador un cop acabada de llegir la línia activa.")]
        public DialogueChoice[] choices;

        [Header("Esdeveniments de Unity")]
        [Tooltip("Accions o mètodes que s'executaran de forma dinàmica en el frame precís que es comença a llegir aquesta línia (ex: iniciar cinemàtiques, tremolar càmera, spawn de partícules).")]
        public UnityEvent onLineReached;
    }

    /// <summary>
    /// Representa un botó de resposta elegible pel jugador que ramifica el diàleg.
    /// </summary>
    [Serializable]
    public class DialogueChoice
    {
        [Tooltip("El text que es mostrarà en el botó d'opció.")]
        public string text;

        [Tooltip("Índex de línia al qual saltem (0-based) si triem aquesta opció. Posa -1 per tancar l'arbre completament.")]
        public int jumpToLineIndex = -1;

        [Tooltip("Efecte de so personalitzat que s'emetrà en el moment de confirmar la selecció.")]
        public AudioClip customSelectSound;

        [Tooltip("Si és cert, aquesta opció seguirà estant disponible encara que el jugador ja l'hagi seleccionat en interaccions anteriors.")]
        public bool repeatable = false;

        [Tooltip("Esdeveniment que s'executa immediatament al seleccionar aquesta branca (ex: donar inici a una baralla, restar diners, donar recompensa).")]
        public UnityEvent onChoiceSelected;
    }
    
    /// <summary>
    /// Grup de línies de diàleg lligades a una única instància d'interacció.
    /// </summary>
    [Serializable]
    public class DialogueVersion
    {
        [Tooltip("Seqüència consecutiva de línies que componen el diàleg.")]
        public DialogueLine[] lines;
        
        [Tooltip("Override d'efecte sonor per a les veus del personatge per a aquesta versió.")]
        public AudioClip voiceOverride;

        [Tooltip("Esdeveniment general que es dispara just a l'inici d'aquesta versió de diàleg.")]
        public UnityEvent onLineReached;
    }

    // =========================================================================
    // IA Generativa (Ollama) — Paràmetres del Prompt i Configuració del Personatge
    // =========================================================================
    [Header("IA Generativa (Ollama)")]

    [Tooltip("Activa la integració de diàlegs lliures mitjançant connexions asíncrones a Ollama.")]
    public bool useGenerativeAI = false;

    [Tooltip("El nom oficial del NPC que es bolcarà a l'etiqueta de text i s'enviarà com a clau al model.")]
    public string aiCharacterName = "";

    [TextArea(3, 8)]
    [Tooltip("Prompt del sistema de personalitat. Defineix el temperament, la manera d'expressar-se i el vocabulari característic del personatge.")]
    public string aiCharacterBehavior = "";

    [TextArea(3, 8)]
    [Tooltip("Límits cognitius i lore del NPC. Essencial per evitar al·lucinacions i donar respostes absurdes de coses que el personatge desconeix.")]
    public string aiKnowledgeLimit = "";

    [TextArea(3, 8)]
    [Tooltip("Context situacional. Explica on es troba el personatge en aquest moment i quin és el seu estat d'ànim.")]
    public string aiInitialContext = "";

    [Tooltip("La direcció URL de l'API web dinàmica de connexió FastAPI (aixecada mitjançant un túnel Cloudflare).")]
    public string aiApiUrl = "https://door-matched-cruises-agrees.trycloudflare.com/chat";

    [Tooltip("La clau de pas secreta requerida per autenticar-se amb la nostra API local.")]
    public string aiApiToken = "el-teu-token-secret-del-tfg";

    [Tooltip("L'idioma vehicular en el qual s'obliga a respondre al model de llenguatge (ex: Català, English...).")]
    public string aiResponseLanguage = "English";

    [Tooltip("Si és cert, el personatge parlarà primer utilitzant els seus diàlegs tradicionals lineals i posteriorment s'obrirà el mode IA lliure.")]
    public bool activateAIAfterNormalDialogue = false;

    [Tooltip("Obre directament la caixa de diàleg de text IA sense mostrar cap línia de diàleg estàndard prèvia.")]
    public bool startAIDirectly = false;

    [Tooltip("Índex de versió (0-based) en el qual aquest interactuable farà la transició a IA. Ex: si és 2, les interaccions 0 i 1 seran diàlegs fixos, i a la 2 s'engegarà el xat dinàmic.")]
    public int activateAIAtVersion = -1;

    [Tooltip("L'avatar estàndard que es mostrarà en xerrar amb el personatge.")]
    public Sprite aiPortrait;

    [Tooltip("L'avatar d'espera que es carregarà a pantalla mentre esperem la resposta asíncrona d'Ollama.")]
    public Sprite aiThinkingPortrait;

    [TextArea(2, 4)]
    [Tooltip("La primera frase amb la qual el personatge obre el xat dinàmic quan és la primera vegada que hi parlem.")]
    public string aiFirstMessage = "Hello! How can I help you today?";

    [TextArea(2, 4)]
    [Tooltip("La frase de benvinguda a partir de la segona conversa en endavant. Si es buida, es mantindrà el missatge inicial.")]
    public string aiRepeatMessage = "";

    [Header("Efectes Sonors de Xat")]
    [Tooltip("So mecànic o auditiu al prémer el jugador cada lletra a l'input de xat.")]
    public AudioClip playerTypingSound;

    [Tooltip("So dinàmic emès per a cada lletra escrita en pantalla quan parla la IA.")]
    public AudioClip aiTypingSound;

    // =========================================================================
    // Requisits d'Inventari (Lògica de Desbloqueig Emergent)
    // =========================================================================
    [Header("Configuració de Diàleg General")]
    [Tooltip("Si és cert, les opcions (choices) que ja hagin estat seleccionades no es tornaran a mostrar al menú (evita redundàncies).")]
    [SerializeField] private bool hideSeenChoices = false;
    public bool HideSeenChoices => hideSeenChoices;

    [Tooltip("Si és cert, s'evitarà el salt total de converses mitjançant la drecera 'F'.")]
    [SerializeField] private bool cannotSkipDialogue = false;
    public bool CannotSkipDialogue => cannotSkipDialogue;

    [Header("Requisit d'Inventari (Opcional)")]
    [Tooltip("El nom d'ID exacte de l'objecte de l'inventari requerit per a activar la interacció especial.")]
    public string requiredItemName = "";

    [Tooltip("Grup de diàleg alternatiu que es reproduirà exclusivament si el jugador porta l'objecte necessari a la motxilla.")]
    public DialogueVersion requirementMetVersion;

    [Tooltip("Accions addicionals a disparar en el frame precís que interaccionem amb l'objecte correcte a la motxilla.")]
    public UnityEvent onRequirementMet;

    [Tooltip("Si és cert, el diàleg de requisit complert només es mostrarà un cop. En futures interaccions es tornarà al flux estàndard.")]
    [SerializeField] private bool requirementOnlyOnce = true;
    private bool requirementAlreadyMet = false;

    [Header("Versions del Diàleg (Seqüència d'Interaccions consecutiva)")]
    [SerializeField] private DialogueVersion[] versions;

    // ── FALLBACK LEGACY (Per retrocompatibilitat de scripts anteriors) ──
    [Header("Legacy (si no s'utilitza versions)")]
    [TextArea(2, 6)]
    [SerializeField] private string description = "…";
    [SerializeField] private Sprite portrait;
    [SerializeField] private RuntimeAnimatorController portraitAnimator;
    
    [Header("Línies Legacy (si no s'utilitza versions)")]
    [SerializeField] private DialogueLine[] lines;

    private int interactionCount = 0; // Guarda quantes vegades s'ha parlat amb aquest personatge
    public int InteractionCount => interactionCount;
    private bool everInteracted = false; // Registre permanent de memòria
    private int aiInteractionCount = 0;  // Registre de converses d'IA per als canvis de benvinguda

    public bool HasBeenInteracted => everInteracted;

    /// <summary>
    /// Força el salt de diàleg a una versió concreta des de disparadors de l'editor.
    /// </summary>
    public void SetNextInteractionVersion(int versionIndex)
    {
        if (versionIndex >= 0)
        {
            interactionCount = versionIndex;
        }
    }

    /// <summary>
    /// Registra de forma manual una interacció. Útil per a fluxos externs de missions.
    /// </summary>
    public void RegisterInteraction()
    {
        everInteracted = true;
    }

    /// <summary>
    /// Recupera la cadena de xat IA de benvinguda i actualitza els registres d'ús.
    /// </summary>
    public string GetAIMessage()
    {
        string msg;
        if (aiInteractionCount == 0 || string.IsNullOrEmpty(aiRepeatMessage))
        {
            msg = aiFirstMessage;
        }
        else
        {
            msg = aiRepeatMessage;
        }
        aiInteractionCount++;
        return msg;
    }

    /// <summary>
    /// Determina si, segons la configuració i les converses mantingudes, el personatge
    /// hauria de derivar al jugador directament al xat d'IA en comptes dels diàlegs lineals.
    /// </summary>
    public bool ShouldUseAIForCurrentInteraction()
    {
        if (!useGenerativeAI) return false;
        if (startAIDirectly) return true;
        if (activateAIAtVersion >= 0 && interactionCount >= activateAIAtVersion) return true;
        return false;
    }

    /// <summary>
    /// El cervell lògic de selecció de text. Retorna la taula exacta de frases (DialogueLine)
    /// resolent la prioritat de requisits, les seqüències de versions, o el fallback legacy.
    /// </summary>
    public DialogueLine[] GetCurrentLines()
    {
        DialogueLine[] linesToReturn = null;
        
        // ── 1. COMPROVACIÓ DEL COMPLIMENT D'OBJECTES REQUERITS ──
        bool hasRequiredItem = false;
        if (!string.IsNullOrEmpty(requiredItemName) && PlayerInventory.Instance != null)
        {
            foreach(var item in PlayerInventory.Instance.Items)
            {
                if (item == requiredItemName)
                {
                    hasRequiredItem = true;
                    break;
                }
            }
        }

        if (hasRequiredItem && (!requirementOnlyOnce || !requirementAlreadyMet))
        {
            requirementAlreadyMet = true;
            onRequirementMet?.Invoke(); // Disparem els esdeveniments de recompensa/compliment
            linesToReturn = requirementMetVersion?.lines ?? new DialogueLine[0];
            
            // Disparem l'esdeveniment global d'inici de la versió de requisit
            requirementMetVersion?.onLineReached?.Invoke();
        }
        // ── 2. CICLE DE VERSIONS DE DIÀLEG SEQÜENCIALS ──
        else if (versions != null && versions.Length > 0)
        {
            // Triem la versió que toca. Si superem l'última, la repetim indefinidament en bucle
            int idx = Mathf.Min(interactionCount, versions.Length - 1);
            linesToReturn = versions[idx]?.lines ?? new DialogueLine[0];
        }
        // ── 3. FALLBACK DE COMPATIBILITAT 1 (Taula simple de línies) ──
        else if (lines != null && lines.Length > 0)
        {
            linesToReturn = lines;
        }
        // ── 4. FALLBACK DE COMPATIBILITAT 2 (Text descriptiu simple) ──
        else
        {
            linesToReturn = new DialogueLine[]
            {
                new DialogueLine
                {
                    text = description,
                    portrait = portrait,
                    portraitAnimator = portraitAnimator
                }
            };
        }

        // Incrementem permanentment el comptador d'interaccions del món
        interactionCount++;
        everInteracted = true;

        // Auto-assignem la referència del propietari per a resoldre ramificacions de decisions
        if (linesToReturn != null)
        {
            foreach (var l in linesToReturn)
            {
                if (l != null) l.owner = this;
            }
        }
        return linesToReturn;
    }

    /// <summary>
    /// Propietat d'enllaç dinàmic per a sistemes anteriors que feien referència directa a .Lines
    /// </summary>
    public DialogueLine[] Lines => GetCurrentLines();
}
