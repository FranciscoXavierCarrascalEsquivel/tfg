using UnityEngine;

public class StartCombatOnKey : MonoBehaviour
{
    public CombatLoader loader;
    public Sprite enemyPortrait;
    public GameObject projectilePrefab;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            loader.StartCombat(new CombatEncounter
            {
                enemyPortrait = enemyPortrait,
                projectilePrefab = projectilePrefab,
                enemyAttackDuration = 6f
            });
        }
    }
}
