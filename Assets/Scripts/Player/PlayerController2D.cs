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
    [SerializeField] private float encounterChancePerSecond = 0.02f; // Probabilitat molt baixa per segon per no ser molest
    [SerializeField] private float minEncounterCooldown = 5f; // Segons mínims abans de poder trobar un enemic caminant
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
    
    private float encounterCooldownTimer = 2f; // Gràcia inicial en començar l'escena

    // Animator params (per evitar typos)
    private static readonly int IsMovingHash = Animator.StringToHash("isMoving");
    private static readonly int MoveXHash = Animator.StringToHash("moveX");
    private static readonly int MoveYHash = Animator.StringToHash("moveY");
    private static readonly int LastMoveXHash = Animator.StringToHash("lastMoveX");
    private static readonly int LastMoveYHash = Animator.StringToHash("lastMoveY");
    private static readonly int SpeedMulHash = Animator.StringToHash("moveSpeedMultiplier"); // ✅ nou

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();

        rb.gravityScale = 0f;
        rb.freezeRotation = true;
    }

    // ✅ Bloqueig/desbloqueig
    public void LockMovement()
    {
        movementLocked = true;
        moveDir = Vector2.zero;
        rb.linearVelocity = Vector2.zero;   // Unity 6: Aturem el moviment físic.
        anim.SetBool(IsMovingHash, false);   // Aturem l'animació de caminar.
        anim.SetFloat(SpeedMulHash, 1f);
    }

    public void UnlockMovement()
    {
        movementLocked = false;
    }


    private void Start()
    {
        lastPos = rb.position;
    }

    private void Update()
    {
        if (encounterCooldownTimer > 0f) encounterCooldownTimer -= Time.deltaTime;

        if (movementLocked)
        {
            anim.SetBool(IsMovingHash, false);
            anim.SetFloat(SpeedMulHash, 1f);
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

        // --- FIX 2: Buffer de moviment ---
        // Si Update va més ràpid que FixedUpdate, hi haurà frames on distanceMoved sigui 0 encara que ens moguem.
        // Usem un timer per filtrar-ho.
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

        // ✅ Multiplicador d'animació segons proporció run/walk
        // si camines = 1, si corres = runSpeed/walkSpeed
        float ratio = (walkSpeed <= 0.0001f) ? 1f : (runSpeed / walkSpeed);
        anim.SetFloat(SpeedMulHash, isRunning ? ratio : 1f);

        // --- TICK DE COMBAT ALEATORI ---
        if (showMovingAnim && combatLoader != null && wildEnemies != null && wildEnemies.Length > 0 && encounterCooldownTimer <= 0f)
        {
            // Tira els daus base per segon
            if (Random.value < encounterChancePerSecond * Time.deltaTime)
            {
                TriggerRandomEncounter();
                encounterCooldownTimer = minEncounterCooldown; // Reinicia el rellotge fred
            }
        }
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
