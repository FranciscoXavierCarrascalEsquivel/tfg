using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

public class FixCanvasScalers : EditorWindow
{
    [MenuItem("Tools/Fix Canvas Scalers")]
    public static void FixAllCanvasScalers()
    {
        CanvasScaler[] scalers = Resources.FindObjectsOfTypeAll<CanvasScaler>();
        int count = 0;

        foreach (CanvasScaler scaler in scalers)
        {
            // Ignorem els Canvas que pertanyen a l'Editor o a instàncies no vàlides
            if (scaler.gameObject.hideFlags == HideFlags.NotEditable || scaler.gameObject.hideFlags == HideFlags.HideAndDontSave)
                continue;

            Undo.RecordObject(scaler, "Fix Canvas Scaler");

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(2560, 1440);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            EditorUtility.SetDirty(scaler.gameObject);
            count++;
        }

        Debug.Log($"S'han arreglat {count} Canvas Scalers a l'escena actual!");
    }
}
