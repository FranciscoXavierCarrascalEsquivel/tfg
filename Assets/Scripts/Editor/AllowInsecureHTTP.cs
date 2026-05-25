using UnityEditor;
using UnityEngine;

/// <summary>
/// Utilitat d'Editor que configura automàticament els paràmetres de seguretat del motor.
/// Activa l'opció de connexions HTTP insegures (PlayerSettings.insecureHttpOption) a nivell de build,
/// requisit indispensable per a permetre que el joc es connecti amb el servei local de xat d'Ollama (que opera sota HTTP, no HTTPS).
/// Decorat amb [InitializeOnLoad] perquè s'executi automàticament tant en obrir el projecte com al recompilar.
/// </summary>
[InitializeOnLoad]
public class AllowInsecureHTTP
{
    static AllowInsecureHTTP()
    {
        // Si no està configurat prèviament, forcem l'accés complet a descàrregues/crides HTTP ordinàries
        if (PlayerSettings.insecureHttpOption != InsecureHttpOption.AlwaysAllowed)
        {
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
            Debug.Log("[AllowInsecureHTTP] S'ha activat 'Allow downloads over HTTP' per permetre connexions a Ollama.");
        }
    }
}
