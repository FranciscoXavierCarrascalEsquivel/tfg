using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuFlow : MonoBehaviour
{
    [Header("UI Refs")]
    [SerializeField] private Image blackOverlay;      // Image negra fullscreen
    [SerializeField] private Image logoImage;         // logo (Image)
    [SerializeField] private CanvasGroup menuGroup;   // CanvasGroup del MenuRoot
    [SerializeField] private Button playButton;
    [SerializeField] private Button quitButton;

    [Header("Timings")]
    [SerializeField] private float waitBeforeLogo = 0.8f;     // X
    [SerializeField] private float logoVisibleTime = 1.6f;    // Y
    [SerializeField] private float waitBeforeMenu = 0.5f;     // Z

    [Header("Fade Durations")]
    [SerializeField] private float blackFadeOutAtStart = 0.4f;
    [SerializeField] private float logoFadeIn = 0.5f;
    [SerializeField] private float logoFadeOut = 0.5f;
    [SerializeField] private float menuFadeIn = 0.5f;
    [SerializeField] private float fadeOutOnClick = 0.6f;

    [Header("Click Fade Options")]
    [Tooltip("Si true, el menú també fa fade out (a més del negre).")]
    [SerializeField] private bool fadeOutMenuToo = false;

    [Header("Play")]
    [SerializeField] private string playSceneName = "FirstTimeSequence";

    [Header("Menu Music")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioClip menuMusic;
    [SerializeField] private bool musicFadeIn = true;
    [SerializeField] private float musicFadeInTime = 1.0f;
    [Range(0f, 1f)]
    [SerializeField] private float musicVolume = 0.8f;

    private bool inputEnabled = false;
    private bool clicked = false;
    private Coroutine musicFadeRoutine;
    private Coroutine flowRoutine;

    private void Awake()
    {
        // Estat inicial
        if (blackOverlay != null)
        {
            blackOverlay.gameObject.SetActive(true);
            blackOverlay.raycastTarget = true;      // bloqueja clics al principi
            SetImageAlpha(blackOverlay, 1f);        // comença en negre
            blackOverlay.transform.SetAsLastSibling(); // assegura a sobre de tot
        }

        if (logoImage != null)
        {
            logoImage.gameObject.SetActive(true);
            logoImage.raycastTarget = false; // no bloquejar clics
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
        // 🔒 Eliminem listeners duplicats si el script es re-carrega
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

    IEnumerator Flow()
    {
        yield return new WaitForSeconds(waitBeforeLogo);

        if (blackOverlay != null)
        {
            blackOverlay.transform.SetAsLastSibling();
            yield return FadeImage(blackOverlay, GetImageAlpha(blackOverlay), 0f, blackFadeOutAtStart);
            // Ara ja no hauria de bloquejar el menú (però encara no està visible)
            blackOverlay.raycastTarget = false;
        }

        if (logoImage != null)
            yield return FadeImage(logoImage, 0f, 1f, logoFadeIn);

        yield return new WaitForSeconds(logoVisibleTime);

        if (logoImage != null)
        {
            yield return FadeImage(logoImage, 1f, 0f, logoFadeOut);
            logoImage.gameObject.SetActive(false);
        }

        yield return new WaitForSeconds(waitBeforeMenu);

        StartMenuMusic();

        if (menuGroup != null)
            yield return FadeCanvasGroup(menuGroup, 0f, 1f, menuFadeIn);

        inputEnabled = true;

        if (menuGroup != null)
        {
            menuGroup.interactable = true;
            menuGroup.blocksRaycasts = true;
        }
    }

    void OnPlay()
    {
        Debug.Log("MainMenuFlow -> OnPlay()");
        if (!inputEnabled || clicked) return;
        clicked = true;

        StartCoroutine(FadeOutThen(() =>
        {
            SceneManager.LoadScene(playSceneName);
        }));
    }

    void OnQuit()
    {
        Debug.Log("MainMenuFlow -> OnQuit()");
        if (!inputEnabled || clicked) return;
        clicked = true;

        StartCoroutine(FadeOutThen(() =>
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }));
    }

    IEnumerator FadeOutThen(System.Action action)
    {
        // Bloqueja inputs immediatament
        inputEnabled = false;

        if (menuGroup != null)
        {
            menuGroup.interactable = false;
            menuGroup.blocksRaycasts = false;
        }

        // ✅ Assegura que el negre estigui a sobre i actiu
        if (blackOverlay != null)
        {
            blackOverlay.gameObject.SetActive(true);
            blackOverlay.transform.SetAsLastSibling();
            blackOverlay.raycastTarget = true;

            // Si vols que el menú també faci fade out
            if (fadeOutMenuToo && menuGroup != null)
            {
                // fem en paral·lel: menú baixa mentre el negre puja
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

    // ---------------- MUSIC ----------------
    void StartMenuMusic()
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

    IEnumerator FadeInMusic()
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

    // ---------------- HELPERS ----------------
    IEnumerator FadeImage(Image img, float from, float to, float duration)
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

    IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration)
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

    void SetImageAlpha(Image img, float a)
    {
        if (img == null) return;
        var c = img.color;
        c.a = a;
        img.color = c;
    }

    float GetImageAlpha(Image img)
    {
        if (img == null) return 0f;
        return img.color.a;
    }
}
