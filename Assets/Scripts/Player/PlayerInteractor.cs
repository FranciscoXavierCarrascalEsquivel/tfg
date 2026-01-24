using UnityEngine;

public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private float radius = 0.6f;
    [SerializeField] private LayerMask interactableLayer;
    [SerializeField] private DialogueUI dialogueUI;

    // ✅ Referència al controlador del jugador per bloquejar moviment
    [SerializeField] private PlayerController2D playerController;

    private void Awake()
    {
        // Auto-assign si t'ho deixes buit (opcional però útil)
        if (playerController == null)
            playerController = GetComponent<PlayerController2D>();

        // Quan es tanqui el diàleg, desbloqueja moviment
        if (dialogueUI != null)
            dialogueUI.OnDialogueClosed += HandleDialogueClosed;
    }

    private void OnDestroy()
    {
        if (dialogueUI != null)
            dialogueUI.OnDialogueClosed -= HandleDialogueClosed;
    }

    private void HandleDialogueClosed()
    {
        if (playerController != null)
            playerController.UnlockMovement();
    }

    private void Update()
    {
        if (!Input.GetKeyDown(interactKey)) return;

        // Si hi ha diàleg obert, la E serveix per skip/avançar línia/tancar quan toca
        if (dialogueUI != null && dialogueUI.IsOpen)
        {
            dialogueUI.AdvanceOrSkip();
            return;
        }

        // Si no hi ha diàleg, intenta trobar objecte
        Collider2D col = Physics2D.OverlapCircle(transform.position, radius, interactableLayer);
        if (!col) return;

        Interactable i = col.GetComponent<Interactable>();
        if (!i) return;

        // ✅ Bloqueja moviment i inicia seqüència (tantes línies com tingui l'objecte)
        if (dialogueUI != null)
        {
            if (playerController != null)
                playerController.LockMovement();

            dialogueUI.StartDialogue(i.Lines);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
