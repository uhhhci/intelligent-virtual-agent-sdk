using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using IVH.Core.Utils;
using IVH.Core.Utils.Logging;
using IVH.Core.Exceptions;

namespace IVH.Core.ServiceConnector.Gemini.Realtime
{
    /// <summary>
    /// Which Gemini realtime endpoint to target. Each backend has different authentication,
    /// URL, and billing characteristics.
    /// </summary>
    public enum GeminiModelType
    {
        //Flash20ExpGoogleAI,        // Expiring (Works on Alpha), disabled for now
        // Serialized by index: append new models, never reorder.
        /// <summary>Gemini 2.5 Flash native audio on Google AI Studio (API key, free tier). Higher latency than Vertex.</summary>
        Flash25PreviewGoogleAI,
        /// <summary>Gemini 2.5 Flash native audio on Google Cloud Vertex AI (service account, paid). Lower latency, enterprise features.</summary>
        Flash25VertexAI,
        /// <summary>Gemini 3.8 Live on Google AI Studio (API key). Thinking is not configurable.</summary>
        Live38GoogleAI,
        /// <summary>Gemini 3.8 Live on Google Cloud Vertex AI (service account, paid). Offered in the <c>eu</c> and <c>us</c> multi-regions and <c>us-central1</c>.</summary>
        Live38VertexAI
    }

    /// <summary>
    /// Low-level wrapper around the Gemini Live bidirectional WebSocket API. Handles authentication
    /// (API-key or Vertex service-account), session setup, audio/image/text send, tool-call routing,
    /// and thread-safe dispatch of server events back to the Unity main thread.
    /// </summary>
    /// <remarks>
    /// High-level agents (<see cref="IntelligentVirtualAgent.GeminiLiveAgent"/>,
    /// <see cref="IntelligentVirtualAgent.GeminiVoiceOnlyAgent"/>) sit on top of this wrapper.
    /// Subscribe to the public events to react to server output.
    /// </remarks>
    /// <summary>What Gemini answers in. AUDIO is what every Live model supports; TEXT only where the model accepts it.</summary>
    public enum GeminiResponseModality { Audio, Text }

    /// <summary>Draws a string field as a dropdown of the Vertex locations that serve the wrapper's selected model.</summary>
    public class VertexLocationAttribute : PropertyAttribute { }

    /// <summary>
    /// The Vertex AI locations that serve each Gemini Live model. Feeds the Inspector dropdown for
    /// <see cref="GeminiRealtimeWrapper.vertexLocation"/> and the check before a session connects.
    /// </summary>
    /// <remarks>
    /// From Google's location table (November 2026). Google adds locations over time, so one missing here
    /// can still be typed in as a custom value. Neither model is served on <c>global</c>: the session is
    /// refused with 1008, "Publisher model ... was not found".
    /// </remarks>
    public static class VertexLiveLocations
    {
        /// <summary>A Vertex location code and where it is.</summary>
        public readonly struct Location
        {
            public readonly string Code;
            public readonly string Place;
            public Location(string code, string place) { Code = code; Place = place; }
        }

        // First entry is the default. Europe first: the SDK's home and most of its users.
        private static readonly Location[] Gemini25 =
        {
            new Location("europe-west4", "Netherlands"),
            new Location("europe-west1", "Belgium"),
            new Location("europe-north1", "Finland"),
            new Location("europe-central2", "Warsaw"),
            new Location("europe-west8", "Milan"),
            new Location("europe-southwest1", "Madrid"),
            new Location("us-central1", "Iowa"),
            new Location("us-east1", "South Carolina"),
            new Location("us-east4", "N. Virginia"),
            new Location("us-east5", "Columbus"),
            new Location("us-south1", "Dallas"),
            new Location("us-west1", "Oregon"),
            new Location("us-west4", "Las Vegas"),
        };

        private static readonly Location[] Gemini38 =
        {
            new Location("eu", "EU multi-region"),
            new Location("us", "US multi-region"),
            new Location("us-central1", "Iowa"),
        };

        /// <summary>The locations that serve <paramref name="model"/>, default first. Empty for the AI Studio models, which have one global endpoint.</summary>
        public static IReadOnlyList<Location> For(GeminiModelType model) => model switch
        {
            GeminiModelType.Flash25VertexAI => Gemini25,
            GeminiModelType.Live38VertexAI => Gemini38,
            _ => Array.Empty<Location>(),
        };

        /// <summary>The location used when none is set: the first one listed for <paramref name="model"/>.</summary>
        public static string DefaultFor(GeminiModelType model)
        {
            var list = For(model);
            return list.Count > 0 ? list[0].Code : "";
        }

        /// <summary>Whether <paramref name="code"/> is a listed location for <paramref name="model"/>.</summary>
        public static bool IsListed(GeminiModelType model, string code)
        {
            foreach (var location in For(model))
                if (location.Code == code) return true;
            return false;
        }
    }

    /// <summary>How readily Gemini's server-side voice activity detection decides that speech started or ended.</summary>
    public enum GeminiVadSensitivity
    {
        /// <summary>Leave the setting out; the server uses its default.</summary>
        ServerDefault,
        /// <summary>Less eager: needs clearer speech to start a turn (noisy rooms), or waits longer before ending one.</summary>
        Low,
        /// <summary>More eager: starts a turn on quieter speech, or ends one sooner after the speaker stops.</summary>
        High
    }

    public class GeminiRealtimeWrapper : MonoBehaviour
    {
        private const string LogTag = "GeminiRealtime";

        [Header("Connection Settings")]
        private string apiKey;
        private string accessToken; // For Vertex

        /// <summary>Which Gemini endpoint to use for this session.</summary>
        public GeminiModelType selectedModel = GeminiModelType.Flash25PreviewGoogleAI;

        /// <summary>
        /// Vertex AI location for the Vertex models; ignored for AI Studio. Empty uses the model's default:
        /// <c>europe-west4</c> for Gemini 2.5 Live, <c>eu</c> (multi-region) for Gemini 3.8 Live.
        /// </summary>
        /// <remarks>
        /// Each model is offered only in some locations (<see cref="VertexLiveLocations"/>), and the server
        /// refuses the session during setup in any other. A nearby location shortens every round trip, and a
        /// different one helps when a location answers "Resource exhausted" (shared capacity, not a project quota).
        /// </remarks>
        [Tooltip("Vertex AI location (Vertex models only). The list shows the locations that serve the selected model. " +
                 "Default: europe-west4 for Gemini 2.5 Live, eu for Gemini 3.8 Live. Pick one near your users; switch when a location answers 'Resource exhausted'.")]
        [VertexLocation]
        public string vertexLocation = "";

        /// <summary>When true, Gemini is told to infer user sentiment from audio and adapt its tone.</summary>
        [Tooltip("Set to true for analyzing user's sentiments from audio. ")]
        public bool affectiveAnalysis = true;

        /// <summary>Enables Gemini's sliding-window context compression to extend long sessions.</summary>
        [Tooltip("Compress context to extend session length.")]
        public bool contextWindowSliding = true;

        /// <summary>
        /// Target token count for the sliding-window compression above. The server trims the oldest
        /// context once the session exceeds this. Native-audio sessions cap at 128k tokens, so there
        /// is headroom to raise this; a low value compresses more often, and each compression is a
        /// server-side pause the user hears as a stall.
        /// </summary>
        [Tooltip("Sliding-window target in tokens. Raise it if the agent stalls periodically in long sessions or forgets earlier turns; lower it only to squeeze out longer sessions. Native-audio sessions cap at 128k.")]
        [Range(4000, 100000)] public int slidingWindowTargetTokens = 12800;

        /// <summary>
        /// Sends <c>thinking_budget = 0</c> in the session setup, disabling the model's internal
        /// reasoning pass. Gemini 2.5 has *dynamic thinking on by default*, which inserts a
        /// noticeable pause before every spoken reply — the single largest source of turn latency in
        /// a realtime voice session. Applies to both the AI Studio and the Vertex backend.
        /// </summary>
        /// <remarks>
        /// Turn this off if you want the model to reason before answering (slower, but better on
        /// multi-step questions), or in the unlikely event your endpoint rejects <c>thinking_config</c>.
        /// Ignored for Gemini 3.8 Live, which accepts no thinking setting.
        /// </remarks>
        [Tooltip("Disable the model's internal 'thinking' pass (thinking_budget = 0). Recommended ON for realtime voice: Gemini 2.5 thinks dynamically by default, which adds a pause before every reply. Ignored for Gemini 3.8 Live, which accepts no thinking setting.")]
        public bool disableThinking = true;

        /// <summary>
        /// How long to wait for the server's <c>setupComplete</c> acknowledgment before treating the
        /// session as failed and firing <see cref="OnFatalError"/>.
        /// </summary>
        [Tooltip("Seconds to wait for Gemini to acknowledge session setup before giving up. Prevents a rejected setup from hanging the agent on 'Connecting...' forever.")]
        [Range(5f, 120f)] public float setupTimeoutSeconds = 20f;

        /// <summary>
        /// Requests server-side transcription of both the user's speech and the agent's audio output.
        /// Text arrives via <see cref="OnTextReceived"/> (agent) and <see cref="OnUserTranscriptReceived"/> (user).
        /// Adds negligible latency because transcription is produced alongside the audio on the server.
        /// </summary>
        [Tooltip("Ask Gemini to also stream text transcripts of the user and agent speech. Off by default (v2.3.3 compat).")]
        public bool enableTranscription = false;

        /// <summary>
        /// AUDIO (default): Gemini speaks. TEXT: Gemini writes and something else speaks (the hybrid agent's
        /// <c>LocalVoiceSpeaker</c>). The native-audio models may refuse TEXT; with AUDIO the hybrid speaks
        /// Gemini's output transcription instead, which every Live model provides.
        /// </summary>
        [Tooltip("AUDIO: Gemini speaks. TEXT: Gemini only writes (for a local voice); not every Live model accepts it.")]
        public GeminiResponseModality responseModality = GeminiResponseModality.Audio;

        // Events
        /// <summary>Fired on the main thread once Gemini acknowledges the setup message and is ready for input.</summary>
        public Action OnSetupComplete;

        /// <summary>Fired on the main thread for each PCM audio chunk received from Gemini (24 kHz, 16-bit mono).</summary>
        public Action<byte[]> OnAudioReceived;

        /// <summary>
        /// Fired on the main thread for each text fragment received from Gemini. With native-audio models,
        /// this fires when <see cref="enableTranscription"/> is true (from the server-side output transcription
        /// channel) or when the model otherwise returns text parts.
        /// </summary>
        public Action<string> OnTextReceived;

        /// <summary>
        /// Fired on the main thread for each fragment of the user's speech transcript, produced by Gemini's
        /// server-side input transcription. Only fires when <see cref="enableTranscription"/> is true.
        /// </summary>
        public Action<string> OnUserTranscriptReceived;

        /// <summary>Fired when Gemini calls the built-in <c>update_avatar_state</c> tool. Parameters: action, emotion, gaze.</summary>
        public Action<string, string, string> OnCommandReceived;

        /// <summary>Fired when Gemini calls the built-in <c>move_agent</c> tool. Parameters: angle (deg), distance (m), speed (m/s), faceMovementDirection.</summary>
        public Action<float, float, float, bool> OnMoveCommand; // angle, distance, speed, faceMovementDirection

        /// <summary>Fired when Gemini calls any user-registered dynamic tool. Parameters: callId, toolName, arguments JSON.</summary>
        public Action<string, string, JToken> OnGenericToolCallReceived;

        // Lifecycle events (added in v2.4; additive to the existing OnSetupComplete semantics).
        /// <summary>Fired on the main thread once the WebSocket is open AND setup is acknowledged. Alias of <see cref="OnSetupComplete"/> for clarity.</summary>
        public Action OnConnected;

        /// <summary>Fired on the main thread when the session ends, with the cause. Fires exactly once per session close.</summary>
        public Action<DisconnectReason> OnDisconnected;

        /// <summary>Fired on the main thread at the start of each auto-reconnect attempt. Parameter is the 1-based attempt number.</summary>
        public Action<int> OnReconnecting;

        /// <summary>Fired on the main thread when the SDK gives up (retries exhausted, auth failure, etc.). Session is done.</summary>
        public Action<IVAException> OnFatalError;

        /// <summary>
        /// Fired on the main thread when Gemini's server-side VAD has detected a user interruption
        /// (<c>serverContent.interrupted == true</c>). After this event, Gemini has stopped generating
        /// audio for the previous turn and is preparing to handle the user's new input. Used by the
        /// agent to know when it's safe to clear post-interrupt audio drop guards.
        /// </summary>
        public Action OnServerInterrupted;

        /// <summary>Gemini finished its turn (<c>serverContent.turnComplete</c>): every word of the reply has been sent.</summary>
        public Action OnTurnComplete;

        /// <summary>
        /// Fired on the main thread when the server announces it will close this connection soon
        /// (<c>goAway</c>; Live API connections are recycled after about ten minutes). Parameter: seconds left.
        /// With <see cref="enableSessionResumption"/> on, the wrapper moves the session to a new
        /// connection by itself once the current reply has finished.
        /// </summary>
        public Action<float> OnGoAway;

        [Header("Auto-Reconnect (opt-in)")]
        /// <summary>If true, attempts to re-establish the session after unexpected disconnects. Defaults to false for v2.3.3 compatibility.</summary>
        [Tooltip("When true, the wrapper will automatically re-open the session after server-side or network disconnects. Off by default.")]
        public bool autoReconnect = false;

        /// <summary>Maximum number of reconnect attempts before firing <see cref="OnFatalError"/>.</summary>
        [Range(1, 20)] public int maxReconnectAttempts = 5;

        /// <summary>Base delay (seconds) for exponential backoff between reconnect attempts. Cap = 30s.</summary>
        [Range(0.1f, 10f)] public float reconnectBaseDelaySeconds = 1.0f;

        [Header("Session Resilience")]
        /// <summary>
        /// Vertex only: when the selected location refuses the session or does not acknowledge it in time
        /// (for example "Resource exhausted", which means that location is out of shared capacity), the
        /// wrapper tries <see cref="vertexFallbackLocations"/> in order before reporting a failure.
        /// </summary>
        [Tooltip("Vertex only: if the location refuses the session (e.g. 'Resource exhausted') or does not answer, try the fallback locations in order before failing.")]
        public bool useVertexLocationFallback = true;

        /// <summary>
        /// Locations tried after <see cref="vertexLocation"/> when <see cref="useVertexLocationFallback"/> is on.
        /// Empty: every other location listed for the model in <see cref="VertexLiveLocations"/>, in list order.
        /// </summary>
        [Tooltip("Locations tried, in order, after the Vertex Location. Empty = every other location that serves the selected model.")]
        public List<string> vertexFallbackLocations = new List<string>();

        /// <summary>
        /// Asks the server for session-resumption handles. When the connection drops or is recycled
        /// (<see cref="OnGoAway"/>), the wrapper reopens it with the last handle, so the conversation
        /// continues with its full history and the agent does not greet again. Needs
        /// <see cref="autoReconnect"/> for unexpected drops; a <c>goAway</c> is handled either way.
        /// </summary>
        [Tooltip("Resume the same conversation on a new connection after a drop or a server-side connection recycle (~10 min), instead of starting over.")]
        public bool enableSessionResumption = false;

        /// <summary>Server-side VAD: how readily speech is detected as the start of a user turn. Low helps in noisy rooms.</summary>
        [Tooltip("How readily the server detects the start of user speech. Low = needs clearer speech (noisy rooms). ServerDefault leaves it to Gemini.")]
        public GeminiVadSensitivity startOfSpeechSensitivity = GeminiVadSensitivity.ServerDefault;

        /// <summary>Server-side VAD: how readily silence is detected as the end of a user turn. High = answers sooner after the user stops.</summary>
        [Tooltip("How readily the server decides the user stopped speaking. High = replies sooner after a pause. ServerDefault leaves it to Gemini.")]
        public GeminiVadSensitivity endOfSpeechSensitivity = GeminiVadSensitivity.ServerDefault;

        /// <summary>Server-side VAD: silence (ms) that ends a user turn. 0 = server default.</summary>
        [Tooltip("Milliseconds of silence that end the user's turn. 0 = server default.")]
        [Range(0, 3000)] public int silenceDurationMs = 0;

        /// <summary>When true, logs outgoing setup/debug frames at Info level. Backed by <see cref="Utils.Logging.IVALogger"/>.</summary>
        public bool verboseLogging = true;

        /// <summary>True while the WebSocket is open. Does not imply the session setup is complete — see <see cref="IsSessionReady"/>.</summary>
        public bool IsConnected { get { var s = _session; return s != null && !s.closedByClient && s.socket.State == WebSocketState.Open; } }

        /// <summary>True once the open session has acknowledged its setup and accepts audio, video and text.</summary>
        public bool IsSessionReady { get { var s = _session; return s != null && s.acknowledged && !s.closedByClient && s.socket.State == WebSocketState.Open; } }

        /// <summary>True while Gemini is generating a reply (from its first audio or text until <c>turnComplete</c> or an interruption).</summary>
        public bool ModelTurnInProgress { get; private set; }

        /// <summary>
        /// Set when <see cref="OnSetupComplete"/> fires: true if this session resumed the previous one
        /// (same conversation, full history), so the agent should not greet again.
        /// </summary>
        public bool IsResumedSession { get; private set; }

        /// <summary>
        /// Set when <see cref="OnSetupComplete"/> fires: true if the wrapper opened this session by itself
        /// (auto-reconnect or connection recycle) rather than through <see cref="ConnectAsync"/>.
        /// </summary>
        public bool IsAutomaticReconnect { get; private set; }

        /// <summary>The Vertex location of the current session (after any fallback). Empty for AI Studio.</summary>
        public string ActiveVertexLocation { get; private set; } = "";

        /// <summary>
        /// One WebSocket connection and everything that belongs to it. Each connection has its own
        /// socket, cancellation and send lock, so a connection that is being replaced can never send on,
        /// close, or report events for the one that replaces it.
        /// </summary>
        private sealed class SessionAttempt
        {
            public ClientWebSocket socket;
            public CancellationTokenSource cts;
            public readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);
            public string location;
            public bool resuming;
            public bool automatic;
            public int conversation;
            public volatile bool acknowledged;
            public volatile bool closedByClient;
            public bool goAwayHandled;
            /// <summary>Completed with null when setup is acknowledged, or with the reason it failed.</summary>
            public readonly TaskCompletionSource<string> setupOutcome =
                new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private SessionAttempt _session;

        // Thread Safety
        private readonly Queue<Action> _mainThreadQueue = new Queue<Action>();
        private readonly object _queueLock = new object();

        /// <summary>Bumped by every connection attempt; an attempt that sees a newer value stops.</summary>
        private int _attemptEpoch;
        /// <summary>Bumped when a new conversation starts (ConnectAsync) or ends (DisconnectAsync). Queued
        /// events of an older conversation are dropped, so a reconnect cuts off the old reply at once.</summary>
        private int _conversation;
        /// <summary>The conversation that most recently had an acknowledged session (-1: none yet).</summary>
        private int _establishedConversation = -1;

        // Reconnect state
        private bool _userRequestedDisconnect;
        private bool _reconnecting;
        private int _reconnectAttempt;
        private string _resumeHandle;
        private string _lastSystemInstruction;
        private string _lastVoiceName;
        private bool _lastHasLocomotion;
        private JArray _lastDynamicTools;
        private bool _usedDynamicToolsConnect;
        
        // Endpoints
        private const string V1ALPHA_URL = "wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1alpha.GenerativeService.BidiGenerateContent";
        private const string V1BETA_URL = "wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent";
        
        // Don't use vertex AI v1beta1 is unstable, using v1 instead. {0} is the host from VertexHost().
        private const string VERTEX_URL_TEMPLATE = "wss://{0}/ws/google.cloud.aiplatform.v1.LlmBidiService/BidiGenerateContent";

        private bool IsVertexModel() => selectedModel == GeminiModelType.Flash25VertexAI || selectedModel == GeminiModelType.Live38VertexAI;

        /// <summary>
        /// Gemini 3.x Live differs from 2.5 in two ways the setup and send paths must respect: it closes the
        /// session on any <c>thinking_config</c> (1007), and it reads <c>media_chunks</c> as audio, so a camera
        /// frame sent there also closes the session.
        /// </summary>
        private bool IsGemini3Live() => selectedModel == GeminiModelType.Live38GoogleAI || selectedModel == GeminiModelType.Live38VertexAI;

        /// <summary>The Vertex location sessions connect to: <see cref="vertexLocation"/>, or the model's default when it is empty.</summary>
        public string VertexLocation => !string.IsNullOrWhiteSpace(vertexLocation)
            ? vertexLocation.Trim().ToLowerInvariant()
            : VertexLiveLocations.DefaultFor(selectedModel);

        /// <summary>Regions use <c>{region}-aiplatform</c>; the <c>eu</c>/<c>us</c> multi-regions and <c>global</c> have their own hosts.</summary>
        private static string VertexHost(string location)
        {
            switch (location)
            {
                case "global": return "aiplatform.googleapis.com";
                case "eu":
                case "us": return $"aiplatform.{location}.rep.googleapis.com";
                default: return $"{location}-aiplatform.googleapis.com";
            }
        }

        private string GetModelString() => selectedModel switch
        {
            // This is the Google AI studio model, it will be deprecated on March 31, 2026.  Afterwards, use Flash25 VertexAI or Flash25PreviewGoogleAI instead
            //GeminiModelType.Flash20ExpGoogleAI => "gemini-2.0-flash-exp",
            
            // This is the Vertex AI Model ID. Vertex AI introduces more costs
            GeminiModelType.Flash25VertexAI => "gemini-live-2.5-flash-native-audio", 
            
            // This is the AI Studio Model ID
            GeminiModelType.Flash25PreviewGoogleAI => "gemini-2.5-flash-native-audio-preview-12-2025",

            // Same model ID on both backends.
            GeminiModelType.Live38GoogleAI => "gemini-3.8-live",
            GeminiModelType.Live38VertexAI => "gemini-3.8-live",

            _ => "gemini-2.5-flash-native-audio-preview-12-2025"
        };

        private void Awake()
        {
            // Only need API key if NOT using Vertex
            if (!IsVertexModel())
                apiKey = GeneralModelHelper.GetGeminiApiKey();
        }

        private readonly List<Action> _dispatchBuffer = new List<Action>();

        private void Update()
        {
            lock (_queueLock)
            {
                _dispatchBuffer.Clear();
                while (_mainThreadQueue.Count > 0) _dispatchBuffer.Add(_mainThreadQueue.Dequeue());
            }
            // Invoked outside the lock and one by one, so a throwing subscriber cannot stall the queue.
            foreach (var action in _dispatchBuffer)
            {
                try { action(); }
                catch (Exception e) { IVALogger.Error(LogTag, "Event handler threw", e); }
            }
            _dispatchBuffer.Clear();
        }

        private void OnDestroy()
        {
            _userRequestedDisconnect = true;
            _conversation++;
            _attemptEpoch++;
            var s = _session;
            _session = null;
            if (s != null) _ = CloseAttemptAsync(s);
        }

        /// <summary>
        /// Opens a Gemini Live session with the built-in <c>update_avatar_state</c> tool (and optionally
        /// <c>move_agent</c>). For custom tool schemas, use <see cref="ConnectWithDynamicToolsAsync"/>.
        /// Always starts a new conversation: a previous session is closed first and none of its pending
        /// audio, text or tool calls reach the subscribers any more.
        /// </summary>
        /// <param name="systemInstruction">Persona / rules prompt delivered at session setup.</param>
        /// <param name="voiceName">Gemini prebuilt voice name (e.g. "Puck", "Charon").</param>
        /// <param name="hasLocomotion">When true, registers the <c>move_agent</c> tool so the model can call it.</param>
        public async Task ConnectAsync(string systemInstruction, string voiceName, bool hasLocomotion=false)
        {
            _lastSystemInstruction = systemInstruction;
            _lastVoiceName = voiceName;
            _lastHasLocomotion = hasLocomotion;
            _usedDynamicToolsConnect = false;
            await StartNewConversationAsync();
        }

        /// <summary>
        /// Opens a Gemini Live session with a caller-supplied set of tool declarations in addition to
        /// the built-in <c>update_avatar_state</c>. Pair with <see cref="OnGenericToolCallReceived"/>
        /// and <see cref="SendGenericToolResponseAsync"/> to service the calls.
        /// </summary>
        /// <param name="systemInstruction">Persona / rules prompt delivered at session setup.</param>
        /// <param name="voiceName">Gemini prebuilt voice name.</param>
        /// <param name="dynamicToolsDeclaration">Array of function declaration JObjects, Gemini tool schema.</param>
        public async Task ConnectWithDynamicToolsAsync(string systemInstruction, string voiceName, JArray dynamicToolsDeclaration)
        {
            _lastSystemInstruction = systemInstruction;
            _lastVoiceName = voiceName;
            _lastDynamicTools = dynamicToolsDeclaration;
            _usedDynamicToolsConnect = true;
            await StartNewConversationAsync();
        }

        private async Task StartNewConversationAsync()
        {
            _userRequestedDisconnect = false;
            _resumeHandle = null;
            _reconnectAttempt = 0;

            var result = await OpenSessionAsync(resume: false, automatic: false, reportFailure: !autoReconnect);
            // With auto-reconnect, a first connect that every location refused is retried with backoff
            // (capacity usually comes back within seconds) before it is reported as fatal.
            if (result == OpenResult.Failed && autoReconnect && !_userRequestedDisconnect) _ = TryReconnectAsync();
        }

        private enum OpenResult { Established, Failed, AuthFailed, Superseded }

        /// <summary>
        /// Closes the current connection and opens a new one, trying the Vertex fallback locations when the
        /// first one refuses. <paramref name="resume"/> reuses the last session-resumption handle;
        /// <paramref name="automatic"/> marks a reconnect the wrapper started by itself, which keeps the
        /// current conversation (its queued events are still delivered).
        /// </summary>
        /// <returns><see cref="OpenResult.Superseded"/> when a newer connect or a disconnect took over meanwhile:
        /// the caller must then not retry, or it would replace the newer session.</returns>
        private async Task<OpenResult> OpenSessionAsync(bool resume, bool automatic, bool reportFailure)
        {
            int epoch = ++_attemptEpoch;
            if (!automatic)
            {
                _conversation++;
                lock (_queueLock) _mainThreadQueue.Clear();
            }
            int conversation = _conversation;
            ModelTurnInProgress = false;

            var previous = _session;
            _session = null;
            if (previous != null) await CloseAttemptAsync(previous);
            if (epoch != _attemptEpoch) return OpenResult.Superseded;

            var auth = await ResolveAuthAsync();
            if (epoch != _attemptEpoch) return OpenResult.Superseded;
            if (!auth.ok)
            {
                FailFast(auth.error, DisconnectReason.AuthFailure);
                return OpenResult.AuthFailed;
            }

            List<string> locations = LocationsToTry();
            string lastFailure = null;
            for (int i = 0; i < locations.Count; i++)
            {
                string location = locations[i];
                string handle = resume ? _resumeHandle : null;

                string failure = await TryOpenAsync(epoch, conversation, location, handle, automatic, auth);
                if (epoch != _attemptEpoch) return OpenResult.Superseded;
                if (failure == null) return OpenResult.Established;

                if (handle != null)
                {
                    // The handle may have expired or belong to another location: start over with a fresh
                    // session instead of failing.
                    IVALogger.Warn(LogTag, $"Could not resume the session ({failure}); starting a new session.");
                    _resumeHandle = null;
                    failure = await TryOpenAsync(epoch, conversation, location, null, automatic, auth);
                    if (epoch != _attemptEpoch) return OpenResult.Superseded;
                    if (failure == null) return OpenResult.Established;
                }

                lastFailure = failure;
                if (i + 1 < locations.Count)
                    IVALogger.Warn(LogTag, $"Vertex location '{location}' did not start the session ({failure}). Trying '{locations[i + 1]}'.");
            }

            string where = IsVertexModel()
                ? $"'{GetModelString()}' in Vertex location(s) {string.Join(", ", locations)}"
                : $"'{GetModelString()}'";
            var error = new ServiceConnectionException($"Gemini did not start a session for {where}: {lastFailure}");
            if (reportFailure) FailFast(error, DisconnectReason.ServerClosed);
            else IVALogger.Warn(LogTag, error.Message);
            return OpenResult.Failed;
        }

        /// <summary>The primary location first, then the fallbacks (Vertex); a single null entry for AI Studio.</summary>
        private List<string> LocationsToTry()
        {
            var result = new List<string>();
            if (!IsVertexModel()) { result.Add(null); return result; }

            result.Add(VertexLocation);
            if (!useVertexLocationFallback) return result;

            IEnumerable<string> fallbacks = vertexFallbackLocations != null && vertexFallbackLocations.Count > 0
                ? vertexFallbackLocations
                : ListedCodes(selectedModel);
            foreach (var raw in fallbacks)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                string code = raw.Trim().ToLowerInvariant();
                if (!result.Contains(code)) result.Add(code);
            }
            return result;
        }

        private static IEnumerable<string> ListedCodes(GeminiModelType model)
        {
            foreach (var location in VertexLiveLocations.For(model)) yield return location.Code;
        }

        private readonly struct AuthInfo
        {
            public readonly bool ok;
            public readonly string accessToken;
            public readonly string projectId;
            public readonly IVAException error;
            public AuthInfo(bool ok, string accessToken, string projectId, IVAException error)
            { this.ok = ok; this.accessToken = accessToken; this.projectId = projectId; this.error = error; }
        }

        /// <summary>
        /// Gets a fresh Vertex OAuth token (service account) or the AI Studio API key.
        /// </summary>
        /// <remarks>
        /// Vertex needs two things the AI Studio path does not: an <c>Authorization: Bearer</c>
        /// header, and the model addressed by its full
        /// <c>projects/{project}/locations/{loc}/publishers/google/models/{model}</c> resource path.
        /// Omitting either leaves the socket open but the session never acknowledges setup, which
        /// surfaces to the user as a connect that hangs forever.
        /// </remarks>
        private async Task<AuthInfo> ResolveAuthAsync()
        {
            if (IsVertexModel())
            {
                try
                {
                    IVALogger.Info(LogTag, "Authenticating with Vertex AI Service Account...");
                    // Looks in C:\Users\[USER]\.aiapi\service_account.json
                    var authResult = await VertexAuthHelper.GetAccessTokenFromUserDir("service_account.json");
                    accessToken = authResult.accessToken;
                    return new AuthInfo(true, authResult.accessToken, authResult.projectId, null);
                }
                catch (Exception e)
                {
                    return new AuthInfo(false, null, null, new AuthException(
                        "Vertex AI authentication failed. Check that ~/.aiapi/service_account.json exists, " +
                        "is a valid service-account key, and that the account has the 'Vertex AI User' role.", e));
                }
            }

            if (string.IsNullOrEmpty(apiKey)) apiKey = GeneralModelHelper.GetGeminiApiKey();
            if (string.IsNullOrEmpty(apiKey))
            {
                return new AuthInfo(false, null, null, new AuthException(
                    "Gemini API key missing. Set it via 'IVA SDK / Setup Wizard / Credentials', which writes ~/.aiapi/auth.json."));
            }
            return new AuthInfo(true, null, null, null);
        }

        /// <summary>
        /// Opens one connection to <paramref name="location"/>, sends the setup and waits for the
        /// server's <c>setupComplete</c>. Returns null on success, otherwise why it failed (the
        /// connection is then closed again).
        /// </summary>
        private async Task<string> TryOpenAsync(int epoch, int conversation, string location, string resumeHandle, bool automatic, AuthInfo auth)
        {
            string modelId = GetModelString();
            string uri;
            if (IsVertexModel())
            {
                if (!VertexLiveLocations.IsListed(selectedModel, location))
                    IVALogger.Warn(LogTag, $"'{modelId}' is not listed for Vertex location '{location}'. Connecting anyway; " +
                        "if Google refuses the session (1008, model not found), pick a location from the Vertex Location list.");
                modelId = $"projects/{auth.projectId}/locations/{location}/publishers/google/models/{modelId}";
                uri = string.Format(VERTEX_URL_TEMPLATE, VertexHost(location));
            }
            else
            {
                uri = $"{V1BETA_URL}?key={apiKey}";
            }

            var attempt = new SessionAttempt
            {
                socket = new ClientWebSocket(),
                cts = new CancellationTokenSource(),
                location = location ?? "",
                resuming = resumeHandle != null,
                automatic = automatic,
                conversation = conversation,
            };
            if (IsVertexModel()) attempt.socket.Options.SetRequestHeader("Authorization", $"Bearer {auth.accessToken}");

            _session = attempt;
            ActiveVertexLocation = location ?? "";

            try
            {
                IVALogger.Info(LogTag, IsVertexModel()
                    ? $"Connecting to {GetModelString()} in {location}{(attempt.resuming ? " (resuming session)" : "")} ..."
                    : $"Connecting to {GetModelString()}{(attempt.resuming ? " (resuming session)" : "")} ...");
                using (var connectTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(setupTimeoutSeconds)))
                    await attempt.socket.ConnectAsync(new Uri(uri), connectTimeout.Token);
            }
            catch (Exception e)
            {
                if (_session == attempt) _session = null;
                await CloseAttemptAsync(attempt);
                return $"could not open the connection: {e.Message}";
            }

            if (epoch != _attemptEpoch)
            {
                if (_session == attempt) _session = null;
                await CloseAttemptAsync(attempt);
                return "superseded";
            }

            _ = ReceiveLoop(attempt);
            await SendJsonAsync(attempt, BuildSetupMessage(modelId, resumeHandle));

            var winner = await Task.WhenAny(attempt.setupOutcome.Task, Task.Delay(TimeSpan.FromSeconds(setupTimeoutSeconds)));
            string failure = winner == attempt.setupOutcome.Task
                ? attempt.setupOutcome.Task.Result
                : $"no setup acknowledgment within {setupTimeoutSeconds:0}s";

            if (failure != null)
            {
                if (_session == attempt) _session = null;
                await CloseAttemptAsync(attempt);
            }
            return failure;
        }

        /// <summary>
        /// Reports an unrecoverable connect failure on the main thread. Without this a failed
        /// handshake is only logged, and the calling agent waits on a <c>setupComplete</c> that will
        /// never arrive — the UI just sits on "Connecting...".
        /// </summary>
        private void FailFast(IVAException error, DisconnectReason reason)
        {
            IVALogger.Error(LogTag, error.Message, error);
            int conversation = _conversation;
            EnqueueMainThread(() =>
            {
                if (conversation != _conversation) return;
                OnDisconnected?.Invoke(reason);
                OnFatalError?.Invoke(error);
            });
        }

        /// <summary>
        /// Closes the WebSocket and releases all session state. Safe to call when already disconnected.
        /// Events of the closed session that are still queued are dropped.
        /// </summary>
        public async Task DisconnectAsync()
        {
            _userRequestedDisconnect = true;
            _resumeHandle = null;
            _conversation++;
            _attemptEpoch++;
            ModelTurnInProgress = false;
            lock (_queueLock) _mainThreadQueue.Clear();

            var s = _session;
            _session = null;
            if (s == null) return;
            await CloseAttemptAsync(s);
            EnqueueMainThread(() => OnDisconnected?.Invoke(DisconnectReason.UserRequested));
        }

        /// <summary>
        /// Closes one connection quickly: a close frame if the socket is idle, then the receive loop is
        /// cancelled and the socket aborted. Never waits on the server for more than half a second.
        /// </summary>
        private static async Task CloseAttemptAsync(SessionAttempt a)
        {
            if (a == null || a.closedByClient) return;
            a.closedByClient = true;
            a.setupOutcome.TrySetResult("closed by client");
            try
            {
                if (a.socket.State == WebSocketState.Open && await a.sendLock.WaitAsync(300))
                {
                    try
                    {
                        using (var t = new CancellationTokenSource(500))
                            await a.socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Closing", t.Token);
                    }
                    finally { a.sendLock.Release(); }
                }
            }
            catch { /* the socket is aborted below anyway */ }
            try { a.cts.Cancel(); } catch { }
            try { a.socket.Abort(); } catch { }
            try { a.socket.Dispose(); } catch { }
        }

        /// <summary>
        /// Builds the <c>setup</c> payload shared by both connect paths: model id, generation config
        /// (voice, thinking budget), system instruction, context-window compression, and transcription.
        /// Callers only add their own <c>tools</c> array.
        /// </summary>
        /// <remarks>
        /// Keeping this in one place is deliberate. The two paths previously diverged — only the
        /// no-dynamic-tools path enabled context-window compression, and only the Vertex backend
        /// disabled thinking — so an agent's latency and maximum session length silently depended on
        /// whether it happened to have a tool attached.
        /// </remarks>
        private JObject BuildSetupContent(string model, string systemPrompt, string voice, string resumeHandle = null)
        {
            var generationConfig = new JObject();
            generationConfig["response_modalities"] = new JArray(responseModality == GeminiResponseModality.Text ? "TEXT" : "AUDIO");

            // Gemini 2.5 runs *dynamic thinking by default*, which inserts a reasoning pause before
            // every spoken reply. In a realtime voice session that pause is the dominant source of
            // perceived latency, so we opt out on both backends unless the user asks for reasoning.
            // Gemini 3.x Live closes the session on any thinking_config (1007: "Thinking budget is not
            // supported for this model"), so it never gets one.
            if (disableThinking && !IsGemini3Live())
            {
                generationConfig["thinking_config"] = new JObject
                {
                    ["thinking_budget"] = 0,
                    ["include_thoughts"] = false,
                };
            }

            var speechConfig = new JObject();
            var voiceConfig = new JObject();
            voiceConfig["prebuilt_voice_config"] = new JObject { ["voice_name"] = voice };
            speechConfig["voice_config"] = voiceConfig;
            if (responseModality == GeminiResponseModality.Audio) generationConfig["speech_config"] = speechConfig;

            var setupContent = new JObject
            {
                ["model"] = IsVertexModel() ? model : $"models/{model}",
                ["generation_config"] = generationConfig,
                ["system_instruction"] = new JObject { ["parts"] = new JArray(new JObject { ["text"] = systemPrompt }) }
            };

            if (contextWindowSliding)
            {
                setupContent["context_window_compression"] = new JObject
                {
                    ["sliding_window"] = new JObject
                    {
                        ["targetTokens"] = slidingWindowTargetTokens,
                    }
                };
            }

            if (enableTranscription)
            {
                // Live API accepts camelCase for both AI Studio (v1beta) and Vertex (v1).
                setupContent["outputAudioTranscription"] = new JObject();
                setupContent["inputAudioTranscription"] = new JObject();
            }

            // Session resumption (verified on Vertex v1 with gemini-live-2.5-flash-native-audio): an empty
            // object asks for resumption handles, a handle resumes that session with its full history.
            if (enableSessionResumption || resumeHandle != null)
            {
                setupContent["sessionResumption"] = resumeHandle != null
                    ? new JObject { ["handle"] = resumeHandle }
                    : new JObject();
            }

            var activityDetection = new JObject();
            if (startOfSpeechSensitivity != GeminiVadSensitivity.ServerDefault)
                activityDetection["startOfSpeechSensitivity"] = startOfSpeechSensitivity == GeminiVadSensitivity.Low ? "START_SENSITIVITY_LOW" : "START_SENSITIVITY_HIGH";
            if (endOfSpeechSensitivity != GeminiVadSensitivity.ServerDefault)
                activityDetection["endOfSpeechSensitivity"] = endOfSpeechSensitivity == GeminiVadSensitivity.Low ? "END_SENSITIVITY_LOW" : "END_SENSITIVITY_HIGH";
            if (silenceDurationMs > 0)
                activityDetection["silenceDurationMs"] = silenceDurationMs;
            if (activityDetection.Count > 0)
                setupContent["realtimeInputConfig"] = new JObject { ["automaticActivityDetection"] = activityDetection };

            return setupContent;
        }

        /// <summary>The full <c>setup</c> message for the stored connect request (built-in or dynamic tools).</summary>
        private JObject BuildSetupMessage(string model, string resumeHandle)
        {
            var setupContent = BuildSetupContent(model, _lastSystemInstruction, _lastVoiceName, resumeHandle);
            setupContent["tools"] = _usedDynamicToolsConnect
                ? BuildDynamicTools(_lastDynamicTools)
                : BuildGenericTools(_lastHasLocomotion);

            var setupData = new JObject { ["setup"] = setupContent };
            if (verboseLogging) IVALogger.Info(LogTag, $"Sending Setup: {setupData.ToString(Formatting.None)}");
            return setupData;
        }

        private static JArray BuildGenericTools(bool hasLocomotion)
        {
            var toolsArray = new JArray();
            var tool = new JObject();
            var avatarFunc = new JObject
            {
                ["name"] = "update_avatar_state",
                ["description"] = "Change the avatar's physical behavior.",
                ["parameters"] = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["action"] = new JObject { ["type"] = "string", ["description"] = "Body animation name" },
                        ["emotion"] = new JObject { ["type"] = "string", ["description"] = "Facial expression name" },
                        ["gaze"] = new JObject { ["type"] = "string", ["description"] = "Target: 'User' or 'Idle'" }
                    },
                    ["required"] = new JArray("action", "emotion", "gaze")
                }
            };
            tool["function_declarations"] = new JArray(avatarFunc);

            if(hasLocomotion){
                var moveFunc = new JObject
                {
                    ["name"] = "move_agent",
                    ["description"] = "Move yourself physically in the 3D environment. Interpret the user's natural language intent into a precise angle, distance, and speed. The angle is relative to YOUR current forward-facing direction: 0 = straight ahead, 90 = your right, -90 = your left, 180 = directly behind you. Set faceMovementDirection to true when the user implies you should turn to face the movement direction first (e.g. 'turn around and walk away', 'run away', 'walk over there'). Set it to false when the user implies you should maintain your current facing (e.g. 'step back', 'back up', 'move to the left a bit').",
                    ["parameters"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["angle"] = new JObject
                            {
                                ["type"] = "number",
                                ["description"] = "Movement angle in degrees relative to your forward direction. 0 = forward, 90 = right, -90 = left, 180 = behind. Any value from -180 to 180."
                            },
                            ["distance"] = new JObject
                            {
                                ["type"] = "number",
                                ["description"] = "Distance in meters. Small step ~1-2, normal step ~3, large movement 5 or more."
                            },
                            ["speed"] = new JObject
                            {
                                ["type"] = "number",
                                ["description"] = "Speed in m/s. 0.5 = cautious/slow, 1.0 = normal walk, 2 = run/jog"
                            },
                            ["faceMovementDirection"] = new JObject
                            {
                                ["type"] = "boolean",
                                ["description"] = "If true, turn to face the movement direction before walking (e.g. 'turn around and walk away', 'run away'). If false, maintain current facing and use strafe/backward movement (e.g. 'step back', 'back up')."
                            }
                        },
                        ["required"] = new JArray("angle", "distance", "speed", "faceMovementDirection")
                    }
                    };
                tool["function_declarations"] = new JArray(avatarFunc, moveFunc);

            }

            toolsArray.Add(tool);
            return toolsArray;
        }

        private static JArray BuildDynamicTools(JArray dynamicFunctionDeclarations)
        {
            var functionDeclarations = new JArray();
            functionDeclarations.Add(new JObject {
                ["name"] = "update_avatar_state",
                ["description"] = "Change the avatar's physical behavior.",
                ["parameters"] = JObject.Parse("{\"type\":\"object\",\"properties\":{\"action\":{\"type\":\"string\"},\"emotion\":{\"type\":\"string\"},\"gaze\":{\"type\":\"string\"}},\"required\":[\"action\",\"emotion\",\"gaze\"]}")
            });

            if (dynamicFunctionDeclarations != null)
                foreach (var dt in dynamicFunctionDeclarations) functionDeclarations.Add(dt.DeepClone());

            var toolWrapper = new JObject { ["function_declarations"] = functionDeclarations };
            return new JArray(toolWrapper);
        }

        /// <summary>Sends a text turn to Gemini as if typed by the user. No-op until the session is ready.</summary>
        public void SendTextMessage(string text)
        {
            if (!IsSessionReady) return;
            var msg = new { client_content = new { turns = new[] { new { role = "user", parts = new[] { new { text = text } } } }, turn_complete = true } };
            _ = SendJsonAsync(_session, msg);
        }

        /// <summary>Streams a PCM audio chunk to Gemini. Expected format: 16-bit signed little-endian mono at 16 kHz. Dropped until the session is ready.</summary>
        public void SendAudioChunk(byte[] pcmData)
        {
            if (!IsSessionReady) return;
            string data = Convert.ToBase64String(pcmData);
            // Gemini 3.x Live takes the typed `audio` field; the 2.5 models keep the deprecated media_chunks they were validated with.
            object msg = IsGemini3Live()
                ? new { realtime_input = new { audio = new { mime_type = "audio/pcm;rate=16000", data } } }
                : (object)new { realtime_input = new { media_chunks = new[] { new { mime_type = "audio/pcm", data } } } };
            _ = SendJsonAsync(_session, msg);
        }

        /// <summary>Streams a JPEG-encoded image frame to Gemini for multimodal reasoning. Dropped until the session is ready.</summary>
        public void SendImage(byte[] imageData)
        {
            if (!IsSessionReady) return;
            string data = Convert.ToBase64String(imageData);
            // Gemini 3.x Live reads media_chunks as audio and closes the session on a JPEG there (1007); frames go in `video`.
            object msg = IsGemini3Live()
                ? new { realtime_input = new { video = new { mime_type = "image/jpeg", data } } }
                : (object)new { realtime_input = new { media_chunks = new[] { new { mime_type = "image/jpeg", data } } } };
            _ = SendJsonAsync(_session, msg);
        }

        private Task SendToolResponse(SessionAttempt a, string id, string functionName = "update_avatar_state")
        {
            var msg = new { tool_response = new { function_responses = new[] { new { id = id, name = functionName, response = new { status = "ok" } } } } };
            return SendJsonAsync(a, msg);
        }

        private static readonly JsonSerializerSettings SendSettings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };

        /// <summary>Sends one frame on <paramref name="a"/>. Frames for a closed or replaced connection are dropped.</summary>
        private async Task SendJsonAsync(SessionAttempt a, object data)
        {
            if (a == null || a.closedByClient || a.socket.State != WebSocketState.Open) return;
            try { await a.sendLock.WaitAsync(a.cts.Token); }
            catch { return; }
            try
            {
                if (a.closedByClient || a.socket.State != WebSocketState.Open) return;
                string json = data is JToken token ? token.ToString(Formatting.None) : JsonConvert.SerializeObject(data, SendSettings);
                await a.socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(json)), WebSocketMessageType.Text, true, a.cts.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { if (!a.closedByClient) IVALogger.Error(LogTag, "Send Error", ex); }
            finally { a.sendLock.Release(); }
        }

        private async Task ReceiveLoop(SessionAttempt a)
        {
            var buffer = new byte[65536];
            DisconnectReason reason = DisconnectReason.Unknown;
            string closeDescription = null;
            try
            {
                while (!a.closedByClient && a.socket.State == WebSocketState.Open)
                {
                    var ms = new System.IO.MemoryStream();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await a.socket.ReceiveAsync(new ArraySegment<byte>(buffer), a.cts.Token);
                        ms.Write(buffer, 0, result.Count);
                    } while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        // The description carries Google's reason (e.g. "Resource exhausted" on a quota
                        // refusal, which arrives as 1011 InternalServerError); without it every refusal looks alike.
                        closeDescription = $"{(int?)result.CloseStatus} {result.CloseStatusDescription}".Trim();
                        if (!a.closedByClient)
                            IVALogger.Warn(LogTag, $"Server Closed Connection{(a.location.Length > 0 ? $" ({a.location})" : "")}. Status: {result.CloseStatus} ({(int?)result.CloseStatus}): {result.CloseStatusDescription}");
                        reason = DisconnectReason.ServerClosed;
                        break;
                    }

                    ProcessMessage(Encoding.UTF8.GetString(ms.ToArray()), a);
                }
            }
            catch (Exception ex)
            {
                if (!a.closedByClient)
                {
                    IVALogger.Info(LogTag, $"Receive Loop Stopped: {ex.Message}");
                    reason = DisconnectReason.NetworkError;
                    closeDescription = ex.Message;
                }
                else
                {
                    reason = DisconnectReason.UserRequested;
                }
            }

            HandleReceiveLoopExit(a, reason, closeDescription);
        }

        private void HandleReceiveLoopExit(SessionAttempt a, DisconnectReason reason, string closeDescription)
        {
            // Closed by us (reconnect, disconnect): whoever closed it already reported what matters.
            if (a.closedByClient) return;

            if (!a.acknowledged)
            {
                // A close before setupComplete is a refused setup (model not offered in this location, an
                // unsupported field, no access, no capacity). The connect loop decides what to do next.
                a.setupOutcome.TrySetResult(string.IsNullOrEmpty(closeDescription) ? "connection closed before setup" : closeDescription);
                return;
            }

            if (_session != a) return;   // a newer connection has already taken over
            _session = null;
            ModelTurnInProgress = false;
            _ = CloseAttemptAsync(a);   // marks it closed at once, then releases the socket

            int conversation = a.conversation;
            EnqueueMainThread(() => { if (conversation == _conversation) OnDisconnected?.Invoke(reason); });

            if (_userRequestedDisconnect || conversation != _conversation) return;
            // After a goAway, ResumeAfterGoAwayAsync is already waiting and takes over once _session is null.
            if (autoReconnect && reason != DisconnectReason.AuthFailure)
                _ = TryReconnectAsync();
        }

        private async Task TryReconnectAsync()
        {
            if (_reconnecting) return;
            _reconnecting = true;
            int conversation = _conversation;
            try
            {
                while (_reconnectAttempt < maxReconnectAttempts && !_userRequestedDisconnect && conversation == _conversation)
                {
                    _reconnectAttempt++;
                    // First retry almost at once (a dropped or recycled connection resumes fine), then back off.
                    float delay = _reconnectAttempt == 1
                        ? 0.25f
                        : Mathf.Min(30f, reconnectBaseDelaySeconds * Mathf.Pow(2, _reconnectAttempt - 2));
                    int attemptSnapshot = _reconnectAttempt;
                    EnqueueMainThread(() => { if (conversation == _conversation) OnReconnecting?.Invoke(attemptSnapshot); });
                    IVALogger.Info(LogTag, $"Reconnect attempt {_reconnectAttempt}/{maxReconnectAttempts} in {delay:0.0}s");

                    try { await Task.Delay(TimeSpan.FromSeconds(delay)); } catch { }
                    if (_userRequestedDisconnect || conversation != _conversation) return;

                    try
                    {
                        var result = await OpenSessionAsync(resume: true, automatic: true, reportFailure: false);
                        if (result != OpenResult.Failed) return;   // established (attempts reset on ack), auth error reported, or superseded
                    }
                    catch (Exception ex)
                    {
                        IVALogger.Warn(LogTag, $"Reconnect attempt {_reconnectAttempt} failed: {ex.Message}");
                    }
                }

                if (_userRequestedDisconnect || conversation != _conversation) return;
                var fatal = new ServiceConnectionException(
                    $"Reconnect retries exhausted after {_reconnectAttempt} attempts", _reconnectAttempt);
                IVALogger.Error(LogTag, fatal.Message, fatal);
                EnqueueMainThread(() =>
                {
                    if (conversation != _conversation) return;
                    OnDisconnected?.Invoke(DisconnectReason.RetriesExhausted);
                    OnFatalError?.Invoke(fatal);
                });
            }
            finally
            {
                _reconnecting = false;
            }
        }

        /// <summary>
        /// Moves the session to a new connection after a <c>goAway</c>: waits until the current reply has
        /// been sent (or the time is nearly up), then reopens with the resumption handle. The agent keeps
        /// playing what it already received; the conversation continues where it was.
        /// </summary>
        private async Task ResumeAfterGoAwayAsync(SessionAttempt a, float timeLeftSeconds)
        {
            int conversation = _conversation;
            double budget = Math.Max(0.5, Math.Min(timeLeftSeconds - 1.5, 30.0));
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (clock.Elapsed.TotalSeconds < budget && ModelTurnInProgress && _session == a && conversation == _conversation)
            {
                try { await Task.Delay(100); } catch { }
            }
            if (_userRequestedDisconnect || conversation != _conversation) return;
            // The connection may already have dropped and been replaced by auto-reconnect.
            if (_session != a && _session != null) return;
            if (_reconnecting) return;

            IVALogger.Info(LogTag, "Server is recycling the connection; resuming the session on a new connection.");
            var result = await OpenSessionAsync(resume: true, automatic: true, reportFailure: !autoReconnect);
            if (result == OpenResult.Failed && autoReconnect && !_userRequestedDisconnect && conversation == _conversation)
                _ = TryReconnectAsync();
        }

        private void ProcessMessage(string json, SessionAttempt a)
        {
            // Messages of a connection that has been replaced or closed are ignored.
            if (a.closedByClient || (_session != a && a.acknowledged)) return;
            int conversation = a.conversation;

            try
            {
                var root = JObject.Parse(json);

                if (root["setupComplete"] != null || root["setup_complete"] != null)
                {
                    a.acknowledged = true;
                    _reconnectAttempt = 0; // Reset on successful setup
                    bool resumed = a.resuming;
                    // Automatic only when it continues a conversation that was already running; a first
                    // connect that needed retries is still the start of the conversation (greet as usual).
                    bool automatic = a.automatic && _establishedConversation == conversation;
                    _establishedConversation = conversation;
                    if (a.location.Length > 0 && a.location != VertexLocation)
                        IVALogger.Warn(LogTag, $"Session started in fallback Vertex location '{a.location}'.");
                    a.setupOutcome.TrySetResult(null);
                    EnqueueMainThread(() =>
                    {
                        if (conversation != _conversation || _session != a) return;
                        IsResumedSession = resumed;
                        IsAutomaticReconnect = automatic;
                        OnSetupComplete?.Invoke();
                        OnConnected?.Invoke();
                    });
                    return;
                }

                JToken resumption = root["sessionResumptionUpdate"] ?? root["session_resumption_update"];
                if (resumption != null)
                {
                    string newHandle = (resumption["newHandle"] ?? resumption["new_handle"])?.ToString();
                    bool resumable = resumption["resumable"]?.Value<bool>() ?? false;
                    if (resumable && !string.IsNullOrEmpty(newHandle) && conversation == _conversation) _resumeHandle = newHandle;
                }

                JToken goAway = root["goAway"] ?? root["go_away"];
                if (goAway != null)
                {
                    float timeLeft = ParseDurationSeconds((goAway["timeLeft"] ?? goAway["time_left"])?.ToString());
                    IVALogger.Info(LogTag, $"Server will close this connection in {timeLeft:0.#}s (goAway).");
                    EnqueueMainThread(() => { if (conversation == _conversation) OnGoAway?.Invoke(timeLeft); });
                    if (enableSessionResumption && !a.goAwayHandled)
                    {
                        a.goAwayHandled = true;
                        _ = ResumeAfterGoAwayAsync(a, timeLeft);
                    }
                }

                // TOOL CALLS
                JToken toolCall = root["toolCall"] ?? root["tool_call"];
                if (toolCall != null)
                {
                    var fnCalls = toolCall["functionCalls"] ?? toolCall["function_calls"];
                    if (fnCalls != null)
                    {
                        foreach (var call in fnCalls)
                        {
                            string fnName = call["name"]?.ToString();
                            string callId = call["id"]?.ToString();

                            if (fnName == "update_avatar_state")
                            {
                                var args = call["args"];
                                string act = args?["action"]?.ToString() ?? "";
                                string emo = args?["emotion"]?.ToString() ?? "";
                                string gaze = args?["gaze"]?.ToString() ?? "";

                                EnqueueMainThread(() => { if (conversation == _conversation) OnCommandReceived?.Invoke(act, emo, gaze); });
                                _ = SendToolResponse(a, callId);
                            }
                            else if (fnName == "move_agent")
                            {
                                var args = call["args"];
                                float angle = args?["angle"]?.Value<float>() ?? 0f;
                                float distance = args?["distance"]?.Value<float>() ?? 1.0f;
                                float speed = args?["speed"]?.Value<float>() ?? 1.0f;
                                bool faceMovementDirection = args?["faceMovementDirection"]?.Value<bool>() ?? true;

                                EnqueueMainThread(() => { if (conversation == _conversation) OnMoveCommand?.Invoke(angle, distance, speed, faceMovementDirection); });
                                _ = SendToolResponse(a, callId, "move_agent");
                            }
                            else
                            {
                                JToken args = call["args"];
                                EnqueueMainThread(() => { if (conversation == _conversation) OnGenericToolCallReceived?.Invoke(callId, fnName, args); });
                            }
                        }
                    }
                }

                // SERVER CONTENT
                JToken serverContent = root["serverContent"] ?? root["server_content"];
                if (serverContent != null)
                {
                    if (serverContent["interrupted"]?.Value<bool>() == true)
                    {
                        // Gemini has acknowledged the interruption server-side and stopped generating
                        // for the prior turn. Surface this so the agent can release post-interrupt
                        // audio drop guards.
                        ModelTurnInProgress = false;
                        EnqueueMainThread(() => { if (conversation == _conversation) OnServerInterrupted?.Invoke(); });
                    }

                    // Server-side transcription channels (only present when enableTranscription is true).
                    JToken outObj = serverContent["outputTranscription"] ?? serverContent["output_transcription"];
                    string outputTranscript = outObj?["text"]?.ToString();
                    if (!string.IsNullOrEmpty(outputTranscript))
                    {
                        // Not a turn marker: the transcription can trail the audio and arrive after turnComplete.
                        EnqueueMainThread(() => { if (conversation == _conversation) OnTextReceived?.Invoke(outputTranscript); });
                    }

                    JToken inObj = serverContent["inputTranscription"] ?? serverContent["input_transcription"];
                    string inputTranscript = inObj?["text"]?.ToString();
                    if (!string.IsNullOrEmpty(inputTranscript))
                    {
                        if (verboseLogging) IVALogger.Debug(LogTag, $"User transcript: {inputTranscript}");
                        EnqueueMainThread(() => { if (conversation == _conversation) OnUserTranscriptReceived?.Invoke(inputTranscript); });
                    }

                    JToken parts = serverContent["modelTurn"]?["parts"] ?? serverContent["model_turn"]?["parts"];
                    if (parts != null)
                    {
                        foreach (var part in parts)
                        {
                            if (part["text"] != null)
                            {
                                string text = part["text"].ToString();
                                ModelTurnInProgress = true;
                                EnqueueMainThread(() => { if (conversation == _conversation) OnTextReceived?.Invoke(text); });
                            }

                            if (part["inlineData"] != null || part["inline_data"] != null)
                            {
                                JToken dataObj = part["inlineData"] ?? part["inline_data"];
                                byte[] audio = Convert.FromBase64String(dataObj["data"].ToString());
                                ModelTurnInProgress = true;
                                EnqueueMainThread(() => { if (conversation == _conversation) OnAudioReceived?.Invoke(audio); });
                            }
                        }
                    }

                    if (serverContent["turnComplete"]?.Value<bool>() == true || serverContent["turn_complete"]?.Value<bool>() == true)
                    {
                        ModelTurnInProgress = false;
                        EnqueueMainThread(() => { if (conversation == _conversation) OnTurnComplete?.Invoke(); });
                    }
                }
            }
            catch (Exception ex) { if(verboseLogging) IVALogger.Warn(LogTag, $"Parse Error: {ex.Message}"); }
        }

        /// <summary>Parses a protobuf JSON duration ("5s", "1.500s") into seconds; 0 when absent or unreadable.</summary>
        private static float ParseDurationSeconds(string duration)
        {
            if (string.IsNullOrEmpty(duration)) return 0f;
            string s = duration.Trim().TrimEnd('s', 'S');
            return float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : 0f;
        }

        private void EnqueueMainThread(Action action) { lock (_queueLock) { _mainThreadQueue.Enqueue(action); } }

        /// <summary>
        /// Returns the result of a tool call to Gemini so the model can continue its turn.
        /// Must be called after handling a <see cref="OnGenericToolCallReceived"/> event.
        /// </summary>
        /// <param name="id">The tool call id from the original invocation.</param>
        /// <param name="name">The tool name from the original invocation.</param>
        /// <param name="responsePayload">Any JSON-serializable object; usually <c>new { status = "success", result = ... }</c> or <c>new { error = "..." }</c>.</param>
        public async Task SendGenericToolResponseAsync(string id, string name, object responsePayload)
        {
            var msg = new { tool_response = new { function_responses = new[] { new { id = id, name = name, response = responsePayload } } } };
            await SendJsonAsync(_session, msg);
        }

    }
}
