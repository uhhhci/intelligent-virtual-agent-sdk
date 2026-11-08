using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IVH.Core.Exceptions;
using IVH.Core.ServiceConnector.LocalVoice;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace IVH.Core.ServiceConnector.Gemini.Realtime
{
    /// <summary>
    /// A realtime session with a <b>Local Voice Server</b> (speech recognition, language model and speech
    /// synthesis on this machine, cloned voices included), with the same events and methods as
    /// <see cref="GeminiRealtimeWrapper"/>, so an agent written against one runs against the other.
    /// </summary>
    /// <remarks>
    /// <para><b>Voices.</b> <c>voiceName</c> in <see cref="ConnectAsync"/> is a server voice id: a stock
    /// voice (<c>kristin</c>) or a cloned one (<c>v-frank-steinicke-12f548</c>). A Gemini voice name the
    /// server does not offer (e.g. <c>Puck</c>) falls back to <see cref="voiceId"/>, then to the server's
    /// default. Cloned and stock voices are chosen the same way; nothing in the SDK changes for a clone.</para>
    /// <para><b>Threads.</b> Events are raised on the main thread, in wire order. Audio is the exception
    /// for the agent's own playback: <see cref="Client"/> delivers it on the network thread straight into
    /// the playback buffer (see <c>LocalLiveAgent</c>); <see cref="OnAudioReceived"/> is raised on the main
    /// thread only when something subscribes to it.</para>
    /// <para><b>Images.</b> The server has no vision model. <see cref="SendImage"/> raises
    /// <see cref="OnFatalError"/>-free, documented <see cref="NotSupportedException"/> once per session
    /// (logged), never a silent drop.</para>
    /// <para><b>Tools.</b> <c>update_avatar_state</c> and <c>move_agent</c> are declared as with Gemini,
    /// and their calls arrive on <see cref="OnCommandReceived"/> / <see cref="OnMoveCommand"/>; the wrapper
    /// answers them itself, as the Gemini wrapper does. Until the server's Phase 2 tool round trip, a
    /// result is recorded in the history but the model does not continue its turn from it.</para>
    /// </remarks>
    public class LocalRealtimeWrapper : MonoBehaviour
    {
        [Header("Local Voice Server")]
        [Tooltip("ws://host:port/v1/realtime of the server (run it with .\\run.ps1 -Preset clone).")]
        public string serverUrl = LocalVoiceProtocol.DefaultUrl;

        [Tooltip("Voice used when the agent's voice name is not one the server offers. A cloned voice id starts with 'v-'.")]
        public string voiceId = "";

        [Tooltip("Cloned voices that carry their owner's speaking style use it (one habit on some replies).")]
        public bool speakingStyle = true;

        [Tooltip("Silence (ms) that ends the user's turn. 900 cuts people off less often than the server's 600.")]
        [Range(300, 2000)] public int endOfTurnPauseMs = 900;

        [Tooltip("Start the reply during the pause and drop it if the user goes on (the server's largest latency saving).")]
        public bool speculativeGeneration = true;

        [Tooltip("Language of the conversation (ISO 639-1).")]
        public string language = "en";

        [Header("Connection")]
        public bool autoReconnect = true;
        public bool verboseLogging = false;

        // ---- GeminiRealtimeWrapper's surface ----
        public Action OnSetupComplete;
        public Action<byte[]> OnAudioReceived;
        public Action<string> OnTextReceived;
        public Action<string> OnUserTranscriptReceived;
        public Action<string, string, string> OnCommandReceived;
        public Action<float, float, float, bool> OnMoveCommand;
        public Action<string, string, JToken> OnGenericToolCallReceived;
        public Action OnConnected;
        public Action<DisconnectReason> OnDisconnected;
        public Action<int> OnReconnecting;
        public Action<IVAException> OnFatalError;
        public Action OnServerInterrupted;

        // ---- what the local server adds ----
        /// <summary>listening / thinking / speaking / idle, with the response id (-1 when none).</summary>
        public Action<string, int> OnAgentState;
        public Action<int> OnResponseCreated;
        /// <summary>(response_id, reason): the server cancelled a response (barge-in, a client request, ...).</summary>
        public Action<int, string> OnResponseCancelled;
        /// <summary>(response_id, segment, text, start_sample): the words of the audio about to play, for subtitles.</summary>
        public Action<int, int, string, long> OnAudioSegment;
        public Action<int> OnResponseAudioDone;
        public Action<JObject> OnMetrics;
        /// <summary>Server-detected end of the user's speech, on this client's clock (ms; NaN without a clock offset).</summary>
        public Action<double> OnUserSpeechStopped;
        public Action OnUserSpeechStarted;

        public bool IsConnected => _client != null && _client.IsConnected;
        /// <summary>The underlying connection (audio events are raised on its network thread).</summary>
        public LocalVoiceClient Client => _client;
        /// <summary>The voices the server offered in the last <c>session.created</c>.</summary>
        public LocalVoiceInfo[] Voices => _client != null ? _client.Voices : new LocalVoiceInfo[0];
        /// <summary>The voice this session speaks.</summary>
        public string ActiveVoice { get; private set; }
        /// <summary>Output rate the server sends at (the audio device's rate).</summary>
        public int OutputSampleRate { get; private set; } = 24000;
        public int InputSampleRate { get; set; } = 16000;
        /// <summary>
        /// True: the model sets the avatar's state by writing an <c>[emotion=...; action=...; gaze=...]</c> tag
        /// instead of calling <c>update_avatar_state</c> (PROTOCOL v1.6 <c>inline_tool</c>); the tool is then not
        /// declared, and its calls still arrive on <see cref="OnCommandReceived"/>. Set before connecting; the
        /// system prompt must ask for the tag (<c>LocalLiveAgent</c> does).
        /// </summary>
        public bool AvatarStateByTag { get; set; }

        private LocalVoiceClient _client;
        private readonly Queue<Action> _main = new Queue<Action>();
        private readonly object _mainLock = new object();
        private bool _userDisconnect;
        private int _disconnectsLogged;
        private bool _imageWarned;
        private string _requestedVoice;
        private int _audioSubscribers => OnAudioReceived == null ? 0 : OnAudioReceived.GetInvocationList().Length;

        private void Update()
        {
            while (true)
            {
                Action a;
                lock (_mainLock) { if (_main.Count == 0) break; a = _main.Dequeue(); }
                try { a(); } catch (Exception e) { Debug.LogException(e); }
            }
        }

        private void Post(Action a) { lock (_mainLock) _main.Enqueue(a); }

        private void OnDestroy() { _ = DisconnectAsync(); }

        /// <summary>
        /// Opens a session: <paramref name="systemInstruction"/> becomes the server's system prompt, and
        /// <c>update_avatar_state</c> (plus <c>move_agent</c> when <paramref name="hasLocomotion"/>) is declared.
        /// </summary>
        public Task ConnectAsync(string systemInstruction, string voiceName, bool hasLocomotion = false) =>
            ConnectInternalAsync(systemInstruction, voiceName, BuildTools(!AvatarStateByTag, hasLocomotion, null));

        /// <summary>As <see cref="ConnectAsync"/>, with the caller's tool declarations (Gemini schema) added.</summary>
        public Task ConnectWithDynamicToolsAsync(string systemInstruction, string voiceName, JArray dynamicToolsDeclaration) =>
            ConnectInternalAsync(systemInstruction, voiceName, BuildTools(!AvatarStateByTag, false, dynamicToolsDeclaration));

        private async Task ConnectInternalAsync(string systemInstruction, string voiceName, JArray tools)
        {
            await DisconnectAsync();
            _userDisconnect = false;
            _requestedVoice = voiceName;
            OutputSampleRate = AudioSettings.outputSampleRate;
            _client = new LocalVoiceClient(serverUrl) { AutoReconnect = autoReconnect };
            _client.Config = new LocalVoiceSessionConfig
            {
                inputSampleRate = InputSampleRate,
                outputSampleRate = OutputSampleRate,
                systemPrompt = systemInstruction,
                language = language,
                speakingStyle = speakingStyle,
                inlineTool = AvatarStateByTag ? "update_avatar_state" : null,
                pauseMs = endOfTurnPauseMs,
                speculative = speculativeGeneration,
                // The agent detects a user speaking over it and cancels (as GeminiLiveAgent does), and it
                // mutes its microphone while speaking when asked to; the server must not second-guess it.
                bargeIn = "client",
                tools = tools,
                clientName = "LocalLiveAgent",
                clientPlatform = $"Unity {Application.unityVersion} {Application.platform}",
                clientVersion = "0.1.0",
            };
            Wire(_client);
            _client.Start();
        }

        private void Wire(LocalVoiceClient c)
        {
            c.SessionCreated += m =>
            {
                // Choose the voice on the network thread, before the first response can start: the
                // configure frame was already sent with the default, so update it now if needed.
                string voice = ResolveVoice(c.Voices, _requestedVoice);
                if (!string.IsNullOrEmpty(voice) && voice != c.Config.voice)
                {
                    c.Config.voice = voice;
                    _ = c.UpdateAsync(new LocalVoiceSessionConfig { voice = voice, language = null });
                }
                ActiveVoice = c.Config.voice;
                Post(() =>
                {
                    _disconnectsLogged = 0;
                Debug.Log($"[LocalRealtimeWrapper] session {c.SessionId} on {serverUrl}: voice {ActiveVoice}, output {c.OutputSampleRate} Hz");
                    OnSetupComplete?.Invoke();
                    OnConnected?.Invoke();
                });
            };
            c.Disconnected += (reason, detail) => Post(() =>
            {
                // The first failure is always logged with its cause; repeats only when verbose, so a server
                // that is down does not flood the Console while the client retries.
                if (verboseLogging || _disconnectsLogged++ == 0)
                    Debug.LogWarning($"[LocalRealtimeWrapper] {serverUrl}: {reason} {detail} (retrying: {autoReconnect && reason != LocalVoiceDisconnect.UserRequested && reason != LocalVoiceDisconnect.Refused})");
                OnDisconnected?.Invoke(Map(reason));
                if (reason == LocalVoiceDisconnect.Refused)
                    OnFatalError?.Invoke(new ServiceConnectionException($"The local voice server refused the session: {detail}"));
            });
            c.Reconnecting += (attempt, wait) => Post(() => OnReconnecting?.Invoke(attempt));
            c.ResponseCreated += id => Post(() => OnResponseCreated?.Invoke(id));
            c.TextDelta += (id, delta) => Post(() => OnTextReceived?.Invoke(delta));
            c.AudioSegment += (id, seg, text, start) => Post(() => OnAudioSegment?.Invoke(id, seg, text, start));
            c.AudioDone += (id, sent, dropped) => Post(() =>
            {
                if (dropped > 0) Debug.LogWarning($"[LocalRealtimeWrapper] response {id}: {dropped} of {sent} audio frames dropped by backpressure.");
                OnResponseAudioDone?.Invoke(id);
            });
            c.Audio += (rid, seq, frame, off, n) =>
            {
                if (_audioSubscribers == 0) return;
                var pcm = new byte[n];
                Buffer.BlockCopy(frame, off, pcm, 0, n);
                Post(() => OnAudioReceived?.Invoke(pcm));
            };
            c.ResponseCancelled += (id, reason) => Post(() =>
            {
                OnResponseCancelled?.Invoke(id, reason);
                if (reason == "barge_in") OnServerInterrupted?.Invoke();
            });
            c.AgentState += (state, id) => Post(() => OnAgentState?.Invoke(state, id));
            c.SpeechStarted += t => Post(() => OnUserSpeechStarted?.Invoke());
            c.SpeechStopped += t => { double ms = c.ServerNsToClientMs(t); Post(() => OnUserSpeechStopped?.Invoke(ms)); };
            c.TranscriptFinal += text => Post(() => OnUserTranscriptReceived?.Invoke(text));
            c.ToolCall += (rid, callId, name, args) => Post(() => HandleToolCall(callId, name, args));
            c.Metrics += m => Post(() => OnMetrics?.Invoke(m));
            c.Error += e => Post(() => Debug.LogWarning($"[LocalRealtimeWrapper] server error {e}"));
        }

        private static DisconnectReason Map(LocalVoiceDisconnect r)
        {
            switch (r)
            {
                case LocalVoiceDisconnect.UserRequested: return DisconnectReason.UserRequested;
                case LocalVoiceDisconnect.ServerClosed: return DisconnectReason.ServerClosed;
                case LocalVoiceDisconnect.Refused: return DisconnectReason.AuthFailure;
                default: return DisconnectReason.NetworkError;
            }
        }

        private string ResolveVoice(LocalVoiceInfo[] offered, string requested)
        {
            bool Offered(string v) => !string.IsNullOrEmpty(v) && Array.Exists(offered, o => o.Id == v);
            if (Offered(requested)) return requested;
            if (Offered(voiceId)) return voiceId;
            if (!string.IsNullOrEmpty(requested) || !string.IsNullOrEmpty(voiceId))
                Debug.LogWarning($"[LocalRealtimeWrapper] Voice '{requested}' / '{voiceId}' is not offered by the server; using its default. Offered: {string.Join(", ", Array.ConvertAll(offered, o => o.Id))}");
            return null;
        }

        private void HandleToolCall(string callId, string name, JObject args)
        {
            if (name == "update_avatar_state")
            {
                OnCommandReceived?.Invoke((string)args["action"] ?? "", (string)args["emotion"] ?? "", (string)args["gaze"] ?? "");
                // A tag's call (inline-...) expects no result: the history holds the tag as text.
                if (callId == null || !callId.StartsWith("inline-", StringComparison.Ordinal))
                    _ = _client.SendToolResultAsync(callId, new JObject { ["status"] = "ok" });
            }
            else if (name == "move_agent")
            {
                OnMoveCommand?.Invoke((float?)args["angle"] ?? 0f, (float?)args["distance"] ?? 1f, (float?)args["speed"] ?? 1f, (bool?)args["faceMovementDirection"] ?? true);
                _ = _client.SendToolResultAsync(callId, new JObject { ["status"] = "ok" });
            }
            else
            {
                OnGenericToolCallReceived?.Invoke(callId, name, args);
            }
        }

        /// <summary>Gemini's function declarations, translated to the server's tool schema (name, description, parameters).</summary>
        private static JArray BuildTools(bool avatarStateTool, bool hasLocomotion, JArray dynamicTools)
        {
            var tools = new JArray();
            if (avatarStateTool)
                tools.Add(new JObject
                {
                    ["name"] = "update_avatar_state",
                    ["description"] = "Change the avatar's facial expression, body action or gaze. Call it together with your spoken reply, never instead of it.",
                    ["parameters"] = JObject.Parse("{\"type\":\"object\",\"properties\":{\"action\":{\"type\":\"string\",\"description\":\"Body animation name, or 'none'\"},\"emotion\":{\"type\":\"string\",\"description\":\"Facial expression name, or 'none'\"},\"gaze\":{\"type\":\"string\",\"description\":\"'User' or 'Idle'\"}},\"required\":[\"action\",\"emotion\",\"gaze\"]}"),
                });
            if (hasLocomotion)
            {
                tools.Add(new JObject
                {
                    ["name"] = "move_agent",
                    ["description"] = "Move yourself in the 3D environment: angle (degrees, 0 = ahead, 90 = right), distance (m), speed (m/s), faceMovementDirection.",
                    ["parameters"] = JObject.Parse("{\"type\":\"object\",\"properties\":{\"angle\":{\"type\":\"number\"},\"distance\":{\"type\":\"number\"},\"speed\":{\"type\":\"number\"},\"faceMovementDirection\":{\"type\":\"boolean\"}},\"required\":[\"angle\",\"distance\",\"speed\",\"faceMovementDirection\"]}"),
                });
            }
            if (dynamicTools != null)
                foreach (var t in dynamicTools)
                    tools.Add(new JObject { ["name"] = t["name"], ["description"] = t["description"] ?? "", ["parameters"] = t["parameters"] ?? new JObject { ["type"] = "object" } });
            return tools;
        }

        /// <summary>Closes the session and stops reconnecting.</summary>
        public async Task DisconnectAsync()
        {
            _userDisconnect = true;
            var c = _client;
            _client = null;
            if (c != null) await c.StopAsync();
        }

        /// <summary>A typed user turn; it takes the same path as speech.</summary>
        public void SendTextMessage(string text)
        {
            if (_client == null) return;
            _ = _client.SendTextAsync(text);
        }

        /// <summary>PCM16 LE mono at <see cref="InputSampleRate"/> (16 kHz by default). Send continuously, silence included.</summary>
        public void SendAudioChunk(byte[] pcmData)
        {
            if (_client == null) return;
            _ = _client.SendAudioAsync(pcmData, 0, pcmData.Length & ~1);
        }

        /// <summary>Not supported: the local server has no vision model. Logged once per session, never dropped silently.</summary>
        public void SendImage(byte[] imageData)
        {
            if (_imageWarned) return;
            _imageWarned = true;
            Debug.LogWarning("[LocalRealtimeWrapper] SendImage is not supported by the local voice server (no vision model); images are not sent. Turn the agent's vision off for local agents.");
        }

        /// <summary>Answers a tool call from <see cref="OnGenericToolCallReceived"/>.</summary>
        public Task SendGenericToolResponseAsync(string id, string name, object responsePayload) =>
            _client == null ? Task.CompletedTask : _client.SendToolResultAsync(id, responsePayload == null ? null : JToken.FromObject(responsePayload));

        /// <summary>Stops the named response at the server (a client-side barge-in).</summary>
        public void CancelResponse(int responseId)
        {
            if (_client == null || responseId < 0) return;
            _ = _client.CancelAsync(responseId);
        }

        /// <summary>Switches voice from the next response on (a stock or cloned voice id).</summary>
        public void SetVoice(string voice)
        {
            if (_client == null) return;
            _client.Config.voice = voice;
            ActiveVoice = voice;
            _ = _client.UpdateAsync(new LocalVoiceSessionConfig { voice = voice, language = null });
        }
    }
}
