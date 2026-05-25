using System.Collections;
using UnityEngine;

/// <summary>
/// Trigger 2D encarregat de realitzar una transició musical creuada (crossfade).
/// Quan el jugador hi passa pel damunt, cerca qualsevol reproductor actiu a l'escena
/// (un altre trigger, bucle de música d'escena o combat), hi aplica un efecte de FadeOut,
/// i posteriorment realitza un FadeIn gradual d'una nova pista de música assignada.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class MusicChangeTrigger : MonoBehaviour
{
    [Header("Configuració de la Nova Música")]
    [Tooltip("La cançó que es reproduirà després de silenciar la música anterior.")]
    [SerializeField] private AudioClip newMusic;
    
    [Range(0f, 1f)]
    [SerializeField] private float newMusicVolume = 0.7f; // Volum final per a la nova música

    [Header("Transició (Fades)")]
    [Tooltip("Durada del silenciament (fade out) de la música actual en segons.")]
    [SerializeField] private float fadeOutDuration = 1.5f;

    [Tooltip("Durada de l'augment gradual (fade in) de la nova música en segons.")]
    [SerializeField] private float fadeInDuration = 1.5f;

    [Header("Condicions de Trigger")]
    [SerializeField] private bool triggerOnlyOnce = true; // Si es marca, només es podrà activar una vegada per partida/escena
    [SerializeField] private string playerTag = "Player"; // Tag identificatiu de l'objecte que pot activar-lo

    private bool hasTriggered; // Estat intern de si ja ha estat trepitjat
    private bool isTransitioning; // Flag per evitar solapaments d'animacions d'àudio simultànies

    // Font d'àudio pròpia creada per codi per emetre el nou clip de manera aïllada
    private AudioSource newMusicSource;

    private void Awake()
    {
        // Generem i configurem un component AudioSource propi per a la cançó d'aquest trigger
        newMusicSource = gameObject.AddComponent<AudioSource>();
        newMusicSource.playOnAwake = false;
        newMusicSource.loop = true;
        newMusicSource.spatialBlend = 0f; // Mode 2D
        newMusicSource.volume = 0f; // Iniciem silenciats
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Proteccions generals d'activació
        if (hasTriggered && triggerOnlyOnce) return;
        if (isTransitioning) return;
        if (!collision.CompareTag(playerTag)) return;

        hasTriggered = true;
        StartCoroutine(ChangeMusicRoutine());
    }

    /// <summary>
    /// Corrutina seqüencial que cerca de forma polimòrfica les fonts actives i fa el canvi de música.
    /// </summary>
    private IEnumerator ChangeMusicRoutine()
    {
        isTransitioning = true;
        Coroutine fadeOutRoutine = null;

        // A) Busquem si algun ALTRE trigger de tipus MusicChangeTrigger s'està reproduint per apagar-lo
        var otherTriggers = FindObjectsByType<MusicChangeTrigger>(FindObjectsSortMode.None);
        foreach (var t in otherTriggers)
        {
            if (t != this && t.IsPlaying())
            {
                fadeOutRoutine = t.FadeOutAndStop(fadeOutDuration);
            }
        }

        // B) Si cap trigger anterior estava sonant, busquem els controladors de música generals de l'escena
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

        // Esperem fins que el silenciament de la pista prèvia hagi culminat completament
        if (fadeOutRoutine != null)
            yield return fadeOutRoutine;

        // Petit silenci de neteja entre pistes
        yield return new WaitForSeconds(0.15f);

        // 3) Comencem el FadeIn de la nova cançó assignada a aquest trigger
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

    /// <summary>
    /// Comprova si aquest trigger concret està reproduint la seva música.
    /// </summary>
    public bool IsPlaying()
    {
        return newMusicSource != null && newMusicSource.isPlaying;
    }

    /// <summary>
    /// Crida el procés de fadr-out i aturada per a la música d'aquest trigger.
    /// </summary>
    public Coroutine FadeOutAndStop(float duration)
    {
        if (IsPlaying())
        {
            return StartCoroutine(FadeOutRoutine(duration));
        }
        return null;
    }

    /// <summary>
    /// Corrutina que redueix progressivament el volum fins a silenciar-lo i aturar l'AudioSource.
    /// </summary>
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
