using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Component de suport HUD que genera i controla de forma dinàmica la targeta de controls (ControlsUI).
/// Crea per codi (sense dependències de prefabs externs) un quadre de text a la cantonada superior dreta de la pantalla.
/// Integra control intel·ligent d'estat de visibilitat:
/// 1) Es manté ocult fins que el jugador supera el diàleg d'inici de la partida per no trencar la immersió.
/// 2) S'amaga a l'instant en entrar a qualsevol menú, botiga o combat (PauseMenuUI, InventoryMenuUI, etc.)
/// 3) Realitza una transició de fosa suau (fade out/in) de forma dinàmica quan s'obre/tanca un diàleg ordinari.
/// </summary>
public class ControlsUI : MonoBehaviour
{
    private GameObject panelGO;           // Contenidor del panell
    private CanvasGroup canvasGroup;       // Control d'opacitat del grup
    private RectTransform panelRT;         // Posicionament a la pantalla
    private TextMeshProUGUI controlsTxt;   // Camp de text gràfic TMP

    // Variables de control d'estat estàtic que sobreviuen entre escenes
    private static bool hasFinishedFirstDialogue = false; // Indica si el jugador ja ha parlat el primer cop
    private static bool wasDialogueOpenOnce = false;       // Auxiliar per a detectar la primera interacció
    private float currentAlpha = 0f;
    private float targetAlpha = 0f;

    private void Start()
    {
        // Donem un breu retard per assegurar que el MainCanvas de l'escena està ben configurat
        StartCoroutine(InitializeRoutine());
    }

    private void StartCoroutine() { } // Declarat per mantenir signatures si existissin

    private IEnumerator InitializeRoutine()
    {
        yield return new WaitForSecondsRealtime(0.1f);
        if (panelGO == null) BuildUI();
    }

    /// <summary>
    /// Construeix de forma procedimental a nivell d'UI tota l'estructura de la targeta de controls.
    /// Això evita dependències de referències trencades de prefabs en les builds executables.
    /// </summary>
    private void BuildUI()
    {
        var canvas = CanvasHelper.GetMainCanvas();
        if (canvas == null) return;

        // Si ja existia un panell previ residual, el destruïm
        var old = GameObject.Find("ControlsPanel");
        if (old != null) Destroy(old);

        panelGO = new GameObject("ControlsPanel");
        panelGO.transform.SetParent(canvas.transform, false);
        
        // Ancoratge superior dret (Top-Right)
        panelRT = panelGO.AddComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(1, 1);
        panelRT.anchorMax = new Vector2(1, 1);
        panelRT.pivot = new Vector2(1, 1);
        panelRT.sizeDelta = new Vector2(350, 400); // Mida compacta estilitzada
        panelRT.anchoredPosition = Vector2.zero; // Enganxat de forma literal a la cantonada superior dreta

        canvasGroup = panelGO.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f; 
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false; // Ignorem ratolí perquè no bloquegi clics de joc

        // Creem l'objecte de text
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
        controlsTxt.alignment = TextAlignmentOptions.Center; 
        controlsTxt.lineSpacing = 16f;
        controlsTxt.margin = new Vector4(0, 10, 0, 0); // Marge superior de seguretat
        
        // Càrrega de font pixelada retro de forma segura
        var font = LoadFont("determination SDF");
        if (font == null) font = LoadFont("PixelOperator SDF");
        if (font == null) font = LoadFont("8bitoperator_jve SDF");
        if (font != null) controlsTxt.font = font;

        // Formatat en català/anglès dels controls del joc
        controlsTxt.text = "<color=#FFE526><size=28>CONTROLS</size></color>\n\n" +
                           "<b>WASD</b>  -  Move\n" +
                           "<b>SHIFT</b>  -  Run (Hold)\n" +
                           "<b>E</b>  -  Interact\n" +
                           "<b>TAB</b>  -  Inventory\n" +
                           "<b>ESC</b>  -  Pause (Hold)";
    }

    private DialogueUI cachedDialogUI; // Memòria cau del gestor de diàlegs
    private float canvasRetryTimer = 0f;

    private void LateUpdate()
    {
        if (panelGO == null)
        {
            // Protecció contra falta de Canvas carregat (reintentem cada 0.5s per evitar saturar la CPU)
            canvasRetryTimer -= Time.unscaledDeltaTime;
            if (canvasRetryTimer > 0f) return;
            canvasRetryTimer = 0.5f;

            var canvas = CanvasHelper.GetMainCanvas();
            if (canvas != null) BuildUI();
            return;
        }

        // Obtenció asíncrona no invasiva del DialogueUI
        if (cachedDialogUI == null) cachedDialogUI = FindFirstObjectByType<DialogueUI>();
        
        bool isDialogueOpen = (cachedDialogUI != null && cachedDialogUI.IsOpen);

        // --- GESTIÓ INTEL·LIGENT DE PRIMERA VISUALITZACIÓ ---
        // S'assegura que no es vegi la caixa de controls fins que el diàleg d'introducció finalitzi completament
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

        // Estat de si hi ha qualsevol menú, pausa, botiga o batalla activa a sobre
        bool isMenuOpen = PauseMenuUI.IsOpen || 
                          InventoryMenuUI.IsOpen || 
                          ShopMenuUI.IsOpen ||
                          CombatLoader.IsInCombat;

        if (isMenuOpen)
        {
            // Ocultació immediata instantània per evitar soroll gràfic o solapament en pantalles plenes
            currentAlpha = 0f;
            targetAlpha = 0f;
            canvasGroup.alpha = 0f;
        }
        else if (isDialogueOpen)
        {
            // Si només s'obre un diàleg tradicional en temps de joc, fem una fosa a negre suau elegant
            targetAlpha = 0f;
            currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, Time.unscaledDeltaTime * 2.5f);
            canvasGroup.alpha = currentAlpha;
        }
        else
        {
            // Tornem a fer visible de manera atenuada (0.5 opacitat) per a mantenir-se discret com a HUD secundari
            targetAlpha = 0.5f; 
            currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, Time.unscaledDeltaTime * 2f);
            canvasGroup.alpha = currentAlpha;
        }
    }

    private Sprite generatedRoundedSprite;

    /// <summary>
    /// Dibuixa dinàmicament per codi una textura d'escaire amb cantonades arrodonides (9-Slice Sprite).
    /// </summary>
    private Sprite GetRoundedSprite()
    {
        if (generatedRoundedSprite != null) return generatedRoundedSprite;
        int size = 12;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        Color w = Color.white;
        Color c = new Color(1f, 1f, 1f, 0f); // Canal Alfa a 0 per tall de cantonada

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Filtre matemàtic de cantonades arrodonides tipus píxel art
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
        
        // Creem l'sprite aplicant perfils de deformació de vora (Vector4 de marges de 9-Slice)
        generatedRoundedSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(4, 4, 4, 4));
        return generatedRoundedSprite;
    }

    /// <summary>
    /// Mètode de càrrega segura de fonts que valida diferents rutes tant a l'Editor com a la Build executada.
    /// </summary>
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
