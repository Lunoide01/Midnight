using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;

// --- CLASSI PER DECODIFICARE IL JSON DI OLLAMA ---
[System.Serializable]
public class OllamaResponse
{
    public Choice[] choices;
}

[System.Serializable]
public class Choice
{
    public Message message;
}

[System.Serializable]
public class Message
{
    public string role;
    public string content;
}
// --------------------------------------------------

public class VampireDialogue : MonoBehaviour
{
    void Start()
    {
        // Simuliamo la prima iterazione del tuo gioco VR
        string fraseVampiro = "Scusa se ti disturbo a quest'ora della notte... fa molto freddo fuori, posso entrare per scaldarmi un po'?";
        
        Debug.Log("Tu (Vampiro): " + fraseVampiro);
        StartCoroutine(ChiediAOllama(fraseVampiro));
    }

    IEnumerator ChiediAOllama(string messaggioGiocatore)
    {
        // L'indirizzo del server locale di Ollama (porta 11434)
        string url = "http://localhost:11434/v1/chat/completions";

        // Il payload con il modello DeepSeek che hai scelto e il System Prompt
        string jsonPayload = @"
        {
            ""model"": ""deepseek-r1:8b"",
            ""messages"": [
                { ""role"": ""system"", ""content"": ""Sei un umano sospettoso chiuso in casa tua. Fuori è buio. Rispondi in modo molto breve (massimo 2 frasi) a chi bussa alla tua porta. Sei spaventato e non vuoi farlo entrare."" },
                { ""role"": ""user"", ""content"": """ + messaggioGiocatore + @""" }
            ],
            ""temperature"": 0.7
        }";

        // Configurazione della richiesta HTTP
        UnityWebRequest request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        
        request.SetRequestHeader("Content-Type", "application/json");

        Debug.Log("DeepSeek sta pensando in locale sul tuo Mac...");

        // Invia la richiesta e attendi la risposta senza bloccare il framerate di Unity
        yield return request.SendWebRequest();

        // Controllo del risultato
        if (request.result == UnityWebRequest.Result.Success)
        {
            // Decodifica il JSON usando le classi definite in alto
            OllamaResponse responseData = JsonUtility.FromJson<OllamaResponse>(request.downloadHandler.text);
            
            if (responseData != null && responseData.choices.Length > 0)
            {
                // Estrae solo il testo della risposta dell'NPC
                string rispostaNPC = responseData.choices[0].message.content;
                Debug.Log("NPC Umano: " + rispostaNPC);
            }
            else
            {
                Debug.LogWarning("Risposta ricevuta, ma impossibile da decodificare. JSON grezzo: " + request.downloadHandler.text);
            }
        }
        else
        {
            // Se dimentichi di accendere Ollama, Unity ti avviserà qui!
            Debug.LogError("Errore di connessione: " + request.error + " - Assicurati che Ollama sia aperto e il modello sia in esecuzione.");
        }
    }
}