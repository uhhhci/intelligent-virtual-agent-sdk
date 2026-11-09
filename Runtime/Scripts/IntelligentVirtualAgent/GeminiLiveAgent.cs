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
    public class GeminiLiveAgent : AgentBase, IGeminiAgent, ILiveAgentControls
    {
        string ILiveAgentControls.MicrophoneDeviceName { get => microphoneDeviceName; set => microphoneDeviceName = value; }
        bool ILiveAgentControls.EnableVocalInterruption { get => enableVocalInterruption; set => enableVocalInterruption = value; }
        bool ILiveAgentControls.MuteMicWhileTalking { get => muteMicWhileTalking; set => muteMicWhileTalking = value; }
        float ILiveAgentControls.VisionUpdateFrequency { get => visionUpdateFrequency; set => visionUpdateFrequency = value; }

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

        /// <summary>
        /// Keeps spoken replies short: at most <see cref="maxSentencesPerReply"/> sentences with one
        /// concrete point, unless the user explicitly asks for more. Spoken monologues are hard to follow
        /// and, with the microphone muted while the agent talks, hard to stop.
        /// </summary>
        [Tooltip("Keep spoken replies short (at most Max Sentences Per Reply) unless the user explicitly asks for more detail.")]
        public bool conciseReplies = true;

        /// <summary>Upper bound on sentences per reply when <see cref="conciseReplies"/> is on.</summary>
        [Tooltip("Maximum number of short sentences per reply when Concise Replies is on.")]
        [Range(1, 8)] public int maxSentencesPerReply = 3;

        /// <summary>
        /// Always answer in the language the user last spoke, switching immediately when they switch
        /// (e.g. from English to German in the same session). Gemini Live's native-audio models detect
        /// the spoken language themselves; this rule stops them from sticking to the first language.
        /// </summary>
        [Tooltip("Answer in the language the user last spoke and switch immediately when they switch languages mid-session.")]
        public bool mirrorUserLanguage = true;

        /// <summary>
        /// Tells the model that its native-audio voice can whisper, sing, sound sad or excited, speak like
        /// a robot, etc., and that it should do so when asked instead of declining.
        /// </summary>
        [Tooltip("Let the agent change its speaking style on request: whisper, sing a few lines, sound sad/happy/excited, speak like a robot, use an accent.")]
        public bool allowExpressiveSpeech = true;

        [Header("Embodiment Self-Description")]
        /// <summary>
        /// Appends an accurate description of the avatar's abilities to the system prompt: the facial
        /// expressions and body motions it can really perform (read from the animator controller), gaze,
        /// lip sync, locomotion, vision and hard limits, plus guidance on how to explain them. Prevents
        /// the agent from promising gestures it cannot do, which surprises and frustrates users.
        /// </summary>
        [Tooltip("Tell the agent precisely which facial expressions and body motions its avatar can perform, and what it cannot do, so it describes itself truthfully and declines impossible requests.")]
        public bool describeEmbodimentCapabilities = false;

        /// <summary>When <see cref="describeEmbodimentCapabilities"/> is on, the first greeting invites the user to ask about the avatar's abilities.</summary>
        [Tooltip("In its first greeting, the agent briefly invites the user to ask what it can and cannot do.")]
        public bool mentionCapabilitiesInGreeting = true;

        /// <summary>Persona-specific facts about the avatar appended to the capability description (e.g. how the likeness was made).</summary>
        [Tooltip("Extra facts about the avatar the agent may share, e.g. how its 3D likeness was created.")]
        [TextArea(2, 6)] public string embodimentNotes = "";

        /// <summary>
        /// Only offer body motions and facial clips whose trigger reaches a state with a clip in the
        /// animator controller. Action classes the controller does not implement are hidden from the model.
        /// </summary>
        [Tooltip("Only offer the model animations the animator controller can actually play.")]
        public bool onlyOfferPlayableAnimations = true;

        /// <summary>Animation triggers never offered to the model (setup or debug clips).</summary>
        [Tooltip("Animation triggers never offered to the model, e.g. calibration or test clips.")]
        public List<string> hiddenAnimationTriggers = new List<string> { "MotionCalibration", "Test" };

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

        /// <summary>
        /// What the camera shows, in the agent's own words, e.g. "a webcam on your display that looks at
        /// the people standing in front of you". Empty uses a default for the camera type.
        /// </summary>
        [Tooltip("What your camera shows, told to the agent, e.g. 'a webcam on your display that looks at the people standing in front of you'. Empty = default wording.")]
        public string visionSourceDescription = "";

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

        /// <summary>Speaker label for the agent's lines in the transcript. Empty = <see cref="AgentBase.agentName"/>.</summary>
        [Tooltip("Label for the agent's lines in the transcript. Empty = the agent's name.")]
        public string agentTranscriptLabel = "";

        /// <summary>Speaker label for the user's lines in the transcript.</summary>
        [Tooltip("Label for the user's lines in the transcript.")]
        public string userTranscriptLabel = "User";

        private Coroutine _visionCoroutine;
        private Coroutine _visionStartRoutine;
        private Coroutine _greetingRoutine;
        /// <summary>Bumped by every <see cref="Connect"/>; an older Connect that is still awaiting stops.</summary>
        private int _connectRequest;
        /// <summary>Microphone device that is actually recording (the selected name may change in between).</summary>
        private string _recordingDevice;
        /// <summary>The last spoken lines of both sides, used to brief a new session after a lost connection.</summary>
        private readonly StringBuilder _recentConversation = new StringBuilder();
        private const int RecentConversationChars = 1500;
        private GeminiRealtimeWrapper _realtimeWrapper;

        /// <summary>The hybrid agent's local voice, when a <see cref="IVH.Core.ServiceConnector.LocalVoice.LocalVoiceSpeaker"/> is enabled on this object: Gemini's audio is then not played.</summary>
        private IVH.Core.ServiceConnector.LocalVoice.LocalVoiceSpeaker _localVoice;
        private bool UsesLocalVoice => _localVoice != null && _localVoice.isActiveAndEnabled;
        /// <summary>The agent is talking: Gemini's clip playing, or the local voice's buffer.</summary>
        private bool AgentSpeaking => UsesLocalVoice ? _localVoice.IsSpeaking : _isPlaying;
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

            _localVoice = GetComponent<IVH.Core.ServiceConnector.LocalVoice.LocalVoiceSpeaker>();
            if (_localVoice != null && _localVoice.enabled && agentInstance != null)
                _localVoice.Attach(_realtimeWrapper, agentInstance, agentAudioSource != null ? agentAudioSource : agentInstance.GetComponent<AudioSource>());

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
            _realtimeWrapper.OnReconnecting += HandleReconnecting;

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
            StopRoutine(ref _visionCoroutine);
            StopRoutine(ref _visionStartRoutine);
            StopRoutine(ref _greetingRoutine);
            StopMicrophone();
            if (_realtimeWrapper != null)
            {
                _realtimeWrapper.OnFatalError -= HandleFatalError;
                _realtimeWrapper.OnReconnecting -= HandleReconnecting;
                _ = _realtimeWrapper.DisconnectAsync();
            }
        }

        private void Update()
        {
            if (_isSessionReady && _realtimeWrapper.IsConnected) ProcessMicrophone();
            ProcessAudioPlayback();
        }

        private void StopRoutine(ref Coroutine routine)
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
        }

        /// <summary>
        /// Opens a new Gemini Live session using the current configuration (the Reconnect button).
        /// Stops the agent at once: its current reply is cut off, audio already received is discarded,
        /// and nothing of the old session reaches the avatar any more. Then builds the system prompt,
        /// registers any dynamic tools, and starts microphone + vision streams once the session is ready.
        /// </summary>
        public async void Connect()
        {
            int request = ++_connectRequest;
            try
            {
                StopForNewSession();
                if (logTextDisplay != null) logTextDisplay.text = "<i>System: Connecting...</i>";
                _lastSpeaker = LogSpeaker.None;
                _fullTranscript.Clear();
                _recentConversation.Clear();

                // A different microphone picked in the HUD takes effect now.
                if (_isRecording && _recordingDevice != microphoneDeviceName) StopMicrophone();

                // Close the previous session before anything else, so it cannot finish its sentence.
                await _realtimeWrapper.DisconnectAsync();
                if (request != _connectRequest) return;   // a newer Connect took over

                string noThinkingPrompt = " STRICT RULE: Do not output internal thoughts, markdown, or verbose planning text. Output direct speech text only.";
                string dynamicPrompt = BuildSystemPrompt();

                // Realtime session: retrieve grounding context once at connection time, since Gemini
                // Live takes the system instruction as a one-shot at setup. Empty when no
                // IContextProvider is attached, matching v3.0 behavior.
                string knowledgePrefix = await BuildContextPrefixAsync(null);
                if (request != _connectRequest) return;

                string finalPrompt = knowledgePrefix + dynamicPrompt + noThinkingPrompt;
                var toolManager = GetComponent<GeminiToolManager>();
                if (toolManager != null && toolManager.definedTools.Count > 0)
                {
                    await _realtimeWrapper.ConnectWithDynamicToolsAsync(finalPrompt, voiceName, toolManager.GetDynamicToolDeclarations());
                }
                else
                {
                    bool locomotion = SupportsLocomotion(characterType) && enableLocomotion;
                    await _realtimeWrapper.ConnectAsync(finalPrompt, voiceName, locomotion);
                }
            }
            catch (Exception e)
            {
                if (request != _connectRequest) return;
                Debug.LogError($"[GeminiLiveAgent] Connect failed: {e}");
                AppendLog($"<color=red>System: Connection failed.</color>\n{e.Message}");
            }
        }

        /// <summary>
        /// Silences the agent and forgets everything of the current session: playback, queued audio,
        /// pending greeting and vision routines, interruption guards.
        /// </summary>
        private void StopForNewSession()
        {
            _isSessionReady = false;
            _handshakeComplete = false;
            StopRoutine(ref _greetingRoutine);
            StopRoutine(ref _visionStartRoutine);
            StopRoutine(ref _visionCoroutine);

            if (UsesLocalVoice) _localVoice.Interrupt();
            if (agentAudioSource != null && agentAudioSource.isPlaying) agentAudioSource.Stop();
            _audioBuffer.Clear();
            _isPlaying = false;
            _droppingAudioPostInterrupt = false;
            _ignoreAudioUntil = 0f;
        }

        private IEnumerator WaitForGreetingToFinishAndStartVision()
        {
            float timeout = 10f;
            while (timeout > 0)
            {
                if (AgentSpeaking) break;
                timeout -= 0.1f;
                yield return new WaitForSeconds(0.1f);
            }
            while (AgentSpeaking)
            {
                yield return new WaitForSeconds(0.2f);
            }

            Debug.Log("<color=cyan>Greeting Finished. Vision Stream ACTIVE.</color>");

            _handshakeComplete = true;
            _visionStartRoutine = null;
            if (vision && _visionCoroutine == null) _visionCoroutine = StartCoroutine(AutoCaptureLoop());
        }

        /// <summary>
        /// Surfaces an unrecoverable session error in the HUD and the Console, and tears down the
        /// mic/vision streams so the scene does not keep capturing into a dead session.
        /// </summary>
        private void HandleFatalError(IVAException error)
        {
            _isSessionReady = false;
            StopMicrophone();
            StopRoutine(ref _visionCoroutine);
            StopRoutine(ref _visionStartRoutine);
            StopRoutine(ref _greetingRoutine);

            Debug.LogError($"[GeminiLiveAgent] {error.Message}");
            if (logTextDisplay != null) logTextDisplay.text = "";
            AppendLog($"<color=red>System: Connection failed.</color>\n{error.Message}");
        }

        private void HandleReconnecting(int attempt)
        {
            Debug.LogWarning($"[GeminiLiveAgent] Connection lost; reconnect attempt {attempt}.");
            if (attempt == 1) AppendLog("<color=#8A93A4><i>System: Connection interrupted, reconnecting...</i></color>");
        }

        private void HandleReady()
        {
            bool resumed = _realtimeWrapper.IsResumedSession;
            bool reconnected = _realtimeWrapper.IsAutomaticReconnect;
            string where = string.IsNullOrEmpty(_realtimeWrapper.ActiveVertexLocation) ? "" : $" ({_realtimeWrapper.ActiveVertexLocation})";

            _isSessionReady = true;
            StartMicrophone();
            SkipBufferedMicrophoneAudio();

            StopRoutine(ref _greetingRoutine);
            if (resumed)
            {
                // Same conversation on a new connection: no greeting, nothing to show.
                Debug.Log($"<color=green>Gemini Live session resumed{where}.</color>");
            }
            else if (reconnected)
            {
                Debug.Log($"<color=green>Gemini Live reconnected with a new session{where}.</color>");
                _greetingRoutine = StartCoroutine(SendReconnectBriefingDelayed());
            }
            else
            {
                Debug.Log($"<color=green>Gemini Live Ready!{where}</color>");
                AppendLog("<color=green>System: Connected.</color>");
                _greetingRoutine = StartCoroutine(SendGreetingDelayed());
            }

            if (vision)
            {
                EnsureWebCamForVision();
                StopRoutine(ref _visionStartRoutine);
                StopRoutine(ref _visionCoroutine);

                if (!resumed && !reconnected && _realtimeWrapper.selectedModel == GeminiModelType.Flash25VertexAI)
                {
                    _visionStartRoutine = StartCoroutine(WaitForGreetingToFinishAndStartVision());
                }
                else
                {
                    _visionCoroutine = StartCoroutine(AutoCaptureLoop());
                }
            }
        }

        /// <summary>
        /// After a lost connection could not be resumed, the new session knows nothing of the
        /// conversation: brief it with the last lines and let the agent apologise in one sentence
        /// instead of greeting again.
        /// </summary>
        private IEnumerator SendReconnectBriefingDelayed()
        {
            yield return new WaitForSeconds(0.5f);
            string recent = _recentConversation.ToString().Trim();
            _realtimeWrapper.SendTextMessage(
                "System: The connection to the language model was lost for a moment and has been restored as a new session. " +
                (recent.Length > 0 ? "The conversation so far ended like this:\n" + recent + "\n" : "") +
                "Do not greet again and do not introduce yourself again. In the language of the conversation, say one short sentence " +
                "that you were briefly disconnected, and invite the person to continue.");
            _greetingRoutine = null;
        }

        private IEnumerator SendGreetingDelayed()
        {
            yield return new WaitForSeconds(1.0f);
            _greetingRoutine = null;

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
            // Keeps running through a reconnect; frames are only sent while a session is ready.
            while (vision && _isSessionReady)
            {
                if (_realtimeWrapper.IsSessionReady) yield return CaptureAndSend();

                yield return new WaitForSeconds(visionUpdateFrequency);
            }
            _visionCoroutine = null;
        }

        /// <summary>Starts the webcam when vision uses it (it is only started in Awake when vision was on from the beginning).</summary>
        private void EnsureWebCamForVision()
        {
            if (targetCameraType != TargetCameraType.WebCam) return;
            if (EnsureWebCamReady() == null)
                AppendLog("<color=#E0A858>System: Vision is on, but no webcam was found.</color>");
        }

        private void StartMicrophone()
        {
            if (_isRecording) return;

            string[] devices = Microphone.devices;
            if (devices.Length == 0)
            {
                Debug.LogError("[GeminiLiveAgent] No microphone found.");
                AppendLog("<color=#E0A858>System: No microphone found.</color>");
                return;
            }
            if (string.IsNullOrEmpty(microphoneDeviceName) || Array.IndexOf(devices, microphoneDeviceName) < 0)
            {
                // A configured device that is unplugged (e.g. a wireless receiver) would otherwise record
                // nothing; Unity's Microphone.Start then never advances and the old wait loop hung the app.
                if (!string.IsNullOrEmpty(microphoneDeviceName))
                {
                    Debug.LogWarning($"[GeminiLiveAgent] Microphone '{microphoneDeviceName}' not found; using '{devices[0]}'.");
                    AppendLog($"<color=#E0A858>System: Microphone '{microphoneDeviceName}' not found, using '{devices[0]}'.</color>");
                }
                microphoneDeviceName = devices[0];
            }

            _micClip = Microphone.Start(microphoneDeviceName, true, 3599, 16000);
            if (_micClip == null)
            {
                Debug.LogError($"[GeminiLiveAgent] Could not start microphone '{microphoneDeviceName}'.");
                AppendLog($"<color=#E0A858>System: Could not start microphone '{microphoneDeviceName}'.</color>");
                return;
            }

            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (Microphone.GetPosition(microphoneDeviceName) <= 0 && clock.ElapsedMilliseconds < 2000) { }

            _recordingDevice = microphoneDeviceName;
            _lastMicPos = Mathf.Max(0, Microphone.GetPosition(_recordingDevice));
            _isRecording = true;
            Debug.Log($"Mic Started: {_recordingDevice}");
        }

        private void StopMicrophone()
        {
            if (_isRecording) { Microphone.End(_recordingDevice); _isRecording = false; }
        }

        /// <summary>Drops microphone audio recorded while no session was ready, so it is not sent as one stale burst.</summary>
        private void SkipBufferedMicrophoneAudio()
        {
            if (_isRecording) _lastMicPos = Mathf.Max(0, Microphone.GetPosition(_recordingDevice));
        }

        private void ProcessMicrophone()
        {
            if (!_isRecording || _micClip == null) return;

            int currentPos = Microphone.GetPosition(_recordingDevice);
            if (currentPos < _lastMicPos) { _lastMicPos = 0; return; }
            
            int diff = currentPos - _lastMicPos;
            if (diff > 800) // ~50ms chunks at 16 kHz
            {
                float[] samples = new float[diff];
                _micClip.GetData(samples, _lastMicPos);

                float currentThreshold = AgentSpeaking ? echoInterruptionThreshold : voiceDetectionThreshold;

                bool isUserTalking = IsSpeechDetected(samples, currentThreshold);

                if (AgentSpeaking)
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
            if (UsesLocalVoice) _localVoice.Interrupt();
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
            if (!AgentSpeaking) return;
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
            // The hybrid speaks Gemini's words in the local voice; Gemini's own audio is not played.
            if (UsesLocalVoice) return;
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
            if (UsesLocalVoice) return;
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
                EnsureWebCamForVision();
                if (_visionCoroutine == null && _visionStartRoutine == null && _isSessionReady)
                {
                    Debug.Log("<color=cyan>Vision Stream toggled ON.</color>");
                    _visionCoroutine = StartCoroutine(AutoCaptureLoop());
                }
            }
            else
            {
                if (_visionCoroutine != null || _visionStartRoutine != null)
                    Debug.Log("<color=cyan>Vision Stream toggled OFF.</color>");
                StopRoutine(ref _visionCoroutine);
                StopRoutine(ref _visionStartRoutine);
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
            sb.AppendLine("1. You control a 3D avatar. ONLY call 'update_avatar_state' if your emotional state or physical action (e.g. facial expression, gaze, body languages) needs to change based on the conversation.");
            sb.AppendLine("2. If the user asks you to move (e.g., 'step back', 'come here'), call 'move_agent'.");
            sb.AppendLine("3. DO NOT call 'update_avatar_state' at the start of every turn. If your state hasn't changed, just speak.");
            sb.AppendLine("4. If the user's request requires using other available tools, you may call them.");
            sb.AppendLine("5. Do not pause your speech to narrate your tool calls. Call the necessary tools and deliver your spoken response normally.");

            if (_realtimeWrapper.affectiveAnalysis)
            {
                sb.AppendLine("6. Listen carefully to the user's voice and watch carefully within your vision. If they sound emotional, match your 'emotion' parameter to their sentiment (e.g., if they sound sad, set emotion to 'sad' or 'concerned').");
            }

            if (proactiveAnswerStyle)
            {
                sb.AppendLine("CONVERSATION STYLE:");
                sb.AppendLine(conciseReplies
                    ? "- Answer the user's question directly in your first sentence, with one concrete detail (a name, a date or an example) rather than a list."
                    : "- Answer the user's question directly and substantively first, drawing on concrete details, names, dates, and examples you know.");
                sb.AppendLine("- Stay in character: speak in the first person about your own life, research, and work.");
                sb.AppendLine("- Do NOT end every reply with a clarifying question. Only ask a follow-up when the request is genuinely ambiguous, and never more than one in a row.");
                sb.AppendLine("- If background knowledge or a knowledge-base search gives you the answer, share it confidently instead of deflecting. If you genuinely do not know, say so briefly and move on.");
            }

            if (conciseReplies)
            {
                sb.AppendLine("REPLY LENGTH (important, this is spoken conversation):");
                sb.AppendLine($"- Keep every reply short: at most {maxSentencesPerReply} short sentence{(maxSentencesPerReply == 1 ? "" : "s")}, about {maxSentencesPerReply * 15} words, then stop and let the other person speak.");
                sb.AppendLine("- Make one point per reply. No lists, no long introductions, no summaries of what you just said.");
                sb.AppendLine("- Go longer only when someone explicitly asks for details, a story, an explanation or a song, and even then stay under about 30 seconds of speech.");
            }

            if (mirrorUserLanguage)
            {
                sb.AppendLine("LANGUAGE:");
                sb.AppendLine("- Always reply in the language the person most recently spoke to you. When someone switches language mid-conversation (for example from English to German), switch with them immediately and stay in the new language until they switch again. Never ask whether you may switch.");
                sb.AppendLine("- Do not switch to a language nobody has spoken to you. Use the polite form of address that fits the language (for example 'Sie' in German) unless you are invited to be informal.");
            }

            if (allowExpressiveSpeech && SpeaksWithGeminiVoice())
            {
                sb.AppendLine("VOICE AND DELIVERY:");
                sb.AppendLine("- You speak with a native-audio voice. Its timbre stays the same, but you can change how you speak whenever someone asks: whisper, speak softly or loudly, faster or slower, sound happy, sad, excited, nervous, bored or angry, speak like a robot, use an accent, or sing a few lines (an improvised tune or a traditional song).");
                sb.AppendLine("- When asked, do it right away and really sound that way, without explaining that you are an AI and without asking for confirmation. Keep it short (a few sentences or a few lines of a song), then return to your normal voice unless asked to keep it.");
            }

            if (vision)
            {
                string source = !string.IsNullOrWhiteSpace(visionSourceDescription)
                    ? visionSourceDescription.Trim()
                    : targetCameraType == TargetCameraType.WebCam
                        ? "a camera that looks at the person you are talking to"
                        : "a virtual camera at your eyes inside the 3D scene";
                sb.AppendLine("VISION:");
                sb.AppendLine($"- Your camera is ON: you receive still images from {source}, about one every {visionUpdateFrequency:0.#} s. Use them to make the conversation feel present.");
                sb.AppendLine("- Notice when someone steps up, waves, smiles or holds something up, and refer to visible things briefly when they fit (for example what someone is holding or pointing at). When asked what you see, describe the latest image in one or two sentences.");
                sb.AppendLine("- Do not describe every image and do not mention the camera unprompted more than once in a while.");
                sb.AppendLine("- Only describe what is really in the images you have received. If no image has arrived yet, or the latest one is dark or unclear, say that you cannot see well right now; never invent what you see.");
                sb.AppendLine("- Never guess who someone is from their face, and never claim to recognise anyone. Do not comment on bodies, age, ethnicity, health or other sensitive traits.");
            }

            HashSet<string> playable = onlyOfferPlayableAnimations ? GetPlayableAnimationTriggers() : null;

            if (actionController != null)
            {
                // Type names: that is what PerformAction matches (a few classes carry a copy-pasted Name).
                var actions = GetOfferedBodyActions(playable).Select(a => a.GetType().Name).ToList();
                sb.AppendLine("Allowed 'action' values: " + string.Join(", ", CleanList(actions)));
            }
            if (faceAnimator != null || emotionHandler != null)
            {
                var emotions = GetOfferedFacialExpressions(playable).Select(kv => kv.Key).ToList();
                sb.AppendLine("Allowed 'emotion' values: " + string.Join(", ", CleanList(emotions)));
            }
            if (eyeGazeController != null)
            {
                sb.AppendLine("Allowed 'gaze' values: 'User', 'Idle'");
            }

            if (describeEmbodimentCapabilities)
            {
                sb.AppendLine();
                sb.Append(AgentCapabilityDescriber.BuildPrompt(BuildEmbodimentCapabilities(playable)));
            }

            return sb.ToString();
        }

        /// <summary>Body actions offered to the model: filtered by sentiment/gender/source, playable triggers and <see cref="hiddenAnimationTriggers"/>.</summary>
        private List<IVH.Core.Actions.AgentAction> GetOfferedBodyActions(HashSet<string> playable)
        {
            if (actionController == null) return new List<IVH.Core.Actions.AgentAction>();
            return actionController.GetActionsFiltered(bodyActionFilter, gender, bodyAnimationControllerType, playable, hiddenAnimationTriggers);
        }

        /// <summary>Facial expressions offered to the model as (name, description).</summary>
        private List<KeyValuePair<string, string>> GetOfferedFacialExpressions(HashSet<string> playable)
        {
            var result = new List<KeyValuePair<string, string>>();
            if (emotionHandlerType == EmotionHandlerType.CC4_Animation)
            {
                if (faceAnimator == null) return result;
                foreach (var clip in faceAnimator.GetFacialAnimationsFiltered(facialExpressionFilter, playable))
                {
                    if (hiddenAnimationTriggers != null && hiddenAnimationTriggers.Contains(clip.GetType().Name)) continue;
                    // Clip descriptions in AgentFacialAnimationTypes mention arm/body movement the face-only
                    // clips do not perform, so only the name is offered.
                    result.Add(new KeyValuePair<string, string>(clip.GetType().Name, null));
                }
            }
            else
            {
                foreach (string emotion in GetFacsEmotionNames())
                    result.Add(new KeyValuePair<string, string>(emotion, EmotionHandler.DescribeEmotion(emotion)));
            }
            return result;
        }

        /// <summary>Collects what the avatar can actually do right now (see <see cref="AgentCapabilityDescriber"/>).</summary>
        public EmbodimentCapabilities BuildEmbodimentCapabilities(HashSet<string> playable = null)
        {
            if (playable == null && onlyOfferPlayableAnimations) playable = GetPlayableAnimationTriggers();

            var caps = new EmbodimentCapabilities
            {
                characterType = characterType,
                emotionHandlerType = emotionHandlerType,
                bodyAnimationType = bodyAnimationControllerType,
                facialExpressionsRelax = characterType == CharacterType.AvatarSDK && emotionHandlerType == EmotionHandlerType.FACS,
                hasLipSync = agentInstance != null && agentInstance.GetComponent<OVRLipSyncContextBase>() != null,
                hasBlinking = agentInstance != null && agentInstance.GetComponent<IVH.Core.Actions.CharacterBlinkBehavior>() != null,
                hasGaze = eyeGazeController != null,
                canWalk = enableLocomotion && agentLocomotion != null && SupportsLocomotion(characterType),
                visionEnabled = vision,
                visionSource = targetCameraType,
                visionIntervalSeconds = visionUpdateFrequency,
                visionSourceDescription = visionSourceDescription,
                canVaryVoiceStyle = SpeaksWithGeminiVoice() && allowExpressiveSpeech,
                userCanInterruptByVoice = enableVocalInterruption || !muteMicWhileTalking,
                additionalNotes = embodimentNotes,
                mentionInGreeting = mentionCapabilitiesInGreeting,
            };
            foreach (var action in GetOfferedBodyActions(playable))
                caps.bodyMotions.Add(new KeyValuePair<string, string>(action.GetType().Name, action.Description));
            caps.facialExpressions.AddRange(GetOfferedFacialExpressions(playable));
            return caps;
        }

        /// <summary>True when the agent speaks with Gemini's own native-audio voice (not a local TTS voice or text output).</summary>
        private bool SpeaksWithGeminiVoice()
        {
            var wrapper = _realtimeWrapper != null ? _realtimeWrapper : GetComponent<GeminiRealtimeWrapper>();
            var localVoice = _localVoice != null ? _localVoice : GetComponent<IVH.Core.ServiceConnector.LocalVoice.LocalVoiceSpeaker>();
            bool local = localVoice != null && localVoice.enabled;
            return wrapper != null && wrapper.responseModality == GeminiResponseModality.Audio && !local;
        }

        /// <summary>The capability section that <see cref="describeEmbodimentCapabilities"/> adds to the prompt. Useful for previews.</summary>
        public string GetEmbodimentDescription()
        {
            return AgentCapabilityDescriber.BuildPrompt(BuildEmbodimentCapabilities());
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

        /// <summary>Character rigs with a humanoid skeleton that the NavMesh locomotion clips support.</summary>
        public static bool SupportsLocomotion(CharacterType type)
        {
            return type == CharacterType.CC4OrDIDIMO || type == CharacterType.AvatarSDK;
        }

        public override void SetupVirtualAgent()
        {

            if (agentPrefab != null && agentInstance == null)
            {
                EnsureCharacterTypeCompatibility();
                agentInstance = Instantiate(agentPrefab, transform.position, transform.rotation);
                agentInstance.name = agentName;
                agentInstance.transform.SetParent(transform);


                AssignAnimatorController();
                SetupLipSync();
                SetupAgentActionController();
                SetupEMotionHandler();
                SetupAgentVisionCamera();
                SetupSimpleEyeBlink();
                SetupFacialRetargeting();
                SetupEyeGazeController();
                SetupAudio();
                if (SupportsLocomotion(characterType))
                {
                    SetupAgentLocomotion();
                }
                RefreshPlayableAnimationCache();
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

        private string AgentLabel => !string.IsNullOrWhiteSpace(agentTranscriptLabel) ? agentTranscriptLabel.Trim()
                                   : !string.IsNullOrWhiteSpace(agentName) ? agentName : "Agent";
        private string UserLabel => !string.IsNullOrWhiteSpace(userTranscriptLabel) ? userTranscriptLabel.Trim() : "User";

        private void HandleTextReceived(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            _fullTranscript.Append(text);
            Debug.Log($"<color=white>{AgentLabel}:</color> {text}");
            RememberRecent(LogSpeaker.Gemini, text);
            AppendTranscriptFragment(LogSpeaker.Gemini, $"<color=cyan>{AgentLabel}:</color> ", text);
        }

        private void HandleUserTranscriptReceived(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            RememberRecent(LogSpeaker.User, text);
            AppendTranscriptFragment(LogSpeaker.User, $"<color=yellow>{UserLabel}:</color> ", text);
        }

        private LogSpeaker _lastRecentSpeaker = LogSpeaker.None;

        /// <summary>Keeps the last spoken lines of both sides (plain text) for a reconnect briefing.</summary>
        private void RememberRecent(LogSpeaker speaker, string fragment)
        {
            if (speaker != _lastRecentSpeaker)
            {
                if (_recentConversation.Length > 0) _recentConversation.Append('\n');
                _recentConversation.Append(speaker == LogSpeaker.User ? "User: " : "You: ");
                _lastRecentSpeaker = speaker;
            }
            _recentConversation.Append(fragment);
            if (_recentConversation.Length > RecentConversationChars)
                _recentConversation.Remove(0, _recentConversation.Length - RecentConversationChars);
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
            TrimLog();
            RequestScrollDown();
        }

        /// <summary>
        /// A UI Text holds at most 65k vertices (about 16k visible characters); a long demo session would
        /// exceed that and stop rendering. Keeps the most recent part of the log.
        /// </summary>
        private void TrimLog()
        {
            const int MaxChars = 12000;
            string t = logTextDisplay.text;
            if (t.Length <= MaxChars) return;
            int cut = t.IndexOf("\n\n", t.Length - MaxChars + 2000, StringComparison.Ordinal);
            if (cut < 0) cut = t.Length - MaxChars;
            logTextDisplay.text = t.Substring(cut).TrimStart('\n');
        }

        /// <summary>Writes a system / status line to the in-game log panel and resets the speaker
        /// state so the next transcript fragment starts a fresh labelled paragraph. No-op when no
        /// <see cref="logTextDisplay"/> is wired.</summary>
        public void AppendLog(string message)
        {
            _lastSpeaker = LogSpeaker.None;
            if (logTextDisplay == null) return;
            logTextDisplay.text += string.IsNullOrEmpty(logTextDisplay.text) ? message : $"\n\n{message}";
            TrimLog();
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