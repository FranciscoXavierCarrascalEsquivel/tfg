using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Carregador i gestor de transicions de combats (CombatLoader).
/// Aquest és el cervell encarregat de connectar de forma neta el món de l'Overworld amb el Combat (additive scene).
/// Flux d'entrada al combat (StartCombat):
/// 1) Bloqueja el jugador i enquadra de cop la càmera per evitar desalineacions visualment molestes.
/// 2) Cerca i fosa de forma gradual a silenci (FadeOut) de tots els àudios ambientals actius, guardant-ne els volums.
/// 3) Realitza una captura gràfica de la pantalla (Texture2D) just abans d'iniciar la transició.
/// 4) Instancia el SplitSnapshot (pantalla partida fragmentada) projectant la captura com a fons per tapar el canvi.
/// 5) Carrega de forma asíncrona additiva la "CombatScene".
/// 6) Executa el PreSetup del combat, activa l'animació d'obertura del Split i engega la música de combat.
/// 
/// Flux de sortida del combat (EndCombat):
/// 1) Fosa a silenci de la cançó de batalla.
/// 2) Recupera recompenses de reclutament de combat.
/// 3) Uneix de nou les dues meitats del Split tancant la vista, descarrega asíncronament el combat.
/// 4) Si hi ha recompensa, l'afegeix a l'inventari i crea un panell dinàmic al Canvas del món (RecruitRewardPanelUI).
/// 5) Restaura els scripts del món, el moviment, i fa un FadeIn de les músiques del mapa que s'havien pausat.
/// </summary>
public class CombatLoader : MonoBehaviour
{
    [Header("Configuració de l'Escena")]
    [SerializeField] private string combatSceneName = "CombatScene"; // Nom de l'escena de combat a carregar additivament
    [SerializeField] private MonoBehaviour[] worldScriptsToDisable; // Components de l'Overworld que cal congelar en combat

    public static bool IsInCombat { get; set; } // Flag d'estat global de si estem enmig d'un combat

    [Header("Efecte de Transició")]
    [SerializeField] private SplitSnapshot splitOverlayPrefab; // Prefab de pantalla partida per al trencament de vidre

    [Header("Triggers Físics")]
    [SerializeField] private ZoneChangeTrigger[] stopMusicTriggers; // Triggers que aturen preventivament la música de fons

    [Header("Canals d'Àudio")]
    [SerializeField] private AudioClip backgroundMusic; // Música d'exploració del mapa
    [SerializeField] private AudioClip combatMusic;     // Música general de combat
    
    private AudioSource combatAudioSource;
    private AudioSource backgroundAudioSource;
    
    // Diccionari temporal per desar els volums de les cançons del món mentre combatem per poder restaurar-les
    private System.Collections.Generic.Dictionary<AudioSource, float> pausedAudioSources = new System.Collections.Generic.Dictionary<AudioSource, float>();

    private void Awake()
    {
        // Setup dels nostres emissors de so dedicats
        combatAudioSource = gameObject.AddComponent<AudioSource>();
        combatAudioSource.loop = true;
        combatAudioSource.playOnAwake = false;
        combatAudioSource.spatialBlend = 0f;

        backgroundAudioSource = gameObject.AddComponent<AudioSource>();
        backgroundAudioSource.loop = true;
        backgroundAudioSource.playOnAwake = false;
        backgroundAudioSource.spatialBlend = 0f;
    }

    private void OnEnable()
    {
        // Enllacem de forma de delegats els esdeveniments de canvi de zona
        if (stopMusicTriggers != null)
        {
            foreach (var t in stopMusicTriggers)
            {
                if (t != null) t.OnZoneTransition += StopBackgroundMusic;
            }
        }
    }

    private void OnDisable()
    {
        if (stopMusicTriggers != null)
        {
            foreach (var t in stopMusicTriggers)
            {
                if (t != null) t.OnZoneTransition -= StopBackgroundMusic;
            }
        }
    }

    private void Start()
    {
        if (backgroundMusic != null)
        {
            backgroundAudioSource.clip = backgroundMusic;
            backgroundAudioSource.Play();
        }
    }

    /// <summary>
    /// Atura suau de la cançó d'exploració activa del mapa.
    /// </summary>
    public void StopBackgroundMusic()
    {
        if (backgroundAudioSource != null && backgroundAudioSource.isPlaying)
        {
            StartCoroutine(FadeAudio(backgroundAudioSource, backgroundAudioSource.volume, 0f, 3f, true));
        }
    }

    /// <summary>
    /// Atura suau de la cançó d'exploració engegada a partir d'un cicle de corrutina.
    /// </summary>
    public Coroutine StopBackgroundMusicCoroutine(float fadeDuration = 3f)
    {
        if (backgroundAudioSource != null && backgroundAudioSource.isPlaying)
        {
            return StartCoroutine(FadeAudio(backgroundAudioSource, backgroundAudioSource.volume, 0f, fadeDuration, true));
        }
        return null;
    }

    /// <summary>
    /// Reprodueix una nova cançó de fons al mapa amb el volum complet.
    /// </summary>
    public void PlayBackgroundMusic(AudioClip clip)
    {
        if (backgroundAudioSource == null) return;
        backgroundAudioSource.volume = 1f; 
        backgroundAudioSource.clip = clip;
        backgroundAudioSource.Play();
    }

    /// <summary>
    /// Engega de forma segura la seqüència de combat a partir d'un objecte encounter.
    /// </summary>
    public void StartCombat(CombatEncounter encounter)
    {
        if (IsInCombat) 
        {
            Debug.LogWarning("CombatLoader: Intent de carregar combat ignorat perque ja n'hi ha un en curs.");
            return;
        }

        IsInCombat = true;
        StartCoroutine(StartCombatRoutine(encounter));
    }

    /// <summary>
    /// Enllaç d'inici directe a partir d'un perfil d'enemic (Didàctic per a disparadors de UnityEvents de l'inspector).
    /// </summary>
    public void StartCombatWithProfile(EnemyProfile profile)
    {
        if (profile == null) return;
        
        CombatEncounter encounter = new CombatEncounter();
        encounter.enemyProfile = profile;
        encounter.enemyAttackDuration = profile.attackDuration;
        
        StartCombat(encounter);
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

    private SplitSnapshot activeOverlay; // Transició activa

    /// <summary>
    /// Corrutina mestra que s'encarrega d'aplicar la transició visual de trencament i de carregar additivament la CombatScene.
    /// </summary>
    private IEnumerator StartCombatRoutine(CombatEncounter encounter)
    {
        LockPlayer(true);
        IsInCombat = true;

        // Centrem immediatament la càmera a sobre de l'objectiu perquè no hi hagi salts visuals
        var cams = FindObjectsByType<CameraBoundedFollow>(FindObjectsSortMode.None);
        foreach (var camFollow in cams) camFollow.SnapToTarget();

        // 1) Esperem al final de frame (EndOfFrame) perquè el render de la càmera estigui complet
        yield return new WaitForEndOfFrame();

        LockPlayer(true);

        // Emmudim a poc a poc (FadeOut) absolutament TOTS els AudioSources del món
        pausedAudioSources.Clear();
        var allAudio = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        foreach (var src in allAudio)
        {
            if (src != combatAudioSource && src.isPlaying)
            {
                pausedAudioSources[src] = src.volume; // Guardem el volum original
                StartCoroutine(FadeAudio(src, src.volume, 0f, 1f, true));
            }
        }

        // Capturem de forma neta els píxels de la pantalla de l'Overworld
        var snapshot = new Texture2D(Screen.width, Screen.height, TextureFormat.RGBA32, false);
        snapshot.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
        snapshot.Apply();

        // 2) Instanciem el filtre gràfic de Split i li bolquem la captura com a textures de tall
        var overlay = Instantiate(splitOverlayPrefab);
        overlay.SetSnapshot(snapshot);
        overlay.keepAlive = true; // Mantenim viu per fer el tancament invers en acabar!
        activeOverlay = overlay;

        // Forcem que el Canvas del trencament de pantalla passi per sobre de tot
        var c = overlay.GetComponent<Canvas>();
        if (c != null)
        {
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 9999;
        }

        // Deixem 2 frames lliures per a assegurar-nos que Unity ha dibuixat l'overlay abans d'iniciar la càrrega
        yield return null;
        yield return null;

        // 3) Carreguem additivament l'escena del combat sense esborrar el mapa de joc
        var loadOp = SceneManager.LoadSceneAsync(combatSceneName, LoadSceneMode.Additive);
        while (!loadOp.isDone) yield return null;

        // Cerquem el gestor del combat d'esquenes a la pantalla i fem el PreSetup gràfic silenciós
        var cm = FindFirstObjectByType<CombatManager>();
        if (cm != null) cm.PreSetup(encounter);

        // 4) Triem i engeguem l'animació de fractura i separació lateral del Split
        yield return StartCoroutine(overlay.PlayOpen());

        // Comencem la música del combat (triant la cançó personalitzada de l'enemic si en té, o la genèrica)
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

        // 5) Desactivem scripts i controladors de l'Overworld
        if (worldScriptsToDisable != null)
        {
            foreach (var s in worldScriptsToDisable) 
            {
                if (s != null && s.gameObject != null) s.enabled = false;
            }
        }

        // 6) Llancem formalment la seqüència activa dels menús de combat de la batalla
        if (cm != null) yield return cm.BeginRoutine(encounter, this);
        else Debug.LogError("CombatManager no trobat a CombatScene");
    }

    /// <summary>
    /// Finalitza el combat.
    /// </summary>
    public void EndCombat()
    {
        StartCoroutine(EndCombatRoutine());
    }

    /// <summary>
    /// Corrutina de tancament i alliberament de combat.
    /// </summary>
    private IEnumerator EndCombatRoutine()
    {
        IsInCombat = false;
        
        // Parem a poc a poc la cançó de batalla
        if (combatAudioSource != null && combatAudioSource.isPlaying)
        {
            if (combatAudioSource.volume > 0.1f) 
                StartCoroutine(FadeCombatMusic(false, 0.8f));
        }

        // Comprovem si l'enemic ha sigut reclutat amb èxit en finalitzar la batalla
        EnemyProfile pendingReward = null;
        var cm = FindFirstObjectByType<CombatManager>();
        if (cm != null)
        {
            pendingReward = cm.ConsumeRecruitReward();
            if (pendingReward != null) Debug.Log("[COMBAT] Detected pending recruit reward: " + pendingReward.enemyName);
        }
        else
        {
            Debug.LogWarning("[COMBAT] Could not find CombatManager to check for rewards!");
        }

        // ANIMACIÓ INVERSA DEL SPLIT: Les meitats de pantalla es tornen a unir i tapen la visual del combat
        SplitSnapshot overlayToDestroy = null;
        if (activeOverlay != null)
        {
            overlayToDestroy = activeOverlay;
            yield return activeOverlay.PlayClose();
            activeOverlay = null; 
        }

        // Descarreguem asíncronament el combat ja ocult de forma totalment neta de la memòria
        var op = SceneManager.UnloadSceneAsync(combatSceneName);
        while (!op.isDone) yield return null;

        // Ara que la càrrega ja no hi és, és 100% segur destruir el SplitSnapshot sense fotogrames buits
        if (overlayToDestroy != null)
        {
            overlayToDestroy.Cleanup();
            Destroy(overlayToDestroy.gameObject);
        }

        // Reactivem els sistemes del món d'exploració
        if (worldScriptsToDisable != null)
        {
            foreach (var s in worldScriptsToDisable) 
            {
                if (s != null && s.gameObject != null) s.enabled = true;
            }
        }
        
        var cams = FindObjectsByType<CameraBoundedFollow>(FindObjectsSortMode.None);
        foreach (var camFollow in cams) camFollow.SnapToTarget();

        // ── SI HI HA RECOMPENSA DE RECLUTAMENT, GENEREM LA TARGETA DE PRESENTACIÓ DE L'AMIC ──
        if (pendingReward != null)
        {
            // Registrem el nou amic i els seus atributs a l'inventari
            if (PlayerInventory.Instance != null)
            {
                PlayerInventory.Instance.ClaimRecruitReward(pendingReward.enemyName);
            }

            // Cerquem el Canvas de la interfície del món a sobre del qual dibuixarem el panell
            Canvas worldCanvas = null;
            Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var c in allCanvases)
            {
                if (c.isRootCanvas && c.gameObject.activeInHierarchy)
                {
                    string n = c.name.ToLower();
                    if (n.Contains("main") || n.Contains("ui") || n.Contains("overworld") || n.Contains("player"))
                    {
                        worldCanvas = c;
                        break;
                    }
                    if (worldCanvas == null) worldCanvas = c; 
                }
            }

            if (worldCanvas == null && allCanvases.Length > 0)
            {
                foreach(var c in allCanvases) if(c.gameObject.activeInHierarchy) { worldCanvas = c; break; }
            }

            if (worldCanvas != null)
            {
                bool rewardDone = false;
                string msg = !string.IsNullOrEmpty(pendingReward.recruitmentCompleteMessage)
                    ? pendingReward.recruitmentCompleteMessage
                    : pendingReward.recruitmentRewardDescription;

                // Generem el bonic panell gràfic per codi de reclutament de la TFG
                RecruitRewardPanelUI.Create(
                    worldCanvas.transform,
                    pendingReward.recruitmentRewardSprite,
                    msg,
                    pendingReward.enemyName,
                    pendingReward.recruitmentRewardSound,
                    () => { rewardDone = true; } // Callback que s'executa en tancar
                );
                
                // Ens quedem en bucle d'espera fins que l'usuari llegeixi i premi Tancar
                yield return new WaitUntil(() => rewardDone);
            }
            else
            {
                Debug.LogWarning("No s'ha trobat cap Canvas actiu al món per mostrar la recompensa de reclutament.");
            }
        }

        LockPlayer(false);

        // FOS DE TORNADA AL VOLUM ORIGINAL DE TOTS ELS SONS DEL MÓN QUE HAVÍEM PAUSAT
        foreach (var kvp in pausedAudioSources)
        {
            if (kvp.Key != null) StartCoroutine(FadeAudio(kvp.Key, 0f, kvp.Value, 1f, false));
        }
        pausedAudioSources.Clear();
        
        // Reprenem de forma segura qualsevol diàleg del mapa que hagués quedat interromput
        var dUI = FindFirstObjectByType<DialogueUI>();
        if (dUI != null) dUI.ResumeAfterCombat();
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

    /// <summary>
    /// Corrutina utilitària de transició de volum gradual (Fading) per a qualsevol AudioSource actiu del joc.
    /// </summary>
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

/// <summary>
/// Llista exhaustiva d'identificadors de patrons d'atac que poden executar els enemics en el TFG.
/// </summary>
public enum EnemyAttackPattern
{
    RandomDrop,
    HorizontalWaves,
    CircleBurst,
    DiagonalCross,
    FastMeteors,
    SnakeWaves,
    RandomDropSpinning,
    HorizontalWavesSpinning,
    CircleBurstSpinning,
    DiagonalCrossSpinning,
    FastMeteorsSpinning,
    SnakeWavesSpinning,
    RainWithRed,
    RainWithRedSpinning,
    RapidFireRed,
    RapidFireRedSpinning,
    RedHomingBarrage,
    RedHomingBarrageSpinning,
    RedSweepWall,
    SimpleStraightLines,
    AlternatingSides,
    SideSweepers,
    ExpandingCross
}

/// <summary>
/// Model de dades d'esdeveniment que representa una única instància de combat iniciada en el joc.
/// </summary>
[System.Serializable]
public class CombatEncounter
{
    [Tooltip("Perfil de dades de l'enemic amb qui es combatirà.")]
    public EnemyProfile enemyProfile;

    [Header("Overrides d'Atributs (Només utilitzats si no hi ha perfil)")]
    public Sprite enemyPortrait;
    public GameObject projectilePrefab;
    public float enemyAttackDuration = 6f;
    public EnemyAttackPattern[] attackPatterns = new EnemyAttackPattern[] { EnemyAttackPattern.RandomDrop };
}
