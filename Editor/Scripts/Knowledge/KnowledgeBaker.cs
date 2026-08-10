using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using IVH.Core.Knowledge;
using IVH.Core.Knowledge.Chunkers;
using IVH.Core.Knowledge.Readers;
using IVH.Core.Memory;
using IVH.Core.Memory.Embedders;
using IVH.Core.Utils;

namespace IVH.Core.Knowledge.EditorScripts
{
    /// <summary>
    /// Editor-time baker that turns a <see cref="KnowledgeBase"/> asset's Markdown documents into
    /// a JSON-serialized <see cref="MemoryItem"/> store. The runtime
    /// <see cref="DocumentGroundingComponent"/> loads this store directly via
    /// <see cref="Memory.Stores.JsonFileMemoryStore"/> — no parsing, chunking, or ingestion-time
    /// embedding calls happen at runtime.
    /// </summary>
    /// <remarks>
    /// Accessed via <c>IVA SDK / Knowledge / Bake Selected KnowledgeBase</c>. The Gemini
    /// embedding API key is read from <c>~/.aiapi/auth.json</c> via
    /// <see cref="GeneralModelHelper.GetGeminiApiKey"/> — the same secure location every other
    /// Gemini service in this SDK uses. Set it via the IVA Setup Wizard. As a fallback for
    /// users without the auth file, an <see cref="EditorPrefs"/> key can also be set via
    /// <c>IVA SDK / Knowledge / Set Embedder API Key</c>.
    /// </remarks>
    public static class KnowledgeBaker
    {
        /// <summary>EditorPrefs key under which the Gemini embedding API key is persisted across sessions when ~/.aiapi/auth.json is not available.</summary>
        public const string ApiKeyEditorPref = "IVA_SDK_GeminiEmbedderKey";

        [MenuItem("IVA SDK/Knowledge/Bake Selected KnowledgeBase", true)]
        private static bool BakeSelectedValidate()
        {
            return Selection.activeObject is KnowledgeBase;
        }

        [MenuItem("IVA SDK/Knowledge/Bake Selected KnowledgeBase")]
        public static void BakeSelected()
        {
            var kb = Selection.activeObject as KnowledgeBase;
            if (kb == null)
            {
                EditorUtility.DisplayDialog("IVA Knowledge Baker",
                    "Select a KnowledgeBase asset in the Project window first.", "OK");
                return;
            }
            _ = BakeAsync(kb);
        }

        [MenuItem("IVA SDK/Knowledge/Set Embedder API Key...")]
        public static void SetApiKey()
        {
            ApiKeyPromptWindow.Open();
        }

        /// <summary>
        /// Bakes the supplied <see cref="KnowledgeBase"/> end-to-end: parse Markdown → chunk →
        /// embed → write JSON store → update asset metadata. Returns once the baked file is
        /// flushed and the asset is saved.
        /// </summary>
        public static async Task BakeAsync(KnowledgeBase kb)
        {
            // Prefer the project-wide secure location (~/.aiapi/auth.json) that every other
            // Gemini service in this SDK uses. Fall back to EditorPrefs for users who already
            // set a key there in an earlier preview, or who want a project-only override.
            string apiKey = GeneralModelHelper.GetGeminiApiKey();
            if (string.IsNullOrEmpty(apiKey))
            {
                apiKey = EditorPrefs.GetString(ApiKeyEditorPref, "");
            }
            if (string.IsNullOrEmpty(apiKey))
            {
                EditorUtility.DisplayDialog("IVA Knowledge Baker",
                    "No Gemini embedding API key found. Set 'gemini_api_key' in ~/.aiapi/auth.json (recommended — use 'IVA SDK / Setup Wizard'), or set the EditorPrefs fallback via 'IVA SDK / Knowledge / Set Embedder API Key...'.",
                    "OK");
                return;
            }

            if (kb.markdownDocuments == null || kb.markdownDocuments.Count == 0)
            {
                EditorUtility.DisplayDialog("IVA Knowledge Baker",
                    "Add at least one Markdown TextAsset to the KnowledgeBase before baking.", "OK");
                return;
            }

            // Write into StreamingAssets so the baked file is included in standalone builds
            // automatically. Stored path on the asset is the file name only — runtime resolves
            // it against Application.streamingAssetsPath.
            const string SubDir = "IVA_Knowledge";
            string streamingDirRelative = "Assets/StreamingAssets/" + SubDir;
            string streamingDirAbsolute = Path.Combine(Directory.GetCurrentDirectory(), streamingDirRelative);
            Directory.CreateDirectory(streamingDirAbsolute);
            string storeFileName = kb.name + ".knowledge.json";
            string storeAbsolute = Path.Combine(streamingDirAbsolute, storeFileName);

            var reader = new MarkdownReader();
            var chunker = new SlidingWindowChunker(kb.chunkCharSize, kb.chunkCharOverlap);
            var embedder = new GeminiEmbedder(apiKey);

            var allChunks = new List<TextChunk>();
            foreach (TextAsset doc in kb.markdownDocuments)
            {
                if (doc == null) continue;
                string fileName = doc.name + ".md";
                foreach (var section in reader.Read(fileName, doc.text))
                {
                    allChunks.AddRange(chunker.Chunk(section));
                }
            }

            if (allChunks.Count == 0)
            {
                EditorUtility.DisplayDialog("IVA Knowledge Baker",
                    "Parsed and chunked the documents but produced 0 chunks — they may be empty.", "OK");
                return;
            }

            var items = new List<MemoryItem>(allChunks.Count);
            try
            {
                for (int i = 0; i < allChunks.Count; i++)
                {
                    var chunk = allChunks[i];
                    bool cancelled = EditorUtility.DisplayCancelableProgressBar(
                        "IVA Knowledge Baker",
                        $"Embedding chunk {i + 1} / {allChunks.Count} ({chunk.sourceFile} §{chunk.sectionIndex})",
                        (float)i / allChunks.Count);
                    if (cancelled)
                    {
                        EditorUtility.ClearProgressBar();
                        EditorUtility.DisplayDialog("IVA Knowledge Baker", "Bake cancelled. No changes written.", "OK");
                        return;
                    }

                    float[] vector = await embedder.EmbedAsync(BuildEmbeddingInput(chunk));
                    var citation = new ChunkCitation
                    {
                        sourceFile = chunk.sourceFile,
                        sectionIndex = chunk.sectionIndex,
                        sectionTitle = chunk.sectionTitle,
                        chunkIndex = chunk.chunkIndex,
                    };
                    items.Add(new MemoryItem
                    {
                        id = Guid.NewGuid().ToString("N"),
                        sessionId = null,
                        userId = null,
                        text = chunk.text,
                        vector = vector,
                        metadataJson = citation.ToJson(),
                        createdAtUtcTicks = DateTime.UtcNow.Ticks,
                    });
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            string json = JsonConvert.SerializeObject(items);
            File.WriteAllText(storeAbsolute, json);

            kb.bakedStorePath = storeFileName;
            kb.embeddingDimension = embedder.Dimension;
            kb.bakedChunkCount = items.Count;
            kb.lastBakedAtUtcTicks = DateTime.UtcNow.Ticks;
            EditorUtility.SetDirty(kb);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("IVA Knowledge Baker",
                $"Baked {items.Count} chunks → {streamingDirRelative}/{storeFileName}\nEmbedding dim: {embedder.Dimension}", "OK");
        }

        // Prepending the section heading to the chunk body sharpens retrieval — embeddings see the
        // topical hint along with the body text.
        private static string BuildEmbeddingInput(TextChunk chunk)
        {
            if (string.IsNullOrEmpty(chunk.sectionTitle)) return chunk.text;
            return chunk.sectionTitle + "\n\n" + chunk.text;
        }
    }

    /// <summary>
    /// Minimal modal window that captures the Gemini embedding API key into <see cref="EditorPrefs"/>.
    /// Shown via <c>IVA SDK / Knowledge / Set Embedder API Key...</c>; persists across Unity restarts.
    /// </summary>
    public class ApiKeyPromptWindow : EditorWindow
    {
        private string _key = "";

        public static void Open()
        {
            var win = GetWindow<ApiKeyPromptWindow>(true, "IVA Knowledge — Embedder API Key", true);
            win.minSize = new Vector2(420, 130);
            win._key = EditorPrefs.GetString(KnowledgeBaker.ApiKeyEditorPref, "");
            win.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Google AI Studio key used at bake time to embed your Markdown corpus. Stored in EditorPrefs on this machine only — never written into the project.",
                MessageType.Info);
            _key = EditorGUILayout.PasswordField("API key", _key);
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save"))
                {
                    EditorPrefs.SetString(KnowledgeBaker.ApiKeyEditorPref, _key ?? "");
                    Close();
                }
                if (GUILayout.Button("Cancel"))
                {
                    Close();
                }
            }
        }
    }
}
