using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using IVH.Core.Memory;
using IVH.Core.Memory.Embedders;
using IVH.Core.Memory.Stores;
using IVH.Core.Utils;
using IVH.Core.Utils.Logging;

namespace IVH.Core.Knowledge
{
    /// <summary>
    /// Runtime side of the document-grounding feature. Loads a baked <see cref="KnowledgeBase"/>
    /// store, embeds the live user query, retrieves the top-K most similar chunks, and returns
    /// a citation-enforcing prompt prefix via the <see cref="IContextProvider"/> contract.
    /// </summary>
    /// <remarks>
    /// Attach to the same GameObject as a <see cref="IntelligentVirtualAgent.AgentBase"/>. When
    /// no <see cref="knowledgeBase"/> is assigned or its <see cref="KnowledgeBase.enabled"/> flag
    /// is false, every public call is a no-op and the agent behaves exactly as in v3.0.
    /// </remarks>
    public class DocumentGroundingComponent : MonoBehaviour, IContextProvider
    {
        [Tooltip("Knowledge base asset (markdown corpus + retrieval config) to ground responses against.")]
        public KnowledgeBase knowledgeBase;

        [Tooltip("Optional override. Leave blank to load from the standard ~/.aiapi/auth.json (the same file every other Gemini service in this SDK uses).")]
        public string geminiApiKey = "";

        [Tooltip("Embedding model. Must match the model used at bake time.")]
        public string embeddingModel = "gemini-embedding-001";

        [Tooltip("When true (default), this component injects retrieved chunks into the prompt as an IContextProvider — once at session start for realtime agents, per turn for ConversationalAgent. Turn OFF when you only want this component to serve as the retrieval backend for a KnowledgeRetrievalTool (per-turn function-calling), so it does not also inject a generic one-shot prefix.")]
        public bool injectAtSessionStart = true;

        /// <summary>Programmatic embedder override — set from code for tests or custom providers.</summary>
        [NonSerialized] public IEmbedder embedderOverride;

        /// <summary>Programmatic store override — set from code for tests or custom backends.</summary>
        [NonSerialized] public IMemoryStore storeOverride;

        private IEmbedder _embedder;
        private IMemoryStore _store;
        private bool _ready;

        /// <summary>True when a knowledge base is assigned, enabled, and a baked store is loaded.</summary>
        public bool IsReady => _ready;

        private void Awake()
        {
            TryInitialize();
        }

        private void TryInitialize()
        {
            _ready = false;

            if (knowledgeBase == null || !knowledgeBase.enabled)
            {
                return;
            }

            if (storeOverride == null && string.IsNullOrEmpty(knowledgeBase.bakedStorePath))
            {
                IVALogger.Warn("DocumentGroundingComponent",
                    $"KnowledgeBase '{knowledgeBase.name}' has no baked store. Run 'IVA SDK / Knowledge / Bake Selected KnowledgeBase' in the editor.");
                return;
            }

            _embedder = embedderOverride ?? BuildDefaultEmbedder();
            if (_embedder == null) return;

            if (knowledgeBase.embeddingDimension > 0 && _embedder.Dimension != knowledgeBase.embeddingDimension)
            {
                IVALogger.Error("DocumentGroundingComponent",
                    $"Embedder dimension ({_embedder.Dimension}) does not match baked dimension ({knowledgeBase.embeddingDimension}). Re-bake or supply a matching embedder. Grounding disabled.");
                return;
            }

            _store = storeOverride ?? new JsonFileMemoryStore(ResolveStorePath(knowledgeBase.bakedStorePath));
            _ready = true;
        }

        private IEmbedder BuildDefaultEmbedder()
        {
            // Priority order: explicit Inspector field, then the project-wide ~/.aiapi/auth.json
            // that every other Gemini service in this SDK already reads. This keeps key
            // management consistent — no per-component duplication of secrets.
            string key = !string.IsNullOrEmpty(geminiApiKey)
                ? geminiApiKey
                : GeneralModelHelper.GetGeminiApiKey();

            if (string.IsNullOrEmpty(key))
            {
                IVALogger.Warn("DocumentGroundingComponent",
                    "No Gemini API key. Set it in ~/.aiapi/auth.json (recommended) via 'IVA SDK / Setup Wizard', or paste it into the geminiApiKey field on this component. Grounding disabled until configured.");
                return null;
            }
            return new GeminiEmbedder(key, embeddingModel);
        }

        private static string ResolveStorePath(string storedPath)
        {
            if (string.IsNullOrEmpty(storedPath)) return storedPath;
            if (Path.IsPathRooted(storedPath)) return storedPath;
            // The baker writes to StreamingAssets/IVA_Knowledge/{file} so the file ships with
            // standalone builds. The asset stores only the file name; resolve here against
            // Application.streamingAssetsPath so the same lookup works in editor and player.
            return Path.Combine(Application.streamingAssetsPath, "IVA_Knowledge", storedPath);
        }

        /// <inheritdoc/>
        public async Task<string> BuildPrefixAsync(string querySeed)
        {
            // injectAtSessionStart lets this component act purely as a retrieval backend for a
            // KnowledgeRetrievalTool (per-turn function calling) without ALSO injecting a one-shot
            // prefix. The one-shot prefix is weak for realtime agents anyway, since querySeed is null
            // at session start and the query degrades to a generic phrase.
            if (!_ready || !injectAtSessionStart) return "";

            string query = string.IsNullOrEmpty(querySeed)
                ? "Background context relevant to this conversation"
                : querySeed;

            var hits = await RetrieveHitsAsync(query);
            if (hits == null) return "";

            return CitationPromptFormatter.Format(hits, knowledgeBase.citationInstructionTemplate, knowledgeBase.maxContextChars);
        }

        /// <summary>
        /// Retrieves the most relevant chunks for <paramref name="query"/> and returns them as a
        /// plain source list (no instruction wrapper), suitable for returning to the model as the
        /// result of a <c>search_knowledge</c> function call. Unlike <see cref="BuildPrefixAsync"/>
        /// this ignores <see cref="injectAtSessionStart"/> — it is an explicit on-demand lookup.
        /// </summary>
        /// <param name="query">Natural-language query supplied by the model.</param>
        /// <returns>Formatted source chunks, or a short human-readable status message when nothing matches.</returns>
        public async Task<string> SearchAsync(string query)
        {
            if (!_ready || _store == null || _embedder == null)
                return "The knowledge base is not available.";
            if (string.IsNullOrWhiteSpace(query))
                return "No search query was provided.";

            var hits = await RetrieveHitsAsync(query);
            if (hits == null || hits.Count == 0)
                return "No relevant information was found in the knowledge base for that query.";

            return CitationPromptFormatter.FormatSourcesOnly(hits, knowledgeBase.maxContextChars);
        }

        /// <summary>
        /// Shared retrieval core: embeds the query, runs the cosine search, and applies the
        /// similarity floor. Returns null when grounding is unavailable or nothing survives the floor.
        /// </summary>
        private async Task<List<(MemoryItem item, float similarity)>> RetrieveHitsAsync(string query)
        {
            if (!_ready || _store == null || _embedder == null) return null;

            float[] vector;
            try { vector = await _embedder.EmbedAsync(query); }
            catch (Exception ex) { IVALogger.Warn("DocumentGroundingComponent", $"Query embedding failed: {ex.Message}"); return null; }

            var hits = await _store.QueryAsync(vector, knowledgeBase.retrievalTopK);
            if (hits == null || hits.Count == 0) return null;

            if (knowledgeBase.minSimilarity > 0f)
            {
                hits.RemoveAll(h => h.similarity < knowledgeBase.minSimilarity);
                if (hits.Count == 0) return null;
            }

            return hits;
        }
    }
}
