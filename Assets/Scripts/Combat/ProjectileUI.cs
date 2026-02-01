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
    }
}
