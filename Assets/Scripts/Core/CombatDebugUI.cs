using UnityEngine;
using UnityEngine.SceneManagement;

public class CombatDebugUI : MonoBehaviour
{
    private PlayerController2D player;
    private CombatLoader combatLoader;

    #if UNITY_EDITOR
    private bool showDebug = false;
    #endif

    private void Start()
    {
        // Persistent debug UI across scenes
        DontDestroyOnLoad(gameObject);
    }

    #if UNITY_EDITOR
    private void Update()
    {
        // Toggle debug UI with F12
        if (Input.GetKeyDown(KeyCode.F12))
        {
            showDebug = !showDebug;
        }
    }

    private void OnGUI()
    {
        if (!showDebug) return;

        // Try to find components if they are null (e.g. after scene change)
        if (player == null) player = FindFirstObjectByType<PlayerController2D>();
        if (combatLoader == null) combatLoader = FindFirstObjectByType<CombatLoader>();

        // GUI Style
        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = 18;

        GUILayout.BeginArea(new Rect(10, 10, 300, Screen.height - 20));
        GUILayout.BeginVertical("box");
        
        GUILayout.Label("COMBAT DEBUG (F12)", GUI.skin.label);

        if (player != null && player.wildEnemies != null && player.wildEnemies.Length > 0)
        {
            GUILayout.Label("Wild Enemies in Player:", GUI.skin.label);
            foreach (var enemy in player.wildEnemies)
            {
                if (enemy == null) continue;

                if (GUILayout.Button($"Fight {enemy.enemyName}", buttonStyle, GUILayout.Height(40)))
                {
                    StartFight(enemy);
                }
            }
        }
        else
        {
            GUILayout.Label("No player or wild enemies found.", GUI.skin.label);
        }

        if (GUILayout.Button("Close Debug", buttonStyle, GUILayout.Height(40)))
        {
            showDebug = false;
        }

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
    #endif

    private void StartFight(EnemyProfile enemy)
    {
        if (combatLoader == null)
        {
            Debug.LogError("CombatDebugUI: CombatLoader not found!");
            return;
        }

        if (player != null) player.LockMovement();

        CombatEncounter enc = new CombatEncounter();
        enc.enemyProfile = enemy;
        
        combatLoader.StartCombat(enc);
        #if UNITY_EDITOR
        showDebug = false;
        #endif
    }
}
