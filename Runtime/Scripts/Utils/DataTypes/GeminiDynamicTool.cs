using System;
using System.Collections.Generic;
using UnityEngine;

namespace IVH.Core.IntelligentVirtualAgent.Tools
{
    /// <summary>
    /// Serializable definition of a Gemini tool that binds a function declaration (name, description,
    /// JSON parameter schema) to a concrete method on a target MonoBehaviour. Registered with
    /// <see cref="GeminiToolManager"/> and invoked via reflection when Gemini calls the tool.
    /// </summary>
    [Serializable]
    public class GeminiDynamicTool
    {
        /// <summary>Public name advertised to Gemini. Sanitized to snake_case/alphanumerics at registration.</summary>
        public string toolName;

        /// <summary>Natural-language description Gemini sees when deciding whether to call this tool.</summary>
        [TextArea(2, 4)] public string description;

        [Header("Target Execution")]
        /// <summary>MonoBehaviour whose method will be invoked when the tool is called.</summary>
        public MonoBehaviour targetComponent;

        /// <summary>Exact case-sensitive name of the public method on <see cref="targetComponent"/> to invoke.</summary>
        public string targetMethodName;

        [Header("Parameters Schema (JSON)")]
        /// <summary>
        /// JSON schema describing the tool's parameters. Must match the signature of <see cref="targetMethodName"/>.
        /// Parameter names in <c>properties</c> are matched (case-insensitive) to the C# parameter names.
        /// </summary>
        [Tooltip("Define properties and required fields. e.g. { \"type\": \"object\", \"properties\": { ... } }")]
        [TextArea(3, 8)]
        public string parametersJson = "{\n  \"type\": \"object\",\n  \"properties\": {},\n  \"required\": []\n}";
    }
}