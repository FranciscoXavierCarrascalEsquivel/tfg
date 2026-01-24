using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CutsceneAsyncLoader : MonoBehaviour
{
    [SerializeField] private string nextSceneName = "GameScene";
    private AsyncOperation op;

    void Start()
    {
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
        op.allowSceneActivation = true; // ahora sí cambia de escena
    }
}
