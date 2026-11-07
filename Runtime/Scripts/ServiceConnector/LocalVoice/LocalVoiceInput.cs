using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace IVH.Core.ServiceConnector.LocalVoice
{
    /// <summary>Where a local agent's input audio comes from: a microphone, or recorded speech for a measurement.</summary>
    public interface ILocalVoiceInput
    {
        /// <summary>Rate of the samples <see cref="Read"/> returns.</summary>
        int SampleRate { get; }
        bool IsRunning { get; }
        bool Start();
        void Stop();
        /// <summary>Appends every sample captured since the last call (mono, -1..1). Call once per frame.</summary>
        int Read(List<float> into);
    }

    /// <summary>
    /// A microphone, chosen by name (the human asked for a device picker, not the OS default; CLAUDE.md §7),
    /// captured into a looping clip and read by position every frame.
    /// </summary>
    public sealed class LocalVoiceMicrophone : ILocalVoiceInput
    {
        public string DeviceName { get; private set; }
        public int SampleRate { get; }
        public bool IsRunning { get; private set; }

        private AudioClip _clip;
        private int _lastPos;
        private float[] _scratch = new float[0];

        public LocalVoiceMicrophone(string deviceName, int sampleRate = 16000)
        {
            DeviceName = string.IsNullOrEmpty(deviceName) ? null : deviceName;
            SampleRate = sampleRate;
        }

        /// <summary>The microphones Unity can open, for a picker.</summary>
        public static string[] Devices => Microphone.devices;

        public bool Start()
        {
            if (IsRunning) return true;
            if (Microphone.devices.Length == 0) { Debug.LogWarning("[LocalVoice] No microphone."); return false; }
            if (DeviceName != null && Array.IndexOf(Microphone.devices, DeviceName) < 0)
            {
                Debug.LogWarning($"[LocalVoice] Microphone '{DeviceName}' not found; using '{Microphone.devices[0]}'.");
                DeviceName = Microphone.devices[0];
            }
            // A 2 s loop: read every frame, so it never wraps unread.
            _clip = Microphone.Start(DeviceName, true, 2, SampleRate);
            _lastPos = 0;
            IsRunning = _clip != null;
            return IsRunning;
        }

        public void Stop()
        {
            if (!IsRunning) return;
            Microphone.End(DeviceName);
            IsRunning = false;
        }

        public int Read(List<float> into)
        {
            if (!IsRunning || _clip == null) return 0;
            int pos = Microphone.GetPosition(DeviceName);
            if (pos < 0 || pos == _lastPos) return 0;
            int total = _clip.samples;
            int n = pos > _lastPos ? pos - _lastPos : total - _lastPos + pos;
            if (_scratch.Length < n) _scratch = new float[n];
            // GetData wraps around the end of a looping clip by itself.
            var tmp = new float[n];
            _clip.GetData(tmp, _lastPos);
            into.AddRange(tmp);
            _lastPos = pos;
            return n;
        }
    }

    /// <summary>
    /// Recorded utterances played into the agent at real-time pace, silence between them, as a microphone
    /// would deliver them. The server counts the end-of-turn pause in received audio, so silence keeps
    /// flowing between utterances. Used by the latency measurements (the human asked for runs that need
    /// nobody at the microphone, 2026-11-07).
    /// </summary>
    public sealed class LocalVoiceFileInput : ILocalVoiceInput
    {
        public int SampleRate { get; }
        public bool IsRunning { get; private set; }

        /// <summary>Client ms (<see cref="LocalVoiceClient.NowMs"/>) at which the last utterance's end of speech was read out.</summary>
        public double LastEndOfSpeechMs { get; private set; } = double.NaN;
        /// <summary>True from the first sample of a queued utterance to its last.</summary>
        public bool Speaking => _current != null;
        public string CurrentId { get; private set; }

        private readonly Queue<(string id, float[] pcm, int eosSample)> _queue = new Queue<(string, float[], int)>();
        private float[] _current;
        private int _currentPos, _currentEos;
        private double _startedMs;
        private long _delivered;

        public LocalVoiceFileInput(int sampleRate = 16000) { SampleRate = sampleRate; }

        /// <summary>Queues a mono PCM16 WAV at <see cref="SampleRate"/>; <paramref name="eosSeconds"/> is where speech ends in it.</summary>
        public void Enqueue(string id, string wavPath, double eosSeconds)
        {
            float[] pcm = ReadWavMono16(wavPath, out int rate);
            if (rate != SampleRate) throw new InvalidDataException($"{wavPath} is {rate} Hz, expected {SampleRate}");
            _queue.Enqueue((id, pcm, (int)Math.Round(eosSeconds * rate)));
        }

        public bool Start()
        {
            _startedMs = LocalVoiceClient.NowMs;
            _delivered = 0;
            IsRunning = true;
            return true;
        }

        public void Stop() { IsRunning = false; }

        /// <summary>Starts the next queued utterance at the next read. False when none is left.</summary>
        public bool PlayNext()
        {
            if (_queue.Count == 0) return false;
            var next = _queue.Dequeue();
            CurrentId = next.id;
            _current = next.pcm;
            _currentPos = 0;
            _currentEos = next.eosSample;
            return true;
        }

        public int Read(List<float> into)
        {
            if (!IsRunning) return 0;
            // Deliver exactly what real time has made due: the pace a microphone has.
            long due = (long)((LocalVoiceClient.NowMs - _startedMs) * SampleRate / 1000.0) - _delivered;
            if (due <= 0) return 0;
            for (long i = 0; i < due; i++)
            {
                if (_current != null)
                {
                    into.Add(_current[_currentPos]);
                    if (_currentPos == _currentEos) LastEndOfSpeechMs = LocalVoiceClient.NowMs;
                    if (++_currentPos >= _current.Length) { _current = null; }
                }
                else into.Add(0f);
            }
            _delivered += due;
            return (int)due;
        }

        /// <summary>Reads a PCM16 WAV and mixes it to mono.</summary>
        public static float[] ReadWavMono16(string path, out int sampleRate)
        {
            using (var r = new BinaryReader(File.OpenRead(path)))
            {
                if (new string(r.ReadChars(4)) != "RIFF") throw new InvalidDataException("not a RIFF file: " + path);
                r.ReadInt32();
                if (new string(r.ReadChars(4)) != "WAVE") throw new InvalidDataException("not a WAVE file: " + path);
                int channels = 1, bits = 16;
                sampleRate = 16000;
                while (r.BaseStream.Position < r.BaseStream.Length)
                {
                    string id = new string(r.ReadChars(4));
                    int size = r.ReadInt32();
                    if (id == "fmt ")
                    {
                        int format = r.ReadInt16();
                        channels = r.ReadInt16();
                        sampleRate = r.ReadInt32();
                        r.ReadInt32(); r.ReadInt16();
                        bits = r.ReadInt16();
                        if (format != 1 || bits != 16) throw new InvalidDataException("only PCM16 WAV is supported: " + path);
                        if (size > 16) r.ReadBytes(size - 16);
                    }
                    else if (id == "data")
                    {
                        int frames = size / (2 * channels);
                        var pcm = new float[frames];
                        for (int f = 0; f < frames; f++)
                        {
                            float sum = 0f;
                            for (int c = 0; c < channels; c++) sum += r.ReadInt16() / 32768f;
                            pcm[f] = sum / channels;
                        }
                        return pcm;
                    }
                    else r.ReadBytes(size + (size & 1));
                }
                throw new InvalidDataException("no data chunk: " + path);
            }
        }
    }
}
