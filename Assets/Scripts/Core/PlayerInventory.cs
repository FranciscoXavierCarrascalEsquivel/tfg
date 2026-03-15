using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton persistent entre escenes. Guarda l'or, els objectes i la vida del jugador.
/// </summary>
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

    [Header("UI Audio")]
    public AudioClip navSound;
    public AudioClip selectSound;

    // ── HP ──────────────────────────────────────────────────────────
    public int MaxHP   { get; private set; }
    public int CurrentHP { get; private set; }

    // ── Or i objectes ────────────────────────────────────────────────
    public int Gold { get; private set; }

    private List<string> items = new List<string>();
    public IReadOnlyList<string> Items => items.AsReadOnly();

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

        MaxHP     = startingMaxHP;
        CurrentHP = startingMaxHP;   // Primera vegada: vida plena
    }

    // ── HP ──────────────────────────────────────────────────────────
    public void SetHP(int hp)
    {
        CurrentHP = Mathf.Clamp(hp, 0, MaxHP);
    }

    public void SetMaxHP(int max)
    {
        MaxHP = Mathf.Max(1, max);
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

    // ── Input: Obrir Inventari (Fora Combat) ───────────────────────
    private void Update()
    {
        // Si s'oprimeix 'I' i no hi ha combat actiu (CombatManager) o no està ja obert
        if (Input.GetKeyDown(KeyCode.I))
        {
            var menuObert = FindFirstObjectByType<InventoryMenuUI>();
            if (menuObert == null)
            {
                var combatManager = FindFirstObjectByType<CombatManager>();
                if (combatManager == null) // Fora del combat
                {
                    Time.timeScale = 0f; // Posa en pausa el món
                    
                    var player = FindFirstObjectByType<PlayerController2D>();
                    if (player != null) player.LockMovement();

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
                        if (player != null) player.UnlockMovement();
                    });
                }
            }
        }
    }

    // ── Debug ───────────────────────────────────────────────────────
    public string GetSummary()
    {
        return $"HP: {CurrentHP}/{MaxHP} | Or: {Gold}G | Items: {string.Join(", ", items)}";
    }
}
