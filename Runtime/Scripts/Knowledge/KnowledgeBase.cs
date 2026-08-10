using System.Collections.Generic;
using UnityEngine;

namespace IVH.Core.Knowledge
{
    /// <summary>
    /// Authoring-time asset that bundles a corpus of reference documents (Markdown only in
    /// v3.0) plus the chunking, retrieval, and citation parameters used both at bake time and
    /// runtime. Drop into a <see cref="DocumentGroundingComponent"/> to ground an agent's
    /// answers in the bundled corpus.
    /// </summary>
    /// <remarks>
    /// The asset holds only configuration and source references; the actual embeddings live in a
    /// separate JSON store written by the editor-time baker (see the Editor menu under
    /// <c>IVA SDK / Knowledge</c>). The runtime never parses Markdown or calls the embeddings
    /// API for ingestion — it only embeds the live query.
    /// </remarks>
    [CreateAssetMenu(fileName = "KnowledgeBase", menuName = "IVA SDK/Knowledge Base", order = 140)]
    public class KnowledgeBase : ScriptableObject
    {
        [Tooltip("Master switch. Default off so adding the asset to a project does not silently change agent behavior.")]
        public bool enabled = false;

        [Header("Sources")]
        [Tooltip("Markdown TextAssets included in this knowledge base. Only .md is supported in v3.0.")]
        public List<TextAsset> markdownDocuments = new List<TextAsset>();

        [Header("Chunking")]
        [Tooltip("Target chunk size in characters. Smaller = finer retrieval, more chunks, more embedding cost at bake time.")]
        [Range(200, 4000)] public int chunkCharSize = 1200;

        [Tooltip("Overlap between consecutive chunks in characters. Helps preserve context across chunk boundaries.")]
        [Range(0, 1000)] public int chunkCharOverlap = 200;

        [Header("Retrieval")]
        [Tooltip("How many top-matching chunks to inject into the agent's prompt per query.")]
        [Range(1, 10)] public int retrievalTopK = 4;

        [Tooltip("Discard hits below this cosine similarity. 0 keeps everything; 0.5 is a reasonable noise floor.")]
        [Range(0f, 1f)] public float minSimilarity = 0f;

        [Tooltip("Hard upper bound on the prefix character length. Protects small-context local LLMs.")]
        [Range(500, 32000)] public int maxContextChars = 4000;

        [Header("Prompting")]
        [TextArea(4, 12)]
        [Tooltip("Instruction template prepended to retrieved chunks. Leave blank for the default natural-speech template (suppresses spoken citation — recommended for voice agents). Use CitationPromptFormatter.StrictCitationInstructionTemplate as a starting point if you want the model to cite sources explicitly. The token {sources} is replaced with the formatted source list at runtime.")]
        public string citationInstructionTemplate = "";

        [Header("Baked store (auto-managed by the editor baker — do not hand-edit)")]
        [Tooltip("File name of the baked JSON store inside StreamingAssets/IVA_Knowledge/. Set automatically by the baker.")]
        public string bakedStorePath = "";

        [Tooltip("Vector dimension produced by the embedder used at bake time. Runtime refuses to load on mismatch.")]
        public int embeddingDimension = 0;

        [Tooltip("Number of chunks in the baked store. Informational only.")]
        public int bakedChunkCount = 0;

        [Tooltip("UTC ticks of the most recent successful bake. Informational only.")]
        public long lastBakedAtUtcTicks = 0;
    }
}
