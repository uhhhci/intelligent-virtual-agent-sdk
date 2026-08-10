using System;

namespace IVH.Core.Exceptions
{
    /// <summary>
    /// Raised (or logged) when a user-registered dynamic tool throws during reflection invocation.
    /// The <see cref="ToolName"/> property identifies which tool failed so the error response sent
    /// back to the model can point at the offending function.
    /// </summary>
    public class ToolExecutionException : IVAException
    {
        /// <summary>Name of the tool that failed, as advertised to Gemini.</summary>
        public string ToolName { get; }

        public ToolExecutionException(string toolName, string message, Exception innerException = null)
            : base(message, innerException)
        {
            ToolName = toolName;
        }
    }
}
