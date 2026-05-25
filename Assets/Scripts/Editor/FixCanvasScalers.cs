using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

/// <summary>
/// Eina d'Editor Window per a Unity que automatitza la resolució d'escalats de pantalles de Canvas.
/// Afegeix un accés directe al menú superior de l'Editor (Tools/Fix Canvas Scalers) que busca
/// de manera automàtica tots els components CanvasScaler de l'escena o actius del projecte,
/// i els configura amb el perfil estàndard homogeni de referència de 2560x1440 amb escalat adaptatiu.
/// Registra les operacions a la cua de desfer (Undo) i marca els canvis per a desar (EditorUtility.SetDirty).
/// </summary>
public class FixCanvasScalers : EditorWindow
{
    [MenuItem("Tools/Fix Canvas Scalers")]
    public static void FixAllCanvasScalers()
    {
        // Trobem absolutament tots els CanvasScaler carregats a nivell de jerarquia i projecte
        CanvasScaler[] scalers = Resources.FindObjectsOfTypeAll<CanvasScaler>();
        int count = 0;

        foreach (CanvasScaler scaler in scalers)
        {
            // Protecció de seguretat: Ignorem els Canvas interns de la pròpia interfície de Unity Editor o no editables
            if (scaler.gameObject.hideFlags == HideFlags.NotEditable || scaler.gameObject.hideFlags == HideFlags.HideAndDontSave)
                continue;

            // Enregistrem l'objecte per a permetre desfer l'acció amb Ctrl+Z
            Undo.RecordObject(scaler, "Fix Canvas Scaler");

            // Forcem el perfil didàctic d'escalat homogeni
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(2560, 1440); // Resolució QHD recomanada de referència
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f; // Equilibri perfecte entre amplada i alçada

            // Avisem Unity que hi ha canvis a l'escena/prefab per a forçar el desat definitiu
            EditorUtility.SetDirty(scaler.gameObject);
            count++;
        }

        Debug.Log($"S'han arreglat {count} Canvas Scalers a l'escena actual!");
    }
}
