using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CombatLoader : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string combatSceneName = "CombatScene";
    [SerializeField] private MonoBehaviour[] worldScriptsToDisable;

    [Header("Split Snapshot Transition")]
    [SerializeField] private SplitSnapshot splitOverlayPrefab;

    public void StartCombat(CombatEncounter encounter)
    {
        StartCoroutine(StartCombatRoutine(encounter));
    }

    private IEnumerator StartCombatRoutine(CombatEncounter encounter)
    {
        // 1) Captura el frame actual (món) al final del frame
        yield return new WaitForEndOfFrame();

        // Captura manual (fiable)
        var snapshot = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        snapshot.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
        snapshot.Apply();

        // 2) Instancia l'overlay i posa el snapshot
        var overlay = Instantiate(splitOverlayPrefab);
        overlay.SetSnapshot(snapshot);

        // Assegura que el canvas va davant
        var c = overlay.GetComponent<Canvas>();
        if (c != null)
        {
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 9999;
        }

        // IMPORTANT: deixa 1-2 frames perquè es vegi segur
        yield return null;
        yield return null;

        // 3) Comença l'animació d'obrir-se (split cap als costats)
        // i al mateix temps carreguem combat per sota
        var loadOp = SceneManager.LoadSceneAsync(combatSceneName, LoadSceneMode.Additive);

        // mentre carrega, deixem que l'animació avanci
        // (encara no desactivem el món fins que ja estigui carregat)
        yield return StartCoroutine(overlay.PlayOpen());

        // 4) Espera que el combat estigui carregat
        while (!loadOp.isDone) yield return null;

        // 5) Ara sí, desactiva scripts del món
        foreach (var s in worldScriptsToDisable) if (s) s.enabled = false;

        // 6) Inicia combat
        var cm = FindFirstObjectByType<CombatManager>();
        if (cm != null) cm.Begin(encounter, this);
        else Debug.LogError("CombatManager no trobat a CombatScene");
    }

    public void EndCombat()
    {
        StartCoroutine(EndCombatRoutine());
    }

    private IEnumerator EndCombatRoutine()
    {
        var op = SceneManager.UnloadSceneAsync(combatSceneName);
        while (!op.isDone) yield return null;

        foreach (var s in worldScriptsToDisable) if (s) s.enabled = true;
    }
}

[System.Serializable]
public class CombatEncounter
{
    public Sprite enemyPortrait;
    public GameObject projectilePrefab;
    public float enemyAttackDuration = 6f;
}
