using System;
using UnityEngine;

/// <summary>
/// Model lògic de Transició de Diàleg Social (SocialTransition).
/// Representa una reacció concreta de la criatura: si el jugador utilitza l'acció social X
/// en un estat determinat, es genera un feedback verbal bidireccional i passem al node de destinació Y.
/// </summary>
[Serializable]
public class SocialTransition
{
    [Tooltip("Nom de l'acció (ex: Parlar, Acariciar, Ballar) que activa aquesta transició.")]
    public string actionName;

    [TextArea(1, 3)]
    [Tooltip("El text o acció verbal pronunciada pel propi jugador. Es mostra en el panell de text inferior abans de la resposta del personatge.")]
    public string playerActionText;

    [TextArea(1, 3)]
    [Tooltip("La resposta verbal o murmuri del NPC en rebre aquesta acció.")]
    public string enemyReactionText;

    [Tooltip("L'identificador únic del següent node de destí a on anem. Deixa-ho buit per no canviar d'estat. Escriu 'AMIC' per resoldre la baralla de forma amistosa.")]
    public string nextNodeId;

    [Tooltip("Opcional: Si es configura, el jugador haurà de portar obligatòriament aquest objecte a la motxilla per poder triar aquesta opció al menú.")]
    public ItemProfile requiredItem;
}

/// <summary>
/// Node o Estat lògic d'amistat de la Criatura (SocialNode).
/// Defineix l'actitud i diàleg en entrar a l'estat actiu, i emmagatzema les transicions
/// de respostes configurades. Si el jugador tria una acció no enllaçada en aquest node,
/// s'emetrà una reacció genèrica de rebuig configurada a l'acte.
/// </summary>
[Serializable]
public class SocialNode
{
    [Tooltip("L'identificador únic del node (imprescindible que no n'hi hagi un altre de repetit en aquest arbre).")]
    public string nodeId;

    [TextArea(1, 3)]
    [Tooltip("El text que pronuncia el monstre en entrar exactament en aquest estat lògic.")]
    public string enemyEntryText;

    [TextArea(1, 2)]
    [Tooltip("El text descriptiu genèric de rebuig si el jugador utilitza accions no configurades en les transicions d'aquest node.")]
    public string defaultReactionText = "...";

    [Tooltip("Si és cert, en entrar en aquest node es desbloquejarà i es farà visible l'acció especial de disculpa 'Apologize'.")]
    public bool enableApology = false;

    [Tooltip("Opcional: Si el jugador ataca físicament al monstre estant en aquest node de xat, la criatura canviarà la seva actitud saltant a aquest node (ex. passar a ràbia).")]
    public string attackNextNodeId;

    [Tooltip("La llista de transicions i accions de xat habilitades des d'aquest estat lògic.")]
    public SocialTransition[] transitions;
}

/// <summary>
/// Arbre de Comportament Social per al Pacifisme en Combat (SocialBehaviorTree).
/// Aquest ScriptableObject representa un graf de camins de decisions en forma de diàlegs.
/// En comptes de dependre d'indicadors feixucs de punts de felicitat volàtils, implementa
/// un graf pur de camins tancats basats en nodes lògics que guien al jugador per diferents bifurcacions
/// narratives de diàlegs fins a desbloquejar el node d'amistat permanent 'AMIC'.
/// </summary>
[CreateAssetMenu(fileName = "NewSocialBT", menuName = "Combat/Social Behavior Tree")]
public class SocialBehaviorTree : ScriptableObject
{
    [Header("Llistat d'accions socials a mostrar al menú d'Actuar")]
    [Tooltip("Els noms de les accions d'aquesta llista han de coincidir exactament amb els actionName de les transicions dels nodes.")]
    public string[] playerActions;

    [Header("Interfície del Menú")]
    [TextArea(1, 2)]
    [Tooltip("Text de capçalera de la caixa d'eleccions.")]
    public string menuHeader = "Què fas?";

    [Header("Node d'Engegada")]
    [Tooltip("L'identificador del node per on comença la conversa social de combat.")]
    public string startNodeId;

    [Header("Resolució i Pacte d'Amistat (Node AMIC)")]
    [TextArea(2, 4)]
    [Tooltip("El text narratiu o diàleg de comiat triomfal que es renderitza quan la criatura et perdona i és reclutada.")]
    public string friendshipText;

    [Tooltip("Or de regal mínim obtingut al resoldre el combat de forma pacífica.")]
    public int friendGoldMin = 0;

    [Tooltip("Or de regal màxim obtingut al resoldre el combat de forma pacífica.")]
    public int friendGoldMax = 0;

    [Header("Nodes del Graf Lògic")]
    [Tooltip("La col·lecció completa de tots els estats de diàleg de la criatura.")]
    public SocialNode[] nodes;

    // =========================================================================
    // API CÀLCULS DE GRAFS PER AL COMBAT MANAGER
    // =========================================================================

    /// <summary>
    /// Localitza un node lògic de l'arbre a partir del seu identificador.
    /// Retorna null si no es troba o la cadena de cerca és invàlida.
    /// </summary>
    public SocialNode GetNode(string nodeId)
    {
        if (nodes == null || string.IsNullOrEmpty(nodeId)) return null;
        foreach (var n in nodes)
        {
            if (n.nodeId == nodeId) return n;
        }
        return null;
    }

    /// <summary>
    /// Troba la transició activa d'una acció realitzada pel jugador sobre un node de diàleg concret.
    /// Retorna null si el jugador realitza una acció social que no està configurada a les bifurcacions d'aquest node.
    /// </summary>
    public SocialTransition GetTransition(SocialNode node, string actionName)
    {
        if (node == null || node.transitions == null) return null;
        foreach (var t in node.transitions)
        {
            if (t.actionName == actionName) return t;
        }
        return null;
    }
}
