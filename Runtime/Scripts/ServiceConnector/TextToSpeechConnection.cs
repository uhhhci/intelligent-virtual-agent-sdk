using System;
using System.Collections;
using IVH.Core.ServiceConnector;
using UnityEngine;
using UnityEngine.Networking;

namespace IVH.Core.ServiceConnector
{
    public class TextToSpeechConnection : MonoBehaviour
    {
        public readonly string[] AvailableVoices = { "Alloy", "Echo", "Fable", "Onyx", "Nova", "Shimmer" };
        public string selectedVoice = "Nova";
        public int textToSpeechPort = 8150;
        private string _textToSpeechAddress;

        public void Start()
        {
            _textToSpeechAddress = "http://"+ServiceConnectorManager.Instance.serverIp + ":" + textToSpeechPort + "/agent/";
        }

        public Coroutine GetAudioWithId(string id, Action<AudioClip> OnAudioClipReceived) { 
        
            return StartCoroutine(DownloadAudioFile(id, OnAudioClipReceived));
        }

        private IEnumerator DownloadAudioFile(string id, Action<AudioClip> OnAudioClipReceived)
        {
            string url = $"{_textToSpeechAddress}audio/{id}.mp3?key={ServiceConnectorManager.Instance.localKey}";

            DownloadHandlerAudioClip downloadHandler = new DownloadHandlerAudioClip(string.Empty, AudioType.MPEG);
            downloadHandler.streamAudio = false;

            UnityWebRequest request = new UnityWebRequest(url, "GET", downloadHandler, null);

            UnityWebRequestAsyncOperation token = request.SendWebRequest();
            yield return token;

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Failed to download audio file with id {id}: {request.error}");
                yield break;
            }

            AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
            if (clip == null)
            {
                Debug.LogError("Downloaded audio clip is null.");
                yield break;
            }

            OnAudioClipReceived?.Invoke(clip);
        }
        // Get the generated audio id only. Typically only useful in multiplayer setting
        public Coroutine GetAudioFileID(string text, Action<string> OnUUIDReceived) {

            return StartCoroutine(GenerateAduioClipID(text, OnUUIDReceived));
        }
        private IEnumerator GenerateAduioClipID(string text, Action<string> OnUUIDReceived)
        {
            // End all lines of text with a period
            var t = text.Split('\n');
            for (int i = 0; i < t.Length; i++)
            {
                t[i] = t[i].Trim();
                if (t[i].Length > 0 && !t[i].EndsWith("."))
                {
                    t[i] += ".";
                }
            }

            text = string.Join("\n", t);

            // Prepare stream
            string url =
                $"{_textToSpeechAddress}tts-openai?text={UnityWebRequest.EscapeURL(text)}&key={ServiceConnectorManager.Instance.localKey}&" +
                $"voice={(selectedVoice.ToLower())}";

            UnityWebRequest request = UnityWebRequest.Get(url);
            UnityWebRequestAsyncOperation token = request.SendWebRequest();  // Explicit token
            yield return token;

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Failed to get audio UUID: " + request.error);
                yield break;
            }

            string uuid = request.GetResponseHeader("X-Audio-UUID");
            if (!string.IsNullOrEmpty(uuid))
            {
                OnUUIDReceived?.Invoke(uuid);
            }
            else
            {
                Debug.LogWarning("No UUID received in response.");
            }
        }

        /// <summary>
        /// Startet eine Coroutine die so lange läuft, bis die Sprachausgabe abgeschlossen ist
        /// </summary>
        /// <param name="text"></param>
        /// <param name="FinishedPlayingAudio_CB"></param>
        /// <returns></returns>
        public Coroutine GenerateAudioFile(string text, Action<AudioClip> FinishedPlayingAudio_CB = null, Action<string> audioClipId =null)
        {
            if (audioClipId != null)
            {
                return StartCoroutine(GenerateAudioFile_Start(text, FinishedPlayingAudio_CB, audioClipId));
            }
            else 
            { 
                return StartCoroutine(GenerateAudioFile_Start(text, FinishedPlayingAudio_CB));
            }
        }

        /// <summary>
        /// Make a request to the TTS API and play the audio
        /// </summary>
        /// <param name="text">The text to be spoken</param>
        /// <param name="FinishedGeneratingAudio_CB">The callback to be invoked when the audio has finished playing</param>
        /// <param name="aduioClipID"> The callback to be invoked when the id of the audio file is ready </param>finished</param>
        /// <returns></returns>
        private IEnumerator GenerateAudioFile_Start(string text, Action<AudioClip> FinishedGeneratingAudio_CB, Action<string> audioClipID =null)
        {
            // End all lines of text with a period
            var t = text.Split('\n');
            for (int i = 0; i < t.Length; i++)
            {
                t[i] = t[i].Trim();
                if (t[i].Length > 0 && !t[i].EndsWith("."))
                {
                    t[i] += ".";
                }
            }

            text = string.Join("\n", t);

            Debug.LogFormat("StartAudioStream: {0}", text);

            // Prepare stream
            string url =
                $"{_textToSpeechAddress}tts-openai?text={UnityWebRequest.EscapeURL(text)}&key={ServiceConnectorManager.Instance.localKey}&" +
                $"voice={(selectedVoice.ToLower())}";

            DownloadHandlerAudioClip downloadHandler = new DownloadHandlerAudioClip(string.Empty, AudioType.MPEG);
            downloadHandler.streamAudio = false;
            UnityWebRequest request = new UnityWebRequest(url, "GET", downloadHandler, null);

            // Start stream
            UnityWebRequestAsyncOperation token = request.SendWebRequest();

            AudioClip audioClip = null;
            while (audioClip == null) // Ensure audio header completed
            {
                try
                {
                    audioClip = DownloadHandlerAudioClip.GetContent(request);
                }
                catch (Exception)
                {
                }

                yield return null;
            }
            // Read UUID from header
            string uuid = request.GetResponseHeader("X-Audio-UUID");
            if (!string.IsNullOrEmpty(uuid))
            {
                //Debug.Log("Received audio UUID: " + uuid);
                audioClipID?.Invoke(uuid);
            }

            if (FinishedGeneratingAudio_CB != null)
            {
                FinishedGeneratingAudio_CB.Invoke(audioClip);
            }
        }
    }
}