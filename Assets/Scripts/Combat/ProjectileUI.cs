using UnityEngine;

/// <summary>
/// Lògica Física i Animació de Balas/Projectils (ProjectileUI).
/// Aquest component és el responsable de controlar el moviment físic i col·lisió de cada bala
/// que llança l'enemic en la fase de "defensa". El projectil es mou procedimentalment de forma lineal,
/// en ones de zig-zag o mitjançant algoritmes de seguiment dinàmic (Homing), podent-se esquivar o aturar (Parry).
/// 
/// DISSENY I INTEGRACIÓ DE BALES DEL TFG:
/// - **Comportament de navegació variat**:
///   - *Lineal*: Moviment en línia recta segons direcció.
///   - *Zig-Zag*: Modulació de posició perpendicular utilitzant la funció matemàtica Cosinus.
///   - *Rotatori (Spinning)*: Giro continu de l'sprite visual per simular un ganivet o meteor.
///   - *Seguiment intel·ligent (Homing)*: Segueix de forma asíncrona la mà del jugador per ràtio
///     de suavitzat Lerp, limitant l'angle a un arc de 45º cap avall per a un disseny de joc net i just.
/// - **Gestió de cues de torns**: Incrementa/decrementa el comptador estàtic de bales actives
///   (`activeProjectiles`) per evitar que el CombatManager passi el torn fins a no quedar cap perill en pantalla.
/// - **Bales Vermelles (Red Projectiles)**: Patró especial que té prohibit el Parry.
///   Si el jugador intenta blocar una d'aquestes bales, rep dany a l'acte de forma repressiva.
/// - **Efecte de desintegració procedimental**: En realitzar un parry correcte sobre una bala estàndard,
///   s'invoca el component asíncron `EnemyDestroyFX` per esmicolar de forma dinàmica l'sprite en píxels de color verd.
/// </summary>
public class ProjectileUI : MonoBehaviour
{
    [SerializeField] private Transform visualRoot; // Contenidor visual de l'sprite (permet rotar-lo sense trencar les coordenades del pare)
    [SerializeField] private float speed = 500f; // Velocitat lineal base

    private RectTransform rt;
    private Vector2 dir; // Vector de direcció normalitzat

    public static int activeProjectiles = 0; // Comptador estàtic d'elements per al control del torn enemic

    private CombatManager cm;

    private bool isParried = false; // Flag per evitar col·lisions redundants un cop parat

    // ── PARÀMETRES DE ZIG-ZAG ──
    private float sinFreq = 0f;
    private float sinAmp = 0f;
    private float sinElapsed = 0f;
    private bool isSpinning = false;
    private float rotationSpeed = 360f;
    private bool isRed = false; // Flag per a atacs especials de bloqueig prohibit

    // ── PARÀMETRES DE SEGUIMENT INTEL·LIGENT (HOMING) ──
    private Transform homingTarget; // Ref de transform a perseguir
    private float homingStrength = 0.8f; // Velocitat de gir del seguiment

    private void Start() => activeProjectiles++; // Incrementem en néixer el projectil
    private void OnDestroy() 
    {
        // Decrementem només si no s'ha restat en rebre el Parry, per evitar comptatges negatius
        if (!isParried) activeProjectiles--;
    }

    private void Awake() 
    {
        rt = GetComponent<RectTransform>();
        cm = FindFirstObjectByType<CombatManager>();
    }

    /// <summary>
    /// Configura les propietats físiques i visuals inicials disparat des del spawner d'atacs.
    /// </summary>
    public void Init(Vector2 direction, float speedOverride = -1f, float zigzagFreq = 0f, float zigzagAmp = 0f, bool spinning = false, bool red = false) 
    { 
        dir = direction.normalized; 
        if (speedOverride > 0f) speed = speedOverride; 
        sinFreq = zigzagFreq;
        sinAmp = zigzagAmp;
        isSpinning = spinning;
        isRed = red;

        if (isRed)
        {
            var img = GetComponent<UnityEngine.UI.Image>();
            if (img != null) img.color = Color.red; // Tintem en vermell per indicar perill absolut
        }
    }

    /// <summary>
    /// Enllaca el transform de la mà del jugador per engegar la persecució.
    /// </summary>
    public void SetHoming(Transform target, float strength = 1.2f)
    {
        homingTarget = target;
        homingStrength = strength;
    }

    private void Update()
    {
        if (!rt) return;

        // ── 1. CÀLCUL DE SEGUIMENT INTEL·LIGENT (HOMING) ──
        if (homingTarget != null)
        {
            Vector2 targetPos = homingTarget.position;
            Vector2 currentPos = transform.position;
            Vector2 targetDir = (targetPos - currentPos).normalized;
            
            // Lerpejem la direcció actual cap a l'objectiu de forma molt orgànica
            dir = Vector2.Lerp(dir, targetDir, Time.deltaTime * homingStrength).normalized;

            // Restringim l'angle d'atac: no permetem girs infinits que facin l'atac inesquivable.
            // Limitem el rumb perquè es dirigeixi cap avall amb petits angles laterals d'esquena a la mà [-135º, -45º].
            float targetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            targetAngle = Mathf.Clamp(targetAngle, -135f, -45f);
            float rad = targetAngle * Mathf.Deg2Rad;
            dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)).normalized;
        }
        
        // ── 2. CÀLCUL DEL MOVIMENT FÍSIC BASE ──
        Vector2 movement = dir * speed * Time.deltaTime;
        
        // ── 3. CÀLCUL D'ONES DE ZIG-ZAG ADAPTATIVES ──
        if (sinAmp > 0f)
        {
            sinElapsed += Time.deltaTime;
            // Vector de direcció perpendicular per modular l'ona lateralment respecte a la trajectòria de vol
            Vector2 perp = new Vector2(-dir.y, dir.x);
            movement += perp * Mathf.Cos(sinElapsed * sinFreq) * sinAmp * Time.deltaTime * sinFreq;
        }

        rt.anchoredPosition += movement;

        // ── 4. ROTACIÓ DE L'AVATAR (EFECTE GANIVET / METEOR) ──
        if (isSpinning)
        {
            Transform targetToRotate = visualRoot != null ? visualRoot : rt;
            targetToRotate.Rotate(0, 0, rotationSpeed * Time.deltaTime);
        }

        // ── 5. DETECEIÓ D'IMPACTE CONTRA EL JUGADOR (LÍMIT HORITZONTAL SUPERAT) ──
        float killY = cm ? cm.GetDestroyLimitY() : -1200f;

        if (rt.anchoredPosition.y < killY)
        {
            // Si supera el llindar per sota, vol dir que col·lisiona amb el fons = DAMAGE
            if (cm && !isRed && !cm.IsEnded)
            {
                if (!cm.IsPlayerImmune())
                {
                    cm.PlayerTakeDamage(10); // Restem 10 de vida
                    cm.TriggerGlobalImmunity(1f); // Micro-segons de seguretat
                }
            }
            Destroy(gameObject); // Alliberament
        }
        // Si el projectil marxa pels costats o dalt per esquives extremes del jugador, s'allibera sense danyar
        else if (rt.anchoredPosition.y > 1200 || rt.anchoredPosition.x < -2000 || rt.anchoredPosition.x > 2000)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Interceptació de la col·lisió de trigger contra els detectors de la mà.
    /// Distingeix si s'atura l'atac de forma triomfal o si és vermell prohibit.
    /// </summary>
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isParried) return;
        
        // Regla didàctica del TFG: només fem parry si impactem contra el script ParryZone (evita col·lisions absurdes amb parets)
        if (collision.GetComponent<ParryZone>() != null)
        {
            var cm = FindFirstObjectByType<CombatManager>();
            if (cm) 
            {
                HandController hand = collision.GetComponentInParent<HandController>();
                if (hand == null) hand = collision.GetComponent<HandController>();

                // ── CAS 1: PROJECTIL VERMELL (BLOQUEIG PROHIBIT) ──
                if (isRed)
                {
                    if (hand != null && hand.IsImmune)
                    {
                        Destroy(gameObject);
                        return; // Ignorem col·lisió si el jugador ja està parpellejant inmune
                    }

                    if (!cm.IsEnded)
                    {
                        if (cm.DamageSound) cm.PlayLocalSound(cm.DamageSound);
                        cm.PlayerTakeDamage(10);
                        
                        if (hand != null)
                        {
                            hand.TriggerImmunity(1f); // Disparem els frames de dany i la invulnerabilitat
                        }
                    }
                    Destroy(gameObject); 
                    return; 
                }

                // ── CAS 2: PROJECTIL ESTÀNDARD (PARRY TRIOMFAL) ──
                var img = GetComponent<UnityEngine.UI.Image>();
                Sprite s = img ? img.sprite : null;
                cm.OnParrySuccess(transform.position, s); // Llança el so de xoc celeste
                
                // Efecte procedimental de desintegració de píxels verds d'amistat
                Color tint = new Color(0.3f, 1f, 0.3f, 1f);
                EnemyDestroyFX.Play(img, () => Destroy(gameObject), tint);
            }

            // Marquem la col·lisió com a resolta
            isParried = true;
            activeProjectiles--; // Restem asíncronament ara perquè no calgui esperar que finalitzi l'animació de 2 segons
            
            enabled = false; // Desactivem el script per a no realitzar l'Update
            var collider = GetComponent<Collider2D>();
            if (collider) collider.enabled = false; // Tanquem el detector
        }
    }
}
