using UnityEngine;

/// <summary>
/// Singleton persistent que reprodueix els sons d'ús d'objectes.
/// S'auto-crea quan es necessita (no cal afegir-lo manualment a la escena).
/// </summary>
public class ItemSoundPlayer : MonoBehaviour
{
    public static ItemSoundPlayer Instance { get; private set; }

    private AudioSource audioSource;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    /// <summary>
    /// Obté (o crea) la instància, i reprodueix el clip indicat.
    /// </summary>
    public static void Play(AudioClip clip)
    {
        if (clip == null) return;

        if (Instance == null)
        {
            var go = new GameObject("ItemSoundPlayer");
            go.AddComponent<ItemSoundPlayer>();
        }

        Instance.audioSource.PlayOneShot(clip);
    }
}
