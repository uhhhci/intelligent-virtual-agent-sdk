using System;

namespace IVH.Core.ServiceConnector.LocalVoice
{
    /// <summary>
    /// The one playback buffer of a local agent: mono float samples at the output rate, written by the
    /// network thread and read by the audio thread (Unity's <c>OnAudioFilterRead</c>). Every sample belongs
    /// to one <c>response_id</c>; a cancelled response is dropped in one call (PROTOCOL §5), so no timeout,
    /// debounce or grace period is needed.
    /// </summary>
    /// <remarks>
    /// One lock, held for a copy at most: the audio callback must never wait on anything slow, and a
    /// lock-free design is not worth its risk for two threads and a few kilobytes per callback.
    /// The buffer only ever holds ONE response: audio for a newer id replaces whatever is queued, because
    /// the server never interleaves responses and a newer id means the older one is over.
    /// </remarks>
    public sealed class PcmRingBuffer
    {
        private readonly float[] _buf;
        private readonly object _lock = new object();
        private int _read;
        private int _count;
        private uint _responseId;
        private bool _hasResponse;
        private bool _responseDone;
        private bool _started;
        private long _playedSamples;
        private int _underruns;
        private bool _starved;

        /// <param name="capacitySamples">At 48 kHz, 48000 × 60 holds a minute, more than any reply.</param>
        public PcmRingBuffer(int capacitySamples)
        {
            _buf = new float[Math.Max(1024, capacitySamples)];
        }

        /// <summary>Response whose samples are in the buffer (0 when none).</summary>
        public uint ResponseId { get { lock (_lock) return _hasResponse ? _responseId : 0; } }

        /// <summary>Samples waiting to be played.</summary>
        public int Buffered { get { lock (_lock) return _count; } }

        /// <summary>Times playback ran dry in the middle of a response (before its audio was done).</summary>
        public int Underruns { get { lock (_lock) return _underruns; } }

        /// <summary>True while samples of a response are being played or are still due.</summary>
        public bool IsActive { get { lock (_lock) return _hasResponse && (_count > 0 || !_responseDone); } }

        /// <summary>Samples of the current response already played.</summary>
        public long PlayedSamples { get { lock (_lock) return _playedSamples; } }

        /// <summary>Raised ON THE AUDIO THREAD when the first sample of a response is played.</summary>
        public event Action<uint> FirstSamplePlayed;

        /// <summary>Raised ON THE AUDIO THREAD when a finished response has been played to its end.</summary>
        public event Action<uint, long> ResponseDrained;

        /// <summary>
        /// Appends PCM16 samples of <paramref name="responseId"/>. Samples of an older response than the one
        /// buffered are ignored; a newer response replaces the buffered one.
        /// </summary>
        public void Write(uint responseId, byte[] pcm16, int offset, int byteCount)
        {
            int n = byteCount / 2;
            if (n <= 0) return;
            lock (_lock)
            {
                if (_hasResponse && responseId < _responseId) return;
                if (!_hasResponse || responseId != _responseId) StartResponseLocked(responseId);
                for (int i = 0; i < n; i++)
                {
                    if (_count == _buf.Length) return; // full: the rest is dropped, never overwritten
                    short s = (short)(pcm16[offset + 2 * i] | (pcm16[offset + 2 * i + 1] << 8));
                    _buf[(_read + _count) % _buf.Length] = s / 32768f;
                    _count++;
                }
                _starved = false;
            }
        }

        /// <summary>Marks the response complete (<c>response.audio.done</c>): running dry after this is its end, not an underrun.</summary>
        public void MarkDone(uint responseId)
        {
            lock (_lock)
            {
                if (_hasResponse && _responseId == responseId) _responseDone = true;
            }
        }

        /// <summary>Drops every buffered sample of <paramref name="responseId"/> (cancel, barge-in). Returns how many.</summary>
        public int Cancel(uint responseId)
        {
            lock (_lock)
            {
                if (!_hasResponse || _responseId != responseId) return 0;
                int dropped = _count;
                _count = 0;
                _hasResponse = false;
                return dropped;
            }
        }

        /// <summary>Drops whatever is buffered.</summary>
        public void Clear()
        {
            lock (_lock)
            {
                _count = 0;
                _hasResponse = false;
            }
        }

        /// <summary>
        /// Fills an interleaved buffer of <paramref name="channels"/> channels (the audio callback's), mono to
        /// every channel, silence where nothing is buffered. Returns the mono samples written.
        /// </summary>
        public int Read(float[] data, int channels)
        {
            int frames = data.Length / channels;
            uint firstOf = 0, drainedOf = 0;
            long drainedAt = 0;
            int got;
            lock (_lock)
            {
                got = Math.Min(frames, _count);
                for (int f = 0; f < got; f++)
                {
                    float v = _buf[_read];
                    _read = (_read + 1) % _buf.Length;
                    int b = f * channels;
                    for (int c = 0; c < channels; c++) data[b + c] = v;
                }
                _count -= got;
                if (got > 0 && !_started && _hasResponse)
                {
                    _started = true;
                    firstOf = _responseId;
                }
                _playedSamples += got;
                if (_hasResponse && _started && _count == 0)
                {
                    if (_responseDone)
                    {
                        drainedOf = _responseId;
                        drainedAt = _playedSamples;
                        _hasResponse = false;
                    }
                    else if (got < frames && !_starved)
                    {
                        // Ran dry before the server said the audio was done: an underrun.
                        _underruns++;
                        _starved = true;
                    }
                }
            }
            for (int i = got * channels; i < data.Length; i++) data[i] = 0f;
            if (firstOf != 0) FirstSamplePlayed?.Invoke(firstOf);
            if (drainedOf != 0) ResponseDrained?.Invoke(drainedOf, drainedAt);
            return got;
        }

        private void StartResponseLocked(uint responseId)
        {
            _responseId = responseId;
            _hasResponse = true;
            _responseDone = false;
            _started = false;
            _starved = false;
            _playedSamples = 0;
            _count = 0;
        }
    }
}
