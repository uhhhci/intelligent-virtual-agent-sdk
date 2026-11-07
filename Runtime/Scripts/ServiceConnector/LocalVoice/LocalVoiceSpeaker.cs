using System;
using IVH.Core.ServiceConnector.Gemini.Realtime;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace IVH.Core.ServiceConnector.LocalVoice
{
    /// <summary>
    /// The hybrid agent's voice: put this next to a <c>GeminiLiveAgent</c> and Gemini Live keeps the
    /// microphone, turn-taking, interruptions, persona, tools and knowledge (RAG), while a <b>Local Voice
    /// Server</b> speaks Gemini's words in a voice Gemini does not have, e.g. a cloned one.
    /// </summary>
    /// <remarks>
    /// <para><b>Where the words come from.</b> Gemini's reply text arrives on
    /// <see cref="GeminiRealtimeWrapper.OnTextReceived"/>: the output transcription of its audio
    /// (response modality AUDIO with transcription on, which every Live model supports), or the text itself
    /// (modality TEXT, where the model accepts it). Either way it is streamed to the server as
    /// <c>speak.text</c> (PROTOCOL v1.5) and closed at Gemini's <c>turnComplete</c>. Gemini's own audio is
    /// not played.</para>
    /// <para><b>Playback</b> is the local agent's: one ring buffer on the avatar's AudioSource, handed to its
    /// <c>OVRLipSyncContext</c>, so CC4 and Avatar SDK avatars lip-sync to the cloned voice exactly as they
    /// do to Gemini's.</para>
    /// <para><b>Interruptions.</b> Gemini's <c>interrupted</c> (and the agent's own voice detection) cancel
    /// the local response by its id and drop its buffered audio at once.</para>
    /// <para>Start the server with <c>.\run.ps1 -Preset clone-tts</c> (speech only: no recogniser or
    /// language model on the GPU).</para>
    /// </remarks>
    public class LocalVoiceSpeaker : MonoBehaviour
    {
        [Tooltip("ws://host:port/v1/realtime of the local voice server (run it with .\\run.ps1 -Preset clone-tts).")]
        public string serverUrl = LocalVoiceProtocol.DefaultUrl;

        [Tooltip("Server voice id: a cloned voice (v-...) or a stock one (kristin).")]
        public string voiceId = "v-frank-steinicke-12f548";

        [Tooltip("Log each reply's timing (Gemini text in, local audio out) to the Console.")]
        public bool logTimings = true;

        /// <summary>True while the local voice is playing (or about to) a reply.</summary>
        public bool IsSpeaking => _playback != null && _playback.Buffer != null && _playback.Buffer.IsActive;

        /// <summary>(response_id, ms from Gemini's first text of the reply to the first audible local sample).</summary>
        public event Action<int, double> OnFirstAudible;

        /// <summary>Client ms of Gemini's first text of the current reply (NaN when none).</summary>
        public double LastTextStartMs { get; private set; } = double.NaN;

        private GeminiRealtimeWrapper _gemini;
        private LocalVoiceClient _client;
        private LocalVoicePlayback _playback;
        private bool _open;
        private int _response = -1;
        private double _textStartMs = double.NaN;

        /// <summary>Wires the speaker to an agent's wrapper and avatar. Called by the agent in its Awake.</summary>
        public void Attach(GeminiRealtimeWrapper gemini, GameObject avatar, AudioSource source)
        {
            _gemini = gemini;
            // The words to speak come from the output transcription when Gemini answers in audio.
            _gemini.enableTranscription = true;
            _gemini.OnTextReceived += HandleText;
            _gemini.OnTurnComplete += HandleTurnComplete;
            _gemini.OnServerInterrupted += Interrupt;

            _playback = avatar.GetComponent<LocalVoicePlayback>() ?? avatar.AddComponent<LocalVoicePlayback>();
            var lipSync = avatar.GetComponent<OVRLipSyncContext>();
            if (lipSync != null) lipSync.audioLoopback = true;
            _playback.Initialize(source, lipSync);
            _playback.FirstAudible += (rid, dsp, ms) =>
            {
                double fromText = double.IsNaN(_textStartMs) ? double.NaN : ms + _playback.OutputLatencyMs - _textStartMs;
                if (logTimings) Debug.Log($"[LocalVoiceSpeaker] response {rid}: Gemini text -> audible {fromText:F0} ms");
                _ = _client?.SendPlaybackStartedAsync((int)rid, _playback.OutputLatencyMs, fromText, "gemini_text");
                OnFirstAudible?.Invoke((int)rid, fromText);
            };

            _client = new LocalVoiceClient(serverUrl);
            _client.Config = new LocalVoiceSessionConfig
            {
                outputSampleRate = AudioSettings.outputSampleRate,
                voice = voiceId,
                bargeIn = "off",
                clientName = "LocalVoiceSpeaker",
                clientPlatform = $"Unity {Application.unityVersion} {Application.platform}",
            };
            _client.Audio += (rid, seq, frame, off, n) => _playback.Buffer.Write(rid, frame, off, n);
            _client.ResponseCreated += rid => _response = rid;
            _client.AudioDone += (rid, sent, dropped) => _playback.Buffer.MarkDone((uint)rid);
            _client.ResponseCancelled += (rid, reason) => _playback.Buffer.Cancel((uint)rid);
            _client.Error += e => Debug.LogWarning($"[LocalVoiceSpeaker] server error {e}");
            _client.SessionCreated += m => Debug.Log($"[LocalVoiceSpeaker] local voice server session {_client.SessionId}, voice {voiceId}");
            _client.Start();
        }

        private void HandleText(string text)
        {
            if (string.IsNullOrEmpty(text) || _client == null) return;
            if (!_open)
            {
                _open = true;
                _textStartMs = LocalVoiceClient.NowMs;
                LastTextStartMs = _textStartMs;
            }
            _ = _client.SendJsonAsync(new JObject { ["type"] = "speak.text", ["text"] = text });
        }

        private void HandleTurnComplete()
        {
            if (!_open || _client == null) return;
            _open = false;
            _ = _client.SendJsonAsync(new JObject { ["type"] = "speak.end" });
        }

        /// <summary>Stops the local voice now: cancels the response at the server and drops its audio.</summary>
        public void Interrupt()
        {
            uint rid = _playback != null && _playback.Buffer != null ? _playback.Buffer.ResponseId : 0;
            _playback?.Buffer?.Clear();
            int id = rid != 0 ? (int)rid : _response;
            if (id >= 0) _ = _client?.CancelAsync(id);
            _open = false;
        }

        private void OnDestroy()
        {
            if (_gemini != null)
            {
                _gemini.OnTextReceived -= HandleText;
                _gemini.OnTurnComplete -= HandleTurnComplete;
                _gemini.OnServerInterrupted -= Interrupt;
            }
            _ = _client?.StopAsync();
        }
    }
}
