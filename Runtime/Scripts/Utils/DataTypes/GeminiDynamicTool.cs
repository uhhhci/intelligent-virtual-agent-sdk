using System;
using System.Collections.Generic;
using UnityEngine;

namespace IVH.Core.IntelligentVirtualAgent.Tools
{
    [Serializable]
    public class GeminiDynamicTool
    {
        public string toolName;
        [TextArea(2, 4)] public string description;
        
        [Header("Target Execution")]
        public MonoBehaviour targetComponent;
        public string targetMethodName;

        [Header("Parameters Schema (JSON)")]
        [Tooltip("Define properties and required fields. e.g. { \"type\": \"object\", \"properties\": { ... } }")]
        [TextArea(3, 8)] 
        public string parametersJson = "{\n  \"type\": \"object\",\n  \"properties\": {},\n  \"required\": []\n}";
    }
}