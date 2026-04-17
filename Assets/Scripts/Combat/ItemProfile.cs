using UnityEngine;

public enum ItemEffectType
{
    HealPlayer,
    DamageEnemy,
    SpeedUpHands,
    KeyItem
}

[CreateAssetMenu(fileName = "NewItem", menuName = "Combat/Item Profile")]
public class ItemProfile : ScriptableObject
{
    public string itemName;
    [TextArea(2, 4)]
    public string itemDescription;
    public Sprite itemIcon;
    
    [Header("Behavior")]
    public ItemEffectType effectType;
    public int effectValue; // Ex: Quanta vida cura, quan de mal fa, o multiplicar de velocitat (ex: 15)
    
    [Tooltip("Nombre de torns que l'efecte del buff estarà actiu (Exclusivament per a SpeedUpHands)")]
    public int buffDurationRounds = 3;
    
    [Header("Shop")]
    public int buyPrice = 10;
    public int sellPrice = 5;
    
    [Header("Audio")]
    [Tooltip("So principal que es reprodueix quan s'utilitza l'objecte (inventari o overworld).")]
    public AudioClip useSound;
    [Tooltip("Quins altres sons addicionals vols que sonin AL MATEIX TEMPS al utilitzar l'objecte? (Deixa buit si no cap)")]
    public AudioClip[] additionalUseSounds;

    [Header("Inventory")]
    [Tooltip("El límit d'espai és global a l'Inventari, no per objecte.")]
    public bool isStackable = true;
    public bool CanUseInOverworld()
    {
        // Només els objectes de curar es poden utilitzar fora del combat
        return effectType == ItemEffectType.HealPlayer;
    }

    public bool CanUseInCombat()
    {
        // L'objecte clau no es pot usar com acció
        return effectType != ItemEffectType.KeyItem;
    }
}
