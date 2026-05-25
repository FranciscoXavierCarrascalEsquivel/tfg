using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Gestor i configurador d'aplicació persistent i global (GlobalAppConfig).
/// S'encarrega d'homogeneïtzar i forçar els paràmetres de visualització del joc:
/// 1) Arrenca de forma instantània abans que cap escena s'hagi carregat (BeforeSceneLoad)
///    i col·loca el joc a pantalla completa a la màxima resolució física del monitor de l'usuari.
/// 2) Es manté viu durant tota la sessió (DontDestroyOnLoad).
/// 3) Monitoritza i corregeix els Canvas i càmeres a cada càrrega de mapa (sceneLoaded)
///    perquè escalin a resolució 2K (2560x1440) de manera adaptativa i responsiva.
/// </summary>
public class GlobalAppConfig : MonoBehaviour
{
    /// <summary>
    /// Mètode d'inicialització automàtica del cicle de vida de Unity.
    /// S'executa abans que es renderitzi el primer frame o es carregui la primera escena.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Initialize()
    {
        // Forcem pantalla completa nativa a la màxima resolució suportada del monitor de l'usuari
        Resolution maxRes = Screen.currentResolution;
        Screen.SetResolution(maxRes.width, maxRes.height, FullScreenMode.FullScreenWindow);

        // Instanciem un element de control persistent per a governar el cicle de vida
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

    private void Start()
    {
        ApplyResponsiveness();
    }

    /// <summary>
    /// Escaneja de forma proactiva l'escena acabada de carregar per configurar els paràmetres d'escala
    /// i resolució recomanats per al joc i la interfície gràfica de l'inventari/debug.
    /// </summary>
    private void ApplyResponsiveness()
    {
        // 1. Assegurem que la viewport rect de la càmera principal ocupa el 100% de la pantalla (sense retalls)
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            mainCam.rect = new Rect(0, 0, 1, 1);
        }

        // 2. Cerquem i forcem que TOTS els Canvas actius de l'escena escalin a 2560x1440 adaptatiu
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (var canvas in canvases)
        {
            // Ignorem explícitament els panells de transició o alerta de bafarada
            if (canvas.name == "EndCanvas" || canvas.name == "AlertCanvas") continue;

            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            if (scaler == null) 
                scaler = canvas.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            
            // Apliquem escalat adaptatiu (equidistants entre alçada i amplada)
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(2560, 1440); 
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f; 
        }
    }

    private void Update()
    {
        // Guarda de seguretat: si l'usuari fa un canvi accidental i treu el mode de pantalla completa,
        // el tornem a restaurar de forma transparent
        if (!Screen.fullScreen)
        {
            Resolution maxRes = Screen.currentResolution;
            Screen.SetResolution(maxRes.width, maxRes.height, FullScreenMode.FullScreenWindow);
        }
    }
}
