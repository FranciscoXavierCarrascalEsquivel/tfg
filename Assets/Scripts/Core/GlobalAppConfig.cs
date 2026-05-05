using UnityEngine;
using UnityEngine.SceneManagement;

public class GlobalAppConfig : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Initialize()
    {
        // Forcem pantalla completa a la màxima resolució disponible des de l'inici
        Resolution maxRes = Screen.currentResolution;
        Screen.SetResolution(maxRes.width, maxRes.height, FullScreenMode.FullScreenWindow);

        GameObject appConfig = new GameObject("GlobalAppConfig");
        appConfig.AddComponent<GlobalAppConfig>();
        DontDestroyOnLoad(appConfig);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyResponsiveness();
    }

    private bool initialized = false;

    void Start()
    {
        ApplyResponsiveness();
    }

    private void ApplyResponsiveness()
    {
        // 1. Reset de la càmera
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            mainCam.rect = new Rect(0, 0, 1, 1);
        }

        // 2. Configurar TOTS els Canvas de la escena a 2K (2560x1440)
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (var canvas in canvases)
        {
            if (canvas.name == "EndCanvas" || canvas.name == "AlertCanvas") continue;

            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            if (scaler == null) scaler = canvas.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(2560, 1440); 
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f; 
        }
    }

    void Update()
    {
        // Ens assegurem que el joc es mantingui en pantalla completa
        if (!Screen.fullScreen)
        {
            Resolution maxRes = Screen.currentResolution;
            Screen.SetResolution(maxRes.width, maxRes.height, FullScreenMode.FullScreenWindow);
        }
    }
}
