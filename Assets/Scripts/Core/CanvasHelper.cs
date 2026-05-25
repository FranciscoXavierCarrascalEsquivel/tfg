using UnityEngine;

/// <summary>
/// Classe estàtica de suport per a la cerca i localització de Canvas a l'escena.
/// Centralitza de forma simplificada la detecció del Canvas principal del nivell de joc (UI),
/// descartant intencionadament contenidors especials de caràcter temporal o d'Overlay alt
/// (com ara "EndCanvas" del final de joc o "AlertCanvas" de l'exclamació).
/// </summary>
public static class CanvasHelper
{
    /// <summary>
    /// Cerca i retorna el Canvas principal vàlid de la jerarquia actual de l'escena de joc.
    /// </summary>
    public static Canvas GetMainCanvas()
    {
        // Busquem tots els objectes actius que tinguin el component Canvas
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        Canvas target = null;

        foreach (var c in canvases)
        {
            // Ignorem explícitament els Canvas de suport temporals de transicions o bafarades d'alerta
            if (c.name != "EndCanvas" && c.name != "AlertCanvas")
            {
                target = c;
                break;
            }
        }

        // Si tots els Canvas trobats pertanyien a les excepcions, triem per defecte el primer de la llista
        if (target == null && canvases.Length > 0) 
            target = canvases[0];

        return target;
    }
}
