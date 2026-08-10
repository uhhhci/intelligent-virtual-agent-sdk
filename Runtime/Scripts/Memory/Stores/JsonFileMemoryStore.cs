using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using IVH.Core.Utils.Logging;

namespace IVH.Core.Memory.Stores
{
    /// <summary>
    /// Zero-dependency memory store. Persists items as a JSON file on disk and does cosine
    /// similarity in C#. Suitable for single-user desktop apps and research studies (&lt;10k items).
    /// For larger scale, swap in a remote store implementation of <see cref="IMemoryStore"/>.
    /// </summary>
    public class JsonFileMemoryStore : IMemoryStore
    {
        private readonly string _filePath;
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        private List<MemoryItem> _items;

        public JsonFileMemoryStore(string filePath = null)
        {
            _filePath = filePath ?? Path.Combine(Application.persistentDataPath, "IVA", "memory", "memory.json");
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath));
        }

        private async Task EnsureLoadedAsync()
        {
            if (_items != null) return;
            await _lock.WaitAsync();
            try
            {
                if (_items != null) return;
                if (File.Exists(_filePath))
                {
                    try
                    {
                        string json = await Task.Run(() => File.ReadAllText(_filePath));
                        _items = JsonConvert.DeserializeObject<List<MemoryItem>>(json) ?? new List<MemoryItem>();
                    }
                    catch (Exception ex)
                    {
                        IVALogger.Warn("JsonFileMemoryStore", $"Could not load {_filePath}: {ex.Message}. Starting empty.");
                        _items = new List<MemoryItem>();
                    }
                }
                else
                {
                    _items = new List<MemoryItem>();
                }
            }
            finally { _lock.Release(); }
        }

        private async Task FlushAsync()
        {
            string json = JsonConvert.SerializeObject(_items);
            await Task.Run(() => File.WriteAllText(_filePath, json));
        }

        public async Task AddAsync(MemoryItem item)
        {
            if (item == null || item.vector == null) return;
            await EnsureLoadedAsync();
            await _lock.WaitAsync();
            try
            {
                int existing = _items.FindIndex(i => i.id == item.id);
                if (existing >= 0) _items[existing] = item;
                else _items.Add(item);
                await FlushAsync();
            }
            finally { _lock.Release(); }
        }

        public async Task<List<(MemoryItem item, float similarity)>> QueryAsync(float[] queryVector, int topK, string userId = null)
        {
            await EnsureLoadedAsync();
            if (queryVector == null || _items == null || _items.Count == 0)
                return new List<(MemoryItem, float)>();

            IEnumerable<MemoryItem> pool = _items;
            if (!string.IsNullOrEmpty(userId)) pool = pool.Where(i => i.userId == userId);

            return pool
                .Select(i => (item: i, similarity: Cosine(queryVector, i.vector)))
                .OrderByDescending(x => x.similarity)
                .Take(topK)
                .ToList();
        }

        public async Task ClearAsync(string userId = null)
        {
            await EnsureLoadedAsync();
            await _lock.WaitAsync();
            try
            {
                if (string.IsNullOrEmpty(userId)) _items.Clear();
                else _items.RemoveAll(i => i.userId == userId);
                await FlushAsync();
            }
            finally { _lock.Release(); }
        }

        private static float Cosine(float[] a, float[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return 0f;
            float dot = 0, na = 0, nb = 0;
            for (int i = 0; i < a.Length; i++)
            {
                dot += a[i] * b[i];
                na += a[i] * a[i];
                nb += b[i] * b[i];
            }
            float denom = Mathf.Sqrt(na) * Mathf.Sqrt(nb);
            return denom > 0 ? dot / denom : 0f;
        }
    }
}
