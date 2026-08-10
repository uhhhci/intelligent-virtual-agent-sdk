using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using IVH.Core.IntelligentVirtualAgent;
using IVH.Core.Knowledge;
using IVH.Core.Memory.Stores;
using IVH.Core.Utils.Logging;

namespace IVH.Core.Memory
{
    /// <summary>
    /// Orchestrates long-term memory for an agent: ingests conversation summaries into an
    /// <see cref="IMemoryStore"/>, retrieves relevant past memories on session start, and
    /// returns a formatted prompt prefix the agent can prepend to its system instruction.
    /// </summary>
    /// <remarks>
    /// Opt-in via <see cref="MemoryConfig.enabled"/>. When disabled, every public method is a no-op.
    /// Attach to the same GameObject as an agent. Implements <see cref="IContextProvider"/> so
    /// <see cref="AgentBase.BuildContextPrefixAsync"/> picks it up automatically alongside any
    /// document-grounding component.
    /// </remarks>
    public class MemoryManager : MonoBehaviour, IContextProvider
    {
        public MemoryConfig config;

        /// <summary>User-facing identity. Memories are partitioned by this key across sessions.</summary>
        public string persistentIdentity = "";

        [Tooltip("Runtime override: provide an IMemoryStore instance programmatically for tests or custom backends.")]
        public IMemoryStore memoryStoreOverride;

        [Tooltip("Runtime override: provide an IEmbedder programmatically.")]
        public IEmbedder embedderOverride;

        private IMemoryStore _store;
        private IEmbedder _embedder;
        private readonly List<string> _pendingTurns = new List<string>();
        private GeminiLiveAgent _liveAgent;

        public bool IsEnabled => config != null && config.enabled;

        private void Awake()
        {
            if (!IsEnabled) return;

            _store = memoryStoreOverride ?? BuildDefaultStore();
            _embedder = embedderOverride;

            // Auto-subscribe to interruption events so they're stitched into long-term memory.
            _liveAgent = GetComponent<GeminiLiveAgent>();
            if (_liveAgent != null) _liveAgent.OnAgentInterrupted += HandleAgentInterrupted;

            IVALogger.Info("MemoryManager", $"Memory enabled for identity '{persistentIdentity}' using {(_store?.GetType().Name ?? "null")}");
        }

        private void OnDestroy()
        {
            if (_liveAgent != null) _liveAgent.OnAgentInterrupted -= HandleAgentInterrupted;
        }

        private async void HandleAgentInterrupted(InterruptionInfo info)
        {
            if (!IsEnabled || info == null) return;
            string note = $"[interrupted] source={info.source} reason={info.reason}"
                        + (string.IsNullOrEmpty(info.lastAgentFragment) ? "" : $" last_agent_said=\"{info.lastAgentFragment}\"");
            await RecordTurnAsync("system", note);
        }

        private IMemoryStore BuildDefaultStore()
        {
            switch (config.storeType)
            {
                case MemoryConfig.StoreType.JsonFile:
                    string path = string.IsNullOrEmpty(config.storePathOverride)
                        ? null
                        : config.storePathOverride;
                    return new JsonFileMemoryStore(path);
                case MemoryConfig.StoreType.InMemory:
                default:
                    return new JsonFileMemoryStore(Path.Combine(Application.temporaryCachePath, "IVA_InMem_Memory.json"));
            }
        }

        /// <summary>
        /// Retrieves the top-k memories for the current user and formats them as a prompt prefix.
        /// Returns an empty string if memory is disabled, no identity is set, or no memories exist.
        /// </summary>
        public async Task<string> BuildPromptPrefixAsync(string querySeed = null)
        {
            if (!IsEnabled || _store == null || _embedder == null || string.IsNullOrEmpty(persistentIdentity))
                return "";

            string queryText = !string.IsNullOrEmpty(querySeed)
                ? querySeed
                : $"Relevant past context for user {persistentIdentity}";

            float[] vector;
            try { vector = await _embedder.EmbedAsync(queryText); }
            catch (Exception ex) { IVALogger.Warn("MemoryManager", $"Embed failed: {ex.Message}"); return ""; }

            var hits = await _store.QueryAsync(vector, config.retrievalTopK, persistentIdentity);
            if (hits == null || hits.Count == 0) return "";

            var sb = new StringBuilder();
            sb.Append($"Context from prior sessions with {persistentIdentity}:\n");
            foreach (var (item, sim) in hits)
            {
                sb.Append($"- ({sim:0.00}) {item.text}\n");
            }
            return sb.ToString();
        }

        /// <summary>
        /// Adds a completed conversation turn to the buffer. When the buffer reaches
        /// <see cref="MemoryConfig.summarizeEveryNTurns"/> turns, a summary is ingested.
        /// </summary>
        public async Task RecordTurnAsync(string speaker, string text)
        {
            if (!IsEnabled || string.IsNullOrEmpty(text)) return;
            _pendingTurns.Add($"{speaker}: {text}");
            if (_pendingTurns.Count >= config.summarizeEveryNTurns)
            {
                await FlushBufferAsync();
            }
        }

        /// <summary>Forces the pending-turn buffer to be summarized and stored immediately.</summary>
        public async Task FlushBufferAsync()
        {
            if (!IsEnabled || _store == null || _embedder == null || _pendingTurns.Count == 0) return;

            string combined = string.Join("\n", _pendingTurns);
            _pendingTurns.Clear();

            // Simple concat-as-summary; a future version can add a cheap-LLM summarization step.
            float[] vector;
            try { vector = await _embedder.EmbedAsync(combined); }
            catch (Exception ex) { IVALogger.Warn("MemoryManager", $"Embed failed during flush: {ex.Message}"); return; }

            var item = new MemoryItem
            {
                id = Guid.NewGuid().ToString("N"),
                sessionId = null,
                userId = persistentIdentity,
                text = combined,
                vector = vector,
                metadataJson = null,
                createdAtUtcTicks = DateTime.UtcNow.Ticks,
            };
            await _store.AddAsync(item);
        }

        /// <summary>Clears all stored memories for the current user identity.</summary>
        public Task ForgetEverythingAsync()
        {
            if (!IsEnabled || _store == null) return Task.CompletedTask;
            return _store.ClearAsync(persistentIdentity);
        }

        /// <inheritdoc/>
        public Task<string> BuildPrefixAsync(string querySeed) => BuildPromptPrefixAsync(querySeed);
    }
}
