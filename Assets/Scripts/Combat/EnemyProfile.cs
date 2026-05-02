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
    
    [Header("Comportament Social (Actuar)")]
    [Tooltip("Behavior Tree que defineix com reacciona l'enemic a les accions socials del jugador.")]
    public SocialBehaviorTree socialBT;
    
    [Header("Recruitment")]
    [Tooltip("Límit màxim de vegades que aquest enemic pot ser reclutat o trobat un cop assolides les vegades màximes d'amistat.")]
    public int maxRecruitLimit = 1;
    [Tooltip("Sprite de recompensa que es mostra al final de la barra de reclutaments al menú.")]
    public Sprite recruitmentRewardSprite;
    [Tooltip("Descripció breu que es veu a les targetes de l'inventari, sota la barra.")]
    [TextArea(2, 3)]
    public string recruitmentRewardDescription = "";
    [Tooltip("Missatge que surt a la pantalla gran un cop es completa TOTA la barra de reclutament d'aquest enemic.")]
    [TextArea(2, 4)]
    public string recruitmentCompleteMessage = "";
    [Tooltip("So que es reprodueix quan s'obté la recompensa de reclutament.")]
    public AudioClip recruitmentRewardSound;

    [Header("Recruitment Bonuses (%)")]
    [Tooltip("Percentatge d'increment d'atac al completar la barra (ex: 20 = +20%)")]
    public float bonusAttackPercent = 0f;
    [Tooltip("Percentatge d'increment de vida màxima al completar la barra (ex: 15 = +15%)")]
    public float bonusHealthPercent = 0f;
    [Tooltip("Percentatge de reducció de dany rebut al completar la barra (ex: 10 = -10% dany rebut)")]
    public float bonusDefensePercent = 0f;
    
    [Header("Rewards")]
    public int goldRewardMin = 10;
    public int goldRewardMax = 30;
    
    [Tooltip("Llista d'objectes i la probabilitat seqüencial d'obtenir-los (ex: 250 = 2 segurs + 50% pel 3è)")]
    public DropItemProbability[] drops;

    [Header("Audio")]
    [Tooltip("So que es reprodueix quan l'enemic mor.")]
    public AudioClip deathSound;
    [Tooltip("Musica personalizada per aquest combat. Si es null, s'usara la musica per defecte.")]
    public AudioClip combatMusic;
    
    [Header("Reaccions de Dialeg (Trets un a l'atzar cada cop)")]
    [Tooltip("So de veu d'aquest enemic")]
    public AudioClip voiceSound;
    public string[] attackReactions;
    public string[] healReactions;
    public string[] fleeFailReactions;
    public string[] deathReactions;
    [Tooltip("Text narratiu que es mostra al clicar 'Raonar' si aquest enemic no té arbre de diàleg social.")]
    public string reasonFallbackDialogue;
    public AudioClip reasonFallbackSound;

    [Header("Phases (Optional)")]
    [Tooltip("Si vols que el combat tingui fases, defineix-les aqui. Si no n'hi ha, s'usara el comportament base.")]
    public EnemyPhase[] phases;
}

[System.Serializable]
public struct PhaseDialogueLine
{
    [TextArea(2, 4)]
    public string message;
    [Tooltip("Velocitat d'escriptura (1 = normal, 0.5 = r\xE0pid, 2 = lent)")]
    public float typingSpeedMultiplier;
    public bool shakeText;
}

[System.Serializable]
public struct EnemyPhase
{
    public string phaseName;
    [Range(0, 100)]
    [Tooltip("Percentatge de vida en el que s'activa aquesta fase. Exemple: 50 vol dir que s'activa quan a l'enemic li queda el 50% de la vida.")]
    public int hpThresholdPercent;
    public Sprite phaseSprite;
    [Tooltip("Atacs especifics d'aquesta fase.")]
    public EnemyAttackPattern[] phaseAttacks;
    [Tooltip("Missatges que diu l'enemic quan canvia a aquesta fase.")]
    public PhaseDialogueLine[] transitionDialogues;
    public AudioClip transitionSound;
    [Tooltip("Si es marca, el combat acabar\xE0 pac\xEDficament (reclutant l'enemic) un cop acabin els di\xE0legs d'entrada d'aquesta fase.")]
    public bool endFightFriendly;
}

[System.Serializable]
public struct DropItemProbability
{
    public string itemName;
    [Tooltip("Probabilitat base en %: 100 = 1 segur. 150 = 1 segur i 50% d'un segon actuant de forma cumulativa.")]
    public int probability;
}
