using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Client HTTP encarregat de connectar el joc amb la Intel·ligència Artificial Generativa (OllamaDialogueClient).
/// Aquest component actua com a pont de xarxa per enviar les preguntes del jugador cap a l'API del servidor (FastAPI/Ollama).
/// Característiques clau:
/// 1) Dissenyat sota patró Singleton persistent (DontDestroyOnLoad) per a l'accés global.
/// 2) BuildSystemPrompt: Construeix dinàmicament el System Prompt de la IA, forçant-la a actuar com l'NPC concret
///    i injectant regles de seguretat (idioma, brevetat extrema de 2-3 frases, bloqueig de spoilers, limitació estricta
///    de coneixement basat en la fitxa del personatge per evitar al·lucinacions, i demanar ignorància en lloc d'inventar-se lore).
/// 3) SendMessageCoroutine: Corrutina altament robusta per a peticions POST JSON en línia, que utilitza polling manual
///    basat en unscaledDeltaTime contra pèrdues de flux en pausar-se el joc, i suporta claus Bearer Token.
/// 4) Gestió de memòria històrica de diàleg (conversationHistories) per a re-enviar el context de xat a cada consulta.
/// </summary>
public class OllamaDialogueClient : MonoBehaviour
{
    /// <summary>Nombre màxim de línies de conversa a emmagatzemar en memòria cau per evitar desbordar el context de la IA.</summary>
    private const int MaxHistoryMessages = 10;

    /// <summary>Temps màxim d'espera en segons per a la petició de xarxa abans de forçar un error de connexió.</summary>
    private const int RequestTimeoutSeconds = 3600;

    /// <summary>Missatge de fallback didàctic en cas que la connexió amb la IA local falli.</summary>
    public const string FallbackMessage = "Sento una interferència entre mons... Ara mateix no puc respondre.";

    // Diccionari per emmagatzemar l'historial de xat individual per a cadascun dels NPCs en actiu
    private Dictionary<int, List<OllamaChatMessage>> conversationHistories = new Dictionary<int, List<OllamaChatMessage>>();

    private static OllamaDialogueClient _instance;
    public static OllamaDialogueClient Instance
    {
        get
        {
            if (_instance == null)
            {
                var existing = FindFirstObjectByType<OllamaDialogueClient>();
                if (existing != null)
                {
                    _instance = existing;
                }
                else
                {
                    var go = new GameObject("OllamaDialogueClient");
                    _instance = go.AddComponent<OllamaDialogueClient>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Construeix una cadena exhaustiva d'instruccions i restriccions (System Prompt) que governa el model LLM.
    /// S'assegura que la IA mai surti del personatge, parli en l'idioma adequat, limiti les seves respostes a 2-3 frases
    /// i admeti ignorància si se li demana informació que no és a la base de dades.
    /// </summary>
    /// <param name="interactable">L'objecte interactuable que conté les definicions de personalitat de l'NPC.</param>
    public string BuildSystemPrompt(Interactable interactable)
    {
        string name = interactable.aiCharacterName;
        string behavior = interactable.aiCharacterBehavior;
        string knowledge = interactable.aiKnowledgeLimit;
        string context = interactable.aiInitialContext;
        string language = !string.IsNullOrEmpty(interactable.aiResponseLanguage) ? interactable.aiResponseLanguage : "English";

        var sb = new StringBuilder();

        // 1. Identitat bàsica
        sb.AppendLine($"You are {name}, a character in a 2D narrative RPG video game.");
        sb.AppendLine($"You must ALWAYS stay in character as {name}. Never break character under any circumstances.");
        sb.AppendLine();

        // 2. Personalitat i estils de comportament
        if (!string.IsNullOrEmpty(behavior))
        {
            sb.AppendLine("CHARACTER PERSONALITY AND BEHAVIOR:");
            sb.AppendLine(behavior);
            sb.AppendLine();
        }

        // 3. Contenció de coneixement històric (Limita al·lucinacions de lore)
        if (!string.IsNullOrEmpty(knowledge))
        {
            sb.AppendLine("YOUR KNOWLEDGE (you ONLY know the following):");
            sb.AppendLine(knowledge);
            sb.AppendLine("You do NOT know anything outside of what is listed above. If asked about something you don't know, say you don't know in character.");
            sb.AppendLine();
        }

        // 4. Context narratiu global de suport
        if (!string.IsNullOrEmpty(context))
        {
            sb.AppendLine("WORLD AND STORY CONTEXT:");
            sb.AppendLine(context);
            sb.AppendLine();
        }

        // 5. REGLES STRICTES DE SEGURETAT I FORMAT (Proteccions del joc)
        sb.AppendLine("STRICT RULES YOU MUST FOLLOW:");
        sb.AppendLine($"1. LANGUAGE: Always respond in {language}. Never switch languages.");
        sb.AppendLine("2. BREVITY: Keep responses short — maximum 2 or 3 sentences. Never write long paragraphs.");
        sb.AppendLine("3. ACCURACY: Only state facts that are explicitly listed in your knowledge above. Do NOT invent names, places, events, or details that are not in your knowledge.");
        sb.AppendLine("4. GRADUAL REVEAL: Do not dump all your knowledge at once. Share information little by little across multiple exchanges. Be mysterious and let the player ask follow-up questions to learn more.");
        sb.AppendLine("5. IN-WORLD ONLY: Never reference the real world, modern technology, the internet, or anything outside the game world.");
        sb.AppendLine("6. DEFLECTION: If the player asks something outside the game world or your knowledge, redirect the conversation back to the game world, the mission, or dangers nearby.");
        sb.AppendLine("7. NO SPOILERS: Do not reveal major plot points, endings, or secrets unless they are explicitly within your permitted knowledge.");
        sb.AppendLine("8. STAY IN CHARACTER: Maintain your described personality and tone at all times. You are not an AI assistant — you are a living character in this world.");
        sb.AppendLine("9. NO FABRICATION: If you don't have specific information, admit ignorance in character (e.g., 'I've heard rumors, but I don't know the details...'). Never make up lore.");

        return sb.ToString();
    }

    /// <summary>
    /// Mètode d'enllaç públic. Envia la pregunta del jugador en segon pla a la corrutina de xarxa.
    /// </summary>
    public void SendMessage(Interactable interactable, string playerMessage, Action<string> onResponse)
    {
        StartCoroutine(SendMessageCoroutine(interactable, playerMessage, onResponse));
    }

    /// <summary>
    /// Corrutina mestra que fa la petició HTTP POST asíncrona, enllaça les claus del token,
    /// fa polling de recepció i gestiona les fallades.
    /// </summary>
    private IEnumerator SendMessageCoroutine(Interactable interactable, string playerMessage, Action<string> onResponse)
    {
        string resultText = null;
        bool hasError = false;

        int npcId = interactable.GetInstanceID();
        string serverUrl = interactable.aiApiUrl;
        string apiToken = interactable.aiApiToken;
        string npcName = string.IsNullOrEmpty(interactable.aiCharacterName) ? "NPC" : interactable.aiCharacterName;

        // Recuperem o instanciem l'historial d'aquest NPC concret
        if (!conversationHistories.ContainsKey(npcId))
        {
            conversationHistories[npcId] = new List<OllamaChatMessage>();
        }
        var history = conversationHistories[npcId];

        // --- CONSTRUIR EL MODEL DE PETICIÓ DE L'API FASTAPI ---
        FastAPIRequest apiRequest = new FastAPIRequest
        {
            player_id = "player_001",
            npc_name = npcName,
            player_message = playerMessage,
            character_behavior = interactable.aiCharacterBehavior,
            knowledge_limit = interactable.aiKnowledgeLimit,
            initial_context = interactable.aiInitialContext
        };

        string json = JsonUtility.ToJson(apiRequest);

        Debug.Log($"[AIChatClient] Enviant petició a: {serverUrl}");
        Debug.Log($"[AIChatClient] Token utilitzat: {(string.IsNullOrEmpty(apiToken) ? "BUIT" : apiToken.Substring(0, Mathf.Min(5, apiToken.Length)) + "...")}");
        Debug.Log($"[AIChatClient] JSON Body: {json}");

        // --- INSTANCIACIÓ SEGURA DE LA PETICIÓ WEB ---
        UnityWebRequest request = null;
        try
        {
            request = new UnityWebRequest(serverUrl, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            
            // Si tenim assignada una clau d'accés (API Key), l'enviem com a capçalera d'autorització Bearer
            if (!string.IsNullOrEmpty(apiToken))
            {
                request.SetRequestHeader("Authorization", $"Bearer {apiToken}");
            }
            
            request.timeout = RequestTimeoutSeconds;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[OllamaDialogueClient] ERROR creant la petició: {e.Message}\n{e.StackTrace}");
            hasError = true;
        }

        if (hasError || request == null)
        {
            if (history.Count > 0) history.RemoveAt(history.Count - 1);
            onResponse?.Invoke(FallbackMessage);
            yield break;
        }

        // --- ENVIAMENT ASÍNCRON DE LA SOL·LICITUD ---
        Debug.Log("[OllamaDialogueClient] Enviant SendWebRequest...");
        UnityWebRequestAsyncOperation asyncOp = null;
        try
        {
            asyncOp = request.SendWebRequest();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[OllamaDialogueClient] ERROR enviant la petició: {e.Message}\nAixò pot ser perquè Unity bloqueja HTTP. Ves a Edit > Project Settings > Player > Other Settings > Allow downloads over HTTP > Always Allowed\n{e.StackTrace}");
            try { request.Dispose(); } catch { }
            if (history.Count > 0) history.RemoveAt(history.Count - 1);
            onResponse?.Invoke(FallbackMessage);
            yield break;
        }

        // --- POLLING MANUAL D'ESPERA ---
        // Fem un control manual en comptes de fer 'yield return asyncOp' per garantir que si la petició rep un bloqueig o l'escena
        // es pausa a la meitat, controlem activament el cicle i evitem corrutines congelades de forma silenciosa.
        float elapsed = 0f;
        while (!asyncOp.isDone)
        {
            elapsed += Time.unscaledDeltaTime;
            if (elapsed > RequestTimeoutSeconds + 5f)
            {
                Debug.LogError($"[OllamaDialogueClient] TIMEOUT MANUAL després de {elapsed:F1}s");
                try { request.Abort(); } catch { }
                try { request.Dispose(); } catch { }
                if (history.Count > 0) history.RemoveAt(history.Count - 1);
                onResponse?.Invoke(FallbackMessage);
                yield break;
            }
            yield return null; 
        }

        Debug.Log($"[OllamaDialogueClient] Petició completada en {elapsed:F1}s. Result: {request.result}");

        // --- PROCESSAMENT DE LA RESPOSTA REBUDA ---
        try
        {
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[AIChatClient] ERROR HTTP ({request.responseCode}): {request.error}");
                Debug.LogError($"[AIChatClient] URL: {serverUrl}");
                if (request.responseCode == 401) Debug.LogError("[AIChatClient] El token d'autorització sembla incorrecte (401 Unauthorized)");
                hasError = true;
            }
            else
            {
                string responseBody = request.downloadHandler.text;
                Debug.Log($"[AIChatClient] Resposta HTTP 200 OK");
                Debug.Log($"[AIChatClient] JSON rebut: {responseBody}");
                
                // Deserialitzem la resposta del camp 'response'
                resultText = ParseFastAPIResponse(responseBody);

                if (string.IsNullOrEmpty(resultText))
                {
                    Debug.LogWarning($"[AIChatClient] No s'ha pogut parsejar la resposta del camp 'response'");
                    hasError = true;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[OllamaDialogueClient] ERROR processant la resposta: {e.Message}\n{e.StackTrace}");
            hasError = true;
        }

        // Alliberem recursos de connexió de forma immediata
        try { request.Dispose(); } catch { }

        // --- DECLARACIÓ DEL CALL DE RETORN AL CREADOR (CALLBACK) ---
        if (hasError || string.IsNullOrEmpty(resultText))
        {
            if (history.Count > 0) history.RemoveAt(history.Count - 1);
            Debug.Log("[OllamaDialogueClient] Retornant missatge de fallback");
            onResponse?.Invoke(FallbackMessage);
        }
        else
        {
            // Desar la resposta correctament a l'historial
            history.Add(new OllamaChatMessage { role = "assistant", content = resultText });
            TrimHistory(history); // Conservem només el límit màxim
            Debug.Log($"[OllamaDialogueClient] Retornant resposta IA: {resultText.Substring(0, Mathf.Min(80, resultText.Length))}...");
            onResponse?.Invoke(resultText);
        }
    }

    /// <summary>
    /// Neteja l'historial de conversa d'un NPC concret.
    /// </summary>
    public void ClearHistory(int npcInstanceId)
    {
        if (conversationHistories.ContainsKey(npcInstanceId))
        {
            conversationHistories[npcInstanceId].Clear();
        }
    }

    /// <summary>
    /// Neteja de forma total tots els historials de xat memoritzats.
    /// </summary>
    public void ClearAllHistories()
    {
        conversationHistories.Clear();
    }

    private string ParseFastAPIResponse(string json)
    {
        try
        {
            var data = JsonUtility.FromJson<FastAPIResponse>(json);
            return data?.response?.Trim();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[AIChatClient] Error parsejant JSON: {e.Message}");
            return null;
        }
    }

    private string BuildRequestJson(string model, List<OllamaChatMessage> messages)
    {
        var sb = new StringBuilder();
        sb.Append("{");
        sb.Append($"\"model\":\"{EscapeJson(model)}\",");
        sb.Append("\"stream\":false,");
        sb.Append("\"messages\":[");

        for (int i = 0; i < messages.Count; i++)
        {
            if (i > 0) sb.Append(",");
            sb.Append("{");
            sb.Append($"\"role\":\"{EscapeJson(messages[i].role)}\",");
            sb.Append($"\"content\":\"{EscapeJson(messages[i].content)}\"");
            sb.Append("}");
        }

        sb.Append("]");
        sb.Append("}");

        return sb.ToString();
    }

    private string EscapeJson(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    /// <summary>
    /// Parser auxiliar manual alternatiu en cas que les respostes malformades facin fallar el deserialitzador JSON de Unity.
    /// </summary>
    private string ParseResponse(string responseBody)
    {
        try
        {
            var response = JsonUtility.FromJson<OllamaChatResponse>(responseBody);
            if (response != null && response.message != null && !string.IsNullOrEmpty(response.message.content))
            {
                Debug.Log("[OllamaDialogueClient] Parsejat via JsonUtility correctament");
                return response.message.content.Trim();
            }
            else
            {
                Debug.LogWarning($"[OllamaDialogueClient] JsonUtility ha parsejat però message és null o buit. response={response != null}, message={response?.message != null}");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[OllamaDialogueClient] JsonUtility ha fallat: {e.Message}");
        }

        // Parsing manual basat en signatures per a major flexibilitat
        try
        {
            int msgIdx = responseBody.IndexOf("\"message\"");
            if (msgIdx >= 0)
            {
                int contentIdx = responseBody.IndexOf("\"content\"", msgIdx);
                if (contentIdx >= 0)
                {
                    int colonIdx = responseBody.IndexOf(':', contentIdx + 9);
                    if (colonIdx >= 0)
                    {
                        int startQuote = responseBody.IndexOf('"', colonIdx + 1);
                        if (startQuote >= 0)
                        {
                            var sb = new StringBuilder();
                            int i = startQuote + 1;
                            while (i < responseBody.Length)
                            {
                                char c = responseBody[i];
                                if (c == '\\' && i + 1 < responseBody.Length)
                                {
                                    char next = responseBody[i + 1];
                                    if (next == '"') { sb.Append('"'); i += 2; continue; }
                                    if (next == 'n') { sb.Append('\n'); i += 2; continue; }
                                    if (next == 'r') { sb.Append('\r'); i += 2; continue; }
                                    if (next == 't') { sb.Append('\t'); i += 2; continue; }
                                    if (next == '\\') { sb.Append('\\'); i += 2; continue; }
                                    sb.Append(c); i++; continue;
                                }
                                if (c == '"') break;
                                sb.Append(c);
                                i++;
                            }

                            string result = sb.ToString().Trim();
                            if (!string.IsNullOrEmpty(result))
                            {
                                Debug.Log($"[OllamaDialogueClient] Parsejat via fallback manual ({result.Length} chars)");
                                return result;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[OllamaDialogueClient] Fallback parser ha fallat: {e.Message}");
        }

        Debug.LogWarning($"[OllamaDialogueClient] CAP parser ha funcionat. Resposta raw:\n{responseBody}");
        return null;
    }

    /// <summary>
    /// Retalla l'historial eliminant les línies més antigues per mantenir-se en els límits de context (FIFO).
    /// </summary>
    private void TrimHistory(List<OllamaChatMessage> history)
    {
        while (history.Count > MaxHistoryMessages)
        {
            history.RemoveAt(0);
        }
    }
}
