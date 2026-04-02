using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class DialogueTrigger : MonoBehaviour
{
    [Header("Configuració")]
    [SerializeField] private Interactable.DialogueLine[] lines;
    [SerializeField] private bool triggerOnlyOnce = true;
    
    [Header("Detecció")]
    [SerializeField] private LayerMask playerLayer = -1; // -1 és "Tot", però l'usuari pot afinar a "Player"
    [SerializeField] private string playerTag = "Player"; // Detecció extra per tag

    private bool hasTriggered;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (hasTriggered && triggerOnlyOnce) return;

        // Comprovem si el que creusa és el jugador, ja sigui per capa o per tag
        bool isPlayer = collision.CompareTag(playerTag) || ((1 << collision.gameObject.layer) & playerLayer.value) != 0;

        if (isPlayer)
        {
            hasTriggered = true;
            
            var dialogueUI = FindFirstObjectByType<DialogueUI>();
            if (dialogueUI == null)
            {
                var go = new GameObject("DialogueManager");
                dialogueUI = go.AddComponent<DialogueUI>();
            }

            // Desactiva el moviment del jugador automàtic
            var playerController = collision.GetComponent<PlayerController2D>();
            if (playerController != null) playerController.LockMovement();

            // Alliberem el jugador un cop acabi aquest text
            System.Action onClosed = null;
            onClosed = () => 
            {
                if (playerController != null) playerController.UnlockMovement();
                dialogueUI.OnDialogueClosed -= onClosed; // evitem repeticions de memòria
            };
            dialogueUI.OnDialogueClosed += onClosed;

            dialogueUI.StartDialogue(lines);
        }
    }
}
