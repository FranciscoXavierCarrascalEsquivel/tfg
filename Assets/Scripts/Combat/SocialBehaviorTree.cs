using System;
using UnityEngine;

/// <summary>
/// Una transici\u00f3: si el jugador usa l'acci\u00f3 X en aquest node, l'enemic reacciona i passem al node Y.
/// </summary>
[Serializable]
public class SocialTransition
{
    [Tooltip("Nom de l'acci\u00f3 que activa aquesta transici\u00f3.")]
    public string actionName;

    [TextArea(1, 3)]
    [Tooltip("Text narratiu del jugador que es mostra com a di\u00e0leg inferior ABANS de la reacci\u00f3 de l'enemic. " +
             "Pot ser buit si no es vol mostrar cap di\u00e0leg del jugador.")]
    public string playerActionText;

    [TextArea(1, 3)]
    [Tooltip("Text que diu l'enemic en respondre a aquesta acci\u00f3.")]
    public string enemyReactionText;

    [Tooltip("ID del node al qual anem després d'aquesta transició. " +
             "Deixa buit per quedar-se al mateix node. Escriu 'AMIC' per acabar la lluita.")]
    public string nextNodeId;

    [Tooltip("Objecte necessari de l'inventari per poder executar aquesta acci\u00f3.")]
    public ItemProfile requiredItem;
}

/// <summary>
/// Un node del graf social. L'enemic diu un text en entrar, i espera l'acci\u00f3 del jugador.
/// Les transicions defineixen que passa per cada acci\u00f3 possible.
/// Si una acci\u00f3 no t\u00e9 transici\u00f3 definida, s'usa la reacci\u00f3 per defecte del node.
/// </summary>
[Serializable]
public class SocialNode
{
    [Tooltip("Identificador \u00fanico d'aquest node (ha de ser \u00fanic dins del BT).")]
    public string nodeId;

    [TextArea(1, 3)]
    [Tooltip("Text que diu l'enemic en ENTRAR a aquest node (pot ser buit).")]
    public string enemyEntryText;

    [TextArea(1, 2)]
    [Tooltip("Reacci\u00f3 gen\u00e8rica si el jugador fa una acci\u00f3 sense transici\u00f3 definida en aquest node.")]
    public string defaultReactionText = "...";

    [Tooltip("Si \u00e9s cert, entrar a aquest node permet al jugador veure el bot\u00f3 'Demanar Disculpes' a partir d'ara.")]
    public bool enableApology = false;

    [Tooltip("ID del node al qual anem si el jugador decideix ATACAR (a trav\u00e9s del men\u00fa Atacar) mentre estem en aquest node. Deixa buit per mantenir el flux normal (sense canvi de node social).")]
    public string attackNextNodeId;

    [Tooltip("Transicions disponibles des d'aquest node.")]
    public SocialTransition[] transitions;
}

/// <summary>
/// Social Behavior Tree basat en un graf de nodes.
/// El jugador navega per camins predefinits fins a trobar el node 'AMIC'.
/// Cap punt d'humor: nom\u00e9s camins.
/// </summary>
[CreateAssetMenu(fileName = "NewSocialBT", menuName = "Combat/Social Behavior Tree")]
public class SocialBehaviorTree : ScriptableObject
{
    [Header("Accions disponibles per al jugador (apareixeran al men\u00fa d'Actuar)")]
    [Tooltip("Aquests noms d'acci\u00f3 han de coincidir EXACTAMENT amb els actionName de les transicions.")]
    public string[] playerActions;

    [Header("Text del men\u00fa")]
    [TextArea(1, 2)]
    public string menuHeader = "Qu\u00e8 fas?";

    [Header("Node inicial")]
    [Tooltip("ID del node on comen\u00e7a la conversa.")]
    public string startNodeId;

    [Header("Resolució amistosa (AMIC)")]
    [TextArea(2, 4)]
    [Tooltip("Text narratiu que es mostra com a diàleg inferior quan l'enemic es perdona i us feu amics. " +
             "Pot ser buit si no es vol mostrar cap diàleg.")]
    public string friendshipText;

    [Tooltip("Or mínim guanyat al perdonar l'enemic.")]
    public int friendGoldMin = 0;

    [Tooltip("Or màxim guanyat al perdonar l'enemic.")]
    public int friendGoldMax = 0;

    [Header("Nodes del graf")]
    public SocialNode[] nodes;

    // ── API per al CombatManager ─────────────────────────────────────────────

    /// <summary>Retorna el node amb l'ID donat, o null si no existeix.</summary>
    public SocialNode GetNode(string nodeId)
    {
        if (nodes == null || string.IsNullOrEmpty(nodeId)) return null;
        foreach (var n in nodes)
            if (n.nodeId == nodeId) return n;
        return null;
    }

    /// <summary>
    /// Donada l'acci\u00f3 del jugador i el node actual, retorna la transici\u00f3 que encaixa.
    /// Retorna null si cap transici\u00f3 de l'acci\u00f3 est\u00e0 definida al node.
    /// </summary>
    public SocialTransition GetTransition(SocialNode node, string actionName)
    {
        if (node == null || node.transitions == null) return null;
        foreach (var t in node.transitions)
            if (t.actionName == actionName) return t;
        return null;
    }
}
