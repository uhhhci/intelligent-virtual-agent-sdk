using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using IVH.Core.Utils.Logging;

namespace IVH.Core.Knowledge
{
    /// <summary>
    /// Injects the full text of one or more documents directly into the agent's prompt, bypassing
    /// chunking, embedding, and retrieval entirely. For corpora small enough to fit the model's
    /// context window (up to a few hundred KB on Gemini 2.5's 1M-token window), this is more reliable
    /// than top-K retrieval: every fact is always present, with zero per-turn latency and no
    /// dependency on a chunk ranking first. Prefer <see cref="KnowledgeRetrievalTool"/> (per-turn
    /// function calling) once a corpus grows past the context budget.
    /// </summary>
    /// <remarks>
    /// Attach to the same GameObject as an <see cref="IntelligentVirtualAgent.AgentBase"/> (or a
    /// <see cref="IntelligentVirtualAgent.GeminiVoiceOnlyAgent"/>). It implements
    /// <see cref="IContextProvider"/>, so it is picked up automatically by
    /// <see cref="ContextProviderAggregator"/> — once at session start for realtime agents, per turn
    /// for <see cref="IntelligentVirtualAgent.ConversationalAgent"/>. No baking required.
    /// </remarks>
    public class FullDocumentContextProvider : MonoBehaviour, IContextProvider
    {
        /// <summary>The default natural-speech wrapper. Token <c>{documents}</c> is replaced with the concatenated corpus.</summary>
        public const string DefaultInstructionTemplate =
            "The following is everything you know about yourself and your work. Treat it as your own " +
            "memory and knowledge, and answer naturally in the first person. Do NOT read out file " +
            "names, headings, or section markers. Draw on specific details — names, dates, projects, " +
            "and facts — when answering. If the user asks about something genuinely not covered here, " +
            "say so briefly instead of inventing.\n\n" +
            "Your knowledge:\n{documents}\n";

        [Tooltip("Master switch. When false this provider contributes nothing (matches having no provider attached).")]
        public bool injectFullCorpus = true;

        [Header("Sources")]
        [Tooltip("Optional. Reuse the markdown documents listed on an existing KnowledgeBase asset. No baking is needed for this provider — only the raw TextAssets are read.")]
        public KnowledgeBase knowledgeBase;

        [Tooltip("Additional documents to inject. Combined with the KnowledgeBase documents above (duplicates are skipped).")]
        public List<TextAsset> documents = new List<TextAsset>();

        [Header("Prompting")]
        [TextArea(4, 12)]
        [Tooltip("Instruction wrapper prepended to the corpus. The token {documents} is replaced with the concatenated document text at runtime. If the token is absent, the corpus is appended after the text.")]
        public string instructionTemplate = DefaultInstructionTemplate;

        [Tooltip("Safety cap on injected characters. The corpus is truncated if it exceeds this. Roughly 4 characters per token, so 200000 ≈ 50k tokens — well within Gemini 2.5's window but a guard against accidentally huge inputs.")]
        [Range(1000, 500000)] public int maxContextChars = 200000;

        /// <inheritdoc/>
        public Task<string> BuildPrefixAsync(string querySeed)
        {
            // querySeed is intentionally ignored: this provider always injects the entire corpus —
            // that is the whole point, since there is no retrieval step that could miss a chunk.
            // A completed Task satisfies the async contract without spawning a thread.
            if (!injectFullCorpus) return Task.FromResult("");

            var sb = new StringBuilder();
            var seen = new HashSet<TextAsset>();
            AppendDocuments(sb, seen, knowledgeBase != null ? knowledgeBase.markdownDocuments : null);
            AppendDocuments(sb, seen, documents);

            if (sb.Length == 0) return Task.FromResult("");

            string corpus = sb.ToString();
            if (corpus.Length > maxContextChars)
            {
                IVALogger.Warn("FullDocumentContextProvider",
                    $"Corpus ({corpus.Length} chars) exceeds maxContextChars ({maxContextChars}); truncating. Raise the cap, or switch to KnowledgeRetrievalTool for large corpora.");
                corpus = corpus.Substring(0, maxContextChars);
            }

            string prefix = instructionTemplate.Contains("{documents}")
                ? instructionTemplate.Replace("{documents}", corpus)
                : instructionTemplate + "\n" + corpus;
            return Task.FromResult(prefix);
        }

        private static void AppendDocuments(StringBuilder sb, HashSet<TextAsset> seen, List<TextAsset> docs)
        {
            if (docs == null) return;
            foreach (var doc in docs)
            {
                if (doc == null || string.IsNullOrEmpty(doc.text)) continue;
                if (!seen.Add(doc)) continue;
                if (sb.Length > 0) sb.Append("\n\n");
                sb.Append("## ").Append(doc.name).Append('\n').Append(doc.text.Trim());
            }
        }
    }
}
