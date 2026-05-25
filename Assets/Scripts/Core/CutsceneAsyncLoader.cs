using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Precarregador asíncron d'escenes en segon pla (CutsceneAsyncLoader).
/// Dissenyat per a minimitzar o eliminar totalment els temps de càrrega gràfics en transicions.
/// En arrencar, utilitza LoadSceneAsync per carregar les dades del següent mapa en memòria RAM/GPU,
/// però restringeix l'activació de l'escena bloquejant 'allowSceneActivation = false'.
/// Un cop la cinemàtica o animació finalitza, es crida FinishCutscene per fer el canvi instantani.
/// </summary>
public class CutsceneAsyncLoader : MonoBehaviour
{
    [Header("Escena Destí")]
    [SerializeField] private string nextSceneName = "GameScene"; // Nom de l'escena següent a carregar
    
    private AsyncOperation op; // Referència de control de l'operació asíncrona de Unity

    private void Start()
    {
        // Engegarem el procés asíncron de precàrrega des del primer frame
        StartCoroutine(Preload());
    }

    /// <summary>
    /// Corrutina en segon pla encarregada de realitzar la càrrega asíncrona sense canviar d'escena.
    /// </summary>
    private IEnumerator Preload()
    {
        op = SceneManager.LoadSceneAsync(nextSceneName);
        op.allowSceneActivation = false; // Bloquegem el pas automàtic a la nova escena

        // El progrés asíncron en Unity es deté al 0.9f quan l'escena està completament carregada i llesta per activar-se
        while (op.progress < 0.9f) 
        {
            yield return null; 
        }
    }

    /// <summary>
    /// Mètode que es crida quan la cinemàtica ha finalitzat i és segur fer el salt instantani a la nova escena.
    /// </summary>
    public void FinishCutscene()
    {
        if (op != null)
        {
            // Autoritzem Unity a activar el mapa que ha estat precuinat en memòria cau
            op.allowSceneActivation = true; 
        }
    }
}
