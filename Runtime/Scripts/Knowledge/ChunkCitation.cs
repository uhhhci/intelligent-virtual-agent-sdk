using System;
using Newtonsoft.Json;

namespace IVH.Core.Knowledge
{
    /// <summary>
    /// Compact citation payload stored as JSON in <see cref="Memory.MemoryItem.metadataJson"/>
    /// when a chunk is baked into the knowledge store. Decoded at retrieval time by
    /// <see cref="CitationPromptFormatter"/> to emit human-readable source attributions.
    /// </summary>
    [Serializable]
    public class ChunkCitation
    {
        /// <summary>Source file name without extension or path.</summary>
        public string sourceFile;

        /// <summary>One-based section index inside the source document.</summary>
        public int sectionIndex;

        /// <summary>Section heading text when present.</summary>
        public string sectionTitle;

        /// <summary>Zero-based chunk position within its parent section.</summary>
        public int chunkIndex;

        /// <summary>Serializes this citation to a compact JSON string suitable for memory storage.</summary>
        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }

        /// <summary>
        /// Best-effort parse from JSON. Returns null when <paramref name="json"/> is null, empty,
        /// or malformed — never throws, so retrieval stays robust against legacy or corrupted items.
        /// </summary>
        public static ChunkCitation TryParse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try { return JsonConvert.DeserializeObject<ChunkCitation>(json); }
            catch { return null; }
        }
    }
}
