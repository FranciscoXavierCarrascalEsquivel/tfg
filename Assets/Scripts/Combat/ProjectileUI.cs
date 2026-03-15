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

    public void Init(Vector2 direction, float speedOverride = -1f, float zigzagFreq = 0f, float zigzagAmp = 0f, bool spinning = false) 
    { 
        dir = direction.normalized; 
        if (speedOverride > 0f) speed = speedOverride; 
        sinFreq = zigzagFreq;
        sinAmp = zigzagAmp;
        isSpinning = spinning;
    }

    private void Update()
    {
        if (!rt) return;
        
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
            if (cm) cm.PlayerTakeDamage(10);
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
        // Millora: Només fem parry si la col·lisió és amb l'objecte que té el script ParryZone
        // Aixo evita que hitboxs de moviment o llimits de la ma detectin el parry per error.
        if (collision.GetComponent<ParryZone>() != null)
        {
            var cm = FindFirstObjectByType<CombatManager>();
            if (cm) 
            {
                var img = GetComponent<UnityEngine.UI.Image>();
                Sprite s = img ? img.sprite : null;
                cm.OnParrySuccess(transform.position, s);
                
                // Efecte de destruccio en pixels (Verd si estem defensant)
                Color tint = cm.IsDefending ? new Color(0.3f, 1f, 0.3f, 1f) : Color.white;
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
