using System;
using System.Threading.Tasks;
using UnityEngine;
using IVH.Core.IntelligentVirtualAgent;
using IVH.Core.IntelligentVirtualAgent.Tools;
using IVH.Core.Utils.Logging;

namespace IVH.Core.Knowledge
{
    /// <summary>
    /// Exposes the knowledge base to a Gemini Live agent as a callable <c>search_knowledge</c> function
    /// tool, so the model retrieves relevant chunks per turn using the user's actual question. This is
    /// the scalable alternative to one-shot injection (<see cref="DocumentGroundingComponent"/> with
    /// <c>injectAtSessionStart</c>) and to full-corpus injection
    /// (<see cref="FullDocumentContextProvider"/>): only the relevant chunks enter the prompt, and the
    /// corpus may be arbitrarily large. The trade-off is one extra model round-trip on turns where the
    /// model decides to search.
    /// </summary>
    /// <remarks>
    /// Place on the agent GameObject alongside a <see cref="DocumentGroundingComponent"/> (the
    /// retrieval backend — its KnowledgeBase must be enabled and baked) and a
    /// <see cref="GeminiToolManager"/> (the function-call plumbing). With <see cref="autoRegister"/>
    /// on, the tool injects its own <see cref="GeminiDynamicTool"/> entry before the manager reads its
    /// list — guaranteed by <see cref="DefaultExecutionOrder"/> running this <c>Awake</c> first — so no
    /// manual Inspector wiring is needed. For a pure retrieval demo, turn the
    /// <see cref="DocumentGroundingComponent"/>'s <c>injectAtSessionStart</c> off so it serves only as
    /// the backend and does not also inject a one-shot prefix.
    /// </remarks>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(DocumentGroundingComponent))]
    public class KnowledgeRetrievalTool : MonoBehaviour
    {
        [Tooltip("Public tool name advertised to Gemini. Sanitized to snake_case at registration.")]
        public string toolName = "search_knowledge";

        [TextArea(2, 5)]
        [Tooltip("Description Gemini reads when deciding whether to call the tool. Tell it WHEN to search.")]
        public string toolDescription =
            "Search your personal knowledge base for accurate, detailed facts about yourself, your " +
            "research, career, collaborators, students, and publications. Call this BEFORE answering " +
            "whenever the user asks for a specific detail you are not completely certain about.";

        [Tooltip("Automatically register this tool with the GeminiToolManager on this GameObject at startup. Turn off to wire a GeminiDynamicTool entry by hand instead.")]
        public bool autoRegister = true;

        private DocumentGroundingComponent _grounding;

        private void Awake()
        {
            _grounding = GetComponent<DocumentGroundingComponent>();
            if (autoRegister) RegisterWithToolManager();
        }

        private void RegisterWithToolManager()
        {
            var toolManager = GetComponent<GeminiToolManager>();
            if (toolManager == null)
            {
                IVALogger.Warn("KnowledgeRetrievalTool",
                    "No GeminiToolManager on this GameObject; cannot auto-register the search tool. Add a GeminiToolManager, or wire a GeminiDynamicTool entry manually.");
                return;
            }

            // Skip if an entry with this name already exists (e.g. hand-wired in the Inspector), so we
            // never advertise a duplicate function declaration to Gemini.
            if (toolManager.definedTools.Exists(t =>
                    t != null && string.Equals(t.toolName, toolName, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            toolManager.definedTools.Add(new GeminiDynamicTool
            {
                toolName = toolName,
                description = toolDescription,
                targetComponent = this,
                targetMethodName = nameof(SearchKnowledge),
                parametersJson =
                    "{\n" +
                    "  \"type\": \"object\",\n" +
                    "  \"properties\": {\n" +
                    "    \"query\": {\n" +
                    "      \"type\": \"string\",\n" +
                    "      \"description\": \"What to look up, phrased as a natural-language question or keywords.\"\n" +
                    "    }\n" +
                    "  },\n" +
                    "  \"required\": [\"query\"]\n" +
                    "}"
            });
        }

        /// <summary>
        /// Retrieves the most relevant knowledge-base chunks for <paramref name="query"/> and returns
        /// them as text for the model to ground its answer in. Invoked by Gemini through the
        /// <c>search_knowledge</c> tool call; the returned string is forwarded back as the tool result.
        /// </summary>
        /// <param name="query">Natural-language search query supplied by the model.</param>
        /// <returns>Formatted source chunks, or a short status message when nothing matches.</returns>
        public async Task<string> SearchKnowledge(string query)
        {
            if (_grounding == null) _grounding = GetComponent<DocumentGroundingComponent>();
            if (_grounding == null) return "The knowledge base is not configured.";
            return await _grounding.SearchAsync(query);
        }
    }
}
