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
