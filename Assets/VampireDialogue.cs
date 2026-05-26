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

// Nuova classe per decodificare la risposta audio
[System.Serializable]
public class WhisperResponse { public string text; }
// ----------------------------------------

public class VampireDialogue : MonoBehaviour
{
    // ⚠️ RIMETTI QUI LA TUA API KEY DI GROQ (inizia con gsk_)
    private string apiKey =  "Inserire qui la chiave da Whatsapp";
    // Variabili per il microfono
    private AudioClip clipRegistrato;
    private bool stoRegistrando = false;
    private string microfonoInUso;

    void Start()
    {
        // Cerca il microfono del tuo Mac (o del Visore)
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
        // Registra fino a 10 secondi, a 16000 Hz (frequenza ideale per Whisper)
        clipRegistrato = Microphone.Start(microfonoInUso, false, 10, 16000);
    }

    void FermaRegistrazioneEInvia()
    {
        stoRegistrando = false;
        Microphone.End(microfonoInUso);
        Debug.Log("⏳ Registrazione terminata. Elaborazione...");

        // Trasforma la registrazione in formato WAV
        byte[] audioWav = ConvertiInWav(clipRegistrato);
        
        // Invia l'audio al cloud per la trascrizione
        StartCoroutine(TrascriviAudioConWhisper(audioWav));
    }

    IEnumerator TrascriviAudioConWhisper(byte[] audioData)
    {
        // L'indirizzo di Groq specifico per i file audio
        string url = "https://api.groq.com/openai/v1/audio/transcriptions";

        // Costruiamo il "pacchetto" contenente il file audio
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", audioData, "audio.wav", "audio/wav");
        form.AddField("model", "whisper-large-v3"); // Il modello audio

        UnityWebRequest request = UnityWebRequest.Post(url, form);
        request.SetRequestHeader("Authorization", "Bearer " + apiKey);

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            // Decodifichiamo il testo che ci ha restituito l'IA
            WhisperResponse response = JsonUtility.FromJson<WhisperResponse>(request.downloadHandler.text);
            string testoTrascritto = response.text;
            
            Debug.Log("🗣️ Tu (Trascritto): " + testoTrascritto);

            // MAGIC MOMENT: Passiamo il testo appena trascritto al "Cervello"
            StartCoroutine(ChiediAlCloud(testoTrascritto));
        }
        else
        {
            Debug.LogError("Errore nella trascrizione vocale: " + request.error + " - " + request.downloadHandler.text);
        }
    }

    IEnumerator ChiediAlCloud(string messaggioGiocatore)
    {
        string url = "https://api.groq.com/openai/v1/chat/completions";

        string jsonPayload = @"
        {
            ""model"": ""llama-3.1-8b-instant"",
            ""messages"": [
                { ""role"": ""system"", ""content"": ""Sei un umano sospettoso chiuso in casa tua. Fuori è buio. Rispondi in modo molto breve (massimo 2 frasi) a chi bussa alla tua porta. Sei spaventato e non vuoi farlo entrare."" },
                { ""role"": ""user"", ""content"": """ + messaggioGiocatore + @""" }
            ],
            ""temperature"": 0.7
        }";

        UnityWebRequest request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", "Bearer " + apiKey);

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            AIResponse responseData = JsonUtility.FromJson<AIResponse>(request.downloadHandler.text);
            if (responseData != null && responseData.choices.Length > 0)
            {
                Debug.Log("🤖 NPC Umano: " + responseData.choices[0].message.content);
            }
        }
        else
        {
            Debug.LogError("Errore del Cervello IA: " + request.error);
        }
    }

    // --- UTILITY: CONVERTE L'AUDIOCLIP IN UN FILE WAV (Necessario per l'API) ---
    private byte[] ConvertiInWav(AudioClip clip)
    {
        MemoryStream stream = new MemoryStream();
        BinaryWriter writer = new BinaryWriter(stream);

        int sampleRate = clip.frequency;
        int channels = clip.channels;
        int samples = clip.samples;
        float[] sampleData = new float[samples * channels];
        clip.GetData(sampleData, 0);

        // Intestazione standard di un file WAV
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

        // Dati audio effettivi
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