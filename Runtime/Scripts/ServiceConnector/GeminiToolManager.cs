using System;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json.Linq;
using IVH.Core.ServiceConnector.Gemini.Realtime;
using IVH.Core.IntelligentVirtualAgent.Tools;
using IVH.Core.Utils.Logging;
using IVH.Core.Exceptions;

namespace IVH.Core.IntelligentVirtualAgent
{
    /// <summary>
    /// Registers and services user-defined tools exposed to Gemini. Each <see cref="GeminiDynamicTool"/>
    /// entry is translated into a Gemini function declaration at session start and invoked via reflection
    /// when the model calls it. Attach this component to the same GameObject as an <see cref="IGeminiAgent"/>.
    /// </summary>
    [RequireComponent(typeof(IGeminiAgent))]
    public class GeminiToolManager : MonoBehaviour
    {
        /// <summary>
        /// Tools to advertise to Gemini. Populate in the Inspector — each entry binds a tool name
        /// and parameter schema to a public method on a target MonoBehaviour.
        /// </summary>
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

                string safeToolName = System.Text.RegularExpressions.Regex.Replace(tool.toolName, @"[^a-zA-Z0-9_-]", "_").ToLower();

                MethodInfo method = tool.targetComponent.GetType().GetMethod(tool.targetMethodName, BindingFlags.Instance | BindingFlags.Public);
                if (method == null)
                {
                    Debug.LogError($"[Gemini Tools] Method '{tool.targetMethodName}' not found on {tool.targetComponent.name}. Skipping.");
                    continue;
                }

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

                _toolCache[safeToolName] = new CachedTool
                {
                    OriginalTool = tool,
                    Method = method,
                    Parameters = method.GetParameters(),
                    SchemaDeclaration = decl // <-- Add this line!
                };
            }
        }

        /// <summary>
        /// Returns the function declarations for all registered tools, in the shape Gemini expects
        /// in the session <c>setup.tools</c> field. Called by agents at connect time.
        /// </summary>
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
                IVALogger.Warn("GeminiTools", $"AI tried to call '{toolName}', but it is not registered.");
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

                object returnValue = cached.Method.Invoke(cached.OriginalTool.targetComponent, invokeArgs);
                object responsePayload = await ResolveToolResultAsync(cached.Method.ReturnType, returnValue);

                await _wrapper.SendGenericToolResponseAsync(callId, toolName, responsePayload);
            }
            catch (Exception e)
            {
                // Unwrap reflection's TargetInvocationException so the user sees their actual error.
                var inner = (e is TargetInvocationException tie && tie.InnerException != null) ? tie.InnerException : e;
                var toolEx = new ToolExecutionException(toolName, inner.Message, inner);
                IVALogger.Error("GeminiTools", $"Tool '{toolName}' execution error: {inner.Message}", toolEx);
                await _wrapper.SendGenericToolResponseAsync(callId, toolName, new { error = inner.Message });
            }
        }

        /// <summary>
        /// Normalizes a tool method's return value into the JSON payload sent back to Gemini. Awaits
        /// <see cref="Task"/> / <see cref="Task{TResult}"/> so async tools (e.g. knowledge retrieval)
        /// can return data; a non-generic Task or a void method yields a bare success acknowledgment,
        /// preserving the pre-existing behavior for the avatar/locomotion tools.
        /// </summary>
        private static async Task<object> ResolveToolResultAsync(Type returnType, object returnValue)
        {
            if (returnValue is Task task)
            {
                await task;
                if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    object value = returnType.GetProperty("Result")?.GetValue(task);
                    return value != null ? new { status = "success", result = value } : (object)new { status = "success" };
                }
                return new { status = "success" };
            }

            if (returnValue != null && returnType != typeof(void))
            {
                return new { status = "success", result = returnValue };
            }

            return new { status = "success" };
        }
    }
}