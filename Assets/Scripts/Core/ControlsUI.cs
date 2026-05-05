using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

public class ControlsUI : MonoBehaviour
{
    private GameObject panelGO;
    private CanvasGroup canvasGroup;
    private RectTransform panelRT;
    private TextMeshProUGUI controlsTxt;

    private static bool hasFinishedFirstDialogue = false;
    private static bool wasDialogueOpenOnce = false;
    private float currentAlpha = 0f;
    private float targetAlpha = 0f;

    private void Start()
    {
        StartCoroutine(InitializeRoutine());
    }

    private IEnumerator InitializeRoutine()
    {
        yield return new WaitForSecondsRealtime(0.1f);
        if (panelGO == null) BuildUI();
    }

    private void BuildUI()
    {
        var canvas = CanvasHelper.GetMainCanvas();
        if (canvas == null) return;

        var old = GameObject.Find("ControlsPanel");
        if (old != null) Destroy(old);

        panelGO = new GameObject("ControlsPanel");
        panelGO.transform.SetParent(canvas.transform, false);
        
        panelRT = panelGO.AddComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(1, 1);
        panelRT.anchorMax = new Vector2(1, 1);
        panelRT.pivot = new Vector2(1, 1);
        panelRT.sizeDelta = new Vector2(350, 400); // Amplada ajustada al text per no deixar buits
        panelRT.anchoredPosition = Vector2.zero; // Enganxat literalment a la cantonada

        canvasGroup = panelGO.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f; 
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        // No fons ni tarjeta per petició de l'usuari

        var txtGO = new GameObject("Text");
        txtGO.transform.SetParent(panelGO.transform, false);
        var txtRT = txtGO.AddComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = Vector2.zero;
        txtRT.offsetMax = Vector2.zero;

        controlsTxt = txtGO.AddComponent<TextMeshProUGUI>();
        controlsTxt.fontSize = 32;
        controlsTxt.color = new Color(0.9f, 0.9f, 0.9f, 1f);
        controlsTxt.alignment = TextAlignmentOptions.Center; // Es manté centrat respecte el seu quadre
        controlsTxt.lineSpacing = 16f;
        controlsTxt.margin = new Vector4(0, 10, 0, 0); // Una mica de marge superior
        
        var font = LoadFont("8bitoperator_jve SDF");
        if (font == null) font = LoadFont("PixelOperator SDF");
        if (font != null) controlsTxt.font = font;

        controlsTxt.text = "<color=#FFE526><size=28>CONTROLS</size></color>\n\n" +
                           "<b>WASD</b>  -  Move\n" +
                           "<b>SHIFT</b>  -  Run (Hold)\n" +
                           "<b>E</b>  -  Interact\n" +
                           "<b>TAB</b>  -  Inventory\n" +
                           "<b>ESC</b>  -  Pause (Hold)";
    }

    private DialogueUI cachedDialogUI;
    private float canvasRetryTimer = 0f;

    private void LateUpdate()
    {
        if (panelGO == null)
        {
            // Evitem cercar el canvas cada frame — reintentem cada 0.5s
            canvasRetryTimer -= Time.unscaledDeltaTime;
            if (canvasRetryTimer > 0f) return;
            canvasRetryTimer = 0.5f;

            var canvas = CanvasHelper.GetMainCanvas();
            if (canvas != null) BuildUI();
            return;
        }

        // Busquem el DialogueUI només si no el tenim o s'ha destruït (canvi d'escena)
        if (cachedDialogUI == null) cachedDialogUI = FindFirstObjectByType<DialogueUI>();
        
        bool isDialogueOpen = (cachedDialogUI != null && cachedDialogUI.IsOpen);

        // Lògica per començar a mostrar-ho només després del primer diàleg
        if (!hasFinishedFirstDialogue)
        {
            if (isDialogueOpen) wasDialogueOpenOnce = true;
            if (wasDialogueOpenOnce && !isDialogueOpen) hasFinishedFirstDialogue = true;
            
            if (!hasFinishedFirstDialogue)
            {
                canvasGroup.alpha = 0f;
                currentAlpha = 0f;
                return;
            }
        }

        // Utilitzem les propietats estàtiques IsOpen en lloc de buscar per tota l'escena
        bool isMenuOpen = PauseMenuUI.IsOpen || 
                          InventoryMenuUI.IsOpen || 
                          ShopMenuUI.IsOpen ||
                          CombatLoader.IsInCombat;

        if (isMenuOpen)
        {
            // Tancat instantani per als menús
            currentAlpha = 0f;
            targetAlpha = 0f;
            canvasGroup.alpha = 0f;
        }
        else if (isDialogueOpen)
        {
            // Transició suau per als diàlegs
            targetAlpha = 0f;
            currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, Time.unscaledDeltaTime * 2.5f);
            canvasGroup.alpha = currentAlpha;
        }
        else
        {
            // Transició suau per tornar a aparèixer després de diàlegs
            targetAlpha = 0.5f; // Opacitat discret pel text
            currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, Time.unscaledDeltaTime * 2f);
            canvasGroup.alpha = currentAlpha;
        }
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
