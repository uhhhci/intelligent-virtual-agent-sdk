// Local Voice Server realtime protocol v1.5, client side. Plain C#: no UnityEngine, IL2CPP-safe
// (no dynamic, no reflection-only paths), Newtonsoft.Json as the rest of the SDK uses.
// The protocol is specified in the server's docs/PROTOCOL.md (LocalVoiceServer repository); this file
// follows it, and where it is silent this file says what it assumes.

using System;
using Newtonsoft.Json.Linq;

namespace IVH.Core.ServiceConnector.LocalVoice
{
    /// <summary>Constants of the wire protocol.</summary>
    public static class LocalVoiceProtocol
    {
        /// <summary>Protocol version in the URL path and in <c>session.created</c>.</summary>
        public const int Version = 1;

        /// <summary>Default WebSocket endpoint of a server on this machine.</summary>
        public const string DefaultUrl = "ws://127.0.0.1:8123/v1/realtime";

        /// <summary>Bytes of the header on every server-to-client audio frame.</summary>
        public const int AudioHeaderBytes = 8;

        /// <summary>
        /// Reads the 8-byte header of a binary audio frame: <c>uint32 response_id, uint32 seq</c>, both
        /// little-endian on the wire (PROTOCOL §1). Decoded byte by byte, so it is right on any host.
        /// </summary>
        public static bool TryReadAudioHeader(byte[] frame, int count, out uint responseId, out uint seq)
        {
            responseId = 0;
            seq = 0;
            if (frame == null || count < AudioHeaderBytes) return false;
            responseId = (uint)(frame[0] | (frame[1] << 8) | (frame[2] << 16) | (frame[3] << 24));
            seq = (uint)(frame[4] | (frame[5] << 8) | (frame[6] << 16) | (frame[7] << 24));
            return true;
        }

        /// <summary>Little-endian PCM16 to float in [-1, 1).</summary>
        public static void Pcm16ToFloat(byte[] src, int offset, int byteCount, float[] dst, int dstOffset)
        {
            int n = byteCount / 2;
            for (int i = 0; i < n; i++)
            {
                short s = (short)(src[offset + 2 * i] | (src[offset + 2 * i + 1] << 8));
                dst[dstOffset + i] = s / 32768f;
            }
        }

        /// <summary>Float in [-1, 1] to little-endian PCM16, clamped, with a linear gain.</summary>
        public static byte[] FloatToPcm16(float[] src, int offset, int count, float gain = 1f)
        {
            var bytes = new byte[count * 2];
            for (int i = 0; i < count; i++)
            {
                float v = src[offset + i] * gain;
                if (v > 1f) v = 1f; else if (v < -1f) v = -1f;
                short s = (short)Math.Round(v * 32767f);
                bytes[2 * i] = (byte)(s & 0xff);
                bytes[2 * i + 1] = (byte)((s >> 8) & 0xff);
            }
            return bytes;
        }
    }

    /// <summary>
    /// What a session asks for at <c>session.configure</c>. Only fields that are set are sent, so the
    /// server's own configuration applies to the rest (PROTOCOL §3).
    /// </summary>
    [Serializable]
    public class LocalVoiceSessionConfig
    {
        public int inputSampleRate = 16000;
        public int outputSampleRate = 24000;
        public string voice;
        public string systemPrompt;
        public string language = "en";
        /// <summary>Speaking-style hints of a cloned voice that carries them (v1.4). Null leaves the server default.</summary>
        public bool? speakingStyle;
        /// <summary>Pause (ms) after which the server ends the user's turn. Null leaves the server default.</summary>
        public int? pauseMs;
        /// <summary><c>server</c>, <c>client</c> or <c>off</c>. Null leaves the server default.</summary>
        public string bargeIn;
        public bool? speculative;
        /// <summary>Tool declarations as JSON Schema objects (<c>name</c>, <c>description</c>, <c>parameters</c>).</summary>
        public JArray tools;
        public string clientName = "LocalRealtimeWrapper";
        public string clientPlatform = "";
        public string clientVersion = "0.1.0";

        public JObject ToMessage(string type = "session.configure")
        {
            var msg = new JObject { ["type"] = type };
            if (type == "session.configure")
            {
                msg["protocol_version"] = LocalVoiceProtocol.Version;
                msg["input_sample_rate"] = inputSampleRate;
                msg["output_sample_rate"] = outputSampleRate;
                msg["binary_audio_header"] = true;
                msg["client"] = new JObject { ["name"] = clientName ?? "", ["platform"] = clientPlatform ?? "", ["version"] = clientVersion ?? "" };
            }
            if (!string.IsNullOrEmpty(voice)) msg["voice"] = voice;
            if (systemPrompt != null) msg["system_prompt"] = systemPrompt;
            if (!string.IsNullOrEmpty(language)) msg["language"] = language;
            if (speakingStyle.HasValue) msg["speaking_style"] = speakingStyle.Value;
            if (tools != null && tools.Count > 0) msg["tools"] = tools;
            if (pauseMs.HasValue) msg["eou"] = new JObject { ["max_silence_ms"] = pauseMs.Value };
            if (!string.IsNullOrEmpty(bargeIn) || speculative.HasValue)
            {
                var tp = new JObject();
                if (!string.IsNullOrEmpty(bargeIn)) tp["barge_in"] = bargeIn;
                if (speculative.HasValue) tp["speculative_generation"] = speculative.Value;
                msg["turn_policy"] = tp;
            }
            return msg;
        }
    }

    /// <summary>One voice the server offers (<c>session.created.voice_info</c>, v1.3).</summary>
    public class LocalVoiceInfo
    {
        public string Id;
        public string Name;
        public bool Cloned;
        public string Engine;
        public double? Rtf;
        public string Streaming;
        public string LicenceClass;
        public bool SpeakingStyle;

        public static LocalVoiceInfo FromJson(JToken t) => new LocalVoiceInfo
        {
            Id = (string)t["id"],
            Name = (string)t["name"] ?? (string)t["id"],
            Cloned = (bool?)t["cloned"] ?? false,
            Engine = (string)t["engine"],
            Rtf = t["rtf"] != null && t["rtf"].Type != JTokenType.Null ? (double?)t["rtf"] : null,
            Streaming = (string)t["streaming"],
            LicenceClass = (string)t["licence_class"],
            SpeakingStyle = (bool?)t["speaking_style"] ?? false,
        };

        public override string ToString() => Cloned ? $"{Name} ({Id}, cloned, {Engine})" : $"{Id} ({Engine})";
    }

    /// <summary>A server <c>error</c> frame (PROTOCOL §4, §8).</summary>
    public class LocalVoiceError
    {
        public string Code;
        public string Message;
        public bool Retryable;

        public override string ToString() => $"{Code}: {Message}";
    }

    /// <summary>Why the connection ended.</summary>
    public enum LocalVoiceDisconnect
    {
        UserRequested,
        ServerClosed,
        NetworkError,
        Refused,
    }
}
