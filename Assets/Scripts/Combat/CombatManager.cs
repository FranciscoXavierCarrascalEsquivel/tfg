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
    [SerializeField] private SkillCheckUI skillCheckPrefab;

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
    [SerializeField] private RectTransform playerUIPanel;
    [SerializeField] private TMPro.TMP_Text playerNameText;
    [SerializeField] private TMPro.TMP_Text playerHPText;
    [SerializeField] private Image playerHPFill;
    
    [Space]
    [SerializeField] private RectTransform enemyUIPanel;
    [SerializeField] private TMPro.TMP_Text enemyNameText;
    [SerializeField] private TMPro.TMP_Text enemyHPText;
    [SerializeField] private Image enemyHPFill;
    [SerializeField] private Image enemyPortraitImage; // <- NOU CAMP PER LA FOTO

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
    private Vector2 playerUIOriginalPos;
    private Vector2 enemyUIOriginalPos;

    private void Awake()
    {
        if (enemyPortraitImage != null) enemyPortraitImage.enabled = false;

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
        
        if (playerUIPanel != null) 
        {
            playerUIOriginalPos = playerUIPanel.anchoredPosition;
            playerUIPanel.anchoredPosition = playerUIOriginalPos + new Vector2(0, 300f);
        }
        else if (playerHPText != null) // Fallback al text si et descuides del panel
        {
            var rt = playerHPText.GetComponent<RectTransform>();
            playerUIOriginalPos = rt.anchoredPosition;
            rt.anchoredPosition = playerUIOriginalPos + new Vector2(0, 300f);
        }
        
        if (enemyUIPanel != null) 
        {
            enemyUIOriginalPos = enemyUIPanel.anchoredPosition;
            enemyUIPanel.anchoredPosition = enemyUIOriginalPos + new Vector2(0, 300f);
        }
        else if (enemyHPText != null) // Fallback
        {
            var rt = enemyHPText.GetComponent<RectTransform>();
            enemyUIOriginalPos = rt.anchoredPosition;
            rt.anchoredPosition = enemyUIOriginalPos + new Vector2(0, 300f);
        }
    }

    public void PreSetup(CombatEncounter encounter)
    {
        Sprite finalEnemySprite = encounter != null ? encounter.enemyPortrait : null;
        if (encounter != null && encounter.enemyProfile != null && encounter.enemyProfile.enemyPortrait != null)
        {
            finalEnemySprite = encounter.enemyProfile.enemyPortrait;
        }

        if (enemyPortraitImage != null)
        {
            if (finalEnemySprite != null)
            {
                enemyPortraitImage.sprite = finalEnemySprite;
                enemyPortraitImage.enabled = true;
            }
            else
            {
                enemyPortraitImage.enabled = false;
            }
        }
    }

    public void Begin(CombatEncounter encounter, CombatLoader loader)
    {
        this.encounter = encounter;
        this.loader = loader;

        playerCurrentHP = playerMaxHP;
        
        // Sobreescriu valors base d'enemic si heu fet algun perfil (ScriptableObject) personalitzat
        string finalEnemyName = "MONSTER";
        Sprite finalEnemySprite = encounter != null ? encounter.enemyPortrait : null;
        
        if (encounter != null && encounter.enemyProfile != null)
        {
            enemyMaxHP = encounter.enemyProfile.maxHP;
            finalEnemyName = encounter.enemyProfile.enemyName.ToUpper();
            if (encounter.enemyProfile.enemyPortrait != null) finalEnemySprite = encounter.enemyProfile.enemyPortrait;
        }

        enemyCurrentHP = enemyMaxHP;
        UpdateStatsUI(true); // Posa les barres completes de cop a l'inci

        // Aplica l'sprite visual
        if (enemyPortraitImage != null && finalEnemySprite != null)
        {
            enemyPortraitImage.sprite = finalEnemySprite;
            enemyPortraitImage.enabled = true;
        }
        else if (enemyPortraitImage != null)
        {
            enemyPortraitImage.enabled = false;
        }

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
        ShowTurnMenu(true);

        // Configura noms
        if (playerNameText != null) playerNameText.text = "FRANC";
        if (enemyNameText != null) enemyNameText.text = finalEnemyName;

        // Dispara les animacions d'entrada tipus Slide UI per tota la resta de text/panells
        if (playerUIPanel != null) StartCoroutine(SlideInRect(playerUIPanel, playerUIOriginalPos, new Vector2(0, 300f), 0.7f));
        else if (playerHPText != null) StartCoroutine(SlideInRect(playerHPText.GetComponent<RectTransform>(), playerUIOriginalPos, new Vector2(0, 300f), 0.7f));
        
        if (enemyUIPanel != null) StartCoroutine(SlideInRect(enemyUIPanel, enemyUIOriginalPos, new Vector2(0, 300f), 0.7f));
        else if (enemyHPText != null) StartCoroutine(SlideInRect(enemyHPText.GetComponent<RectTransform>(), enemyUIOriginalPos, new Vector2(0, 300f), 0.7f));
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
        else if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return))
        {
            ConfirmSelection();
        }
        else if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift) || Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Escape))
        {
            if (currentPhase == MenuPhase.Target)
            {
                SetMenuPhase(MenuPhase.Main);
            }
        }
        
        // --- DEBUG SHORTCUT ---
        // Prem 'O' en qualsevol moment del teu torn per forçar forçar la victòria i veure l'animació reverse
        if (Input.GetKeyDown(KeyCode.O))
        {
            Debug.Log("DEBUG: Forçant Victòria amb la 'O'");
            state = State.End;
            StartCoroutine(VictoryRoutine());
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
            // Saltem el pas de Targejar l'Enemic (TargetPhase) entrant directament a l'Atac (La Ruleta)
            if (selectedIndex == 0) StartCoroutine(PerformAttackRoutine());
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
            }
            
            // Re-evalua l'estil constantment per si volem canvis agressius de color cridaner
            outline.effectColor = new Color(1f, 0.95f, 0f, 1f); // Groc gairebé pur i brillant
            outline.effectDistance = new Vector2(8, -8); // Molt més gruixut i visible

            outline.enabled = (i == selectedIndex);
        }
    }

    private Coroutine playerHPAnim;
    private Coroutine enemyHPAnim;

    private void UpdateStatsUI(bool instant = false)
    {
        if (playerHPText) playerHPText.text = $"HP {playerCurrentHP} / {playerMaxHP}";
        if (enemyHPText) enemyHPText.text = $"HP {enemyCurrentHP} / {enemyMaxHP}";

        float targetPlayerFill = (float)playerCurrentHP / playerMaxHP;
        if (playerHPFill) 
        {
            if (instant) playerHPFill.fillAmount = targetPlayerFill;
            else 
            {
                if (playerHPAnim != null) StopCoroutine(playerHPAnim);
                playerHPAnim = StartCoroutine(AnimateHPBar(playerHPFill, targetPlayerFill, 0.4f));
            }
        }

        float targetEnemyFill = (float)enemyCurrentHP / enemyMaxHP;
        if (enemyHPFill) 
        {
            if (instant) enemyHPFill.fillAmount = targetEnemyFill;
            else 
            {
                if (enemyHPAnim != null) StopCoroutine(enemyHPAnim);
                enemyHPAnim = StartCoroutine(AnimateHPBar(enemyHPFill, targetEnemyFill, 0.4f));
            }
        }
        
        if (currentPhase == MenuPhase.Target && fightButton != null)
        {
            SetButtonText(fightButton, $"Enemy ({enemyCurrentHP} HP)");
        }
    }

    private IEnumerator AnimateHPBar(Image hpImage, float targetFill, float duration)
    {
        if (hpImage == null) yield break;
        
        float startFill = hpImage.fillAmount;
        float time = 0f;
        while (time < duration)
        {
            time += Time.deltaTime;
            // Moviment Cúbic suau però directe
            float t = time / duration;
            float easeT = 1f - Mathf.Pow(1f - t, 3f);
            hpImage.fillAmount = Mathf.Lerp(startFill, targetFill, easeT);
            yield return null;
        }
        hpImage.fillAmount = targetFill;
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
        // Changing state to something else avoids PlayerTurn triggering ConfirmSelection via Space again.
        state = State.Resolve; 
        
        // Amaguem el menú amb la seva animació de sortida instantàniament en decidir atacar perque el centre d'atenció sigui la ruleta
        ShowTurnMenu(false);

        int finalDmg = 0;

        // Perform Skill Check if available
        if (skillCheckPrefab != null && turnMenu != null)
        {
            SkillCheckUI skillCheck = Instantiate(skillCheckPrefab, turnMenu.transform.parent);
            skillCheck.gameObject.SetActive(true); 
            skillCheck.transform.SetAsLastSibling(); 
            
            RectTransform rt = skillCheck.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = new Vector2(0, 150); // Més amunt dels botons
            }

            // Wait until skill check finishes and returns damage via callback
            bool checkFinished = false;
            skillCheck.StartSkillCheck((calcDmg) => 
            {
                finalDmg = calcDmg;
                checkFinished = true;
            });
            
            // Aturam l'execució d'aquest IEnumerator fins que la funcio onDamage callback hagi set cridada.
            yield return new WaitUntil(() => checkFinished);
            
            Destroy(skillCheck.gameObject); 
        }
        else 
        {
            // Fallback just in case no UI
            finalDmg = Random.Range(5, 15);
            yield return new WaitForSeconds(1f);
        }

        Debug.Log($"FIGHT! Dealt {finalDmg} damage.");

        enemyCurrentHP -= finalDmg;
        if (enemyCurrentHP < 0) enemyCurrentHP = 0;
        UpdateStatsUI();

        // Esperem un petit instant curt fins passar al torn enemic un cop ha donat l'espasada
        yield return new WaitForSeconds(0.6f);

        if (enemyCurrentHP == 0)
        {
            state = State.End;
            Debug.Log("ENEMY DEFEATED");
            StartCoroutine(VictoryRoutine());
            yield break;
        }

        EndPlayerTurn();
    }

    private IEnumerator VictoryRoutine()
    {
        ShowTurnMenu(false);
        if (enemyHPText) enemyHPText.text = "";
        if (playerHPText) playerHPText.text = "";
        
        // Creem un Text de Victoria improvisat a la pantalla per donar el resum
        GameObject go = new GameObject("VictoryPanel");
        go.transform.SetParent(turnMenu.transform.parent, false);
        var txt = go.AddComponent<TMPro.TextMeshProUGUI>();
        txt.fontSize = 28;
        txt.alignment = TMPro.TextAlignmentOptions.Center;
        txt.color = Color.yellow;
        
        // Càlcul de premis segons el perfil
        int gold = Random.Range(30, 80);
        string itemName = "Potion";
        
        if (encounter != null && encounter.enemyProfile != null)
        {
            gold = Random.Range(encounter.enemyProfile.goldRewardMin, encounter.enemyProfile.goldRewardMax + 1);
            itemName = encounter.enemyProfile.dropItemName;
        }

        txt.text = $"YOU WON!\n\nObtained: {gold} G\nObject: '{itemName}'\n\n[Press E or Enter to continue]";
        
        RectTransform rt = txt.GetComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(0, 100);
        rt.sizeDelta = new Vector2(800, 400);

        // Bloquegem un petit instant per previndre tancar al instant i donar temps a llegir
        yield return new WaitForSeconds(0.5f); 
        
        // Esperem confirmació de l'usuari amb la 'E'
        while (!Input.GetKeyDown(KeyCode.E) && !Input.GetKeyDown(KeyCode.Space) && !Input.GetKeyDown(KeyCode.Return))
        {
            yield return null;
        }

        loader.EndCombat();
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

    private Coroutine turnMenuAnim;

    private void ShowTurnMenu(bool show)
    {
        if (turnMenu == null) return;
        
        if (turnMenuAnim != null) StopCoroutine(turnMenuAnim);
        
        if (show) 
        {
            turnMenu.SetActive(true);
            SetMenuPhase(MenuPhase.Main);
            turnMenuAnim = StartCoroutine(SlideMenuTo(turnMenu.GetComponent<RectTransform>(), turnMenuOriginalPos, 0.6f, true));
        }
        else
        {
            // Amagar cap avall només si està de fet a l'escena:
            if (turnMenu.activeInHierarchy)
            {
                turnMenuAnim = StartCoroutine(SlideOutAndHide(turnMenu.GetComponent<RectTransform>(), turnMenuOriginalPos + new Vector2(0, -500f), 0.5f));
            }
        }
    }

    private IEnumerator SlideMenuTo(RectTransform rect, Vector2 targetPos, float duration, bool easeOut)
    {
        if (rect == null) yield break;
        
        Vector2 startPos = rect.anchoredPosition;
        float time = 0;
        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;
            float easeT = easeOut ? (1f - Mathf.Pow(1f - t, 3f)) : (t * t * t);
            
            rect.anchoredPosition = Vector2.Lerp(startPos, targetPos, easeT);
            yield return null;
        }
        rect.anchoredPosition = targetPos;
    }

    private IEnumerator SlideOutAndHide(RectTransform rect, Vector2 targetPos, float duration)
    {
        yield return SlideMenuTo(rect, targetPos, duration, false);
        turnMenu.SetActive(false);
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

        float dur = 2f;
        if (encounter != null) dur = encounter.enemyProfile != null ? encounter.enemyProfile.attackDuration : encounter.enemyAttackDuration;

        var spawner = FindFirstObjectByType<EnemyAttackSpawner>();
        if (spawner != null)
        {
            EnemyAttackPattern chosenPattern = EnemyAttackPattern.RandomDrop;
            GameObject prefab = encounter != null ? encounter.projectilePrefab : null;

            if (encounter != null)
            {
                if (encounter.enemyProfile != null)
                {
                    prefab = encounter.enemyProfile.projectilePrefab;
                    if (encounter.enemyProfile.attackPatterns != null && encounter.enemyProfile.attackPatterns.Length > 0)
                    {
                        chosenPattern = encounter.enemyProfile.attackPatterns[Random.Range(0, encounter.enemyProfile.attackPatterns.Length)];
                    }
                }
                else if (encounter.attackPatterns != null && encounter.attackPatterns.Length > 0)
                {
                    chosenPattern = encounter.attackPatterns[Random.Range(0, encounter.attackPatterns.Length)];
                }
            }

            spawner.Configure(prefab, chosenPattern);
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
