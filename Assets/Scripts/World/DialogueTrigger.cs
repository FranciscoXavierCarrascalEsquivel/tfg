using UnityEngine;
using System.Collections;

/// <summary>
/// Trigger de Diàleg basat en col·lisió 2D per a l'Overworld.
/// Quan el jugador entra o roman dins del collider, es congela el seu moviment i es dispara
/// una seqüència de text (diàleg obligatori / tutorial). Opcionalment, el script permet forçar
/// un moviment programat del jugador (auto-walk) a través d'una ruta de waypoints un cop finalitza
/// la conversa, útil per a seqüències cinemàtiques nates del joc.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class DialogueTrigger : MonoBehaviour
{
    [Header("Configuració del Diàleg")]
    [SerializeField] private Interactable.DialogueLine[] lines; // Frases del diàleg
    [SerializeField] private bool triggerOnlyOnce = true; // Si es marca, el trigger es destruirà/desactivarà després del primer ús
    
    [Header("Detecció Física")]
    [SerializeField] private LayerMask playerLayer = -1; // Màscara de capa del jugador
    [SerializeField] private string playerTag = "Player"; // Tag identificatiu del jugador

    [Header("Moviment Automàtic Post-Diàleg (Opcional)")]
    [Tooltip("Llista ordenada de punts de referència per on caminarà el jugador un cop acabi el diàleg. El jugador hi anirà en ordre seqüencial.")]
    [SerializeField] private Transform[] walkWaypoints;
    
    [Tooltip("Velocitat a la que caminarà el jugador cap al destí de forma automàtica.")]
    [SerializeField] private float walkSpeed = 5f;

    [Tooltip("Distància límit mínima a cada waypoint per considerar que s'hi ha arribat.")]
    [SerializeField] private float arrivalThreshold = 0.15f;

    [Header("Desactivació Externa")]
    [Tooltip("Si s'assigna un objecte Interactable aquí, el trigger es desactivarà de forma definitiva un cop el jugador hagi completat la interacció amb ell.")]
    [SerializeField] private Interactable disableAfterInteraction;

    private bool hasTriggered; // Estat de control de si ja s'ha disparat
    private bool permanentlyDisabled; // Flag de desactivació completa/permanent
    private bool isProcessing;  // Control d'estat de transició (actiu des de l'inici del diàleg fins a l'acabament de l'auto-walk)
    private float cooldownTimer = 0f; // Temporitzador d'anti-rebot
    private const float COOLDOWN_DURATION = 1.0f; // Durada d'espera contra dobles esdeveniments consecutius

    /// <summary>
    /// Desactiva de forma permanent el trigger. Es pot cridar des de UnityEvents interns d'altres components (p. ex. Interactables).
    /// </summary>
    public void DisableTrigger()
    {
        permanentlyDisabled = true;
    }

    /// <summary>
    /// Reactiva completament el trigger, netejant els consums previs.
    /// </summary>
    public void EnableTrigger()
    {
        permanentlyDisabled = false;
        hasTriggered = false;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        TryTrigger(collision);
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        // Protecció de seguretat: Només provem de re-disparar si no s'està processant cap seqüència activa ni estem en cooldown
        if (!isProcessing && cooldownTimer <= 0f)
            TryTrigger(collision);
    }

    private void Update()
    {
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;
    }

    /// <summary>
    /// Prova d'iniciar el diàleg si es compleixen totes les condicions i l'entitat col·lisionadora és el Jugador.
    /// </summary>
    private void TryTrigger(Collider2D collision)
    {
        if (permanentlyDisabled) return;
        if (isProcessing) return; 

        // Comprovem si s'havia lligat a un objecte que ja ha finalitzat la seva interacció
        if (disableAfterInteraction != null && disableAfterInteraction.HasBeenInteracted)
        {
            permanentlyDisabled = true;
            return;
        }

        if (hasTriggered && triggerOnlyOnce) return;

        // Validació robusta del tag o de la capa de físiques del jugador
        bool isPlayer = collision.CompareTag(playerTag) || ((1 << collision.gameObject.layer) & playerLayer.value) != 0;

        if (isPlayer)
        {
            hasTriggered = true;
            isProcessing = true;  // Bloquegem re-entrades durant la corrutina
            
            var dialogueUI = FindFirstObjectByType<DialogueUI>();
            if (dialogueUI == null)
            {
                var go = new GameObject("DialogueManager");
                dialogueUI = go.AddComponent<DialogueUI>();
            }

            // Congelem el moviment del jugador
            var playerController = collision.GetComponent<PlayerController2D>();
            if (playerController != null) playerController.LockMovement();

            var playerRb = collision.GetComponent<Rigidbody2D>();
            var playerAnim = collision.GetComponent<Animator>();

            // Callback que es cridarà quan l'usuari tanqui l'última línia de diàleg
            System.Action onClosed = null;
            onClosed = () => 
            {
                dialogueUI.OnDialogueClosed -= onClosed; // Alliberament de memòria

                // Si tenim dependència d'un interactable extern, mantenim el trigger actiu fins que aquest es completi
                if (disableAfterInteraction != null)
                {
                    hasTriggered = false;
                }

                // Si hi ha camins definits per a auto-walk, iniciem la marxa forçada
                if (walkWaypoints != null && walkWaypoints.Length > 0 && playerController != null)
                {
                    StartCoroutine(AutoWalkRoutine(playerController, playerRb, playerAnim));
                }
                else
                {
                    // Si no hi ha camí, alliberem el jugador immediatament amb un petit cooldown de seguretat
                    cooldownTimer = COOLDOWN_DURATION;
                    isProcessing = false;
                    if (playerController != null) playerController.UnlockMovement();
                }
            };
            dialogueUI.OnDialogueClosed += onClosed;

            // Engeguem el diàleg a la interfície d'usuari
            dialogueUI.StartDialogue(lines);
        }
    }

    /// <summary>
    /// Corrutina d'auto-walk: mou físicament el Rigidbody del jugador a través de la llista de Waypoints,
    /// sincronitzant alhora les seves velocitats reals, els sons de passos i els paràmetres de l'Animator.
    /// </summary>
    private IEnumerator AutoWalkRoutine(PlayerController2D playerController, Rigidbody2D rb, Animator anim)
    {
        if (rb == null || walkWaypoints == null || walkWaypoints.Length == 0)
        {
            if (playerController != null) playerController.UnlockMovement();
            yield break;
        }

        // Activem el mode auto-walk per a impedir que el PlayerController2D cancel·li o sobreescrigui les animacions
        playerController.SetAutoWalking(true);

        // Agafem les propietats de so i moviment configurades en el propi Jugador per a màxima coherència
        float speed = playerController.WalkSpeed > 0.01f ? playerController.WalkSpeed : walkSpeed;
        AudioClip footstepClip = playerController.WalkSound;
        float footstepInterval = playerController.WalkSoundInterval;
        float footstepTimer = 0f;

        // Cache de hashes de l'Animator
        int IsMovingHash = Animator.StringToHash("isMoving");
        int MoveXHash = Animator.StringToHash("moveX");
        int MoveYHash = Animator.StringToHash("moveY");
        int LastMoveXHash = Animator.StringToHash("lastMoveX");
        int LastMoveYHash = Animator.StringToHash("lastMoveY");
        int SpeedMulHash = Animator.StringToHash("moveSpeedMultiplier");

        // Comprovem la presència dels paràmetres per evitar errors a la consola en cas de canvis en l'Animator del model
        bool hasIsMoving = false, hasMoveX = false, hasMoveY = false;
        bool hasLastMoveX = false, hasLastMoveY = false, hasSpeedMul = false;
        if (anim != null)
        {
            foreach (var p in anim.parameters)
            {
                if (p.nameHash == IsMovingHash) hasIsMoving = true;
                if (p.nameHash == MoveXHash) hasMoveX = true;
                if (p.nameHash == MoveYHash) hasMoveY = true;
                if (p.nameHash == LastMoveXHash) hasLastMoveX = true;
                if (p.nameHash == LastMoveYHash) hasLastMoveY = true;
                if (p.nameHash == SpeedMulHash) hasSpeedMul = true;
            }
        }

        Vector2 lastAnimDir = Vector2.down; 

        // Recorrem cada punt de la ruta de manera seqüencial
        for (int i = 0; i < walkWaypoints.Length; i++)
        {
            if (walkWaypoints[i] == null) continue;

            Vector2 targetPos = walkWaypoints[i].position;

            // Caminem cap al punt actual fins a estar prou a prop (límit d'arribada)
            while (Vector2.Distance(rb.position, targetPos) > arrivalThreshold)
            {
                Vector2 dir = (targetPos - rb.position).normalized;

                // Calculem l'eix dominant per orientar l'animació gràfica del personatge
                Vector2 animDir;
                if (Mathf.Abs(dir.y) >= Mathf.Abs(dir.x))
                    animDir = new Vector2(0, Mathf.Sign(dir.y));
                else
                    animDir = new Vector2(Mathf.Sign(dir.x), 0);

                lastAnimDir = animDir; 

                // Actualitzem l'Animator amb l'estat de marxa actiu
                if (anim != null)
                {
                    if (hasIsMoving) anim.SetBool(IsMovingHash, true);
                    if (hasMoveX) anim.SetFloat(MoveXHash, animDir.x);
                    if (hasMoveY) anim.SetFloat(MoveYHash, animDir.y);
                    if (hasLastMoveX) anim.SetFloat(LastMoveXHash, animDir.x);
                    if (hasLastMoveY) anim.SetFloat(LastMoveYHash, animDir.y);
                    if (hasSpeedMul) anim.SetFloat(SpeedMulHash, 1f);
                }

                // Gestió dels sons de passos durant el camí automàtic
                footstepTimer -= Time.fixedDeltaTime;
                if (footstepTimer <= 0f && footstepClip != null)
                {
                    ItemSoundPlayer.Play(footstepClip);
                    footstepTimer = footstepInterval;
                }

                // Desplaçament físic a través del Rigidbody2D
                rb.MovePosition(rb.position + dir * speed * Time.fixedDeltaTime);
                yield return new WaitForFixedUpdate();
            }

            // Realitzem un ajust final de seguretat (snap) al punt exacte abans d'apuntar al següent objectiu
            rb.MovePosition(targetPos);
        }

        // Hem completat tota la ruta: aturem el personatge mantenint l'orientació del seu últim pas
        if (anim != null)
        {
            if (hasIsMoving) anim.SetBool(IsMovingHash, false);
            if (hasMoveX) anim.SetFloat(MoveXHash, 0f);
            if (hasMoveY) anim.SetFloat(MoveYHash, 0f);
            if (hasLastMoveX) anim.SetFloat(LastMoveXHash, lastAnimDir.x);
            if (hasLastMoveY) anim.SetFloat(LastMoveYHash, lastAnimDir.y);
            if (hasSpeedMul) anim.SetFloat(SpeedMulHash, 1f);
        }

        // Retornem el control del personatge al propi controlador principal
        playerController.SetFacingDirection(lastAnimDir);
        playerController.SetAutoWalking(false);
        
        // Apliquem el cooldown de seguretat i alliberem el processament
        cooldownTimer = COOLDOWN_DURATION;
        isProcessing = false;
        if (playerController != null) playerController.UnlockMovement();
    }
}
