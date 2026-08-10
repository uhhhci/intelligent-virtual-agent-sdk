using System;

namespace IVH.Core.Knowledge.Chunkers
{
    /// <summary>
    /// A single retrieval unit produced by an <see cref="IChunker"/>. Carries the raw text plus
    /// the metadata needed to cite it back to a user-facing source (filename + section heading).
    /// </summary>
    [Serializable]
    public class TextChunk
    {
        /// <summary>Plain-text content of the chunk.</summary>
        public string text;

        /// <summary>Source file name (without extension or path) for citation display.</summary>
        public string sourceFile;

        /// <summary>One-based section index inside the source document.</summary>
        public int sectionIndex;

        /// <summary>Section heading, when one was extracted from the document.</summary>
        public string sectionTitle;

        /// <summary>Zero-based position of this chunk inside its parent section.</summary>
        public int chunkIndex;
    }
}
