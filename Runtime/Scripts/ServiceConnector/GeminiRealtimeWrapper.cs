using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using IVH.Core.Utils;

namespace IVH.Core.ServiceConnector.Gemini.Realtime
{
    public enum GeminiModelType
    {
        //Flash20ExpGoogleAI,        // Expiring (Works on Alpha), disabled for now
        Flash25PreviewGoogleAI,   // High Latency (AI Studio - API Key)
        Flash25VertexAI          // Vertex AI Enterprise (Vertex - Service Account)
    }

    public class GeminiRealtimeWrapper : MonoBehaviour
    {
        [Header("Connection Settings")]
        private string apiKey;
        private string accessToken; // For Vertex
        public GeminiModelType selectedModel = GeminiModelType.Flash25PreviewGoogleAI;

        [Tooltip("Set to true for analyzing user's sentiments from audio. ")]
        public bool affectiveAnalysis = true; 

        [Tooltip("Compress context to extend session length.")]
        public bool contextWindowSliding = true; 

        // Events
        public Action OnSetupComplete; 
        public Action<byte[]> OnAudioReceived;
        public Action<string> OnTextReceived;
        public Action<string, string, string> OnCommandReceived;
        
        // 1. Add a generic event for dynamic tool calls
        public Action<string, string, JToken> OnGenericToolCallReceived;

        public bool verboseLogging = true;
        public bool IsConnected => _webSocket != null && _webSocket.State == WebSocketState.Open;

        private ClientWebSocket _webSocket;
        private CancellationTokenSource _cancellationTokenSource;
        
        // Thread Safety
        private readonly Queue<Action> _mainThreadQueue = new Queue<Action>();
        private readonly object _queueLock = new object();
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        
        // Endpoints
        private const string V1ALPHA_URL = "wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1alpha.GenerativeService.BidiGenerateContent";
        private const string V1BETA_URL = "wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent";
        
        // Don't use vertex AI v1beta1 is unstable, using v1 instead.
        private const string VERTEX_PROJECT_LOCATION = "us-central1"; 
        private const string VERTEX_URL_TEMPLATE = "wss://{0}-aiplatform.googleapis.com/ws/google.cloud.aiplatform.v1.LlmBidiService/BidiGenerateContent";

        private bool IsVertexModel() => selectedModel == GeminiModelType.Flash25VertexAI;

        private string GetModelString() => selectedModel switch
        {
            // This is the Google AI studio model, it will be deprecated on March 31, 2026.  Afterwards, use Flash25 VertexAI or Flash25PreviewGoogleAI instead
            //GeminiModelType.Flash20ExpGoogleAI => "gemini-2.0-flash-exp",
            
            // This is the Vertex AI Model ID. Vertex AI introduces more costs
            GeminiModelType.Flash25VertexAI => "gemini-live-2.5-flash-native-audio", 
            
            // This is the AI Studio Model ID
            GeminiModelType.Flash25PreviewGoogleAI => "gemini-2.5-flash-native-audio-preview-12-2025",

            _ => "gemini-2.5-flash-native-audio-preview-12-2025"
        };

        private string GetUrl(string projectId = "")
        {
            if (IsVertexModel())
            {
                // Vertex URL (us-central1-aiplatform...)
                Debug.Log(string.Format(VERTEX_URL_TEMPLATE, VERTEX_PROJECT_LOCATION));
                return string.Format(VERTEX_URL_TEMPLATE, VERTEX_PROJECT_LOCATION);
            }
            else
            {
                // Standard URL
                string baseUrl = V1BETA_URL;
                return $"{baseUrl}?key={apiKey}";
            }
        }

        private void Awake()
        {
            // Only need API key if NOT using Vertex
            if (!IsVertexModel())
                apiKey = GeneralModelHelper.GetGeminiApiKey();
        }

        private void Update()
        {
            lock (_queueLock)
            {
                while (_mainThreadQueue.Count > 0) _mainThreadQueue.Dequeue().Invoke();
            }
        }

        public async Task ConnectAsync(string systemInstruction, string voiceName)
        {
            await DisconnectAsync();

            string modelId = GetModelString();
            string finalUri = "";

            // --- AUTH SELECTION ---
            if (IsVertexModel())
            {
                try 
                {
                    Debug.Log("Authenticating with Vertex AI Service Account...");
                    // Looks in C:\Users\[USER]\.aiapi\service_account.json
                    var authResult = await VertexAuthHelper.GetAccessTokenFromUserDir("service_account.json");
                    
                    this.accessToken = authResult.accessToken;
                    
                    // Vertex requires FULL resource path for the model
                    modelId = $"projects/{authResult.projectId}/locations/{VERTEX_PROJECT_LOCATION}/publishers/google/models/{modelId}";
                    finalUri = GetUrl(authResult.projectId);
                }
                catch(Exception e)
                {
                    Debug.LogError($"Vertex Auth Failed: {e.Message}");
                    return;
                }
            }
            else
            {
                // Standard API Key check
                if (string.IsNullOrEmpty(apiKey)) apiKey = GeneralModelHelper.GetGeminiApiKey();
                if (string.IsNullOrEmpty(apiKey)) { Debug.LogError("API Key Missing!"); return; }
                finalUri = GetUrl();
            }
            
            _webSocket = new ClientWebSocket();
            
            // --- HEADER INJECTION ---
            if (IsVertexModel())
            {
                _webSocket.Options.SetRequestHeader("Authorization", $"Bearer {accessToken}");
            }

            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                Debug.Log($"Connecting to {modelId} ...");
                await _webSocket.ConnectAsync(new Uri(finalUri), CancellationToken.None);
                
                _ = ReceiveLoop();
                await SendSetupWithGenericTool(modelId, systemInstruction, voiceName);
            }
            catch (Exception e) { Debug.LogError($"Connection Error: {e.Message}"); }
        }

        public async Task DisconnectAsync()
        {
            if (_webSocket != null)
            {
                _cancellationTokenSource?.Cancel();
                if (_sendLock.CurrentCount == 0) _sendLock.Release();
                
                try { await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None); }
                catch { }
                
                _webSocket.Dispose();
                _webSocket = null;
            }
        }

        private async Task SendSetupWithGenericTool(string model, string systemPrompt, string voice)
        {
            var generationConfig = new JObject();
            generationConfig["response_modalities"] = new JArray("AUDIO");
            
            // disable thinking to improve latency -> this should work, but at the moment it is causing an internal server error. Clearly a bug from Google AI studio...
            if(selectedModel == GeminiModelType.Flash25VertexAI){
                var ThinkingConfig = new JObject
                {
                ["thinking_budget"]  = 0,
                ["include_thoughts"] = false,  
                };
                generationConfig["thinking_config"]=ThinkingConfig;
            }
            var speechConfig = new JObject();
            var voiceConfig = new JObject();
            voiceConfig["prebuilt_voice_config"] = new JObject { ["voice_name"] = voice };
            speechConfig["voice_config"] = voiceConfig;
            generationConfig["speech_config"] = speechConfig;

            var setupContent = new JObject
            {
                ["model"] = IsVertexModel() ? model : $"models/{model}",
                ["generation_config"] = generationConfig,
                ["system_instruction"] = new JObject { ["parts"] = new JArray(new JObject { ["text"] = systemPrompt }) }
            };
                
            var toolsArray = new JArray();
            var tool = new JObject();
            var avatarFunc = new JObject
            {
                ["name"] = "update_avatar_state",
                ["description"] = "Change the avatar's physical behavior.",
                ["parameters"] = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["action"] = new JObject { ["type"] = "string", ["description"] = "Body animation name" },
                        ["emotion"] = new JObject { ["type"] = "string", ["description"] = "Facial expression name" },
                        ["gaze"] = new JObject { ["type"] = "string", ["description"] = "Target: 'User' or 'Idle'" }
                    },
                    ["required"] = new JArray("action", "emotion", "gaze")
                }
            };
            tool["function_declarations"] = new JArray(avatarFunc);
            toolsArray.Add(tool);
            setupContent["tools"] = toolsArray;

            if (contextWindowSliding)
            {
                setupContent["context_window_compression"] = new JObject
                {
                    ["sliding_window"] = new JObject
                    {
                        ["targetTokens"] = 12800,
                    }
                };
            }

            var setupData = new JObject { ["setup"] = setupContent };

            if (verboseLogging) Debug.Log($"Sending Setup: {setupData.ToString(Formatting.None)}");
            
            await SendJsonAsync(setupData);
        }

        public void SendTextMessage(string text)
        {
            if (!IsConnected) return;
            var msg = new { client_content = new { turns = new[] { new { role = "user", parts = new[] { new { text = text } } } }, turn_complete = true } };
            _ = SendJsonAsync(msg);
        }

        public void SendAudioChunk(byte[] pcmData)
        {
            if (!IsConnected) return;
            var msg = new { realtime_input = new { media_chunks = new[] { new { mime_type = "audio/pcm", data = Convert.ToBase64String(pcmData) } } } };
            _ = SendJsonAsync(msg);
        }

        public void SendImage(byte[] imageData)
        {
            if (!IsConnected) return;
            var msg = new { realtime_input = new { media_chunks = new[] { new { mime_type = "image/jpeg", data = Convert.ToBase64String(imageData) } } } };
            _ = SendJsonAsync(msg);
        }


        private async Task SendToolResponse(string id)
        {
            var msg = new { tool_response = new { function_responses = new[] { new { id = id, name = "update_avatar_state", response = new { status = "ok" } } } } };
            await SendJsonAsync(msg);
        }

        private async Task SendJsonAsync(object data)
        {
            if (!IsConnected) return;
            
            await _sendLock.WaitAsync();
            try 
            {
                if (!IsConnected) return;
                string json = JsonConvert.SerializeObject(data, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                await _webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(json)), WebSocketMessageType.Text, true, CancellationToken.None); 
            }
            catch(Exception ex) { Debug.LogError($"Send Error: {ex.Message}"); }
            finally { _sendLock.Release(); }
        }

        private async Task ReceiveLoop()
        {
            var buffer = new byte[65536];
            try
            {
                while (IsConnected && !_cancellationTokenSource.IsCancellationRequested)
                {
                    var ms = new System.IO.MemoryStream();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cancellationTokenSource.Token);
                        ms.Write(buffer, 0, result.Count);
                    } while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Close) 
                    {
                        Debug.LogWarning($"Server Closed Connection. Status: {result.CloseStatus}");
                        break;
                    }

                    string jsonResponse = Encoding.UTF8.GetString(ms.ToArray());
                    ProcessMessage(jsonResponse);
                }
            }
            catch (Exception ex) 
            { 
                if(!_cancellationTokenSource.IsCancellationRequested) Debug.Log($"Receive Loop Stopped: {ex.Message}"); 
            }
        }

        private void ProcessMessage(string json)
        {
            try
            {
                var root = JObject.Parse(json);
                
                if (root["setupComplete"] != null || root["setup_complete"] != null)
                {
                    EnqueueMainThread(() => OnSetupComplete?.Invoke());
                    return;
                }

                // TOOL CALLS
                JToken toolCall = root["toolCall"] ?? root["tool_call"];
                if (toolCall != null)
                {
                    var fnCalls = toolCall["functionCalls"] ?? toolCall["function_calls"];
                    if (fnCalls != null)
                    {
                        foreach (var call in fnCalls)
                        {
                            if (call["name"]?.ToString() == "update_avatar_state")
                            {
                                var args = call["args"];
                                string act = args?["action"]?.ToString() ?? "";
                                string emo = args?["emotion"]?.ToString() ?? "";
                                string gaze = args?["gaze"]?.ToString() ?? "";
                                string id = call["id"]?.ToString();

                                EnqueueMainThread(() => OnCommandReceived?.Invoke(act, emo, gaze));
                                _ = SendToolResponse(id);
                            }
                            else
                            {
                                // handle generic function calling event other than updating avatar states
                                string toolName = call["name"]?.ToString();
                                string id = call["id"]?.ToString();
                                JToken args = call["args"];
                                EnqueueMainThread(() => OnGenericToolCallReceived?.Invoke(id, toolName, args));
                            }
                        }
                    }
                }

                // SERVER CONTENT
                JToken serverContent = root["serverContent"] ?? root["server_content"];
                if (serverContent != null)
                {
                    if (serverContent["interrupted"]?.Value<bool>() == true)
                    {
                         // Handle interruption
                    }

                    JToken parts = serverContent["modelTurn"]?["parts"] ?? serverContent["model_turn"]?["parts"];
                    if (parts != null)
                    {
                        foreach (var part in parts)
                        {
                            if (part["text"] != null) 
                                EnqueueMainThread(() => OnTextReceived?.Invoke(part["text"].ToString()));
                            
                            if (part["inlineData"] != null || part["inline_data"] != null)
                            {
                                JToken dataObj = part["inlineData"] ?? part["inline_data"];
                                byte[] audio = Convert.FromBase64String(dataObj["data"].ToString());
                                EnqueueMainThread(() => OnAudioReceived?.Invoke(audio));
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { if(verboseLogging) Debug.LogWarning($"Parse Error: {ex.Message}"); }
        }

        private void EnqueueMainThread(Action action) { lock (_queueLock) { _mainThreadQueue.Enqueue(action); } }

        // 2. Add an extended connection method that takes dynamic tools
        public async Task ConnectWithDynamicToolsAsync(string systemInstruction, string voiceName, JArray dynamicToolsDeclaration)
        {
            await DisconnectAsync();
            string modelId = GetModelString();
            string finalUri = IsVertexModel() ? GetUrl("vertex_project_id_placeholder") : GetUrl(); 
            // Note: ensure your auth logic from your existing ConnectAsync applies here

            _webSocket = new ClientWebSocket();
            if (IsVertexModel()) _webSocket.Options.SetRequestHeader("Authorization", $"Bearer {accessToken}");
            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                await _webSocket.ConnectAsync(new Uri(finalUri), CancellationToken.None);
                _ = ReceiveLoop();
                await SendSetupWithDynamicTools(modelId, systemInstruction, voiceName, dynamicToolsDeclaration);
            }
            catch (Exception e) { Debug.LogError($"Connection Error: {e.Message}"); }
        }

        // 3. Add the setup method that merges hardcoded and dynamic tools
        private async Task SendSetupWithDynamicTools(string model, string systemPrompt, string voice, JArray dynamicFunctionDeclarations)
        {
            var generationConfig = new JObject();
            generationConfig["response_modalities"] = new JArray("AUDIO");
            
            if(selectedModel == GeminiModelType.Flash25VertexAI)
            {
                var ThinkingConfig = new JObject
                {
                    ["thinking_budget"]  = 0,
                    ["include_thoughts"] = false,  
                };
                generationConfig["thinking_config"] = ThinkingConfig;
            }
            
            var speechConfig = new JObject();
            var voiceConfig = new JObject();
            voiceConfig["prebuilt_voice_config"] = new JObject { ["voice_name"] = voice };
            speechConfig["voice_config"] = voiceConfig;
            generationConfig["speech_config"] = speechConfig;

            var setupContent = new JObject
            {
                ["model"] = IsVertexModel() ? model : $"models/{model}",
                ["generation_config"] = generationConfig, 
                ["system_instruction"] = new JObject { ["parts"] = new JArray(new JObject { ["text"] = systemPrompt }) }
            };

            var toolsArray = new JArray();
            var toolWrapper = new JObject();
            var functionDeclarations = new JArray();

            functionDeclarations.Add(new JObject { 
                ["name"] = "update_avatar_state", 
                ["description"] = "Change the avatar's physical behavior.",
                ["parameters"] = JObject.Parse("{\"type\":\"object\",\"properties\":{\"action\":{\"type\":\"string\"},\"emotion\":{\"type\":\"string\"},\"gaze\":{\"type\":\"string\"}},\"required\":[\"action\",\"emotion\",\"gaze\"]}")
            });

            foreach(var dt in dynamicFunctionDeclarations) {
                functionDeclarations.Add(dt);
            }

            toolWrapper["function_declarations"] = functionDeclarations;
            toolsArray.Add(toolWrapper);
            setupContent["tools"] = toolsArray;

            var setupData = new JObject { ["setup"] = setupContent };

            if (verboseLogging) Debug.Log($"Sending Setup: {setupData.ToString(Newtonsoft.Json.Formatting.None)}");
            
            await SendJsonAsync(setupData);
        }
        public async Task SendGenericToolResponseAsync(string id, string name, object responsePayload)
        {
            var msg = new { tool_response = new { function_responses = new[] { new { id = id, name = name, response = responsePayload } } } };
            await SendJsonAsync(msg);
        }

    }
}