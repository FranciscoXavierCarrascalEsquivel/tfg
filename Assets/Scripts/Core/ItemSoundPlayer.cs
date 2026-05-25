using UnityEngine;

/// <summary>
/// Reproductor persistent de sons d'interfície i d'ús d'objectes (ItemSoundPlayer).
/// Funciona com a patró Singleton que s'auto-instancia de forma mandrosa (Lazy Initialization).
/// Si un altre script intenta reproduir un so de sobtat i el reproductor no existeix,
/// aquest es genera automàticament a la jerarquia de Unity en estat persistent (DontDestroyOnLoad),
/// evitant la necessitat de pre-assignar-lo manualment a les escenes des de l'Inspector.
/// </summary>
public class ItemSoundPlayer : MonoBehaviour
{
    // Instància estàtica per a l'accés global
    public static ItemSoundPlayer Instance { get; private set; }

    private AudioSource audioSource; // Component emissor de so intern

    private void Awake()
    {
        // Control de patró Singleton per evitar duplicats en carregar escenes
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject); // Evita que es destrueixi en carregar o descarregar zones
        
        // Creem i configurem el component AudioSource per a efectes puntuals (SFX)
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // Mode 2D global
    }

    /// <summary>
    /// Reprodueix immediatament un efecte de so de forma no col·lisionadora (PlayOneShot).
    /// </summary>
    /// <param name="clip">Clip d'àudio a reproduir.</param>
    public static void Play(AudioClip clip)
    {
        if (clip == null) return;

        // Auto-instanciació de seguretat si és la primera vegada que es crida
        if (Instance == null)
        {
            var go = new GameObject("ItemSoundPlayer");
            go.AddComponent<ItemSoundPlayer>();
        }

        // Reprodueix el so solapant-se de forma neta amb altres efectes sonors actius
        Instance.audioSource.PlayOneShot(clip);
    }
}
