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
    public interface IGeminiAgent { }
    [RequireComponent(typeof(GeminiRealtimeWrapper))]
    public class GeminiLiveAgent : AgentBase, IGeminiAgent
    {
        [Header("Gemini Configuration")]
        public string voiceName = "Puck"; 
        public bool autoConnectOnStart = true;
        
        [Header("Audio Input")]
        public string microphoneDeviceName;
        [Range(0.1f, 10f)] public float inputGain = 2.0f; 
        [Header("VAD & Interruption")]
        [Tooltip("If enabled, the agent will stop talking when it detects your voice.")]
        public bool enableVocalInterruption = true;
        [Tooltip("Mutes the microphone while the agent is speaking to prevent it from hearing its own echo (Use if not wearing headphones). Note: Disables interruption!")]
        public bool muteMicWhileTalking = true;

        [Tooltip("Volume threshold (0.0 to 1.0) required to trigger voice detection.")]
        [Range(0.005f, 0.2f)] public float voiceDetectionThreshold = 0.04f;
        
        [Tooltip("If true, filters out non-vocal frequencies (hum, clicks) before detecting voice.")]
        public bool useVocalFrequencyFilter = true;

        [Tooltip("How long to wait (in seconds) after interruption before accepting new audio (avoids echoes).")]
        public float interruptionDebounceTime = 0.5f;
        
        [Tooltip("How often (in seconds) to send a frame to Gemini. 0.5 = 2fps, 1.0 = 1fps.")]
        [HideInInspector] [Range(0.2f, 5.0f)] public float visionUpdateFrequency = 1.0f;
        private Coroutine _visionCoroutine;
        // --- Internal State ---
        private GeminiRealtimeWrapper _realtimeWrapper;
        private List<float> _audioBuffer = new List<float>();
        private StringBuilder _fullTranscript = new StringBuilder();
        
        private AudioClip _playbackClip;
        private bool _isPlaying = false;
        private AudioClip _micClip;
        private int _lastMicPos;
        private bool _isRecording;
        private bool _isSessionReady = false;
        private bool _handshakeComplete = false; 
        private float _ignoreAudioUntil = 0f;

        // DSP Memory for Bandpass Filter
        private float _lpPrev = 0f;
        private float _hpPrevInput = 0f;
        private float _hpPrevOutput = 0f;

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
                StartCoroutine(QueueCommandUntilStopped(act, emo, gaze));
            };

            _realtimeWrapper.OnMoveCommand += (angle, distance, speed, faceMovementDirection) => 
            {
                Debug.Log($"<color=cyan>MOVE:</color> Angle: {angle}°, Dist: {distance}m, Speed: {speed}m/s, FaceDir: {faceMovementDirection}");
                
                if (agentInstance == null) return;

                Vector3 startPos = agentInstance.transform.position;
                Vector3 flatForward = Vector3.ProjectOnPlane(agentInstance.transform.forward, Vector3.up).normalized;

                Vector3 moveDirection = Quaternion.Euler(0f, angle, 0f) * flatForward;
                Vector3 targetPos = startPos + moveDirection.normalized * distance;

                if (agentLocomotion != null)
                {
                    agentLocomotion.MoveToPoint(targetPos, speed, faceMovementDirection);
                    if (player != null)
                        agentLocomotion.cameraPos = player.transform.position;
                }
            };
        }

        private void Start()
        {
            base.FindPlayer();
            if(autoConnectOnStart) Connect();
        }

        private void OnDestroy()
        {
            if (_visionCoroutine != null) StopCoroutine(_visionCoroutine);
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

            _isSessionReady = false;
            string noThinkingPrompt = "";
            noThinkingPrompt = " STRICT RULE: Do not output internal thoughts, markdown, or verbose planning text. Output direct speech text only.";
            string dynamicPrompt = BuildSystemPrompt();

            string finalPrompt = dynamicPrompt + noThinkingPrompt;
            var toolManager = GetComponent<GeminiToolManager>();
            if (toolManager != null && toolManager.definedTools.Count > 0)
            {
                _ = _realtimeWrapper.ConnectWithDynamicToolsAsync(finalPrompt, voiceName, toolManager.GetDynamicToolDeclarations());
            }
            else
            {
                if (characterType == CharacterType.CC4OrDIDIMO && enableLocomotion==true)
                {
                    // has locomotion is true
                    _ = _realtimeWrapper.ConnectAsync(finalPrompt, voiceName, true);
                }
                else
                {
                    _ = _realtimeWrapper.ConnectAsync(finalPrompt, voiceName, false);
                }
            }

        }

        private IEnumerator WaitForGreetingToFinishAndStartVision()
        {
            float timeout = 10f;
            while (timeout > 0)
            {
                if (_isPlaying && agentAudioSource.isPlaying)
                {
                    break; // Audio started!
                }
                timeout -= 0.1f;
                yield return new WaitForSeconds(0.1f);
            }
            while (_isPlaying && agentAudioSource.isPlaying)
            {
                yield return new WaitForSeconds(0.2f);
            }
            
            Debug.Log("<color=cyan>Greeting Finished. Vision Stream ACTIVE.</color>");
            
            _handshakeComplete = true; 
            _visionCoroutine = StartCoroutine(AutoCaptureLoop());
        }
        private void HandleReady()
        {
            Debug.Log("<color=green>Gemini Live Ready!</color>");
            _isSessionReady = true;
            StartMicrophone();
            StartCoroutine(SendGreetingDelayed());
            // Start automatic vision if enabled
            if (vision)
            {
                if (_visionCoroutine != null) StopCoroutine(_visionCoroutine);

                if(_realtimeWrapper.selectedModel == GeminiModelType.Flash25VertexAI)
                {
                    StartCoroutine(WaitForGreetingToFinishAndStartVision());
                }
                else
                {
                    _visionCoroutine = StartCoroutine(AutoCaptureLoop());
                    
                }
            }

        }
        private IEnumerator SendGreetingDelayed()
        {
            yield return new WaitForSeconds(1.0f);
            
            if(_realtimeWrapper.selectedModel == GeminiModelType.Flash25VertexAI || _realtimeWrapper.selectedModel == GeminiModelType.Flash25PreviewGoogleAI)
            {
                // We MUST force the initial tool call to kickstart Vertex's state machine
                string silentSetupPrompt = "System: STARTUP SEQUENCE.\n" +
                    "1. Call 'update_avatar_state' to set your initial pose.\n" +
                    "2. Wait for the tool confirmation.\n" +
                    "3. After confirmation, verbally greet the user.\n" +
                    "INSTRUCTION: You are a real-time audio model. Output audio. Do not generate text.";
                    
                _realtimeWrapper.SendTextMessage(silentSetupPrompt);
            }
            else
            {
                _realtimeWrapper.SendTextMessage("System: Session started. Call update_avatar_state ONCE and Greet the user.");   
            }
        }

        private IEnumerator AutoCaptureLoop()
        {
            while (_isSessionReady && _realtimeWrapper.IsConnected)
            {
                yield return CaptureAndSend();

                yield return new WaitForSeconds(visionUpdateFrequency);
            }
        }

        private void StartMicrophone()
        {
            if (_isRecording) return;
            if (string.IsNullOrEmpty(microphoneDeviceName) && Microphone.devices.Length > 0) 
                microphoneDeviceName = Microphone.devices[0];
            
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
        if (currentPos < _lastMicPos) { _lastMicPos = 0; return; }
        
        int diff = currentPos - _lastMicPos;
        if (diff > 800) // ~50ms chunks
        {
            float[] samples = new float[diff];
            _micClip.GetData(samples, _lastMicPos);

            // --- NEW ECHO CANCELLATION LOGIC ---
            // If the agent is talking and we want to prevent echo, zero out the audio chunk.
            if (muteMicWhileTalking && _isPlaying)
            {
                Array.Clear(samples, 0, samples.Length);
            }

            // --- 1. INTELLIGENT VOICE DETECTION ---
            // Because samples might be zeroed out above, this will correctly return false 
            // while the agent is speaking, naturally preventing false interruption triggers.
            bool detectedSpeech = IsSpeechDetected(samples);

            // --- 2. INTERRUPTION TRIGGER ---
            if (_isPlaying && detectedSpeech && enableVocalInterruption)
            {
                Debug.Log($"<color=yellow>INTERRUPTING: Speech Detected</color>");
                InterruptPlayback();
            }

            // --- 3. PREPARE & SEND DATA ---
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
        /// <summary>
        /// Analyzes audio chunk to determine if human speech is present.
        /// Uses a bandpass filter (300Hz-3000Hz) to ignore rumble and hiss.
        /// </summary>
        private bool IsSpeechDetected(float[] rawSamples)
        {
            float sumSquared = 0f;
            int count = rawSamples.Length;

            for (int i = 0; i < count; i++)
            {
                float sample = rawSamples[i];

                if (useVocalFrequencyFilter)
                {
                    // Apply Bandpass Filter (Approx 200Hz - 3000Hz at 16kHz sample rate)
                    // 1. Low Pass (remove high hiss > 3kHz)
                    // Simple IIR Low Pass: y[i] = y[i-1] + alpha * (x[i] - y[i-1])
                    // Alpha ~ 0.5 for ~3kHz cut-off at 16kHz
                    _lpPrev = _lpPrev + 0.5f * (sample - _lpPrev);
                    float lowPassed = _lpPrev;

                    // 2. High Pass (remove rumble < 200Hz)
                    // Simple IIR High Pass: y[i] = alpha * (y[i-1] + x[i] - x[i-1])
                    // Alpha ~ 0.9 for ~200Hz cut-off
                    float highPassed = 0.9f * (_hpPrevOutput + lowPassed - _hpPrevInput);
                    _hpPrevOutput = highPassed;
                    _hpPrevInput = lowPassed;

                    sample = highPassed;
                }

                sumSquared += sample * sample;
            }

            // Calculate RMS of the *filtered* signal
            float rms = Mathf.Sqrt(sumSquared / count);

            // Compare RMS against threshold (scaled by input gain to match user expectation)
            return (rms * inputGain) > voiceDetectionThreshold;
        }

        private void InterruptPlayback()
        {
            if (agentAudioSource.isPlaying) agentAudioSource.Stop();
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

                _playbackClip = AudioClip.Create("GeminiStream", data.Length, 1, 24000, false);
                _playbackClip.SetData(data, 0);
                agentAudioSource.clip = _playbackClip;
                agentAudioSource.Play();
                _isPlaying = true;
                StartCoroutine(WaitForAudioEnd(_playbackClip.length));
            }
        }

        private IEnumerator WaitForAudioEnd(float duration) 
        { 
            yield return new WaitForSeconds(duration); 
            if(!agentAudioSource.isPlaying || agentAudioSource.clip == _playbackClip) _isPlaying = false; 
        }
        // --- View & Prompt Helpers ---

        public void SendCurrentView() => StartCoroutine(CaptureAndSend());
        private IEnumerator CaptureAndSend()
        {
            if (targetCameraType == TargetCameraType.WebCam)
            {
                yield return CaptureWebcamImage(); 
                if (webCamImageData != null && _isSessionReady) _realtimeWrapper.SendImage(webCamImageData);
            }

            if(targetCameraType == TargetCameraType.AgentCamera)
            {
                yield return CaptureEgocentricImageCoroutine(); 
                if (egoImageData != null && _isSessionReady) _realtimeWrapper.SendImage(egoImageData);
            }
        }
        private string BuildSystemPrompt()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Your name is {agentName}. You are a conversational embodied intelligent virtual agent. Your age is {age}. Your gender is {gender}. Your occupation is {occupation}. Additional information: {additionalDescription}.");
            // Inside BuildSystemPrompt()
            sb.AppendLine("SYSTEM RULES:");
            sb.AppendLine("1. You control a 3D avatar. ONLY call 'update_avatar_state' if your emotional state or physical action (e.g. facial expression, gaze, body languages) needs to change based on the conversation.");            sb.AppendLine("2. If the user asks you to move (e.g., 'step back', 'come here'), call 'move_agent'.");
            sb.AppendLine("3. DO NOT call 'update_avatar_state' at the start of every turn. If your state hasn't changed, just speak.");
            sb.AppendLine("4. If the user's request requires using other available tools, you may call them.");
            sb.AppendLine("5. Do not pause your speech to narrate your tool calls. Call the necessary tools and deliver your spoken response normally."); 

            // Logic for Affective Analysis support
            if (_realtimeWrapper.affectiveAnalysis)
            {
                sb.AppendLine("5. Listen carefully to the user's voice and watch carefully within your vision. If they sound emotional, match your 'emotion' parameter to their sentiment (e.g., if they sound sad, set emotion to 'sad' or 'concerned').");
            }

            if (actionController != null)
            {
                var actions = actionController.GetSimpleActionNameFiltered(bodyActionFilter, gender, bodyAnimationControllerType);
                sb.AppendLine("Allowed 'action' values: " + string.Join(", ", CleanList(actions)));
            }
            if(emotionHandlerType == EmotionHandlerType.CC4_Animation){
                if (faceAnimator != null)
                {
                    var emotions = faceAnimator.GetSimpleFacialExpressionNameFiltered(facialExpressionFilter);
                    sb.AppendLine("Allowed 'emotion' values: " + string.Join(", ", CleanList(emotions)));
                }
            }
            else
            {
                if (faceAnimator != null)
                {
                    var emotions = faceAnimator.GetSimpleDidimoActionName();
                    sb.AppendLine("Allowed 'emotion' values: " + string.Join(", ", CleanList(emotions)));
                }
            }
            if (eyeGazeController != null)
            {
                sb.AppendLine("Allowed 'gaze' values: 'User', 'Idle'");
            }
            
            return sb.ToString();
        }
        public bool IsSessionReady()
        {
            return _isSessionReady;
        }
        private void HandleGaze(string mode)
        {
             if (string.IsNullOrEmpty(mode) || mode == "none") return;
             if (mode.Equals("LookAtUser", StringComparison.OrdinalIgnoreCase) || mode.Equals("User", StringComparison.OrdinalIgnoreCase)) {
                 if (player == null) FindPlayer();
                 if (eyeGazeController != null) { eyeGazeController.playerTarget = player; eyeGazeController.currentGazeMode = IVH.Core.Actions.EyeGazeController.GazeMode.LookAtPlayer; }
             } else if (eyeGazeController != null) eyeGazeController.currentGazeMode = IVH.Core.Actions.EyeGazeController.GazeMode.Idle;
        }

        private List<string> CleanList(List<string> input)
        {
            if (input == null) return new List<string>();
            return input.Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => Regex.Replace(s.Trim(), @"[^a-zA-Z0-9_]", "")) 
                .Distinct().ToList();
        }

        private IEnumerator QueueCommandUntilStopped(string act, string emo, string gaze)
        {
            yield return new WaitForSeconds(0.5f);

            if (agentLocomotion != null)
            {
                while (agentLocomotion.isMoving)
                {
                    yield return null;
                }
            }

            
            if(!string.IsNullOrEmpty(act) && act != "none") PerformAction(act);
            if(!string.IsNullOrEmpty(emo) && emo != "none") ExpressEmotion(emo);
            if(!string.IsNullOrEmpty(gaze) && gaze != "none") HandleGaze(gaze);
        }

        // setup agent
        public override void SetupVirtualAgent()
        {

            if (agentPrefab != null && agentInstance == null)
            {
                agentInstance = Instantiate(agentPrefab, transform.position, transform.rotation);
                agentInstance.name = agentName;
                agentInstance.transform.SetParent(transform);


                AssignAnimatorController();
                //AssignCharacterController();
                SetupLipSync();
                SetupAgentActionController();
                SetupEMotionHandler();
                SetupAgentVisionCamera();
                SetupSimpleEyeBlink();
                SetupEyeGazeController();
                SetupAudio();
                //SetupUIIndicator();
                if (characterType == CharacterType.CC4OrDIDIMO)
                {
                    SetupAgentLocomotion();
                }
                _realtimeWrapper = GetComponent<GeminiRealtimeWrapper>();
                if (_realtimeWrapper == null) _realtimeWrapper = gameObject.AddComponent<GeminiRealtimeWrapper>();

            }
            else
            {
                Debug.LogWarning("Agent prefab is not assigned or agent is already set up.");
            }
        }

    }
}