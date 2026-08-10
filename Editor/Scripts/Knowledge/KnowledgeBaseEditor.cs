using System;
using UnityEditor;
using UnityEngine;
using IVH.Core.Knowledge;

namespace IVH.Core.Knowledge.EditorScripts
{
    /// <summary>
    /// Custom inspector for <see cref="KnowledgeBase"/>. Renders the default property grid plus
    /// a one-click "Bake" button and a status block (chunk count, last-baked time, baked path)
    /// so the developer never has to dig into the menu bar to refresh the store.
    /// </summary>
    [CustomEditor(typeof(KnowledgeBase))]
    public class KnowledgeBaseEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var kb = (KnowledgeBase)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Bake", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(kb.markdownDocuments == null || kb.markdownDocuments.Count == 0))
            {
                if (GUILayout.Button("Bake Now"))
                {
                    _ = KnowledgeBaker.BakeAsync(kb);
                }
            }

            string status = kb.lastBakedAtUtcTicks > 0
                ? $"{kb.bakedChunkCount} chunks · dim {kb.embeddingDimension} · baked {new DateTime(kb.lastBakedAtUtcTicks, DateTimeKind.Utc).ToLocalTime():yyyy-MM-dd HH:mm}"
                : "Not baked yet.";
            EditorGUILayout.HelpBox(status, MessageType.None);

            if (!string.IsNullOrEmpty(kb.bakedStorePath))
            {
                EditorGUILayout.SelectableLabel(kb.bakedStorePath, EditorStyles.miniLabel, GUILayout.Height(16));
            }
        }
    }
}
