using System.Threading.Tasks;

namespace IVH.Core.Knowledge
{
    /// <summary>
    /// Supplies a text prefix to be prepended to an agent's prompt before each LLM call (or
    /// once at session start for realtime agents). Implementations may retrieve from a vector
    /// store, a document corpus, long-term memory, or any other source of grounding context.
    /// </summary>
    /// <remarks>
    /// Multiple implementations on the same GameObject are aggregated by
    /// <see cref="IntelligentVirtualAgent.AgentBase.BuildContextPrefixAsync"/> in the order
    /// returned by <c>GetComponents</c>. Return an empty string when there is nothing to add.
    /// </remarks>
    public interface IContextProvider
    {
        /// <summary>
        /// Builds the prefix string to prepend to the agent's prompt.
        /// </summary>
        /// <param name="querySeed">
        /// The current user utterance (or any short topical seed). May be null for realtime
        /// agents that retrieve once at session start.
        /// </param>
        /// <returns>The formatted prefix, or empty string when no context is available.</returns>
        Task<string> BuildPrefixAsync(string querySeed);
    }
}
