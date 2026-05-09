using UnityEditor;
using UnityEngine;

/// <summary>
/// Script d'Editor que configura automàticament Unity per permetre connexions HTTP
/// (necessari per connectar-se a Ollama que usa HTTP, no HTTPS).
/// S'executa automàticament en obrir el projecte o recompilar.
/// </summary>
[InitializeOnLoad]
public class AllowInsecureHTTP
{
    static AllowInsecureHTTP()
    {
        // Permetre connexions HTTP (Ollama usa HTTP, no HTTPS)
        if (PlayerSettings.insecureHttpOption != InsecureHttpOption.AlwaysAllowed)
        {
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
            Debug.Log("[AllowInsecureHTTP] S'ha activat 'Allow downloads over HTTP' per permetre connexions a Ollama.");
        }
    }
}
