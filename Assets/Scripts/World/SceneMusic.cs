using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class SceneMusic : MonoBehaviour
{
    [Header("Music Settings")]
    [SerializeField] private AudioClip musicClip;
    
    [Tooltip("Si és true, la música començarà automàticament en iniciar l'escena")]
    [SerializeField] private bool playOnStart = true;
    
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.7f;
    
    [Header("Trigger Settings")]
    [Tooltip("Si és true, aquest component actuarà com a trigger per parar la música")]
    [SerializeField] private bool useAsTrigger = false;
    
    [Tooltip("Tag de l'objecte que ha de tocar el trigger (normalment 'Player')")]
    [SerializeField] private string triggerTag = "Player";
    
    [Tooltip("Si és true, la música es reprendrà quan l'objecte surti del trigger")]
    [SerializeField] private bool resumeOnExit = false;
    
    private AudioSource audioSource;

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

    private void SetupAudioSource()
    {
        audioSource.playOnAwake = false;
        audioSource.loop = true;           // Música en bucle
        audioSource.spatialBlend = 0f;     // 2D (no espacial)
        audioSource.volume = volume;
    }

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

    public void StopMusic()
    {
        audioSource.Stop();
    }

    public void SetVolume(float newVolume)
    {
        volume = Mathf.Clamp01(newVolume);
        audioSource.volume = volume;
    }

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
