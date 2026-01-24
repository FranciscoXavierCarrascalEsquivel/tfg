using System;
using UnityEngine;

public class Interactable : MonoBehaviour
{
    [Serializable]
    public class DialogueLine
    {
        [TextArea(2, 6)]
        public string text = "…";

        [Header("Portrait (optional)")]
        public Sprite portrait;

        [Tooltip("Opcional: si vols retrat animat (Animator Controller)")]
        public RuntimeAnimatorController portraitAnimator;
    }

    [Header("Dialogue Lines (1 o més)")]
    [SerializeField] private DialogueLine[] lines;

    // Compatibilitat amb el que ja tens (si tens escenes velles amb només description/portrait)
    [Header("Legacy (si no omples 'lines')")]
    [TextArea(2, 6)]
    [SerializeField] private string description = "…";
    [SerializeField] private Sprite portrait;
    [SerializeField] private RuntimeAnimatorController portraitAnimator;

    /// <summary>
    /// Retorna les línies. Si no n'hi ha, crea una línia legacy.
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
