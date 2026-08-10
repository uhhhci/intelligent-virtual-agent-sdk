using System;

namespace IVH.Core.Memory
{
    /// <summary>
    /// A single unit of long-term memory: a chunk of conversational context plus its
    /// vector representation for retrieval. Items are typically conversation summaries
    /// produced every N turns, not raw transcripts.
    /// </summary>
    [Serializable]
    public class MemoryItem
    {
        public string id;
        public string sessionId;
        public string userId;
        public string text;
        public float[] vector;
        public string metadataJson;
        public long createdAtUtcTicks;

        public DateTime CreatedAt => new DateTime(createdAtUtcTicks, DateTimeKind.Utc);
    }
}
