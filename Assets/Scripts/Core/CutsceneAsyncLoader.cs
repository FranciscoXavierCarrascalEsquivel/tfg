using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CutsceneAsyncLoader : MonoBehaviour
{
    [SerializeField] private string nextSceneName = "GameScene";
    private AsyncOperation op;

    void Start()
    {
        // Iniciem la precàrrega de la següent escena
        StartCoroutine(Preload());
    }

    IEnumerator Preload()
    {
        op = SceneManager.LoadSceneAsync(nextSceneName);
        op.allowSceneActivation = false; // NO entra aún en la escena
        while (op.progress < 0.9f) yield return null; // 0.9 = ya está cargada
    }

    // Llama a esto al terminar la cinemática
    public void FinishCutscene()
    {
        // Activem l'escena que hem estat precarregant
        op.allowSceneActivation = true; 
    }
}
