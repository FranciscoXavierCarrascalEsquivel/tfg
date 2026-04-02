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

    [Header("Dialogue Lines (1 o més)")]
    [SerializeField] private DialogueLine[] lines; // Línies de diàleg de l'objecte.

    // Compatibilitat amb el que ja tens (si tens escenes velles amb només description/portrait)
    [Header("Legacy (si no omples 'lines')")]
    [TextArea(2, 6)]
    [SerializeField] private string description = "…";
    [SerializeField] private Sprite portrait;
    [SerializeField] private RuntimeAnimatorController portraitAnimator;

    /// <summary>
    /// Retorna les línies de diàleg. Si no n'hi ha, fa servir les dades legacy.
    /// </summary>
    public DialogueLine[] Lines
    {
        get
        {
            if (lines != null && lines.Length > 0) return lines;

            // fallback legacy
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
    }
}
