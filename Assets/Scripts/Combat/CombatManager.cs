using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class CombatManager : MonoBehaviour
{
    public enum State
    {
        Enter,
        PlayerTurn,
        EnemyTurn,
        Resolve,
        End
    }

    [Header("UI")]
    [SerializeField] private GameObject turnMenu;
    [SerializeField] private Button fightButton;
    [SerializeField] private Button reasonButton;
    [SerializeField] private Button defendButton;
    [SerializeField] private Button itemButton;

    private State state;
    private CombatEncounter encounter;
    private CombatLoader loader;

    // --- IMPORTANT: això es crida des del CombatLoader ---
    public void Begin(CombatEncounter encounter, CombatLoader loader)
    {
        this.encounter = encounter;
        this.loader = loader;

        // ✅ Sempre comença el jugador
        state = State.PlayerTurn;

        ShowTurnMenu(true);
        HookButtons();
    }

    // =========================
    // UI
    // =========================

    private void HookButtons()
    {
        // Evita duplicats si Begin es crida més d’un cop
        fightButton.onClick.RemoveAllListeners();
        reasonButton.onClick.RemoveAllListeners();
        defendButton.onClick.RemoveAllListeners();
        itemButton.onClick.RemoveAllListeners();

        fightButton.onClick.AddListener(OnFight);
        reasonButton.onClick.AddListener(OnReason);
        defendButton.onClick.AddListener(OnDefend);
        itemButton.onClick.AddListener(OnItem);
    }

    private void ShowTurnMenu(bool show)
    {
        if (turnMenu != null) turnMenu.SetActive(show);

        // També pots desactivar els botons individualment si vols
        if (fightButton)  fightButton.interactable  = show;
        if (reasonButton) reasonButton.interactable = show;
        if (defendButton) defendButton.interactable = show;
        if (itemButton)   itemButton.interactable   = show;
    }

    // =========================
    // Player actions
    // =========================

    private void OnFight()
    {
        if (state != State.PlayerTurn) return;

        Debug.Log("FIGHT!");
        EndPlayerTurn();
    }

    private void OnReason()
    {
        if (state != State.PlayerTurn) return;

        Debug.Log("REASON!");
        EndPlayerTurn();
    }

    private void OnDefend()
    {
        if (state != State.PlayerTurn) return;

        Debug.Log("DEFEND!");
        EndPlayerTurn();
    }

    private void OnItem()
    {
        if (state != State.PlayerTurn) return;

        Debug.Log("ITEM!");
        EndPlayerTurn();
    }

    private void EndPlayerTurn()
    {
        // Apaga menú mentre passa el torn
        ShowTurnMenu(false);

        // Ara sí: toca torn enemic (en el futur faràs projectils/parry)
        state = State.EnemyTurn;
        StartCoroutine(EnemyTurnRoutine());
    }

    // =========================
    // Enemy turn
    // =========================

    private IEnumerator EnemyTurnRoutine()
    {
        Debug.Log("ENEMY TURN started");

        // Aquí és on després faràs:
        // - spawn projectils
        // - duració encounter.enemyAttackDuration
        // - parries etc.

        float t = 0f;
        float dur = (encounter != null) ? encounter.enemyAttackDuration : 2f;

        while (t < dur)
        {
            t += Time.deltaTime;
            yield return null;
        }

        Debug.Log("ENEMY TURN ended");

        // Torna al jugador
        state = State.PlayerTurn;
        ShowTurnMenu(true);
    }
}
