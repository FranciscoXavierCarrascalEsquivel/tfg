using System;
using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class Interactable : MonoBehaviour
{
    [Serializable]
    public class DialogueLine
    {
        [TextArea(2, 6)]
        public string text = "…";

        [Header("Portrait (optional)")]
        public Sprite portrait;

        [Tooltip("Nom del personatge (opcional)")]
        public string speakerName = "";

        [Tooltip("Mostrar retrat a la dreta?")]
        public bool isRightSide = false;

        [Tooltip("Si vols que el quadre surti a la part superior de la pantalla")]
        public bool showOnTop = false;

        [Tooltip("Si és cert, fons forma núvol i mode pensament.")]
        public bool isThought = false;

        [Tooltip("Tancar i obrir finestra en aquesta línia?")]
        public bool forceReopen = false;

        [Tooltip("Opcional: so exclusiu de veu per aquesta línia/personatge.")]
        public AudioClip customVoiceSound;

        [Tooltip("Opcional: si vols retrat animat (Animator Controller)")]
        public RuntimeAnimatorController portraitAnimator;

        [Tooltip("Si vols canviar l'sprite d'un objecte en aquesta línia de diàleg (opcional)")]
        public Sprite interactableSpriteChange;

        [Tooltip("Opcional: A quin SpriteRenderer aplicar el canvi d'sprite? (Si està buit, s'aplicarà al mateix objecte Interactable)")]
        public SpriteRenderer targetSpriteRenderer;

        [Tooltip("Opcional: So a reproduir quan l'Interactable canvia d'sprite")]
        public AudioClip interactableSpriteChangeSound;

        [HideInInspector]
        public Interactable owner;

        [Header("Behaviour Tree & Branching")]
        [Tooltip("Si és cert, el diàleg es tancarà un cop finalitzi aquesta línia, ignorant les següents.")]
        public bool isEndNode = false;

        [Header("Ritme (Puses & Auto-Avanç)")]
        [Tooltip("Opcional: Espera X segons (pantalla amagada/buida) ABANS de començar a mostrar aquesta línia.")]
        public float delayBeforeLine = 0f;

        [Tooltip("Opcional: Si és &gt; 0, el diàleg avançarà a la següent línia automàticament al cap d'aquests segons un cop hagi acabat de teclejar (sense esperar a premer la E).")]
        public float autoAdvanceTime = 0f;

        [Header("Xarxa de Nodes")]

        [Tooltip("Sobreescriu quina Versió de diàleg (índex) es reproduirà la propera vegada que interaccionis amb l'objecte. (-1 descarta)")]
        public int setNextInteractionVersion = -1;

        [Tooltip("A quin índex saltem automàticament des d'aquí? (-1 vol dir llegir la posició següent de forma lineal)")]
        public int jumpToLineIndex = -1;

        [Tooltip("Opcional: Afegeix respostes interactives si vols que el jugador esculli camins diferents.")]
        public DialogueChoice[] choices;

        [Header("Esdeveniments")]
        [Tooltip("Accions extres a executar quan el jugador arriba a aquesta línia de diàleg (ex: Donar un objecte, Iniciar una animació).")]
        public UnityEvent onLineReached;
    }

    [Serializable]
    public class DialogueChoice
    {
        [Tooltip("Text de la resposta que veurà el jugador")]
        public string text;

        [Tooltip("A quina línia (índex de la matriu 'lines' començant per 0) saltem? Fica -1 per finalitzar i tancar l'arbre.")]
        public int jumpToLineIndex = -1;

        [Tooltip("Opcional: So exclusiu al seleccionar aquesta resposta específica.")]
        public AudioClip customSelectSound;

        [Tooltip("Si és cert, aquesta opció no desapareixerà mai encara que ja l'haguem escollit abans.")]
        public bool repeatable = false;

        [Tooltip("Accions extres a executar quan s'escull aquesta resposta (ex: Iniciar combat, Restar or).")]
        public UnityEvent onChoiceSelected;
    }
    
    [Serializable]
    public class DialogueVersion
    {
        [Tooltip("Sequència de línies que es mostraran en aquesta interacció.")]
        public DialogueLine[] lines;
        public AudioClip voiceOverride;
        public UnityEvent onLineReached;
    }

    [Header("Configuració de Diàleg")]
    [Tooltip("Si és cert, les opcions de diàleg (choices) que ja hem escollit s'amagaran la propera vegada, excepte si estan marcades com a repetibles.")]
    [SerializeField] private bool hideSeenChoices = false;
    public bool HideSeenChoices => hideSeenChoices;

    [Header("Requisit d'Inventari (Opcional)")]
    [Tooltip("Nom de l'objecte necessari a l'inventari per executar l'acció especial en comptes del diàleg normal.")]
    public string requiredItemName = "";

    [Tooltip("Si tenim l'objecte, mostrem aquest diàleg en comptes de les Versions normals.")]
    public DialogueVersion requirementMetVersion;

    [Tooltip("S'executarà automàticament al interactuar si tenim l'objecte a l'inventari.")]
    public UnityEvent onRequirementMet;

    [Header("Versions de Diàleg (cada interacció usa la següent; l'ultima es repeteix en bucle)")]
    [SerializeField] private DialogueVersion[] versions;

    // Compatibilitat amb el que ja tens (si no omples 'versions')
    [Header("Legacy (si no omples 'versions')")]
    [TextArea(2, 6)]
    [SerializeField] private string description = "…";
    [SerializeField] private Sprite portrait;
    [SerializeField] private RuntimeAnimatorController portraitAnimator;
    
    [Header("Legacy lines (si no omples 'versions')")]
    [SerializeField] private DialogueLine[] lines;

    private int interactionCount = 0;
    private bool everInteracted = false;

    /// <summary>Cert si el jugador ha interactuat almenys una vegada amb aquest objecte (no es reseteja mai).</summary>
    public bool HasBeenInteracted => everInteracted;

    public void SetNextInteractionVersion(int versionIndex)
    {
        if (versionIndex >= 0)
        {
            interactionCount = versionIndex;
        }
    }

    /// <summary>
    /// Retorna les línies de diàleg corresponents a la interacció actual i incrementa el comptador.
    /// La darrera versió es repeteix indefinidament.
    /// </summary>
    public DialogueLine[] GetCurrentLines()
    {
        DialogueLine[] linesToReturn = null;
        
        // Comprovar si hi ha un requisit d'objecte i si el tenim
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

        if (hasRequiredItem)
        {
            onRequirementMet?.Invoke();
            linesToReturn = requirementMetVersion?.lines ?? new DialogueLine[0];
            
            // Si estem forçant la versió amb l'objecte, disparem l'event propi si el té
            requirementMetVersion?.onLineReached?.Invoke();
        }
        // Si hi ha versions configurades, les fem servir
        else if (versions != null && versions.Length > 0)
        {
            int idx = Mathf.Min(interactionCount, versions.Length - 1);
            linesToReturn = versions[idx]?.lines ?? new DialogueLine[0];
        }
        // Fallback: l'antic camp lines
        else if (lines != null && lines.Length > 0)
        {
            linesToReturn = lines;
        }
        // Fallback legacy
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

        // Incrementem sempre, independentment del camí usat
        interactionCount++;
        everInteracted = true;

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
    /// Propietat de compatibilitat per codi que feia servir .Lines
    /// </summary>
    public DialogueLine[] Lines => GetCurrentLines();
}
