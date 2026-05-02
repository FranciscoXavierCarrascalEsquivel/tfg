using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class MusicChangeTrigger : MonoBehaviour
{
    [Header("Nova Música")]
    [Tooltip("La cançó que es reproduirà després del fade out.")]
    [SerializeField] private AudioClip newMusic;
    
    [Range(0f, 1f)]
    [SerializeField] private float newMusicVolume = 0.7f;

    [Header("Transició")]
    [Tooltip("Durada del fade out de la música actual (segons).")]
    [SerializeField] private float fadeOutDuration = 1.5f;

    [Tooltip("Durada del fade in de la nova música (segons).")]
    [SerializeField] private float fadeInDuration = 1.5f;

    [Header("Configuració")]
    [SerializeField] private bool triggerOnlyOnce = true;
    [SerializeField] private string playerTag = "Player";

    private bool hasTriggered;
    private bool isTransitioning;

    // AudioSource propi per la nova música
    private AudioSource newMusicSource;

    private void Awake()
    {
        newMusicSource = gameObject.AddComponent<AudioSource>();
        newMusicSource.playOnAwake = false;
        newMusicSource.loop = true;
        newMusicSource.spatialBlend = 0f;
        newMusicSource.volume = 0f;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (hasTriggered && triggerOnlyOnce) return;
        if (isTransitioning) return;
        if (!collision.CompareTag(playerTag)) return;

        hasTriggered = true;
        StartCoroutine(ChangeMusicRoutine());
    }

    private IEnumerator ChangeMusicRoutine()
    {
        isTransitioning = true;

        // 1) Busquem qualsevol font de música activa i li fem fade out
        
        Coroutine fadeOutRoutine = null;

        // A) Busquem si algun ALTRE MusicChangeTrigger està reproduint música
        var otherTriggers = FindObjectsByType<MusicChangeTrigger>(FindObjectsSortMode.None);
        foreach (var t in otherTriggers)
        {
            if (t != this && t.IsPlaying())
            {
                fadeOutRoutine = t.FadeOutAndStop(fadeOutDuration);
            }
        }

        // B) Si cap altre trigger estava sonant, busquem els reproductors generals
        var loopMusic = FindFirstObjectByType<TriggerMusicLoopSection2D>();
        var sceneMusic = FindFirstObjectByType<SceneMusic>();
        var combatLoader = FindFirstObjectByType<CombatLoader>();

        if (fadeOutRoutine == null)
        {
            if (loopMusic != null)
            {
                fadeOutRoutine = StartCoroutine(loopMusic.FadeOutAndStop(fadeOutDuration));
            }
            else if (sceneMusic != null)
            {
                fadeOutRoutine = StartCoroutine(sceneMusic.FadeOutAndPause(fadeOutDuration));
            }
            else if (combatLoader != null)
            {
                fadeOutRoutine = combatLoader.StopBackgroundMusicCoroutine(fadeOutDuration);
            }
        }


        // Esperem que acabi el fade out
        if (fadeOutRoutine != null)
            yield return fadeOutRoutine;

        // 2) Petit silenci
        yield return new WaitForSeconds(0.15f);

        // 3) Fade in de la nova música
        if (newMusic != null)
        {
            newMusicSource.clip = newMusic;
            newMusicSource.volume = 0f;
            newMusicSource.Play();

            float t = 0f;
            while (t < fadeInDuration)
            {
                t += Time.deltaTime;
                newMusicSource.volume = Mathf.Lerp(0f, newMusicVolume, Mathf.Clamp01(t / fadeInDuration));
                yield return null;
            }
            newMusicSource.volume = newMusicVolume;
        }

        isTransitioning = false;
    }

    public bool IsPlaying()
    {
        return newMusicSource != null && newMusicSource.isPlaying;
    }

    public Coroutine FadeOutAndStop(float duration)
    {
        if (IsPlaying())
        {
            return StartCoroutine(FadeOutRoutine(duration));
        }
        return null;
    }

    private IEnumerator FadeOutRoutine(float duration)
    {
        float startVol = newMusicSource.volume;
        float t = 0;
        while (t < duration)
        {
            t += Time.deltaTime;
            newMusicSource.volume = Mathf.Lerp(startVol, 0f, t / duration);
            yield return null;
        }
        newMusicSource.volume = 0f;
        newMusicSource.Stop();
    }
}
