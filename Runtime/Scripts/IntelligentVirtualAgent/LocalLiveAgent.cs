using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using IVH.Core.Exceptions;
using IVH.Core.ServiceConnector.Gemini.Realtime;
using IVH.Core.ServiceConnector.LocalVoice;
using UnityEngine;
using UnityEngine.UI;

namespace IVH.Core.IntelligentVirtualAgent
{
    /// <summary>
    /// Full-body embodied agent whose whole conversation runs on this machine: a <b>Local Voice Server</b>
    /// recognises the user's speech, writes the reply with a local language model and speaks it in a stock
    /// or <b>cloned</b> voice. The avatar side is the SDK's own: the same lip sync, facial expressions, body
    /// actions and gaze as <see cref="GeminiLiveAgent"/>, for CC4 and Avatar SDK (MetaPerson) characters.
    /// </summary>
    /// <remarks>
    /// <para>Swapping a <see cref="GeminiLiveAgent"/> scene to local: replace the agent and its wrapper with
    /// this component and a <see cref="LocalRealtimeWrapper"/>; the inspector fields have the same names, so
    /// the samples' scene builder copies them across (<c>Tools ▸ IVA Samples ▸ Local Agent</c>). Start the
    /// server first (<c>.\run.ps1 -Preset clone</c> in the LocalVoiceServer repository).</para>
    /// <para>What differs from Gemini, and why:</para>
    /// <list type="bullet">
    /// <item>Playback is one ring buffer read on the audio thread (<see cref="LocalVoicePlayback"/>), keyed by
    /// the server's response id, so a cancelled reply stops at once and needs no timeout or debounce.</item>
    /// <item>No vision: the server has no image model; <see cref="AgentBase.vision"/> is ignored.</item>
    /// <item>Grounding: attached <c>IContextProvider</c>s (e.g. <c>FullDocumentContextProvider</c>) are
    /// prepended to the system prompt once per session, as Gemini Live does. The server's small model has a
    /// limited context, so <see cref="maxContextChars"/> caps it.</item>
    /// </list>
    /// </remarks>
    [RequireComponent(typeof(LocalRealtimeWrapper))]
    public class LocalLiveAgent : AgentBase, ILiveAgentControls
    {
        string ILiveAgentControls.MicrophoneDeviceName { get => microphoneDeviceName; set => microphoneDeviceName = value; }
        bool ILiveAgentControls.EnableVocalInterruption { get => enableVocalInterruption; set => enableVocalInterruption = value; }
        bool ILiveAgentControls.MuteMicWhileTalking { get => muteMicWhileTalking; set => muteMicWhileTalking = value; }
        float ILiveAgentControls.VisionUpdateFrequency { get => 0f; set { } }

        /// <summary>No vision on a local agent (the server has no image model); logged, never silent.</summary>
        public void ToggleVisionStream(bool enable)
        {
            if (enable) AppendLog("<color=#E0A858>System: The local agent has no vision (the local server has no image model).</color>");
            vision = false;
        }

        [Header("Local Voice")]
        /// <summary>Server voice id: stock (<c>kristin</c>) or cloned (<c>v-…</c>). A Gemini name falls back to the wrapper's voice id.</summary>
        [Tooltip("Server voice id: a stock voice (kristin) or a cloned one (v-...). See GET /v1/voices.")]
        public string voiceName = "kristin";

        public bool autoConnectOnStart = true;

        [Tooltip("Ask the agent to greet the user when the session starts.")]
        public bool greetOnConnect = true;

        [Tooltip("Answer directly and substantively in first person; do not end every reply with a question.")]
        public bool proactiveAnswerStyle = true;

        [Tooltip("Ask for short spoken replies (one to three sentences). Small local models ramble otherwise.")]
        public bool shortReplies = true;

        /// <remarks>
        /// Measured 2026-11-08 (Qwen3-1.7B, Frank's persona, 8 turns): asked to call <c>update_avatar_state</c>
        /// as a function, the model chose a state in 4 turns and spoke in only 1 (a turn with a call had no
        /// words); asked to open each reply with a tag, it chose a fitting state in 7 and spoke in all 8. The
        /// server removes the tag from speech and text and sends it as the same tool call (PROTOCOL v1.6).
        /// </remarks>
        [Tooltip("Expressions, gestures and gaze come from a [emotion=...; action=...; gaze=...] tag the model writes at the start of each reply, instead of a function call. Small local models rarely call functions while they talk.")]
        public bool avatarStateByTag = true;

        /// <remarks>
        /// The server's model (Qwen3-1.7B through Ollama) has a 16,384-token context, and Ollama silently cuts a
        /// longer prompt to HALF of it from the front: measured 2026-11-08, Frank's persona plus a second
        /// document and the avatar description arrived as 8,194 tokens, the persona's start was lost and
        /// every turn re-read the whole prompt (1.6 s before the first word). 44,000 characters (~10k tokens
        /// at ~4.3 characters per token) leaves room for the rules, the avatar description and the
        /// conversation, and keeps the prefix stable so it is cached after the first turn.
        /// </remarks>
        [Tooltip("Upper bound on grounding text (characters) in the system prompt. The local model's context is 16k tokens; a longer prompt is cut in half by Ollama. ~4.3 characters per token.")]
        public int maxContextChars = 44000;

        [Header("Embodiment Self-Description")]
        public bool describeEmbodimentCapabilities = false;
        public bool mentionCapabilitiesInGreeting = true;
        [TextArea(2, 6)] public string embodimentNotes = "";
        public bool onlyOfferPlayableAnimations = true;
        public List<string> hiddenAnimationTriggers = new List<string> { "MotionCalibration", "Test" };

        [Header("Audio Input")]
        [Tooltip("Microphone device name. Blank = the first device.")]
        public string microphoneDeviceName;
        [Range(0.1f, 10f)] public float inputGain = 2.0f;

        [Header("VAD & Interruption")]
        public bool enableVocalInterruption = true;
        [Tooltip("Silence the microphone while the agent speaks (no headphones). Interruption still works above the echo threshold.")]
        public bool muteMicWhileTalking = true;
        [Range(0.05f, 0.5f)] public float echoInterruptionThreshold = 0.15f;
        [Range(0.005f, 0.2f)] public float voiceDetectionThreshold = 0.04f;
        public bool useVocalFrequencyFilter = true;

        [Header("UI Interface")]
        [Tooltip("Write the user's words and the agent's reply into the log panel. The local server always transcribes, so this costs nothing.")]
        public bool showSpeechTranscripts = true;
        public Text logTextDisplay;
        public ScrollRect scrollRect;

        /// <summary>Fires whenever the agent is interrupted (voice or programmatic).</summary>
        public event Action<InterruptionInfo> OnAgentInterrupted;

        /// <summary>(response_id, end of speech → first audible sample in ms, or NaN when the end of speech is unknown).</summary>
        public event Action<int, double> OnFirstAudible;

        /// <summary>Last state the server reported: idle, listening, thinking or speaking.</summary>
        public string AgentStateName { get; private set; } = "idle";
        public LocalVoicePlayback Playback => _playback;
        public LocalRealtimeWrapper Wrapper => _wrapper;

        /// <summary>
        /// Replaces the microphone (e.g. with <see cref="LocalVoiceFileInput"/> for a measurement). Set before
        /// <see cref="Connect"/>; null means the microphone.
        /// </summary>
        public ILocalVoiceInput InputOverride { get; set; }

        private LocalRealtimeWrapper _wrapper;
        private LocalVoicePlayback _playback;
        private ILocalVoiceInput _input;
        private readonly List<float> _captured = new List<float>(4096);
        private readonly List<float> _pending = new List<float>(4096);
        private bool _sessionReady;
        private int _currentResponse = -1;
        private int _playingResponse = -1;
        private double _lastEosMs = double.NaN;
        private string _lastEosSource;
        private readonly StringBuilder _fullTranscript = new StringBuilder();
        private float _lpPrev, _hpPrevInput, _hpPrevOutput;
        private float _ignoreSpeechUntil;
        /// <summary>The names the prompt offered; a command naming anything else is skipped (the model invents some).</summary>
        private List<string> _offeredActions = new List<string>();
        private List<string> _offeredEmotions = new List<string>();

        protected override void Awake()
        {
            base.Awake();
            _wrapper = GetComponent<LocalRealtimeWrapper>();
            if (_wrapper == null) _wrapper = gameObject.AddComponent<LocalRealtimeWrapper>();

            _wrapper.OnSetupComplete += HandleReady;
            _wrapper.OnTextReceived += HandleTextReceived;
            _wrapper.OnUserTranscriptReceived += HandleUserTranscript;
            _wrapper.OnCommandReceived += (act, emo, gaze) => StartCoroutine(QueueCommandUntilStopped(act, emo, gaze));
            _wrapper.OnMoveCommand += HandleMove;
            _wrapper.OnFatalError += HandleFatalError;
            _wrapper.OnDisconnected += reason =>
            {
                _sessionReady = false;
                if (reason != DisconnectReason.UserRequested) AppendLog("<color=#E0A858>System: Connection to the local voice server lost; retrying...</color>");
            };
            _wrapper.OnResponseCreated += id => _currentResponse = id;
            _wrapper.OnResponseCancelled += (id, reason) =>
            {
                _playback?.Buffer?.Cancel((uint)id);
                if (_playingResponse == id) _playingResponse = -1;
            };
            _wrapper.OnResponseAudioDone += id => _playback?.Buffer?.MarkDone((uint)id);
            _wrapper.OnAgentState += (state, id) => AgentStateName = state;
            _wrapper.OnUserSpeechStopped += ms =>
            {
                // A file input knows exactly where speech ended; prefer it to the server's VAD estimate.
                if (_input is LocalVoiceFileInput) return;
                _lastEosMs = ms;
                _lastEosSource = "server_vad";
            };

            SetupPlayback();
        }

        private void SetupPlayback()
        {
            if (agentInstance == null) { Debug.LogWarning("[LocalLiveAgent] No avatar instance; run Setup Virtual Agent first."); return; }
            if (agentAudioSource == null) agentAudioSource = agentInstance.GetComponent<AudioSource>() ?? agentInstance.AddComponent<AudioSource>();
            _playback = agentInstance.GetComponent<LocalVoicePlayback>() ?? agentInstance.AddComponent<LocalVoicePlayback>();
            var lipSync = agentInstance.GetComponent<OVRLipSyncContext>();
            if (lipSync != null) lipSync.audioLoopback = true;
            _playback.Initialize(agentAudioSource, lipSync);
            _playback.FirstAudible += HandleFirstAudible;
            _playback.Drained += id => { if (_playingResponse == (int)id) _playingResponse = -1; };

            // The client delivers audio on its network thread; it goes straight into the ring buffer.
        }

        private void Start()
        {
            FindPlayer();
            if (eyeGazeController != null && player != null) eyeGazeController.playerTarget = player;
            if (autoConnectOnStart) Connect();
        }

        private void OnDestroy()
        {
            _input?.Stop();
            if (_wrapper != null)
            {
                _wrapper.OnFatalError -= HandleFatalError;
                _ = _wrapper.DisconnectAsync();
            }
        }

        /// <summary>Opens (or reopens) the session with the server, with the persona, grounding and avatar vocabulary in the prompt.</summary>
        public async void Connect()
        {
            if (logTextDisplay != null) logTextDisplay.text = "<i>System: Connecting to the local voice server...</i>";
            _sessionReady = false;
            string knowledge = await BuildContextPrefixAsync(null) ?? "";
            if (knowledge.Length > maxContextChars)
            {
                Debug.LogWarning($"[LocalLiveAgent] Grounding text is {knowledge.Length} characters; cut to {maxContextChars} (maxContextChars).");
                knowledge = knowledge.Substring(0, maxContextChars);
            }
            string prompt = knowledge + BuildSystemPrompt();
            Debug.Log($"[LocalLiveAgent] system prompt {prompt.Length} characters (grounding {knowledge.Length}), voice {voiceName}");
            _wrapper.InputSampleRate = 16000;
            _wrapper.AvatarStateByTag = avatarStateByTag;

            var toolManager = GetComponent<GeminiToolManager>();
            if (toolManager != null && toolManager.definedTools.Count > 0)
                await _wrapper.ConnectWithDynamicToolsAsync(prompt, voiceName, toolManager.GetDynamicToolDeclarations());
            else
                await _wrapper.ConnectAsync(prompt, voiceName, GeminiLiveAgent.SupportsLocomotion(characterType) && enableLocomotion);
            // The wrapper's client exists now: route its audio into the ring buffer on the network thread.
            var client = _wrapper.Client;
            if (client != null && _playback != null)
                client.Audio += (rid, seq, frame, off, n) => _playback.Buffer.Write(rid, frame, off, n);
        }

        private void HandleReady()
        {
            _sessionReady = true;
            AppendLog($"<color=green>System: Connected to the local voice server ({_wrapper.ActiveVoice}).</color>");
            StartInput();
            if (greetOnConnect)
            {
                string greet = "System: The session has started. Greet the user briefly, in one or two sentences.";
                if (describeEmbodimentCapabilities && mentionCapabilitiesInGreeting)
                    greet += " Mention that they can ask what you can and cannot do.";
                _wrapper.SendTextMessage(greet);
                _lastEosMs = LocalVoiceClient.NowMs;
                _lastEosSource = "typed_send";
            }
        }

        private void StartInput()
        {
            if (_input != null && _input.IsRunning) return;
            _input = InputOverride ?? new LocalVoiceMicrophone(microphoneDeviceName, 16000);
            if (!_input.Start()) Debug.LogWarning("[LocalLiveAgent] Audio input did not start.");
            else if (_input is LocalVoiceMicrophone mic) Debug.Log($"[LocalLiveAgent] Microphone: {mic.DeviceName ?? "(default)"}");
        }

        private void HandleFatalError(IVAException error)
        {
            _sessionReady = false;
            Debug.LogError($"[LocalLiveAgent] {error.Message}");
            AppendLog($"<color=red>System: {error.Message}</color>");
        }

        private void Update()
        {
            if (_input == null || !_input.IsRunning) return;
            _captured.Clear();
            _input.Read(_captured);
            if (_captured.Count == 0) return;
            var file = _input as LocalVoiceFileInput;
            if (file != null && !double.IsNaN(file.LastEndOfSpeechMs) && file.LastEndOfSpeechMs != _lastEosMs)
            {
                _lastEosMs = file.LastEndOfSpeechMs;
                _lastEosSource = "file";
            }
            if (!_sessionReady || !_wrapper.IsConnected) return;

            bool agentSpeaking = _playback != null && _playback.Buffer.IsActive;
            float[] samples = _captured.ToArray();
            if (agentSpeaking && file == null)
            {
                bool loud = Time.time >= _ignoreSpeechUntil && IsSpeechDetected(samples, echoInterruptionThreshold);
                if (loud && enableVocalInterruption) InterruptPlayback(InterruptionSource.UserVoice, "voice", 0f);
                else if (muteMicWhileTalking) Array.Clear(samples, 0, samples.Length);
            }

            // Whole 20 ms frames (320 samples at 16 kHz): the server's own frame size.
            _pending.AddRange(samples);
            const int frame = 320;
            int whole = _pending.Count / frame * frame;
            if (whole == 0) return;
            var bytes = LocalVoiceProtocol.FloatToPcm16(_pending.GetRange(0, whole).ToArray(), 0, whole, file != null ? 1f : inputGain);
            _pending.RemoveRange(0, whole);
            _wrapper.SendAudioChunk(bytes);
        }

        private bool IsSpeechDetected(float[] raw, float threshold)
        {
            float sum = 0f;
            for (int i = 0; i < raw.Length; i++)
            {
                float s = raw[i];
                if (useVocalFrequencyFilter)
                {
                    _lpPrev = _lpPrev + 0.5f * (s - _lpPrev);
                    float hp = 0.9f * (_hpPrevOutput + _lpPrev - _hpPrevInput);
                    _hpPrevOutput = hp;
                    _hpPrevInput = _lpPrev;
                    s = hp;
                }
                sum += s * s;
            }
            return Mathf.Sqrt(sum / Mathf.Max(1, raw.Length)) * inputGain > threshold;
        }

        /// <summary>Stops the agent's current reply now (UI button, custom logic, tests).</summary>
        public void RequestInterrupt(string reason = "programmatic")
        {
            if (_playback == null || !_playback.Buffer.IsActive) return;
            InterruptPlayback(InterruptionSource.Programmatic, reason, 0f);
        }

        private void InterruptPlayback(InterruptionSource source, string reason, float magnitude)
        {
            uint rid = _playback.Buffer.ResponseId;
            _playback.Buffer.Clear();
            if (rid != 0) _wrapper.CancelResponse((int)rid);
            _playingResponse = -1;
            _ignoreSpeechUntil = Time.time + 0.3f;
            try
            {
                OnAgentInterrupted?.Invoke(new InterruptionInfo
                {
                    source = source,
                    reason = reason,
                    magnitude = magnitude,
                    timestampUtc = DateTime.UtcNow,
                    lastAgentFragment = _fullTranscript.Length > 120 ? _fullTranscript.ToString(_fullTranscript.Length - 120, 120) : _fullTranscript.ToString(),
                });
            }
            catch (Exception e) { Debug.LogWarning($"OnAgentInterrupted subscriber threw: {e.Message}"); }
        }

        private void HandleFirstAudible(uint responseId, double dspTime, double clientMs)
        {
            _playingResponse = (int)responseId;
            double outputLatency = _playback.OutputLatencyMs;
            double eosToAudible = double.IsNaN(_lastEosMs) ? double.NaN : clientMs + outputLatency - _lastEosMs;
            var client = _wrapper.Client;
            if (client != null) _ = client.SendPlaybackStartedAsync((int)responseId, outputLatency, eosToAudible, _lastEosSource);
            OnFirstAudible?.Invoke((int)responseId, eosToAudible);
            _lastEosMs = double.NaN;
        }

        private void HandleMove(float angle, float distance, float speed, bool faceMovementDirection)
        {
            if (agentInstance == null || agentLocomotion == null) return;
            Vector3 flatForward = Vector3.ProjectOnPlane(agentInstance.transform.forward, Vector3.up).normalized;
            Vector3 target = agentInstance.transform.position + (Quaternion.Euler(0f, angle, 0f) * flatForward).normalized * distance;
            agentLocomotion.MoveToPoint(target, speed, faceMovementDirection);
            if (player != null) agentLocomotion.cameraPos = player.transform.position;
        }

        private IEnumerator QueueCommandUntilStopped(string act, string emo, string gaze)
        {
            if (agentLocomotion != null) while (agentLocomotion.isMoving) yield return null;
            var placed = PlaceAvatarNames(act, emo);
            act = placed.Key;
            emo = placed.Value;
            Debug.Log($"[LocalLiveAgent] avatar: action {act ?? "-"}, emotion {emo ?? "-"}, gaze {(string.IsNullOrEmpty(gaze) ? "-" : gaze)}");
            if (act != null) SafeRun(() => PerformAction(act), "action " + act);
            if (emo != null) SafeRun(() => ExpressEmotion(emo), "emotion " + emo);
            if (!string.IsNullOrEmpty(gaze) && !gaze.Equals("none", StringComparison.OrdinalIgnoreCase)) HandleGaze(gaze);
        }

        /// <summary>
        /// (action, emotion) to perform for the names the model gave. A small model sometimes puts a name under the
        /// wrong key or under none (measured 2026-11-08: <c>[ConfidentAndSmile; action=...]</c>, the emotion key left
        /// out), so each name goes to the slot whose offered list contains it, its own key's first.
        /// </summary>
        private KeyValuePair<string, string> PlaceAvatarNames(string rawAction, string rawEmotion)
        {
            string act = null, emo = null;
            void Place(string value, bool isAction)
            {
                if (string.IsNullOrWhiteSpace(value) || value.Trim().Equals("none", StringComparison.OrdinalIgnoreCase)) return;
                List<string> own = isAction ? _offeredActions : _offeredEmotions;
                List<string> other = isAction ? _offeredEmotions : _offeredActions;
                bool ownFree = isAction ? act == null : emo == null;
                bool otherFree = isAction ? emo == null : act == null;
                string m;
                // An avatar that offers no names cannot check them: the name is used in its own slot as given.
                if (ownFree && (m = Offered(value, own)) != null) { if (isAction) act = m; else emo = m; return; }
                if (otherFree && other.Count > 0 && (m = Offered(value, other)) != null) { if (isAction) emo = m; else act = m; return; }
                if (verboseCommandLog) Debug.Log($"[LocalLiveAgent] The model asked for '{value}', which is not offered or whose slot is taken; skipped.");
            }
            Place(rawAction, true);
            Place(rawEmotion, false);
            return new KeyValuePair<string, string>(act, emo);
        }

        /// <summary>The offered name <paramref name="value"/> means (case-insensitive), or null for one never offered.</summary>
        private static string Offered(string value, List<string> offered)
        {
            value = value.Trim();
            if (offered == null || offered.Count == 0) return value;
            foreach (var o in offered)
                if (o.Equals(value, StringComparison.OrdinalIgnoreCase)) return o;
            // Words the model uses although they were not offered (measured: "friendly", "interested"), mapped
            // to an offered expression when this avatar has it.
            if (EmotionSynonyms.TryGetValue(value, out var synonym))
                foreach (var o in offered)
                    if (o.Equals(synonym, StringComparison.OrdinalIgnoreCase)) return o;
            return null;
        }

        [Tooltip("Log avatar commands the model asked for, including names that were not offered.")]
        public bool verboseCommandLog = false;

        private static readonly Dictionary<string, string> EmotionSynonyms = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "friendly", "happy" }, { "cheerful", "happy" }, { "warm", "happy" }, { "smiling", "happy" }, { "joyful", "happy" },
            { "interested", "attentive" }, { "curious", "attentive" }, { "engaged", "attentive" }, { "focused", "attentive" },
            { "enthusiastic", "excited" }, { "eager", "excited" },
            { "empathetic", "sympathetic" }, { "concerned", "sympathetic" }, { "compassionate", "sympathetic" },
            { "pensive", "thoughtful" }, { "serious", "thoughtful" }, { "reflective", "thoughtful" },
            { "calm", "neutral" }, { "relaxed", "neutral" },
            { "playful", "amused" }, { "humorous", "amused" },
            { "confident", "proud" },
        };

        private static void SafeRun(Action a, string what)
        {
            try { a(); } catch (Exception e) { Debug.LogWarning($"[LocalLiveAgent] Could not perform {what}: {e.Message}"); }
        }

        private void HandleGaze(string mode)
        {
            if (eyeGazeController == null) return;
            if (mode.Equals("User", StringComparison.OrdinalIgnoreCase) || mode.Equals("LookAtUser", StringComparison.OrdinalIgnoreCase))
            {
                if (player == null) FindPlayer();
                eyeGazeController.playerTarget = player;
                eyeGazeController.currentGazeMode = IVH.Core.Actions.EyeGazeController.GazeMode.LookAtPlayer;
            }
            else eyeGazeController.currentGazeMode = IVH.Core.Actions.EyeGazeController.GazeMode.Idle;
        }

        /// <summary>The system prompt: persona, avatar rules and vocabulary, as <see cref="GeminiLiveAgent"/> builds it, plus spoken-reply rules for a small model.</summary>
        protected virtual string BuildSystemPrompt()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Your name is {agentName}. You are a conversational embodied intelligent virtual agent. Your age is {age}. Your gender is {gender}. Your occupation is {occupation}. Additional information: {additionalDescription}.");
            sb.AppendLine("Everything you write is spoken aloud by a speech synthesiser: plain sentences only, no lists, no markdown, no emojis, no stage directions.");
            if (shortReplies) sb.AppendLine("Keep each reply to one to three sentences unless the user asks for more.");

            HashSet<string> playable = onlyOfferPlayableAnimations ? GetPlayableAnimationTriggers() : null;
            _offeredActions = actionController != null
                ? CleanList(actionController.GetActionsFiltered(bodyActionFilter, gender, bodyAnimationControllerType, playable, hiddenAnimationTriggers).Select(a => a.GetType().Name).ToList())
                : new List<string>();
            _offeredEmotions = (faceAnimator != null || emotionHandler != null)
                ? CleanList(OfferedExpressions(playable).Select(kv => kv.Key).ToList())
                : new List<string>();

            if (avatarStateByTag) AppendTagRules(sb);
            else
            {
                sb.AppendLine("SYSTEM RULES:");
                sb.AppendLine("1. You control a 3D avatar. Call 'update_avatar_state' only when your facial expression, body action or gaze should change, and always ALSO say your reply in words.");
                sb.AppendLine("2. Do not call 'update_avatar_state' on every turn. If nothing changes, just speak.");
                sb.AppendLine("3. Never narrate your tool calls.");
            }
            if (proactiveAnswerStyle)
            {
                sb.AppendLine("CONVERSATION STYLE:");
                sb.AppendLine("- Answer directly and substantively first, with concrete details you know.");
                sb.AppendLine("- Stay in character: speak in the first person about your own life and work.");
                sb.AppendLine("- Do not end every reply with a question. If you do not know, say so briefly.");
            }
            if (_offeredActions.Count > 0) sb.AppendLine("Allowed 'action' values: " + string.Join(", ", _offeredActions));
            if (_offeredEmotions.Count > 0) sb.AppendLine("Allowed 'emotion' values: " + string.Join(", ", _offeredEmotions));
            if (eyeGazeController != null) sb.AppendLine("Allowed 'gaze' values: 'User', 'Idle'");
            if (describeEmbodimentCapabilities)
            {
                sb.AppendLine();
                sb.Append(AgentCapabilityDescriber.BuildPrompt(BuildEmbodimentCapabilities(playable)));
            }
            return sb.ToString();
        }

        /// <summary>
        /// The tag instructions, with examples drawn from this avatar's own names. Varied examples matter: with a
        /// single "Waving" example the model waved in 8 of 8 replies (measured 2026-11-08).
        /// </summary>
        private void AppendTagRules(StringBuilder sb)
        {
            string Pick(List<string> offered, params string[] wanted)
            {
                foreach (var w in wanted)
                    foreach (var o in offered)
                        if (o.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0) return o;
                return offered.Count > 0 ? offered[0] : "none";
            }
            string Ex(string emo, string act, string gaze, string words) => $"[emotion={emo}; action={act}; gaze={gaze}] {words}";
            var e = _offeredEmotions;
            var a = _offeredActions;
            sb.AppendLine("AVATAR: you have a 3D body. Start EVERY reply with one tag in square brackets saying how your face, body and gaze should look while you say it, then say your reply:");
            sb.AppendLine("[emotion=<emotion>; action=<action>; gaze=<gaze>] your spoken words");
            sb.AppendLine("Use only the allowed values listed below. Choose the emotion that matches what you are saying and the action that fits it; greet or wave only for hello and goodbye. The tag is never spoken.");
            sb.AppendLine("Examples:");
            sb.AppendLine(Ex(Pick(e, "happy", "smile"), Pick(a, "wav", "greet"), "User", "Hello, nice to meet you!"));
            sb.AppendLine(Ex(Pick(e, "thought", "attentive", "neutral"), Pick(a, "explain", "talk", "think"), "User", "In short, it works by slowly rotating the virtual world."));
            sb.AppendLine(Ex(Pick(e, "sympath", "sad", "concern"), Pick(a, "nod", "acknowledg"), "User", "I am sorry to hear that."));
            sb.AppendLine(Ex(Pick(e, "amused", "laugh", "happy"), Pick(a, "shrug", "laugh"), "Idle", "Well, nobody is perfect."));
        }

        private List<KeyValuePair<string, string>> OfferedExpressions(HashSet<string> playable)
        {
            var result = new List<KeyValuePair<string, string>>();
            if (emotionHandlerType == EmotionHandlerType.CC4_Animation)
            {
                if (faceAnimator == null) return result;
                foreach (var clip in faceAnimator.GetFacialAnimationsFiltered(facialExpressionFilter, playable))
                {
                    if (hiddenAnimationTriggers != null && hiddenAnimationTriggers.Contains(clip.GetType().Name)) continue;
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

        /// <summary>What the avatar can do, for the self-description (see <see cref="AgentCapabilityDescriber"/>).</summary>
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
                canWalk = enableLocomotion && agentLocomotion != null && GeminiLiveAgent.SupportsLocomotion(characterType),
                visionEnabled = false,
                visionSource = targetCameraType,
                visionIntervalSeconds = 0f,
                userCanInterruptByVoice = enableVocalInterruption,
                additionalNotes = embodimentNotes,
                mentionInGreeting = mentionCapabilitiesInGreeting,
            };
            if (actionController != null)
                foreach (var a in actionController.GetActionsFiltered(bodyActionFilter, gender, bodyAnimationControllerType, playable, hiddenAnimationTriggers))
                    caps.bodyMotions.Add(new KeyValuePair<string, string>(a.GetType().Name, a.Description));
            caps.facialExpressions.AddRange(OfferedExpressions(playable));
            return caps;
        }

        /// <summary>The capability text <see cref="describeEmbodimentCapabilities"/> adds to the prompt.</summary>
        public string GetEmbodimentDescription() => AgentCapabilityDescriber.BuildPrompt(BuildEmbodimentCapabilities());

        public bool IsSessionReady() => _sessionReady;

        private static List<string> CleanList(List<string> input) =>
            (input ?? new List<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => Regex.Replace(s.Trim(), @"[^a-zA-Z0-9_]", "")).Distinct().ToList();

        /// <summary>Sets up the avatar exactly as <see cref="GeminiLiveAgent"/> does, with a <see cref="LocalRealtimeWrapper"/>.</summary>
        public override void SetupVirtualAgent()
        {
            if (agentPrefab == null || agentInstance != null)
            {
                Debug.LogWarning("Agent prefab is not assigned or agent is already set up.");
                return;
            }
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
            if (GeminiLiveAgent.SupportsLocomotion(characterType)) SetupAgentLocomotion();
            RefreshPlayableAnimationCache();
            if (GetComponent<LocalRealtimeWrapper>() == null) gameObject.AddComponent<LocalRealtimeWrapper>();
        }

        // ---- transcript panel (same behaviour as GeminiLiveAgent) ----
        private enum LogSpeaker { None, User, Agent }
        private LogSpeaker _lastSpeaker = LogSpeaker.None;

        private void HandleTextReceived(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            _fullTranscript.Append(text);
            if (showSpeechTranscripts) AppendFragment(LogSpeaker.Agent, $"<color=cyan>{agentName}:</color> ", text);
        }

        private void HandleUserTranscript(string text)
        {
            if (string.IsNullOrEmpty(text) || !showSpeechTranscripts) return;
            AppendFragment(LogSpeaker.User, "<color=yellow>User:</color> ", text);
        }

        private void AppendFragment(LogSpeaker speaker, string prefix, string fragment)
        {
            if (logTextDisplay == null) return;
            if (_lastSpeaker != speaker)
            {
                if (!string.IsNullOrEmpty(logTextDisplay.text)) logTextDisplay.text += "\n\n";
                logTextDisplay.text += prefix;
                _lastSpeaker = speaker;
            }
            logTextDisplay.text += fragment;
            ScrollDown();
        }

        /// <summary>Writes a system line to the in-game log panel.</summary>
        public void AppendLog(string message)
        {
            _lastSpeaker = LogSpeaker.None;
            if (logTextDisplay == null) return;
            logTextDisplay.text += string.IsNullOrEmpty(logTextDisplay.text) ? message : $"\n\n{message}";
            ScrollDown();
        }

        private void ScrollDown()
        {
            if (scrollRect == null || !isActiveAndEnabled) return;
            StartCoroutine(ScrollNextFrame());
        }

        private IEnumerator ScrollNextFrame()
        {
            yield return new WaitForEndOfFrame();
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }
}
