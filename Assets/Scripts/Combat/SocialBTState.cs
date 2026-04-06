/// <summary>
/// Estat runtime del Social BT durant un combat.
/// Nom\u00e9s guarda el node actual: el sistema \u00e9s pur de camins, sense punts.
/// </summary>
public class SocialBTState
{
    public string currentNodeId;
    public bool apologyEnabled;

    public SocialBTState(string startNodeId)
    {
        currentNodeId = startNodeId;
        apologyEnabled = false;
    }

    public void MoveTo(string nextNodeId)
    {
        if (!string.IsNullOrEmpty(nextNodeId))
            currentNodeId = nextNodeId;
        // Si nextNodeId \u00e9s buit, quedem al mateix node.
    }

    public bool IsFriend => currentNodeId == "AMIC";
}
