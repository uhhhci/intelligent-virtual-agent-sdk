using System.Collections.Generic;
using System.Threading.Tasks;

namespace IVH.Core.Memory
{
    /// <summary>
    /// Persistent vector store abstraction. Implementations may use in-memory lists, JSON files,
    /// SQLite, or remote vector databases (Chroma, Pinecone).
    /// </summary>
    public interface IMemoryStore
    {
        /// <summary>Persist a memory item. Idempotent by <see cref="MemoryItem.id"/>.</summary>
        Task AddAsync(MemoryItem item);

        /// <summary>
        /// Return up to <paramref name="topK"/> items most similar (cosine) to the query vector,
        /// optionally filtered by user id. Items are returned in descending similarity order.
        /// </summary>
        Task<List<(MemoryItem item, float similarity)>> QueryAsync(float[] queryVector, int topK, string userId = null);

        /// <summary>Remove all items for the given user. Pass null to clear the whole store.</summary>
        Task ClearAsync(string userId = null);
    }
}
