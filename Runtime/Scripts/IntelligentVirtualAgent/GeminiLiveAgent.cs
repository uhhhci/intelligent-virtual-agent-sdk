using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using IVH.Core.IntelligentVirtualAgent;
using IVH.Core.ServiceConnector.Gemini.Realtime;
using IVH.Core.Exceptions;
using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace IVH.Core.IntelligentVirtualAgent
{
    /// <summary>Marker interface for agents backed by a <see cref="GeminiRealtimeWrapper"/>.</summary>
    public interface IGeminiAgent { }

    /// <summary>
    /// Full-body embodied agent powered by Gemini Live's realtime WebSocket API. Handles microphone
    /// capture, voice-activity detection, interruption, vision streaming, and routing model-emitted
    /// commands (body action / emotion / gaze / locomotion) to the avatar's behavior subsystems.
    /// </summary>
    /// <remarks>
    /// For a voice-only variant without an avatar, use <see cref="GeminiVoiceOnlyAgent"/>.
    /// For modular STT/LLM/TTS pipelines, use <see cref="ConversationalAgent"/>.
    /// </remarks>
    [RequireComponent(typeof(GeminiRealtimeWrapper))]
    public class GeminiLiveAgent : AgentBase, IGeminiAgent
    {
        [Header("Gemini Configuration")]
        /// <summary>Gemini TTS voice name (e.g. "Puck", "Charon", "Kore", "Fenrir", "Aoede").</summary>
        public string voiceName = "Puck";

        /// <summary>If true, opens the Gemini Live session automatically in <c>Start()</c>.</summary>
        public bool autoConnectOnStart = true;

        /// <summary>
        /// Appends conversational-style guidance to the system prompt: answer directly and
        /// substantively, stay in first person, share concrete details, and avoid ending every reply
        /// with a clarifying question. Recommended for knowledge-grounded personas — it counteracts
        /// the "Is there a specific aspect you're interested in?" stalling loop. Set false to restore
        /// the bare prompt without the conversation-style block.
        /// </summary>
        [Tooltip("Add guidance to answer directly and substantively in first person, and not end every reply with a clarifying question. Recommended for grounded personas.")]
        public bool proactiveAnswerStyle = true;

        [Header("Audio Input")]
        /// <summary>Microphone device name. Leave blank to auto-select the first available device.</summary>
        public string microphoneDeviceName;

        /// <summary>Linear gain applied to microphone samples before sending to Gemini. 1.0 = no change.</summary>
        [Range(0.1f, 10f)] public float inputGain = 2.0f;

        [Header("VAD & Interruption")]
        /// <summary>If true, user voice above <see cref="echoInterruptionThreshold"/> interrupts agent speech.</summary>
        [Tooltip("If enabled, the agent will stop talking when it detects your voice.")]
        public bool enableVocalInterruption = true;

        /// <summary>Silences the microphone while the agent is speaking (for open-mic setups without headphones).</summary>
        [Tooltip("Mutes the microphone while the agent is speaking to prevent it from hearing its own echo (Use if not wearing headphones). Note: Disables interruption!")]
        public bool muteMicWhileTalking = true;

        /// <summary>RMS volume (0.0–0.5) required to interrupt agent speech. Higher = more tolerant of echo.</summary>
        [Tooltip("Volume threshold required to interrupt the agent while it is speaking (must be higher than the echo volume).")]
        [Range(0.05f, 0.5f)] public float echoInterruptionThreshold = 0.15f;

        /// <summary>RMS volume (0.005–0.2) required to register user speech when the agent is silent.</summary>
        [Tooltip("Volume threshold (0.0 to 1.0) required to trigger voice detection.")]
        [Range(0.005f, 0.2f)] public float voiceDetectionThreshold = 0.04f;

        /// <summary>If true, applies a bandpass filter to the mic signal before VAD to reject non-vocal noise.</summary>
        [Tooltip("If true, filters out non-vocal frequencies (hum, clicks) before detecting voice.")]
        public bool useVocalFrequencyFilter = true;

        /// <summary>Seconds to ignore incoming audio after an interruption, to avoid re-triggering on echo tail.</summary>
        [Tooltip("How long to wait (in seconds) after interruption before accepting new audio (avoids echoes).")]
        public float interruptionDebounceTime = 0.5f;

        /// <summary>Interval in seconds between vision frames. 0.5 = 2 fps, 1.0 = 1 fps. Higher = more tokens.</summary>
        [Tooltip("How often (in seconds) to send a frame to Gemini. 0.5 = 2fps, 1.0 = 1fps.")]
        [HideInInspector] [Range(0.2f, 5.0f)] public float visionUpdateFrequency = 1.0f;

        [Header("UI Interface")]
        /// <summary>
        /// When true, the agent asks Gemini for server-side input/output transcription and renders
        /// both the user's spoken command and Gemini's spoken reply as text in <see cref="logTextDisplay"/>.
        /// Backed by <see cref="GeminiRealtimeWrapper.enableTranscription"/>; the wrapper-level flag is
        /// forced on in <c>Awake</c> while this is true so a manually-set <c>false</c> there cannot
        /// silently blank the panel.
        /// </summary>
        [Tooltip("Show the user's spoken command and Gemini's spoken reply as text in the in-game log panel. Enables server-side transcription on the wrapper.")]
        public bool showSpeechTranscripts = false;

        /// <summary>UI Text used to render the running conversation log. Wired up by the editor scaffolder.</summary>
        public Text logTextDisplay;

        /// <summary>Scroll rect that wraps <see cref="logTextDisplay"/>. Auto-pinned to the bottom on each new fragment.</summary>
        public ScrollRect scrollRect;

        private Coroutine _visionCoroutine;
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

        // After a client-side interrupt, drop ALL incoming agent audio until Gemini's server-side
        // VAD acknowledges via OnServerInterrupted, or a safety timeout elapses. Without this, the
        // prior turn's queued audio resumes after the debounce window and the agent appears to
        // "talk through" the interruption.
        private bool _droppingAudioPostInterrupt = false;
        private float _postInterruptDropUntil = 0f;

        /// <summary>
        /// Maximum time (seconds) to drop incoming agent audio after a client-side interruption
        /// while waiting for Gemini's server-side acknowledgment. Belt-and-suspenders guard.
        /// </summary>
        [Tooltip("Hard cap on how long we drop incoming agent audio after a client-side interrupt while waiting for server-side ack.")]
        [Range(1f, 30f)] public float postInterruptDropTimeoutSeconds = 10f;

        /// <summary>
        /// Fires whenever the agent is interrupted (voice or programmatic). Used by
        /// <see cref="Observability.SessionRecorder"/> and <see cref="Memory.MemoryManager"/> to log
        /// the event. Subscribe in your own code to drive UI feedback.
        /// </summary>
        public event Action<InterruptionInfo> OnAgentInterrupted;

        // DSP Memory for Bandpass Filter
        private float _lpPrev = 0f;
        private float _hpPrevInput = 0f;
        private float _hpPrevOutput = 0f;

        protected override void Awake()
        {
            base.Awake();
            _realtimeWrapper = GetComponent<GeminiRealtimeWrapper>();
            if (_realtimeWrapper == null) _realtimeWrapper = gameObject.AddComponent<GeminiRealtimeWrapper>();

            // Force the wrapper-level transcription flag on when the agent wants transcripts in the
            // UI panel, otherwise OnUserTranscriptReceived / OnTextReceived (output transcription)
            // never fire and the log stays blank even with logTextDisplay wired.
            if (showSpeechTranscripts) _realtimeWrapper.enableTranscription = true;

            _realtimeWrapper.OnSetupComplete += HandleReady;
            _realtimeWrapper.OnAudioReceived += HandleAudioReceived;

            _realtimeWrapper.OnTextReceived += HandleTextReceived;
            _realtimeWrapper.OnUserTranscriptReceived += HandleUserTranscriptReceived;

            _realtimeWrapper.OnCommandReceived += (act, emo, gaze) => {
                Debug.Log($"<color=cyan>CMD:</color> {act}");
                StartCoroutine(QueueCommandUntilStopped(act, emo, gaze));
            };

            _realtimeWrapper.OnServerInterrupted += HandleServerInterrupted;

            // Without this, a failed handshake (bad key, missing Vertex service account, rejected
            // setup) is only written to the Console and the HUD sits on "Connecting..." indefinitely.
            _realtimeWrapper.OnFatalError += HandleFatalError;

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
            if (_realtimeWrapper != null)
            {
                _realtimeWrapper.OnFatalError -= HandleFatalError;
                _realtimeWrapper.DisconnectAsync();
            }
        }

        private void Update()
        {
            if (_isSessionReady && _realtimeWrapper.IsConnected) ProcessMicrophone();
            ProcessAudioPlayback();
        }

        /// <summary>
        /// Opens the Gemini Live WebSocket session using the current configuration. Builds the
        /// system prompt, registers any dynamic tools, and starts microphone + vision streams
        /// once the session is ready.
        /// </summary>
        public async void Connect()

        {
            if (logTextDisplay != null) logTextDisplay.text = "<i>System: Connecting...</i>";
            _isSessionReady = false;
            string noThinkingPrompt = "";
            noThinkingPrompt = " STRICT RULE: Do not output internal thoughts, markdown, or verbose planning text. Output direct speech text only.";
            string dynamicPrompt = BuildSystemPrompt();

            // Realtime session: retrieve grounding context once at connection time, since Gemini
            // Live takes the system instruction as a one-shot at setup. Empty when no
            // IContextProvider is attached, matching v3.0 behavior.
            string knowledgePrefix = await BuildContextPrefixAsync(null);
            string finalPrompt = knowledgePrefix + dynamicPrompt + noThinkingPrompt;
            var toolManager = GetComponent<GeminiToolManager>();
            if (toolManager != null && toolManager.definedTools.Count > 0)
            {
                _ = _realtimeWrapper.ConnectWithDynamicToolsAsync(finalPrompt, voiceName, toolManager.GetDynamicToolDeclarations());
            }
            else
            {
                if (characterType == CharacterType.CC4OrDIDIMO && enableLocomotion==true)
                {
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
                    break;
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
        /// <summary>
        /// Surfaces an unrecoverable session error in the HUD and the Console, and tears down the
        /// mic/vision streams so the scene does not keep capturing into a dead session.
        /// </summary>
        private void HandleFatalError(IVAException error)
        {
            _isSessionReady = false;
            StopMicrophone();
            if (_visionCoroutine != null) { StopCoroutine(_visionCoroutine); _visionCoroutine = null; }

            Debug.LogError($"[GeminiLiveAgent] {error.Message}");
            if (logTextDisplay != null) logTextDisplay.text = "";
            AppendLog($"<color=red>System: Connection failed.</color>\n{error.Message}");
        }

        private void HandleReady()
        {
            Debug.Log("<color=green>Gemini Live Ready!</color>");
            AppendLog("<color=green>System: Connected.</color>");
            _isSessionReady = true;
            StartMicrophone();
            StartCoroutine(SendGreetingDelayed());
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
                // Force the initial tool call to kickstart Vertex's state machine.
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
            if (diff > 800) // ~50ms chunks at 16 kHz
            {
                float[] samples = new float[diff];
                _micClip.GetData(samples, _lastMicPos);

                float currentThreshold = _isPlaying ? echoInterruptionThreshold : voiceDetectionThreshold;

                bool isUserTalking = IsSpeechDetected(samples, currentThreshold);

                if (_isPlaying)
                {
                    if (isUserTalking && enableVocalInterruption)
                    {
                        Debug.Log($"<color=yellow>INTERRUPTING: Loud Speech Detected Over Agent</color>");
                        InterruptPlayback();
                        // Forward the interrupting samples to Gemini so it hears the new utterance.
                    }
                    else if (muteMicWhileTalking)
                    {
                        // Below the echo threshold while the agent is speaking — likely speaker
                        // bleed, so zero the buffer to keep Gemini from hearing itself.
                        Array.Clear(samples, 0, samples.Length);
                    }
                }

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
        private void InterruptPlayback() => InterruptPlayback(InterruptionSource.UserVoice, "voice", 0f);

        private void InterruptPlayback(InterruptionSource source, string reason, float magnitude)
        {
            if (agentAudioSource.isPlaying) agentAudioSource.Stop();
            _audioBuffer.Clear();
            _isPlaying = false;
            _ignoreAudioUntil = Time.time + interruptionDebounceTime;

            // Keep dropping inbound audio chunks until Gemini's server-side ack arrives (or the
            // safety timeout elapses). Prevents the prior turn from resuming after the debounce.
            _droppingAudioPostInterrupt = true;
            _postInterruptDropUntil = Time.time + postInterruptDropTimeoutSeconds;

            try
            {
                OnAgentInterrupted?.Invoke(new InterruptionInfo
                {
                    source = source,
                    reason = reason,
                    magnitude = magnitude,
                    timestampUtc = DateTime.UtcNow,
                    lastAgentFragment = _fullTranscript != null ? GetLastTranscriptFragment() : null,
                });
            }
            catch (Exception e) { Debug.LogWarning($"OnAgentInterrupted subscriber threw: {e.Message}"); }
        }

        private string GetLastTranscriptFragment()
        {
            if (_fullTranscript == null || _fullTranscript.Length == 0) return null;
            string s = _fullTranscript.ToString();
            const int Tail = 120;
            return s.Length > Tail ? s.Substring(s.Length - Tail) : s;
        }

        /// <summary>
        /// Public programmatic interrupt — call from UI buttons, custom logic, or tests.
        /// Behaves identically to a voice-detected interrupt.
        /// </summary>
        public void RequestInterrupt(string reason = "programmatic")
        {
            if (!_isPlaying) return;
            InterruptPlayback(InterruptionSource.Programmatic, reason, 0f);
        }

        private void HandleServerInterrupted()
        {
            // Authoritative interrupt signal from Gemini's server-side VAD. This fires regardless of
            // whether the client-side echo-threshold check tripped, so it covers the case where the
            // user spoke just below the local interrupt threshold but Gemini still heard them.
            if (_isPlaying || agentAudioSource.isPlaying)
            {
                Debug.Log("<color=yellow>INTERRUPTING (server VAD): stopping audio now.</color>");

                if (agentAudioSource.isPlaying) agentAudioSource.Stop();
                _audioBuffer.Clear();
                _isPlaying = false;
                _ignoreAudioUntil = Time.time + interruptionDebounceTime;

                try
                {
                    OnAgentInterrupted?.Invoke(new InterruptionInfo
                    {
                        source = InterruptionSource.UserVoice,
                        reason = "server_vad",
                        magnitude = 0f,
                        timestampUtc = DateTime.UtcNow,
                        lastAgentFragment = _fullTranscript != null ? GetLastTranscriptFragment() : null,
                    });
                }
                catch (Exception e) { Debug.LogWarning($"OnAgentInterrupted subscriber threw: {e.Message}"); }
            }
            else
            {
                Debug.Log("<color=green>Server confirmed interruption.</color>");
            }

            // Gemini has stopped generating for the prior turn. Subsequent audio chunks will be the
            // NEW turn (response to the user's interrupting query) — release the drop guard.
            _droppingAudioPostInterrupt = false;
        }

        private void HandleAudioReceived(byte[] pcmData)
        {
            if (Time.time < _ignoreAudioUntil) return;

            // Drop residual audio from the interrupted turn until Gemini acks server-side.
            if (_droppingAudioPostInterrupt)
            {
                if (Time.time >= _postInterruptDropUntil)
                {
                    Debug.LogWarning("Post-interrupt drop timed out without server ack — releasing.");
                    _droppingAudioPostInterrupt = false;
                }
                else
                {
                    return;
                }
            }

            int count = pcmData.Length / 2;
            for (int i = 0; i < count; i++) _audioBuffer.Add(BitConverter.ToInt16(pcmData, i * 2) / 32768.0f);
        }

        private void ProcessAudioPlayback()
        {
            if (Time.time < _ignoreAudioUntil) return;
            if (_droppingAudioPostInterrupt && Time.time < _postInterruptDropUntil) return;

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

        /// <summary>Captures a single frame from the configured camera and sends it to Gemini immediately.</summary>
        public void SendCurrentView() => StartCoroutine(CaptureAndSend());

        /// <summary>
        /// Turns the continuous vision stream on or off mid-session. Useful for toggling attention
        /// to the user's environment in response to conversation context.
        /// </summary>
        /// <param name="enable">True to start streaming frames, false to stop.</param>
        public void ToggleVisionStream(bool enable)
        {
            vision = enable;
            
            if (enable)
            {
                if (_visionCoroutine == null && _isSessionReady)
                {
                    Debug.Log("<color=cyan>Vision Stream toggled ON.</color>");
                    _visionCoroutine = StartCoroutine(AutoCaptureLoop());
                }
            }
            else
            {
                if (_visionCoroutine != null)
                {
                    Debug.Log("<color=cyan>Vision Stream toggled OFF.</color>");
                    StopCoroutine(_visionCoroutine);
                    _visionCoroutine = null;
                }
            }
        }
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
        /// <summary>
        /// Builds the Gemini Live system prompt from agent demographics, available non-verbal
        /// vocabulary, and runtime flags. Marked <c>protected virtual</c> so subclasses can
        /// prepend or append their own instructions without re-implementing <see cref="Connect"/>.
        /// </summary>
        protected virtual string BuildSystemPrompt()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Your name is {agentName}. You are a conversational embodied intelligent virtual agent. Your age is {age}. Your gender is {gender}. Your occupation is {occupation}. Additional information: {additionalDescription}.");
            sb.AppendLine("SYSTEM RULES:");
            sb.AppendLine("1. You control a 3D avatar. ONLY call 'update_avatar_state' if your emotional state or physical action (e.g. facial expression, gaze, body languages) needs to change based on the conversation.");            sb.AppendLine("2. If the user asks you to move (e.g., 'step back', 'come here'), call 'move_agent'.");
            sb.AppendLine("3. DO NOT call 'update_avatar_state' at the start of every turn. If your state hasn't changed, just speak.");
            sb.AppendLine("4. If the user's request requires using other available tools, you may call them.");
            sb.AppendLine("5. Do not pause your speech to narrate your tool calls. Call the necessary tools and deliver your spoken response normally.");

            if (_realtimeWrapper.affectiveAnalysis)
            {
                sb.AppendLine("5. Listen carefully to the user's voice and watch carefully within your vision. If they sound emotional, match your 'emotion' parameter to their sentiment (e.g., if they sound sad, set emotion to 'sad' or 'concerned').");
            }

            if (proactiveAnswerStyle)
            {
                sb.AppendLine("CONVERSATION STYLE:");
                sb.AppendLine("- Answer the user's question directly and substantively first, drawing on concrete details, names, dates, and examples you know.");
                sb.AppendLine("- Stay in character: speak in the first person about your own life, research, and work.");
                sb.AppendLine("- Do NOT end every reply with a clarifying question. Only ask a follow-up when the request is genuinely ambiguous, and never more than one in a row.");
                sb.AppendLine("- If background knowledge or a knowledge-base search gives you the answer, share it confidently instead of deflecting. If you genuinely do not know, say so briefly and move on.");
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
        /// <summary>Returns true once Gemini has acknowledged the setup frame and is ready to receive audio/video.</summary>
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

        public override void SetupVirtualAgent()
        {

            if (agentPrefab != null && agentInstance == null)
            {
                agentInstance = Instantiate(agentPrefab, transform.position, transform.rotation);
                agentInstance.name = agentName;
                agentInstance.transform.SetParent(transform);


                AssignAnimatorController();
                SetupLipSync();
                SetupAgentActionController();
                SetupEMotionHandler();
                SetupAgentVisionCamera();
                SetupSimpleEyeBlink();
                SetupEyeGazeController();
                SetupAudio();
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

        /// <summary>Identifies which speaker most recently wrote to the UI log so streamed transcript
        /// fragments from the same speaker concatenate onto a single line while a speaker change
        /// inserts a paragraph break.</summary>
        private enum LogSpeaker { None, User, Gemini }
        private LogSpeaker _lastSpeaker = LogSpeaker.None;

        private void HandleTextReceived(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            _fullTranscript.Append(text);
            Debug.Log($"<color=white>Gemini:</color> {text}");
            AppendTranscriptFragment(LogSpeaker.Gemini, "<color=cyan>Gemini:</color> ", text);
        }

        private void HandleUserTranscriptReceived(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            AppendTranscriptFragment(LogSpeaker.User, "<color=yellow>User:</color> ", text);
        }

        private void AppendTranscriptFragment(LogSpeaker speaker, string speakerPrefix, string fragment)
        {
            if (logTextDisplay == null) return;

            if (_lastSpeaker != speaker)
            {
                if (!string.IsNullOrEmpty(logTextDisplay.text)) logTextDisplay.text += "\n\n";
                logTextDisplay.text += speakerPrefix;
                _lastSpeaker = speaker;
            }

            logTextDisplay.text += fragment;
            RequestScrollDown();
        }

        /// <summary>Writes a system / status line to the in-game log panel and resets the speaker
        /// state so the next transcript fragment starts a fresh labelled paragraph. No-op when no
        /// <see cref="logTextDisplay"/> is wired.</summary>
        public void AppendLog(string message)
        {
            _lastSpeaker = LogSpeaker.None;
            if (logTextDisplay == null) return;
            logTextDisplay.text += string.IsNullOrEmpty(logTextDisplay.text) ? message : $"\n\n{message}";
            RequestScrollDown();
        }

        private void RequestScrollDown()
        {
            if (scrollRect == null) return;
            StopCoroutine(nameof(ForceScrollDown));
            StartCoroutine(nameof(ForceScrollDown));
        }

        private IEnumerator ForceScrollDown()
        {
            yield return new WaitForEndOfFrame();
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;
            yield return new WaitForEndOfFrame();
            scrollRect.verticalNormalizedPosition = 0f;
        }

    }
}