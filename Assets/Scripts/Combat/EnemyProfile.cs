using UnityEngine;

/// <summary>
/// Contenidor de dades serialitzades d'un enemic (EnemyProfile).
/// S'implementa com un ScriptableObject de Unity per permetre la creació senzilla d'instàncies
/// de monstres i enemics directament des del directori del projecte, facilitant als dissenyadors
/// del TFG ajustar balancejos, atacs i reaccions narratives sense modificar una sola línia de codi.
/// 
/// DISSENY I ESTRUCTURA DELS COMBATS DEL TFG:
/// - **Comportament emergent**: Defineix la vinculació del seu propi SocialBehaviorTree per avaluar accions pacífiques (Actuar).
/// - **Reclutament progressiu**: Controla el límit d'amistat (`maxRecruitLimit`) i els bonus passius de combat
///   que rep el jugador en completar cadascun dels aliats (bonus de dany %, vida % o defensa %).
/// - **Fases de combat adaptatives**: Suporta la programació de fases de boss (`EnemyPhase`) que canvien visualment
///   l'sprite del monstre, re-ajusten els patrons de projectils actius i mostren converses intermèdies de transformació dramàtica.
/// </summary>
[CreateAssetMenu(fileName = "NewEnemy", menuName = "Combat/Enemy Profile")]
public class EnemyProfile : ScriptableObject
{
    [Header("Atributs Generals de la Criatura")]
    public string enemyName = "Monster";
    public int minHP = 10;
    public int maxHP = 15;
    
    [Tooltip("El retrat bidimensional que es visualitza en els menús de xat o de l'Overworld.")]
    public Sprite enemyPortrait;
    
    [Tooltip("El prefab físic del projectil o l'atac que spawneja el monstre en la seva fase ofensiva.")]
    public GameObject projectilePrefab;
    
    [Tooltip("El temps màxim de durada de la fase d'esquiva de bales (en segons).")]
    public float attackDuration = 6f;
    
    [Tooltip("Llista de patrons ofensius. En cada torn d'atac, la IA triarà una d'aquestes coreografies a l'atzar.")]
    public EnemyAttackPattern[] attackPatterns = new EnemyAttackPattern[] { EnemyAttackPattern.RandomDrop };
    
    [Range(0f, 1f)]
    [Tooltip("Probabilitat matemàtica de fugir amb èxit d'aquest combat (0 = impossible, 1 = sempre).")]
    public float fleeProbability = 0.5f;
    
    [Header("Comportament Social (Actuar - Pacifisme)")]
    [Tooltip("El Behavior Tree (Arbre de Comportament) que defineix com reacciona el personatge a les peticions socials del jugador.")]
    public SocialBehaviorTree socialBT;
    
    [Header("Paràmetres de Reclutament (Amistat)")]
    [Tooltip("Nombre total de vegades que hem d'aliar-nos amb ell per assolir el seu nivell màxim d'amistat.")]
    public int maxRecruitLimit = 1;
    
    [Tooltip("L'sprite retro de recompensa passiva que rebrà el jugador en completar l'amistat d'aquest enemic.")]
    public Sprite recruitmentRewardSprite;
    
    [Tooltip("Explicació didàctica del benefici o habilitat passiva obtinguda en l'inventari.")]
    [TextArea(2, 3)]
    public string recruitmentRewardDescription = "";
    
    [Tooltip("El missatge narratiu complet que es desplegarà a la interfície gran en obtenir el darrer cor de la barra.")]
    [TextArea(2, 4)]
    public string recruitmentCompleteMessage = "";
    
    [Tooltip("El feedback sonor triomfal que s'emet al reclamar la seva amistat.")]
    public AudioClip recruitmentRewardSound;

    [Header("Bonificacions de Reclutament (%)")]
    [Tooltip("Percentatge d'increment del dany del jugador al completar la barra (ex: 20 = +20% dany).")]
    public float bonusAttackPercent = 0f;
    
    [Tooltip("Percentatge d'increment de la salut màxima del jugador al completar la barra (ex: 15 = +15% HP).")]
    public float bonusHealthPercent = 0f;
    
    [Tooltip("Percentatge de reducció de dany enemics rebut (ex: 10 = -10% de dany absorbit).")]
    public float bonusDefensePercent = 0f;
    
    [Header("Recompenses de Derrota Violenta (Matar - Kill)")]
    public int goldRewardMin = 10;
    public int goldRewardMax = 30;
    
    [Tooltip("Taula d'objectes atorgats per drop asíncron amb ràtios de probabilitat seqüencials cumulatius.")]
    public DropItemProbability[] drops;

    [Header("Recompenses de Derrota Pacífica (Reclutar - Pacifist)")]
    public int amicableGoldRewardMin = 5;
    public int amicableGoldRewardMax = 15;
    
    [Tooltip("Taula d'objectes concedits per drop en resoldre la baralla de manera pacífica.")]
    public DropItemProbability[] amicableDrops;

    [Header("Audio i Sons")]
    [Tooltip("So que s'emet en el frame exacte que la criatura és eliminada de forma violenta.")]
    public AudioClip deathSound;
    
    [Tooltip("Música de combat personalitzada. Si és nul·la, el CombatLoader engegarà el tema general del joc.")]
    public AudioClip combatMusic;
    
    [Header("Reaccions Narratives de Xat (Varietat de Diàlegs)")]
    [Tooltip("La veu o so característic utilitzat pel typewriter del personatge.")]
    public AudioClip voiceSound;
    
    public string[] attackReactions;
    public string[] healReactions;
    public string[] fleeFailReactions;
    public string[] deathReactions;
    
    [Tooltip("Text narratiu genèric de rescat si decidim usar la mecànica de Raonar i manca un arbre de diàleg complex.")]
    public string reasonFallbackDialogue;
    public AudioClip reasonFallbackSound;

    [Header("Fases de Combat Dinàmiques (Optional)")]
    [Tooltip("Fases de transformació o transicions de capità definides per percentatges de vida del monstre.")]
    public EnemyPhase[] phases;
}

/// <summary>
/// Representa una línia de diàleg de fase especial d'espectacle de boss.
/// </summary>
[System.Serializable]
public struct PhaseDialogueLine
{
    [TextArea(2, 4)]
    [Tooltip("El missatge narratiu de xat.")]
    public string message;
    
    [Tooltip("Multiplicador de velocitat del typewriter: 1 = normal, 0.5 = ràpid, 2 = lent.")]
    public float typingSpeedMultiplier;
    
    [Tooltip("Si és cert, s'aplica una sacsejada tridimensional a la caixa de text per a tons enfadats.")]
    public bool shakeText;
}

/// <summary>
/// Defineix les propietats gràfiques i mecàniques d'una fase de cap.
/// </summary>
[System.Serializable]
public struct EnemyPhase
{
    public string phaseName;
    
    [Range(0, 100)]
    [Tooltip("Llindar de percentatge de vida en el qual s'inicia la transformació de fase. Ex: 50% vida.")]
    public int hpThresholdPercent;
    
    [Tooltip("Nou sprite de combat que carregarà el personatge de la botiga.")]
    public Sprite phaseSprite;
    
    [Tooltip("Atacs i patrons de bales exclusius engegats a partir d'aquesta fase.")]
    public EnemyAttackPattern[] phaseAttacks;
    
    [Tooltip("Seqüència de diàlegs progressius pronunciats enmig de la fosa de transició.")]
    public PhaseDialogueLine[] transitionDialogues;
    
    [Tooltip("Feedback acústic o so de transformació engegats al canviar de fase.")]
    public AudioClip transitionSound;
    
    [Tooltip("Si es marca, la baralla finalitzarà a l'acte de forma pacífica just en acabar la seqüència de textos.")]
    public bool endFightFriendly;
}

/// <summary>
/// Struct que encapsula la probabilitat cumulativa d'un objecte.
/// </summary>
[System.Serializable]
public struct DropItemProbability
{
    public string itemName;
    
    [Tooltip("Probabilitat base en %: 100 = 1 unitat assegurada. 150 = 1 unitat assegurada + 50% de probabilitat de rebre una segona unitat cumulativa.")]
    public int probability;
}
