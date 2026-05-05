using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class PlayerController2D : MonoBehaviour
{
    private static TMP_FontAsset cachedAlertFont;

    [Header("Alert & Encounters")]
    [Tooltip("El jugador a qui apareixerà l'exclamació a sobre.")]
    public Transform playerTransform;
    [Tooltip("El so que es reproduirà en aparèixer l'exclamació.")]
    public AudioClip alertSound;


    [Header("Movement")]
    [SerializeField] private float walkSpeed = 5f;       // Velocitat quan el jugador camina.
    [SerializeField] private float runSpeed = 8f;        // Velocitat quan el jugador corre.
    [SerializeField] private KeyCode runKey = KeyCode.LeftShift; // Tecla per córrer.

    [Header("Audio")]
    [SerializeField] private AudioClip walkSound;
    [SerializeField] private float walkSoundInterval = 0.6f;
    [SerializeField] private AudioClip wallCollisionSound;
    [SerializeField] private float wallCollisionSoundInterval = 0.4f;

    private float walkSoundTimer;
    private float wallSoundTimer;

    [Header("Random Encounters")]
    [SerializeField] private CombatLoader combatLoader;
    [SerializeField] private float encounterChancePerStep = 0.05f; // Probabilitat de trobar enemic cada metre recorregut
    [SerializeField] private float minDistanceBetweenEncounters = 8f; // Distància mínima (metres) abans de la següent batalla
    [Tooltip("Llista d'enemics que et poden sortir aleatòriament per l'Overworld")]
    public EnemyProfile[] wildEnemies;
    
    [SerializeField] private bool disableEncounters = false;

    private Rigidbody2D rb; // Referència al component Rigidbody2D per a les físiques.
    private Animator anim;  // Referència a l'Animator per gestionar les animacions.

    private Vector2 moveDir;
    private Vector2 lastDir = Vector2.down;

    private bool movementLocked;
    private bool isRunning;
    private bool isAutoWalking;

    /// <summary>Per permetre que scripts externs (DialogueTrigger) puguin accedir al so i velocitat de caminar.</summary>
    public AudioClip WalkSound => walkSound;
    public float WalkSoundInterval => walkSoundInterval;
    public float WalkSpeed => walkSpeed;

    /// <summary>Indica que un script extern controla l'animació (no sobreescriure des d'Update).</summary>
    public void SetAutoWalking(bool value) { isAutoWalking = value; }

    /// <summary>Estableix la direcció d'idle del personatge (on mira quan està parat).</summary>
    public void SetFacingDirection(Vector2 dir) { lastDir = dir; }
    
    private Vector2 lastPos;
    private float timeSinceLastMove;
    private const float MOVEMENT_TIMEOUT = 0.15f; // Temps sense moviment real abans de considerar que estem parats
    
    private float distanceWalkedSinceEncounter = 3f; // Gràcia inicial en començar l'escena (3 metres)
    private float encounterSuppressionTimer = 0f;    // Temps de gràcia forçat per factors externs
    private bool canEncounterAnyWildEnemy = true;
    private float encounterCheckTimer = 0f;

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
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        CheckAnimatorParams();

        // Carreguem els àudios des de Resources (funciona a les builds .exe)
        if (walkSound == null) walkSound = Resources.Load<AudioClip>("SFX/step");
        if (wallCollisionSound == null) wallCollisionSound = Resources.Load<AudioClip>("SFX/bump");
    }

    // Bloqueig/desbloqueig
    public bool IsMovementLocked => movementLocked;
    public void LockMovement()
    {
        movementLocked = true;
        moveDir = Vector2.zero;
        rb.linearVelocity = Vector2.zero;
        if (hasIsMovingParam) anim.SetBool(IsMovingHash, false);
        if (hasSpeedMulParam) anim.SetFloat(SpeedMulHash, 1f);
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

    private bool hasSpeedMulParam;
    private bool hasMoveXParam;
    private bool hasMoveYParam;
    private bool hasLastMoveXParam;
    private bool hasLastMoveYParam;
    private bool hasIsMovingParam;

    private void CheckAnimatorParams()
    {
        if (anim == null) return;
        foreach (var p in anim.parameters)
        {
            if (p.nameHash == SpeedMulHash) hasSpeedMulParam = true;
            if (p.nameHash == MoveXHash) hasMoveXParam = true;
            if (p.nameHash == MoveYHash) hasMoveYParam = true;
            if (p.nameHash == LastMoveXHash) hasLastMoveXParam = true;
            if (p.nameHash == LastMoveYHash) hasLastMoveYParam = true;
            if (p.nameHash == IsMovingHash) hasIsMovingParam = true;
        }
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
            if (!isAutoWalking)
            {
                if (hasIsMovingParam) anim.SetBool(IsMovingHash, false);
                if (hasSpeedMulParam) anim.SetFloat(SpeedMulHash, 1f);
            }
            
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
        else if (isMovingInput)
        {
            timeSinceLastMove += Time.deltaTime;
        }
        else
        {
            timeSinceLastMove = 0f;
        }

        // Recuperem la posició actual per al següent frame
        lastPos = rb.position;

        // Considerem que estem "bloquejats" només si hem estat una estona sense moure'ns
        bool isStuck = timeSinceLastMove > MOVEMENT_TIMEOUT;

        // Animació ON si: hi ha input I no estem bloquejats
        bool showMovingAnim = isMovingInput && !isStuck;

        // --- GESTIÓ D'ÀUDIO DE MOVIMENT ---
        if (walkSoundTimer > 0f) walkSoundTimer -= Time.deltaTime;
        if (wallSoundTimer > 0f) wallSoundTimer -= Time.deltaTime;

        if (isMovingInput)
        {
            if (isStuck)
            {
                if (wallSoundTimer <= 0f && wallCollisionSound != null)
                {
                    ItemSoundPlayer.Play(wallCollisionSound);
                    wallSoundTimer = wallCollisionSoundInterval;
                }
            }
            else
            {
                if (walkSoundTimer <= 0f && walkSound != null)
                {
                    ItemSoundPlayer.Play(walkSound);
                    walkSoundTimer = isRunning ? walkSoundInterval * 0.8f : walkSoundInterval;
                }
            }
        }

        // --- SISTEMA DE COMBAT ALEATORI PER DISTÀNCIA ---
        if (!disableEncounters && showMovingAnim && combatLoader != null && wildEnemies != null && wildEnemies.Length > 0 && encounterSuppressionTimer <= 0f)
        {
            // Throttle: No cal comprovar la base de dades d'enemics 120 cops per segon
            encounterCheckTimer -= Time.deltaTime;
            if (encounterCheckTimer <= 0f)
            {
                encounterCheckTimer = 0.25f; // Comprovar cada 0.25 segons
                canEncounterAnyWildEnemy = false;
                if (PlayerInventory.Instance == null)
                {
                    canEncounterAnyWildEnemy = true;
                }
                else
                {
                    foreach (var enemy in wildEnemies)
                    {
                        if (enemy == null) continue;
                        if (PlayerInventory.Instance.GetRecruitedCount(enemy.enemyName) < PlayerInventory.Instance.GetAvailableRecruitLimit(enemy))
                        {
                            canEncounterAnyWildEnemy = true;
                            break;
                        }
                    }
                }
            }

            if (canEncounterAnyWildEnemy)
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
        }

        // Animator:
        if (hasIsMovingParam) anim.SetBool(IsMovingHash, showMovingAnim);

        // Per al MOVE (walk): usa el moviment real (4 direccions)
        Vector2 moveAnimDir = Vector2.zero;
        if (isMovingInput)
        {
            if (Mathf.Abs(input.y) >= Mathf.Abs(input.x))
                moveAnimDir = new Vector2(0, Mathf.Sign(input.y));
            else
                moveAnimDir = new Vector2(Mathf.Sign(input.x), 0);
        }
        if (hasMoveXParam) anim.SetFloat(MoveXHash, moveAnimDir.x);
        if (hasMoveYParam) anim.SetFloat(MoveYHash, moveAnimDir.y);

        // Per a IDLE: sempre l’última direcció coneguda
        if (hasLastMoveXParam) anim.SetFloat(LastMoveXHash, lastDir.x);
        if (hasLastMoveYParam) anim.SetFloat(LastMoveYHash, lastDir.y);

        // Multiplicador d'animació segons proporció run/walk
        if (hasSpeedMulParam)
        {
            float ratio = (walkSpeed <= 0.0001f) ? 1f : (runSpeed / walkSpeed);
            anim.SetFloat(SpeedMulHash, isRunning ? ratio : 1f);
        }
    }

    private void TriggerRandomEncounter()
    {
        Debug.Log("WILD ENCOUNTER TRIGGERED!");
        LockMovement(); 
        
        if (hasIsMovingParam) anim.SetBool(IsMovingHash, false);
        if (hasMoveXParam) anim.SetFloat(MoveXHash, 0);
        if (hasMoveYParam) anim.SetFloat(MoveYHash, 0);
        if (hasSpeedMulParam) anim.SetFloat(SpeedMulHash, 1f);

        System.Collections.Generic.List<EnemyProfile> validEnemies = new System.Collections.Generic.List<EnemyProfile>();
        foreach (var enemy in wildEnemies)
        {
            if (PlayerInventory.Instance == null || PlayerInventory.Instance.GetRecruitedCount(enemy.enemyName) < PlayerInventory.Instance.GetAvailableRecruitLimit(enemy))
            {
                validEnemies.Add(enemy);
            }
        }

        if (validEnemies.Count == 0)
        {
            UnlockMovement();
            return;
        }

        EnemyProfile chosenEnemy = validEnemies[Random.Range(0, validEnemies.Count)];
        CombatEncounter enc = new CombatEncounter();
        enc.enemyProfile = chosenEnemy;
        
        StartCoroutine(AlertAndStartCombat(enc));
    }

    private IEnumerator AlertAndStartCombat(CombatEncounter enc)
    {
        // 1) Reproduir so d'alerta
        if (alertSound != null)
        {
            var tempGO = new GameObject("TempAudio");
            var src = tempGO.AddComponent<AudioSource>();
            src.clip = alertSound;
            src.Play();
            Destroy(tempGO, alertSound.length + 0.1f);
        }

        // 2) Mostrar exclamació groga
        yield return ShowAlertEffect();

        // 3) Esperar mig segon (l'efecte ja triga una mica)
        yield return new WaitForSeconds(0.5f);

        // 4) Començar combat
        combatLoader.StartCombat(enc);
    }

    private IEnumerator ShowAlertEffect()
    {
        // Creem un Canvas Overlay temporal perquè l'exclamació es vegi PER SOBRE de tot
        var alertGO = new GameObject("AlertCanvas");
        var canvas = alertGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;
        alertGO.AddComponent<CanvasScaler>();

        // Contenidor del text
        var txtGO = new GameObject("AlertText");
        txtGO.transform.SetParent(alertGO.transform, false);
        var rt = txtGO.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(200f, 120f);

        var txt = txtGO.AddComponent<TextMeshProUGUI>();
        txt.text = "!";
        txt.fontSize = 110f;
        txt.color = new Color(0.95f, 0.8f, 0.15f); // Groc
        txt.alignment = TextAlignmentOptions.Center;
        txt.overflowMode = TextOverflowModes.Overflow;
        txt.textWrappingMode = TextWrappingModes.NoWrap;

        // Font (mateixa que diàlegs)
        if (cachedAlertFont == null)
        {
#if UNITY_EDITOR
            cachedAlertFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/8bitoperator_jve SDF.asset") 
                ?? UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/8bitoperator_jve SDF.asset");
#endif
            if (cachedAlertFont == null) cachedAlertFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/8bitoperator_jve SDF");
            if (cachedAlertFont == null) cachedAlertFont = Resources.Load<TMP_FontAsset>("Fonts/8bitoperator_jve SDF") ?? Resources.Load<TMP_FontAsset>("8bitoperator_jve SDF");
        }
        if (cachedAlertFont != null) txt.font = cachedAlertFont;

        // Outline negre natiu de TMP
        txt.fontSharedMaterial = Instantiate(txt.fontSharedMaterial);
        txt.fontSharedMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.35f);
        txt.fontSharedMaterial.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0f, 0f, 0f, 1f));

        txt.ForceMeshUpdate();

        // Convertir posició del jugador al món → posició de pantalla
        Camera cam = Camera.main;

        // Animació pop-in (escala)
        txtGO.transform.localScale = Vector3.zero;
        float elapsed = 0f;
        float dur = 0.15f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            float scale = t < 0.7f ? Mathf.Lerp(0f, 1.3f, t / 0.7f) : Mathf.Lerp(1.3f, 1f, (t - 0.7f) / 0.3f);
            txtGO.transform.localScale = Vector3.one * scale;

            // Seguim la posició del jugador cada frame
            if (cam != null)
            {
                Vector3 currentWorldPos = (playerTransform != null ? playerTransform.position : transform.position) + Vector3.up * 0.8f;
                rt.position = cam.WorldToScreenPoint(currentWorldPos);
            }

            yield return null;
        }
        txtGO.transform.localScale = Vector3.one;

        // Espera addicional continuant l'actualització de la posició
        float waitElapsed = 0f;
        while (waitElapsed < 0.3f)
        {
            waitElapsed += Time.deltaTime;
            if (cam != null)
            {
                Vector3 currentWorldPos = (playerTransform != null ? playerTransform.position : transform.position) + Vector3.up * 0.8f;
                rt.position = cam.WorldToScreenPoint(currentWorldPos);
            }
            yield return null;
        }

        Destroy(alertGO);
    }

    private void FixedUpdate()
    {
        if (movementLocked) return;

        float currentSpeed = isRunning ? runSpeed : walkSpeed;
        rb.MovePosition(rb.position + moveDir * currentSpeed * Time.fixedDeltaTime);
    }
    
    public void SetEncountersState(bool state)
    {
        disableEncounters = !state;
        if (state)
        {
            distanceWalkedSinceEncounter = 0f;
        }
    }
}
