using System;
using System.Diagnostics;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace IVH.Core.ServiceConnector.LocalVoice
{
    /// <summary>
    /// A realtime session with a Local Voice Server (<c>ws://…/v1/realtime</c>, protocol v1.5). Plain C#:
    /// it runs in Unity, in a .NET test, anywhere.
    /// </summary>
    /// <remarks>
    /// <para><b>Threads.</b> Every event is raised on the receive thread, in wire order, so audio can go
    /// straight into the playback buffer without a frame's delay. A Unity caller marshals the rest to the
    /// main thread itself (<c>LocalRealtimeWrapper</c> does).</para>
    /// <para><b>Reconnect.</b> One loop owns the connection: connect, read until the socket ends, wait,
    /// connect again, with the wait doubling from <see cref="ReconnectMinDelay"/> to
    /// <see cref="ReconnectMaxDelay"/> and reset by a successful <c>session.created</c>. There is exactly
    /// one timer, so a server that is down cannot multiply connection attempts (the browser page once
    /// doubled its loops on every failure and filled all eight session slots; the server's PHASE4.md
    /// §8.5). After a reconnect the last configuration is sent again, so the session resumes as it was,
    /// minus its history (the server keeps history per connection).</para>
    /// <para><b>Clock.</b> Pings carry this client's monotonic milliseconds; the pong's server time gives
    /// the offset between the two clocks (server minus client), so a server timestamp such as the end of
    /// the user's speech can be placed on this client's clock (<see cref="ServerNsToClientMs"/>).</para>
    /// </remarks>
    public sealed class LocalVoiceClient : IDisposable
    {
        public Uri Uri { get; }
        public string ApiKey { get; set; }
        public TimeSpan ReconnectMinDelay { get; set; } = TimeSpan.FromSeconds(1);
        public TimeSpan ReconnectMaxDelay { get; set; } = TimeSpan.FromSeconds(15);
        public TimeSpan PingInterval { get; set; } = TimeSpan.FromSeconds(5);
        public bool AutoReconnect { get; set; } = true;

        /// <summary>Configuration sent after every <c>session.created</c>, including after a reconnect.</summary>
        public LocalVoiceSessionConfig Config { get; set; } = new LocalVoiceSessionConfig();

        public bool IsConnected => _ws != null && _ws.State == WebSocketState.Open && _sessionCreated;
        public string SessionId { get; private set; }
        public int OutputSampleRate { get; private set; } = 24000;
        public LocalVoiceInfo[] Voices { get; private set; } = new LocalVoiceInfo[0];
        public JObject LastSessionCreated { get; private set; }
        /// <summary>Round trip of the last ping, ms (NaN before the first pong).</summary>
        public double RttMs { get; private set; } = double.NaN;
        /// <summary>Server monotonic ms minus client monotonic ms, from the last ping (NaN before the first pong).</summary>
        public double ClockOffsetMs { get; private set; } = double.NaN;
        public int ConnectAttempts { get; private set; }
        public int SessionsCreated { get; private set; }

        // ---- events (receive thread) ----
        public event Action<JObject> SessionCreated;
        public event Action<LocalVoiceDisconnect, string> Disconnected;
        public event Action<int, TimeSpan> Reconnecting;
        public event Action<int> ResponseCreated;
        public event Action<int, string> TextDelta;
        public event Action<int, string> TextDone;
        /// <summary>(response_id, seq, frame, byteOffset, byteCount): the PCM16 after the 8-byte header.</summary>
        public event Action<uint, uint, byte[], int, int> Audio;
        /// <summary>(response_id, segment, text, start_sample).</summary>
        public event Action<int, int, string, long> AudioSegment;
        /// <summary>(response_id, frames_sent, frames_dropped).</summary>
        public event Action<int, int, int> AudioDone;
        public event Action<int, string> ResponseCancelled;
        /// <summary>(state, response_id or -1).</summary>
        public event Action<string, int> AgentState;
        /// <summary>Server monotonic ns of the acoustic onset.</summary>
        public event Action<long> SpeechStarted;
        /// <summary>Server monotonic ns of the acoustic offset.</summary>
        public event Action<long> SpeechStopped;
        public event Action<string> TranscriptPartial;
        public event Action<string> TranscriptFinal;
        /// <summary>(response_id, call_id, name, args).</summary>
        public event Action<int, string, string, JObject> ToolCall;
        public event Action<JObject> Metrics;
        public event Action<LocalVoiceError> Error;
        /// <summary>Any other text frame, for frames this client does not know yet.</summary>
        public event Action<JObject> OtherMessage;

        private ClientWebSocket _ws;
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private CancellationTokenSource _life;
        private Task _loop;
        private volatile bool _sessionCreated;
        private int _pingId;
        private static readonly Stopwatch Mono = Stopwatch.StartNew();

        public LocalVoiceClient(string url = LocalVoiceProtocol.DefaultUrl)
        {
            Uri = new Uri(url);
        }

        /// <summary>This process's monotonic clock, ms (the clock every client-side measurement uses).</summary>
        public static double NowMs => Mono.Elapsed.TotalMilliseconds;

        /// <summary>A server <c>t_ns</c> on this client's clock (ms), or NaN without a ping round yet.</summary>
        public double ServerNsToClientMs(long serverNs) => double.IsNaN(ClockOffsetMs) ? double.NaN : serverNs / 1e6 - ClockOffsetMs;

        /// <summary>Starts the connection loop. Returns once the first <c>session.created</c> arrived, or throws after <paramref name="firstConnectTimeout"/>.</summary>
        public async Task StartAsync(TimeSpan? firstConnectTimeout = null)
        {
            if (_loop != null) throw new InvalidOperationException("already started");
            _life = new CancellationTokenSource();
            var first = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Action<JObject> onFirst = _ => first.TrySetResult(true);
            SessionCreated += onFirst;
            _loop = Task.Run(() => RunAsync(_life.Token));
            var timeout = Task.Delay(firstConnectTimeout ?? TimeSpan.FromSeconds(10));
            var done = await Task.WhenAny(first.Task, timeout).ConfigureAwait(false);
            SessionCreated -= onFirst;
            if (done != first.Task)
                throw new TimeoutException($"No session from {Uri} within {(firstConnectTimeout ?? TimeSpan.FromSeconds(10)).TotalSeconds:0} s (is the server running?). Still retrying in the background.");
        }

        /// <summary>Starts the loop without waiting for the first session.</summary>
        public void Start()
        {
            if (_loop != null) return;
            _life = new CancellationTokenSource();
            _loop = Task.Run(() => RunAsync(_life.Token));
        }

        public async Task StopAsync()
        {
            if (_life == null) return;
            _life.Cancel();
            var ws = _ws;
            if (ws != null && ws.State == WebSocketState.Open)
            {
                try
                {
                    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2)))
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", cts.Token).ConfigureAwait(false);
                }
                catch { }
            }
            try { if (_loop != null) await _loop.ConfigureAwait(false); } catch { }
            _loop = null;
            _life = null;
        }

        public void Dispose()
        {
            try { StopAsync().Wait(TimeSpan.FromSeconds(3)); } catch { }
        }

        private async Task RunAsync(CancellationToken life)
        {
            TimeSpan delay = ReconnectMinDelay;
            int attempt = 0;
            while (!life.IsCancellationRequested)
            {
                ConnectAttempts++;
                LocalVoiceDisconnect reason = LocalVoiceDisconnect.NetworkError;
                string detail = null;
                bool hadSession = false;
                using (var ws = new ClientWebSocket())
                {
                    _ws = ws;
                    _sessionCreated = false;
                    if (!string.IsNullOrEmpty(ApiKey)) ws.Options.SetRequestHeader("Authorization", "Bearer " + ApiKey);
                    try
                    {
                        using (var cts = CancellationTokenSource.CreateLinkedTokenSource(life))
                        {
                            cts.CancelAfter(TimeSpan.FromSeconds(5));
                            await ws.ConnectAsync(Uri, cts.Token).ConfigureAwait(false);
                        }
                        _ = PingLoopAsync(ws, life);
                        (reason, detail) = await ReceiveLoopAsync(ws, life).ConfigureAwait(false);
                        hadSession = _sessionCreated;
                    }
                    catch (OperationCanceledException) when (life.IsCancellationRequested)
                    {
                        reason = LocalVoiceDisconnect.UserRequested;
                    }
                    catch (Exception e)
                    {
                        reason = LocalVoiceDisconnect.NetworkError;
                        detail = e.Message;
                    }
                    _sessionCreated = false;
                    _ws = null;
                }

                if (life.IsCancellationRequested) reason = LocalVoiceDisconnect.UserRequested;
                SafeInvoke(() => Disconnected?.Invoke(reason, detail));
                if (reason == LocalVoiceDisconnect.UserRequested || reason == LocalVoiceDisconnect.Refused || !AutoReconnect) return;

                if (hadSession) { delay = ReconnectMinDelay; attempt = 0; }
                attempt++;
                var wait = delay;
                SafeInvoke(() => Reconnecting?.Invoke(attempt, wait));
                try { await Task.Delay(wait, life).ConfigureAwait(false); } catch (OperationCanceledException) { return; }
                delay = TimeSpan.FromMilliseconds(Math.Min(ReconnectMaxDelay.TotalMilliseconds, delay.TotalMilliseconds * 2));
            }
        }

        private async Task<(LocalVoiceDisconnect, string)> ReceiveLoopAsync(ClientWebSocket ws, CancellationToken life)
        {
            var buffer = new byte[64 * 1024];
            var ms = new MemoryStream();
            while (ws.State == WebSocketState.Open)
            {
                ms.SetLength(0);
                WebSocketReceiveResult r;
                do
                {
                    r = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), life).ConfigureAwait(false);
                    if (r.MessageType == WebSocketMessageType.Close)
                    {
                        // 1008 (policy) and 1013 (full) are refusals; retrying 1008 cannot help.
                        var status = r.CloseStatus ?? WebSocketCloseStatus.NormalClosure;
                        var reason = (int)status == 1008 ? LocalVoiceDisconnect.Refused : LocalVoiceDisconnect.ServerClosed;
                        try { await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None).ConfigureAwait(false); } catch { }
                        return (reason, $"{(int)status} {r.CloseStatusDescription}");
                    }
                    ms.Write(buffer, 0, r.Count);
                } while (!r.EndOfMessage);

                byte[] bytes = ms.GetBuffer();
                int len = (int)ms.Length;
                if (r.MessageType == WebSocketMessageType.Binary)
                {
                    if (LocalVoiceProtocol.TryReadAudioHeader(bytes, len, out uint rid, out uint seq))
                    {
                        // A copy: the receive buffer is reused for the next frame.
                        var frame = new byte[len];
                        Buffer.BlockCopy(bytes, 0, frame, 0, len);
                        SafeInvoke(() => Audio?.Invoke(rid, seq, frame, LocalVoiceProtocol.AudioHeaderBytes, len - LocalVoiceProtocol.AudioHeaderBytes));
                    }
                }
                else
                {
                    string json = Encoding.UTF8.GetString(bytes, 0, len);
                    JObject msg;
                    try { msg = JObject.Parse(json); } catch { continue; }
                    await DispatchAsync(msg).ConfigureAwait(false);
                }
            }
            return (LocalVoiceDisconnect.ServerClosed, ws.CloseStatusDescription);
        }

        private async Task DispatchAsync(JObject m)
        {
            string type = (string)m["type"];
            int rid = (int?)m["response_id"] ?? -1;
            switch (type)
            {
                case "session.created":
                    if (((int?)m["protocol_version"] ?? LocalVoiceProtocol.Version) != LocalVoiceProtocol.Version)
                    {
                        SafeInvoke(() => Error?.Invoke(new LocalVoiceError { Code = "protocol.unsupported_version", Message = "server speaks protocol " + m["protocol_version"] }));
                        // A client MUST close rather than guess (PROTOCOL header). Cancel the loop from
                        // inside it: awaiting StopAsync here would wait for this very loop.
                        _life?.Cancel();
                        return;
                    }
                    SessionId = (string)m["session_id"];
                    OutputSampleRate = (int?)m["output_sample_rate"] ?? 24000;
                    var infos = m["voice_info"] as JArray;
                    if (infos != null)
                    {
                        var list = new LocalVoiceInfo[infos.Count];
                        for (int i = 0; i < infos.Count; i++) list[i] = LocalVoiceInfo.FromJson(infos[i]);
                        Voices = list;
                    }
                    LastSessionCreated = m;
                    // Configure before anyone hears of the session, so the first thing they send is
                    // already under this session's rates and voice.
                    await SendJsonAsync(Config.ToMessage()).ConfigureAwait(false);
                    if (Config.outputSampleRate > 0) OutputSampleRate = Config.outputSampleRate;
                    _sessionCreated = true;
                    SessionsCreated++;
                    _ = SendPingAsync();
                    SafeInvoke(() => SessionCreated?.Invoke(m));
                    break;
                case "response.created": SafeInvoke(() => ResponseCreated?.Invoke(rid)); break;
                case "response.text.delta": SafeInvoke(() => TextDelta?.Invoke(rid, (string)m["delta"] ?? "")); break;
                case "response.text.done": SafeInvoke(() => TextDone?.Invoke(rid, (string)m["text"] ?? "")); break;
                case "response.audio.segment":
                    SafeInvoke(() => AudioSegment?.Invoke(rid, (int?)m["segment"] ?? 0, (string)m["text"] ?? "", (long?)m["start_sample"] ?? 0));
                    break;
                case "response.audio.done":
                    SafeInvoke(() => AudioDone?.Invoke(rid, (int?)m["frames_sent"] ?? 0, (int?)m["frames_dropped"] ?? 0));
                    break;
                case "response.cancelled": SafeInvoke(() => ResponseCancelled?.Invoke(rid, (string)m["reason"] ?? "")); break;
                case "agent.state": SafeInvoke(() => AgentState?.Invoke((string)m["state"] ?? "", rid)); break;
                case "input.speech_started": SafeInvoke(() => SpeechStarted?.Invoke((long?)m["t_ns"] ?? 0)); break;
                case "input.speech_stopped": SafeInvoke(() => SpeechStopped?.Invoke((long?)m["t_ns"] ?? 0)); break;
                case "transcript.partial": SafeInvoke(() => TranscriptPartial?.Invoke((string)m["text"] ?? "")); break;
                case "transcript.final": SafeInvoke(() => TranscriptFinal?.Invoke((string)m["text"] ?? "")); break;
                case "response.tool_call":
                    SafeInvoke(() => ToolCall?.Invoke(rid, (string)m["call_id"], (string)m["name"], m["args"] as JObject ?? new JObject()));
                    break;
                case "metrics": SafeInvoke(() => Metrics?.Invoke(m)); break;
                case "pong": OnPong(m); break;
                case "error":
                    SafeInvoke(() => Error?.Invoke(new LocalVoiceError { Code = (string)m["code"], Message = (string)m["message"], Retryable = (bool?)m["retryable"] ?? false }));
                    break;
                default: SafeInvoke(() => OtherMessage?.Invoke(m)); break;
            }
        }

        private void OnPong(JObject m)
        {
            double now = NowMs;
            double? sent = (double?)m["client_t"];
            long? tns = (long?)m["t_ns"];
            if (sent == null || tns == null) return;
            RttMs = now - sent.Value;
            ClockOffsetMs = tns.Value / 1e6 - (sent.Value + now) / 2.0;
        }

        private async Task PingLoopAsync(ClientWebSocket ws, CancellationToken life)
        {
            try
            {
                while (!life.IsCancellationRequested && ws.State == WebSocketState.Open)
                {
                    await Task.Delay(PingInterval, life).ConfigureAwait(false);
                    if (_sessionCreated) await SendPingAsync().ConfigureAwait(false);
                }
            }
            catch { }
        }

        /// <summary>Sends a <c>ping</c> carrying this client's clock; the pong updates <see cref="RttMs"/> and <see cref="ClockOffsetMs"/>.</summary>
        public Task SendPingAsync() =>
            SendJsonAsync(new JObject { ["type"] = "ping", ["id"] = Interlocked.Increment(ref _pingId), ["client_t"] = NowMs });

        // ---- client -> server ----

        /// <summary>Changes voice, prompt, tools or turn settings mid-session (takes effect from the next response).</summary>
        public Task UpdateAsync(LocalVoiceSessionConfig changes) => SendJsonAsync(changes.ToMessage("session.update"));

        public Task SendTextAsync(string text) => SendJsonAsync(new JObject { ["type"] = "input_text.send", ["text"] = text });

        public Task CommitAsync() => SendJsonAsync(new JObject { ["type"] = "input_audio.commit" });

        public Task CancelAsync(int responseId) => SendJsonAsync(new JObject { ["type"] = "response.cancel", ["response_id"] = responseId });

        public Task SendToolResultAsync(string callId, JToken result, bool isError = false) =>
            SendJsonAsync(new JObject { ["type"] = "tool.result", ["call_id"] = callId, ["result"] = result ?? JValue.CreateNull(), ["is_error"] = isError });

        public Task SendPlaybackStartedAsync(int responseId, double outputLatencyMs, double? eosToAudibleMs, string eosSource)
        {
            var m = new JObject { ["type"] = "playback_started", ["response_id"] = responseId, ["output_latency_ms"] = outputLatencyMs };
            if (eosToAudibleMs.HasValue && !double.IsNaN(eosToAudibleMs.Value)) m["eos_to_audible_ms"] = eosToAudibleMs.Value;
            if (!double.IsNaN(RttMs)) m["rtt_ms"] = RttMs;
            if (!double.IsNaN(ClockOffsetMs)) m["clock_offset_ms"] = ClockOffsetMs;
            if (!string.IsNullOrEmpty(eosSource)) m["eos_source"] = eosSource;
            return SendJsonAsync(m);
        }

        /// <summary>PCM16 mono at the configured input rate; an even byte count (PROTOCOL §3). Dropped while disconnected.</summary>
        public async Task SendAudioAsync(byte[] pcm16, int offset, int count)
        {
            if ((count & 1) != 0) throw new ArgumentException("PCM16 audio must be an even number of bytes", nameof(count));
            var ws = _ws;
            if (ws == null || !_sessionCreated || ws.State != WebSocketState.Open) return;
            await _sendLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (ws.State == WebSocketState.Open)
                    await ws.SendAsync(new ArraySegment<byte>(pcm16, offset, count), WebSocketMessageType.Binary, true, CancellationToken.None).ConfigureAwait(false);
            }
            catch { /* the receive loop notices a dead socket and reconnects */ }
            finally { _sendLock.Release(); }
        }

        /// <summary>Sends one JSON message. Dropped (false) while no socket is open.</summary>
        public async Task<bool> SendJsonAsync(JObject message)
        {
            var ws = _ws;
            if (ws == null || ws.State != WebSocketState.Open) return false;
            byte[] bytes = Encoding.UTF8.GetBytes(message.ToString(Formatting.None));
            await _sendLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (ws.State != WebSocketState.Open) return false;
                await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
                return true;
            }
            catch { return false; }
            finally { _sendLock.Release(); }
        }

        private static void SafeInvoke(Action a)
        {
            try { a(); }
            catch (Exception e) { Trace.WriteLine("[LocalVoiceClient] event handler threw: " + e); }
        }
    }
}
