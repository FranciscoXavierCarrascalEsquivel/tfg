using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CombatLoader : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string combatSceneName = "CombatScene";
    [SerializeField] private MonoBehaviour[] worldScriptsToDisable;

    [Header("Split Snapshot Transition")]
    [SerializeField] private SplitSnapshot splitOverlayPrefab;

    [Header("Audio")]
    [SerializeField] private AudioClip combatMusic;
    private AudioSource combatAudioSource;
    private System.Collections.Generic.Dictionary<AudioSource, float> pausedAudioSources = new System.Collections.Generic.Dictionary<AudioSource, float>();

    private void Awake()
    {
        combatAudioSource = gameObject.AddComponent<AudioSource>();
        combatAudioSource.loop = true;
        combatAudioSource.playOnAwake = false;
        combatAudioSource.spatialBlend = 0f;
    }

    public void StartCombat(CombatEncounter encounter)
    {
        StartCoroutine(StartCombatRoutine(encounter));
    }

    private void LockPlayer(bool isLocked)
    {
        var player = FindFirstObjectByType<PlayerController2D>();
        if (player != null)
        {
            if (isLocked) player.LockMovement();
            else player.UnlockMovement();
        }
    }

    private SplitSnapshot activeOverlay;

    private IEnumerator StartCombatRoutine(CombatEncounter encounter)
    {
        // 1) Captura el frame actual (món) al final del frame
        yield return new WaitForEndOfFrame();

        // Lock player physically and logically so they do not move behind scenes
        LockPlayer(true);

        // Fade ALL active AudioSources in the scene
        pausedAudioSources.Clear();
        var allAudio = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        foreach (var src in allAudio)
        {
            if (src != combatAudioSource && src.isPlaying)
            {
                pausedAudioSources[src] = src.volume;
                StartCoroutine(FadeAudio(src, src.volume, 0f, 1f, true));
            }
        }

        // Snapshot captures the frame cleanly without the UI overlapping
        var snapshot = new Texture2D(Screen.width, Screen.height, TextureFormat.RGBA32, false);
        snapshot.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
        snapshot.Apply();

        // 2) Instancia l'overlay i posa el snapshot
        var overlay = Instantiate(splitOverlayPrefab);
        overlay.SetSnapshot(snapshot);
        overlay.keepAlive = true; // IMPORTANT perquè no s'autodestrueixi al final de PlayOpen!
        activeOverlay = overlay;

        // Assegura que el canvas va davant
        var c = overlay.GetComponent<Canvas>();
        if (c != null)
        {
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 9999;
        }

        // IMPORTANT: deixa 1-2 frames perquè es vegi segur
        yield return null;
        yield return null;

        // 3) Comença l'animació d'obrir-se (split cap als costats)
        // i al mateix temps carreguem combat per sota
        var loadOp = SceneManager.LoadSceneAsync(combatSceneName, LoadSceneMode.Additive);

        // mentre carrega, deixem que l'animació avanci
        // (encara no desactivem el món fins que ja estigui carregat)
        yield return StartCoroutine(overlay.PlayOpen());

        // 4) Espera que el combat estigui carregat
        while (!loadOp.isDone) yield return null;

        // Ara sí que l'animació d'entrada ha acabat, posem la música de combat
        if (combatMusic != null && combatAudioSource != null)
        {
            combatAudioSource.clip = combatMusic;
            combatAudioSource.volume = 0f;
            combatAudioSource.Play();
            StartCoroutine(FadeCombatMusic(true, 1f));
        }

        // 5) Ara sí, desactiva scripts del món
        foreach (var s in worldScriptsToDisable) if (s) s.enabled = false;

        // 6) Inicia combat
        var cm = FindFirstObjectByType<CombatManager>();
        if (cm != null) cm.Begin(encounter, this);
        else Debug.LogError("CombatManager no trobat a CombatScene");
    }

    public void EndCombat()
    {
        StartCoroutine(EndCombatRoutine());
    }

    private IEnumerator EndCombatRoutine()
    {
        // Parem la música de combat a poc a poc
        if (combatAudioSource != null && combatAudioSource.isPlaying)
        {
            StartCoroutine(FadeCombatMusic(false, 1f));
        }

        // REVERSE OVERLAY: fa que els trossos de la pantalla del món tornin a ajuntarse 
        // tapant de nou tot el combat actual.
        if (activeOverlay != null)
        {
            yield return activeOverlay.PlayClose();
            activeOverlay = null;
        }

        var op = SceneManager.UnloadSceneAsync(combatSceneName);
        while (!op.isDone) yield return null;

        foreach (var s in worldScriptsToDisable) if (s) s.enabled = true;
        
        LockPlayer(false);

        // Resume ALL previously paused AudioSources securely
        foreach (var kvp in pausedAudioSources)
        {
            if (kvp.Key != null) StartCoroutine(FadeAudio(kvp.Key, 0f, kvp.Value, 1f, false));
        }
        pausedAudioSources.Clear();
    }

    private IEnumerator FadeCombatMusic(bool fadeIn, float duration)
    {
        if (combatAudioSource == null) yield break;

        float time = 0;
        float startVol = combatAudioSource.volume;
        float targetVol = fadeIn ? 1f : 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            combatAudioSource.volume = Mathf.Lerp(startVol, targetVol, time / duration);
            yield return null;
        }

        combatAudioSource.volume = targetVol;
        if (!fadeIn) combatAudioSource.Stop();
    }

    private IEnumerator FadeAudio(AudioSource source, float startVol, float endVol, float duration, bool pauseAtEnd)
    {
        if (source == null) yield break;
        if (!pauseAtEnd) source.UnPause();
        
        float time = 0;
        while (time < duration)
        {
            if (source == null) yield break;
            time += Time.deltaTime;
            source.volume = Mathf.Lerp(startVol, endVol, time / duration);
            yield return null;
        }

        if (source != null)
        {
            source.volume = endVol;
            if (pauseAtEnd) source.Pause();
        }
    }
}

public enum EnemyAttackPattern
{
    RandomDrop,
    HorizontalWaves,
    TargetedHoming,
    CircleBurst
}

[System.Serializable]
public class CombatEncounter
{
    public Sprite enemyPortrait;
    public GameObject projectilePrefab;
    public float enemyAttackDuration = 6f;
    [Tooltip("Llista d'atacs que pot fer aquest enemic. S'escollirà un a l'atzar cada torn.")]
    public EnemyAttackPattern[] attackPatterns = new EnemyAttackPattern[] { EnemyAttackPattern.RandomDrop };
}
