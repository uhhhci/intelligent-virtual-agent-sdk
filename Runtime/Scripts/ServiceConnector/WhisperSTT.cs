using System;
using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine;
using Whisper;

namespace IVH.Core.ServiceConnector
{
    public class WhisperSTT : MonoBehaviour
    {
        public WhisperManager whisperManager;
        public MicrophoneManager microphoneManager;

        public delegate void TranscriptionDelegate(string transcriptionResult);

        public TranscriptionDelegate transcriptionDelegate;

        private int recordingHZ;
        private AudioClip recording;

        void Start()
        {
            if (whisperManager == null)
            {
                Debug.LogError("WhisperManager is not assigned.");
                return;
            }

            if (microphoneManager == null)
            {
                microphoneManager = FindObjectOfType<MicrophoneManager>();
            }
        }

        public Coroutine GetSpeechToText(Action<string> final_result)
        {
            return StartCoroutine(StartRecordingCoroutine(final_result));
        }

        public IEnumerator StartRecordingCoroutine(Action<string> final_result = null)
        {
            if (microphoneManager == null || string.IsNullOrEmpty(microphoneManager.SelectedMicrophoneDevice))
            {
                Debug.LogWarning("No microphone was selected");
                yield break;
            }

            if (!whisperManager.IsLoaded)
            {
                Debug.Log("Whisper model isn't loaded! Initializing Whisper model...");
                var initModelTask = whisperManager.InitModel();
                yield return new WaitUntil(() => initModelTask.IsCompleted);

                if (!whisperManager.IsLoaded)
                {
                    Debug.LogError("Failed to load Whisper. Make sure you placed the Whisper file, that you specified, in the StreamingAssets/Whisper folder.");
                    yield break;
                }
            }

            Microphone.GetDeviceCaps(microphoneManager.SelectedMicrophoneDevice, out var minFreq, out int maxFreq);
            recordingHZ = Mathf.Clamp(16000, minFreq, maxFreq);

            recording = Microphone.Start(microphoneManager.SelectedMicrophoneDevice, true, 10, recordingHZ);

            Debug.Log("Listening SST");
            yield return new WaitForSeconds(10);

            Microphone.End(microphoneManager.SelectedMicrophoneDevice);
            Debug.Log("Stopped SST");

            var samples = new float[recording.samples * recording.channels];
            recording.GetData(samples, 0);

            var transcriptionTask = whisperManager.GetTextAsync(samples, recordingHZ, recording.channels);
            yield return new WaitUntil(() => transcriptionTask.IsCompleted);

            var result = transcriptionTask.Result;
            string filteredResult = FilterNonSpeechAnnotations(result.Result);
            final_result?.Invoke(filteredResult);
            Debug.Log("Final Local STT: " + filteredResult);
        }

        private string FilterNonSpeechAnnotations(string transcription)
        {
            // Remove non-speech annotations like [Musik]
            return Regex.Replace(transcription, @"\[[^\]]*\]", string.Empty).Trim();
        }

        public void StopSTT()
        {
            Debug.Log("Force StopSTT");
            StopRecording();
        }

        public void StopRecording(string device = null)
        {
            if (device != null)
                Microphone.End(device);
            else
                microphoneManager.StopAllRecordings();
        }
    }
}