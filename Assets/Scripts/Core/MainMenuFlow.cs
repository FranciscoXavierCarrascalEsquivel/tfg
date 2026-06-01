using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Orquestrador del flux del Menú Principal (MainMenuFlow).
/// Controla tota l'experiència en entrar al joc:
/// 1) Esborra de forma proactiva l'inventari previ de memòria per garantir una neteja de dades.
/// 2) Orquestra la línia de temps introductòria del logotip (fosa a transparent del negre, fade-in/fade-out
///    del logotip, i finalment activació suau dels botons de la interfície gràfica).
/// 3) Activa de forma gradual la música d'ambient del menú amb efecte de volum (FadeIn).
/// 4) Gestiona els botons de Jugar (Play) i Sortir (Quit) bloquejant inputs dobles i fent foses de tancament.
/// </summary>
public class MainMenuFlow : MonoBehaviour
{
    [Header("Elements Gràfics (UI)")]
    [SerializeField] private Image blackOverlay;      // Cortina negra per a realitzar foses de pantalla.
    [SerializeField] private Image logoImage;         // Logotip de presentació inicial.
    [SerializeField] private CanvasGroup menuGroup;   // Grup que allotja els botons del menú (Jugar, Sortir, etc.).
    [SerializeField] private Button playButton;       // Botó de començar partida.
    [SerializeField] private Button quitButton;       // Botó de tancar aplicació.

    [Header("Temps de Retard (Timings)")]
    [SerializeField] private float waitBeforeLogo = 0.8f;     // Retard en negre abans de mostrar logotip
    [SerializeField] private float logoVisibleTime = 1.6f;    // Temps de presència en pantalla del logotip
    [SerializeField] private float waitBeforeMenu = 0.5f;     // Interval abans d'activar el menú

    [Header("Duracions de les Foses (Fades)")]
    [SerializeField] private float blackFadeOutAtStart = 0.4f;
    [SerializeField] private float logoFadeIn = 0.5f;
    [SerializeField] private float logoFadeOut = 0.5f;
    [SerializeField] private float menuFadeIn = 0.5f;
    [SerializeField] private float fadeOutOnClick = 0.6f;

    [Header("Opcions de Fosa en Clicar")]
    [Tooltip("Si és true, els botons del menú també s'esvaeixen a l'hora que puja el fons negre en prémer Jugar.")]
    [SerializeField] private bool fadeOutMenuToo = false;

    [Header("Configuració de Càrrega")]
    [SerializeField] private string playSceneName = "FirstTimeSequence"; // Nom de l'escena inicial de partida

    [Header("Música de Menú")]
    [SerializeField] private AudioSource musicSource; // Cançó ambient del menú
    [SerializeField] private AudioClip menuMusic;
    [SerializeField] private bool musicFadeIn = true;
    [SerializeField] private float musicFadeInTime = 1.0f;
    [Range(0f, 1f)]
    [SerializeField] private float musicVolume = 0.8f; // Volum normal objectiu

    private bool inputEnabled = false; // Flag per evitar que el jugador pitgi tecles abans d'hora
    private bool clicked = false;      // Evita doble clic conflictiu
    private Coroutine musicFadeRoutine;
    private Coroutine flowRoutine;

    private void Awake()
    {
        // 1. Reset absolut de dades: Si existia un inventari persistent d'una sessió anterior, el destruïm
        if (PlayerInventory.Instance != null)
        {
            Destroy(PlayerInventory.Instance.gameObject);
        }

        // 2. Reset de flags estàtics de control per evitar estats bloquejats en re-entrar al joc
        CombatLoader.IsInCombat = false;
        CombatLoader.IsCombatLoading = false;
        ShopMenuUI.IsOpen = false;
        InventoryMenuUI.IsOpen = false;
        PauseMenuUI.IsOpen = false;
        PlayerInteractor.IsShaking = false;
        DialogueUI.ForceDisableSkipGlobals = false;
        ControlsUI.ResetTutorialState();

        // Establim l'estat silenciós i ocult de tots els components per defecte
        if (blackOverlay != null)
        {
            blackOverlay.gameObject.SetActive(true);
            blackOverlay.raycastTarget = true;      
            SetImageAlpha(blackOverlay, 1f);        // Iniciem en negre absolut
            blackOverlay.transform.SetAsLastSibling(); // La cortina de seguretat s'apila per damunt de tot
        }

        if (logoImage != null)
        {
            logoImage.gameObject.SetActive(true);
            logoImage.raycastTarget = false; 
            SetImageAlpha(logoImage, 0f);
        }

        if (menuGroup != null)
        {
            menuGroup.alpha = 0f;
            menuGroup.interactable = false;
            menuGroup.blocksRaycasts = false;
        }

        if (musicSource != null)
        {
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.volume = 0f;
        }
    }

    private void Start()
    {
        // Netegem possibles subscripcions prèvies redundants i enllacem els botons de forma neta
        if (playButton != null)
        {
            playButton.onClick.RemoveListener(OnPlay);
            playButton.onClick.AddListener(OnPlay);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(OnQuit);
            quitButton.onClick.AddListener(OnQuit);
        }

        flowRoutine = StartCoroutine(Flow());
    }

    /// <summary>
    /// Corrutina seqüencial que organitza tota la línia gràfica del menú de forma harmoniosa.
    /// </summary>
    private IEnumerator Flow()
    {
        yield return new WaitForSeconds(waitBeforeLogo);

        // Desfem la cortina negra inicial
        if (blackOverlay != null)
        {
            blackOverlay.transform.SetAsLastSibling();
            yield return FadeImage(blackOverlay, GetImageAlpha(blackOverlay), 0f, blackFadeOutAtStart);
            blackOverlay.raycastTarget = false; // Desbloquegem interaccions de pantalla
        }

        // Mostrem el logotip
        if (logoImage != null)
            yield return FadeImage(logoImage, 0f, 1f, logoFadeIn);

        yield return new WaitForSeconds(logoVisibleTime);

        // Ocultem el logotip
        if (logoImage != null)
        {
            yield return FadeImage(logoImage, 1f, 0f, logoFadeOut);
            logoImage.gameObject.SetActive(false);
        }

        yield return new WaitForSeconds(waitBeforeMenu);

        // Engeguem de forma progressiva la música d'ambient del menú
        StartMenuMusic();

        // Fem aparèixer els botons del menú
        if (menuGroup != null)
            yield return FadeCanvasGroup(menuGroup, 0f, 1f, menuFadeIn);

        inputEnabled = true;

        if (menuGroup != null)
        {
            // Atorguem finalment els permisos de clic
            menuGroup.interactable = true;
            menuGroup.blocksRaycasts = true;
        }
    }

    private void OnPlay()
    {
        Debug.Log("MainMenuFlow -> OnPlay()");
        if (!inputEnabled || clicked) return;
        clicked = true;

        // Anem a negre i carreguem la escena de joc
        StartCoroutine(FadeOutThen(() =>
        {
            SceneManager.LoadScene(playSceneName);
        }));
    }

    private void OnQuit()
    {
        Debug.Log("MainMenuFlow -> OnQuit()");
        if (!inputEnabled || clicked) return;
        clicked = true;

        // Anem a negre i tanquem l'execució de forma neta
        StartCoroutine(FadeOutThen(() =>
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }));
    }

    /// <summary>
    /// Corrutina de tancament que s'encarrega d'aplicar un fos a negre de protecció abans d'executar una acció.
    /// </summary>
    private IEnumerator FadeOutThen(System.Action action)
    {
        inputEnabled = false;

        if (menuGroup != null)
        {
            menuGroup.interactable = false;
            menuGroup.blocksRaycasts = false;
        }

        if (blackOverlay != null)
        {
            blackOverlay.gameObject.SetActive(true);
            blackOverlay.transform.SetAsLastSibling();
            blackOverlay.raycastTarget = true;

            if (fadeOutMenuToo && menuGroup != null)
            {
                // Si s'indica, esvaeix en paral·lel el menú de botons alhora que s'enfosqueix el fons
                Coroutine c1 = StartCoroutine(FadeCanvasGroup(menuGroup, menuGroup.alpha, 0f, fadeOutOnClick));
                yield return FadeImage(blackOverlay, GetImageAlpha(blackOverlay), 1f, fadeOutOnClick);
                yield return c1;
            }
            else
            {
                yield return FadeImage(blackOverlay, GetImageAlpha(blackOverlay), 1f, fadeOutOnClick);
            }
        }

        action?.Invoke();
    }

    // =========================================================================
    // SEGMENT DE GESTIÓ MUSICAL DEL MENÚ
    // =========================================================================

    private void StartMenuMusic()
    {
        if (musicSource == null || menuMusic == null) return;
        if (musicSource.isPlaying && musicSource.clip == menuMusic) return;

        musicSource.clip = menuMusic;
        musicSource.loop = true;

        if (musicFadeRoutine != null)
        {
            StopCoroutine(musicFadeRoutine);
            musicFadeRoutine = null;
        }

        if (!musicFadeIn)
        {
            musicSource.volume = musicVolume;
            musicSource.Play();
            return;
        }

        musicFadeRoutine = StartCoroutine(FadeInMusic());
    }

    /// <summary>
    /// Incrementa progressivament el volum del so de fons des de 0 fins al nivell objectiu.
    /// </summary>
    private IEnumerator FadeInMusic()
    {
        musicSource.volume = 0f;
        musicSource.Play();

        float dur = Mathf.Max(0.001f, musicFadeInTime);
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(0f, musicVolume, t / dur);
            yield return null;
        }
        musicSource.volume = musicVolume;
        musicFadeRoutine = null;
    }

    // =========================================================================
    // METODES UTILS D'INTERPOLACIÓ DE UI (HELPERS)
    // =========================================================================

    private IEnumerator FadeImage(Image img, float from, float to, float duration)
    {
        if (img == null) yield break;

        if (duration <= 0.001f)
        {
            SetImageAlpha(img, to);
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(from, to, t / duration);
            SetImageAlpha(img, a);
            yield return null;
        }
        SetImageAlpha(img, to);
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration)
    {
        if (cg == null) yield break;

        if (duration <= 0.001f)
        {
            cg.alpha = to;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            cg.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        cg.alpha = to;
    }

    private void SetImageAlpha(Image img, float a)
    {
        if (img == null) return;
        var c = img.color;
        c.a = a;
        img.color = c;
    }

    private float GetImageAlpha(Image img)
    {
        if (img == null) return 0f;
        return img.color.a;
    }
}
