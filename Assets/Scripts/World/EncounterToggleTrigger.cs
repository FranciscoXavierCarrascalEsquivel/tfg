using UnityEngine;

public class EncounterToggleTrigger : MonoBehaviour
{
    [Tooltip("Si es marca, al trepitjar el trigger s'activaran els combats aleatoris. Si no, es desactivaran.")]
    public bool enableEncounters = false;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            PlayerController2D pController = collision.GetComponent<PlayerController2D>();
            if (pController != null)
            {
                pController.SetEncountersState(enableEncounters);
            }
        }
    }
}
