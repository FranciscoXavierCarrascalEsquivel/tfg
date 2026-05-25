using System.Collections;
using UnityEngine;

/// <summary>
/// Gestiona la reproducció de música d'ambient en una escena o mapa.
/// Utilitza un component AudioSource per a reproduir àudio 2D en bucle.
/// Proporciona mètodes per fer transicions suaus (FadeIn i FadeOut), canviar de pista
/// i, de manera opcional, funcionar com a trigger físic per aturar/reprendre la música
/// quan el jugador passa per una determinada regió del mapa.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class SceneMusic : MonoBehaviour
{
    [Header("Configuració de Música")]
    [SerializeField] private AudioClip musicClip; // Clip d'àudio que es reproduirà
    
    [Tooltip("Si és true, la música començarà automàticament en iniciar l'escena")]
    [SerializeField] private bool playOnStart = true;
    
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.7f; // Volum normal/objectiu de la música
    
    [Header("Configuració de Trigger (Opcional)")]
    [Tooltip("Si és true, aquest component actuarà com a trigger per parar la música")]
    [SerializeField] private bool useAsTrigger = false;
    
    [Tooltip("Tag de l'objecte que ha de tocar el trigger (normalment 'Player')")]
    [SerializeField] private string triggerTag = "Player";
    
    [Tooltip("Si és true, la música es reprendrà quan l'objecte surti del trigger")]
    [SerializeField] private bool resumeOnExit = false;
    
    private AudioSource audioSource; // Component intern que emet l'àudio

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        SetupAudioSource();
    }

    private void Start()
    {
        if (playOnStart && musicClip != null)
        {
            PlayMusic();
        }
    }

    /// <summary>
    /// Configura les propietats inicials de l'AudioSource per a assegurar un format de pista de fons.
    /// </summary>
    private void SetupAudioSource()
    {
        audioSource.playOnAwake = false;
        audioSource.loop = true;           // Sempre reproduïm música d'ambient en bucle
        audioSource.spatialBlend = 0f;     // Mode 2D (el volum és homogeni a tota la pantalla)
        audioSource.volume = volume;
    }

    /// <summary>
    /// Inicia la reproducció immediata de la pista de música assignada.
    /// </summary>
    public void PlayMusic()
    {
        if (musicClip == null)
        {
            Debug.LogWarning("No hi ha cap AudioClip assignat a SceneMusic!");
            return;
        }

        audioSource.clip = musicClip;
        audioSource.Play();
    }

    /// <summary>
    /// Atura completament la reproducció musical activa.
    /// </summary>
    public void StopMusic()
    {
        audioSource.Stop();
    }

    /// <summary>
    /// Intercanvia la cançó actual per una de nova, i prepara el volum a 0 per a fer posteriorment un efecte de FadeIn.
    /// </summary>
    /// <param name="clip">Nou clip d'àudio.</param>
    /// <param name="targetVolume">Volum final desitjat (si és negatiu, mantindrà el preestablert).</param>
    public void ChangeClip(AudioClip clip, float targetVolume = -1f)
    {
        audioSource.Stop();
        musicClip = clip;
        audioSource.clip = clip;
        if (targetVolume >= 0f) volume = Mathf.Clamp01(targetVolume);
        audioSource.volume = 0f;
        audioSource.Play();
    }

    /// <summary>
    /// Modifica instantàniament el volum de l'AudioSource de forma controlada.
    /// </summary>
    public void SetVolume(float newVolume)
    {
        volume = Mathf.Clamp01(newVolume);
        audioSource.volume = volume;
    }

    /// <summary>
    /// Corrutina per a disminuir gradualment el volum fins a 0 (FadeOut) i pausar la reproducció.
    /// </summary>
    /// <param name="duration">Durada en segons de la transició.</param>
    public IEnumerator FadeOutAndPause(float duration)
    {
        float startVol = audioSource.volume;
        float time = 0;
        
        while (time < duration)
        {
            time += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(startVol, 0f, time / duration);
            yield return null;
        }

        audioSource.volume = 0f;
        audioSource.Pause();
    }

    /// <summary>
    /// Corrutina per reprendre l'àudio (UnPause) i augmentar progressivament el volum fins al nivell normal (FadeIn).
    /// </summary>
    /// <param name="duration">Durada en segons de la transició.</param>
    public IEnumerator FadeInAndResume(float duration)
    {
        audioSource.UnPause();
        float time = 0;
        
        while (time < duration)
        {
            time += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(0f, volume, time / duration);
            yield return null;
        }

        audioSource.volume = volume;
    }

    // =========================================================================
    // DETECCIONS DE TRIGGER (Opcional)
    // =========================================================================

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!useAsTrigger) return;
        
        if (other.CompareTag(triggerTag))
        {
            StopMusic();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!useAsTrigger) return;
        
        if (resumeOnExit && other.CompareTag(triggerTag))
        {
            PlayMusic();
        }
    }
}
