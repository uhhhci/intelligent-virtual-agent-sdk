using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using IVH.Core.IntelligentVirtualAgent;
using IVH.Core.ServiceConnector.Gemini.Realtime;
using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace IVH.Core.IntelligentVirtualAgent
{
    public class GeminiLiveAgent : AgentBase
    {
        [Header("Gemini Configuration")]
        public string voiceName = "Puck"; 
        public bool autoConnectOnStart = true;
        
        [Header("Audio Input")]
        public string microphoneDeviceName;
        [Range(0.1f, 10f)] public float inputGain = 2.0f; 
        
        // Noise Gate: Audio below this is sent as silence (0) to prevent static from interrupting the AI
        [Range(0.001f, 0.1f)] public float noiseGateThreshold = 0.02f; 

        private GeminiRealtimeWrapper _realtimeWrapper;
        private List<float> _audioBuffer = new List<float>();
        private StringBuilder _fullTranscript = new StringBuilder();
        
        private AudioClip _playbackClip;
        private bool _isPlaying = false;
        private AudioClip _micClip;
        private int _lastMicPos;
        private bool _isRecording;
        private bool _isSessionReady = false;

        protected override void Awake()
        {
            base.Awake();
            _realtimeWrapper = GetComponent<GeminiRealtimeWrapper>();
            if (_realtimeWrapper == null) _realtimeWrapper = gameObject.AddComponent<GeminiRealtimeWrapper>();

            _realtimeWrapper.OnSetupComplete += HandleReady;
            _realtimeWrapper.OnAudioReceived += HandleAudioReceived;
            
            _realtimeWrapper.OnTextReceived += (text) => {
                _fullTranscript.Append(text);
                Debug.Log($"<color=white>Gemini:</color> {text}"); 
            };

            _realtimeWrapper.OnCommandReceived += (act, emo, gaze) => {
                Debug.Log($"<color=cyan>CMD:</color> {act}");
                if(!string.IsNullOrEmpty(act) && act != "none") PerformAction(act);
                if(!string.IsNullOrEmpty(emo) && emo != "none") ExpressEmotion(emo);
                HandleGaze(gaze);
            };
        }

        private void Start()
        {
            base.FindPlayer();
            if(autoConnectOnStart) Connect();
        }

        private void OnDestroy()
        {
            StopMicrophone();
            if (_realtimeWrapper != null) _realtimeWrapper.DisconnectAsync();
        }

        public void Connect()
        {
            _isSessionReady = false;
            string dynamicPrompt = BuildSystemPrompt();
            _ = _realtimeWrapper.ConnectAsync(dynamicPrompt, voiceName);
        }

        private void HandleReady()
        {
            Debug.Log("<color=green>Gemini Live Ready!</color>");
            _isSessionReady = true;
            StartMicrophone();
            _realtimeWrapper.SendTextMessage("System: Session started. Greet the user.");
        }

        private void StartMicrophone()
        {
            if (_isRecording) return;
            if (string.IsNullOrEmpty(microphoneDeviceName) && Microphone.devices.Length > 0) 
                microphoneDeviceName = Microphone.devices[0];
            
            // --- FIX IS HERE ---
            // Increased buffer from 20 to 3599 seconds (approx 1 hour).
            // This prevents the buffer from wrapping around during a session, 
            // which caused the "hang after 3 rounds" bug.
            _micClip = Microphone.Start(microphoneDeviceName, true, 3599, 16000);
            
            while(Microphone.GetPosition(microphoneDeviceName) <= 0) { } 
            
            _lastMicPos = 0;
            _isRecording = true;
            Debug.Log($"Mic Started: {microphoneDeviceName}");
        }

        private void StopMicrophone()
        {
            if (_isRecording) { Microphone.End(microphoneDeviceName); _isRecording = false; }
        }

        private void ProcessMicrophone()
        {
            if (!_isRecording || _micClip == null) return;
            
            int currentPos = Microphone.GetPosition(microphoneDeviceName);
            
            // Safety check for buffer wrap (unlikely now with 1h buffer, but good practice)
            if (currentPos < _lastMicPos) 
            {
                _lastMicPos = 0; // Reset logic if we somehow hit the hour mark
                return;
            }
            
            int diff = currentPos - _lastMicPos;
            
            // Send roughly every 50ms-100ms
            if (diff > 800) 
            {
                float[] samples = new float[diff];
                _micClip.GetData(samples, _lastMicPos);

                byte[] pcmData = new byte[samples.Length * 2];
                float maxVol = 0f;

                for (int i = 0; i < samples.Length; i++)
                {
                    float rawSample = samples[i];
                    if (Mathf.Abs(rawSample) > maxVol) maxVol = Mathf.Abs(rawSample);
                }

                // Noise Gate: Send silence if volume is too low to prevent interruption bugs
                bool isSilence = maxVol < noiseGateThreshold;

                for (int i = 0; i < samples.Length; i++)
                {
                    float sample = isSilence ? 0f : (samples[i] * inputGain);
                    sample = Mathf.Clamp(sample, -1f, 1f);
                    
                    short val = (short)(sample * 32767);
                    BitConverter.GetBytes(val).CopyTo(pcmData, i * 2);
                }

                // Visual Debug to confirm mic is still running
                if (maxVol > 0.05f) Debug.Log($"<color=grey>Mic Input ({maxVol:F2})</color>");

                // Interruption Logic
                if (_isPlaying && maxVol > 0.2f) 
                {
                    agentAudioSource.Stop();
                    _audioBuffer.Clear();
                    _isPlaying = false;
                }

                _realtimeWrapper.SendAudioChunk(pcmData);

                _lastMicPos = currentPos;
            }
        }

        // --- Rest of the Script (Standard) ---
        private string BuildSystemPrompt()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Your name is {agentName}. You are a conversational virtual human.");
            sb.AppendLine("RULES:");
            sb.AppendLine("1. You must call 'update_avatar_state' for EVERY response.");
            sb.AppendLine("2. Call the function FIRST, then speak.");
            sb.AppendLine("3. DO NOT speak the action names.");

            if (actionController != null)
            {
                var actions = actionController.GetSimpleActionNameFiltered(bodyActionFilter, gender, bodyAnimationControllerType);
                sb.AppendLine("Actions: " + string.Join(", ", CleanList(actions)));
            }
            if (faceAnimator != null)
            {
                var emotions = faceAnimator.GetSimpleFacialExpressionNameFiltered(facialExpressionFilter);
                sb.AppendLine("Emotions: " + string.Join(", ", CleanList(emotions)));
            }
            return sb.ToString();
        }

        private void HandleGaze(string mode)
        {
             if (string.IsNullOrEmpty(mode) || mode == "none") return;
             if (mode.Equals("User", StringComparison.OrdinalIgnoreCase)) {
                 if (player == null) FindPlayer();
                 if (eyeGazeController != null) { eyeGazeController.playerTarget = player; eyeGazeController.currentGazeMode = IVH.Core.Actions.EyeGazeController.GazeMode.LookAtPlayer; }
             } else if (eyeGazeController != null) eyeGazeController.currentGazeMode = IVH.Core.Actions.EyeGazeController.GazeMode.Idle;
        }

        private void HandleAudioReceived(byte[] pcmData)
        {
            int count = pcmData.Length / 2;
            for (int i = 0; i < count; i++) _audioBuffer.Add(BitConverter.ToInt16(pcmData, i * 2) / 32768.0f);
        }

        private void Update()
        {
            if (_isSessionReady && _realtimeWrapper.IsConnected) ProcessMicrophone();
            ProcessAudioPlayback();
        }

        public void SendCurrentView() => StartCoroutine(CaptureWebcamAndSend());
        private IEnumerator CaptureWebcamAndSend()
        {
            yield return CaptureWebcamImage(); 
            if (webCamImageData != null && _isSessionReady) _realtimeWrapper.SendImage(webCamImageData);
        }

        private void ProcessAudioPlayback()
        {
            if (!_isPlaying && _audioBuffer.Count > 2400) 
            {
                float[] data = _audioBuffer.ToArray();
                _audioBuffer.Clear();
                _playbackClip = AudioClip.Create("GeminiStream", data.Length, 1, 24000, false);
                _playbackClip.SetData(data, 0);
                agentAudioSource.clip = _playbackClip;
                agentAudioSource.Play();
                _isPlaying = true;
                StartCoroutine(WaitForAudioEnd(_playbackClip.length));
            }
        }
        private IEnumerator WaitForAudioEnd(float d) { yield return new WaitForSeconds(d); _isPlaying = false; }

        private List<string> CleanList(List<string> input)
        {
            if (input == null) return new List<string>();
            return input.Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => Regex.Replace(s.Trim(), @"[^a-zA-Z0-9_]", "")) 
                .Distinct().ToList();
        }
    }
}