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
        
        // --- CHANGE: Default Gain Increased to 5.0 to fix low volume issues ---
        [Range(0.1f, 20f)] public float inputGain = 5.0f; 
        public bool debugMicrophoneLevels = true; 

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
            _realtimeWrapper.OnTextReceived += (text) => { Debug.Log($"<color=white>Gemini:</color> {text}"); };
            _realtimeWrapper.OnCommandReceived += (act, emo, gaze) => {
                Debug.Log($"<color=cyan>COMMAND:</color> {act} / {emo} / {gaze}");
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
            if (string.IsNullOrEmpty(microphoneDeviceName) && Microphone.devices.Length > 0) microphoneDeviceName = Microphone.devices[0];
            
            // Force 16kHz for Gemini
            _micClip = Microphone.Start(microphoneDeviceName, true, 20, 16000);
            while(Microphone.GetPosition(microphoneDeviceName) <= 0) { } 
            
            _lastMicPos = 0;
            _isRecording = true;
        }

        private void StopMicrophone()
        {
            if (_isRecording) { Microphone.End(microphoneDeviceName); _isRecording = false; }
        }

        private void ProcessMicrophone()
        {
            if (!_isRecording || _micClip == null) return;
            
            int currentPos = Microphone.GetPosition(microphoneDeviceName);
            if (currentPos < _lastMicPos) currentPos = _micClip.samples; 
            
            int diff = currentPos - _lastMicPos;
            if (diff > 800) // ~50ms chunks
            {
                float[] samples = new float[diff];
                _micClip.GetData(samples, _lastMicPos);

                byte[] pcmData = new byte[samples.Length * 2];
                float maxVol = 0f;

                for (int i = 0; i < samples.Length; i++)
                {
                    float sample = samples[i] * inputGain; // Apply Gain
                    if (Mathf.Abs(sample) > maxVol) maxVol = Mathf.Abs(sample);
                    
                    sample = Mathf.Clamp(sample, -1f, 1f);
                    short val = (short)(sample * 32767);
                    BitConverter.GetBytes(val).CopyTo(pcmData, i * 2);
                }

                _realtimeWrapper.SendAudioChunk(pcmData);

                if (debugMicrophoneLevels && maxVol > 0.05f) 
                    Debug.Log($"Mic Input > {maxVol:F2}");

                _lastMicPos = currentPos;
                if (_lastMicPos >= _micClip.samples) _lastMicPos = 0;
            }
        }

        private string BuildSystemPrompt()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Your name is {agentName}. You are a conversational virtual human.");
            sb.AppendLine("You must use the function 'update_avatar_state' to perform actions or change emotions.");
            sb.AppendLine("DO NOT speak the action names. Just call the function.");

            if (actionController != null)
            {
                var actions = actionController.GetSimpleActionNameFiltered(bodyActionFilter, gender, bodyAnimationControllerType);
                sb.AppendLine("Available Actions: " + string.Join(", ", CleanList(actions)));
            }

            if (faceAnimator != null)
            {
                var emotions = faceAnimator.GetSimpleFacialExpressionNameFiltered(facialExpressionFilter);
                sb.AppendLine("Available Emotions: " + string.Join(", ", CleanList(emotions)));
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

        private void ProcessAudioPlayback()
        {
            if (!_isPlaying && _audioBuffer.Count > 3200) // Lower buffer for faster response
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