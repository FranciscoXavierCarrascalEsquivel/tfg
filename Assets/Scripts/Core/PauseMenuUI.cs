using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// Controlador didàctic del Menú de Pausa (PauseMenuUI).
/// Aquest component de presentació Premium es genera completament per codi quan l'usuari prem ESC.
/// Característiques:
/// 1) Atura completament el flux temporal del joc (Time.timeScale = 0f) i congela els controls del jugador.
/// 2) Construeix de forma procedimental a nivell d'UI tot el panell de pausa (fons transparent fosc,
///    panell amb vores arrodonides gràfiques, contorn daurat, botons estil píxel i la fletxa selectora ">").
/// 3) Implementa animacions físiques de lliscament elàstic (intro/outro) i de flotació sinusoïdal
///    estil "bobbing" continu per a fer l'entorn més dinàmic i viu.
/// 4) Suporta navegació bidireccional per teclat (WASD / Fletxes) amb so, i una pantalla de confirmació de seguretat
///    ("EXIT GAME?") per evitar pèrdues de progrés accidentals.
/// </summary>
public class PauseMenuUI : MonoBehaviour
{
    public static bool IsOpen { get; private set; } // Flag global que indica si la pausa està activa

    [Header("Paleta Estètica")]
    [SerializeField] private Color overlayColor = new Color(0f, 0f, 0f, 0.75f); // Color fosc translúcid de fons
    [SerializeField] private Color panelColor = new Color(0.05f, 0.05f, 0.1f, 1f); // Color del panell interior (blau fosc)
    [SerializeField] private Color accentColor = new Color(0.95f, 0.8f, 0.15f, 1f); // Color daurat de ressaltat

    private bool wasPlayerLocked = false; // Desa l'estat previ del jugador per a restaurar-lo correctament
    private bool isConfirming = false;     // Controla si s'està mostrant el submenú de confirmació de sortida
    private TextMeshProUGUI titleTxt;      // Text del títol del panell
    private Image fadeOverlay;             // Overlay negra per al tancament de sortida

    private GameObject rootGO;
    private RectTransform panelRT;
    private int selectedIndex = 0; // Control de selecció (0: Resume, 1: Exit)
    private Image[] buttonBgs = new Image[2];
    private TextMeshProUGUI[] buttonTexts = new TextMeshProUGUI[2];

    private TextMeshProUGUI selectorArrow; // Fletxa selectora bidimensional gràfica
    private float floatTimer = 0f; // Comptador de temps per a l'efecte flotació

    private static PauseMenuUI instance;

    /// <summary>
    /// Genera i mostra dinàmicament la interfície del Menú de Pausa.
    /// </summary>
    public static void Show()
    {
        if (instance != null) return;

        var canvas = CanvasHelper.GetMainCanvas();
        if (canvas == null)
        {
            // Canvas temporal de seguretat si no existia cap principal
            var canGO = new GameObject("PauseCanvas");
            canvas = canGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;
            canGO.AddComponent<CanvasScaler>();
            canGO.AddComponent<GraphicRaycaster>();
        }

        var go = new GameObject("PauseMenuUI");
        go.transform.SetParent(canvas.transform, false);
        instance = go.AddComponent<PauseMenuUI>();
        instance.Build();
        
        // Bloquegem el moviment del jugador
        var player = FindFirstObjectByType<PlayerController2D>();
        if (player != null)
        {
            instance.wasPlayerLocked = player.IsMovementLocked;
            player.LockMovement();
        }

        IsOpen = true;
        Time.timeScale = 0f; // Congelem el rellotge de físiques i lògiques del joc!
    }

    /// <summary>
    /// Construeix tota la jerarquia d'elements d'interfície gràfica de forma dinàmica a la memòria.
    /// </summary>
    private void Build()
    {
        // 1. Overlay (Fons transparent fosc que cobreix el joc)
        rootGO = gameObject;
        var rt = rootGO.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        
        var overlayImg = rootGO.AddComponent<Image>();
        overlayImg.color = overlayColor;

        // 2. Panell de Contingut
        var pGO = new GameObject("Panel");
        pGO.transform.SetParent(transform, false);
        panelRT = pGO.AddComponent<RectTransform>();
        panelRT.sizeDelta = new Vector2(400f, 350f);
        panelRT.anchoredPosition = new Vector2(0, -1000f); // Comença a baix (fora de pantalla) per a l'animació d'entrada

        var pImg = pGO.AddComponent<Image>();
        pImg.color = panelColor;
        pImg.sprite = GetRoundedSprite(); // Cantonades arrodonides pixel-art
        pImg.type = Image.Type.Sliced;

        var outline = pGO.AddComponent<Outline>();
        outline.effectColor = accentColor;
        outline.effectDistance = new Vector2(4f, -4f);

        // 3. Títol
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(panelRT, false);
        var titleRT = titleGO.AddComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0, 0.75f); titleRT.anchorMax = new Vector2(1, 1);
        titleRT.offsetMin = titleRT.offsetMax = Vector2.zero;
        titleTxt = titleGO.AddComponent<TextMeshProUGUI>();
        SetFont(titleTxt, 42f, accentColor, FontStyles.Bold, TextAlignmentOptions.Center);
        titleTxt.text = "PAUSED";

        // 4. Botons de Control
        CreateButton(0, "Resume", new Vector2(0, 20f));
        CreateButton(1, "Exit to Menu", new Vector2(0, -60f));

        // 5. Fletxa Selectora
        var arrowGO = new GameObject("Selector");
        arrowGO.transform.SetParent(panelRT, false);
        var arrowRT = arrowGO.AddComponent<RectTransform>();
        arrowRT.sizeDelta = new Vector2(50f, 50f);
        selectorArrow = arrowGO.AddComponent<TextMeshProUGUI>();
        SetFont(selectorArrow, 32f, accentColor, FontStyles.Bold, TextAlignmentOptions.Center);
        selectorArrow.text = ">";

        // 6. Imatge fosca de fosa de sortida
        var fGO = new GameObject("FadeOverlay");
        fGO.transform.SetParent(transform, false);
        var fRT = fGO.AddComponent<RectTransform>();
        fRT.anchorMin = Vector2.zero; fRT.anchorMax = Vector2.one;
        fRT.offsetMin = fRT.offsetMax = Vector2.zero;
        fadeOverlay = fGO.AddComponent<Image>();
        fadeOverlay.color = new Color(0,0,0,0);
        fadeOverlay.raycastTarget = false;

        // Arrenquem l'animació de lliscament d'entrada
        StartCoroutine(IntroAnim());
    }

    private void CreateButton(int index, string label, Vector2 pos)
    {
        var btnGO = new GameObject($"Button_{index}");
        btnGO.transform.SetParent(panelRT, false);
        var rt = btnGO.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(300f, 60f);
        rt.anchoredPosition = pos;

        buttonBgs[index] = btnGO.AddComponent<Image>();
        buttonBgs[index].color = new Color(0.15f, 0.15f, 0.25f, 1f);
        buttonBgs[index].sprite = GetRoundedSprite();
        buttonBgs[index].type = Image.Type.Sliced;

        var txtGO = new GameObject("Text");
        txtGO.transform.SetParent(rt, false);
        var txtRT = txtGO.AddComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero; txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = txtRT.offsetMax = Vector2.zero;
        buttonTexts[index] = txtGO.AddComponent<TextMeshProUGUI>();
        SetFont(buttonTexts[index], 28f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
        buttonTexts[index].text = label;
    }

    /// <summary>
    /// Corrutina de lliscament elàstic cap a dalt del panell en obrir-se la pausa.
    /// </summary>
    private IEnumerator IntroAnim()
    {
        float elapsed = 0f;
        float dur = 0.3f;
        Vector2 start = new Vector2(0, -1000f);
        Vector2 target = Vector2.zero;

        // Use unscaledDeltaTime per a funcionar amb el joc pausat!
        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / dur;
            // Corba d'amortiguament cúbic invers
            float ease = 1f - Mathf.Pow(1f - t, 3f);
            panelRT.anchoredPosition = Vector2.Lerp(start, target, ease);
            yield return null;
        }
        panelRT.anchoredPosition = target;
    }

    private void Update()
    {
        // --- EFECTE DE FLOTACIÓ SINUSOÏDAL CONTINU (BOBBING) ---
        floatTimer += Time.unscaledDeltaTime;
        float yOffset = Mathf.Sin(floatTimer * 3f) * 10f; // Amplitud màxima de 10 píxels
        panelRT.anchoredPosition = new Vector2(0, yOffset);

        // Inputs: Tecla de cancel·lació / retorn
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isConfirming) 
            {
                PlaySelectSound();
                CancelExit();
            }
            else 
            {
                PlaySelectSound();
                Resume();
            }
            return;
        }

        // Inputs: Navegació vertical
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
        {
            selectedIndex = (selectedIndex - 1 + 2) % 2;
            PlayNavSound();
            UpdateHighlights();
        }
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
        {
            selectedIndex = (selectedIndex + 1) % 2;
            PlayNavSound();
            UpdateHighlights();
        }

        // Inputs: Acció d'execució/confirmació
        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
        {
            PlaySelectSound();
            if (isConfirming)
            {
                if (selectedIndex == 0) CancelExit();
                else StartCoroutine(ExitToMenuRoutine());
            }
            else
            {
                if (selectedIndex == 0) Resume();
                else AskForExit();
            }
        }

        UpdateHighlights();
    }

    /// <summary>
    /// Actualitza visualment els colors de ressaltat daurat/vermell de les opcions del menú,
    /// fa desplaçar verticalment la fletxa selectora ">" a la seva posició i afegeix un micro-moviment de text.
    /// </summary>
    private void UpdateHighlights()
    {
        for (int i = 0; i < 2; i++)
        {
            bool sel = (i == selectedIndex);
            if (isConfirming)
            {
                // Vermell si està demanant confirmació per a sortir
                buttonBgs[i].color = sel ? new Color(0.5f, 0.2f, 0.2f, 1f) : new Color(0.15f, 0.15f, 0.25f, 1f);
            }
            else
            {
                // Blau ressaltat normal
                buttonBgs[i].color = sel ? new Color(0.3f, 0.3f, 0.5f, 1f) : new Color(0.15f, 0.15f, 0.25f, 1f);
            }
            buttonTexts[i].color = sel ? accentColor : Color.white;
            buttonTexts[i].rectTransform.anchoredPosition = sel ? new Vector2(0, -2f) : Vector2.zero;

            if (sel)
            {
                // Alineem la fletxa a l'esquerra del botó triat
                var targetPos = buttonBgs[i].rectTransform.anchoredPosition;
                targetPos.x -= 180f; 
                selectorArrow.rectTransform.anchoredPosition = targetPos;
            }
        }
    }

    /// <summary>
    /// Modifica les etiquetes del panell per obrir el submenú de confirmació per a sortir a Jugar.
    /// </summary>
    private void AskForExit()
    {
        isConfirming = true;
        titleTxt.text = "EXIT GAME?";
        titleTxt.color = new Color(1f, 0.4f, 0.4f); // Vermell d'alerta
        buttonTexts[0].text = "No, stay";
        buttonTexts[1].text = "Yes, exit";
        selectedIndex = 0; // Triem per defecte quedar-nos per seguretat
        UpdateHighlights();
    }

    /// <summary>
    /// Restableix els textos normals del menú de pausa principal.
    /// </summary>
    private void CancelExit()
    {
        isConfirming = false;
        titleTxt.text = "PAUSED";
        titleTxt.color = accentColor;
        buttonTexts[0].text = "Resume";
        buttonTexts[1].text = "Exit to Menu";
        selectedIndex = 1; 
        UpdateHighlights();
    }

    /// <summary>
    /// Tanca la pausa, fa l'animació de sortida cap a sota i torna a engegar el rellotge físic.
    /// </summary>
    public void Resume()
    {
        StartCoroutine(ResumeRoutine());
    }

    private IEnumerator ResumeRoutine()
    {
        yield return StartCoroutine(OutroAnim());
        
        Time.timeScale = 1f; // Reactivem el temps temporal global del joc!

        var player = FindFirstObjectByType<PlayerController2D>();
        var dialogUI = FindFirstObjectByType<DialogueUI>();
        
        // Només retornem el moviment al jugador si no estava prèviament bloquejat per un diàleg actiu de fons
        if (player != null && !wasPlayerLocked && (dialogUI == null || !dialogUI.IsOpen))
        {
            player.UnlockMovement();
        }

        IsOpen = false;
        instance = null;
        Destroy(gameObject);
    }

    /// <summary>
    /// Llisca ràpidament el panell cap avall en tancar-se.
    /// </summary>
    private IEnumerator OutroAnim()
    {
        float elapsed = 0f;
        float dur = 0.25f;
        Vector2 start = panelRT.anchoredPosition;
        Vector2 target = new Vector2(0, -1000f);

        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / dur;
            float ease = t * t * t; // Corba d'acceleració directa (EaseIn)
            panelRT.anchoredPosition = Vector2.Lerp(start, target, ease);
            yield return null;
        }
        panelRT.anchoredPosition = target;
    }

    /// <summary>
    /// Transició creuada cap a l'escena del Menú Principal amb fosa a negre suau.
    /// </summary>
    private IEnumerator ExitToMenuRoutine()
    {
        float elapsed = 0f;
        float dur = 0.5f;
        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeOverlay.color = new Color(0, 0, 0, elapsed / dur);
            yield return null;
        }
        fadeOverlay.color = Color.black;

        Time.timeScale = 1f; // Restaurem temps
        SceneManager.LoadScene("MainMenu");
    }

    // =========================================================================
    // METODES D'ÀUDIO I PROCEDIMENTALS (HELPERS)
    // =========================================================================

    private void PlayNavSound()
    {
        if (PlayerInventory.Instance != null && PlayerInventory.Instance.navSound != null)
            ItemSoundPlayer.Play(PlayerInventory.Instance.navSound);
    }

    private void PlaySelectSound()
    {
        if (PlayerInventory.Instance != null && PlayerInventory.Instance.selectSound != null)
            ItemSoundPlayer.Play(PlayerInventory.Instance.selectSound);
    }

    private Sprite generatedRoundedSprite;
    private Sprite GetRoundedSprite()
    {
        if (generatedRoundedSprite != null) return generatedRoundedSprite;
        int size = 12;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        Color w = Color.white;
        Color c = new Color(1f, 1f, 1f, 0f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool corner = false;
                if (x==0 && y<=2) corner = true;
                else if (x==1 && y<=1) corner = true;
                else if (x==2 && y==0) corner = true;
                else if (x==size-1 && y<=2) corner = true;
                else if (x==size-2 && y<=1) corner = true;
                else if (x==size-3 && y==0) corner = true;
                else if (x==0 && y>=size-3) corner = true;
                else if (x==1 && y>=size-2) corner = true;
                else if (x==2 && y>=size-1) corner = true;
                else if (x==size-1 && y>=size-3) corner = true;
                else if (x==size-2 && y>=size-2) corner = true;
                else if (x==size-3 && y>=size-1) corner = true;

                tex.SetPixel(x, y, corner ? c : w);
            }
        }
        tex.Apply();
        
        generatedRoundedSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(4, 4, 4, 4));
        return generatedRoundedSprite;
    }

    private void SetFont(TextMeshProUGUI t, float size, Color col, FontStyles style, TextAlignmentOptions align)
    {
        t.fontSize = size; t.color = col; t.fontStyle = style; t.alignment = align;
        var f = LoadFont("determination SDF");
        if (f == null) f = LoadFont("PixelOperator SDF");
        if (f == null) f = LoadFont("8bitoperator_jve SDF");
        if (f != null) t.font = f;
    }

    private TMP_FontAsset LoadFont(string n)
    {
#if UNITY_EDITOR
        var f = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"Assets/Fonts/{n}.asset");
        if (f != null) return f;
        f = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            $"Assets/TextMesh Pro/Resources/Fonts & Materials/{n}.asset");
        if (f != null) return f;
#endif
        var r = Resources.Load<TMP_FontAsset>($"Fonts & Materials/{n}"); 
        return r ?? Resources.Load<TMP_FontAsset>($"Fonts/{n}") ?? Resources.Load<TMP_FontAsset>(n);
    }
}
