using System.Collections;
using System.Text;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

public class TerminalCutscene : MonoBehaviour
{
    [System.Serializable]
    public class TerminalLine
    {
        [TextArea(2, 6)] public string text; // El text que s'escriurà al terminal.
        public bool response;               // Si és una resposta del sistema o entrada d'usuari.
        public Color color = Color.white;   // El color del text d'aquesta línia.
    }

    [Header("UI")]
    [SerializeField] private TMP_Text terminalText;

    [Header("Video Intro (Opcional)")]
    [Tooltip("Vídeo que es reproduirà fullscreen en entrar, abans del terminal.")]
    [SerializeField] private VideoClip introVideoClip;
    [Tooltip("Petita pausa o fosa en negre just després que s'acabi el vídeo.")]
    [SerializeField] private float videoFadeDuration = 0.5f;

    [Header("Splash (abans del terminal, després del vídeo)")]
    [SerializeField] private Image splashImage;      // Referència a la imatge de splash.
    [SerializeField] private Sprite splashSprite;    // L'sprite que es mostrarà.
    [SerializeField] private float splashFadeIn = 0.6f; // Temps d'aparició.
    [SerializeField] private float splashHold = 1.2f;   // Temps que es manté la imatge.
    [SerializeField] private float splashFadeOut = 0.6f; // Temps de desaparició.
    [SerializeField] private Color splashTint = Color.white; // Tint de la imatge.

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip splashSfx;

    [Header("Glitch Audio (1 cop quan comença)")]
    [SerializeField] private AudioClip glitchStartSfx;
    [Range(0f, 1f)][SerializeField] private float glitchStartVolume = 0.9f;

    [Header("Prompt (Ubuntu)")]
    [SerializeField] private string userPrompt = "user:~$";
    [SerializeField] private string promptSeparator = " ";
    [SerializeField] private Color promptColor = new Color(0.6f, 1f, 0.6f, 1f);

    [Header("Lines")]
    [SerializeField] private TerminalLine[] lines;

    [Header("Typing")]
    [SerializeField] private float charsPerSecond = 45f;
    [SerializeField] private float linePause = 0.35f;

    [Header("Cursor")]
    [SerializeField] private string cursorChar = "█";
    [SerializeField] private float cursorBlinkSeconds = 0.45f;

    [Header("Key Sound (optional)")]
    [SerializeField] private AudioClip keyClick;
    [SerializeField] private int soundEveryNChars = 2;

    [Header("Finish")]
    [SerializeField] private string nextSceneName = "Zona_Test";
    [SerializeField] private float endPause = 0.8f;

    // ---------------- GLITCH ----------------
    [Header("Glitch (progressiu)")]
    [Tooltip("A partir d'aquest índex de línia (0-based), s'activa el glitch.")]
    [SerializeField] private int glitchStartLineIndex = -1;

    [Tooltip("Quant augmenta la intensitat per línia després de començar.")]
    [SerializeField] private float glitchIncreasePerLine = 0.18f;

    [Range(0f, 1f)]
    [SerializeField] private float glitchMaxIntensity = 1f;

    [SerializeField] private string glitchChars = "#$%&*@!?/\\[]{}<>~^";
    [Range(0f, 0.35f)]
    [SerializeField] private float maxCorruptionRatio = 0.12f;

    [SerializeField] private float maxJitterPixels = 8f;
    [SerializeField] private float glitchTickBase = 0.12f;

    // intern glitch
    private float glitchIntensity = 0f;
    private bool glitchRunning = false;
    private bool glitchHasStartedOnce = false;
    private Coroutine glitchRoutine;
    private Vector2 baseAnchoredPos;
    private float baseTextAlpha = 1f;

    // intern general
    private int charCount;
    private bool cursorOn = true;
    private bool skipping = false;
    private bool promptAlreadyPrintedForNextUserLine = false;

    // text net (sense corrupció)
    private string cleanText = "";

    private void Awake()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
    }

    private void Start()
    {
        if (terminalText == null)
        {
            Debug.LogError("TerminalCutscene: terminalText NO assignat a l'Inspector.");
            return;
        }

        terminalText.text = "";
        cleanText = "";

        baseAnchoredPos = terminalText.rectTransform.anchoredPosition;
        baseTextAlpha = terminalText.color.a;

        // Inicialitza splash
        if (splashImage != null)
        {
            splashImage.gameObject.SetActive(true);
            splashImage.sprite = splashSprite;
            splashImage.color = new Color(splashTint.r, splashTint.g, splashTint.b, 0f);
        }

        // Iniciem la seqüència principal de la cutscene
        StartCoroutine(RunSequence());
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            skipping = true;
    }

    IEnumerator RunSequence()
    {
        // 0) Reproducció del Vídeo Intro
        if (introVideoClip != null)
        {
            yield return StartCoroutine(PlayVideoRoutine());
        }

        // 1) Splash
        if (splashImage != null && splashSprite != null)
        {
            if (splashSfx != null && audioSource != null)
                audioSource.PlayOneShot(splashSfx, 1f);

            yield return FadeImage(splashImage, 0f, 1f, splashFadeIn);
            yield return Wait(splashHold);
            yield return FadeImage(splashImage, 1f, 0f, splashFadeOut);

            splashImage.gameObject.SetActive(false);
        }
        else
        {
            if (splashImage != null) splashImage.gameObject.SetActive(false);
        }

        // 2) Terminal
        StartCoroutine(BlinkCursor()); // Iniciem el parpelleig del cursor

        // Iniciem la impressió de les línies del terminal
        yield return PlayCutscene();
    }

    IEnumerator PlayVideoRoutine()
    {
        // Amaguem el Canvas original perquè no destorbi
        Canvas rootCanvas = terminalText != null ? terminalText.GetComponentInParent<Canvas>() : null;
        if (rootCanvas != null) rootCanvas.enabled = false;

        // Creem un Canvas temporal a prova de bombes (Overlay Topmost) independent de la càmera/URP
        GameObject canvasGO = new GameObject("TempVideoCanvas");
        Canvas tempCanvas = canvasGO.AddComponent<Canvas>();
        tempCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        tempCanvas.sortingOrder = 999;
        
        // Fons negre absolut darrere el vídeo
        GameObject bgGO = new GameObject("BlackBG");
        bgGO.transform.SetParent(canvasGO.transform, false);
        var bgRaw = bgGO.AddComponent<RawImage>();
        bgRaw.color = Color.black;
        var bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero; bgRT.offsetMax = Vector2.zero;

        // La imatge on es projectarà el vídeo
        GameObject videoImgGO = new GameObject("VideoTarget");
        videoImgGO.transform.SetParent(canvasGO.transform, false);
        var rawImg = videoImgGO.AddComponent<RawImage>();
        rawImg.color = Color.white;
        var rRT = rawImg.GetComponent<RectTransform>();
        rRT.anchorMin = Vector2.zero; rRT.anchorMax = Vector2.one;
        rRT.offsetMin = Vector2.zero; rRT.offsetMax = Vector2.zero;

        GameObject videoGO = new GameObject("IntroVideoPlayer");
        var vp = videoGO.AddComponent<VideoPlayer>();
        
        vp.playOnAwake = false;
        vp.clip = introVideoClip;
        
        // Modalitat API Only: Agafem els fotogrames manualment, molt més estable en URP
        vp.renderMode = VideoRenderMode.APIOnly; 
        vp.isLooping = false;
        
        if (audioSource != null)
        {
            vp.audioOutputMode = VideoAudioOutputMode.AudioSource;
            vp.SetTargetAudioSource(0, audioSource);
        }
        else
        {
            vp.audioOutputMode = VideoAudioOutputMode.Direct;
        }

        vp.Prepare();
        
        // Esperem fins que el disc/pista estigui llest a la memòria per evitar lag strikes
        while (!vp.isPrepared && !skipping)
        {
            yield return null;
        }

        if (!skipping)
        {
            vp.Play();
            yield return null; // assepare que Play fa un pas

            // Esperar que el vídeo acabi per complet connectant textures en temps real
            while (vp.isPlaying && !skipping)
            {
                if (vp.texture != null)
                {
                    rawImg.texture = vp.texture;
                }
                yield return null;
            }
        }

        vp.Stop();
        Destroy(videoGO);
        Destroy(canvasGO); // Netegem la pantalla negra i la interfície temporal

        // Tornem a activar la interfície del text/Splash quan ha passat el vídeo
        if (rootCanvas != null) rootCanvas.enabled = true;
        
        if (!skipping) yield return new WaitForSeconds(videoFadeDuration);
    }

    IEnumerator PlayCutscene()
    {
        if (lines == null || lines.Length == 0)
        {
            yield return Wait(endPause);
            LoadNextScene();
            yield break;
        }

        for (int i = 0; i < lines.Length; i++)
        {
            // --- activar/pujar glitch segons línia ---
            if (glitchStartLineIndex >= 0 && i >= glitchStartLineIndex)
            {
                // intensitat puja per línia
                float target = Mathf.Clamp01((i - glitchStartLineIndex + 1) * glitchIncreasePerLine);
                glitchIntensity = Mathf.Min(glitchMaxIntensity, Mathf.Max(glitchIntensity, target));

                // arrencar glitch loop 1 cop
                if (!glitchRunning)
                {
                    glitchRunning = true;
                    glitchRoutine = StartCoroutine(GlitchLoop());

                    // ✅ audio quan comença el glitch (1 cop)
                    if (!glitchHasStartedOnce && glitchStartSfx != null && audioSource != null)
                    {
                        glitchHasStartedOnce = true;
                        audioSource.PlayOneShot(glitchStartSfx, glitchStartVolume);
                    }
                }
            }

            TerminalLine l = lines[i];
            bool hasNext = (i < lines.Length - 1);
            bool nextIsUser = hasNext && (lines[i + 1] != null) && (!lines[i + 1].response);

            // Prompt abans de línies d’usuari (si no l’hem imprès ja “per avançat”)
            if (!l.response)
            {
                if (!promptAlreadyPrintedForNextUserLine && !string.IsNullOrWhiteSpace(userPrompt))
                {
                    AppendColored(userPrompt, promptColor);
                    AppendRaw(promptSeparator);
                }
                promptAlreadyPrintedForNextUserLine = false;
            }

            // Escriure línia
            if (l.response)
            {
                AppendColored(l.text, l.color);
            }
            else
            {
                yield return TypeLineColored(l.text, l.color);
            }

            AppendRaw("\n");

            // Si l'actual és response i la següent és user: prompt immediat + pausa normal
            if (l.response && nextIsUser && !string.IsNullOrWhiteSpace(userPrompt))
            {
                AppendColored(userPrompt, promptColor);
                AppendRaw(promptSeparator);
                promptAlreadyPrintedForNextUserLine = true;

                yield return Wait(linePause);
                continue;
            }

            yield return Wait(linePause);
        }

        yield return Wait(endPause);

        // spike final curt si hi ha glitch actiu
        if (glitchRunning)
        {
            glitchIntensity = 1f;
            yield return new WaitForSeconds(0.25f);
        }

        LoadNextScene();
    }

    IEnumerator TypeLineColored(string line, Color color)
    {
        string open = ColorTagOpen(color);
        string close = "</color>";

        AppendRaw(open);

        if (skipping)
        {
            AppendRaw(line);
        }
        else
        {
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                AppendRaw(c.ToString());

                if (!char.IsWhiteSpace(c))
                {
                    charCount++;
                    if (audioSource != null && keyClick != null && (charCount % soundEveryNChars == 0))
                        audioSource.PlayOneShot(keyClick, 0.6f);
                }

                yield return Wait(1f / Mathf.Max(1f, charsPerSecond));
            }
        }

        AppendRaw(close);
    }

    void AppendColored(string s, Color c)
    {
        AppendRaw(ColorTagOpen(c) + s + "</color>");
    }

    string ColorTagOpen(Color c)
    {
        string hex = ColorUtility.ToHtmlStringRGB(c);
        return $"<color=#{hex}>";
    }

    void AppendRaw(string s)
    {
        // Guardem text net (inclou tags de color)
        cleanText += s;

        // Render base (el glitch loop pot sobreescriure el text temporalment)
        terminalText.text = cleanText + (cursorOn ? cursorChar : "");
    }

    IEnumerator BlinkCursor()
    {
        while (true)
        {
            cursorOn = !cursorOn;
            // si hi ha glitch actiu, deixem que el glitch escrigui; però per simplicitat actualitzem igual
            terminalText.text = cleanText + (cursorOn ? cursorChar : "");
            yield return new WaitForSeconds(cursorBlinkSeconds);
        }
    }

    IEnumerator FadeImage(Image img, float from, float to, float duration)
    {
        if (img == null) yield break;

        if (skipping || duration <= 0.001f)
        {
            img.color = new Color(splashTint.r, splashTint.g, splashTint.b, to);
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(from, to, t / duration);
            img.color = new Color(splashTint.r, splashTint.g, splashTint.b, a);
            yield return null;
        }

        img.color = new Color(splashTint.r, splashTint.g, splashTint.b, to);
    }

    IEnumerator Wait(float seconds)
    {
        if (!skipping) yield return new WaitForSeconds(seconds);
    }

    // ---------------- GLITCH LOOP ----------------
    IEnumerator GlitchLoop()
    {
        while (glitchRunning)
        {
            float tick = Mathf.Lerp(glitchTickBase, 0.02f, glitchIntensity);

            ApplyJitter(glitchIntensity);
            ApplyFlicker(glitchIntensity);
            ApplyCorruption(glitchIntensity);

            yield return new WaitForSeconds(tick);
        }
    }

    void ApplyJitter(float intensity)
    {
        float amp = maxJitterPixels * intensity;
        if (amp <= 0.01f)
        {
            terminalText.rectTransform.anchoredPosition = baseAnchoredPos;
            return;
        }

        float x = Random.Range(-amp, amp);
        float y = Random.Range(-amp, amp);
        terminalText.rectTransform.anchoredPosition = baseAnchoredPos + new Vector2(x, y);
    }

    void ApplyFlicker(float intensity)
    {
        float chance = Mathf.Lerp(0f, 0.25f, intensity);

        var c = terminalText.color;
        if (Random.value < chance)
            c.a = Mathf.Clamp01(Random.Range(0.35f, 0.95f));
        else
            c.a = baseTextAlpha;

        terminalText.color = c;
    }

    void ApplyCorruption(float intensity)
    {
        float ratio = Mathf.Lerp(0f, maxCorruptionRatio, intensity);
        if (ratio <= 0.001f)
        {
            terminalText.text = cleanText + (cursorOn ? cursorChar : "");
            return;
        }

        string baseText = cleanText;
        int len = baseText.Length;
        if (len == 0)
        {
            terminalText.text = (cursorOn ? cursorChar : "");
            return;
        }

        int targetChanges = Mathf.Clamp(Mathf.RoundToInt(len * ratio), 1, Mathf.Max(1, len / 8));

        StringBuilder sb = new StringBuilder(baseText);
        int changes = 0;
        int guard = 0;

        while (changes < targetChanges && guard < len * 10)
        {
            guard++;
            int idx = Random.Range(0, len);

            if (IsIndexInsideTag(baseText, idx)) continue;

            char current = sb[idx];
            if (current == '\n' || current == '\r' || current == ' ') continue;

            sb[idx] = glitchChars[Random.Range(0, glitchChars.Length)];
            changes++;
        }

        terminalText.text = sb.ToString() + (cursorOn ? cursorChar : "");
    }

    bool IsIndexInsideTag(string text, int index)
    {
        int safeIndex = Mathf.Clamp(index, 0, text.Length - 1);

        int lastOpen = text.LastIndexOf('<', safeIndex);
        if (lastOpen == -1) return false;

        int lastClose = text.LastIndexOf('>', safeIndex);
        return lastOpen > lastClose;
    }

    void LoadNextScene()
    {
        // parar glitch
        glitchRunning = false;
        if (glitchRoutine != null) StopCoroutine(glitchRoutine);

        // restaurar visuals
        if (terminalText != null)
        {
            terminalText.rectTransform.anchoredPosition = baseAnchoredPos;

            var c = terminalText.color;
            c.a = baseTextAlpha;
            terminalText.color = c;
        }

        Debug.Log("Loading scene: " + nextSceneName);

        if (!Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            Debug.LogError($"No puc carregar '{nextSceneName}'. No està a Scenes In Build o el nom no coincideix.");
            return;
        }

        SceneManager.LoadScene(nextSceneName);
    }
}
