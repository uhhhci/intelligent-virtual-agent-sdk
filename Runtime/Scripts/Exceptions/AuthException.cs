using System;

namespace IVH.Core.Exceptions
{
    /// <summary>
    /// Raised when authentication to an LLM provider fails — missing API key, invalid service-account
    /// JSON, expired token, insufficient permissions, etc. The message should point the user at the
    /// credential file or setup wizard.
    /// </summary>
    public class AuthException : IVAException
    {
        public AuthException() { }
        public AuthException(string message) : base(message) { }
        public AuthException(string message, Exception innerException) : base(message, innerException) { }
    }
}
