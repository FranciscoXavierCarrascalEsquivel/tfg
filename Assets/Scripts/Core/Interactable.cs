using System;
using UnityEngine;
using UnityEngine.Events;

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

        [Header("Behaviour Tree & Branching")]
        [Tooltip("Si és cert, el diàleg es tancarà un cop finalitzi aquesta línia, ignorant les següents.")]
        public bool isEndNode = false;

        [Tooltip("A quin índex saltem automàticament des d'aquí? (-1 vol dir llegir la posició següent de forma lineal)")]
        public int jumpToLineIndex = -1;

        [Tooltip("Opcional: Afegeix respostes interactives si vols que el jugador esculli camins diferents.")]
        public DialogueChoice[] choices;
    }

    [Serializable]
    public class DialogueChoice
    {
        [Tooltip("Text de la resposta que veurà el jugador")]
        public string text;

        [Tooltip("A quina línia (índex de la matriu 'lines' començant per 0) saltem? Fica -1 per finalitzar i tancar l'arbre.")]
        public int jumpToLineIndex = -1;

        [Tooltip("Accions extres a executar quan s'escull aquesta resposta (ex: Iniciar combat, Restar or).")]
        public UnityEvent onChoiceSelected;
    }
    
    [Serializable]
    public class DialogueVersion
    {
        [Tooltip("Sequència de línies que es mostraran en aquesta interacció.")]
        public DialogueLine[] lines;
    }

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

    /// <summary>
    /// Retorna les línies de diàleg corresponents a la interacció actual i incrementa el comptador.
    /// La darrera versió es repeteix indefinidament.
    /// </summary>
    public DialogueLine[] GetCurrentLines()
    {
        // Si hi ha versions configurades, les fem servir
        if (versions != null && versions.Length > 0)
        {
            int idx = Mathf.Min(interactionCount, versions.Length - 1);
            interactionCount++;
            return versions[idx]?.lines ?? new DialogueLine[0];
        }

        // Fallback: l'antic camp lines
        if (lines != null && lines.Length > 0) return lines;

        // Fallback legacy
        return new DialogueLine[]
        {
            new DialogueLine
            {
                text = description,
                portrait = portrait,
                portraitAnimator = portraitAnimator
            }
        };
    }

    /// <summary>
    /// Propietat de compatibilitat per codi que feia servir .Lines
    /// </summary>
    public DialogueLine[] Lines => GetCurrentLines();
}
