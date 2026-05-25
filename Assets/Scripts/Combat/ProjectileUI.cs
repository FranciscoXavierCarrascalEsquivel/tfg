using UnityEngine;

public class ProjectileUI : MonoBehaviour
{
    [SerializeField] private Transform visualRoot;
    [SerializeField] private float speed = 500f;

    private RectTransform rt;
    private Vector2 dir;

    public static int activeProjectiles = 0;

    private CombatManager cm;

    private bool isParried = false;

    // Moviment Zig-Zag (opcional)
    private float sinFreq = 0f;
    private float sinAmp = 0f;
    private float sinElapsed = 0f;
    private bool isSpinning = false;
    private float rotationSpeed = 360f;
    private bool isRed = false;

    // Homing settings
    private Transform homingTarget;
    private float homingStrength = 0.8f;

    private void Start() => activeProjectiles++;
    private void OnDestroy() 
    {
        if (!isParried) activeProjectiles--;
    }

    private void Awake() 
    {
        rt = GetComponent<RectTransform>();
        cm = FindFirstObjectByType<CombatManager>();
    }

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
            if (img != null) img.color = Color.red;
        }
    }

    public void SetHoming(Transform target, float strength = 1.2f)
    {
        homingTarget = target;
        homingStrength = strength;
    }

    private void Update()
    {
        if (!rt) return;

        // Si tenim un objectiu (Homing), actualitzem la direcció constantment
        if (homingTarget != null)
        {
            Vector2 targetPos = homingTarget.position;
            Vector2 currentPos = transform.position;
            Vector2 targetDir = (targetPos - currentPos).normalized;
            
            // Lerpejem la direcció perque el gir no sigui infinitament instantani (més orgànic)
            dir = Vector2.Lerp(dir, targetDir, Time.deltaTime * homingStrength).normalized;

            // Restringim l'angle: No pot anar més de 45 graus cap als costats respecte a la vertical (cap avall)
            // L'interval de seguretat és [-135, -45] graus, on -90 és recte cap avall.
            float targetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            targetAngle = Mathf.Clamp(targetAngle, -135f, -45f);
            float rad = targetAngle * Mathf.Deg2Rad;
            dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)).normalized;
        }
        
        Vector2 movement = dir * speed * Time.deltaTime;
        if (sinAmp > 0f)
        {
            sinElapsed += Time.deltaTime;
            // Vector perpendicular per fer la onada lateralment a la direcció de vol
            Vector2 perp = new Vector2(-dir.y, dir.x);
            movement += perp * Mathf.Cos(sinElapsed * sinFreq) * sinAmp * Time.deltaTime * sinFreq;
        }

        rt.anchoredPosition += movement;

        if (isSpinning)
        {
            Transform targetToRotate = visualRoot != null ? visualRoot : rt;
            targetToRotate.Rotate(0, 0, rotationSpeed * Time.deltaTime);
        }

        // Si el projectil passa la línia per sota (killY), vol dir que ha impactat el jugador = RETALL DE VIDA
        float killY = cm ? cm.GetDestroyLimitY() : -1200f;

        if (rt.anchoredPosition.y < killY)
        {
            if (cm && !isRed && !cm.IsEnded)
            {
                if (!cm.IsPlayerImmune())
                {
                    cm.PlayerTakeDamage(10);
                    cm.TriggerGlobalImmunity(1f);
                }
            }
            Destroy(gameObject);
        }
        // Si el projectil marxa fora de la pantalla per dalt o pels costats, NO fa mal, simplement es destrueix (neteja)
        else if (rt.anchoredPosition.y > 1200 || rt.anchoredPosition.x < -2000 || rt.anchoredPosition.x > 2000)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isParried) return;
        
        // Millora: Només fem parry si la col·lisió és amb l'objecte que té el script ParryZone
        // Aixo evita que hitboxs de moviment o llimits de la ma detectin el parry per error.
        if (collision.GetComponent<ParryZone>() != null)
        {
            var cm = FindFirstObjectByType<CombatManager>();
            if (cm) 
            {
                HandController hand = collision.GetComponentInParent<HandController>();
                if (hand == null) hand = collision.GetComponent<HandController>();

                if (isRed)
                {
                    if (hand != null && hand.IsImmune)
                    {
                        Destroy(gameObject);
                        return;
                    }

                    // Els vermells només resten vida si els toques (parry)
                    if (!cm.IsEnded)
                    {
                        if (cm.DamageSound) cm.PlayLocalSound(cm.DamageSound);
                        cm.PlayerTakeDamage(10);
                        
                        if (hand != null)
                        {
                            hand.TriggerImmunity(1f);
                        }
                    }
                    Destroy(gameObject); 
                    return; 
                }

                var img = GetComponent<UnityEngine.UI.Image>();
                Sprite s = img ? img.sprite : null;
                cm.OnParrySuccess(transform.position, s);
                
                // Efecte de destruccio en pixels verd (perque curam)
                Color tint = new Color(0.3f, 1f, 0.3f, 1f);
                EnemyDestroyFX.Play(img, () => Destroy(gameObject), tint);
            }

            // PARRY EXITÓS
            isParried = true;
            activeProjectiles--; // ho restem ara per no allargar el torn enemic 2 segons
            
            enabled = false; // perque deixi de fer Update i no faci mal
            var collider = GetComponent<Collider2D>();
            if (collider) collider.enabled = false; // perque no torni a col·lisionar
        }
    }
}
