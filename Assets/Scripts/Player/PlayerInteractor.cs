using UnityEngine;

/// <summary>
/// Component encarregat de detectar interaccions des del punt de vista del jugador.
/// Mitjançant la tecla d'interacció (habitualment 'E') i un cercle de col·lisió bidimensional (OverlapCircle),
/// troba objectes a la capa 'Interactable' (NPCs, cofres, cartells) i decideix si obrir un diàleg tradicional,
/// un menú especial o iniciar un xat impulsat per Intel·ligència Artificial Generativa (mode IA).
/// </summary>
public class PlayerInteractor : MonoBehaviour
{
    // Permet bloquejar interaccions globals a tot el joc quan hi ha tremolors forts de càmera
    public static bool IsShaking { get; set; } 

    [Header("Configuració de l'Interacció")]
    [SerializeField] private KeyCode interactKey = KeyCode.E; // Tecla per interaccionar
    [SerializeField] private float radius = 0.6f; // Distància de detecció (radi del cercle)
    [SerializeField] private LayerMask interactableLayer; // Capa física on es troben els objectes interactuables

    [Header("Referències de Control")]
    [SerializeField] private DialogueUI dialogueUI; // Gestor de la interfície de diàleg normal
    [SerializeField] private PlayerController2D playerController; // Controlador del jugador (per a congelar-lo)

    private void Awake()
    {
        // Auto-assignació de referències per comoditat
        if (playerController == null)
            playerController = GetComponent<PlayerController2D>();

        if (dialogueUI == null)
            dialogueUI = FindFirstObjectByType<DialogueUI>();

        // Si no existeix el gestor de diàlegs, en creem un dinàmicament per evitar referències nul·les (NullReferenceException)
        if (dialogueUI == null)
        {
            var go = new GameObject("DialogueManager");
            dialogueUI = go.AddComponent<DialogueUI>();
        }
    }

    private void Update()
    {
        // 1) Condicions de tall: Evitem interactuar si el joc està en pausa o en sacsejada
        if (PauseMenuUI.IsOpen || IsShaking) return;

        // Si el diàleg assistit per IA ja està obert, no deixem processar res més
        if (AIDialogueUI.Instance != null && AIDialogueUI.Instance.IsOpen) return;

        // Comprovem si s'ha premut la tecla d'interacció
        if (!Input.GetKeyDown(interactKey)) return;

        // Bloqueig de seguretat si hi ha qualsevol menú contextual interactiu actiu (botiga, inventari, combat, pausa)
        if (ShopMenuUI.IsOpen || InventoryMenuUI.IsOpen || CombatLoader.IsInCombat || PauseMenuUI.IsOpen) return;

        // 2) Flux de Diàleg Obert: Si ja estem parlant, la 'E' s'encarrega d'avançar el text o saltar-lo
        if (dialogueUI != null && dialogueUI.IsOpen)
        {
            if (PauseMenuUI.IsOpen) return; // Protecció addicional per a la pausa
            if (!dialogueUI.canAdvance) return; // No permetem avançar si la interfície està blocada (escrivint efectes de so, etc.)
            dialogueUI.AdvanceOrSkip();
            return;
        }

        // 3) Detecció física d'elements interactuables al voltant del jugador
        Collider2D col = Physics2D.OverlapCircle(transform.position, radius, interactableLayer);
        if (!col) return;

        Interactable i = col.GetComponent<Interactable>();
        if (!i) return;

        // Congelem el moviment del personatge mentre dura l'interacció/diàleg
        if (playerController != null)
            playerController.LockMovement();

        // --- SISTEMA D'INTERACCIÓ DE CONTINGUT DE IA GENERATIVA (OLLAMA / LLM) ---
        if (i.useGenerativeAI)
        {
            // PRIORITAT 1: startAIDirectly -> Obre la bafarada de xat de IA des del primer moment
            if (i.startAIDirectly)
            {
                OpenAIDialogue(i);
                return;
            }

            // PRIORITAT 2: activateAIAtVersion -> Mode híbrid evolutiu.
            // L'NPC diu frases fixes i, a partir d'un número de visites determinat, desbloqueja el xat lliure.
            if (i.activateAIAtVersion >= 0)
            {
                if (i.ShouldUseAIForCurrentInteraction())
                {
                    // S'ha assolit o superat el nombre de diàlegs ordinaris definits
                    if (i.activateAIAfterNormalDialogue)
                    {
                        // Escenari mixt: llegeix el text prefixat d'aquest cop, i just en tancar-lo, obre la IA
                        if (dialogueUI != null)
                        {
                            System.Action onNormalClosed = null;
                            onNormalClosed = () =>
                            {
                                dialogueUI.OnDialogueClosed -= onNormalClosed; // Neteja de subscripció
                                OpenAIDialogue(i);
                            };
                            dialogueUI.OnDialogueClosed += onNormalClosed;
                            dialogueUI.StartDialogue(i.GetCurrentLines());
                        }
                    }
                    else
                    {
                        // Pas directe a mode xat lliure IA
                        i.RegisterInteraction();
                        OpenAIDialogue(i);
                    }
                    return;
                }
                else
                {
                    // Encara estem en fase de diàlegs tradicionals
                    if (dialogueUI != null)
                    {
                        System.Action onClosed = null;
                        onClosed = () =>
                        {
                            if (playerController != null) playerController.UnlockMovement();
                            dialogueUI.OnDialogueClosed -= onClosed;
                        };
                        dialogueUI.OnDialogueClosed += onClosed;
                        dialogueUI.StartDialogue(i.GetCurrentLines());
                    }
                    return;
                }
            }

            // PRIORITAT 3: activateAIAfterNormalDialogue -> Diàleg estàndard primer + IA directament després
            if (i.activateAIAfterNormalDialogue)
            {
                if (dialogueUI != null)
                {
                    System.Action onNormalClosed = null;
                    onNormalClosed = () =>
                    {
                        dialogueUI.OnDialogueClosed -= onNormalClosed;
                        OpenAIDialogue(i);
                    };
                    dialogueUI.OnDialogueClosed += onNormalClosed;
                    dialogueUI.StartDialogue(i.GetCurrentLines());
                }
                return;
            }

            // Comportament per defecte si està marcada la IA però cap prioritat específica està configurada
            OpenAIDialogue(i);
            return;
        }

        // --- DIÀLEG TRADICIONAL ESTÀNDARD (NPCs estàtics / Cartells) ---
        if (dialogueUI != null)
        {
            System.Action onClosed = null;
            onClosed = () =>
            {
                // Un cop es tanca el diàleg, tornem a desbloquejar el jugador
                if (playerController != null) playerController.UnlockMovement();
                dialogueUI.OnDialogueClosed -= onClosed; // Alliberem de memòria per evitar sobreacumulació de callbacks
            };
            dialogueUI.OnDialogueClosed += onClosed;

            dialogueUI.StartDialogue(i.Lines);
        }
    }

    /// <summary>
    /// Gestiona la transició per a obrir la interfície de diàleg generatiu i enllaçar el desbloqueig del moviment al tancar.
    /// </summary>
    /// <param name="npc">Objecte interactuable d'origen.</param>
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

        // Registrem formalment la interacció per actualitzar estats interns de l'NPC
        npc.RegisterInteraction();

        // Obrim la interfície de xat intel·ligent
        aiUI.Open(npc);
    }

    /// <summary>
    /// Dibuixa el radi de cerca d'interacció en vermell/groc a la finestra d'Escena d'Unity per a facilitar el disseny de nivells.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
