using UnityEngine;

public enum ItemEffectType
{
    HealPlayer,
    DamageEnemy,
    SpeedUpHands
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
    
    [Header("Inventory")]
    [Tooltip("El límit d'espai és global a l'Inventari, no per objecte.")]
    public bool isStackable = true;
    public bool CanUseInOverworld()
    {
        // Només els objectes de curar es poden utilitzar fora del combat
        return effectType == ItemEffectType.HealPlayer;
    }
}
