using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class CombatDebugUI : MonoBehaviour
{
    private PlayerController2D player;
    private CombatLoader combatLoader;

    #if UNITY_EDITOR
    private bool showDebug = false;
    private Vector2 itemsScroll = Vector2.zero;
    private Vector2 enemiesScroll = Vector2.zero;
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

        // GUI Styles
        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = 18;
        buttonStyle.richText = true;

        GUIStyle headerStyle = new GUIStyle(GUI.skin.label);
        headerStyle.fontSize = 22;
        headerStyle.fontStyle = FontStyle.Bold;
        headerStyle.alignment = TextAnchor.MiddleCenter;
        headerStyle.richText = true;
        
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 18;
        labelStyle.richText = true;

        float width = 1100f;
        float height = Screen.height - 60f;
        GUILayout.BeginArea(new Rect(20, 30, width, height));
        GUILayout.BeginVertical("box");
        
        GUILayout.Label("<b>GAME CHEATS & COMBAT DEBUG (F12)</b>", headerStyle);
        GUILayout.Space(15);

        GUILayout.BeginHorizontal();

        // ----------------- COLUMN 1: STATS & CHEATS -----------------
        GUILayout.BeginVertical("box", GUILayout.Width(350));
        
        GUILayout.Label("<b>ESTADÍSTIQUES I TRUCS</b>", headerStyle);
        GUILayout.Space(15);
        
        if (PlayerInventory.Instance != null)
        {
            GUILayout.Label($"Vida actual: <b>{PlayerInventory.Instance.CurrentHP}</b> / <b>{PlayerInventory.Instance.MaxHP}</b>", labelStyle);
            GUILayout.Label($"Or: <b>{PlayerInventory.Instance.Gold}</b> G", labelStyle);
            
            GUILayout.Space(15);
            if (GUILayout.Button("Restaurar Vida", buttonStyle, GUILayout.Height(50)))
            {
                PlayerInventory.Instance.SetHP(PlayerInventory.Instance.MaxHP);
                Debug.Log("[DEBUG] Vida restaurada al màxim.");
            }
            
            GUILayout.Space(10);
            if (GUILayout.Button("Afegir +500 Or", buttonStyle, GUILayout.Height(50)))
            {
                PlayerInventory.Instance.AddGold(500);
                Debug.Log("[DEBUG] S'han afegit 500 unitats d'or.");
            }

            GUILayout.Space(10);
            string skipButtonText = DialogueUI.ForceDisableSkipGlobals ? "<b><color=#ff4d4d>Salts Bloquejats</color></b>" : "Bloquejar salt de diàlegs";
            if (GUILayout.Button(skipButtonText, buttonStyle, GUILayout.Height(50)))
            {
                DialogueUI.ForceDisableSkipGlobals = !DialogueUI.ForceDisableSkipGlobals;
                Debug.Log($"[DEBUG] Forçar diàlegs no saltables: {DialogueUI.ForceDisableSkipGlobals}");
            }
        }
        else
        {
            GUILayout.Label("No s'ha trobat l'inventari del jugador.", labelStyle);
        }
        
        GUILayout.Space(25);
        GUILayout.Label("<b>ESTAT DEL FINAL</b>", headerStyle);
        GUILayout.Space(10);
        
        int totalKills = 0;
        int totalRecruits = 0;
        int totalMaxPopulation = 0;

        EnemyProfile[] allEnemies = Resources.LoadAll<EnemyProfile>("Enemies");
        foreach (var p in allEnemies)
        {
            if (p != null) totalMaxPopulation += p.maxRecruitLimit;
        }
        totalMaxPopulation = Mathf.Max(1, totalMaxPopulation - 1);

        if (PlayerInventory.Instance != null)
        {
            foreach (var kv in PlayerInventory.Instance.KilledEnemies) totalKills += kv.Value;
            foreach (var kv in PlayerInventory.Instance.RecruitedEnemies) totalRecruits += kv.Value;
        }

        GUILayout.Label($"Enemics Totals: <b>{totalMaxPopulation}</b>", labelStyle);
        GUILayout.Label($"Morts Totals: <b>{totalKills}</b>", labelStyle);
        GUILayout.Label($"Reclutaments Totals: <b>{totalRecruits}</b>", labelStyle);
        
        GUILayout.Space(15);
        
        string endingType = "Mixte";
        Color endingColor = Color.yellow;
        if (totalKills == 0 && totalRecruits == 0)
        {
            endingType = "Ignorant / Observador";
            endingColor = Color.cyan;
        }
        else if (totalRecruits == 0 && totalKills >= totalMaxPopulation && totalMaxPopulation > 0)
        {
            endingType = "Genocida";
            endingColor = Color.red;
        }
        else if (totalKills == 0 && totalRecruits >= totalMaxPopulation && totalMaxPopulation > 0)
        {
            endingType = "Reclutador / Pacifista";
            endingColor = Color.green;
        }
        
        GUIStyle endingStyle = new GUIStyle(labelStyle);
        endingStyle.normal.textColor = endingColor;
        endingStyle.fontStyle = FontStyle.Bold;
        GUILayout.Label($"Final Predit: {endingType.ToUpper()}", endingStyle);

        GUILayout.EndVertical();

        // ----------------- COLUMN 2: OBJECTES (SPAWNER) -----------------
        GUILayout.BeginVertical("box", GUILayout.Width(350));
        
        GUILayout.Label("<b>GENERADOR D'OBJECTES</b>", headerStyle);
        GUILayout.Space(10);
        
        List<ItemProfile> availableItems = new List<ItemProfile>();
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:ItemProfile");
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            ItemProfile item = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemProfile>(path);
            if (item != null) availableItems.Add(item);
        }
        
        if (availableItems.Count == 0 && PlayerInventory.Instance != null && PlayerInventory.Instance.itemDatabase != null)
        {
            availableItems.AddRange(PlayerInventory.Instance.itemDatabase);
        }
        
        if (availableItems.Count > 0)
        {
            itemsScroll = GUILayout.BeginScrollView(itemsScroll, GUILayout.ExpandHeight(true));
            foreach (var item in availableItems)
            {
                if (item == null) continue;
                
                if (GUILayout.Button($"Afegir: {item.itemName}", buttonStyle, GUILayout.Height(45)))
                {
                    if (PlayerInventory.Instance != null)
                    {
                        PlayerInventory.Instance.AddItem(item.itemName);
                        Debug.Log($"[DEBUG] S'ha afegit l'objecte: {item.itemName}");
                    }
                }
                GUILayout.Space(5);
            }
            GUILayout.EndScrollView();
        }
        else
        {
            GUILayout.Label("No s'han trobat objectes al projecte.", labelStyle);
        }

        GUILayout.EndVertical();

        // ----------------- COLUMN 3: WILD ENEMIES (FIGHT) -----------------
        GUILayout.BeginVertical("box", GUILayout.Width(350));
        
        GUILayout.Label("<b>ENEMICS WILD (FIGHT)</b>", headerStyle);
        GUILayout.Space(10);
        
        if (player != null && player.wildEnemies != null && player.wildEnemies.Length > 0)
        {
            enemiesScroll = GUILayout.BeginScrollView(enemiesScroll, GUILayout.ExpandHeight(true));
            foreach (var enemy in player.wildEnemies)
            {
                if (enemy == null) continue;

                if (GUILayout.Button($"Lluitar {enemy.enemyName}", buttonStyle, GUILayout.Height(50)))
                {
                    StartFight(enemy);
                }
                GUILayout.Space(5);
            }
            GUILayout.EndScrollView();
        }
        else
        {
            GUILayout.Label("No hi ha enemics a prop.", labelStyle);
        }

        GUILayout.EndVertical();

        GUILayout.EndHorizontal();

        GUILayout.Space(15);
        if (GUILayout.Button("Tancar Menú de Depuració (Close)", buttonStyle, GUILayout.Height(55)))
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
