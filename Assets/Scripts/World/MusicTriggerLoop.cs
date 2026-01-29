using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class TriggerMusicLoopSection2D : MonoBehaviour
{
    [Header("Music")]
    [SerializeField] private AudioClip clip; // Clip d'audio que sonarà.

    [Tooltip("Segons on comença el bucle (reubicació)")]
    [SerializeField] private float loopStartTime = 12.0f;

    [Tooltip("Segons on acaba el bucle (quan arriba aquí, es prepara el següent cicle)")]
    [SerializeField] private float loopEndTime = 28.0f;

    [Tooltip("Temps (en segons) abans de loopEndTime per iniciar el següent cicle en paral·lel. Ex: 0.5")]
    [SerializeField] private float overlapTime = 0.0f;

    [Header("Options")]
    [SerializeField] private bool startFromBeginning = true; // Si és fals, comença directament al punt de loop.
    [SerializeField] private bool triggerOnlyOnce = true;    // Si només s'ha de disparar un cop.

    private AudioSource srcA;   // Font d'audio activa.
    private AudioSource srcB;   // Font d'audio secundària per fer el solapament.
    private AudioSource active; // Referència a la font que sona actualment.
    private AudioSource next;   // Referència a la font que sonarà després.

    private bool activeFlag;
    private bool used;

    private int loopStartSamples;
    private int loopEndSamples;
    private int overlapSamples;

    // per evitar disparar overlap múltiples cops dins del mateix cicle
    private bool overlapStartedThisCycle = false;

    private void Awake()
    {
        srcA = GetComponent<AudioSource>();
        SetupSource(srcA);

        // Crea un segon AudioSource automàticament
        srcB = gameObject.AddComponent<AudioSource>();
        SetupSource(srcB);

        active = srcA;
        next = srcB;

        RecomputeSamples();
    }

    private void SetupSource(AudioSource s)
    {
        s.playOnAwake = false;
        s.loop = false;          // IMPORTANT: el loop el fem nosaltres
        s.spatialBlend = 0f;     // 2D
        s.volume = 1f;
    }

    private void OnValidate()
    {
        if (clip != null) RecomputeSamples();
    }

    private void RecomputeSamples()
    {
        if (clip == null) return;

        // Rang coherents
        loopStartTime = Mathf.Clamp(loopStartTime, 0f, clip.length - 0.001f);
        loopEndTime   = Mathf.Clamp(loopEndTime,   0f, clip.length);

        if (loopEndTime <= loopStartTime)
            loopEndTime = Mathf.Min(loopStartTime + 0.1f, clip.length);

        overlapTime = Mathf.Max(0f, overlapTime);

        loopStartSamples = Mathf.FloorToInt(loopStartTime * clip.frequency);
        loopEndSamples   = Mathf.FloorToInt(loopEndTime   * clip.frequency);
        overlapSamples   = Mathf.FloorToInt(overlapTime   * clip.frequency);

        // Si overlap és massa gran, que com a mínim deixi una mica de marge
        int segmentLen = loopEndSamples - loopStartSamples;
        if (overlapSamples >= segmentLen)
            overlapSamples = Mathf.Max(0, segmentLen - 1);
    }

    private void Update()
    {
        if (!activeFlag || clip == null) return;

        // Si l'actiu no sona (per qualsevol motiu), no fem res
        if (!active.isPlaying) return;

        int t = active.timeSamples;

        // 1) Iniciar el següent cicle abans d'arribar a loopEnd
        //    quan t >= loopEnd - overlap
        if (!overlapStartedThisCycle && overlapSamples > 0)
        {
            if (t >= (loopEndSamples - overlapSamples))
            {
                StartOverlapNextCycle();
                overlapStartedThisCycle = true;
            }
        }

        // 2) Quan l'actiu arriba al final del loop, l'aturem i fem el canvi
        if (t >= loopEndSamples)
        {
            // Aturem el cicle actual (el següent ja sonarà si hi ha overlap)
            active.Stop();

            // si NO hi havia overlap, iniciem el següent aquí mateix
            if (overlapSamples == 0)
            {
                StartNextFromLoopStart();
            }

            // swap: el "next" passa a ser "active"
            SwapSources();

            // nou cicle -> permetre overlap de nou
            overlapStartedThisCycle = false;
        }
    }

    private void StartOverlapNextCycle()
    {
        // Comença el "next" des de loopStart mentre l'actiu encara sona
        next.clip = clip;
        next.timeSamples = loopStartSamples;
        next.Play();
    }

    private void StartNextFromLoopStart()
    {
        next.clip = clip;
        next.timeSamples = loopStartSamples;
        next.Play();
    }

    private void SwapSources()
    {
        var tmp = active;
        active = next;
        next = tmp;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (triggerOnlyOnce && used) return;

        used = true;
        activeFlag = true;

        // Reseteja
        overlapStartedThisCycle = false;

        // Assigna clip als dos
        srcA.clip = clip;
        srcB.clip = clip;

        // Atura per si de cas
        srcA.Stop();
        srcB.Stop();

        // Posem A com actiu inicial
        active = srcA;
        next = srcB;

        // Inici
        if (startFromBeginning)
            active.time = 0f;
        else
            active.time = loopStartTime;

        active.Play();
    }

    // Opcional: si vols que quan surti del trigger pari
    private void OnTriggerExit2D(Collider2D other)
    {
        // if (!other.CompareTag("Player")) return;
        // activeFlag = false;
        // srcA.Stop();
        // srcB.Stop();
        // overlapStartedThisCycle = false;
    }
}
