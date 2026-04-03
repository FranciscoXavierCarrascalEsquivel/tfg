using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemy", menuName = "Combat/Enemy Profile")]
public class EnemyProfile : ScriptableObject
{
    public string enemyName = "Monster";
    public int minHP = 10;
    public int maxHP = 15;
    public Sprite enemyPortrait;
    public GameObject projectilePrefab;
    public float attackDuration = 6f;
    [Tooltip("Llista d'atacs que pot fer aquest enemic. S'escollirà un a l'atzar cada torn.")]
    public EnemyAttackPattern[] attackPatterns = new EnemyAttackPattern[] { EnemyAttackPattern.RandomDrop };
    
    [Range(0f, 1f)]
    [Tooltip("Probabilitat d'escapar amb èxit d'aquest enemic (0 = impossible, 1 = sempre).")]
    public float fleeProbability = 0.5f;
    
    [Header("Rewards")]
    public int goldRewardMin = 10;
    public int goldRewardMax = 30;
    
    [Tooltip("Llista d'objectes i la probabilitat seqüencial d'obtenir-los (ex: 250 = 2 segurs + 50% pel 3è)")]
    public DropItemProbability[] drops;

    [Header("Audio")]
    [Tooltip("So que es reprodueix quan l'enemic mor.")]
    public AudioClip deathSound;
    
    [Header("Reaccions de Dialeg (Trets un a l'atzar cada cop)")]
    [Tooltip("So de veu d'aquest enemic")]
    public AudioClip voiceSound;
    public string[] attackReactions;
    public string[] healReactions;
    public string[] fleeFailReactions;
}

[System.Serializable]
public struct DropItemProbability
{
    public string itemName;
    [Tooltip("Probabilitat base en %: 100 = 1 segur. 150 = 1 segur i 50% d'un segon actuant de forma cumulativa.")]
    public int probability;
}
