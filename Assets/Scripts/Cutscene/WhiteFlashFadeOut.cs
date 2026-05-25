using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Component de suport per a la transició d'explosió gràfica.
/// Aquest objecte sobreviu al canvi d'escena (gràcies a DontDestroyOnLoad gestionat pel creador),
/// s'acobla a l'esdeveniment sceneLoaded per a detectar quan s'ha completat la càrrega del següent mapa,
/// localitza de forma polimòrfica tots els AudioSources de tipus música de fons de la nova escena,
/// els emmudeix a l'instant, i posteriorment realitza un fos a transparent (FadeOut) de la imatge de flaix blanc
/// en paral·lel al FadeIn de totes les músiques ambientals fins a arribar als seus volums originals.
/// </summary>
public class WhiteFlashFadeOut : MonoBehaviour
{
    [Header("Temps de Transició")]
    public float waitBeforeFade = 0.5f; // Espera dramàtica en blanc pur en arrencar la nova escena
    public float fadeOutDuration = 2f;  // Durada de la fosa a transparent del blanc

    // Llistes per a governar el volum de les noves músiques de fons de l'escena de destí
    private AudioSource[] allMusicSources;
    private float[] targetVolumes;

    private void Awake()
    {
        // Ens subscrivim a l'esdeveniment global de Unity per saber quan es completa la càrrega de qualsevol escena
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        // Important: des-subscripció per evitar pèrdues de referència en memòria (Memory Leaks)
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>
    /// Callback del motor de Unity que s'executa automàticament en completar la càrrega d'una nova escena.
    /// Cerca i emmudeix immediatament els emissors musicals actius de fons.
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Trobem absolutament tots els emissors del mapa
        var sources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        
        System.Collections.Generic.List<AudioSource> validSources = new System.Collections.Generic.List<AudioSource>();
        System.Collections.Generic.List<float> validVolumes = new System.Collections.Generic.List<float>();
        
        foreach (var src in sources)
        {
            // Filtrem exclusivament els que estan dissenyats per sonar en bucle o per defecte
            if (src.playOnAwake || src.loop)
            {
                validVolumes.Add(src.volume);
                src.volume = 0f; // Emmudiment silenciós immediat
                validSources.Add(src);
            }
        }
        
        allMusicSources = validSources.ToArray();
        targetVolumes = validVolumes.ToArray();
    }

    private void Start()
    {
        StartCoroutine(FadeOutRoutine());
    }

    /// <summary>
    /// Corrutina d'aclariment de pantalla i restabliment gradual de so (FadeOut de flaix + FadeIn de BGM).
    /// </summary>
    private IEnumerator FadeOutRoutine()
    {
        yield return new WaitForSeconds(waitBeforeFade);

        Image img = GetComponentInChildren<Image>();
        if (img != null)
        {
            float elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeOutDuration;
                
                // 1. Aclarim progressivament el blanc fins a transparent
                img.color = new Color(1f, 1f, 1f, 1f - t);
                
                // 2. Augmentem el volum de forma proporcional per a cadascuna de les pistes trobades
                if (allMusicSources != null)
                {
                    for (int i = 0; i < allMusicSources.Length; i++)
                    {
                        if (allMusicSources[i] != null)
                        {
                            allMusicSources[i].volume = Mathf.Lerp(0f, targetVolumes[i], t);
                        }
                    }
                }

                yield return null;
            }
            
            // Assegurem el volum final exacte configurat originalment
            if (allMusicSources != null)
            {
                for (int i = 0; i < allMusicSources.Length; i++)
                {
                    if (allMusicSources[i] != null)
                    {
                        allMusicSources[i].volume = targetVolumes[i];
                    }
                }
            }
        }
        
        // Destruïm el Canvas de transició un cop ha finalitzat completament la seva feina
        Destroy(gameObject);
    }
}
