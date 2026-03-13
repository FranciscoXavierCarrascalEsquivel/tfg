using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemy", menuName = "Combat/Enemy Profile")]
public class EnemyProfile : ScriptableObject
{
    public string enemyName = "Monster";
    public int maxHP = 15;
    public Sprite enemyPortrait;
    public GameObject projectilePrefab;
    public float attackDuration = 6f;
    [Tooltip("Llista d'atacs que pot fer aquest enemic. S'escollirà un a l'atzar cada torn.")]
    public EnemyAttackPattern[] attackPatterns = new EnemyAttackPattern[] { EnemyAttackPattern.RandomDrop };
    
    [Header("Rewards")]
    public int goldRewardMin = 10;
    public int goldRewardMax = 30;
    public string dropItemName = "Potion";
}
