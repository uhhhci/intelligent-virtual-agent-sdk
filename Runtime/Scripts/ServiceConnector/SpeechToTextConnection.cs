using System;
using System.Collections;
using UnityEngine;
using IVH.Core.Utils.Patterns;

namespace IVH.Core.ServiceConnector
{
    public class SpeechToTextConnection : MonoBehaviour
    {
        public WebsocketConnection websocketService;
        public MicrophoneManager microphoneManager;

        public delegate void TranscriptionDelegate(WebsocketConnection.TranscriptionResult transcriptionResult);

        public TranscriptionDelegate transcriptionDelegate;

        [Header("For Mobile")]
        public bool AutoselectMicrophone;

        private int recordingHZ;
        private int lastSamplePosition;
        private const float PRE_BUFFER_DURATION = 0.2f; // 200ms
        private const float GRACE_PERIOD_SECONDS = 0.5f; // 500ms

        void Start()
        {
            websocketService = ServiceConnectorManager.Instance.Websocket;

            if (microphoneManager == null)
            {
                microphoneManager = FindObjectOfType<MicrophoneManager>();
            }

            if (AutoselectMicrophone && microphoneManager != null)
            {
                microphoneManager.AutoselectMicrophone = true;
            }
        }

        public Coroutine GetSpeechToText(AgentLanguage language, Action<string> intermediate_result, Action<string> final_result)
        {
            return StartCoroutine(StartRecordingCoroutine(language, intermediate_result, final_result));
        }

        public IEnumerator StartRecordingCoroutine(AgentLanguage language, Action<string> intermediate_result = null, Action<string> final_result = null)
        {
            if (microphoneManager == null || string.IsNullOrEmpty(microphoneManager.SelectedMicrophoneDevice))
            {
                Debug.LogWarning("No microphone was selected");
                yield break;
            }

            Microphone.GetDeviceCaps(microphoneManager.SelectedMicrophoneDevice, out var minFreq, out int maxFreq);

            WebsocketConnection.AudioConfiguration audioConfig = null;


            string languageCode = LanguageHelper.GetLanguageCode(language);

            audioConfig = new WebsocketConnection.AudioConfiguration("LINEAR16", Mathf.Clamp(16000, minFreq, maxFreq), languageCode);

            recordingHZ = audioConfig.sampleRateHertz;

            var enable_audio_task = websocketService.EnableAudioWebsocket(audioConfig, (started_successfully) =>
            {
                if (!started_successfully)
                {
                    Debug.LogError("There was an error starting Speech to Text");
                }
                else
                {
                    UnityMainThreadDispatcher.Instance.Enqueue(() => { StartCoroutine(RecordingHandler(microphoneManager.recording, microphoneManager.SelectedMicrophoneDevice)); });
                }
            });

            if (enable_audio_task != null)
            {
                yield return new WaitUntil(() => enable_audio_task.IsCompleted);
            }
            bool final = false;
            if (intermediate_result != null || final_result != null)
            {
                void STT_Callbacks(WebsocketConnection.TranscriptionResult result)
                {
                    if (!result.isFinal)
                    {
                        intermediate_result(result.alternatives[0].transcript);
                    }
                    else
                    {
                        final_result(result.alternatives[0].transcript);
                        final = true;
                    }
                }

                websocketService.transcriptionListeners.Add(STT_Callbacks);

                Debug.Log("Listening SST");
                yield return new WaitUntil(() => final);
                websocketService.transcriptionListeners.Remove(STT_Callbacks);
                Debug.Log("Stopped SST");
            }
        }



        private IEnumerator RecordingHandler(AudioClip _recording, string _microphoneID)
        {
            float preBufferSamples = recordingHZ * PRE_BUFFER_DURATION;
            while (Microphone.GetPosition(_microphoneID) < preBufferSamples)
            {
                yield return null; // Wait until the pre-buffer is full
            }

            while (Microphone.IsRecording(_microphoneID))
            {
                int currentPosition = Microphone.GetPosition(_microphoneID);

                // Check if there is new data to process.
                if (currentPosition != lastSamplePosition)
                {
                    // Read the new audio samples from the clip.
                    int samplesToRead = 0;
                    if (currentPosition > lastSamplePosition)
                    {
                        samplesToRead = currentPosition - lastSamplePosition;
                    }
                    else // The position has wrapped around the buffer.
                    {
                        samplesToRead = (_recording.samples - lastSamplePosition) + currentPosition;
                    }

                    if (samplesToRead > 0)
                    {
                        float[] audioData = new float[samplesToRead];
                        _recording.GetData(audioData, lastSamplePosition);
                        // Convert the float chunk to a 16-bit PCM byte array.
                        byte[] pcm16Data = ConvertFloatTo16Bit(audioData);

                        // Send the data.
                        if (pcm16Data.Length > 0)
                        {
                            websocketService.sendAudioData(pcm16Data);
                        }
                    }

                    // Update the last position.
                    lastSamplePosition = currentPosition;
                }

                // Wait for the next frame.
                yield return null;
            }
        }


        #region utils
        /// <summary>
        /// Converts a float array of audio samples to a 16-bit PCM byte array.
        /// </summary>
        private byte[] ConvertFloatTo16Bit(float[] samples)
        {
            byte[] pcmData = new byte[samples.Length * 2]; // 16 bits = 2 bytes per sample
            int byteIndex = 0;

            for (int i = 0; i < samples.Length; i++)
            {
                // Clamp sample to [-1, 1] and convert to 16-bit integer.
                short intSample = (short)(Mathf.Clamp(samples[i], -1f, 1f) * 32767);

                // Convert short to two bytes (little-endian).
                pcmData[byteIndex++] = (byte)(intSample & 0xFF);
                pcmData[byteIndex++] = (byte)((intSample >> 8) & 0xFF);
            }
            return pcmData;
        }

        #endregion
        // old recording handler
        // private IEnumerator RecordingHandler(AudioClip _recording, string _microphoneID)
        // {
        //     if (_recording == null)
        //     {
        //         Debug.Log("Stopping");
        //         yield break;
        //     }

        //     bool bFirstBlock = true;
        //     int midPoint = _recording.samples / 2;
        //     float[] samples; // Allocate once outside the loop

        //     while (_recording != null)
        //     {
        //         int writePos = Microphone.GetPosition(_microphoneID);
        //         if (writePos > _recording.samples || !Microphone.IsRecording(_microphoneID))
        //         {
        //             Debug.Log("RecordingHandler() Microphone disconnected.");
        //             yield break;
        //         }

        //         if ((bFirstBlock && writePos >= midPoint) || (!bFirstBlock && writePos < midPoint))
        //         {
        //             samples = new float[midPoint];
        //             _recording.GetData(samples, bFirstBlock ? 0 : midPoint);

        //             var clip = AudioClip.Create("Recording", midPoint, _recording.channels, recordingHZ, false);
        //             clip.SetData(samples, 0);

        //             websocketService.sendAudioData(WebsocketConnection.GetL16(clip));

        //             bFirstBlock = !bFirstBlock;
        //         }
        //         else
        //         {
        //             int remaining = bFirstBlock ? (midPoint - writePos) : (_recording.samples - writePos);
        //             float timeRemaining = (float)remaining / (float)recordingHZ;

        //             yield return new WaitForSeconds(timeRemaining);
        //         }
        //     }

        //     yield break;
        // }

        // Don't need these functions. Especially we shouldn't call the synchronous process in an asynchrouns thread.
        // This results in frame jitter and game freeze and impact user experiences
        // public void StopSTT()
        // {
        //     Debug.Log("Force StopSTT");
        //     var stop_task = websocketService.StopSTT_Task();
        // }

        // public void StopRecording(string device = null)
        // {
        //     UnityMainThreadDispatcher.Instance.Enqueue(() =>
        //     {
        //         if (device != null)
        //             Microphone.End(device);
        //         else
        //             microphoneManager.StopAllRecordings();
        //     });
        // }
    }
}