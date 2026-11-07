using System;
using UnityEngine;

namespace IVH.Core.ServiceConnector.LocalVoice
{
    /// <summary>
    /// Plays a local agent's voice through the avatar's own <see cref="AudioSource"/>, from ONE ring buffer
    /// read in <see cref="OnAudioFilterRead"/>: never an <see cref="AudioClip"/> per chunk, so there is no
    /// clip boundary to click at and no per-chunk scheduling delay.
    /// </summary>
    /// <remarks>
    /// <para><b>Lip sync.</b> The avatar's <c>OVRLipSyncContext</c> normally analyses the audio of the same
    /// GameObject in its own <c>OnAudioFilterRead</c>, which only sees this component's samples if it runs
    /// after it, and Unity orders filters by component order. So this component takes lip sync over: it sets
    /// the context's <c>skipAudioSource</c> and hands it every buffer it plays
    /// (<c>ProcessAudioSamples</c>). Lip sync then works the same for CC4 and Avatar SDK characters (both
    /// use that context, with different viseme drivers) whatever the component order, and is the same
    /// lip-sync path <c>GeminiLiveAgent</c> drives.</para>
    /// <para><b>Rate.</b> The server sends audio at <see cref="AudioSettings.outputSampleRate"/> (asked for at
    /// <c>session.configure</c>), so nothing is resampled here.</para>
    /// <para><b>Threads.</b> <see cref="Buffer"/> is written by the network thread and read by the audio
    /// thread; its events are raised on the audio thread, so this component queues them and raises
    /// <see cref="FirstAudible"/> and <see cref="Drained"/> on the main thread.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class LocalVoicePlayback : MonoBehaviour
    {
        /// <summary>The buffer every response is written to.</summary>
        public PcmRingBuffer Buffer { get; private set; }

        /// <summary>Main thread: (response_id, DSP time the first sample was handed to the device, client ms at that moment).</summary>
        public event Action<uint, double, double> FirstAudible;

        /// <summary>Main thread: a finished response has been played to its last sample.</summary>
        public event Action<uint> Drained;

        /// <summary>Device latency from the DSP buffer to the speaker, ms (Unity reports buffer size × count).</summary>
        public double OutputLatencyMs { get; private set; }

        /// <summary>RMS of the last played buffer (0..1), for avatar mouth fallbacks or meters.</summary>
        public float Level { get; private set; }

        private AudioSource _source;
        private OVRLipSyncContext _lipSync;
        private readonly object _evLock = new object();
        private uint _pendingFirst;
        private double _pendingFirstDsp, _pendingFirstMs;
        private uint _pendingDrained;
        private int _channels = 2;
        private bool _ready;

        /// <summary>Creates the buffer and starts the source playing (silence until audio arrives).</summary>
        public void Initialize(AudioSource source, OVRLipSyncContext lipSyncContext)
        {
            _source = source;
            Buffer = new PcmRingBuffer(AudioSettings.outputSampleRate * 120);
            Buffer.FirstSamplePlayed += id =>
            {
                lock (_evLock)
                {
                    _pendingFirst = id;
                    _pendingFirstDsp = AudioSettings.dspTime;
                    _pendingFirstMs = LocalVoiceClient.NowMs;
                }
            };
            Buffer.ResponseDrained += (id, n) => { lock (_evLock) _pendingDrained = id; };

            AudioSettings.GetDSPBufferSize(out int bufferLength, out int numBuffers);
            OutputLatencyMs = 1000.0 * bufferLength * numBuffers / AudioSettings.outputSampleRate;

            AttachLipSync(lipSyncContext);

            // A source must be playing for its filter chain to run. A silent looping clip keeps it playing
            // for the life of the agent; every audible sample comes from the ring buffer.
            int rate = AudioSettings.outputSampleRate;
            var silence = AudioClip.Create("LocalVoiceCarrier", rate, 1, rate, false);
            silence.SetData(new float[rate], 0);
            _source.clip = silence;
            _source.loop = true;
            _source.playOnAwake = false;
            if (!_source.isPlaying) _source.Play();
            _ready = true;
        }

        private void AttachLipSync(OVRLipSyncContext context)
        {
            if (context == null) return;
            context.skipAudioSource = true;
            _lipSync = context;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (!_ready || Buffer == null) return;
            _channels = channels;
            Buffer.Read(data, channels);
            float sum = 0f;
            for (int i = 0; i < data.Length; i += channels) sum += data[i] * data[i];
            Level = Mathf.Sqrt(sum / Mathf.Max(1, data.Length / channels));
            if (_lipSync != null)
            {
                // ProcessAudioSamples also scales by the context's gain and, with audioLoopback off, zeroes
                // the buffer (its microphone use). Agents set audioLoopback = true (AgentBase.SetupLipSync).
                try { _lipSync.ProcessAudioSamples(data, channels); }
                catch { /* never take the audio thread down for lip sync */ }
            }
        }

        private void Update()
        {
            uint first = 0, drained = 0;
            double dsp = 0, ms = 0;
            lock (_evLock)
            {
                first = _pendingFirst; dsp = _pendingFirstDsp; ms = _pendingFirstMs; _pendingFirst = 0;
                drained = _pendingDrained; _pendingDrained = 0;
            }
            if (first != 0) FirstAudible?.Invoke(first, dsp, ms);
            if (drained != 0) Drained?.Invoke(drained);
        }

        private void OnDisable()
        {
            _ready = false;
        }
    }
}
