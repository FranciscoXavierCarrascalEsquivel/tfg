using UnityEngine;

/// <summary>
/// Etiqueta o Marcador de la Zona de Parada (ParryZone).
/// Aquesta és una classe selectiva i buida que actua com a "tag" o marcador de tipus.
/// 
/// BONA PRÀCTICA D'ARQUITECTURA DEL TFG:
/// - En comptes d'utilitzar etiquetes textuals fràgils del motor de Unity (ex: `gameObject.CompareTag("ParryZone")`),
///   les quals són propenses a errors de tecleig en l'inspector, s'empra la cerca per tipus de component
///   (`GetComponent<ParryZone>()`).
/// - Això garanteix comprovacions de seguretat en temps de compilació, evitant bugs difícils de depurar.
/// </summary>
public class ParryZone : MonoBehaviour
{
}
