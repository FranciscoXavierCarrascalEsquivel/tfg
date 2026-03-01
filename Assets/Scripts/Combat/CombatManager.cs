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

    public enum MenuPhase
    {
        Main,
        Target
    }

    [Header("UI")]
    [SerializeField] private GameObject turnMenu;
    [SerializeField] private Button fightButton;
    [SerializeField] private Button reasonButton;
    [SerializeField] private Button defendButton;
    [SerializeField] private Button itemButton;

    private Button[] mainButtons;
    private int selectedIndex = 0;
    private MenuPhase currentPhase = MenuPhase.Main;
    private string originalFightText;

    private State state;
    private CombatEncounter encounter;
    private CombatLoader loader;
    
    [Header("Stats")]
    public int playerMaxHP = 100;
    public int enemyMaxHP = 100;
    private int playerCurrentHP;
    private int enemyCurrentHP;

    [Header("UI Stats")]
    [SerializeField] private TMPro.TMP_Text playerHPText;
    [SerializeField] private TMPro.TMP_Text enemyHPText;

    private HandController[] handControllers;

    public void Begin(CombatEncounter encounter, CombatLoader loader)
    {
        this.encounter = encounter;
        this.loader = loader;

        playerCurrentHP = playerMaxHP;
        enemyCurrentHP = enemyMaxHP;
        UpdateStatsUI();

        mainButtons = new Button[] { fightButton, reasonButton, defendButton, itemButton };
        
        if (fightButton != null)
        {
            originalFightText = GetButtonText(fightButton);
            if (string.IsNullOrEmpty(originalFightText)) originalFightText = "FIGHT";
        }

        foreach (var btn in mainButtons)
        {
            if (btn != null) DisableMouseInteraction(btn);
        }

        // Find and disable hands initially
        handControllers = FindObjectsByType<HandController>(FindObjectsSortMode.None);
        SetHandsActive(false);

        state = State.PlayerTurn;
        SetMenuPhase(MenuPhase.Main);
        ShowTurnMenu(true);
    }

    private void Update()
    {
        if (state != State.PlayerTurn) return;

        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
        {
            MoveSelection(-1);
        }
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
        {
            MoveSelection(1);
        }
        else if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
        {
            ConfirmSelection();
        }
        else if (Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Escape))
        {
            if (currentPhase == MenuPhase.Target)
            {
                SetMenuPhase(MenuPhase.Main);
            }
        }
    }

    // =========================
    // UI Helpers
    // =========================

    private void MoveSelection(int direction)
    {
        int maxOptions = currentPhase == MenuPhase.Main ? mainButtons.Length : 1;
        selectedIndex += direction;
        
        if (selectedIndex < 0) selectedIndex = maxOptions - 1;
        if (selectedIndex >= maxOptions) selectedIndex = 0;
        
        UpdateSelectionVisuals();
    }

    private void ConfirmSelection()
    {
        if (currentPhase == MenuPhase.Main)
        {
            if (selectedIndex == 0) SetMenuPhase(MenuPhase.Target);
            else if (selectedIndex == 1) OnReason();
            else if (selectedIndex == 2) OnDefend();
            else if (selectedIndex == 3) OnItem();
        }
        else if (currentPhase == MenuPhase.Target)
        {
            if (selectedIndex == 0)
            {
                StartCoroutine(PerformAttackRoutine());
            }
        }
    }

    private void DisableMouseInteraction(Button btn)
    {
        btn.onClick.RemoveAllListeners();
        Navigation nav = btn.navigation;
        nav.mode = Navigation.Mode.None;
        btn.navigation = nav;

        Graphic[] graphics = btn.GetComponentsInChildren<Graphic>();
        foreach (var g in graphics)
        {
            g.raycastTarget = false;
        }
    }

    private string GetButtonText(Button btn)
    {
        var tmp = btn.GetComponentInChildren<TMPro.TMP_Text>();
        if (tmp != null) return tmp.text;
        var txt = btn.GetComponentInChildren<Text>();
        if (txt != null) return txt.text;
        return "";
    }

    private void SetButtonText(Button btn, string text)
    {
        var tmp = btn.GetComponentInChildren<TMPro.TMP_Text>();
        if (tmp != null) { tmp.text = text; return; }
        var txt = btn.GetComponentInChildren<Text>();
        if (txt != null) { txt.text = text; return; }
    }

    private void SetMenuPhase(MenuPhase newPhase)
    {
        currentPhase = newPhase;
        selectedIndex = 0;

        if (currentPhase == MenuPhase.Main)
        {
            if (fightButton != null) SetButtonText(fightButton, originalFightText);
        }
        else if (currentPhase == MenuPhase.Target)
        {
            if (fightButton != null) SetButtonText(fightButton, $"Enemy ({enemyCurrentHP} HP)");
        }

        UpdateSelectionVisuals();
    }

    private void UpdateSelectionVisuals()
    {
        for (int i = 0; i < mainButtons.Length; i++)
        {
            Button btn = mainButtons[i];
            if (btn == null) continue;

            if (currentPhase == MenuPhase.Target && i > 0)
            {
                btn.gameObject.SetActive(false);
                continue;
            }
            else
            {
                btn.gameObject.SetActive(true);
            }

            Outline outline = btn.GetComponent<Outline>();
            if (outline == null)
            {
                outline = btn.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(1f, 0.8f, 0f, 1f); // Yellowish outline
                outline.effectDistance = new Vector2(3, -3);
            }

            outline.enabled = (i == selectedIndex);
        }
    }

    private void UpdateStatsUI()
    {
        if (playerHPText) playerHPText.text = $"HP: {playerCurrentHP}/{playerMaxHP}";
        if (enemyHPText) enemyHPText.text = $"HP: {enemyCurrentHP}/{enemyMaxHP}";
        
        if (currentPhase == MenuPhase.Target && fightButton != null)
        {
            SetButtonText(fightButton, $"Enemy ({enemyCurrentHP} HP)");
        }
    }

    public void PlayerTakeDamage(int damage)
    {
        if (state == State.End) return;

        playerCurrentHP -= damage;
        if (playerCurrentHP < 0) playerCurrentHP = 0;
        UpdateStatsUI();

        if (playerCurrentHP == 0)
        {
            state = State.End;
            Debug.Log("PLAYER DIED");
            loader.EndCombat();
        }
    }

    // =========================
    // Player actions
    // =========================

    private IEnumerator PerformAttackRoutine()
    {
        state = State.Resolve; 
        
        int dmg = Random.Range(1, 11); // 1 to 10
        Debug.Log($"FIGHT! Dealt {dmg} damage.");
        
        if (fightButton != null) SetButtonText(fightButton, $"Dealt {dmg} damage!");

        enemyCurrentHP -= dmg;
        if (enemyCurrentHP < 0) enemyCurrentHP = 0;
        UpdateStatsUI();

        yield return new WaitForSeconds(1.5f);

        if (enemyCurrentHP == 0)
        {
            state = State.End;
            Debug.Log("ENEMY DEFEATED");
            loader.EndCombat();
            yield break;
        }

        EndPlayerTurn();
    }

    private void OnReason()
    {
        Debug.Log("REASON!");
        EndPlayerTurn();
    }

    private void OnDefend()
    {
        Debug.Log("DEFEND!");
        EndPlayerTurn();
    }

    private void OnItem()
    {
        Debug.Log("ITEM!");
        EndPlayerTurn();
    }

    private void EndPlayerTurn()
    {
        ShowTurnMenu(false);
        state = State.EnemyTurn;
        StartCoroutine(EnemyTurnRoutine());
    }

    private void ShowTurnMenu(bool show)
    {
        if (turnMenu != null) turnMenu.SetActive(show);
        
        if (show) 
        {
            SetMenuPhase(MenuPhase.Main);
        }
    }

    private void SetHandsActive(bool active)
    {
        if (handControllers == null) return;
        foreach (var hand in handControllers)
        {
            if (hand != null) hand.canMove = active;
        }
    }

    // =========================
    // Enemy turn
    // =========================

    private IEnumerator EnemyTurnRoutine()
    {
        Debug.Log("ENEMY TURN started");

        SetHandsActive(true);

        float dur = (encounter != null) ? encounter.enemyAttackDuration : 2f;

        var spawner = FindFirstObjectByType<EnemyAttackSpawner>();
        if (spawner != null)
        {
             yield return spawner.Run(dur);
        }
        else
        {
            yield return new WaitForSeconds(dur);
        }

        Debug.Log("ENEMY TURN ended");

        SetHandsActive(false);

        state = State.PlayerTurn;
        ShowTurnMenu(true);
    }
}
