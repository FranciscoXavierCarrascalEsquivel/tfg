using UnityEngine;

/// <summary>
/// Identificadors de tipus d'efectes físics que pot desencadenar l'ús d'un objecte.
/// </summary>
public enum ItemEffectType
{
    HealPlayer,   // Recupera vida del jugador (+HP)
    DamageEnemy,  // Resta vida a la criatura enemiga
    SpeedUpHands, // Concedeix un buff de velocitat a les mans (esquiva de bales)
    KeyItem       // Objecte de missió clau (no es pot utilitzar a la voluntat)
}

/// <summary>
/// Model de dades d'un objecte consumible o de col·lecció (ItemProfile).
/// Implementat sota el patró ScriptableObject de Unity per permetre dissenyar ràpidament
/// nous objectes de botigues o tresors a l'inspector del projecte de forma completament visual.
/// </summary>
[CreateAssetMenu(fileName = "NewItem", menuName = "Combat/Item Profile")]
public class ItemProfile : ScriptableObject
{
    [Header("Atributs Generals de l'Objecte")]
    public string itemName;
    
    [TextArea(2, 4)]
    [Tooltip("Explicació didàctica dels beneficis gràfics que es mostra a la motxilla o botiga.")]
    public string itemDescription;
    
    [Tooltip("L'sprite bidimensional retro d'icona de l'objecte.")]
    public Sprite itemIcon;
    
    [Header("Comportament i Efectes de Joc")]
    public ItemEffectType effectType;
    
    [Tooltip("El valor numèric de l'efecte: vida recuperada, dany rebut per l'enemic o increment de velocitat de mans.")]
    public int effectValue; 
    
    [Tooltip("Nombre de torns complets que l'efecte del buff estarà actiu (Exclusivament dissenyat per a buffs com SpeedUpHands).")]
    public int buffDurationRounds = 3;
    
    [Header("Preus de Botiga")]
    [Tooltip("Preu or de compra del botiguer.")]
    public int buyPrice = 10;
    
    [Tooltip("Preu or obtingut al vendre'l de la motxilla.")]
    public int sellPrice = 5;
    
    [Header("Efectes Sonors (Audio)")]
    [Tooltip("So principal que s'emet quan el jugador consumeix l'objecte.")]
    public AudioClip useSound;
    
    [Tooltip("Sons secundaris adicionals a reproduir en el mateix frame d'ús de forma concurrent (ex: murmuri + espurnes).")]
    public AudioClip[] additionalUseSounds;

    [Header("Configuració d'Inventari")]
    [Tooltip("Determina si podem portar elements repetits d'aquest ítem agrupats.")]
    public bool isStackable = true;

    /// <summary>
    /// Regla del TFG: fora del combat, només es permet consumir pocions per recuperar vida.
    /// </summary>
    public bool CanUseInOverworld()
    {
        return effectType == ItemEffectType.HealPlayer;
    }

    /// <summary>
    /// Regla del TFG: en combat, es permet usar qualsevol mena d'objecte consumible excepte els objectes clau.
    /// </summary>
    public bool CanUseInCombat()
    {
        return effectType != ItemEffectType.KeyItem;
    }
}
