using UnityEngine;

/// <summary>
/// Sensor de Parada de Projectils en Combat (ParryHitbox).
/// Aquest component s'acobla al trigger de col·lisió de parry instal·lat a la mà.
/// El seu únic propòsit és rebre la col·lisió física de bales de tipus trigger (`Collider2D`),
/// validar si pertanyen a un projectil enemic, i notificar a la mà o al gestor per concedir
/// punts d'amistat/energia social (`powerGain`).
/// 
/// LÒGICA DEL PARADE DEL TFG:
/// - **Col·lisió de contacte**: Mitjançant l'API `OnTriggerEnter2D`, intercepta els projectils.
/// - **Alliberament desacoblat**: No destrueix directament el projectil, sinó que invoca
///   el delegat `OnParry` perquè el component del projectil o de la partícula visual iniciï
///   la seva pròpia coreografia de desintegració, evitant salts bruscs o desaparicions tallades.
/// </summary>
public class ParryHitbox : MonoBehaviour
{
    public System.Action<int> OnParry; // Delegat per notificar la parada amb èxit

    [SerializeField] private int powerGain = 10; // Energia concedida al jugador per cada parada correcta

    private void OnTriggerEnter2D(Collider2D other)
    {
        var proj = other.GetComponent<ProjectileUI>();
        if (proj == null) return; // Si la col·lisió no és amb una bala del combat, la ignorem

        // Informem de la parada atorgant l'energia obtinguda
        OnParry?.Invoke(powerGain);
        
        // Didàctic TFG: S'ha eliminat la crida directa a Destroy(proj.gameObject) d'aquesta classe.
        // Ara la bala detecta l'esdeveniment i és la pròpia classe ProjectileUI.cs qui s'encarrega
        // d'activar l'efecte de desintegració pixelat de forma autònoma i neta.
    }
}
