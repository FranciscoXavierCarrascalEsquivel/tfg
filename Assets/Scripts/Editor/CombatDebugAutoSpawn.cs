using UnityEngine;
using UnityEditor;

[InitializeOnLoad]
public static class CombatDebugAutoSpawn
{
    static CombatDebugAutoSpawn()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            // Create a dedicated object for debugging if it doesn't exist
            if (GameObject.Find("CombatDebugUI") == null)
            {
                GameObject debugGO = new GameObject("CombatDebugUI");
                debugGO.AddComponent<CombatDebugUI>();
                Object.DontDestroyOnLoad(debugGO);
            }
        }
    }
}
