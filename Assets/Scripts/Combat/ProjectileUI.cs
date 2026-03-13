using UnityEngine;

public class ProjectileUI : MonoBehaviour
{
    [SerializeField] private float speed = 500f;

    private RectTransform rt;
    private Vector2 dir;

    public static int activeProjectiles = 0;

    private CombatManager cm;

    private void Start() => activeProjectiles++;
    private void OnDestroy() => activeProjectiles--;

    private void Awake() 
    {
        rt = GetComponent<RectTransform>();
        cm = FindFirstObjectByType<CombatManager>();
    }

    public void Init(Vector2 direction, float speedOverride = -1f) 
    { 
        dir = direction.normalized; 
        if (speedOverride > 0f) speed = speedOverride; 
    }

    private void Update()
    {
        if (!rt) return;
        rt.anchoredPosition += dir * speed * Time.deltaTime;

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
        // Si xoquem amb qualsevol objecte que provingui de la Mà (el Parry Zone)
        if (collision.GetComponentInParent<HandController>() != null)
        {
            var cm = FindFirstObjectByType<CombatManager>();
            if (cm) 
            {
                cm.PlayParrySound();
                var img = GetComponent<UnityEngine.UI.Image>();
                cm.SpawnParryEffect(transform.position, img ? img.sprite : null);
            }

            // PARRY EXITÓS (El projectil desapareix i no resta vida al sortir de pantalla)
            Destroy(gameObject);
        }
    }
}
