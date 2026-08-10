using System;

namespace IVH.Core.IntelligentVirtualAgent
{
    /// <summary>
    /// Source of an agent interruption — used by <see cref="GeminiLiveAgent.OnAgentInterrupted"/>
    /// subscribers (SessionRecorder, MemoryManager, custom UI) to react appropriately.
    /// </summary>
    public enum InterruptionSource
    {
        /// <summary>User's voice exceeded the echo interruption threshold while agent was speaking.</summary>
        UserVoice,
        /// <summary>A user gesture in the configured interrupt set was reported by Gemini.</summary>
        UserGesture,
        /// <summary>User-code called a public interrupt method (e.g. UI button).</summary>
        Programmatic,
    }

    /// <summary>
    /// Snapshot of a single interruption event. Passed to subscribers of
    /// <see cref="GeminiLiveAgent.OnAgentInterrupted"/>.
    /// </summary>
    [Serializable]
    public class InterruptionInfo
    {
        /// <summary>Why the interruption happened.</summary>
        public InterruptionSource source;

        /// <summary>Free-form descriptor — gesture name, "voice", or custom string.</summary>
        public string reason;

        /// <summary>Energy / confidence value at the time of detection (RMS for voice, 0–1 for gesture).</summary>
        public float magnitude;

        /// <summary>UTC timestamp.</summary>
        public DateTime timestampUtc;

        /// <summary>Last fragment of the agent's transcript before being cut off, if available.</summary>
        public string lastAgentFragment;
    }
}
