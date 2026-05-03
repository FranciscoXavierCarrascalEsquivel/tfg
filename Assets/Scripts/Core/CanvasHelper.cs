using UnityEngine;

public static class CanvasHelper
{
    public static Canvas GetMainCanvas()
    {
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        Canvas target = null;
        foreach (var c in canvases)
        {
            if (c.name != "EndCanvas" && c.name != "AlertCanvas")
            {
                target = c;
                break;
            }
        }
        if (target == null && canvases.Length > 0) target = canvases[0];
        return target;
    }
}
