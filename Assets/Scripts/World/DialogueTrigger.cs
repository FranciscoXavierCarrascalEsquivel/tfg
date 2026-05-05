using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider2D))]
public class DialogueTrigger : MonoBehaviour
{
    [Header("Configuració")]
    [SerializeField] private Interactable.DialogueLine[] lines;
    [SerializeField] private bool triggerOnlyOnce = true;
    
    [Header("Detecció")]
    [SerializeField] private LayerMask playerLayer = -1;
    [SerializeField] private string playerTag = "Player";

    [Header("Moviment Automàtic (Opcional)")]
    [Tooltip("Llista ordenada de punts per on caminarà el jugador un cop acabi el diàleg. El jugador anirà del primer a l'últim en ordre.")]
    [SerializeField] private Transform[] walkWaypoints;
    
    [Tooltip("Velocitat a la que caminarà el jugador cap al destí.")]
    [SerializeField] private float walkSpeed = 5f;

    [Tooltip("Distància mínima a cada punt per considerar que el jugador ha arribat.")]
    [SerializeField] private float arrivalThreshold = 0.15f;

    [Header("Desactivació per Esdeveniment")]
    [Tooltip("Si s'assigna un Interactable aquí, el trigger es desactivarà permanentment un cop el jugador hagi interactuat amb ell.")]
    [SerializeField] private Interactable disableAfterInteraction;

    private bool hasTriggered;
    private bool permanentlyDisabled;
    private bool isProcessing;  // True des que es dispara fins que es tanca el diàleg + auto-walk
    private float cooldownTimer = 0f;
    private const float COOLDOWN_DURATION = 1.0f;

    /// <summary>
    /// Crida aquest mètode des d'un UnityEvent (per exemple, des de l'onLineReached d'un Interactable)
    /// per desactivar permanentment aquest trigger.
    /// </summary>
    public void DisableTrigger()
    {
        permanentlyDisabled = true;
    }

    /// <summary>
    /// Torna a activar el trigger si ha estat desactivat.
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
        // Només re-disparar si no estem processant ni en cooldown (evita el bug de doble diàleg)
        if (!isProcessing && cooldownTimer <= 0f)
            TryTrigger(collision);
    }

    private void Update()
    {
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;
    }

    private void TryTrigger(Collider2D collision)
    {
        if (permanentlyDisabled) return;
        if (isProcessing) return;  // Ja estem en mig d'un diàleg/auto-walk

        // Comprovem si l'Interactable assignat ja ha estat usat
        if (disableAfterInteraction != null && disableAfterInteraction.HasBeenInteracted)
        {
            permanentlyDisabled = true;
            return;
        }

        if (hasTriggered && triggerOnlyOnce) return;

        bool isPlayer = collision.CompareTag(playerTag) || ((1 << collision.gameObject.layer) & playerLayer.value) != 0;

        if (isPlayer)
        {
            hasTriggered = true;
            isProcessing = true;  // Bloquejar re-entrada fins que tot acabi
            
            var dialogueUI = FindFirstObjectByType<DialogueUI>();
            if (dialogueUI == null)
            {
                var go = new GameObject("DialogueManager");
                dialogueUI = go.AddComponent<DialogueUI>();
            }

            var playerController = collision.GetComponent<PlayerController2D>();
            if (playerController != null) playerController.LockMovement();

            var playerRb = collision.GetComponent<Rigidbody2D>();
            var playerAnim = collision.GetComponent<Animator>();

            System.Action onClosed = null;
            onClosed = () => 
            {
                dialogueUI.OnDialogueClosed -= onClosed;

                // Si tenim disableAfterInteraction configurat, el trigger es pot repetir fins que es cridi DisableTrigger
                if (disableAfterInteraction != null)
                {
                    hasTriggered = false;
                }

                if (walkWaypoints != null && walkWaypoints.Length > 0 && playerController != null)
                {
                    // L'auto-walk gestiona el desbloqueig i el cooldown al acabar
                    StartCoroutine(AutoWalkRoutine(playerController, playerRb, playerAnim));
                }
                else
                {
                    // Cooldown + fi de processament
                    cooldownTimer = COOLDOWN_DURATION;
                    isProcessing = false;
                    if (playerController != null) playerController.UnlockMovement();
                }
            };
            dialogueUI.OnDialogueClosed += onClosed;

            dialogueUI.StartDialogue(lines);
        }
    }

    private IEnumerator AutoWalkRoutine(PlayerController2D playerController, Rigidbody2D rb, Animator anim)
    {
        if (rb == null || walkWaypoints == null || walkWaypoints.Length == 0)
        {
            if (playerController != null) playerController.UnlockMovement();
            yield break;
        }

        // Diem al PlayerController que no sobreescrigui l'animació
        playerController.SetAutoWalking(true);

        // Agafem la velocitat i so de caminar reals del jugador
        float speed = playerController.WalkSpeed > 0.01f ? playerController.WalkSpeed : walkSpeed;
        AudioClip footstepClip = playerController.WalkSound;
        float footstepInterval = playerController.WalkSoundInterval;
        float footstepTimer = 0f;

        int IsMovingHash = Animator.StringToHash("isMoving");
        int MoveXHash = Animator.StringToHash("moveX");
        int MoveYHash = Animator.StringToHash("moveY");
        int LastMoveXHash = Animator.StringToHash("lastMoveX");
        int LastMoveYHash = Animator.StringToHash("lastMoveY");
        int SpeedMulHash = Animator.StringToHash("moveSpeedMultiplier");

        // Comprovem quins paràmetres existeixen a l'Animator per evitar errors
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

        Vector2 lastAnimDir = Vector2.down; // Direcció per defecte si no es mou

        for (int i = 0; i < walkWaypoints.Length; i++)
        {
            if (walkWaypoints[i] == null) continue;

            Vector2 targetPos = walkWaypoints[i].position;

            while (Vector2.Distance(rb.position, targetPos) > arrivalThreshold)
            {
                Vector2 dir = (targetPos - rb.position).normalized;

                Vector2 animDir;
                if (Mathf.Abs(dir.y) >= Mathf.Abs(dir.x))
                    animDir = new Vector2(0, Mathf.Sign(dir.y));
                else
                    animDir = new Vector2(Mathf.Sign(dir.x), 0);

                lastAnimDir = animDir; // Guardem l'última direcció

                if (anim != null)
                {
                    if (hasIsMoving) anim.SetBool(IsMovingHash, true);
                    if (hasMoveX) anim.SetFloat(MoveXHash, animDir.x);
                    if (hasMoveY) anim.SetFloat(MoveYHash, animDir.y);
                    if (hasLastMoveX) anim.SetFloat(LastMoveXHash, animDir.x);
                    if (hasLastMoveY) anim.SetFloat(LastMoveYHash, animDir.y);
                    if (hasSpeedMul) anim.SetFloat(SpeedMulHash, 1f);
                }

                // So de caminar
                footstepTimer -= Time.fixedDeltaTime;
                if (footstepTimer <= 0f && footstepClip != null)
                {
                    ItemSoundPlayer.Play(footstepClip);
                    footstepTimer = footstepInterval;
                }

                rb.MovePosition(rb.position + dir * speed * Time.fixedDeltaTime);
                yield return new WaitForFixedUpdate();
            }

            // Snap al punt exacte abans de girar cap al següent
            rb.MovePosition(targetPos);
        }

        // Hem acabat: parem l'animació però mantenim la direcció de mirada
        if (anim != null)
        {
            if (hasIsMoving) anim.SetBool(IsMovingHash, false);
            if (hasMoveX) anim.SetFloat(MoveXHash, 0f);
            if (hasMoveY) anim.SetFloat(MoveYHash, 0f);
            if (hasLastMoveX) anim.SetFloat(LastMoveXHash, lastAnimDir.x);
            if (hasLastMoveY) anim.SetFloat(LastMoveYHash, lastAnimDir.y);
            if (hasSpeedMul) anim.SetFloat(SpeedMulHash, 1f);
        }

        // Actualitzem la direcció interna del PlayerController ABANS de desbloquejar
        playerController.SetFacingDirection(lastAnimDir);
        playerController.SetAutoWalking(false);
        
        // Cooldown + fi de processament DESPRÉS de l'auto-walk per evitar doble trigger
        cooldownTimer = COOLDOWN_DURATION;
        isProcessing = false;
        if (playerController != null) playerController.UnlockMovement();
    }
}
