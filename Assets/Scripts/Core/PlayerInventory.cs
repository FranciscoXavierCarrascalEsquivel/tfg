using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton d'Inventari i Estat General del Jugador (PlayerInventory).
/// Aquest script és la base de dades persistent central del jugador durant tota la partida.
/// Guarda de forma segura:
/// 1) Els atributs vitals del jugador: vida actual (CurrentHP) i màxima (MaxHP) amb càlculs dinàmics.
/// 2) El registre econòmic (Gold) i la llista d'objectes (items) continguts amb capacitat límit.
/// 3) El diari d'enemics: enemics trobats (encounteredEnemies), eliminats (killedEnemies) i reclutats (recruitedEnemies).
/// 4) El sistema de recompenses evolutives de combat: en reclutar completament una espècie d'enemic,
///    s'apliquen de forma permanent bonus percentuals acumulatius a l'Atac (%), Vida (%) i Defensa (%).
/// 5) Detecció d'inputs globals a nivell d'Update fora de combat: obrir/tancar l'inventari (Tab/I),
///    i l'anti-rebot per a obrir la pausa mantenint premut ESC durant 0.5 segons.
/// </summary>

/// <summary>
/// Representa una línia de diàleg de botiga amb un pes de probabilitat assignat per a selecció aleatòria (weighted random).
/// </summary>
[System.Serializable]
public class ShopDialogVariant
{
    [TextArea(2, 4)] public string text = "...";
    [Range(0f, 100f)] public float weight = 10f; // Pes de probabilitat d'aparició d'aquesta expressió
    [Tooltip("Sprite de retrat opcional per aquesta frase concreta.")]
    public Sprite expressionSprite;
}

public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory Instance { get; private set; } // Instància de Singleton global

    [Header("Valors Inicials")]
    [SerializeField] private int startingMaxHP = 100;
    
    [Header("Base de Dades d'Objectes (Items)")]
    [Tooltip("Límit màxim d'objectes individuals a l'inventari.")]
    public int maxItemsCapacity = 20;

    [Tooltip("Registre de perfils d'objectes registrats en el joc.")]
    public List<ItemProfile> itemDatabase = new List<ItemProfile>();

    [Header("Base de Dades d'Enemics")]
    [Tooltip("Registre de perfils d'enemics registrats en el joc.")]
    public List<EnemyProfile> enemyDatabase = new List<EnemyProfile>();

    [Header("Botiga (Items a la Venda)")]
    [Tooltip("Llista d'objectes a la venda de la botiga.")]
    public List<ItemProfile> shopItems = new List<ItemProfile>();

    [Header("Botiguer (NPC)")]
    public Sprite shopkeeperSprite; // Retrat per defecte del venedor
    
    public List<ShopDialogVariant> shopWelcomeMsgs = new List<ShopDialogVariant>();
    public List<ShopDialogVariant> shopBuyMsgs = new List<ShopDialogVariant>();
    public List<ShopDialogVariant> shopSellMsgs = new List<ShopDialogVariant>();
    public List<ShopDialogVariant> shopCantAffordMsgs = new List<ShopDialogVariant>();
    public List<ShopDialogVariant> shopInventoryFullMsgs = new List<ShopDialogVariant>();

    [Header("Àudios d'Interfície (UI)")]
    public AudioClip navSound;
    public AudioClip selectSound;
    public AudioClip shopBuySound;
    public AudioClip shopSellSound;
    public AudioClip shopVoiceSound;

    // ── GESTIÓ DINÀMICA DE VIDA (HP) ──────────────────────────────────────────
    private int baseMaxHP;
    public int MaxHP 
    { 
        get 
        {
            // Apliquem un increment percentual a la vida màxima base si hi ha bonus acumulats de reclutament
            float bonus = GetTotalHealthBonus();
            if (bonus <= 0f) return baseMaxHP;
            return baseMaxHP + Mathf.RoundToInt(baseMaxHP * (bonus / 100f));
        }
    }
    public int CurrentHP { get; private set; } // Vida actual del personatge

    // ── OR I COMPTES ────────────────────────────────────────────────
    public int Gold { get; private set; } // Or del jugador

    private List<string> items = new List<string>(); // Llista de noms d'objectes a l'inventari
    public IReadOnlyList<string> Items => items.AsReadOnly();

    // Diccionaris per emmagatzemar registres d'enemics (recruited, killed, encountered)
    private Dictionary<string, int> recruitedEnemies = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, int> RecruitedEnemies => recruitedEnemies;

    private Dictionary<string, int> killedEnemies = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, int> KilledEnemies => killedEnemies;

    private List<string> encounteredEnemies = new List<string>();
    public IReadOnlyList<string> EncounteredEnemies => encounteredEnemies.AsReadOnly();

    // Recompenses de reclutament ja atorgades (HashSet per evitar aplicacions duplicades)
    private HashSet<string> claimedRecruitRewards = new HashSet<string>();

    // Registre hash d'opcions de diàleg ja vistes (suporta l'opció hideSeenChoices)
    private HashSet<string> seenChoices = new HashSet<string>();

    /// <summary>
    /// Registra que s'ha vist una opció de diàleg.
    /// </summary>
    public void MarkChoiceSeen(string choiceText)
    {
        if (string.IsNullOrEmpty(choiceText)) return;
        seenChoices.Add(choiceText.Trim());
    }

    /// <summary>
    /// Comprova si l'usuari ja ha escollit aquesta opció anteriorment en diàleg.
    /// </summary>
    public bool IsChoiceSeen(string choiceText)
    {
        if (string.IsNullOrEmpty(choiceText)) return false;
        return seenChoices.Contains(choiceText.Trim());
    }

    // Propietats de control del menú de pausa
    private float escapeHoldTime = 0f;
    private const float EscapeHoldRequired = 0.5f; // Segons premuts requerits
    private PlayerController2D cachedPlayer;
    private DialogueUI cachedDialogueUI;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject); // Inventari es manté permanent entre zones

        baseMaxHP = startingMaxHP;
        CurrentHP = startingMaxHP;   

        // Instanciem dinàmicament el gestor de bafarades HUD de controls
        if (gameObject.GetComponent<ControlsUI>() == null)
            gameObject.AddComponent<ControlsUI>();
    }

    // =========================================================================
    // MODIFICADORS DE VIDA (HP)
    // =========================================================================

    public void SetHP(int hp)
    {
        CurrentHP = Mathf.Clamp(hp, 0, MaxHP);
    }

    public void SetMaxHP(int max)
    {
        baseMaxHP = Mathf.Max(1, max);
        CurrentHP = Mathf.Min(CurrentHP, MaxHP);
    }

    // =========================================================================
    // MODIFICADORS ECONÒMICS (GOLD)
    // =========================================================================

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

    // =========================================================================
    // CONTROL DE LLISTAT D'OBJECTES (ITEMS)
    // =========================================================================

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

    // =========================================================================
    // CONTROL DE SISTEMA DE RECLUTAMENT I DIARI D'ENEMICS
    // =========================================================================

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

    /// <summary>
    /// Calcula el límit màxim de reclutament real d'un enemic, restant les baixes de combats comeses pel jugador.
    /// Si matem un enemic d'aquesta espècie, el límit de reclutament d'amics d'aquest grup disminueix!
    /// </summary>
    public int GetAvailableRecruitLimit(EnemyProfile enemy)
    {
        if (enemy == null) return 0;
        return Mathf.Max(0, enemy.maxRecruitLimit - GetKilledCount(enemy.enemyName));
    }

    /// <summary>
    /// Comprova si s'acaba de completar al 100% el reclutament d'una determinada espècie en aquest instant de combat.
    /// </summary>
    public EnemyProfile CheckRecruitmentJustCompleted(string enemyName)
    {
        if (string.IsNullOrEmpty(enemyName)) return null;
        if (HasClaimedRecruitReward(enemyName)) return null; // Ja s'havia processat abans
        
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

    // =========================================================================
    // SISTEMA D'ACUMULACIÓ DE BONUS D'AMIC (STAT BONUSES)
    // =========================================================================

    public void ClaimRecruitReward(string enemyName)
    {
        if (!string.IsNullOrEmpty(enemyName)) claimedRecruitRewards.Add(enemyName);
    }

    public bool HasClaimedRecruitReward(string enemyName)
    {
        return claimedRecruitRewards.Contains(enemyName);
    }

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

    // =========================================================================
    // ENTRADES DE TECLAT I NAVEGACIÓ (HUD INPUT MANAGEMENT)
    // =========================================================================

    private void Update()
    {
        if (cachedPlayer == null) cachedPlayer = FindFirstObjectByType<PlayerController2D>();
        if (cachedDialogueUI == null) cachedDialogueUI = FindFirstObjectByType<DialogueUI>();

        // 1. Inputs: Obrir Inventari (Tabulador o Tecla I)
        if (Input.GetKeyDown(KeyCode.I) || Input.GetKeyDown(KeyCode.Tab))
        {
            // Bloqueig de l'inventari si s'està parlant o en mode xat intel·ligent
            bool isDialogueOpen = (cachedDialogueUI != null && cachedDialogueUI.IsOpen);
            bool isAIOpen = (AIDialogueUI.Instance != null && AIDialogueUI.Instance.IsOpen);
            if (isDialogueOpen || isAIOpen) return; 

            if (!InventoryMenuUI.IsOpen && !ShopMenuUI.IsOpen)
            {
                if (!CombatLoader.IsInCombat) 
                {
                    Time.timeScale = 0f; // Congelem el rellotge de físiques
                    
                    if (cachedPlayer != null) cachedPlayer.LockMovement();

                    InventoryMenuUI.Show(isCombat: false, onItemSelected: (profile) =>
                    {
                        // Lògica d'ús fora de combat: Només permetem curar d'acord a les regles de joc
                        if (profile.effectType == ItemEffectType.HealPlayer)
                        {
                            int before = CurrentHP;
                            SetHP(CurrentHP + profile.effectValue);
                            int healed = CurrentHP - before;
                            Debug.Log($"Utilitzat objecte curatiu fora de combat. Nova vida: {CurrentHP}");
                        }
                    }, 
                    onClose: () => {
                        Time.timeScale = 1f; // Reactivem rellotge
                        if (cachedPlayer != null && (cachedDialogueUI == null || !cachedDialogueUI.IsOpen))
                            cachedPlayer.UnlockMovement();
                    });
                }
            }
        }

        // 2. Inputs: Menú de Pausa (Mantenir premut ESC durant 0.5 segons)
        if (Input.GetKey(KeyCode.Escape))
        {
            bool isAnyMenuOpen = InventoryMenuUI.IsOpen || ShopMenuUI.IsOpen;
            
            if (!isAnyMenuOpen && !PauseMenuUI.IsOpen)
            {
                escapeHoldTime += Time.unscaledDeltaTime; // Unscaled per evitar errors
                if (escapeHoldTime >= EscapeHoldRequired)
                {
                    escapeHoldTime = 0f;
                    PauseMenuUI.Show(); // Obrim la pausa!
                }
            }
        }
        else
        {
            escapeHoldTime = 0f;
        }
    }

    /// <summary>
    /// Congela controls i obri el Canvas dinàmic de la botiga al mapa.
    /// </summary>
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

    /// <summary>
    /// Selecciona aleatòriament una línia de diàleg de botiga ponderada pel seu pes (Weighted Random Selection).
    /// </summary>
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

    public string GetSummary()
    {
        return $"HP: {CurrentHP}/{MaxHP} | Or: {Gold}G | Items: {string.Join(", ", items)}";
    }
}
