using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using IVH.Core.ServiceConnector.Gemini.Realtime;

namespace IVH.Core.IntelligentVirtualAgent
{
    [RequireComponent(typeof(GeminiRealtimeWrapper))]
    [RequireComponent(typeof(AudioSource))]
    public class GeminiVoiceOnlyAgent : MonoBehaviour, IGeminiAgent
    {
        [Header("Gemini Configuration")]
        public string voiceName = "Puck";
        public bool autoConnectOnStart = true;

        [Header("Settings")]
        public bool showThinkingProcess = false; // SET TO FALSE TO HIDE THINKING

        [Header("Agent Persona")]
        [TextArea(3, 10)]
        public string systemInstruction = "You are a helpful AI voice assistant.";

        [Header("Audio Input")]
        public string microphoneDeviceName;
        [Range(0.1f, 10f)] public float inputGain = 2.0f;

        [Header("VAD & Interruption")]
        [Tooltip("If enabled, the agent will stop talking when it detects your voice.")]
        public bool enableVocalInterruption = true;
        [Tooltip("Mutes the microphone while the agent is speaking to prevent it from hearing its own echo (Use if not wearing headphones). Note: Disables interruption if vocal interruption is off!")]
        public bool muteMicWhileTalking = true;
        [Tooltip("Volume threshold required to interrupt the agent while it is speaking (must be higher than the echo volume).")]
        [Range(0.05f, 0.5f)] public float echoInterruptionThreshold = 0.15f;
        [Tooltip("Volume threshold (0.0 to 1.0) required to trigger voice detection.")]
        [Range(0.005f, 0.2f)] public float voiceDetectionThreshold = 0.04f;
        [Tooltip("If true, filters out non-vocal frequencies (hum, clicks) before detecting voice.")]
        public bool useVocalFrequencyFilter = true;
        [Tooltip("How long to wait (in seconds) after interruption before accepting new audio (avoids echoes).")]
        public float interruptionDebounceTime = 0.5f;

        [Header("UI Interface")]
        public Text logTextDisplay;
        public ScrollRect scrollRect;

        // --- Internal State ---
        private GeminiRealtimeWrapper _realtimeWrapper;
        private AudioSource _agentAudioSource;
        private List<float> _audioBuffer = new List<float>();
        private AudioClip _playbackClip;
        private bool _isPlaying = false;

        private AudioClip _micClip;
        private int _lastMicPos;
        private bool _isRecording;
        private bool _isSessionReady = false;
        private float _ignoreAudioUntil = 0f;

        // VAD Logic State
        private float _lpPrev = 0f;
        private float _hpPrevInput = 0f;
        private float _hpPrevOutput = 0f;

        // User Speech Tracking
        private bool _isUserSpeaking = false;
        private float _silenceTimer = 0f;
        private const float SILENCE_THRESHOLD = 0.8f;

        private void Awake()
        {
            _agentAudioSource = GetComponent<AudioSource>();
            _realtimeWrapper = GetComponent<GeminiRealtimeWrapper>();

            _realtimeWrapper.OnSetupComplete += HandleReady;
            _realtimeWrapper.OnAudioReceived += HandleAudioReceived;
            _realtimeWrapper.OnTextReceived += HandleTextReceived;
        }

        private void Start()
        {
            if (autoConnectOnStart) Connect();
        }

        private void OnDestroy()
        {
            StopMicrophone();
            if (_realtimeWrapper != null) _realtimeWrapper.DisconnectAsync();
        }

        private void Update()
        {
            if (_isSessionReady && _realtimeWrapper.IsConnected) ProcessMicrophone();
            ProcessAudioPlayback();
        }

        public void Connect()
        {
            if (logTextDisplay) logTextDisplay.text = "<i>System: Connecting...</i>";
            _isSessionReady = false;

            string noThinkingPrompt = "";
            if (!showThinkingProcess)
            {
                noThinkingPrompt = " STRICT RULE: Do not output internal thoughts, markdown, or verbose planning text. Output direct speech text only.";
            }

            string finalPrompt = systemInstruction + noThinkingPrompt;
            var toolManager = GetComponent<GeminiToolManager>();

            if (toolManager != null && toolManager.definedTools.Count > 0)
            {
                _ = _realtimeWrapper.ConnectWithDynamicToolsAsync(finalPrompt, voiceName, toolManager.GetDynamicToolDeclarations());
            }
            else
            {
                _ = _realtimeWrapper.ConnectAsync(finalPrompt, voiceName);
            }
        }

        private void HandleSystemError(string errorMessage)
        {
            AppendLog($"<color=red><b>System Error:</b> {errorMessage}</color>");
            Debug.LogError($"[Gemini Agent Error] {errorMessage}");
        }

        public void Disconnect()
        {
            if (!_isSessionReady) return;
            
            AppendLog("<color=orange>System: Disconnecting...</color>");
            
            _isSessionReady = false;
            StopMicrophone();

            if (_agentAudioSource != null && _agentAudioSource.isPlaying)
            {
                _agentAudioSource.Stop();
            }
            
            _audioBuffer.Clear();
            _isPlaying = false;

            if (_realtimeWrapper != null)
            {
                _ = _realtimeWrapper.DisconnectAsync();
            }
        }
        public void Reconnect()
        {
            AppendLog("<color=orange>System: Forcing Reconnect...</color>");
            StartCoroutine(ReconnectRoutine());
        }

        private IEnumerator ReconnectRoutine()
        {
            // 1. Instantly halt Update() processing
            _isSessionReady = false;

            // 2. Stop microphone input
            StopMicrophone();

            // 3. Stop audio playback and clear buffers
            if (_agentAudioSource != null && _agentAudioSource.isPlaying)
            {
                _agentAudioSource.Stop();
            }
            _audioBuffer.Clear();
            _isPlaying = false;

            // 4. Disconnect the underlying socket/wrapper
            if (_realtimeWrapper != null)
            {
                _ = _realtimeWrapper.DisconnectAsync();
            }

            // 5. Wait briefly to ensure sockets close and state is cleared on the main thread
            yield return new WaitForSeconds(0.5f);

            // 6. Spin up a completely fresh session
            Connect();
        }

        private void HandleReady()
        {
            AppendLog("<color=green>System: Connected.</color>");
            _isSessionReady = true;
            StartMicrophone();

            if (_realtimeWrapper.selectedModel == GeminiModelType.Flash25VertexAI || _realtimeWrapper.selectedModel == GeminiModelType.Flash25PreviewGoogleAI)
            {
                _realtimeWrapper.SendTextMessage("System: Session Started. Greet the user.");
            }
        }

        private void HandleTextReceived(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            if (!showThinkingProcess)
            {
                if (text.Contains("**") || text.StartsWith("*")) return;
                if (text.ToLower().Contains("processing the initial instruction")) return;
            }
            AppendLog($"<color=cyan>Gemini:</color> {text}");
        }

        private void AppendLog(string message)
        {
            if (logTextDisplay != null)
            {
                logTextDisplay.text += $"\n\n{message}";
                if (scrollRect != null)
                {
                    StopCoroutine(ForceScrollDown());
                    StartCoroutine(ForceScrollDown());
                }
            }
            else
            {
                Debug.Log(message);
            }
        }

        IEnumerator ForceScrollDown()
        {
            yield return new WaitForEndOfFrame();
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;
            yield return new WaitForEndOfFrame();
            scrollRect.verticalNormalizedPosition = 0f;
        }

        // --- Audio Logic ---
        private void StartMicrophone()
        {
            if (_isRecording) return;

            // If no mic is explicitly set in the inspector, let's find the right one
            if (string.IsNullOrEmpty(microphoneDeviceName))
            {
                if (Microphone.devices.Length > 0)
                {
                    // Set a fallback default first
                    microphoneDeviceName = Microphone.devices[0]; 

                    // Hunt for the Oculus or VR headset microphone
                    foreach (string device in Microphone.devices)
                    {
                        string lowerDevice = device.ToLower();
                        if (lowerDevice.Contains("oculus") || lowerDevice.Contains("virtual") )
                        {
                            microphoneDeviceName = device;
                            AppendLog($"<color=green>System: Auto-selected VR Mic ({microphoneDeviceName})</color>");
                            break;
                        }
                    }
                }
                else
                {
                    AppendLog("<color=red>System Error: No microphone devices found by Unity!</color>");
                    return;
                }
            }

            _micClip = Microphone.Start(microphoneDeviceName, true, 3599, 16000);
            while (Microphone.GetPosition(microphoneDeviceName) <= 0) { }

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
            if (currentPos < _lastMicPos) { _lastMicPos = 0; return; }

            int diff = currentPos - _lastMicPos;
            if (diff > 800) // ~50ms chunks
            {
                float[] samples = new float[diff];
                _micClip.GetData(samples, _lastMicPos);

                // Determine threshold based on whether the agent is talking
                float currentThreshold = _isPlaying ? echoInterruptionThreshold : voiceDetectionThreshold;
                bool detectedSpeech = IsSpeechDetected(samples, currentThreshold);

                if (_isPlaying)
                {
                    if (detectedSpeech && enableVocalInterruption)
                    {
                        // Speech loud enough to overcome echo threshold detected!
                        _silenceTimer = 0f;
                        if (!_isUserSpeaking)
                        {
                            _isUserSpeaking = true;
                            AppendLog($"<color=yellow>User:</color> <i>[Speaking...]</i>");
                        }
                        AppendLog("<color=red>[Interruption]</color>");
                        InterruptPlayback();
                        // Array is kept intact so Gemini hears the interruption
                    }
                    else if (muteMicWhileTalking)
                    {
                        // Sound is below the echo threshold. Zero out array to mute mic for Gemini.
                        Array.Clear(samples, 0, samples.Length);
                    }
                }
                else
                {
                    // Agent is quiet, do normal VAD/Silence tracking for UI
                    if (detectedSpeech)
                    {
                        _silenceTimer = 0f;
                        if (!_isUserSpeaking)
                        {
                            _isUserSpeaking = true;
                            AppendLog($"<color=yellow>User:</color> <i>[Speaking...]</i>");
                        }
                    }
                    else
                    {
                        _silenceTimer += (diff / 16000f);
                        if (_silenceTimer > SILENCE_THRESHOLD) _isUserSpeaking = false;
                    }
                }

                // Send Audio
                byte[] pcmData = new byte[samples.Length * 2];
                for (int i = 0; i < samples.Length; i++)
                {
                    float sample = samples[i] * inputGain;
                    sample = Mathf.Clamp(sample, -1f, 1f);
                    short val = (short)(sample * 32767);
                    BitConverter.GetBytes(val).CopyTo(pcmData, i * 2);
                }

                _realtimeWrapper.SendAudioChunk(pcmData);
                _lastMicPos = currentPos;
            }
        }

        private bool IsSpeechDetected(float[] rawSamples, float currentThreshold)
        {
            float sumSquared = 0f;
            int count = rawSamples.Length;
            for (int i = 0; i < count; i++)
            {
                float sample = rawSamples[i];
                if (useVocalFrequencyFilter)
                {
                    _lpPrev = _lpPrev + 0.5f * (sample - _lpPrev);
                    float lowPassed = _lpPrev;
                    float highPassed = 0.9f * (_hpPrevOutput + lowPassed - _hpPrevInput);
                    _hpPrevOutput = highPassed;
                    _hpPrevInput = lowPassed;
                    sample = highPassed;
                }
                sumSquared += sample * sample;
            }
            float rms = Mathf.Sqrt(sumSquared / count);
            return (rms * inputGain) > currentThreshold;
        }

        private void InterruptPlayback()
        {
            if (_agentAudioSource.isPlaying) _agentAudioSource.Stop();
            _audioBuffer.Clear();
            _isPlaying = false;
            _ignoreAudioUntil = Time.time + interruptionDebounceTime;
        }

        private void HandleAudioReceived(byte[] pcmData)
        {
            if (Time.time < _ignoreAudioUntil) return;
            int count = pcmData.Length / 2;
            for (int i = 0; i < count; i++) _audioBuffer.Add(BitConverter.ToInt16(pcmData, i * 2) / 32768.0f);
        }

        private void ProcessAudioPlayback()
        {
            if (Time.time < _ignoreAudioUntil) return;
            if (!_isPlaying && _audioBuffer.Count > 2400)
            {
                float[] data = _audioBuffer.ToArray();
                _audioBuffer.Clear();
                if (data.Length == 0) return;

                _playbackClip = AudioClip.Create("GeminiVoiceStream", data.Length, 1, 24000, false);
                _playbackClip.SetData(data, 0);
                _agentAudioSource.clip = _playbackClip;
                _agentAudioSource.Play();
                _isPlaying = true;
                StartCoroutine(WaitForAudioEnd(_playbackClip.length));
            }
        }

        private IEnumerator WaitForAudioEnd(float duration)
        {
            yield return new WaitForSeconds(duration);
            if (!_agentAudioSource.isPlaying || _agentAudioSource.clip == _playbackClip) _isPlaying = false;
        }
    }
}