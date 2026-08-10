using UnityEngine;

namespace IVH.Core.Memory
{
    /// <summary>
    /// Configuration for <see cref="MemoryManager"/>. Controls whether cross-session memory is on,
    /// which store backend to use, ingestion cadence, and retrieval depth.
    /// </summary>
    [CreateAssetMenu(fileName = "MemoryConfig", menuName = "IVA SDK/Memory Config", order = 130)]
    public class MemoryConfig : ScriptableObject
    {
        public enum StoreType { JsonFile, InMemory }

        [Tooltip("Master switch. Default off for backward compatibility.")]
        public bool enabled = false;

        [Tooltip("Where to persist memories. JsonFile is zero-config, cross-platform.")]
        public StoreType storeType = StoreType.JsonFile;

        [Tooltip("Optional custom file path for JsonFile store. Leave blank for default persistentDataPath.")]
        public string storePathOverride = "";

        [Tooltip("After this many user/agent turns, summarize and ingest as a memory item.")]
        [Range(2, 20)] public int summarizeEveryNTurns = 4;

        [Tooltip("How many top-matching memories to inject into the system prompt at session start.")]
        [Range(1, 10)] public int retrievalTopK = 5;
    }
}
