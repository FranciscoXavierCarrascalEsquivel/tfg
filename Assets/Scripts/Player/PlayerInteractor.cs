using UnityEngine;

public class PlayerInteractor : MonoBehaviour
{
    public static bool IsShaking { get; set; } // Bloqueja interaccions globals si la pantalla tremola

    [SerializeField] private KeyCode interactKey = KeyCode.E; // Tecla d'interacció
    [SerializeField] private float radius = 0.6f; // Distancia màxima entre el jugador i l'objecte interactuable.
    [SerializeField] private LayerMask interactableLayer; // Capa d'in
    [SerializeField] private DialogueUI dialogueUI; // 
    [SerializeField] private PlayerController2D playerController; // Objecte que te programat el controlador del jugador (així podem bloquejar i desbloquejar el moviment).

    private void Awake()
    {
        if (playerController == null)
            playerController = GetComponent<PlayerController2D>();

        if (dialogueUI == null)
            dialogueUI = FindFirstObjectByType<DialogueUI>();

        if (dialogueUI == null)
        {
            var go = new GameObject("DialogueManager");
            dialogueUI = go.AddComponent<DialogueUI>();
        }

    }

    private void Update()
    {
        if (PauseMenuUI.IsOpen || IsShaking) return;

        // Si el mode IA està obert, no processar cap interacció
        if (AIDialogueUI.Instance != null && AIDialogueUI.Instance.IsOpen) return;

        if (!Input.GetKeyDown(interactKey)) return;

        // Si la botiga està oberta, estem en combat o en pausa, es bloqueja qualsevol interacció amb el món.
        if (ShopMenuUI.IsOpen || CombatLoader.IsInCombat || PauseMenuUI.IsOpen) return;

        // Si hi ha diàleg obert, la E serveix per avançar línia o tancar quan toca.
        
        if (dialogueUI != null && dialogueUI.IsOpen)
        {
            if (PauseMenuUI.IsOpen) return; // No avançar diàleg en pausa
            if (!dialogueUI.canAdvance) return; // NO avançar si està bloquejat
            dialogueUI.AdvanceOrSkip();
            return;
        }

        // Si no hi ha diàleg, intenta trobar objecte
        Collider2D col = Physics2D.OverlapCircle(transform.position, radius, interactableLayer);
        if (!col) return;

        Interactable i = col.GetComponent<Interactable>();
        if (!i) return;

        // Bloquejar moviment
        if (playerController != null)
            playerController.LockMovement();

        // --- Lògica IA Generativa ---
        if (i.useGenerativeAI)
        {
            // Prioritat: startAIDirectly > activateAIAfterNormalDialogue > default (IA directa)
            if (i.startAIDirectly)
            {
                // Obrir directament el mode IA
                OpenAIDialogue(i);
                return;
            }

            if (i.activateAIAfterNormalDialogue)
            {
                // Primer diàleg normal, després mode IA
                if (dialogueUI != null)
                {
                    System.Action onNormalClosed = null;
                    onNormalClosed = () =>
                    {
                        dialogueUI.OnDialogueClosed -= onNormalClosed;
                        // Quan el diàleg normal es tanca, obrir mode IA
                        OpenAIDialogue(i);
                    };
                    dialogueUI.OnDialogueClosed += onNormalClosed;
                    dialogueUI.StartDialogue(i.GetCurrentLines());
                }
                return;
            }

            // Default: si useGenerativeAI és cert però cap opció especial,
            // obre directament la IA (comportament més simple i estable)
            OpenAIDialogue(i);
            return;
        }

        // --- Diàleg Normal (comportament original) ---
        if (dialogueUI != null)
        {
            System.Action onClosed = null;
            onClosed = () =>
            {
                if (playerController != null) playerController.UnlockMovement();
                dialogueUI.OnDialogueClosed -= onClosed; // Evita repeticions de memòria
            };
            dialogueUI.OnDialogueClosed += onClosed;

            dialogueUI.StartDialogue(i.Lines);
        }
    }

    /// <summary>
    /// Obre el mode de diàleg IA per a un NPC concret.
    /// </summary>
    private void OpenAIDialogue(Interactable npc)
    {
        var aiUI = AIDialogueUI.Instance;
        if (aiUI == null) return;

        System.Action onAIClosed = null;
        onAIClosed = () =>
        {
            aiUI.OnAIDialogueClosed -= onAIClosed;
            if (playerController != null) playerController.UnlockMovement();
        };
        aiUI.OnAIDialogueClosed += onAIClosed;

        // Registrar la interacció per a triggers del món
        npc.RegisterInteraction();

        aiUI.Open(npc);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
