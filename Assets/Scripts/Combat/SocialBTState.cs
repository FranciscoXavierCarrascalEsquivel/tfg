/// <summary>
/// Estat en Temps Real de l'Arbre Social de Combat (SocialBTState).
/// Aquesta classe s'encarrega d'emmagatzemar l'estat asíncron transitori del personatge durant la batalla.
/// A diferència d'arbres de decisió complexos basats en sistemes numèrics pesants o punts de karma,
/// aquest model està basat en la representació de camins lògics o salts consecutius de nodes de diàleg (pur camí de decisió).
/// 
/// DADES QUE EMMAGATZEMA:
/// - `currentNodeId`: Identificador únic del node actiu en l'arbre on es troba elpersonatge.
/// - `apologyEnabled`: Indica si la disculpa està disponible/desbloquejada en funció de les eleccions prèvies.
/// - `IsFriend`: Retorna cert en el moment exacte que el node actiu assoleix la paraula clau de tancament triomfal ("AMIC").
/// </summary>
public class SocialBTState
{
    public string currentNodeId; // Identificador del node actual en l'arbre lògic
    public bool apologyEnabled;  // Indica si s'ha habilitat l'acció de disculpa/perdó

    public SocialBTState(string startNodeId)
    {
        currentNodeId = startNodeId;
        apologyEnabled = false; // Bloquejat per defecte en iniciar el combat
    }

    /// <summary>
    /// Desplaça el personatge cap a un nou node de decisió.
    /// Si l'identificador següent és invàlid o buit, el personatge es manté en el mateix estat.
    /// </summary>
    public void MoveTo(string nextNodeId)
    {
        if (!string.IsNullOrEmpty(nextNodeId))
            currentNodeId = nextNodeId;
    }

    /// <summary>
    /// Retorna cert si s'ha assolit l'estat d'amistat i reconciliació total ("AMIC").
    /// Condició necessària per poder cridar a l'acció de "Reclutar" pacíficament.
    /// </summary>
    public bool IsFriend => currentNodeId == "AMIC";
}
