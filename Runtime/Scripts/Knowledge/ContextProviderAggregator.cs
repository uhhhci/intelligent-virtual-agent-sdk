using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace IVH.Core.Knowledge
{
    /// <summary>
    /// Static helper that aggregates the output of every <see cref="IContextProvider"/> attached to
    /// a host <see cref="GameObject"/>. Used by both <see cref="IntelligentVirtualAgent.AgentBase"/>
    /// and the standalone <see cref="IntelligentVirtualAgent.GeminiVoiceOnlyAgent"/> (which does
    /// not inherit from <c>AgentBase</c>) so the same composition rules apply everywhere.
    /// </summary>
    public static class ContextProviderAggregator
    {
        /// <summary>
        /// Aggregates non-empty prefixes from every <see cref="IContextProvider"/> on
        /// <paramref name="host"/>, in component order, separated by newlines.
        /// Returns the empty string when no providers exist or none have content.
        /// </summary>
        public static async Task<string> BuildPrefixAsync(GameObject host, string querySeed)
        {
            if (host == null) return "";
            var providers = host.GetComponents<IContextProvider>();
            if (providers == null || providers.Length == 0) return "";
            var sb = new StringBuilder();
            foreach (var provider in providers)
            {
                if (provider == null) continue;
                string piece = await provider.BuildPrefixAsync(querySeed);
                if (string.IsNullOrEmpty(piece)) continue;
                sb.Append(piece);
                if (!piece.EndsWith("\n")) sb.Append('\n');
            }
            return sb.ToString();
        }
    }
}
