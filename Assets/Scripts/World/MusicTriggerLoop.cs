using UnityEngine;

/// <summary>
/// Sistema avançat de reproducció musical amb bucles precisos sense pauses (gapless audio looping).
/// Desenvolupat a mida per al projecte (TFG) utilitzant metadades de mostreig de mostres de so (samples)
/// a través de dos components AudioSource paral·lels (Double Buffering d'Àudio).
/// Quan el jugador travessa el trigger 2D, comença la reproducció. Si s'indica un solapament (overlap),
/// el segon reproductor s'inicia de forma anticipada per a crear transicions perfectes sense buits d'àudio.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class TriggerMusicLoopSection2D : MonoBehaviour
{
    [Header("Àudio i Temps de Bucle")]
    [SerializeField] private AudioClip clip; // Cançó principal que conté el bucle.

    [Tooltip("Instants en segons on comença el punt de bucle (reubicació)")]
    [SerializeField] private float loopStartTime = 12.0f;

    [Tooltip("Instants en segons on finalitza la secció de bucle (punt de retorn)")]
    [SerializeField] private float loopEndTime = 28.0f;

    [Tooltip("Temps (en segons) de solapament anticipat per començar la següent reproducció en paral·lel. Ex: 0.5 per fer crossfade.")]
    [SerializeField] private float overlapTime = 0.0f;

    [Header("Opcions de Comportament")]
    [SerializeField] private bool startFromBeginning = true; // Si és fals, comença directament en el punt de loop inicial.
    [SerializeField] private bool triggerOnlyOnce = true;    // Activa si només s'ha de disparar un sol cop al trepitjar el trigger.

    // Buffering dual d'AudioSources per fer les transicions perfectes
    private AudioSource srcA;   // Primer canal d'àudio
    private AudioSource srcB;   // Segon canal d'àudio (per a solapaments i transició de salt)
    private AudioSource active; // Canal que es troba sonant actualment
    private AudioSource next;   // Canal inactiu preparat per a sonar en el següent cicle

    private bool activeFlag; // Flag d'estat per saber si el reproductor està actiu
    private bool used; // Flag per comprovar si ja s'ha consumit el trigger únic

    // Mostres d'àudio (Samples) calculades a partir del temps en segons i la freqüència de la cançó
    private int loopStartSamples;
    private int loopEndSamples;
    private int overlapSamples;

    // Control intern per a no iniciar múltiples solapaments simultanis durant el mateix cicle
    private bool overlapStartedThisCycle = false;

    private void Awake()
    {
        // 1) Configurem i preparem el primer canal (associat al component original)
        srcA = GetComponent<AudioSource>();
        SetupSource(srcA);

        // 2) Instanciem un segon AudioSource dinàmicament per a fer possible el buffering dual d'àudio
        srcB = gameObject.AddComponent<AudioSource>();
        SetupSource(srcB);

        // Assignació inicial del buffer dual
        active = srcA;
        next = srcB;

        // Calculem les equivalències en mostres (samples) per tenir màxima precisió matemàtica
        RecomputeSamples();
    }

    /// <summary>
    /// Força una configuració estricta a les fonts per a reproduccions de tipus música de fons (BGM) controlada manualment.
    /// </summary>
    private void SetupSource(AudioSource s)
    {
        s.playOnAwake = false;
        s.loop = false;          // IMPORTANT: el bucle el controlem manualment per codi a la funció Update!
        s.spatialBlend = 0f;     // Mode de so 2D pur (so global, no localitzat 3D)
        s.volume = 1f;
    }

    private void OnValidate()
    {
        // Si canviem els valors a l'inspector en temps de disseny, re-calculem les mostres immediatament
        if (clip != null) RecomputeSamples();
    }

    /// <summary>
    /// Transforma els temps en segons a unitats nàtives de mostres de so (samples) de l'AudioClip.
    /// La precisió per samples evita els micro-silencis típics que té Unity en fer bucles amb valors floats.
    /// </summary>
    private void RecomputeSamples()
    {
        if (clip == null) return;

        // Limitem els rangs de seguretat segons la duració real del clip
        loopStartTime = Mathf.Clamp(loopStartTime, 0f, clip.length - 0.001f);
        loopEndTime   = Mathf.Clamp(loopEndTime,   0f, clip.length);

        if (loopEndTime <= loopStartTime)
            loopEndTime = Mathf.Min(loopStartTime + 0.1f, clip.length);

        overlapTime = Mathf.Max(0f, overlapTime);

        // Convertim segons -> mostres (segons * freqüència de mostreig en Hz, normalment 44100Hz o 48000Hz)
        loopStartSamples = Mathf.FloorToInt(loopStartTime * clip.frequency);
        loopEndSamples   = Mathf.FloorToInt(loopEndTime   * clip.frequency);
        overlapSamples   = Mathf.FloorToInt(overlapTime   * clip.frequency);

        // Control de seguretat: el solapament no pot ser superior a la durada de la pròpia secció de bucle
        int segmentLen = loopEndSamples - loopStartSamples;
        if (overlapSamples >= segmentLen)
            overlapSamples = Mathf.Max(0, segmentLen - 1);
    }

    private void Update()
    {
        // Retornem si el reproductor està aturat o no té cançó carregada
        if (!activeFlag || clip == null) return;

        // Si la font activa ha sigut aturada per un factor extern (pauses generals, etc.), no continuem
        if (!active.isPlaying) return;

        int t = active.timeSamples; // Mostra actual de la reproducció

        // 1) DISPARADOR DE SOLAPAMENT (Overlap): Iniciar la nova pista una mica abans per fer transició suau
        if (!overlapStartedThisCycle && overlapSamples > 0)
        {
            if (t >= (loopEndSamples - overlapSamples))
            {
                StartOverlapNextCycle();
                overlapStartedThisCycle = true;
            }
        }

        // 2) DISPARADOR DE BUCLE (Loop Cut): Quan s'arriba al límit de final de bucle
        if (t >= loopEndSamples)
        {
            // Aturem el cicle actual
            active.Stop();

            // Si no s'ha configurat solapament (overlap = 0), arrenquem el següent canal exactament en aquest instant
            if (overlapSamples == 0)
            {
                StartNextFromLoopStart();
            }

            // Realitzem l'intercanvi de buffers (el canal següent passa a ser l'actiu i viceversa)
            SwapSources();

            // Resetegem el control d'overlap per al nou cicle que comença
            overlapStartedThisCycle = false;
        }
    }

    /// <summary>
    /// Comença a fer sonar el segon AudioSource des del punt d'inici de bucle de forma solapada.
    /// </summary>
    private void StartOverlapNextCycle()
    {
        next.clip = clip;
        next.timeSamples = loopStartSamples;
        next.Play();
    }

    /// <summary>
    /// Comença immediatament la reproducció del canal següent des del punt d'inici del bucle.
    /// </summary>
    private void StartNextFromLoopStart()
    {
        next.clip = clip;
        next.timeSamples = loopStartSamples;
        next.Play();
    }

    /// <summary>
    /// Intercanvia les referències dels reproductors actiu i inactiu (Double Buffering).
    /// </summary>
    private void SwapSources()
    {
        var tmp = active;
        active = next;
        next = tmp;
    }

    // =========================================================================
    // TRIGGERS D'INTERACCIÓ 2D
    // =========================================================================

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (triggerOnlyOnce && used) return;

        used = true;
        activeFlag = true;

        overlapStartedThisCycle = false;

        // Assignem la cançó a ambdues fonts
        srcA.clip = clip;
        srcB.clip = clip;

        // Forcem l'aturada inicial preventiva
        srcA.Stop();
        srcB.Stop();

        // Assignem la font A com a inicialment activa
        active = srcA;
        next = srcB;

        // Escollim el punt d'inici (principi de cançó o primer salt)
        if (startFromBeginning)
            active.time = 0f;
        else
            active.time = loopStartTime;

        active.Play();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        // Opcional: si es vol aturar la reproducció en sortir del volum del trigger
        // if (!other.CompareTag("Player")) return;
        // activeFlag = false;
        // srcA.Stop();
        // srcB.Stop();
        // overlapStartedThisCycle = false;
    }

    /// <summary>
    /// Atura de forma total i immediata la reproducció de tots dos buffers.
    /// </summary>
    public void StopAll()
    {
        activeFlag = false;
        srcA.Stop();
        srcB.Stop();
        overlapStartedThisCycle = false;
    }

    /// <summary>
    /// Corrutina per realitzar una davallada de volum gradual (FadeOut) en ambdós buffers, seguida d'aturada.
    /// </summary>
    public System.Collections.IEnumerator FadeOutAndStop(float duration)
    {
        float startVolA = srcA.volume;
        float startVolB = srcB.volume;
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float ratio = Mathf.Clamp01(t / duration);
            srcA.volume = Mathf.Lerp(startVolA, 0f, ratio);
            srcB.volume = Mathf.Lerp(startVolB, 0f, ratio);
            yield return null;
        }

        StopAll();
        
        // Restaurem els volums originals dels canals per a futures reproduccions
        srcA.volume = startVolA;
        srcB.volume = startVolB;
    }
}
