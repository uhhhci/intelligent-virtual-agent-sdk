using System.Collections.Generic;
using IVH.Core.Knowledge.Readers;

namespace IVH.Core.Knowledge.Chunkers
{
    /// <summary>
    /// Splits a document section into retrieval-sized text chunks. Chunkers are pure functions
    /// over already-parsed <see cref="DocumentSection"/> input; document parsing happens in
    /// <see cref="IDocumentReader"/>.
    /// </summary>
    public interface IChunker
    {
        /// <summary>Splits a single section into one or more chunks.</summary>
        IEnumerable<TextChunk> Chunk(DocumentSection section);
    }
}
