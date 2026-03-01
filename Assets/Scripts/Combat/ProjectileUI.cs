using UnityEngine;

public class ProjectileUI : MonoBehaviour
{
    [SerializeField] private float speed = 500f;

    private RectTransform rt;
    private Vector2 dir;

    private void Awake() => rt = GetComponent<RectTransform>();

    public void Init(Vector2 direction) => dir = direction.normalized;

    private void Update()
    {
        if (!rt) return;
        rt.anchoredPosition += dir * speed * Time.deltaTime;

        // Check if out of bounds (bottom)
        // Assuming canvas is ScreenSpaceOverlay or similar, and using anchoredPosition.
        // A simple check: if y < -Screen.height/2 (approx) or a fixed threshold.
        // Let's use a safe threshold like -600 or check parent rect.
        if (rt.anchoredPosition.y < -600)
        {
            var cm = FindFirstObjectByType<CombatManager>();
            if (cm) cm.PlayerTakeDamage(10);
            Destroy(gameObject);
        }
    }
}
