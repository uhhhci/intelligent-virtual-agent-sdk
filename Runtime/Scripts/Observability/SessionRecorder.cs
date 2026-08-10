using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;
using IVH.Core.Exceptions;
using IVH.Core.IntelligentVirtualAgent;
using IVH.Core.ServiceConnector.Gemini.Realtime;
using IVH.Core.Utils.Logging;

namespace IVH.Core.Observability
{
    /// <summary>
    /// Observability layer for realtime sessions. Attach to the same GameObject as a
    /// <see cref="GeminiRealtimeWrapper"/> to capture timestamped events + latency stats to a JSONL
    /// file, plus a session summary on end. Opt-in via <see cref="SessionRecorderConfig"/>.
    /// </summary>
    /// <remarks>
    /// Event shape matches the <see cref="SessionEvent"/> contract. All file I/O runs on a background
    /// task so Unity's main thread stays free.
    /// </remarks>
    [RequireComponent(typeof(GeminiRealtimeWrapper))]
    public class SessionRecorder : MonoBehaviour
    {
        [Tooltip("Config asset. Leave empty to use defaults (disabled).")]
        public SessionRecorderConfig config;

        private GeminiRealtimeWrapper _wrapper;
        private GeminiLiveAgent _liveAgent; // optional — only for interruption events
        private readonly MetricsCollector _metrics = new MetricsCollector();
        private readonly ConcurrentQueue<SessionEvent> _queue = new ConcurrentQueue<SessionEvent>();
        private string _sessionId;
        private DateTime _startedAtUtc;
        private StreamWriter _writer;
        private Task _writeTask;
        private volatile bool _running;

        private void Awake()
        {
            _wrapper = GetComponent<GeminiRealtimeWrapper>();
            if (!IsEnabled) return;

            _sessionId = Guid.NewGuid().ToString("N").Substring(0, 12);
            _startedAtUtc = DateTime.UtcNow;

            try { OpenWriter(); }
            catch (Exception ex)
            {
                IVALogger.Error("SessionRecorder", "Failed to open log file", ex);
                return;
            }

            SubscribeToWrapper();

            _liveAgent = GetComponent<GeminiLiveAgent>();
            if (_liveAgent != null)
            {
                _liveAgent.OnAgentInterrupted += HandleAgentInterrupted;
            }

            _running = true;
            _writeTask = Task.Run(WriteLoop);

            Emit(new SessionEvent { type = "session_start", model = _wrapper.selectedModel.ToString() });
        }

        private void OnDisable() => Shutdown();
        private void OnDestroy() => Shutdown();

        private bool IsEnabled => config != null && config.enabled;

        private void OpenWriter()
        {
            string dir = !string.IsNullOrEmpty(config.outputDirectory)
                ? config.outputDirectory
                : Path.Combine(Application.persistentDataPath, "IVA", "sessions");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, $"{_startedAtUtc:yyyyMMdd_HHmmss}_{_sessionId}.jsonl");
            _writer = new StreamWriter(path, append: false, encoding: Encoding.UTF8);
            IVALogger.Info("SessionRecorder", $"Recording session to {path}");
        }

        private void SubscribeToWrapper()
        {
            _wrapper.OnConnected += HandleConnected;
            _wrapper.OnDisconnected += HandleDisconnected;
            _wrapper.OnReconnecting += HandleReconnecting;
            _wrapper.OnFatalError += HandleFatal;
            _wrapper.OnTextReceived += HandleText;
            _wrapper.OnUserTranscriptReceived += HandleUserTranscript;
            _wrapper.OnAudioReceived += HandleAudio;
            _wrapper.OnCommandReceived += HandleCommand;
            _wrapper.OnMoveCommand += HandleMove;
            _wrapper.OnGenericToolCallReceived += HandleGenericTool;
        }

        private void UnsubscribeFromWrapper()
        {
            if (_wrapper == null) return;
            _wrapper.OnConnected -= HandleConnected;
            _wrapper.OnDisconnected -= HandleDisconnected;
            _wrapper.OnReconnecting -= HandleReconnecting;
            _wrapper.OnFatalError -= HandleFatal;
            _wrapper.OnTextReceived -= HandleText;
            _wrapper.OnUserTranscriptReceived -= HandleUserTranscript;
            _wrapper.OnAudioReceived -= HandleAudio;
            _wrapper.OnCommandReceived -= HandleCommand;
            _wrapper.OnMoveCommand -= HandleMove;
            _wrapper.OnGenericToolCallReceived -= HandleGenericTool;
        }

        private void HandleConnected() => Emit(new SessionEvent { type = "connected" });
        private void HandleDisconnected(DisconnectReason reason) => Emit(new SessionEvent { type = "disconnected", reason = reason.ToString() });

        private void HandleAgentInterrupted(InterruptionInfo info)
        {
            if (info == null) return;
            _metrics.RecordInterruption();
            Emit(new SessionEvent
            {
                type = "interruption",
                speaker = "user",
                reason = $"{info.source}:{info.reason}",
                energy = info.magnitude,
                text = (config != null && config.scrubText) ? null : info.lastAgentFragment,
            });
        }
        private void HandleReconnecting(int attempt)
        {
            _metrics.RecordReconnect();
            Emit(new SessionEvent { type = "reconnecting", latency_ms = attempt });
        }
        private void HandleFatal(IVAException ex)
        {
            _metrics.RecordError();
            Emit(new SessionEvent { type = "fatal_error", reason = ex.Message });
        }
        private void HandleText(string text)
        {
            if (config.recordTurnEvents) _metrics.RecordTurn();
            Emit(new SessionEvent { type = "text", speaker = "agent", text = config.scrubText ? null : text });
        }
        private void HandleUserTranscript(string text)
        {
            if (config.recordTurnEvents) _metrics.RecordTurn();
            Emit(new SessionEvent { type = "text", speaker = "user", text = config.scrubText ? null : text });
        }
        private void HandleAudio(byte[] audio) => Emit(new SessionEvent { type = "audio", speaker = "agent", bytes = audio.Length });
        private void HandleCommand(string action, string emotion, string gaze)
        {
            _metrics.RecordToolCall();
            Emit(new SessionEvent { type = "tool_call", name = "update_avatar_state", args = $"{{\"action\":\"{action}\",\"emotion\":\"{emotion}\",\"gaze\":\"{gaze}\"}}" });
        }
        private void HandleMove(float angle, float distance, float speed, bool face)
        {
            _metrics.RecordToolCall();
            Emit(new SessionEvent { type = "tool_call", name = "move_agent", args = $"{{\"angle\":{angle},\"distance\":{distance},\"speed\":{speed},\"faceMovementDirection\":{face.ToString().ToLower()}}}" });
        }
        private void HandleGenericTool(string callId, string toolName, Newtonsoft.Json.Linq.JToken args)
        {
            _metrics.RecordToolCall();
            Emit(new SessionEvent
            {
                type = "tool_call",
                name = toolName,
                args = config.scrubText ? null : (args != null ? args.ToString(Formatting.None) : null),
            });
        }

        /// <summary>Public hook for user code to record an interruption event.</summary>
        public void RecordInterruption(string reason = null, float energy = 0f)
        {
            if (!_running) return;
            _metrics.RecordInterruption();
            Emit(new SessionEvent { type = "interruption", reason = reason, energy = energy });
        }

        /// <summary>Public hook for user code to record a VAD decision (e.g. speech vs silence).</summary>
        public void RecordVadDecision(float energy, bool isSpeech)
        {
            if (!_running || config == null || !config.recordVadDecisions) return;
            Emit(new SessionEvent { type = "vad_decision", energy = energy, classified = isSpeech ? "speech" : "silence" });
        }

        /// <summary>Public hook for user code to record an emotion-analysis result.</summary>
        public void RecordUserEmotion(float valence, float arousal)
        {
            if (!_running) return;
            Emit(new SessionEvent { type = "user_emotion", valence = valence, arousal = arousal });
        }

        private void Emit(SessionEvent evt)
        {
            if (!_running) return;
            evt.t = DateTime.UtcNow.ToString("o");
            evt.session = _sessionId;
            _queue.Enqueue(evt);
        }

        private async Task WriteLoop()
        {
            while (_running || !_queue.IsEmpty)
            {
                if (_queue.TryDequeue(out var evt))
                {
                    try
                    {
                        string line = JsonConvert.SerializeObject(evt, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                        await _writer.WriteLineAsync(line);
                    }
                    catch (Exception ex)
                    {
                        IVALogger.Warn("SessionRecorder", $"Write error: {ex.Message}");
                    }
                }
                else
                {
                    await Task.Delay(50);
                }
            }
            try { await _writer.FlushAsync(); } catch { }
        }

        private void Shutdown()
        {
            if (!_running) return;
            Emit(new SessionEvent { type = "session_end" });
            _running = false;
            UnsubscribeFromWrapper();

            if (_liveAgent != null)
            {
                _liveAgent.OnAgentInterrupted -= HandleAgentInterrupted;
                _liveAgent = null;
            }

            try { _writeTask?.Wait(2000); } catch { }
            try { _writer?.Flush(); _writer?.Dispose(); _writer = null; } catch { }

            TryWriteSummary();
        }

        private void TryWriteSummary()
        {
            if (config == null) return;
            try
            {
                string dir = !string.IsNullOrEmpty(config.outputDirectory)
                    ? config.outputDirectory
                    : Path.Combine(Application.persistentDataPath, "IVA", "sessions");
                string path = Path.Combine(dir, $"{_startedAtUtc:yyyyMMdd_HHmmss}_{_sessionId}.summary.json");
                string json = JsonConvert.SerializeObject(
                    _metrics.BuildSummary(_sessionId, _startedAtUtc, DateTime.UtcNow),
                    Formatting.Indented);
                File.WriteAllText(path, json);
                IVALogger.Info("SessionRecorder", $"Summary written to {path}");
            }
            catch (Exception ex)
            {
                IVALogger.Warn("SessionRecorder", $"Could not write summary: {ex.Message}");
            }
        }
    }
}
