using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;
using IVH.Core.ServiceConnector.Gemini.Realtime;
using IVH.Core.IntelligentVirtualAgent.Tools;

namespace IVH.Core.IntelligentVirtualAgent
{
    [RequireComponent(typeof(GeminiVoiceOnlyAgent))]
    public class GeminiToolManager : MonoBehaviour
    {
        public List<GeminiDynamicTool> definedTools = new List<GeminiDynamicTool>();
        
        private GeminiRealtimeWrapper _wrapper;
        
        private class CachedTool
        {
            public GeminiDynamicTool OriginalTool;
            public MethodInfo Method;
            public ParameterInfo[] Parameters;
            public JObject SchemaDeclaration;
        }
        
        private Dictionary<string, CachedTool> _toolCache = new Dictionary<string, CachedTool>(StringComparer.OrdinalIgnoreCase);

        private void Awake()
        {
            _wrapper = GetComponent<GeminiRealtimeWrapper>();
            _wrapper.OnGenericToolCallReceived += HandleDynamicToolCall;

            // Pre-calculate everything when the scene starts
            InitializeToolCache();
        }

        private void OnDestroy()
        {
            if (_wrapper != null) _wrapper.OnGenericToolCallReceived -= HandleDynamicToolCall;
        }

        private void InitializeToolCache()
        {
            _toolCache.Clear();

            foreach (var tool in definedTools)
            {
                if (string.IsNullOrEmpty(tool.toolName) || tool.targetComponent == null) continue;

                // 1. Calculate the safe name ONCE
                string safeToolName = System.Text.RegularExpressions.Regex.Replace(tool.toolName, @"[^a-zA-Z0-9_-]", "_").ToLower();

                // 2. Perform Reflection ONCE
                MethodInfo method = tool.targetComponent.GetType().GetMethod(tool.targetMethodName, BindingFlags.Instance | BindingFlags.Public);
                if (method == null)
                {
                    Debug.LogError($"[Gemini Tools] Method '{tool.targetMethodName}' not found on {tool.targetComponent.name}. Skipping.");
                    continue;
                }

                // 3. Pre-build the JSON schema ONCE
                var decl = new JObject
                {
                    ["name"] = safeToolName,
                    ["description"] = tool.description
                };

                if (!string.IsNullOrWhiteSpace(tool.parametersJson))
                {
                    try 
                    { 
                        JObject parsedParams = JObject.Parse(tool.parametersJson); 
                        if (parsedParams["required"] is JArray reqArray && reqArray.Count == 0)
                            parsedParams.Remove("required");
                        decl["parameters"] = parsedParams; 
                    }
                    catch (Exception e) 
                    { 
                        Debug.LogError($"[Gemini Tools] Invalid JSON in '{tool.toolName}': {e.Message}"); 
                        continue;
                    }
                }
                else
                {
                    decl["parameters"] = new JObject { ["type"] = "object", ["properties"] = new JObject() };
                }

                // 4. Save to Cache
                _toolCache[safeToolName] = new CachedTool
                {
                    OriginalTool = tool,
                    Method = method,
                    Parameters = method.GetParameters(), // Cache parameter info array
                    SchemaDeclaration = decl
                };
            }
        }

        public JArray GetDynamicToolDeclarations()
        {
            JArray declarations = new JArray();
            foreach (var cached in _toolCache.Values)
            {
                declarations.Add(cached.SchemaDeclaration);
            }
            return declarations;
        }

        private async void HandleDynamicToolCall(string callId, string toolName, JToken args)
        {
            if (!_toolCache.TryGetValue(toolName, out CachedTool cached))
            {
                Debug.LogWarning($"[Gemini Tools] AI tried to call '{toolName}', but it is not registered.");
                await _wrapper.SendGenericToolResponseAsync(callId, toolName, new { error = "Tool not found" });
                return;
            }

            try
            {
                object[] invokeArgs = new object[cached.Parameters.Length];

                if (cached.Parameters.Length > 0 && args != null && args.Type == JTokenType.Object)
                {
                    JObject jsonArgs = (JObject)args;
                    
                    for (int i = 0; i < cached.Parameters.Length; i++)
                    {
                        ParameterInfo paramInfo = cached.Parameters[i];
                        
                        if (jsonArgs.TryGetValue(paramInfo.Name, StringComparison.OrdinalIgnoreCase, out JToken tokenValue))
                        {
                            invokeArgs[i] = tokenValue.ToObject(paramInfo.ParameterType);
                        }
                        else
                        {
                            invokeArgs[i] = paramInfo.HasDefaultValue ? paramInfo.DefaultValue : 
                                            (paramInfo.ParameterType.IsValueType ? Activator.CreateInstance(paramInfo.ParameterType) : null);
                        }
                    }
                }

                cached.Method.Invoke(cached.OriginalTool.targetComponent, invokeArgs);

                await _wrapper.SendGenericToolResponseAsync(callId, toolName, new { status = "success" });
            }
            catch (Exception e)
            {
                Debug.LogError($"[Gemini Tools] Execution Error: {e.Message}");
                await _wrapper.SendGenericToolResponseAsync(callId, toolName, new { error = e.Message });
            }
        }
    }
}