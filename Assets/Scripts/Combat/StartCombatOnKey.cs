using UnityEngine;

/// <summary>
/// Iniciador de Combats per Drecera de Prova (StartCombatOnKey).
/// Aquest component conté el script de desenvolupament i proves utilitzat durant les fases inicials del TFG
/// per a provar el trigger de fosa asíncrona de batalles a partir de la tecla 'P'.
/// 
/// NOTA DE DEPURAICÓ DEL TFG:
/// - Actualment es troba desactivat intencionadament, ja que totes les funcionalitats de trucs, cheateos,
///   i disparadors de batalles dinàmiques s'han consolidat i integrat a dins del nou panell visual `CombatDebugUI.cs` (Cheat Menu / F12).
/// </summary>
public class StartCombatOnKey : MonoBehaviour
{
    public CombatLoader loader;
    public Sprite enemyPortrait;
    public GameObject projectilePrefab;

    // Desactivat permanentment a la versió final: evitem pèrdues de rendiment en el cicle d'Update buits del motor.
    // void Update() { }
}
