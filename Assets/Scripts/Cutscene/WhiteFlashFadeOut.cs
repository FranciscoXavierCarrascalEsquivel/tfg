using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class WhiteFlashFadeOut : MonoBehaviour
{
    public float waitBeforeFade = 0.5f;
    public float fadeOutDuration = 2f;

    private AudioSource[] allMusicSources;
    private float[] targetVolumes;

    private void Awake()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // En carregar la nova escena, busquem TOTS els AudioSources que siguin música de fons (loop o playOnAwake)
        var sources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        
        System.Collections.Generic.List<AudioSource> validSources = new System.Collections.Generic.List<AudioSource>();
        System.Collections.Generic.List<float> validVolumes = new System.Collections.Generic.List<float>();
        
        foreach (var src in sources)
        {
            if (src.playOnAwake || src.loop)
            {
                validVolumes.Add(src.volume);
                src.volume = 0f; // Emmudim a l'instant
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
                
                // 1. Imatge es torna transparent
                img.color = new Color(1f, 1f, 1f, 1f - t);
                
                // 2. Totes les músiques pugen de volum simultàniament
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
            
            // Assegurem el volum final
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
        
        Destroy(gameObject);
    }
}
