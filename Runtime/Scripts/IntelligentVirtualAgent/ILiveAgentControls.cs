namespace IVH.Core.IntelligentVirtualAgent
{
    /// <summary>
    /// What the in-game HUD (<see cref="GeminiLiveAgentUIControls"/>) needs from a realtime agent, so one HUD
    /// drives both <see cref="GeminiLiveAgent"/> and <see cref="LocalLiveAgent"/>.
    /// </summary>
    public interface ILiveAgentControls
    {
        /// <summary>Opens (or reopens) the realtime session.</summary>
        void Connect();
        /// <summary>Stops the agent's current reply.</summary>
        void RequestInterrupt(string reason = "programmatic");
        /// <summary>Starts or stops sending camera frames (a no-op for agents without vision).</summary>
        void ToggleVisionStream(bool enable);
        /// <summary>Writes a system line to the agent's log panel.</summary>
        void AppendLog(string message);
        string MicrophoneDeviceName { get; set; }
        bool EnableVocalInterruption { get; set; }
        bool MuteMicWhileTalking { get; set; }
        float VisionUpdateFrequency { get; set; }
    }
}
