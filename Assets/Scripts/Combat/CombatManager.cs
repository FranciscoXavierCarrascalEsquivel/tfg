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
    
    // Variables per controlar el buff de velocitat
    private int speedBuffRoundsLeft = 0;
    private float currentSpeedBuffValue = 0f;

    // Estat de defensa
    public bool IsDefending => isDefending;
    private bool isDefending = false;

    [Header("UI Stats")]
    [SerializeField] private RectTransform playerUIPanel;
    [SerializeField] private TMPro.TMP_Text playerNameText;
    [SerializeField] private TMPro.TMP_Text playerHPText;
    [SerializeField] private Image playerHPFill;
    [SerializeField] private Image playerPortraitImage; // NOU CAMP: Aquí poses la imatge a la que aniran les partícules
    
    [Space]
    [SerializeField] private RectTransform enemyUIPanel;
    [SerializeField] private TMPro.TMP_Text enemyNameText;
    [SerializeField] private TMPro.TMP_Text enemyHPText;
    [SerializeField] private Image enemyHPFill;
    [SerializeField] private Image enemyPortraitImage; // <- NOU CAMP PER LA FOTO

    [Header("Audio Feedback")]
    [SerializeField] private AudioClip moveMenuSound;
    [SerializeField] private AudioClip confirmMenuSound; // NOU: So al triar opció
    [SerializeField] private AudioClip attackSound;      // So al iniciar l'atac (premem E al minijoc)
    [SerializeField] private AudioClip enemyHitSound;    // So quan l'enemic rep dany
    [SerializeField] private AudioClip takeDamageSound;
    [SerializeField] private AudioClip parrySound;
    [SerializeField] private AudioClip defendParrySound; // NOU: So al fer parry mentre es defensa
    [SerializeField] private AudioClip explosionSound; // NOU: Soroll de la explosio de pixels
    [SerializeField] private AudioClip playerMoveSound;
    [SerializeField] private AudioClip victorySound;    // So de victòria al final del combat
    private AudioSource audioSource;
    private AudioSource loopAudioSource;

    [Header("VFX & Limits")]
    [SerializeField] private GameObject parryParticlePrefab;
    [SerializeField] private RectTransform projectileDestroyLimit;

    [Header("Item Animation Settings")]
    [Tooltip("Punt on neix l'objecte (part baixa de la pantalla)")]
    [SerializeField] private RectTransform throwStartPoint;
    [Tooltip("Alçada màxima de la paràbola")]
    [SerializeField] private float throwArcHeight = 400f;
    [Tooltip("Línia de terra on cauen els objectes després d'impactar")]
    [SerializeField] private RectTransform itemGroundLine;

    private HandController[] handControllers;

    // Default positions used for Entrance Animations
    private Vector2 playerUIOriginalPos;
    private Vector2 enemyUIOriginalPos;
    private Vector2 playerNameOriginalPos;
    private Vector2 playerHPTextOriginalPos;
    private Vector2 enemyNameOriginalPos;
    private Vector2 enemyHPTextOriginalPos;
    private Vector2 turnMenuOriginalPos;

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

        // Emmagatzemem les posicions originals de tots els textos per fer slide in/out
        if (playerNameText != null) playerNameOriginalPos = playerNameText.rectTransform.anchoredPosition;
        if (playerHPText != null) playerHPTextOriginalPos = playerHPText.rectTransform.anchoredPosition;
        if (enemyNameText != null) enemyNameOriginalPos = enemyNameText.rectTransform.anchoredPosition;
        if (enemyHPText != null) enemyHPTextOriginalPos = enemyHPText.rectTransform.anchoredPosition;
        
        // Inicialment els desplacem fora (cap amunt)
        if (playerNameText != null) playerNameText.rectTransform.anchoredPosition += new Vector2(0, 300f);
        if (playerHPText != null) playerHPText.rectTransform.anchoredPosition += new Vector2(0, 300f);
        if (enemyNameText != null) enemyNameText.rectTransform.anchoredPosition += new Vector2(0, 300f);
        if (enemyHPText != null) enemyHPText.rectTransform.anchoredPosition += new Vector2(0, 300f);
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

        // Llegeix HP de l'inventari persistent (si existeix i té vida > 0)
        if (PlayerInventory.Instance != null && PlayerInventory.Instance.CurrentHP > 0)
        {
            playerMaxHP = PlayerInventory.Instance.MaxHP;
            playerCurrentHP = PlayerInventory.Instance.CurrentHP;
        }
        else
        {
            playerCurrentHP = playerMaxHP;
        }
        
        // Sobreescriu valors base d'enemic si heu fet algun perfil (ScriptableObject) personalitzat
        string finalEnemyName = "MONSTER";
        Sprite finalEnemySprite = encounter != null ? encounter.enemyPortrait : null;
        
        if (encounter != null && encounter.enemyProfile != null)
        {
            enemyMaxHP = Random.Range(encounter.enemyProfile.minHP, encounter.enemyProfile.maxHP + 1);
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
        if (enemyUIPanel != null) StartCoroutine(SlideInRect(enemyUIPanel, enemyUIOriginalPos, new Vector2(0, 300f), 0.7f));
        
        // També els textos individuals si existeixen
        if (playerNameText != null) StartCoroutine(SlideInRect(playerNameText.rectTransform, playerNameOriginalPos, new Vector2(0, 300f), 0.7f));
        if (playerHPText != null) StartCoroutine(SlideInRect(playerHPText.rectTransform, playerHPTextOriginalPos, new Vector2(0, 300f), 0.7f));
        if (enemyNameText != null) StartCoroutine(SlideInRect(enemyNameText.rectTransform, enemyNameOriginalPos, new Vector2(0, 300f), 0.7f));
        if (enemyHPText != null) StartCoroutine(SlideInRect(enemyHPText.rectTransform, enemyHPTextOriginalPos, new Vector2(0, 300f), 0.7f));
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

    private IEnumerator SlideOutRect(RectTransform rect, Vector2 originalPos, Vector2 exitOffset, float duration)
    {
        if (rect == null) yield break;
        
        Vector2 targetPos = originalPos + exitOffset;
        float time = 0;
        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;
            // Cubic Ease In per una sortida que s'accelera
            float easeT = t * t * t;
            rect.anchoredPosition = Vector2.Lerp(originalPos, targetPos, easeT);
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

        // Bloquejar input del combat mentre l'inventari és obert
        if (InventoryMenuUI.IsOpen) return;

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
        if (confirmMenuSound && audioSource) audioSource.PlayOneShot(confirmMenuSound);
        
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

    private RectTransform selectionCursorFrame;

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

            // L'antic codi destruïa el contorn (outline) descontrolant l'ombra/disseny fosc del botó en la UI predeterminada.
            // Ara ho evitem absolutament per mantenir els botons vius i idèntics a la scene.
        }

        if (mainButtons.Length == 0 || selectedIndex < 0 || selectedIndex >= mainButtons.Length) return;

        Button selBtn = mainButtons[selectedIndex];
        if (selBtn == null || !selBtn.gameObject.activeInHierarchy) return;

        if (selectionCursorFrame == null)
        {
            GameObject go = new GameObject("SelectionFX_Frame");
            selectionCursorFrame = go.AddComponent<RectTransform>();
            
            // Afegim 4 sub-imatges per fer les línies de contorn (vores extremadament fines de 2px segons pauta)
            CreateBorder(selectionCursorFrame, "Top", new Vector2(0, 1), new Vector2(1, 1), new Vector2(-2, 0), new Vector2(2, 2));
            CreateBorder(selectionCursorFrame, "Bot", new Vector2(0, 0), new Vector2(1, 0), new Vector2(-2, -2), new Vector2(2, 0));
            CreateBorder(selectionCursorFrame, "Left", new Vector2(0, 0), new Vector2(0, 1), new Vector2(-2, 0), new Vector2(0, 0));
            CreateBorder(selectionCursorFrame, "Right", new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 0), new Vector2(2, 0));

            StartCoroutine(CursorGlowRoutine(selectionCursorFrame));
        }

        selectionCursorFrame.SetParent(selBtn.transform, false);
        selectionCursorFrame.SetAsLastSibling(); // Sempre per sobre per fer d'il·luminador
        
        selectionCursorFrame.anchorMin = Vector2.zero;
        selectionCursorFrame.anchorMax = Vector2.one;
        selectionCursorFrame.offsetMin = Vector2.zero;
        selectionCursorFrame.offsetMax = Vector2.zero;
    }

    private void CreateBorder(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.offsetMin = oMin; rt.offsetMax = oMax;
        
        Image img = go.AddComponent<Image>();
        img.color = new Color(1f, 0.9f, 0.1f, 1f); // Groc
        img.raycastTarget = false;
    }

    private IEnumerator CursorGlowRoutine(RectTransform rt)
    {
        Image[] borders = rt.GetComponentsInChildren<Image>();
        
        while (true)
        {
            if (rt == null) yield break;

            float t = (Mathf.Sin(Time.unscaledTime * 4f) + 1f) / 2f; // Animació més lenta
            rt.localScale = Vector3.Lerp(new Vector3(1f, 1f, 1f), new Vector3(1.02f, 1.05f, 1f), t); // Rebot menys agressiu
            
            Color glowCore = new Color(1f, 0.9f, 0.1f, Mathf.Lerp(0.5f, 1f, t));
            foreach(var img in borders)
            {
                if (img != null) img.color = glowCore;
            }
            
            // Spawn random partícules menys freqüents
            if (rt.gameObject.activeInHierarchy && Random.value < 0.05f)
            {
                SpawnCursorParticle(rt);
            }
            
            yield return null;
        }
    }

    private void SpawnCursorParticle(RectTransform parent)
    {
        GameObject p = new GameObject("C_Part");
        p.transform.SetParent(parent, false);
        p.transform.SetAsLastSibling(); // Per sobre del contorn directament
        
        RectTransform partRT = p.AddComponent<RectTransform>();
        
        // Col·loquem la partícula en algun punt dels voltants dels marges del botó
        float anchorX = Random.value < 0.5f ? (Random.value < 0.5f ? 0f : 1f) : Random.value;
        float anchorY = (anchorX > 0f && anchorX < 1f) ? (Random.value < 0.5f ? 0f : 1f) : Random.value;
        
        partRT.anchorMin = new Vector2(anchorX, anchorY);
        partRT.anchorMax = new Vector2(anchorX, anchorY);
        partRT.anchoredPosition = Vector2.zero;

        float size = Random.Range(3f, 6f); // Molt més petits i subtils
        partRT.sizeDelta = new Vector2(size, size);
        
        Image img = p.AddComponent<Image>();
        img.raycastTarget = false;
        img.color = new Color(1f, 0.95f, 0.4f, 1f);
        
        StartCoroutine(AnimateCursorParticle(partRT, img));
    }

    private IEnumerator AnimateCursorParticle(RectTransform rt, Image img)
    {
        Vector2 vel = new Vector2(Random.Range(-15f, 15f), Random.Range(20f, 60f)); // Moviment més suau cap a dalt
        float life = Random.Range(0.4f, 1.0f);
        float elapsed = 0f;
        
        while(elapsed < life)
        {
            if (rt == null || img == null) yield break;
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / life;
            
            rt.anchoredPosition += vel * Time.unscaledDeltaTime;
            
            Color c = img.color;
            c.a = 1f - t;
            img.color = c;
            rt.localScale = Vector3.one * (1f - t);
            
            yield return null;
        }
        
        if (rt != null) Destroy(rt.gameObject);
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

        int finalDamage = isDefending ? Mathf.CeilToInt(damage / 2f) : damage;
        playerCurrentHP -= finalDamage;
        if (playerCurrentHP < 0) playerCurrentHP = 0;
        UpdateStatsUI();

        if (takeDamageSound) audioSource.PlayOneShot(takeDamageSound);

        // Guarda HP actualitzat a l'inventari persistent
        if (PlayerInventory.Instance != null)
            PlayerInventory.Instance.SetHP(playerCurrentHP);

        if (playerCurrentHP == 0)
        {
            state = State.End;
            Debug.Log("PLAYER DIED");
            loader.EndCombat();
        }
    }

    public void PlayParrySound()
    {
        if (isDefending && defendParrySound != null)
        {
            if (audioSource) audioSource.PlayOneShot(defendParrySound);
        }
        else
        {
            if (parrySound && audioSource) audioSource.PlayOneShot(parrySound);
        }
    }

    public void PlayExplosionSound()
    {
        if (explosionSound && audioSource) audioSource.PlayOneShot(explosionSound);
    }

    public void SpawnParryEffect(Vector3 position, Sprite projectileSprite = null)
    {
        if (parryParticlePrefab)
        {
            var effect = Instantiate(parryParticlePrefab, position, Quaternion.identity, transform);
            
            if (projectileSprite != null)
            {
                var img = effect.GetComponent<UnityEngine.UI.Image>();
                if (img) 
                {
                    img.sprite = projectileSprite;
                    if (isDefending) img.color = new Color(0.2f, 1f, 0.2f, 1f); // Verd si estem defensant
                }
            }

            Destroy(effect, 2f); // Auto-cleanup fallback
        }
    }

    public void OnParrySuccess(Vector3 pos, Sprite projectileSprite)
    {
        PlayParrySound();
        SpawnParryEffect(pos, projectileSprite);

        if (isDefending)
        {
            playerCurrentHP++;
            if (playerCurrentHP > playerMaxHP) playerCurrentHP = playerMaxHP;
            UpdateStatsUI();

            // Mostrem FX de curació sobre la barra de vida per reforçar el feedback
            Transform canvasParent = turnMenu != null ? turnMenu.transform.parent : transform;
            Image targetImg = playerPortraitImage != null ? playerPortraitImage : playerHPFill;
            HealFXUI.ShowAboveBar(canvasParent, targetImg, "+1", new Color(0.25f, 1f, 0.35f), 1f);
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
            skillCheck.SetAttackSound(attackSound);
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

        // So i tremolor de l'enemic en rebre dany
        if (enemyHitSound) audioSource.PlayOneShot(enemyHitSound);
        if (enemyPortraitImage != null) StartCoroutine(ShakeEnemySprite(enemyPortraitImage.rectTransform, 0.35f, 14f));

        // Esperem un petit instant curt fins passar al torn enemic un cop ha donat l'espasada
        yield return new WaitForSeconds(0.6f);

        if (enemyCurrentHP == 0)
        {
            state = State.End;
            Debug.Log("ENEMY DEFEATED");
            StartCoroutine(DefeatAndVictoryRoutine());
            yield break;
        }

        EndPlayerTurn();
    }

    // Tremolor de l'sprite de l'enemic en rebre dany
    private IEnumerator ShakeEnemySprite(RectTransform rt, float duration, float magnitude)
    {
        if (rt == null) yield break;
        Vector2 originalPos = rt.anchoredPosition;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float damping = 1f - Mathf.Clamp01(elapsed / duration); // Va decreixent
            float x = Random.Range(-1f, 1f) * magnitude * damping;
            float y = Random.Range(-1f, 1f) * magnitude * damping;
            rt.anchoredPosition = originalPos + new Vector2(x, y);
            yield return null;
        }
        rt.anchoredPosition = originalPos;
    }

    /// Quan l'enemic mor: primer l'escapcem en pixels, despres la pantalla de victoria.
    private IEnumerator DefeatAndVictoryRoutine()
    {
        ShowTurnMenu(false);

        // Slide Out de tota la UI de combat cap amunt
        float outDur = 0.5f;
        Vector2 outOff = new Vector2(0, 400f);
        if (playerUIPanel != null) StartCoroutine(SlideOutRect(playerUIPanel, playerUIOriginalPos, outOff, outDur));
        if (enemyUIPanel != null) StartCoroutine(SlideOutRect(enemyUIPanel, enemyUIOriginalPos, outOff, outDur));
        if (playerNameText != null) StartCoroutine(SlideOutRect(playerNameText.rectTransform, playerNameOriginalPos, outOff, outDur));
        if (playerHPText != null) StartCoroutine(SlideOutRect(playerHPText.rectTransform, playerHPTextOriginalPos, outOff, outDur));
        if (enemyNameText != null) StartCoroutine(SlideOutRect(enemyNameText.rectTransform, enemyNameOriginalPos, outOff, outDur));
        if (enemyHPText != null) StartCoroutine(SlideOutRect(enemyHPText.rectTransform, enemyHPTextOriginalPos, outOff, outDur));

        // So de mort de l'enemic (des del seu perfil)
        AudioClip deathClip = encounter?.enemyProfile?.deathSound;
        if (deathClip) audioSource.PlayOneShot(deathClip);

        if (enemyPortraitImage != null && enemyPortraitImage.enabled)
        {
            bool fxDone = false;
            // Determina el canvas pare (el mateix que el panell de victoria usara)
            Transform canvasParent = enemyPortraitImage.transform.parent;
            EnemyDestroyFX.Play(enemyPortraitImage, () => fxDone = true);
            yield return new WaitUntil(() => fxDone);
            yield return new WaitForSeconds(0.25f); // petit respir
        }

        StartCoroutine(VictoryRoutine());
    }

    private IEnumerator VictoryRoutine()
    {
        ShowTurnMenu(false);
        // So de victòria
        if (victorySound) audioSource.PlayOneShot(victorySound);

        if (enemyHPText) enemyHPText.text = "";
        if (playerHPText) playerHPText.text = "";

        // Càlcul de premis segons el perfil
        int gold = Random.Range(30, 80);
        System.Collections.Generic.List<string> earnedItems = new System.Collections.Generic.List<string>();

        if (encounter != null && encounter.enemyProfile != null)
        {
            gold = Random.Range(encounter.enemyProfile.goldRewardMin, encounter.enemyProfile.goldRewardMax + 1);
            
            if (encounter.enemyProfile.drops != null)
            {
                foreach (var drop in encounter.enemyProfile.drops)
                {
                    int prob = drop.probability;
                    while (prob >= 100)
                    {
                        earnedItems.Add(drop.itemName);
                        prob -= 100;
                    }
                    if (prob > 0 && Random.Range(0, 100) < prob)
                    {
                        earnedItems.Add(drop.itemName);
                    }
                }
            }
        }

        // Guarda HP restant del jugador i recompenses a l'inventari persistent
        if (PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.SetHP(playerCurrentHP);
            PlayerInventory.Instance.AddGold(gold);
            foreach (var item in earnedItems)
            {
                if (!string.IsNullOrEmpty(item) && item != "none" && item != "—")
                    PlayerInventory.Instance.AddItem(item);
            }
        }

        int totalGold = PlayerInventory.Instance != null ? PlayerInventory.Instance.Gold : gold;

        // Mostra el panell animat de victòria
        Transform canvasParent = turnMenu != null ? turnMenu.transform.parent : transform;
        bool done = false;
        VictoryPanelUI.Create(canvasParent, gold, earnedItems, totalGold, () => done = true);

        yield return new WaitUntil(() => done);

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
        isDefending = true;
        EndPlayerTurn();
    }

    private void OnItem()
    {
        InventoryMenuUI.Show(isCombat: true, onItemSelected: (profile) =>
        {
            StartCoroutine(ProcessItemSequence(profile));
        });
    }

    private IEnumerator ProcessItemSequence(ItemProfile profile)
    {
        yield return StartCoroutine(ApplyItemEffect(profile));
        
        if (enemyCurrentHP <= 0)
        {
            state = State.End;
            StartCoroutine(DefeatAndVictoryRoutine());
        }
        else
        {
            EndPlayerTurn();
        }
    }

    private IEnumerator ApplyItemEffect(ItemProfile profile)
    {
        Debug.Log($"Utilitzant objecte en combat: {profile.itemName}");
        Transform canvasParent = turnMenu != null ? turnMenu.transform.parent : transform;

        if (profile.effectType == ItemEffectType.HealPlayer)
        {
            if (profile.useSound != null) audioSource.PlayOneShot(profile.useSound);
            playerCurrentHP += profile.effectValue;
            if (playerCurrentHP > playerMaxHP) playerCurrentHP = playerMaxHP;

            // Text verd + partícules verdes
            Image targetImg = playerPortraitImage != null ? playerPortraitImage : playerHPFill;
            HealFXUI.ShowAboveBar(canvasParent, targetImg, $"+{profile.effectValue} HP",
                                  new Color(0.25f, 1f, 0.35f));
        }
        else if (profile.effectType == ItemEffectType.DamageEnemy)
        {
            // --- ANIMACIÓ DE TIRAR OBJECTE ---
            // Ara li passem un callback o esperem a que impacti per restar vida
            yield return StartCoroutine(AnimateItemThrow(profile, () => {
                // AQUEST CODI S'EXECUTA JUST EN EL MOMENT DE L'IMPACTE
                enemyCurrentHP -= profile.effectValue;
                if (enemyCurrentHP < 0) enemyCurrentHP = 0;
                UpdateStatsUI();

                if (enemyHitSound != null) audioSource.PlayOneShot(enemyHitSound);

                // Taronja sobre la barra de l'enemic
                HealFXUI.ShowAboveBar(canvasParent, enemyHPFill, $"-{profile.effectValue} HP",
                                      new Color(1f, 0.45f, 0.1f));
            }));
        }
        else if (profile.effectType == ItemEffectType.SpeedUpHands)
        {
            if (profile.useSound != null) audioSource.PlayOneShot(profile.useSound);
            var hands = FindObjectsByType<HandController>(FindObjectsSortMode.None);
            
            if (speedBuffRoundsLeft <= 0)
            {
                currentSpeedBuffValue = (profile.effectValue / 100f);
                foreach (var h in hands) h.speedMultiplier += currentSpeedBuffValue;
            }
            
            speedBuffRoundsLeft = profile.buffDurationRounds;
            HealFXUI.Show(canvasParent, $"VELOC +{profile.effectValue}% ({speedBuffRoundsLeft} TORNS)", new Color(1f, 0.9f, 0.15f));
        }
        UpdateStatsUI();
        yield return null;
    }

    private IEnumerator AnimateItemThrow(ItemProfile profile, System.Action onImpact)
    {
        Transform canvasParent = turnMenu != null ? turnMenu.transform.parent : transform;
        
        GameObject go = new GameObject("ThrownItem");
        go.transform.SetParent(canvasParent, false);
        Image img = go.AddComponent<Image>();
        img.sprite = profile.itemIcon;
        img.preserveAspect = true;
        img.raycastTarget = false;
        
        RectTransform rt = go.GetComponent<RectTransform>();

        // 1. Punt d'Inici (Des del Inspector o fallback)
        Vector2 startPos = throwStartPoint != null ? throwStartPoint.anchoredPosition : new Vector2(0, -700f); 

        // 2. Punt de Col·lisió (Directament el centre del rival)
        Vector2 impactPos;
        if (enemyPortraitImage != null) impactPos = enemyPortraitImage.rectTransform.anchoredPosition;
        else if (enemyUIPanel != null) impactPos = enemyUIPanel.anchoredPosition;
        else impactPos = new Vector2(0, 300f);

        // 3. Línia de terra (On cau després de col·lisionar)
        float groundY = itemGroundLine != null ? itemGroundLine.anchoredPosition.y : impactPos.y - 150f;
        Vector2 groundPos = new Vector2(impactPos.x + Random.Range(-100f, 100f), groundY);

        rt.anchoredPosition = startPos;
        float startScale = 3.5f;
        float endScale = 0.8f;
        rt.localScale = Vector3.one * startScale;

        if (profile.useSound != null) audioSource.PlayOneShot(profile.useSound);

        // --- FASE 1: VOL FINS L'IMPACTE ---
        float duration = 0.5f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            Vector2 currentPos = Vector2.Lerp(startPos, impactPos, t);
            float parabola = 4f * t * (1f - t);
            currentPos.y += parabola * throwArcHeight;
            rt.anchoredPosition = currentPos;
            rt.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, t);
            rt.Rotate(0, 0, -800f * Time.deltaTime);
            yield return null;
        }

        // --- MOMENT DE L'IMPACTE ---
        onImpact?.Invoke();

        // --- FASE 2: CAURE AL TERRA ---
        elapsed = 0f;
        float fallDuration = 0.3f;
        Vector2 posAtImpact = rt.anchoredPosition;
        while (elapsed < fallDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fallDuration;
            // Cau de forma més directa (Ease In)
            rt.anchoredPosition = Vector2.Lerp(posAtImpact, groundPos, t * t);
            rt.Rotate(0, 0, -200f * Time.deltaTime);
            yield return null;
        }

        // Petit rebot al terra
        float bounce = 0f;
        while(bounce < 0.2f)
        {
            bounce += Time.deltaTime;
            float b = Mathf.Abs(Mathf.Sin(bounce * Mathf.PI / 0.2f)) * 15f;
            rt.anchoredPosition = groundPos + new Vector2(0, b);
            yield return null;
        }
        rt.anchoredPosition = groundPos;

        yield return new WaitForSeconds(1f);
        
        float fade = 1f;
        while(fade > 0f)
        {
            fade -= Time.deltaTime * 2f;
            img.color = new Color(1,1,1,fade);
            yield return null;
        }
        Destroy(go);
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
            
            // Esperem un instant per assegurar que els últims projectils s'han registrat be
            yield return new WaitForSeconds(0.1f);

            // Wait until all projectiles have finished traveling and are destroyed
            yield return new WaitUntil(() => ProjectileUI.activeProjectiles <= 0);
        }
        else
        {
            yield return new WaitForSeconds(dur);
        }

        Debug.Log("ENEMY TURN ended");

        SetHandsActive(false);

        // Disminuïm i revisem l'estat del buff de velocitat en tornar al torn del jugador
        if (speedBuffRoundsLeft > 0)
        {
            speedBuffRoundsLeft--;
            if (speedBuffRoundsLeft == 0)
            {
                // El buff s'ha esgotat, el traiem!
                var hands = FindObjectsByType<HandController>(FindObjectsSortMode.None);
                foreach (var h in hands) h.speedMultiplier -= currentSpeedBuffValue;
                currentSpeedBuffValue = 0f;
                Debug.Log("Buff de velocitat de mans exhaurit!");
            }
        }

        state = State.PlayerTurn;
        isDefending = false; // Reset de la defensa al començar el següent torn
        ShowTurnMenu(true);
    }
}
