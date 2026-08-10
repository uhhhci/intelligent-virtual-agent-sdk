using System;

namespace IVH.Core.Exceptions
{
    /// <summary>
    /// Base class for all IVA SDK exceptions. Catch this to handle any SDK-originated error,
    /// or catch a more specific subclass for targeted handling.
    /// </summary>
    /// <remarks>
    /// Existing v2.3.3 code paths log errors via <see cref="UnityEngine.Debug.LogError"/> rather than
    /// throwing. Throwing is opt-in: agents only raise these exceptions when the caller explicitly
    /// asks for strict mode. Default behavior remains log-and-continue for backward compatibility.
    /// </remarks>
    public class IVAException : Exception
    {
        public IVAException() { }
        public IVAException(string message) : base(message) { }
        public IVAException(string message, Exception innerException) : base(message, innerException) { }
    }
}
