using System;

/// <summary>
/// Classes per deserialitzar la resposta JSON de la API FastAPI.
/// </summary>
[Serializable]
public class FastAPIResponse
{
    public string response;
}

/// <summary>
/// Classes per serialitzar la petició JSON cap a la API FastAPI.
/// </summary>
[Serializable]
public class FastAPIRequest
{
    public string player_id;
    public string npc_name;
    public string player_message;
    public string character_behavior;
    public string knowledge_limit;
    public string initial_context;
}

// Mantinc les classes d'Ollama per compatibilitat si cal, però ja no s'usen per la nova API
[Serializable]
public class OllamaChatResponse
{
    public string model;
    public OllamaChatMessage message;
    public bool done;
}

[Serializable]
public class OllamaChatMessage
{
    public string role;
    public string content;
}
