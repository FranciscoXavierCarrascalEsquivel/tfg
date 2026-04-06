using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CombatLoader : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string combatSceneName = "CombatScene";
    [SerializeField] private MonoBehaviour[] worldScriptsToDisable;

    public static bool IsInCombat { get; private set; }

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
        IsInCombat = true;
        // Snap camera to target immediately so the snapshot is perfectly centered/clamped
        var cams = FindObjectsByType<CameraBoundedFollow>(FindObjectsSortMode.None);
        foreach (var camFollow in cams) camFollow.SnapToTarget();

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

        // 3) Carreguem combat per sota i esperem primer que el món estigui instanciat
        var loadOp = SceneManager.LoadSceneAsync(combatSceneName, LoadSceneMode.Additive);
        while (!loadOp.isDone) yield return null;

        // Ara que la UI existeix d'esquenes però ningú la veu per l'Overlay,
        // cridem l'Setup abans de l'animació per tapar possibles "errors d'un frame vaci"
        var cm = FindFirstObjectByType<CombatManager>();
        if (cm != null) cm.PreSetup(encounter);

        // 4) Llavors, mentre ja estan les imatges precarregades, obrim l'animació de transició
        yield return StartCoroutine(overlay.PlayOpen());

        // Ara sí que l'animació d'entrada ha acabat, posem la música de combat
        // Prioritat: Música de l'enemic > Música global del CombatLoader
        AudioClip musicToPlay = combatMusic;
        if (encounter != null && encounter.enemyProfile != null && encounter.enemyProfile.combatMusic != null)
        {
            musicToPlay = encounter.enemyProfile.combatMusic;
        }

        if (musicToPlay != null && combatAudioSource != null)
        {
            combatAudioSource.clip = musicToPlay;
            combatAudioSource.volume = 0f;
            combatAudioSource.Play();
            StartCoroutine(FadeCombatMusic(true, 1f));
        }

        // 5) Ara sí, desactiva scripts del món
        foreach (var s in worldScriptsToDisable) if (s) s.enabled = false;

        // 6) Inicia combat de debò on els menús llisquen
        if (cm != null) yield return cm.BeginRoutine(encounter, this);
        else Debug.LogError("CombatManager no trobat a CombatScene");
    }

    public void EndCombat()
    {
        StartCoroutine(EndCombatRoutine());
    }

    private IEnumerator EndCombatRoutine()
    {
        IsInCombat = false;
        // Parem la música de combat a poc a poc (especialment si no ha començat encara el fadeout llarg)
        if (combatAudioSource != null && combatAudioSource.isPlaying)
        {
            if (combatAudioSource.volume > 0.1f) // Si encara té volum, fem un fade ràpid de seguretat
                StartCoroutine(FadeCombatMusic(false, 0.8f));
        }

        // Recuperem la recompensa pendent ABANS de destruir l'escena de combat
        EnemyProfile pendingReward = null;
        var cm = FindFirstObjectByType<CombatManager>();
        if (cm != null)
        {
            pendingReward = cm.ConsumeRecruitReward();
        }

        // REVERSE OVERLAY: fa que els trossos de la pantalla del món tornin a ajuntarse 
        // tapant de nou tot el combat actual.
        SplitSnapshot overlayToDestroy = null;
        if (activeOverlay != null)
        {
            overlayToDestroy = activeOverlay;
            yield return activeOverlay.PlayClose();
            activeOverlay = null; // Ja no és l'actiu, però encara existeix el GameObject
        }

        var op = SceneManager.UnloadSceneAsync(combatSceneName);
        while (!op.isDone) yield return null;

        // ARA que l'escena ja no existeix, podem destruir l'overlay sense por a veure un frame buit
        if (overlayToDestroy != null)
        {
            overlayToDestroy.Cleanup();
            Destroy(overlayToDestroy.gameObject);
        }

        foreach (var s in worldScriptsToDisable) if (s) s.enabled = true;
        
        // Assegurem que la càmera estigui exactament on toca abans d'alliberar el jugador
        var cams = FindObjectsByType<CameraBoundedFollow>(FindObjectsSortMode.None);
        foreach (var camFollow in cams) camFollow.SnapToTarget();

        // ── Si hi ha recompensa de reclutament pendent, la mostrem ARA al món ──
        if (pendingReward != null)
        {
            // Reclamem la recompensa perquè els bonus s'apliquin a partir d'ara
            if (PlayerInventory.Instance != null)
            {
                PlayerInventory.Instance.ClaimRecruitReward(pendingReward.enemyName);
            }

            // Millora de detecció de Canvas: busquem el Canvas principal de la UI del món
            Canvas worldCanvas = null;
            Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var c in allCanvases)
            {
                if (c.isRootCanvas && c.gameObject.activeInHierarchy && c.enabled)
                {
                    // Prioritzem el que es digui "MainCanvas" o "UI"
                    if (c.name.Contains("Main") || c.name.Contains("UI") || c.name.Contains("Overworld"))
                    {
                        worldCanvas = c;
                        break;
                    }
                    worldCanvas = c; // Fallback al primer root actiu que trobi
                }
            }

            if (worldCanvas != null)
            {
                bool rewardDone = false;
                string msg = !string.IsNullOrEmpty(pendingReward.recruitmentCompleteMessage)
                    ? pendingReward.recruitmentCompleteMessage
                    : pendingReward.recruitmentRewardDescription;

                RecruitRewardPanelUI.Create(
                    worldCanvas.transform,
                    pendingReward.recruitmentRewardSprite,
                    msg,
                    pendingReward.enemyName,
                    pendingReward.recruitmentRewardSound,
                    () => { rewardDone = true; }
                );
                
                // Esperem que l'usuari tanqui el panell
                yield return new WaitUntil(() => rewardDone);
            }
            else
            {
                Debug.LogWarning("No s'ha trobat cap Canvas actiu al món per mostrar la recompensa de reclutament.");
            }
        }

        LockPlayer(false);

        // Resume ALL previously paused AudioSources securely
        foreach (var kvp in pausedAudioSources)
        {
            if (kvp.Key != null) StartCoroutine(FadeAudio(kvp.Key, 0f, kvp.Value, 1f, false));
        }
        pausedAudioSources.Clear();
    }

    private Coroutine activeFadeMusicCoroutine;
    public IEnumerator FadeCombatMusic(bool fadeIn, float duration)
    {
        if (combatAudioSource == null) yield break;
        if (activeFadeMusicCoroutine != null) StopCoroutine(activeFadeMusicCoroutine);
        activeFadeMusicCoroutine = StartCoroutine(FadeCombatMusicInternal(fadeIn, duration));
        yield return activeFadeMusicCoroutine;
    }

    private IEnumerator FadeCombatMusicInternal(bool fadeIn, float duration)
    {
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
        activeFadeMusicCoroutine = null;
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

    private void OnGUI()
    {
        // Botó per fer debug i entrar en combat aleatori des del món
        if (GUI.Button(new Rect(10, 10, 220, 50), "TEST: Combat Aleatori"))
        {
            DebugStartRandomCombat();
        }
    }

    private void DebugStartRandomCombat()
    {
        if (IsInCombat) return;

        if (PlayerInventory.Instance == null || PlayerInventory.Instance.enemyDatabase.Count == 0)
        {
            Debug.LogWarning("Sense base de dades enemics!");
            return;
        }

        var enemy = PlayerInventory.Instance.enemyDatabase[Random.Range(0, PlayerInventory.Instance.enemyDatabase.Count)];
        var enc = new CombatEncounter { enemyProfile = enemy };
        StartCombat(enc);
    }
}

public enum EnemyAttackPattern
{
    RandomDrop,
    HorizontalWaves,
    CircleBurst,
    DiagonalCross,
    FastMeteors,
    SnakeWaves,
    // Versions Spinning
    RandomDropSpinning,
    HorizontalWavesSpinning,
    CircleBurstSpinning,
    DiagonalCrossSpinning,
    FastMeteorsSpinning,
    SnakeWavesSpinning,
    // Nous patrons (Red Projectiles / Strictly Downward)
    RainWithRed,
    RainWithRedSpinning,
    RapidFireRed,
    RedHomingBarrage,
    RedSweepWall
}

[System.Serializable]
public class CombatEncounter
{
    [Tooltip("Si poses un perfil aquí, ignora la resta i utilitza les dades completes del monstre.")]
    public EnemyProfile enemyProfile;

    [Header("Overrides Temporals (Ignorats si hi ha Perfil)")]
    public Sprite enemyPortrait;
    public GameObject projectilePrefab;
    public float enemyAttackDuration = 6f;
    public EnemyAttackPattern[] attackPatterns = new EnemyAttackPattern[] { EnemyAttackPattern.RandomDrop };
}
