using System;
using System.Collections;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using IVH.Core.Utils;
using System.Collections.Generic;
using System.Threading.Tasks;
using IVH.Core.Utils.Patterns;
using IVH.Core.Utils.StaticHelper;
// Other script avaliable: https://www.davideaversa.it/blog/elevenlabs-text-to-speech-unity-script/
// can try which ones is more flexiable later. 
namespace IVH.Core.ServiceConnector
{

    public class ElevenLabTTS : MonoBehaviour
    {
        // [Header("API Settings")]
        public ElevenLabsVoiceType voiceType = ElevenLabsVoiceType.Cassidy;

        [SerializeField]
        private string customVoiceID = "";

        public string CustomVoiceID => customVoiceID;
    
        private string apiKey = "";
        
        private string baseUrl = "https://api.elevenlabs.io/v1/text-to-speech";

        void Start()
        {
            apiKey = GeneralModelHelper.GetElevenLabAPIKey();
        }

        private string getVoiceId(ElevenLabsVoiceType voiceType)
        {
            if (voiceType == ElevenLabsVoiceType.Cassidy)
            {
                return "56AoDkrOh6qfVPDXZ7Pt";
            }
            else if (voiceType == ElevenLabsVoiceType.Callum)
            {
                return "N2lVS1w4EtoT3dr4eOWO";
            }
            else if (voiceType == ElevenLabsVoiceType.Custom)
            {
                return customVoiceID;
            }
            else
            {
                return "";
            }
        }
        public void SpeakText(string text, AudioSource audioSource, string voiceId = "56AoDkrOh6qfVPDXZ7Pt")
        {
            StartCoroutine(SendTextToSpeechRequest(text, audioSource));
        }

        public IEnumerator SendTextToSpeechRequest(string text, AudioSource audioSource, string voiceId = "56AoDkrOh6qfVPDXZ7Pt")
        {
            if (string.IsNullOrEmpty(text)) yield break; // Skip if text is empty

            // Get the voice ID if a specific voice type is set
            if (getVoiceId(voiceType) != "")
            {
                voiceId = getVoiceId(voiceType);
            }

            string url = $"{baseUrl}/{voiceId}";

            // Create the request payload
            ElevenLabsRequest requestPayload = new ElevenLabsRequest
            {
                text = text,
                model_id = "eleven_multilingual_v2",
                voice_settings = new ElevenLabsVoiceSettings
                {
                    stability = 0.75f,
                    similarity_boost = 0.75f
                }
            };

            string jsonData = JsonUtility.ToJson(requestPayload);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);

            using (UnityWebRequest request = UnityWebRequest.PostWwwForm(url, jsonData))
            {
                float startTime = Time.time;

                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerAudioClip("Eleven_Lab_TTS", AudioType.MPEG);
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("xi-api-key", apiKey);

                yield return request.SendWebRequest();

                // float ttsLatency = Time.time - startTime;
                // latencyMeasurement?.Invoke(ttsLatency);

                if (request.result == UnityWebRequest.Result.Success)
                {
                    // Get the audio clip
                    AudioClip clip = DownloadHandlerAudioClip.GetContent(request);

                    // Play the audio clip
                    if (clip != null)
                    {
                        audioSource.clip = clip;
                        audioSource.Play();

                        // Wait for the audio clip to finish playing
                        yield return new WaitForSeconds(clip.length);
                    }
                    else
                    {
                        Debug.LogWarning("Failed to load audio clip from response.");
                    }
                }
                else
                {
                    // Log errors only in development mode
                    if (Debug.isDebugBuild)
                    {
                        Debug.LogWarning($"Error in Text-to-Speech request: {request.error}");
                        Debug.LogWarning($"Response: {request.downloadHandler.text}");
                    }
                }
            }
        }

        /*    private void PlayAudioClip(byte[] audioData)
            {
                AudioClip clip = WavUtility.ToAudioClip(audioData, "TTS_Audio");
                audioSource.clip = clip;
                audioSource.Play();
            }*/
    }

    // Helper class to process WAV data
    /*public static class WavUtility
    {
        public static AudioClip ToAudioClip(byte[] wavData, string name)
        {
            // Parse WAV header
            int channels = BitConverter.ToInt16(wavData, 22);
            int sampleRate = BitConverter.ToInt32(wavData, 24);
            int dataStartIndex = 44;

            // Extract audio samples
            int sampleCount = (wavData.Length - dataStartIndex) / 2;
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                short sample = BitConverter.ToInt16(wavData, dataStartIndex + i * 2);
                samples[i] = sample / 32768f; // Normalize to range -1.0 to 1.0
            }

            // Create AudioClip
            AudioClip audioClip = AudioClip.Create(name, sampleCount, channels, sampleRate, false);
            audioClip.SetData(samples, 0);

            return audioClip;
        }
    }
    */
    // Helper classes for the payload
    [Serializable]
    public class ElevenLabsRequest
    {
        public string text;
        public string model_id;
        public ElevenLabsVoiceSettings voice_settings;
    }

    [Serializable]
    public class ElevenLabsVoiceSettings
    {
        public float stability;
        public float similarity_boost;
    }
}