using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public class FixRataDialogue
{
    static FixRataDialogue()
    {
        EditorApplication.delayCall += RunFix;
    }

    private static void RunFix()
    {
        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.name != "Zona_Test") return;

        GameObject rataObj = GameObject.Find("Rata");
        if (rataObj == null) return;

        Interactable interactable = rataObj.GetComponent<Interactable>();
        if (interactable == null) return;

        bool changed = false;
        SerializedObject so = new SerializedObject(interactable);

        // 1. Set HideSeenChoices
        SerializedProperty hideProp = so.FindProperty("hideSeenChoices");
        if (hideProp != null && !hideProp.boolValue)
        {
            hideProp.boolValue = true;
            changed = true;
        }

        SerializedProperty versionsProp = so.FindProperty("versions");
        if (versionsProp != null && versionsProp.arraySize > 0)
        {
            SerializedProperty linesProp = versionsProp.GetArrayElementAtIndex(0).FindPropertyRelative("lines");
            if (linesProp != null)
            {
                bool lineAdded = false;

                for (int i = 0; i < linesProp.arraySize; i++)
                {
                    SerializedProperty lineProp = linesProp.GetArrayElementAtIndex(i);
                    SerializedProperty textPropLine = lineProp.FindPropertyRelative("text");
                    
                    // Check if we already added the final line to avoid duplicating
                    if (textPropLine != null && textPropLine.stringValue.Contains("Once you have the file with the email address"))
                    {
                        lineAdded = true;
                    }

                    SerializedProperty choicesProp = lineProp.FindPropertyRelative("choices");
                    if (choicesProp != null && choicesProp.arraySize > 0)
                    {
                        for (int j = choicesProp.arraySize - 1; j >= 0; j--)
                        {
                            SerializedProperty choiceProp = choicesProp.GetArrayElementAtIndex(j);
                            SerializedProperty textProp = choiceProp.FindPropertyRelative("text");
                            
                            if (textProp != null)
                            {
                                // 2. Remove the specific choice
                                if (textProp.stringValue.Contains("Tell me more about what deletes worlds."))
                                {
                                    choicesProp.DeleteArrayElementAtIndex(j);
                                    changed = true;
                                    continue;
                                }

                                // 3. Set repeatable to true for exit options
                                if (textProp.stringValue.Contains("I need a minute to process this.") ||
                                    textProp.stringValue.Contains("I think I'm ready to go."))
                                {
                                    SerializedProperty repProp = choiceProp.FindPropertyRelative("repeatable");
                                    if (repProp != null && !repProp.boolValue)
                                    {
                                        repProp.boolValue = true;
                                        changed = true;
                                    }
                                }
                            }
                        }
                    }
                }

                // 4. Add the final line
                if (!lineAdded)
                {
                    // Find the last line index
                    int lastLineIdx = linesProp.arraySize - 1;
                    
                    // Add new element at the end
                    linesProp.arraySize++;
                    SerializedProperty newLineProp = linesProp.GetArrayElementAtIndex(linesProp.arraySize - 1);
                    
                    // Copy properties from the previous Ravel line to keep portrait etc.
                    // We know Ravel speaks the last line ("Now go. Before the maze...")
                    SerializedProperty prevLineProp = linesProp.GetArrayElementAtIndex(lastLineIdx);
                    
                    // Copy all fields manually or use a trick.
                    // Actually, setting them manually is safer.
                    newLineProp.FindPropertyRelative("text").stringValue = "Once you have the file with the email address, come back here to use the computer. We will try to use it to reach another place before we are erased by oblivion.";
                    newLineProp.FindPropertyRelative("speakerName").stringValue = "Ravel";
                    newLineProp.FindPropertyRelative("isRightSide").boolValue = true;
                    newLineProp.FindPropertyRelative("showOnTop").boolValue = true;
                    newLineProp.FindPropertyRelative("isEndNode").boolValue = true;
                    newLineProp.FindPropertyRelative("setNextInteractionVersion").intValue = 2; // the end behavior
                    
                    // Copy portrait
                    SerializedProperty portraitProp = prevLineProp.FindPropertyRelative("portrait");
                    if (portraitProp != null)
                    {
                        newLineProp.FindPropertyRelative("portrait").objectReferenceValue = portraitProp.objectReferenceValue;
                    }

                    // Set previous line's isEndNode to false
                    prevLineProp.FindPropertyRelative("isEndNode").boolValue = false;
                    prevLineProp.FindPropertyRelative("setNextInteractionVersion").intValue = -1;

                    changed = true;
                }
            }
        }

        if (changed)
        {
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(interactable);
            EditorSceneManager.MarkSceneDirty(activeScene);
            Debug.Log("<color=green>[FixRataDialogue]</color> Rata dialogue fixed automatically! The scene has been marked as unsaved. Please SAVE the scene now.");
        }
    }
}
