using System.Collections;
using System.Text;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Controlador didàctic d'introducció estil terminal retro (TerminalCutscene).
/// Aquest script recrea un emulador de terminal interactiu que simula la càrrega del sistema.
/// Suporta:
/// 1) Reproducció de vídeo preliminar (VideoPlayer) a pantalla completa de forma resistent a URP.
/// 2) Mostrar un logotip de Splash amb transicions de fos (fades) i de so.
/// 3) Impressió lletra per lletra del text, acompanyat de sons de tecleig de teclat mecànic.
/// 4) Cursor de bloc parpellejant.
/// 5) SISTEMA DE CORRUPCIÓ DIGITAL (GLITCH): Genera un efecte progressiu on el text és corromput
///    amb caràcters de control estranys (ignorant correctament les etiquetes HTML de color),
///    mentre el quadre de text tremola (jitter) i la seva opacitat parpelleja (flicker).
/// 6) Seqüència d'explosió final amb flaix blanc que es manté actiu durant la càrrega de la següent escena.
/// </summary>
public class TerminalCutscene : MonoBehaviour
{
    /// <summary>
    /// Struct de configuració individualitzada per a cada línia de la consola.
    /// </summary>
    [System.Serializable]
    public class TerminalLine
    {
        [TextArea(2, 6)] public string text; // Contingut textual que s'escriurà
        public bool response;               // Si és true, és una sortida del sistema. Si és false, simula tecleig d'usuari.
        public Color color = Color.white;   // Color de text aplicat de forma interna via Tags de TMP.
    }

    [Header("Interfície (UI)")]
    [SerializeField] private TMP_Text terminalText; // Quadre de text principal de TextMeshPro

    [Header("Vídeo d'Intro (Opcional)")]
    [Tooltip("Vídeo que es reproduirà fullscreen en entrar, abans del terminal.")]
    [SerializeField] private VideoClip introVideoClip;
    [Tooltip("Pausa de silenci en negre despres d'acabar el vídeo.")]
    [SerializeField] private float videoFadeDuration = 0.5f;

    [Header("Pantalla de Splash (Opcional)")]
    [SerializeField] private Image splashImage;      // Target gràfic per al splash
    [SerializeField] private Sprite splashSprite;    // Sprite logotip
    [SerializeField] private float splashFadeIn = 0.6f; 
    [SerializeField] private float splashHold = 1.2f;   
    [SerializeField] private float splashFadeOut = 0.6f; 
    [SerializeField] private Color splashTint = Color.white; 

    [Header("Canals d'Àudio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip splashSfx; // So en mostrar el Splash

    [Header("So d'inici de Fallada (Glitch)")]
    [SerializeField] private AudioClip glitchStartSfx;
    [Range(0f, 1f)][SerializeField] private float glitchStartVolume = 0.9f;

    [Header("Prompt d'Usuari (Ubuntu/Linux)")]
    [SerializeField] private string userPrompt = "user:~$";
    [SerializeField] private string promptSeparator = " ";
    [SerializeField] private Color promptColor = new Color(0.6f, 1f, 0.6f, 1f); // Verd brillant prompt

    [Header("Línies de Guió")]
    [SerializeField] private TerminalLine[] lines;

    [Header("Velocitat d'Escriptura")]
    [SerializeField] private float charsPerSecond = 45f; // Caràcters escrits per segon
    [SerializeField] private float linePause = 0.35f;     // Temps d'espera entre línies

    [Header("Disseny del Cursor")]
    [SerializeField] private string cursorChar = "█"; // Caràcter de bloc clàssic
    [SerializeField] private float cursorBlinkSeconds = 0.45f; // Freqüència del parpelleig

    [Header("Àudio del Tecleig (Tecla)")]
    [SerializeField] private AudioClip keyClick;
    [SerializeField] private int soundEveryNChars = 2; // Reprodueix so cada N caràcters per evitar saturació

    [Header("Destí Final")]
    [SerializeField] private string nextSceneName = "Zona_Test"; // Escena de joc a carregar
    [SerializeField] private float endPause = 0.8f;

    [Header("Àudio Transició d'Esclat")]
    [SerializeField] private AudioClip explosionSound;

    [Header("Sistema de Glitch Progressiu")]
    [Tooltip("A partir de quin índex de línia comença la corrupció (0-based). -1 per desactivat.")]
    [SerializeField] private int glitchStartLineIndex = -1;

    [Tooltip("Quant augmenta la intensitat de fallada amb cada nova línia.")]
    [SerializeField] private float glitchIncreasePerLine = 0.18f;

    [Range(0f, 1f)]
    [SerializeField] private float glitchMaxIntensity = 1f;

    [SerializeField] private string glitchChars = "#$%&*@!?/\\[]{}<>~^"; // Símbols de glitch
    [Range(0f, 0.35f)]
    [SerializeField] private float maxCorruptionRatio = 0.12f; // Proporció màxima de caràcters a corrompre

    [SerializeField] private float maxJitterPixels = 8f; // Força màxima de tremolor del quadre de text
    [SerializeField] private float glitchTickBase = 0.12f; // Temps base de tick de refresc de fallada

    // Paràmetres interns d'estat del glitch
    private float glitchIntensity = 0f;
    private bool glitchRunning = false;
    private bool glitchHasStartedOnce = false;
    private Coroutine glitchRoutine;
    private Vector2 baseAnchoredPos;
    private float baseTextAlpha = 1f;

    // Controladors de text i estat general
    private int charCount;
    private bool cursorOn = true;
    private bool skipping = false; // Permet saltar (Skip) prement Espai
    private bool promptAlreadyPrintedForNextUserLine = false;

    // Magatzem del text pur totalment net d'errors de codi (permet restaurar o corrompre sobre una base neta)
    private string cleanText = "";

    private void Awake()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        // Personalització didàctica del prompt: Si conté l'identificador fraviercaes04, el canviem nòvadament pel nom de sistema de l'usuari real!
        if (!string.IsNullOrEmpty(userPrompt))
        {
            userPrompt = userPrompt.Replace("fraviercaes04", System.Environment.UserName);
        }
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

        // Inicialització en silenci del splash
        if (splashImage != null)
        {
            splashImage.gameObject.SetActive(true);
            splashImage.sprite = splashSprite;
            splashImage.color = new Color(splashTint.r, splashTint.g, splashTint.b, 0f);
        }

        // Executem la línia temporal seqüencial del terminal
        StartCoroutine(RunSequence());
    }

    private void Update()
    {
        // En prémer espai, activem el mode Skip per accelerar la cinemàtica
        if (Input.GetKeyDown(KeyCode.Space))
            skipping = true;
    }

    /// <summary>
    /// Corrutina mestra que allotja la línia temporal dels esdeveniments (Video -> Splash -> Terminal -> Canvi d'escena).
    /// </summary>
    private IEnumerator RunSequence()
    {
        // 0) Reproducció de l'Intro en Vídeo (si s'ha assignat)
        if (introVideoClip != null)
        {
            yield return StartCoroutine(PlayVideoRoutine());
        }

        // 1) Pantalla de Splash
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

        // 2) Terminal de text actiu
        StartCoroutine(BlinkCursor()); 
        yield return PlayCutscene();
    }

    /// <summary>
    /// Corrutina robusta de reproducció de vídeo sobre un Canvas temporal d'Overlay
    /// independent de configuracions de càmera o renderers d'URP.
    /// </summary>
    private IEnumerator PlayVideoRoutine()
    {
        // Desactivem el Canvas original temporalment per no embrutar
        Canvas rootCanvas = terminalText != null ? terminalText.GetComponentInParent<Canvas>() : null;
        if (rootCanvas != null) rootCanvas.enabled = false;

        // Creem dinàmicament el Canvas temporal amb el rang superior de dibuix
        GameObject canvasGO = new GameObject("TempVideoCanvas");
        Canvas tempCanvas = canvasGO.AddComponent<Canvas>();
        tempCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        tempCanvas.sortingOrder = 999;
        
        // Fons negre sòlid darrere del vídeo
        GameObject bgGO = new GameObject("BlackBG");
        bgGO.transform.SetParent(canvasGO.transform, false);
        var bgRaw = bgGO.AddComponent<RawImage>();
        bgRaw.color = Color.black;
        var bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero; bgRT.offsetMax = Vector2.zero;

        // Finestra gràfica d'imatge on es bolcarà el buffer del VideoPlayer
        GameObject videoImgGO = new GameObject("VideoTarget");
        videoImgGO.transform.SetParent(canvasGO.transform, false);
        var rawImg = videoImgGO.AddComponent<RawImage>();
        rawImg.color = Color.white;
        var rRT = rawImg.GetComponent<RectTransform>();
        rRT.anchorMin = Vector2.zero; rRT.anchorMax = Vector2.one;
        rRT.offsetMin = Vector2.zero; rRT.offsetMax = Vector2.zero;

        // Inicialitzem el component VideoPlayer
        GameObject videoGO = new GameObject("IntroVideoPlayer");
        var vp = videoGO.AddComponent<VideoPlayer>();
        
        vp.playOnAwake = false;
        vp.clip = introVideoClip;
        
        // Mode API Only: Agafem de forma dinàmica la textura frame a frame, solucionant incompatibilitats d'URP/WebGL/EXE
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

        // Càrrega i preparació de vídeo a la targeta gràfica
        vp.Prepare();
        
        while (!vp.isPrepared && !skipping)
        {
            yield return null;
        }

        if (!skipping)
        {
            vp.Play();
            yield return null; 

            // Escriure la textura a l'element gràfic frame a frame mentre el vídeo s'estigui reproduint
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
        Destroy(canvasGO); 

        // Restaurem el Canvas principal de l'escena
        if (rootCanvas != null) rootCanvas.enabled = true;
        
        if (!skipping) yield return new WaitForSeconds(videoFadeDuration);
    }

    /// <summary>
    /// Corrutina de processament i escriptura línia a línia de la terminal, amb càlculs dinàmics de glitch.
    /// </summary>
    private IEnumerator PlayCutscene()
    {
        if (lines == null || lines.Length == 0)
        {
            yield return Wait(endPause);
            LoadNextScene();
            yield break;
        }

        for (int i = 0; i < lines.Length; i++)
        {
            // --- CONTROL DEL GLITCH TERMINAL ---
            if (glitchStartLineIndex >= 0 && i >= glitchStartLineIndex)
            {
                // Incrementem linealment la intensitat del glitch a cada línia addicional
                float target = Mathf.Clamp01((i - glitchStartLineIndex + 1) * glitchIncreasePerLine);
                glitchIntensity = Mathf.Min(glitchMaxIntensity, Mathf.Max(glitchIntensity, target));

                // Iniciem el bucle de corrupció visual un sol cop
                if (!glitchRunning)
                {
                    glitchRunning = true;
                    glitchRoutine = StartCoroutine(GlitchLoop());

                    // Reproduïm àudio sonor de fallada greu/trencament de sistema
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

            // Si la línia simula ser entrada de l'usuari, imprimim el prompt d'inici
            if (!l.response)
            {
                if (!promptAlreadyPrintedForNextUserLine && !string.IsNullOrWhiteSpace(userPrompt))
                {
                    AppendColored(userPrompt, promptColor);
                    AppendRaw(promptSeparator);
                }
                promptAlreadyPrintedForNextUserLine = false;
            }

            // Impressió de contingut estàndard o tecleig lletra a lletra
            if (l.response)
            {
                AppendColored(l.text, l.color); // Les sortides de codi del sistema s'escriuen de cop
            }
            else
            {
                yield return TypeLineColored(l.text, l.color); // L'input d'usuari es tecleja
            }

            AppendRaw("\n");

            // Si hem d'imprimir prompt de forma anticipada pel següent frame
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

        // Si tenim fallada, donem un pic final màxim abans del flaix
        if (glitchRunning)
        {
            glitchIntensity = 1f;
            yield return new WaitForSeconds(0.25f);
        }

        // Flaix blanc amb persistència sonora a la següent escena
        yield return StartCoroutine(ExplosionFlashRoutine());
        
        LoadNextScene();
    }

    /// <summary>
    /// Genera la transició elàstica d'explosió a la pantalla (Flaix blanc persistent a nivell de DontDestroyOnLoad).
    /// </summary>
    private IEnumerator ExplosionFlashRoutine()
    {
        GameObject canvasGO = new GameObject("ExplosionCanvasTransition");
        DontDestroyOnLoad(canvasGO); // IMPEDEIX queUnity el destrueixi en descarregar l'escena!

        Canvas tempCanvas = canvasGO.AddComponent<Canvas>();
        tempCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        tempCanvas.sortingOrder = 9999; 

        GameObject whiteGO = new GameObject("WhiteFlash");
        whiteGO.transform.SetParent(canvasGO.transform, false);
        var img = whiteGO.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0f);
        var rRT = whiteGO.GetComponent<RectTransform>();
        rRT.anchorMin = Vector2.zero; rRT.anchorMax = Vector2.one;
        rRT.offsetMin = Vector2.zero; rRT.offsetMax = Vector2.zero;

        float explosionDuration = 0.5f;

        if (audioSource != null)
        {
            audioSource.Stop();
        }

        // Aturem de forma mútua qualsevol emissor musical d'ambient
        var sceneMusic = FindFirstObjectByType<SceneMusic>();
        if (sceneMusic != null) sceneMusic.StopMusic();
        
        var loopMusic = FindFirstObjectByType<TriggerMusicLoopSection2D>();
        if (loopMusic != null) StartCoroutine(loopMusic.FadeOutAndStop(0.1f));

        if (audioSource != null && explosionSound != null)
        {
            audioSource.PlayOneShot(explosionSound);
            explosionDuration = explosionSound.length;
        }

        // Piquem a blanc immediat (0.05s)
        float flashInDuration = 0.05f;
        float elapsedF = 0f;
        while (elapsedF < flashInDuration)
        {
            elapsedF += Time.deltaTime;
            img.color = new Color(1f, 1f, 1f, elapsedF / flashInDuration);
            yield return null;
        }
        img.color = Color.white;

        // Deixem sonar l'esclat abans de fer el canvi d'escena físic
        yield return new WaitForSeconds(Mathf.Max(0.1f, explosionDuration - flashInDuration));

        // Assignem l'script encarregat de fer el fosa a negre automàticament en aparèixer a la nova escena
        canvasGO.AddComponent<WhiteFlashFadeOut>();
    }

    /// <summary>
    /// Corrutina didàctica d'impressió incremental enriquida amb tags HTML i so de tecleig dinàmic.
    /// </summary>
    private IEnumerator TypeLineColored(string line, Color color)
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

                // Reproduïm so d'escriptura (clic) a intervals regulars si no és espai en blanc
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

    private void AppendColored(string s, Color c)
    {
        AppendRaw(ColorTagOpen(c) + s + "</color>");
    }

    private string ColorTagOpen(Color c)
    {
        string hex = ColorUtility.ToHtmlStringRGB(c);
        return $"<color=#{hex}>";
    }

    private void AppendRaw(string s)
    {
        cleanText += s;
        terminalText.text = cleanText + (cursorOn ? cursorChar : "");
    }

    /// <summary>
    /// Bucle infinit de parpelleig del cursor de terminal.
    /// </summary>
    private IEnumerator BlinkCursor()
    {
        while (true)
        {
            cursorOn = !cursorOn;
            terminalText.text = cleanText + (cursorOn ? cursorChar : "");
            yield return new WaitForSeconds(cursorBlinkSeconds);
        }
    }

    private IEnumerator FadeImage(Image img, float from, float to, float duration)
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

    private IEnumerator Wait(float seconds)
    {
        if (!skipping) yield return new WaitForSeconds(seconds);
    }

    // =========================================================================
    // IMPLEMENTACIÓ DELS GLITCHES I CORRUPCIÓ DIGITAL
    // =========================================================================

    private IEnumerator GlitchLoop()
    {
        while (glitchRunning)
        {
            // A major intensitat, els ticks ocorren més ràpid (frenetisme de fallada)
            float tick = Mathf.Lerp(glitchTickBase, 0.02f, glitchIntensity);

            ApplyJitter(glitchIntensity);
            ApplyFlicker(glitchIntensity);
            ApplyCorruption(glitchIntensity);

            yield return new WaitForSeconds(tick);
        }
    }

    /// <summary>
    /// Aplica una sacsejada de vibració física del quadre de text de la terminal.
    /// </summary>
    private void ApplyJitter(float intensity)
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

    /// <summary>
    /// Modifica de forma erràtica l'opacitat del text de console per simular llum de monitor inestable.
    /// </summary>
    private void ApplyFlicker(float intensity)
    {
        float chance = Mathf.Lerp(0f, 0.25f, intensity);

        var c = terminalText.color;
        if (Random.value < chance)
            c.a = Mathf.Clamp01(Random.Range(0.35f, 0.95f));
        else
            c.a = baseTextAlpha;

        terminalText.color = c;
    }

    /// <summary>
    /// Reemplaça caràcters de lletres reals per símbols de soroll/glitch, assegurant que no trenquem
    /// les etiquetes del parser XML internes de color (<color=#HEX>...</color>) de TextMeshPro.
    /// </summary>
    private void ApplyCorruption(float intensity)
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

        // Calculem quants caràcters corromprem segons proporció d'intensitat
        int targetChanges = Mathf.Clamp(Mathf.RoundToInt(len * ratio), 1, Mathf.Max(1, len / 8));

        StringBuilder sb = new StringBuilder(baseText);
        int changes = 0;
        int guard = 0;

        // Bucle amb guardacostes contra bloqueig de fils (len * 10)
        while (changes < targetChanges && guard < len * 10)
        {
            guard++;
            int idx = Random.Range(0, len);

            // IMPORTANT: Protegim els tags XML perquè no surtin errors gràfics a la pantalla
            if (IsIndexInsideTag(baseText, idx)) continue;

            char current = sb[idx];
            if (current == '\n' || current == '\r' || current == ' ') continue;

            // Reemplacem per un símbol aleatori de la llista de glitch
            sb[idx] = glitchChars[Random.Range(0, glitchChars.Length)];
            changes++;
        }

        terminalText.text = sb.ToString() + (cursorOn ? cursorChar : "");
    }

    /// <summary>
    /// Comprova de forma seqüencial si un determinat índex del text es troba dins dels límits d'un tag d'obertura/tancament (< >).
    /// </summary>
    private bool IsIndexInsideTag(string text, int index)
    {
        int safeIndex = Mathf.Clamp(index, 0, text.Length - 1);

        int lastOpen = text.LastIndexOf('<', safeIndex);
        if (lastOpen == -1) return false;

        int lastClose = text.LastIndexOf('>', safeIndex);
        return lastOpen > lastClose;
    }

    /// <summary>
    /// Neteja els bucles de fallades actius, restaura els valors normals de text i crida SceneManager.
    /// </summary>
    private void LoadNextScene()
    {
        glitchRunning = false;
        if (glitchRoutine != null) StopCoroutine(glitchRoutine);

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
