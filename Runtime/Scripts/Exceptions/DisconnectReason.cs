namespace IVH.Core.Exceptions
{
    /// <summary>
    /// Describes why a realtime session ended. Passed to <c>GeminiRealtimeWrapper.OnDisconnected</c>.
    /// </summary>
    public enum DisconnectReason
    {
        /// <summary>Disconnect initiated by user code via <c>DisconnectAsync()</c>.</summary>
        UserRequested,
        /// <summary>Server closed the connection cleanly.</summary>
        ServerClosed,
        /// <summary>Network error — socket read/write failed.</summary>
        NetworkError,
        /// <summary>Authentication failed mid-session (e.g. token expired).</summary>
        AuthFailure,
        /// <summary>Reconnect retries exhausted after repeated failures.</summary>
        RetriesExhausted,
        /// <summary>Unknown cause.</summary>
        Unknown
    }
}
