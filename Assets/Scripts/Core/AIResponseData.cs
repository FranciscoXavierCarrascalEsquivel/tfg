using System;

/// <summary>
/// Estructures de dades serialitzables per a la comunicació amb l'API FastAPI/Ollama (JSON).
/// Conté definides les classes de petició (Request) i resposta (Response) de xat generatiu
/// per a poder realitzar correctament la conversió (deserialització/serialització) dels paquets HTTP.
/// </summary>

/// <summary>
/// Representa l'estructura de resposta JSON retornada pel servidor de FastAPI.
/// </summary>
[Serializable]
public class FastAPIResponse
{
    public string response; // Resposta en format text pla processada pel model de llenguatge (LLM)
}

/// <summary>
/// Representa l'estructura de petició JSON que enviem cap al servidor de FastAPI.
/// </summary>
[Serializable]
public class FastAPIRequest
{
    public string player_id;          // Identificador únic del jugador per a mantenir la memòria de sessió a l'API
    public string npc_name;           // Nom de l'NPC amb qui s'està parlant (ravel, rata, etc.)
    public string player_message;     // El text que ha escrit el jugador a la bafarada de xat
    public string character_behavior; // El prompt de personalitat i regles de comportament de l'NPC (System Prompt)
    public string knowledge_limit;    // Lògica de restricció de coneixement d'història / lore del personatge
    public string initial_context;    // Context històric addicional de suport/situació inicial
}

// =========================================================================
// ESTRUCTURES PER A OLLAMA DIRECTE (Mantingudes per a retrocompatibilitat)
// =========================================================================

/// <summary>
/// Model de resposta directa del canal de xat d'Ollama.
/// </summary>
[Serializable]
public class OllamaChatResponse
{
    public string model;              // Nom del model de llenguatge utilitzat (ex: llama3, gemma)
    public OllamaChatMessage message; // Missatge contingut
    public bool done;                 // Indica si s'ha completat la generació de la resposta (flag de finalització)
}

/// <summary>
/// Representa un missatge de xat individual dins del format de conversa d'Ollama.
/// </summary>
[Serializable]
public class OllamaChatMessage
{
    public string role;    // El rol de l'emissor de la línia (user, assistant, system)
    public string content; // Contingut textual del missatge
}
