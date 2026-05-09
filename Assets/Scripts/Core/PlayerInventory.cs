using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton persistent entre escenes. Guarda l'or, els objectes i la vida del jugador.
/// </summary>

[System.Serializable]
public class ShopDialogVariant
{
    [TextArea(2, 4)] public string text = "...";
    [Range(0f, 100f)] public float weight = 10f;
    [Tooltip("Sprite opcional per aquesta frase (deixa-ho buit per utilitzar el per defecte)")]
    public Sprite expressionSprite;
}

public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory Instance { get; private set; }

    [Header("Valors inicials")]
    [SerializeField] private int startingMaxHP = 100;
    
    [Header("Item Database")]
    [Tooltip("Límit global d'objectes a l'inventari")]
    public int maxItemsCapacity = 20;

    [Tooltip("Posa aquí els Item Profiles per relacionar-los.")]
    public List<ItemProfile> itemDatabase = new List<ItemProfile>();

    [Header("Enemy Database")]
    [Tooltip("Posa aquí els Enemy Profiles per poder consultar la llista i els límits.")]
    public List<EnemyProfile> enemyDatabase = new List<EnemyProfile>();

    [Header("Shop")]
    [Tooltip("Objectes disponibles per comprar a la botiga.")]
    public List<ItemProfile> shopItems = new List<ItemProfile>();

    [Header("Shopkeeper")]
    public Sprite shopkeeperSprite;
    
    public List<ShopDialogVariant> shopWelcomeMsgs = new List<ShopDialogVariant>();
    public List<ShopDialogVariant> shopBuyMsgs = new List<ShopDialogVariant>();
    public List<ShopDialogVariant> shopSellMsgs = new List<ShopDialogVariant>();
    public List<ShopDialogVariant> shopCantAffordMsgs = new List<ShopDialogVariant>();
    public List<ShopDialogVariant> shopInventoryFullMsgs = new List<ShopDialogVariant>();

    [Header("UI Audio")]
    public AudioClip navSound;
    public AudioClip selectSound;
    public AudioClip shopBuySound;
    public AudioClip shopSellSound;
    public AudioClip shopVoiceSound;

    // ── HP ──────────────────────────────────────────────────────────
    private int baseMaxHP;
    public int MaxHP 
    { 
        get 
        {
            float bonus = GetTotalHealthBonus();
            if (bonus <= 0f) return baseMaxHP;
            return baseMaxHP + Mathf.RoundToInt(baseMaxHP * (bonus / 100f));
        }
    }
    public int CurrentHP { get; private set; }

    // ── Or i objectes ────────────────────────────────────────────────
    public int Gold { get; private set; }

    private List<string> items = new List<string>();
    public IReadOnlyList<string> Items => items.AsReadOnly();

    private Dictionary<string, int> recruitedEnemies = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, int> RecruitedEnemies => recruitedEnemies;

    private Dictionary<string, int> killedEnemies = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, int> KilledEnemies => killedEnemies;

    private List<string> encounteredEnemies = new List<string>();
    public IReadOnlyList<string> EncounteredEnemies => encounteredEnemies.AsReadOnly();

    // Recompenses de reclutament ja reclamades (per no aplicar-les dues vegades)
    private HashSet<string> claimedRecruitRewards = new HashSet<string>();

    // Diàlegs ja vistos (opcions de resposta)
    private HashSet<string> seenChoices = new HashSet<string>();

    public void MarkChoiceSeen(string choiceText)
    {
        if (string.IsNullOrEmpty(choiceText)) return;
        seenChoices.Add(choiceText.Trim());
    }

    public bool IsChoiceSeen(string choiceText)
    {
        if (string.IsNullOrEmpty(choiceText)) return false;
        return seenChoices.Contains(choiceText.Trim());
    }

    private float escapeHoldTime = 0f;
    private const float EscapeHoldRequired = 0.5f;
    private PlayerController2D cachedPlayer;
    private DialogueUI cachedDialogueUI;

    // ────────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        baseMaxHP = startingMaxHP;
        CurrentHP = startingMaxHP;   // Primera vegada: vida plena

        // Crea el panell de controls
        if (gameObject.GetComponent<ControlsUI>() == null)
            gameObject.AddComponent<ControlsUI>();
    }

    // ── HP ──────────────────────────────────────────────────────────
    public void SetHP(int hp)
    {
        CurrentHP = Mathf.Clamp(hp, 0, MaxHP);
    }

    public void SetMaxHP(int max)
    {
        baseMaxHP = Mathf.Max(1, max);
        CurrentHP = Mathf.Min(CurrentHP, MaxHP);
    }

    // ── Or ──────────────────────────────────────────────────────────
    public void AddGold(int amount)
    {
        Gold += Mathf.Max(0, amount);
    }

    public bool SpendGold(int amount)
    {
        if (Gold < amount) return false;
        Gold -= amount;
        return true;
    }

    // ── Objectes ────────────────────────────────────────────────────
    public ItemProfile GetItemProfile(string itemName)
    {
        if (itemDatabase == null) return null;
        return itemDatabase.Find(x => x.itemName == itemName || x.name == itemName);
    }
    
    public int CountItem(string itemName)
    {
        int count = 0;
        foreach (var i in items) if (i == itemName) count++;
        return count;
    }

    public void AddItem(string itemName)
    {
        if (string.IsNullOrEmpty(itemName)) return;
        
        if (items.Count >= maxItemsCapacity)
        {
            Debug.Log($"Inventari ple! L'objecte '{itemName}' s'ha perdut.");
            return;
        }

        ItemProfile profile = GetItemProfile(itemName);
        if (profile == null)
        {
            Debug.LogWarning($"Afegeixo objecte '{itemName}' però no hi ha ItemProfile asiganat al PlayerInventory!");
        }

        items.Add(itemName);
    }

    public bool RemoveItem(string itemName)
    {
        return items.Remove(itemName);
    }

    // ── Enemics reclutats ───────────────────────────────────────────
    public void RecruitEnemy(string enemyName)
    {
        if (string.IsNullOrEmpty(enemyName)) return;
        
        if (recruitedEnemies.ContainsKey(enemyName))
        {
            recruitedEnemies[enemyName]++;
        }
        else
        {
            recruitedEnemies[enemyName] = 1;
        }
        Debug.Log($"Enemic reclutat: {enemyName}. Total: {recruitedEnemies[enemyName]}");
    }

    public int GetRecruitedCount(string enemyName)
    {
        if (string.IsNullOrEmpty(enemyName) || !recruitedEnemies.ContainsKey(enemyName)) return 0;
        return recruitedEnemies[enemyName];
    }
    
    public EnemyProfile GetEnemyProfile(string enemyName)
    {
        if (enemyDatabase == null) return null;
        return enemyDatabase.Find(x => x.enemyName == enemyName || x.name == enemyName);
    }
    
    // ── Enemics matats i límits ─────────────────────────────────────
    public void EncounterEnemy(string enemyName)
    {
        if (string.IsNullOrEmpty(enemyName)) return;
        if (!encounteredEnemies.Contains(enemyName)) encounteredEnemies.Add(enemyName);
    }

    public bool HasEncounteredEnemy(string enemyName)
    {
        if (string.IsNullOrEmpty(enemyName)) return false;
        return encounteredEnemies.Contains(enemyName);
    }

    public void KillEnemy(string enemyName)
    {
        if (string.IsNullOrEmpty(enemyName)) return;
        if (killedEnemies.ContainsKey(enemyName)) killedEnemies[enemyName]++;
        else killedEnemies[enemyName] = 1;
        Debug.Log($"Enemic matat: {enemyName}. Total morts: {killedEnemies[enemyName]}");
    }

    public int GetKilledCount(string enemyName)
    {
        if (string.IsNullOrEmpty(enemyName) || !killedEnemies.ContainsKey(enemyName)) return 0;
        return killedEnemies[enemyName];
    }

    public int GetAvailableRecruitLimit(EnemyProfile enemy)
    {
        if (enemy == null) return 0;
        return Mathf.Max(0, enemy.maxRecruitLimit - GetKilledCount(enemy.enemyName));
    }

    /// <summary>
    /// Comprova si s'acaba de completar la barra de reclutament per a un enemic.
    /// Retorna l'EnemyProfile si s'ha completat just ara, null si no.
    /// </summary>
    public EnemyProfile CheckRecruitmentJustCompleted(string enemyName)
    {
        if (string.IsNullOrEmpty(enemyName)) return null;

        // Si ja hem reclamat la recompensa, no hem d'activar el panell de "Completat!" de nou.
        if (HasClaimedRecruitReward(enemyName)) return null;
        
        foreach (var ep in enemyDatabase)
        {
            if (ep == null) continue;
            if (ep.enemyName == enemyName)
            {
                int limit = GetAvailableRecruitLimit(ep);
                int recruited = GetRecruitedCount(enemyName);
                if (limit > 0 && recruited >= limit)
                    return ep;
            }
        }
        return null;
    }

    // ── Recruitment Bonuses ──────────────────────────────────────────────

    /// <summary>Marca la recompensa de reclutament com a reclamada.</summary>
    public void ClaimRecruitReward(string enemyName)
    {
        if (!string.IsNullOrEmpty(enemyName)) claimedRecruitRewards.Add(enemyName);
    }

    public bool HasClaimedRecruitReward(string enemyName)
    {
        return claimedRecruitRewards.Contains(enemyName);
    }

    /// <summary>Retorna el bonus total d'atac (%) de totes les barres completades.</summary>
    public float GetTotalAttackBonus()
    {
        float total = 0f;
        foreach (var ep in enemyDatabase)
        {
            if (ep == null) continue;
            if (claimedRecruitRewards.Contains(ep.enemyName))
                total += ep.bonusAttackPercent;
        }
        return total;
    }

    /// <summary>Retorna el bonus total de vida (%) de totes les barres completades.</summary>
    public float GetTotalHealthBonus()
    {
        float total = 0f;
        foreach (var ep in enemyDatabase)
        {
            if (ep == null) continue;
            if (claimedRecruitRewards.Contains(ep.enemyName))
                total += ep.bonusHealthPercent;
        }
        return total;
    }

    /// <summary>Retorna el bonus total de defensa (%) de totes les barres completades.</summary>
    public float GetTotalDefenseBonus()
    {
        float total = 0f;
        foreach (var ep in enemyDatabase)
        {
            if (ep == null) continue;
            if (claimedRecruitRewards.Contains(ep.enemyName))
                total += ep.bonusDefensePercent;
        }
        return total;
    }

    // ── Input: Obrir Inventari o Botiga (Fora Combat) ───────────────────────
    private void Update()
    {
        // Cache refs si s'han destruït (canvi d'escena)
        if (cachedPlayer == null) cachedPlayer = FindFirstObjectByType<PlayerController2D>();
        if (cachedDialogueUI == null) cachedDialogueUI = FindFirstObjectByType<DialogueUI>();

        // Si s'oprimeix 'I' o 'TAB' i no hi ha combat actiu (CombatManager) o no està ja obert
        if (Input.GetKeyDown(KeyCode.I) || Input.GetKeyDown(KeyCode.Tab))
        {
            // Bloquejar inventari durant diàlegs normals o IA
            bool isDialogueOpen = (cachedDialogueUI != null && cachedDialogueUI.IsOpen);
            bool isAIOpen = (AIDialogueUI.Instance != null && AIDialogueUI.Instance.IsOpen);
            if (isDialogueOpen || isAIOpen) return; // No obrir inventari durant diàlegs

            // Només obrim l'inventari si no hi ha cap menú obert
            if (!InventoryMenuUI.IsOpen && !ShopMenuUI.IsOpen)
            {
                if (!CombatLoader.IsInCombat) // Fora del combat
                {
                    Time.timeScale = 0f; // Posa en pausa el món
                    
                    if (cachedPlayer != null) cachedPlayer.LockMovement();

                    InventoryMenuUI.Show(isCombat: false, onItemSelected: (profile) =>
                    {
                        // En l'escena normal, només podem curar.
                        if (profile.effectType == ItemEffectType.HealPlayer)
                        {
                            int before = CurrentHP;
                            SetHP(CurrentHP + profile.effectValue);
                            int healed = CurrentHP - before;
                            Debug.Log($"Utilitzat objecte curatiu fora de combat. Nova vida: {CurrentHP}");
                        }
                    }, 
                    onClose: () => {
                        Time.timeScale = 1f; // Continua el temps al tancar
                        if (cachedPlayer != null && (cachedDialogueUI == null || !cachedDialogueUI.IsOpen))
                            cachedPlayer.UnlockMovement();
                    });
                }
            }
        }

        // Menu de Pausa (Mantenir ESC 0.5 segons)
        if (Input.GetKey(KeyCode.Escape))
        {
            bool isAnyMenuOpen = InventoryMenuUI.IsOpen || ShopMenuUI.IsOpen;
            
            if (!isAnyMenuOpen && !PauseMenuUI.IsOpen)
            {
                escapeHoldTime += Time.unscaledDeltaTime;
                if (escapeHoldTime >= EscapeHoldRequired)
                {
                    escapeHoldTime = 0f;
                    PauseMenuUI.Show();
                }
            }
        }
        else
        {
            escapeHoldTime = 0f;
        }

    }

    public void ShowShopMenu()
    {
        if (cachedPlayer == null) cachedPlayer = FindFirstObjectByType<PlayerController2D>();
        if (cachedPlayer != null) cachedPlayer.LockMovement();
        Time.timeScale = 0f;

        ShopMenuUI.Show(onClose: () => {
            Time.timeScale = 1f;
            if (cachedPlayer != null && (cachedDialogueUI == null || !cachedDialogueUI.IsOpen))
                cachedPlayer.UnlockMovement();
        });
    }

    public ShopDialogVariant GetRandomMsg(List<ShopDialogVariant> list)
    {
        if (list == null || list.Count == 0) return null;
        float total = 0f;
        foreach (var v in list) total += v.weight;
        if (total <= 0f) return list[0];
        
        float r = Random.Range(0f, total);
        float current = 0f;
        foreach (var v in list)
        {
            current += v.weight;
            if (r <= current) return v;
        }
        return list[list.Count - 1];
    }

    // ── Debug ───────────────────────────────────────────────────────
    public string GetSummary()
    {
        return $"HP: {CurrentHP}/{MaxHP} | Or: {Gold}G | Items: {string.Join(", ", items)}";
    }
}
