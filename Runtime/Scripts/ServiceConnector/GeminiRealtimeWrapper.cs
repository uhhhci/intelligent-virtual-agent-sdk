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
        /// <summary>Google AI Studio (API key, free tier). Higher latency than Vertex.</summary>
        Flash25PreviewGoogleAI,
        /// <summary>Google Cloud Vertex AI (service account, paid). Lower latency, enterprise features.</summary>
        Flash25VertexAI
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
    public class GeminiRealtimeWrapper : MonoBehaviour
    {
        private const string LogTag = "GeminiRealtime";

        [Header("Connection Settings")]
        private string apiKey;
        private string accessToken; // For Vertex

        /// <summary>Which Gemini endpoint to use for this session.</summary>
        public GeminiModelType selectedModel = GeminiModelType.Flash25PreviewGoogleAI;

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
        /// </remarks>
        [Tooltip("Disable the model's internal 'thinking' pass (thinking_budget = 0). Recommended ON for realtime voice: Gemini 2.5 thinks dynamically by default, which adds a pause before every reply.")]
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

        [Header("Auto-Reconnect (opt-in)")]
        /// <summary>If true, attempts to re-establish the session after unexpected disconnects. Defaults to false for v2.3.3 compatibility.</summary>
        [Tooltip("When true, the wrapper will automatically re-open the session after server-side or network disconnects. Off by default.")]
        public bool autoReconnect = false;

        /// <summary>Maximum number of reconnect attempts before firing <see cref="OnFatalError"/>.</summary>
        [Range(1, 20)] public int maxReconnectAttempts = 5;

        /// <summary>Base delay (seconds) for exponential backoff between reconnect attempts. Cap = 30s.</summary>
        [Range(0.1f, 10f)] public float reconnectBaseDelaySeconds = 1.0f;

        /// <summary>When true, logs outgoing setup/debug frames at Info level. Backed by <see cref="Utils.Logging.IVALogger"/>.</summary>
        public bool verboseLogging = true;

        /// <summary>True while the WebSocket is open. Does not imply the session setup is complete — see <see cref="OnSetupComplete"/>.</summary>
        public bool IsConnected => _webSocket != null && _webSocket.State == WebSocketState.Open;

        private ClientWebSocket _webSocket;
        private CancellationTokenSource _cancellationTokenSource;

        // Thread Safety
        private readonly Queue<Action> _mainThreadQueue = new Queue<Action>();
        private readonly object _queueLock = new object();
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);

        // Reconnect state
        private bool _userRequestedDisconnect;
        private bool _disconnectFired;
        private bool _setupAcknowledged;
        private int _reconnectAttempt;
        private string _lastSystemInstruction;
        private string _lastVoiceName;
        private bool _lastHasLocomotion;
        private JArray _lastDynamicTools;
        private bool _usedDynamicToolsConnect;
        
        // Endpoints
        private const string V1ALPHA_URL = "wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1alpha.GenerativeService.BidiGenerateContent";
        private const string V1BETA_URL = "wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent";
        
        // Don't use vertex AI v1beta1 is unstable, using v1 instead.
        private const string VERTEX_PROJECT_LOCATION = "us-central1"; 
        private const string VERTEX_URL_TEMPLATE = "wss://{0}-aiplatform.googleapis.com/ws/google.cloud.aiplatform.v1.LlmBidiService/BidiGenerateContent";

        private bool IsVertexModel() => selectedModel == GeminiModelType.Flash25VertexAI;

        private string GetModelString() => selectedModel switch
        {
            // This is the Google AI studio model, it will be deprecated on March 31, 2026.  Afterwards, use Flash25 VertexAI or Flash25PreviewGoogleAI instead
            //GeminiModelType.Flash20ExpGoogleAI => "gemini-2.0-flash-exp",
            
            // This is the Vertex AI Model ID. Vertex AI introduces more costs
            GeminiModelType.Flash25VertexAI => "gemini-live-2.5-flash-native-audio", 
            
            // This is the AI Studio Model ID
            GeminiModelType.Flash25PreviewGoogleAI => "gemini-2.5-flash-native-audio-preview-12-2025",

            _ => "gemini-2.5-flash-native-audio-preview-12-2025"
        };

        private string GetUrl(string projectId = "")
        {
            if (IsVertexModel())
            {
                // Vertex URL (us-central1-aiplatform...)
                IVALogger.Info(LogTag, string.Format(VERTEX_URL_TEMPLATE, VERTEX_PROJECT_LOCATION));
                return string.Format(VERTEX_URL_TEMPLATE, VERTEX_PROJECT_LOCATION);
            }
            else
            {
                // Standard URL
                string baseUrl = V1BETA_URL;
                return $"{baseUrl}?key={apiKey}";
            }
        }

        private void Awake()
        {
            // Only need API key if NOT using Vertex
            if (!IsVertexModel())
                apiKey = GeneralModelHelper.GetGeminiApiKey();
        }

        private void Update()
        {
            lock (_queueLock)
            {
                while (_mainThreadQueue.Count > 0) _mainThreadQueue.Dequeue().Invoke();
            }
        }

        /// <summary>
        /// Opens a Gemini Live session with the built-in <c>update_avatar_state</c> tool (and optionally
        /// <c>move_agent</c>). For custom tool schemas, use <see cref="ConnectWithDynamicToolsAsync"/>.
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
            _userRequestedDisconnect = false;
            _disconnectFired = false;

            await DisconnectInternalAsync(userRequested: false, fireEvent: false);

            var endpoint = await ResolveEndpointAsync();
            if (!endpoint.ok) return;

            if (!await OpenSocketAsync(endpoint.uri, endpoint.modelId)) return;

            await SendSetupWithGenericTool(endpoint.modelId, systemInstruction, voiceName, hasLocomotion);
        }

        /// <summary>
        /// Resolves the endpoint URI and the fully-qualified model id for the selected backend,
        /// acquiring a fresh Vertex OAuth token when needed. Both connect paths go through this, so
        /// the Vertex handshake is identical whether or not dynamic tools are in play.
        /// </summary>
        /// <remarks>
        /// Vertex needs two things the AI Studio path does not: an <c>Authorization: Bearer</c>
        /// header, and the model addressed by its full
        /// <c>projects/{project}/locations/{loc}/publishers/google/models/{model}</c> resource path.
        /// Omitting either leaves the socket open but the session never acknowledges setup, which
        /// surfaces to the user as a connect that hangs forever.
        /// </remarks>
        private async Task<(bool ok, string uri, string modelId)> ResolveEndpointAsync()
        {
            string modelId = GetModelString();

            if (IsVertexModel())
            {
                try
                {
                    IVALogger.Info(LogTag, "Authenticating with Vertex AI Service Account...");
                    // Looks in C:\Users\[USER]\.aiapi\service_account.json
                    var authResult = await VertexAuthHelper.GetAccessTokenFromUserDir("service_account.json");

                    this.accessToken = authResult.accessToken;

                    // Vertex requires FULL resource path for the model
                    modelId = $"projects/{authResult.projectId}/locations/{VERTEX_PROJECT_LOCATION}/publishers/google/models/{modelId}";
                    return (true, GetUrl(authResult.projectId), modelId);
                }
                catch (Exception e)
                {
                    FailFast(new AuthException(
                        "Vertex AI authentication failed. Check that ~/.aiapi/service_account.json exists, " +
                        "is a valid service-account key, and that the account has the 'Vertex AI User' role.", e),
                        DisconnectReason.AuthFailure);
                    return (false, null, null);
                }
            }

            if (string.IsNullOrEmpty(apiKey)) apiKey = GeneralModelHelper.GetGeminiApiKey();
            if (string.IsNullOrEmpty(apiKey))
            {
                FailFast(new AuthException(
                    "Gemini API key missing. Set it via 'IVA SDK / Setup Wizard / Credentials', which writes ~/.aiapi/auth.json."),
                    DisconnectReason.AuthFailure);
                return (false, null, null);
            }

            return (true, GetUrl(), modelId);
        }

        /// <summary>
        /// Opens the WebSocket to <paramref name="uri"/>, attaching the Vertex bearer header when
        /// required, and starts the receive loop. Returns false (and reports a fatal error) on failure.
        /// </summary>
        private async Task<bool> OpenSocketAsync(string uri, string modelId)
        {
            _webSocket = new ClientWebSocket();

            // --- HEADER INJECTION ---
            if (IsVertexModel())
            {
                _webSocket.Options.SetRequestHeader("Authorization", $"Bearer {accessToken}");
            }

            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                IVALogger.Info(LogTag, $"Connecting to {modelId} ...");
                await _webSocket.ConnectAsync(new Uri(uri), CancellationToken.None);
            }
            catch (Exception e)
            {
                FailFast(new ServiceConnectionException($"Could not open a Gemini Live session: {e.Message}", e),
                    DisconnectReason.NetworkError);
                return false;
            }

            _ = ReceiveLoop();
            _ = WatchForSetupAckAsync();
            return true;
        }

        /// <summary>
        /// Fails the session if the server accepts the socket but never sends <c>setupComplete</c>.
        /// A rejected setup message is not always answered with a close frame — the socket can simply
        /// sit open — and without this the agent waits forever on a session that will never start.
        /// </summary>
        private async Task WatchForSetupAckAsync()
        {
            _setupAcknowledged = false;
            var socket = _webSocket;

            try { await Task.Delay(TimeSpan.FromSeconds(setupTimeoutSeconds)); } catch { return; }

            // Only act on the session we were watching — a reconnect may have replaced it.
            if (_setupAcknowledged || _userRequestedDisconnect) return;
            if (socket == null || socket != _webSocket || socket.State != WebSocketState.Open) return;

            FailFast(new ServiceConnectionException(
                $"Gemini accepted the connection but did not acknowledge session setup within {setupTimeoutSeconds:0}s. " +
                (IsVertexModel()
                    ? "For Vertex, verify the service account has the 'Vertex AI User' role and that the Vertex AI API is enabled on the project."
                    : "Check the API key and whether the free-tier quota for this model is exhausted.")),
                DisconnectReason.ServerClosed);

            await DisconnectInternalAsync(userRequested: false, fireEvent: false);
        }

        /// <summary>
        /// Reports an unrecoverable connect failure on the main thread. Without this a failed
        /// handshake is only logged, and the calling agent waits on a <c>setupComplete</c> that will
        /// never arrive — the UI just sits on "Connecting...".
        /// </summary>
        private void FailFast(IVAException error, DisconnectReason reason)
        {
            IVALogger.Error(LogTag, error.Message, error);

            bool fireDisconnect = !_disconnectFired;
            _disconnectFired = true;

            EnqueueMainThread(() =>
            {
                if (fireDisconnect) OnDisconnected?.Invoke(reason);
                OnFatalError?.Invoke(error);
            });
        }

        /// <summary>Closes the WebSocket and releases all session state. Safe to call when already disconnected.</summary>
        public async Task DisconnectAsync()
        {
            _userRequestedDisconnect = true;
            await DisconnectInternalAsync(userRequested: true, fireEvent: true);
        }

        private async Task DisconnectInternalAsync(bool userRequested, bool fireEvent)
        {
            if (_webSocket != null)
            {
                _cancellationTokenSource?.Cancel();
                if (_sendLock.CurrentCount == 0) _sendLock.Release();

                try { await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None); }
                catch { }

                _webSocket.Dispose();
                _webSocket = null;
            }

            if (fireEvent && !_disconnectFired)
            {
                _disconnectFired = true;
                var reason = userRequested ? DisconnectReason.UserRequested : DisconnectReason.Unknown;
                EnqueueMainThread(() => OnDisconnected?.Invoke(reason));
            }
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
        private JObject BuildSetupContent(string model, string systemPrompt, string voice)
        {
            var generationConfig = new JObject();
            generationConfig["response_modalities"] = new JArray("AUDIO");

            // Gemini 2.5 runs *dynamic thinking by default*, which inserts a reasoning pause before
            // every spoken reply. In a realtime voice session that pause is the dominant source of
            // perceived latency, so we opt out on both backends unless the user asks for reasoning.
            if (disableThinking)
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
            generationConfig["speech_config"] = speechConfig;

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

            return setupContent;
        }

        private async Task SendSetupWithGenericTool(string model, string systemPrompt, string voice, bool hasLocomotion=false)
        {
            var setupContent = BuildSetupContent(model, systemPrompt, voice);

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
            setupContent["tools"] = toolsArray;

            var setupData = new JObject { ["setup"] = setupContent };

            if (verboseLogging) IVALogger.Info(LogTag, $"Sending Setup: {setupData.ToString(Formatting.None)}");

            await SendJsonAsync(setupData);
        }

        /// <summary>Sends a text turn to Gemini as if typed by the user. No-op when disconnected.</summary>
        public void SendTextMessage(string text)
        {
            if (!IsConnected) return;
            var msg = new { client_content = new { turns = new[] { new { role = "user", parts = new[] { new { text = text } } } }, turn_complete = true } };
            _ = SendJsonAsync(msg);
        }

        /// <summary>Streams a PCM audio chunk to Gemini. Expected format: 16-bit signed little-endian mono at 16 kHz.</summary>
        public void SendAudioChunk(byte[] pcmData)
        {
            if (!IsConnected) return;
            var msg = new { realtime_input = new { media_chunks = new[] { new { mime_type = "audio/pcm", data = Convert.ToBase64String(pcmData) } } } };
            _ = SendJsonAsync(msg);
        }

        /// <summary>Streams a JPEG-encoded image frame to Gemini for multimodal reasoning.</summary>
        public void SendImage(byte[] imageData)
        {
            if (!IsConnected) return;
            var msg = new { realtime_input = new { media_chunks = new[] { new { mime_type = "image/jpeg", data = Convert.ToBase64String(imageData) } } } };
            _ = SendJsonAsync(msg);
        }


        private async Task SendToolResponse(string id, string functionName = "update_avatar_state")
        {
            var msg = new { tool_response = new { function_responses = new[] { new { id = id, name = functionName, response = new { status = "ok" } } } } };
            await SendJsonAsync(msg);
        }

        private async Task SendJsonAsync(object data)
        {
            if (!IsConnected) return;
            
            await _sendLock.WaitAsync();
            try 
            {
                if (!IsConnected) return;
                string json = JsonConvert.SerializeObject(data, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                await _webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(json)), WebSocketMessageType.Text, true, CancellationToken.None); 
            }
            catch(Exception ex) { IVALogger.Error(LogTag, "Send Error", ex); }
            finally { _sendLock.Release(); }
        }

        private async Task ReceiveLoop()
        {
            var buffer = new byte[65536];
            DisconnectReason reason = DisconnectReason.Unknown;
            try
            {
                while (IsConnected && !_cancellationTokenSource.IsCancellationRequested)
                {
                    var ms = new System.IO.MemoryStream();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cancellationTokenSource.Token);
                        ms.Write(buffer, 0, result.Count);
                    } while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        IVALogger.Warn(LogTag, $"Server Closed Connection. Status: {result.CloseStatus}");
                        reason = DisconnectReason.ServerClosed;
                        break;
                    }

                    string jsonResponse = Encoding.UTF8.GetString(ms.ToArray());
                    ProcessMessage(jsonResponse);
                }
            }
            catch (Exception ex)
            {
                if (!_cancellationTokenSource.IsCancellationRequested)
                {
                    IVALogger.Info(LogTag, $"Receive Loop Stopped: {ex.Message}");
                    reason = DisconnectReason.NetworkError;
                }
                else
                {
                    reason = DisconnectReason.UserRequested;
                }
            }

            HandleReceiveLoopExit(reason);
        }

        private void HandleReceiveLoopExit(DisconnectReason reason)
        {
            if (_userRequestedDisconnect) return; // DisconnectAsync already fired the event

            if (!_disconnectFired)
            {
                _disconnectFired = true;
                EnqueueMainThread(() => OnDisconnected?.Invoke(reason));
            }

            if (autoReconnect && reason != DisconnectReason.UserRequested && reason != DisconnectReason.AuthFailure)
            {
                _ = TryReconnectAsync();
            }
        }

        private async Task TryReconnectAsync()
        {
            while (_reconnectAttempt < maxReconnectAttempts && !_userRequestedDisconnect)
            {
                _reconnectAttempt++;
                float delay = Mathf.Min(30f, reconnectBaseDelaySeconds * Mathf.Pow(2, _reconnectAttempt - 1));
                int attemptSnapshot = _reconnectAttempt;
                EnqueueMainThread(() => OnReconnecting?.Invoke(attemptSnapshot));
                IVALogger.Info(LogTag, $"Reconnect attempt {_reconnectAttempt}/{maxReconnectAttempts} in {delay:0.0}s");

                try { await Task.Delay(TimeSpan.FromSeconds(delay)); } catch { }
                if (_userRequestedDisconnect) return;

                try
                {
                    _disconnectFired = false; // Allow next disconnect cycle to fire cleanly
                    if (_usedDynamicToolsConnect)
                    {
                        await ConnectWithDynamicToolsAsync(_lastSystemInstruction, _lastVoiceName, _lastDynamicTools);
                    }
                    else
                    {
                        await ConnectAsync(_lastSystemInstruction, _lastVoiceName, _lastHasLocomotion);
                    }
                    return; // Success; _reconnectAttempt resets on setup ack
                }
                catch (Exception ex)
                {
                    IVALogger.Warn(LogTag, $"Reconnect attempt {_reconnectAttempt} failed: {ex.Message}");
                }
            }

            var fatal = new ServiceConnectionException(
                $"Reconnect retries exhausted after {_reconnectAttempt} attempts", _reconnectAttempt);
            EnqueueMainThread(() =>
            {
                OnDisconnected?.Invoke(DisconnectReason.RetriesExhausted);
                OnFatalError?.Invoke(fatal);
            });
        }

        private void ProcessMessage(string json)
        {
            try
            {
                var root = JObject.Parse(json);
                
                if (root["setupComplete"] != null || root["setup_complete"] != null)
                {
                    _setupAcknowledged = true;
                    _reconnectAttempt = 0; // Reset on successful setup
                    EnqueueMainThread(() =>
                    {
                        OnSetupComplete?.Invoke();
                        OnConnected?.Invoke();
                    });
                    return;
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

                                EnqueueMainThread(() => OnCommandReceived?.Invoke(act, emo, gaze));
                                _ = SendToolResponse(callId); 
                            }
                            else if (fnName == "move_agent")
                            {
                                var args = call["args"];
                                string dir = args?["direction"]?.ToString() ?? "Backward";
                                float angle = args["angle"]?.Value<float>() ?? 0f;
                                float distance = args["distance"]?.Value<float>() ?? 1.0f;
                                float speed = args["speed"]?.Value<float>() ?? 1.0f;
                                bool faceMovementDirection = args["faceMovementDirection"]?.Value<bool>() ?? true;

                                EnqueueMainThread(() => OnMoveCommand?.Invoke(angle, distance, speed, faceMovementDirection));
                                _ = SendToolResponse(callId, "move_agent");
                            }
                            else
                            {
                                JToken args = call["args"];
                                EnqueueMainThread(() => OnGenericToolCallReceived?.Invoke(callId, fnName, args));
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
                        EnqueueMainThread(() => OnServerInterrupted?.Invoke());
                    }

                    // Server-side transcription channels (only present when enableTranscription is true).
                    JToken outObj = serverContent["outputTranscription"] ?? serverContent["output_transcription"];
                    string outputTranscript = outObj?["text"]?.ToString();
                    if (!string.IsNullOrEmpty(outputTranscript))
                        EnqueueMainThread(() => OnTextReceived?.Invoke(outputTranscript));

                    JToken inObj = serverContent["inputTranscription"] ?? serverContent["input_transcription"];
                    string inputTranscript = inObj?["text"]?.ToString();
                    if (!string.IsNullOrEmpty(inputTranscript))
                    {
                        if (verboseLogging) IVALogger.Debug(LogTag, $"User transcript: {inputTranscript}");
                        EnqueueMainThread(() => OnUserTranscriptReceived?.Invoke(inputTranscript));
                    }

                    JToken parts = serverContent["modelTurn"]?["parts"] ?? serverContent["model_turn"]?["parts"];
                    if (parts != null)
                    {
                        foreach (var part in parts)
                        {
                            if (part["text"] != null) 
                                EnqueueMainThread(() => OnTextReceived?.Invoke(part["text"].ToString()));
                            
                            if (part["inlineData"] != null || part["inline_data"] != null)
                            {
                                JToken dataObj = part["inlineData"] ?? part["inline_data"];
                                byte[] audio = Convert.FromBase64String(dataObj["data"].ToString());
                                EnqueueMainThread(() => OnAudioReceived?.Invoke(audio));
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { if(verboseLogging) IVALogger.Warn(LogTag, $"Parse Error: {ex.Message}"); }
        }

        private void EnqueueMainThread(Action action) { lock (_queueLock) { _mainThreadQueue.Enqueue(action); } }

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
            _userRequestedDisconnect = false;
            _disconnectFired = false;

            await DisconnectInternalAsync(userRequested: false, fireEvent: false);

            // Shares ResolveEndpointAsync with ConnectAsync. Before v3.0.1 this path built its own
            // URI and skipped Vertex auth entirely, so any agent with a tool attached (e.g. the RAG
            // sample's search_knowledge) could not connect to Vertex at all.
            var endpoint = await ResolveEndpointAsync();
            if (!endpoint.ok) return;

            if (!await OpenSocketAsync(endpoint.uri, endpoint.modelId)) return;

            await SendSetupWithDynamicTools(endpoint.modelId, systemInstruction, voiceName, dynamicToolsDeclaration);
        }

        private async Task SendSetupWithDynamicTools(string model, string systemPrompt, string voice, JArray dynamicFunctionDeclarations)
        {
            var setupContent = BuildSetupContent(model, systemPrompt, voice);

            var toolsArray = new JArray();
            var toolWrapper = new JObject();
            var functionDeclarations = new JArray();

            functionDeclarations.Add(new JObject {
                ["name"] = "update_avatar_state",
                ["description"] = "Change the avatar's physical behavior.",
                ["parameters"] = JObject.Parse("{\"type\":\"object\",\"properties\":{\"action\":{\"type\":\"string\"},\"emotion\":{\"type\":\"string\"},\"gaze\":{\"type\":\"string\"}},\"required\":[\"action\",\"emotion\",\"gaze\"]}")
            });

            foreach(var dt in dynamicFunctionDeclarations) {
                functionDeclarations.Add(dt);
            }

            toolWrapper["function_declarations"] = functionDeclarations;
            toolsArray.Add(toolWrapper);
            setupContent["tools"] = toolsArray;

            var setupData = new JObject { ["setup"] = setupContent };

            if (verboseLogging) IVALogger.Info(LogTag, $"Sending Setup: {setupData.ToString(Newtonsoft.Json.Formatting.None)}");

            await SendJsonAsync(setupData);
        }
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
            await SendJsonAsync(msg);
        }

    }
}