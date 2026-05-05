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
        // Check if the component exists and hasn't been destroyed
        if (interactable == null || interactable.Equals(null)) return;

        bool changed = false;
        SerializedObject so = null;
        
        try {
            so = new SerializedObject(interactable);
        } catch {
            return; // Exit if the object is in an invalid state
        }
        
        if (so == null) return;

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
            bool lineAdded = false;

            // Iterem per TOTES les versions per trobar i netejar opcions repetides i fer els botons 'repeatable'
            for (int v = 0; v < versionsProp.arraySize; v++)
            {
                SerializedProperty linesProp = versionsProp.GetArrayElementAtIndex(v).FindPropertyRelative("lines");
                if (linesProp != null)
                {
                    for (int i = 0; i < linesProp.arraySize; i++)
                    {
                        SerializedProperty lineProp = linesProp.GetArrayElementAtIndex(i);
                        SerializedProperty textPropLine = lineProp.FindPropertyRelative("text");
                        
                        // Check if we already added the final line to avoid duplicating
                        if (textPropLine != null && textPropLine.stringValue.Contains("Once you have the file with the email address"))
                        {
                            lineAdded = true;
                        }

                        // Forçar que TOTS els diàlegs tinguin el forceReopen = false
                        SerializedProperty forceReopenProp = lineProp.FindPropertyRelative("forceReopen");
                        if (forceReopenProp != null && forceReopenProp.boolValue)
                        {
                            forceReopenProp.boolValue = false;
                            changed = true;
                        }

                        // Pre-calculate the correct index for "There is something above this place" inside this version
                        int correctJumpIndex = -1;
                        for (int k = 0; k < linesProp.arraySize; k++)
                        {
                            SerializedProperty p = linesProp.GetArrayElementAtIndex(k).FindPropertyRelative("text");
                            if (p != null)
                            {
                                string t = p.stringValue.ToLower();
                                if (t.Contains("something above this place") || t.Contains("hi ha alguna cosa per sobre") || t.Contains("no es una persona") || t.Contains("not a person"))
                                {
                                    correctJumpIndex = k;
                                    break;
                                }
                            }
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
                                    string txtVal = textProp.stringValue.ToLower();
                                    
                                    // 2. Remove the specific choice (repeated explanation about deleting worlds)
                                    if (txtVal.Contains("deletes worlds") || txtVal.Contains("esborrar mons") || txtVal.Contains("esborra mons"))
                                    {
                                        choicesProp.DeleteArrayElementAtIndex(j);
                                        changed = true;
                                        continue;
                                    }

                                    // Fix the 'Erased by who' choice to point to the start of the explanation dynamically
                                    if (txtVal.Contains("erased? by who") || txtVal.Contains("esborrats? per qui") || txtVal.Contains("esborrat? per qui") || txtVal.Contains("esborrats?"))
                                    {
                                        SerializedProperty jumpProp = choiceProp.FindPropertyRelative("jumpToLineIndex");
                                        if (jumpProp != null && correctJumpIndex != -1 && jumpProp.intValue != correctJumpIndex)
                                        {
                                            jumpProp.intValue = correctJumpIndex;
                                            changed = true;
                                        }
                                    }

                                    // 3. Set repeatable to true for exit options
                                    if (txtVal.Contains("process this") || txtVal.Contains("processar") ||
                                        txtVal.Contains("ready to go") || txtVal.Contains("llest per marxar") || txtVal.Contains("marxar de la conversa"))
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
                }
            }

            // 4. Add the final line to the FIRST version
            SerializedProperty linesProp0 = versionsProp.GetArrayElementAtIndex(0).FindPropertyRelative("lines");
            if (linesProp0 != null && !lineAdded)
            {
                int lastLineIdx = linesProp0.arraySize - 1;
                
                linesProp0.arraySize++;
                SerializedProperty newLineProp = linesProp0.GetArrayElementAtIndex(linesProp0.arraySize - 1);
                SerializedProperty prevLineProp = linesProp0.GetArrayElementAtIndex(lastLineIdx);
                
                newLineProp.FindPropertyRelative("text").stringValue = "Once you have the file with the email address, come back here to use the computer. We will try to use it to reach another place before we are erased by oblivion.";
                newLineProp.FindPropertyRelative("speakerName").stringValue = "Ravel";
                newLineProp.FindPropertyRelative("isRightSide").boolValue = true;
                newLineProp.FindPropertyRelative("showOnTop").boolValue = true;
                newLineProp.FindPropertyRelative("isEndNode").boolValue = true;
                newLineProp.FindPropertyRelative("setNextInteractionVersion").intValue = 2;
                
                SerializedProperty portraitProp = prevLineProp.FindPropertyRelative("portrait");
                if (portraitProp != null)
                {
                    newLineProp.FindPropertyRelative("portrait").objectReferenceValue = portraitProp.objectReferenceValue;
                }

                prevLineProp.FindPropertyRelative("isEndNode").boolValue = false;
                prevLineProp.FindPropertyRelative("setNextInteractionVersion").intValue = -1;

                changed = true;
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
