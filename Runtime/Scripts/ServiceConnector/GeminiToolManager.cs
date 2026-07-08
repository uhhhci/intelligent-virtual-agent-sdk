using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;
using IVH.Core.ServiceConnector.Gemini.Realtime;
using IVH.Core.IntelligentVirtualAgent.Tools;

namespace IVH.Core.IntelligentVirtualAgent
{
    [RequireComponent(typeof(IGeminiAgent))]
    public class GeminiToolManager : MonoBehaviour
    {
        public List<GeminiDynamicTool> definedTools = new List<GeminiDynamicTool>();

        [Header("Attribute-based Tools")]
        [Tooltip("Komponenten, deren mit [GeminiTool] markierte Methoden automatisch als Tools " +
                 "registriert werden. Das Parameter-Schema wird aus der Methodensignatur generiert.")]
        public List<MonoBehaviour> toolProviders = new List<MonoBehaviour>();

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

            RegisterAttributeTools();
        }

        // Scannt alle toolProviders nach [GeminiTool]-Methoden und registriert sie mit
        // automatisch aus der Signatur generiertem Parameter-Schema.
        private void RegisterAttributeTools()
        {
            foreach (var provider in toolProviders)
            {
                if (provider == null)
                {
                    Debug.LogWarning("[Gemini Tools] A toolProviders entry is null (empty slot in the list). Skipping.");
                    continue;
                }

                int registeredForProvider = 0;
                var methods = provider.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public);
                foreach (var method in methods)
                {
                    var attr = method.GetCustomAttribute<GeminiToolAttribute>();
                    if (attr == null) continue;

                    string toolName = string.IsNullOrEmpty(attr.Name) ? method.Name : attr.Name;
                    string safeToolName = System.Text.RegularExpressions.Regex.Replace(toolName, @"[^a-zA-Z0-9_-]", "_").ToLower();

                    if (_toolCache.ContainsKey(safeToolName))
                    {
                        Debug.LogWarning($"[Gemini Tools] Tool '{safeToolName}' from {provider.name}.{method.Name} " +
                                         $"is already registered (duplicate name). Skipping.");
                        continue;
                    }

                    JObject parametersSchema = BuildSchemaFromMethod(method);

                    var decl = new JObject
                    {
                        ["name"] = safeToolName,
                        ["description"] = attr.Description,
                        ["parameters"] = parametersSchema
                    };

                    _toolCache[safeToolName] = new CachedTool
                    {
                        // OriginalTool trägt nur die für die Ausführung nötige Ziel-Referenz.
                        OriginalTool = new GeminiDynamicTool
                        {
                            toolName = toolName,
                            description = attr.Description,
                            targetComponent = provider,
                            targetMethodName = method.Name,
                            parametersJson = parametersSchema.ToString()
                        },
                        Method = method,
                        Parameters = method.GetParameters(),
                        SchemaDeclaration = decl
                    };

                    registeredForProvider++;
                }

                if (registeredForProvider == 0)
                {
                    Debug.LogWarning($"[Gemini Tools] Provider '{provider.name}' ({provider.GetType().Name}) " +
                                     $"has no [GeminiTool] methods — nothing registered from it.");
                }
            }
        }

        // Baut ein JSON-Schema-Objekt aus den Parametern einer Methode.
        private JObject BuildSchemaFromMethod(MethodInfo method)
        {
            var properties = new JObject();
            var required = new JArray();

            foreach (var p in method.GetParameters())
            {
                JArray enumValues;
                string jsonType = MapClrTypeToJsonType(p.ParameterType, out enumValues);

                var propSchema = new JObject { ["type"] = jsonType };
                if (enumValues != null) propSchema["enum"] = enumValues;

                var paramAttr = p.GetCustomAttribute<GeminiToolParamAttribute>();
                if (paramAttr != null && !string.IsNullOrEmpty(paramAttr.Description))
                    propSchema["description"] = paramAttr.Description;

                properties[p.Name] = propSchema;

                // Parameter ohne Default-Wert sind Pflicht.
                if (!p.HasDefaultValue) required.Add(p.Name);
            }

            var schema = new JObject
            {
                ["type"] = "object",
                ["properties"] = properties
            };
            // Leeres "required" weglassen (Gemini akzeptiert sonst teils keine Deklaration).
            if (required.Count > 0) schema["required"] = required;

            return schema;
        }

        // Bildet einen C#-Typ auf den passenden JSON-Schema-Typ ab. enumValues wird nur bei Enums gesetzt.
        private string MapClrTypeToJsonType(Type type, out JArray enumValues)
        {
            enumValues = null;

            if (type.IsEnum)
            {
                enumValues = new JArray();
                foreach (var name in Enum.GetNames(type)) enumValues.Add(name);
                return "string";
            }
            if (type == typeof(bool)) return "boolean";
            if (type == typeof(string)) return "string";
            if (type == typeof(float) || type == typeof(double) || type == typeof(decimal)) return "number";
            if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte)) return "integer";

            // Fallback: unbekannte Typen als String deklarieren.
            return "string";
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
                            // Der Wert konnte nicht gebunden werden: Meistens weicht der Property-Name im
                            // parametersJson-Schema vom C#-Parameternamen ab. Das ist die häufigste Fehlerquelle,
                            // deshalb warnen wir explizit statt still null/default einzusetzen.
                            Debug.LogWarning(
                                $"[Gemini Tools] Argument '{paramInfo.Name}' for tool '{toolName}' was not found in the " +
                                $"call arguments (got: [{string.Join(", ", ((JObject)args).Properties().Select(p => p.Name))}]). " +
                                $"Check that the parametersJson property name matches the C# parameter name. Using default value.");

                            invokeArgs[i] = paramInfo.HasDefaultValue ? paramInfo.DefaultValue :
                                            (paramInfo.ParameterType.IsValueType ? Activator.CreateInstance(paramInfo.ParameterType) : null);
                        }
                    }
                }

                object result = cached.Method.Invoke(cached.OriginalTool.targetComponent, invokeArgs);

                await _wrapper.SendGenericToolResponseAsync(callId, toolName, result ?? (object) new { status = "success" });
            }
            catch (Exception e)
            {
                // Method.Invoke verpackt Ausnahmen der Zielmethode in eine TargetInvocationException,
                // deren Message immer "Exception has been thrown by the target of an invocation" lautet.
                // Wir entpacken die echte Ursache, damit Typ, Meldung und Stacktrace im Log erscheinen.
                Exception actual = (e as TargetInvocationException)?.InnerException ?? e;
                Debug.LogError(
                    $"[Gemini Tools] Execution Error in '{toolName}': " +
                    $"{actual.GetType().Name}: {actual.Message}\n{actual.StackTrace}");
                await _wrapper.SendGenericToolResponseAsync(callId, toolName, new { error = actual.Message });
            }
        }
    }
}