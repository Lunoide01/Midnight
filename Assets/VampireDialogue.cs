using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;

// --- CLASSI PER DECODIFICARE IL JSON ---
[System.Serializable]
public class AIResponse { public Choice[] choices; }
[System.Serializable]
public class Choice { public Message message; }
[System.Serializable]
public class Message { public string role; public string content; }

[System.Serializable]
public class WhisperResponse { public string text; }
// ----------------------------------------

[RequireComponent(typeof(AudioSource))] // Aggiunge automaticamente l'altoparlante all'oggetto
public class VampireDialogue : MonoBehaviour
{
    [Header("API Keys (⚠️ Non pubblicare su GitHub!)")]
    private string groqApiKey = "";
    private string elevenLabsApiKey = "";

    [Header("Configurazione Voce")]
    private string voiceId = "pNInz6obpgDQGcFmaJgB"; // ID di "Adam", ottima voce multilingua

    // Componenti e variabili microfono
    private AudioSource audioSource;
    private AudioClip clipRegistrato;
    private bool stoRegistrando = false;
    private string microfonoInUso;

    void Start()
    {
        // Inizializza l'altoparlante di Unity
        audioSource = GetComponent<AudioSource>();

        // Cerca il microfono del sistema
        if (Microphone.devices.Length > 0)
        {
            microfonoInUso = Microphone.devices[0];
            Debug.Log("🎙️ Microfono trovato: " + microfonoInUso);
        }
        else
        {
            Debug.LogError("⚠️ Nessun microfono rilevato dal sistema!");
        }
    }

    void Update()
    {
        // Tieni premuto SPAZIO per parlare
        if (Input.GetKeyDown(KeyCode.Space) && !stoRegistrando)
        {
            IniziaRegistrazione();
        }
        
        // Rilascia SPAZIO per inviare
        if (Input.GetKeyUp(KeyCode.Space) && stoRegistrando)
        {
            FermaRegistrazioneEInvia();
        }
    }

    void IniziaRegistrazione()
    {
        stoRegistrando = true;
        Debug.Log("🔴 Registrazione in corso... (Parla ora!)");
        clipRegistrato = Microphone.Start(microfonoInUso, false, 10, 16000);
    }

    void FermaRegistrazioneEInvia()
    {
        stoRegistrando = false;
        Microphone.End(microfonoInUso);
        Debug.Log("⏳ Registrazione terminata. Elaborazione...");

        byte[] audioWav = ConvertiInWav(clipRegistrato);
        StartCoroutine(TrascriviAudioConWhisper(audioWav));
    }

    IEnumerator TrascriviAudioConWhisper(byte[] audioData)
    {
        string url = "https://api.groq.com/openai/v1/audio/transcriptions";

        WWWForm form = new WWWForm();
        form.AddBinaryData("file", audioData, "audio.wav", "audio/wav");
        form.AddField("model", "whisper-large-v3");

        UnityWebRequest request = UnityWebRequest.Post(url, form);
        request.SetRequestHeader("Authorization", "Bearer " + groqApiKey);

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            WhisperResponse response = JsonUtility.FromJson<WhisperResponse>(request.downloadHandler.text);
            string testoTrascritto = response.text;
            
            Debug.Log("🗣️ Tu (Trascritto): " + testoTrascritto);
            StartCoroutine(ChiediAlCloud(testoTrascritto));
        }
        else
        {
            Debug.LogError("Errore trascrizione: " + request.error);
        }
    }

    IEnumerator ChiediAlCloud(string messaggioGiocatore)
    {
        string url = "https://api.groq.com/openai/v1/chat/completions";

        string jsonPayload = @"
        {
            ""model"": ""llama-3.1-8b-instant"",
            ""messages"": [
                { ""role"": ""system"", ""content"": ""Sei un umano sospettoso chiuso in casa tua. Fuori è buio. Rispondi in modo molto breve (massimo 2 frasi) a chi bussa alla tua porta. Sei spaventato e non vuoi farlo entrare. Rispondi esclusivamente in italiano."" },
                { ""role"": ""user"", ""content"": """ + messaggioGiocatore + @""" }
            ],
            ""temperature"": 0.7
        }";

        UnityWebRequest request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", "Bearer " + groqApiKey);

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            AIResponse responseData = JsonUtility.FromJson<AIResponse>(request.downloadHandler.text);
            if (responseData != null && responseData.choices.Length > 0)
            {
                string rispostaNPC = responseData.choices[0].message.content;
                Debug.Log("🤖 NPC Umano (Testo): " + rispostaNPC);

                // 🔥 Invia il testo a ElevenLabs per farlo parlare
                StartCoroutine(GeneraVoceElevenLabs(rispostaNPC));
            }
        }
        else
        {
            Debug.LogError("Errore Cervello IA: " + request.error);
        }
    }

    IEnumerator GeneraVoceElevenLabs(string testoDaPronunciare)
    {
        string url = "https://api.elevenlabs.io/v1/text-to-speech/" + voiceId;

        // Puliamo il testo da eventuali a capo o virgolette che rompono il JSON
        string testoPulito = testoDaPronunciare.Replace("\"", "\\\"").Replace("\n", " ");

        // Payload per ElevenLabs configurato per l'italiano
        string jsonPayload = @"
        {
            ""text"": """ + testoPulito + @""",
            ""model_id"": ""eleven_multilingual_v2"",
            ""voice_settings"": {
                ""stability"": 0.45,
                ""similarity_boost"": 0.75
            }
        }";

        UnityWebRequest request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        
        // Gestore speciale di Unity per scaricare file audio
        request.downloadHandler = new DownloadHandlerAudioClip(url, AudioType.MPEG);

        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("xi-api-key", elevenLabsApiKey);

        Debug.Log("⏳ ElevenLabs sta generando l'audio della risposta...");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            // Estraiamo la clip audio generata dal cloud
            AudioClip clipVoce = DownloadHandlerAudioClip.GetContent(request);
            
            if (clipVoce != null)
            {
                // Assegna la clip all'altoparlante e falla suonare
                audioSource.clip = clipVoce;
                audioSource.Play();
                Debug.Log("🔊 L'NPC sta parlando adesso!");
            }
        }
        else
        {
            Debug.LogError("Errore ElevenLabs: " + request.error);
        }
    }

    private byte[] ConvertiInWav(AudioClip clip)
    {
        MemoryStream stream = new MemoryStream();
        BinaryWriter writer = new BinaryWriter(stream);

        int sampleRate = clip.frequency;
        int channels = clip.channels;
        int samples = clip.samples;
        float[] sampleData = new float[samples * channels];
        clip.GetData(sampleData, 0);

        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + samples * channels * 2);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * 2);
        writer.Write((short)(channels * 2));
        writer.Write((short)16);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(samples * channels * 2);

        foreach (float sample in sampleData)
        {
            short intSample = (short)(Mathf.Clamp(sample, -1f, 1f) * 32767);
            writer.Write(intSample);
        }

        byte[] wavBytes = stream.ToArray();
        writer.Close();
        stream.Close();
        return wavBytes;
    }
}