using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class PlayerController2D : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 5f;       // Velocitat quan el jugador camina.
    [SerializeField] private float runSpeed = 8f;        // Velocitat quan el jugador corre.
    [SerializeField] private KeyCode runKey = KeyCode.LeftShift; // Tecla per córrer.

    [Header("Random Encounters")]
    [SerializeField] private CombatLoader combatLoader;
    [SerializeField] private float encounterChancePerStep = 0.05f; // Probabilitat de trobar enemic cada metre recorregut
    [SerializeField] private float minDistanceBetweenEncounters = 8f; // Distància mínima (metres) abans de la següent batalla
    [Tooltip("Llista d'enemics que et poden sortir aleatòriament per l'Overworld")]
    [SerializeField] private EnemyProfile[] wildEnemies;

    private Rigidbody2D rb; // Referència al component Rigidbody2D per a les físiques.
    private Animator anim;  // Referència a l'Animator per gestionar les animacions.

    private Vector2 moveDir;
    private Vector2 lastDir = Vector2.down;

    private bool movementLocked;
    private bool isRunning;
    
    private Vector2 lastPos;
    private float timeSinceLastMove;
    private const float MOVEMENT_TIMEOUT = 0.05f; // Temps sense moviment real abans de considerar que estem parats
    
    private float distanceWalkedSinceEncounter = 3f; // Gràcia inicial en començar l'escena (3 metres)
    private float encounterSuppressionTimer = 0f;    // Temps de gràcia forçat per factors externs

    // Animator params (per evitar typos)
    private static readonly int IsMovingHash = Animator.StringToHash("isMoving");
    private static readonly int MoveXHash = Animator.StringToHash("moveX");
    private static readonly int MoveYHash = Animator.StringToHash("moveY");
    private static readonly int LastMoveXHash = Animator.StringToHash("lastMoveX");
    private static readonly int LastMoveYHash = Animator.StringToHash("lastMoveY");
    private static readonly int SpeedMulHash = Animator.StringToHash("moveSpeedMultiplier");

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();

        rb.gravityScale = 0f;
        rb.freezeRotation = true;
    }

    // Bloqueig/desbloqueig
    public void LockMovement()
    {
        movementLocked = true;
        moveDir = Vector2.zero;
        rb.linearVelocity = Vector2.zero;
        anim.SetBool(IsMovingHash, false);
        anim.SetFloat(SpeedMulHash, 1f);
    }

    public void UnlockMovement()
    {
        movementLocked = false;
    }

    /// <summary>
    /// Evita que surtin monstres durant X segons (per exemple després d'un fade out)
    /// </summary>
    public void SuppressEncounters(float duration)
    {
        encounterSuppressionTimer = duration;
        // Reiniciem la posició de referencia per evitar salts de distància si el jugador és teletransportat
        lastPos = rb != null ? rb.position : (Vector2)transform.position;
    }

    private void Start()
    {
        lastPos = rb.position;
    }

    private void Update()
    {
        if (encounterSuppressionTimer > 0f) encounterSuppressionTimer -= Time.deltaTime;

        if (movementLocked)
        {
            anim.SetBool(IsMovingHash, false);
            anim.SetFloat(SpeedMulHash, 1f);
            
            // Actualitzem lastPos encara que estiguem bloquejats per evitar salts de distància al desbloquejar
            lastPos = rb.position; 
            return;
        }

        float mh = Input.GetAxisRaw("Horizontal");
        float mv = Input.GetAxisRaw("Vertical");
        Vector2 input = new Vector2(mh, mv);

        bool isMovingInput = input.sqrMagnitude > 0.01f;

        // Correm si hi ha input i mantenim premuda la tecla de correr
        isRunning = isMovingInput && Input.GetKey(runKey);

        moveDir = input.normalized;

        // Guarda l'última direcció "dominant" només quan et mous
        if (isMovingInput)
        {
            if (Mathf.Abs(input.y) >= Mathf.Abs(input.x))
                lastDir = new Vector2(0, Mathf.Sign(input.y));
            else
                lastDir = new Vector2(Mathf.Sign(input.x), 0);
        }

        // Càlcul de distància moguda real
        float distanceMoved = Vector2.Distance(rb.position, lastPos);
        
        if (distanceMoved > 0.001f)
        {
            timeSinceLastMove = 0f;
        }
        else
        {
            timeSinceLastMove += Time.deltaTime;
        }

        // Recuperem la posició actual per al següent frame
        lastPos = rb.position;

        // Considerem que estem "bloquejats" només si hem estat una estona sense moure'ns
        bool isStuck = timeSinceLastMove > MOVEMENT_TIMEOUT;

        // Animació ON si: hi ha input I no estem bloquejats
        bool showMovingAnim = isMovingInput && !isStuck;

        // --- SISTEMA DE COMBAT ALEATORI PER DISTÀNCIA ---
        if (showMovingAnim && combatLoader != null && wildEnemies != null && wildEnemies.Length > 0 && encounterSuppressionTimer <= 0f)
        {
            distanceWalkedSinceEncounter += distanceMoved;

            if (distanceWalkedSinceEncounter >= minDistanceBetweenEncounters)
            {
                // Un cop superada la distància mínima, cada "pas" te una probabilitat
                if (Random.value < encounterChancePerStep * distanceMoved)
                {
                    distanceWalkedSinceEncounter = 0f; // Reinicia el comptador de distància
                    TriggerRandomEncounter();
                    return; // Sortim per evitar que es pugui processar res més aquest frame
                }
            }
        }

        // Animator:
        anim.SetBool(IsMovingHash, showMovingAnim);

        // Per al MOVE (walk): usa el moviment real (4 direccions)
        Vector2 moveAnimDir = Vector2.zero;
        if (isMovingInput)
        {
            if (Mathf.Abs(input.y) >= Mathf.Abs(input.x))
                moveAnimDir = new Vector2(0, Mathf.Sign(input.y));
            else
                moveAnimDir = new Vector2(Mathf.Sign(input.x), 0);
        }
        anim.SetFloat(MoveXHash, moveAnimDir.x);
        anim.SetFloat(MoveYHash, moveAnimDir.y);

        // Per a IDLE: sempre l’última direcció coneguda
        anim.SetFloat(LastMoveXHash, lastDir.x);
        anim.SetFloat(LastMoveYHash, lastDir.y);

        // Multiplicador d'animació segons proporció run/walk
        float ratio = (walkSpeed <= 0.0001f) ? 1f : (runSpeed / walkSpeed);
        anim.SetFloat(SpeedMulHash, isRunning ? ratio : 1f);
    }

    private void TriggerRandomEncounter()
    {
        Debug.Log("WILD ENCOUNTER TRIGGERED!");
        LockMovement(); // Parem el jugador instantàniament
        
        // Aturem animacions per seguretat
        anim.SetBool(IsMovingHash, false);
        anim.SetFloat(MoveXHash, 0);
        anim.SetFloat(MoveYHash, 0);
        anim.SetFloat(SpeedMulHash, 1f);

        // Escollim un enemic a l'atzar
        EnemyProfile chosenEnemy = wildEnemies[Random.Range(0, wildEnemies.Length)];
        
        CombatEncounter enc = new CombatEncounter();
        enc.enemyProfile = chosenEnemy;
        
        combatLoader.StartCombat(enc);
    }

    private void FixedUpdate()
    {
        if (movementLocked) return;

        float currentSpeed = isRunning ? runSpeed : walkSpeed;
        rb.MovePosition(rb.position + moveDir * currentSpeed * Time.fixedDeltaTime);
    }
}
