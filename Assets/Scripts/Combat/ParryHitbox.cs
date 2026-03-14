using UnityEngine;

public class ParryHitbox : MonoBehaviour
{
    public System.Action<int> OnParry;
    [SerializeField] private int powerGain = 10;

    private void OnTriggerEnter2D(Collider2D other)
    {
        var proj = other.GetComponent<ProjectileUI>();
        if (proj == null) return;

        OnParry?.Invoke(powerGain);
        
        // ELIMINAT: Destroy(proj.gameObject);
        // Ara ProjectileUI.cs s'encarrega d'executar l'animació de desintegració
        // i es destrueix a si mateix en acabar via callback de EnemyDestroyFX.
    }
}
