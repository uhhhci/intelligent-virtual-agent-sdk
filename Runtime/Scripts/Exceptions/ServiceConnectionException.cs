using System;

namespace IVH.Core.Exceptions
{
    /// <summary>
    /// Raised when the SDK fails to open, maintain, or recover a realtime connection to an LLM
    /// provider. Check <see cref="Exception.InnerException"/> for the underlying socket or HTTP error.
    /// </summary>
    public class ServiceConnectionException : IVAException
    {
        /// <summary>Number of reconnect attempts made before giving up. Zero if no reconnect was attempted.</summary>
        public int AttemptsExhausted { get; }

        public ServiceConnectionException() { }
        public ServiceConnectionException(string message) : base(message) { }
        public ServiceConnectionException(string message, Exception innerException) : base(message, innerException) { }
        public ServiceConnectionException(string message, int attemptsExhausted, Exception innerException = null)
            : base(message, innerException)
        {
            AttemptsExhausted = attemptsExhausted;
        }
    }
}
