using UnityEngine;

/// <summary>
/// Trigger 2D encarregat d'activar o desactivar els combats aleatoris quan el jugador el trepitja.
/// Útil per a delimitar zones segures (com ara pobles o interiors de cases) de les zones de perill.
/// </summary>
public class EncounterToggleTrigger : MonoBehaviour
{
    [Tooltip("Si es marca, al trepitjar el trigger s'activaran els combats aleatoris. Si es desmarca, es desactivaran.")]
    public bool enableEncounters = false;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Comprovem si el cos físic que entra al trigger correspon al Jugador
        if (collision.CompareTag("Player"))
        {
            PlayerController2D pController = collision.GetComponent<PlayerController2D>();
            if (pController != null)
            {
                // Canviem l'estat d'activació dels combats aleatoris en el controlador del jugador
                pController.SetEncountersState(enableEncounters);
            }
        }
    }
}
