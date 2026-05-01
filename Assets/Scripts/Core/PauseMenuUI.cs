using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class PauseMenuUI : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    [Header("Aesthetics")]
    [SerializeField] private Color overlayColor = new Color(0f, 0f, 0f, 0.75f);
    [SerializeField] private Color panelColor = new Color(0.05f, 0.05f, 0.1f, 1f);
    [SerializeField] private Color accentColor = new Color(0.95f, 0.8f, 0.15f, 1f);

    private bool wasPlayerLocked = false;
    private bool isConfirming = false;
    private TextMeshProUGUI titleTxt;
    private Image fadeOverlay;

    private GameObject rootGO;
    private RectTransform panelRT;
    private int selectedIndex = 0; // 0: Resume, 1: Exit
    private Image[] buttonBgs = new Image[2];
    private TextMeshProUGUI[] buttonTexts = new TextMeshProUGUI[2];

    private TextMeshProUGUI selectorArrow;
    private float floatTimer = 0f;

    private static PauseMenuUI instance;

    public static void Show()
    {
        if (instance != null) return;

        var canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
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
        
        var player = FindFirstObjectByType<PlayerController2D>();
        if (player != null)
        {
            instance.wasPlayerLocked = player.IsMovementLocked;
            player.LockMovement();
        }

        IsOpen = true;
        Time.timeScale = 0f;
    }

    private void Build()
    {
        // Overlay
        rootGO = gameObject;
        var rt = rootGO.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        
        var overlayImg = rootGO.AddComponent<Image>();
        overlayImg.color = overlayColor;

        // Panel
        var pGO = new GameObject("Panel");
        pGO.transform.SetParent(transform, false);
        panelRT = pGO.AddComponent<RectTransform>();
        panelRT.sizeDelta = new Vector2(400f, 350f);
        panelRT.anchoredPosition = new Vector2(0, -1000f); 

        var pImg = pGO.AddComponent<Image>();
        pImg.color = panelColor;
        pImg.sprite = GetRoundedSprite();
        pImg.type = Image.Type.Sliced;

        var outline = pGO.AddComponent<Outline>();
        outline.effectColor = accentColor;
        outline.effectDistance = new Vector2(4f, -4f);

        // Title
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(panelRT, false);
        var titleRT = titleGO.AddComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0, 0.75f); titleRT.anchorMax = new Vector2(1, 1);
        titleRT.offsetMin = titleRT.offsetMax = Vector2.zero;
        titleTxt = titleGO.AddComponent<TextMeshProUGUI>();
        SetFont(titleTxt, 42f, accentColor, FontStyles.Bold, TextAlignmentOptions.Center);
        titleTxt.text = "PAUSED";

        // Buttons
        CreateButton(0, "Resume", new Vector2(0, 20f));
        CreateButton(1, "Exit to Menu", new Vector2(0, -60f));

        // Selector Arrow
        var arrowGO = new GameObject("Selector");
        arrowGO.transform.SetParent(panelRT, false);
        var arrowRT = arrowGO.AddComponent<RectTransform>();
        arrowRT.sizeDelta = new Vector2(50f, 50f);
        selectorArrow = arrowGO.AddComponent<TextMeshProUGUI>();
        SetFont(selectorArrow, 32f, accentColor, FontStyles.Bold, TextAlignmentOptions.Center);
        selectorArrow.text = ">";

        // Fade Overlay (for exit)
        var fGO = new GameObject("FadeOverlay");
        fGO.transform.SetParent(transform, false);
        var fRT = fGO.AddComponent<RectTransform>();
        fRT.anchorMin = Vector2.zero; fRT.anchorMax = Vector2.one;
        fRT.offsetMin = fRT.offsetMax = Vector2.zero;
        fadeOverlay = fGO.AddComponent<Image>();
        fadeOverlay.color = new Color(0,0,0,0);
        fadeOverlay.raycastTarget = false;

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

    private IEnumerator IntroAnim()
    {
        float elapsed = 0f;
        float dur = 0.3f;
        Vector2 start = new Vector2(0, -1000f);
        Vector2 target = Vector2.zero;

        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / dur;
            float ease = 1f - Mathf.Pow(1f - t, 3f);
            panelRT.anchoredPosition = Vector2.Lerp(start, target, ease);
            yield return null;
        }
        panelRT.anchoredPosition = target;
    }

    private void Update()
    {
        // Floating animation
        floatTimer += Time.unscaledDeltaTime;
        float yOffset = Mathf.Sin(floatTimer * 3f) * 10f;
        panelRT.anchoredPosition = new Vector2(0, yOffset);

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isConfirming) CancelExit();
            else Resume();
            return;
        }

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

    private void UpdateHighlights()
    {
        for (int i = 0; i < 2; i++)
        {
            bool sel = (i == selectedIndex);
            if (isConfirming)
            {
                buttonBgs[i].color = sel ? new Color(0.5f, 0.2f, 0.2f, 1f) : new Color(0.15f, 0.15f, 0.25f, 1f);
            }
            else
            {
                buttonBgs[i].color = sel ? new Color(0.3f, 0.3f, 0.5f, 1f) : new Color(0.15f, 0.15f, 0.25f, 1f);
            }
            buttonTexts[i].color = sel ? accentColor : Color.white;
            buttonTexts[i].rectTransform.anchoredPosition = sel ? new Vector2(0, -2f) : Vector2.zero;

            if (sel)
            {
                var targetPos = buttonBgs[i].rectTransform.anchoredPosition;
                targetPos.x -= 180f; // Position to the left of the button
                selectorArrow.rectTransform.anchoredPosition = targetPos;
            }
        }
    }

    private void AskForExit()
    {
        isConfirming = true;
        titleTxt.text = "EXIT GAME?";
        titleTxt.color = new Color(1f, 0.4f, 0.4f);
        buttonTexts[0].text = "No, stay";
        buttonTexts[1].text = "Yes, exit";
        selectedIndex = 0; // Default to stay
        UpdateHighlights();
    }

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

    public void Resume()
    {
        Time.timeScale = 1f;

        var player = FindFirstObjectByType<PlayerController2D>();
        var dialogUI = FindFirstObjectByType<DialogueUI>();
        
        // Només desbloquegem si NO estava bloquejat prèviament i no hi ha diàleg
        if (player != null && !wasPlayerLocked && (dialogUI == null || !dialogUI.IsOpen))
        {
            player.UnlockMovement();
        }

        IsOpen = false;
        instance = null;
        Destroy(gameObject);
    }

    private IEnumerator ExitToMenuRoutine()
    {
        // Fade to black
        float elapsed = 0f;
        float dur = 0.5f;
        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeOverlay.color = new Color(0, 0, 0, elapsed / dur);
            yield return null;
        }
        fadeOverlay.color = Color.black;

        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

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
        var f = LoadFont("8bitoperator_jve SDF"); if (f == null) f = LoadFont("PixelOperator SDF");
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
        var r = Resources.Load<TMP_FontAsset>($"Fonts & Materials/{n}"); return r ?? Resources.Load<TMP_FontAsset>(n);
    }
}
