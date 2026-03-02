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
    [SerializeField] private DiceRollUI dicePrefab;

    private Button[] mainButtons;
    private int selectedIndex = 0;
    private MenuPhase currentPhase = MenuPhase.Main;
    private string originalFightText;

    private State state;
    private CombatEncounter encounter;
    private CombatLoader loader;
    
    [Header("Stats")]
    public int playerMaxHP = 100;
    public int enemyMaxHP = 15;
    private int playerCurrentHP;
    private int enemyCurrentHP;

    [Header("UI Stats")]
    [SerializeField] private TMPro.TMP_Text playerHPText;
    [SerializeField] private TMPro.TMP_Text enemyHPText;

    [Header("Audio Feedback")]
    [SerializeField] private AudioClip moveMenuSound;
    [SerializeField] private AudioClip takeDamageSound;
    [SerializeField] private AudioClip parrySound;
    [SerializeField] private AudioClip playerMoveSound;
    private AudioSource audioSource;
    private AudioSource loopAudioSource;

    [Header("VFX & Limits")]
    [SerializeField] private GameObject parryParticlePrefab;
    [SerializeField] private RectTransform projectileDestroyLimit;

    private HandController[] handControllers;

    // Default positions used for Entrance Animations
    private Vector2 turnMenuOriginalPos;
    private Vector2 playerHPOriginalPos;
    private Vector2 enemyHPOriginalPos;

    private void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        loopAudioSource = gameObject.AddComponent<AudioSource>();
        loopAudioSource.playOnAwake = false;
        loopAudioSource.loop = true;
        loopAudioSource.spatialBlend = 0f;

        if (turnMenu != null) 
        {
            var rt = turnMenu.GetComponent<RectTransform>();
            turnMenuOriginalPos = rt.anchoredPosition;
            rt.anchoredPosition = turnMenuOriginalPos + new Vector2(0, -500f);
        }
        
        if (playerHPText != null) 
        {
            var rt = playerHPText.GetComponent<RectTransform>();
            playerHPOriginalPos = rt.anchoredPosition;
            rt.anchoredPosition = playerHPOriginalPos + new Vector2(0, 300f);
        }
        
        if (enemyHPText != null) 
        {
            var rt = enemyHPText.GetComponent<RectTransform>();
            enemyHPOriginalPos = rt.anchoredPosition;
            rt.anchoredPosition = enemyHPOriginalPos + new Vector2(0, 300f);
        }
    }

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

        SetupButtonInteractions();

        // Find and disable hands initially
        handControllers = FindObjectsByType<HandController>(FindObjectsSortMode.None);
        SetHandsActive(false);

        state = State.PlayerTurn;
        SetMenuPhase(MenuPhase.Main);
        ShowTurnMenu(true);

        // Dispara les animacions d'entrada tipus Slide UI
        if (turnMenu != null) StartCoroutine(SlideInRect(turnMenu.GetComponent<RectTransform>(), turnMenuOriginalPos, new Vector2(0, -500f), 0.7f));
        if (playerHPText != null) StartCoroutine(SlideInRect(playerHPText.GetComponent<RectTransform>(), playerHPOriginalPos, new Vector2(0, 300f), 0.7f));
        if (enemyHPText != null) StartCoroutine(SlideInRect(enemyHPText.GetComponent<RectTransform>(), enemyHPOriginalPos, new Vector2(0, 300f), 0.7f));
    }

    private IEnumerator SlideInRect(RectTransform rect, Vector2 targetPos, Vector2 startOffset, float duration)
    {
        if (rect == null) yield break;
        
        Vector2 startPos = targetPos + startOffset;
        rect.anchoredPosition = startPos;
        
        float time = 0;
        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;
            // Cubic Ease Out per un moviment suau i polit cap al final
            float easeT = 1f - Mathf.Pow(1f - t, 3f);
            rect.anchoredPosition = Vector2.Lerp(startPos, targetPos, easeT);
            yield return null;
        }
        
        rect.anchoredPosition = targetPos;
    }

    private void Update()
    {
        // --- Handle Player Movement Sound looping centrally ---
        bool anyHandMoving = false;
        if (handControllers != null)
        {
            foreach (var h in handControllers)
            {
                if (h != null && h.IsMoving)
                {
                    anyHandMoving = true;
                    break;
                }
            }
        }

        if (anyHandMoving && playerMoveSound)
        {
            if (!loopAudioSource.isPlaying)
            {
                loopAudioSource.clip = playerMoveSound;
                loopAudioSource.Play();
            }
        }
        else
        {
            if (loopAudioSource != null && loopAudioSource.isPlaying)
            {
                loopAudioSource.Stop();
            }
        }

        // --- Handle UI Input ---
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
        
        if (moveMenuSound) audioSource.PlayOneShot(moveMenuSound);
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

    private void SetupButtonInteractions()
    {
        for (int i = 0; i < mainButtons.Length; i++)
        {
            Button btn = mainButtons[i];
            if (btn != null)
            {
                // Disable all mouse interaction
                btn.interactable = false;
            }
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

        if (takeDamageSound) audioSource.PlayOneShot(takeDamageSound);

        if (playerCurrentHP == 0)
        {
            state = State.End;
            Debug.Log("PLAYER DIED");
            loader.EndCombat();
        }
    }

    public void PlayParrySound()
    {
        if (parrySound && audioSource) audioSource.PlayOneShot(parrySound);
    }

    public void SpawnParryEffect(Vector3 position, Sprite projectileSprite = null)
    {
        if (parryParticlePrefab)
        {
            var effect = Instantiate(parryParticlePrefab, position, Quaternion.identity, transform);
            
            if (projectileSprite != null)
            {
                var img = effect.GetComponent<UnityEngine.UI.Image>();
                if (img) img.sprite = projectileSprite;
            }

            Destroy(effect, 2f); // Auto-cleanup fallback
        }
    }

    public float GetDestroyLimitY()
    {
        return projectileDestroyLimit != null ? projectileDestroyLimit.anchoredPosition.y : -1200f;
    }

    // =========================
    // Player actions
    // =========================

    private IEnumerator PerformAttackRoutine()
    {
        state = State.Resolve; 
        
        int dmg = Random.Range(1, 7); // 1 to 6
        
        if (fightButton != null) SetButtonText(fightButton, "Rolling...");

        // If defined, run the visual 2D juice simulation
        if (dicePrefab != null && turnMenu != null)
        {
            DiceRollUI dice = Instantiate(dicePrefab, turnMenu.transform.parent);
            dice.gameObject.SetActive(true); // Assegurem que el Prefab estigui encés
            dice.transform.SetAsLastSibling(); // El posem per davant de tot al Canvas
            
            RectTransform rt = dice.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = new Vector2(0, 150); // Més amunt dels botons
                rt.localScale = Vector3.one;
            }

            yield return dice.RollRoutine(dmg);
            Destroy(dice.gameObject, 1.5f); // Cleanup dice after user sees the final number
        }

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
            EnemyAttackPattern chosenPattern = EnemyAttackPattern.RandomDrop;
            
            if (encounter != null && encounter.attackPatterns != null && encounter.attackPatterns.Length > 0)
            {
                int r = Random.Range(0, encounter.attackPatterns.Length);
                chosenPattern = encounter.attackPatterns[r];
            }

            // We assume EnemyAttackSpawner Configure signature is: Configure(GameObject prefab, EnemyAttackPattern pattern)
            spawner.Configure(encounter != null ? encounter.projectilePrefab : null, chosenPattern);
            yield return spawner.Run(dur);

            // Wait until all projectiles have finished traveling and are destroyed
            yield return new WaitUntil(() => ProjectileUI.activeProjectiles <= 0);
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
