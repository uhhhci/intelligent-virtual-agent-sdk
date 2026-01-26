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
        Flash20ExpGoogleAI,        // Expiring on March 31. 
        Flash25NativePreviewGoogleAI,      // High Latency (Needs Beta)
        //Flash25_Native_VertexAI  // requires vertex AI, google account.json setup
    }

    public class GeminiRealtimeWrapper : MonoBehaviour
    {
        [Header("Connection Settings")]
        private string apiKey;
        public GeminiModelType selectedModel = GeminiModelType.Flash25NativePreviewGoogleAI;

        [Tooltip("Set to true for analyzing user's sentiments from audio. ")]
        [HideInInspector]public bool affectiveAnalysis = false; 

        [Tooltip("Compress context to extend session length.")]
        public bool contextWindowSliding = true; 

        // Events
        public Action OnSetupComplete; 
        public Action<byte[]> OnAudioReceived;
        public Action<string> OnTextReceived;
        public Action<string, string, string> OnCommandReceived;
        
        [HideInInspector]public bool verboseLogging = false;
        public bool IsConnected => _webSocket != null && _webSocket.State == WebSocketState.Open;

        private ClientWebSocket _webSocket;
        private CancellationTokenSource _cancellationTokenSource;
        
        // Thread Safety
        private readonly Queue<Action> _mainThreadQueue = new Queue<Action>();
        private readonly object _queueLock = new object();
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        
        private const string V1ALPHA_URL = "wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1alpha.GenerativeService.BidiGenerateContent";
        private const string V1BETA_URL = "wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent";

        private string GetModelString() => selectedModel switch
        {
            // The "Experimental" model (Only one that works on Alpha)
            GeminiModelType.Flash20ExpGoogleAI => "gemini-2.0-flash-exp",
                        

           // GeminiModelType.Flash25_Native_VertexAI => "gemini-live-2.5-flash-native-audio", 
            // The "Smart but Slow" one

            GeminiModelType.Flash25NativePreviewGoogleAI => "gemini-2.5-flash-native-audio-preview-12-2025",

            _ => "gemini-2.5-flash-native-audio-preview-12-2025"
        };

        private string GetBaseUrl()
        {
            // ONLY the old experimental model uses Alpha. 
            if (selectedModel == GeminiModelType.Flash20ExpGoogleAI ) return V1ALPHA_URL;
            return V1BETA_URL;
        }

        private void Awake()
        {
            // Ensure you have your API key setting logic here
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
            if (string.IsNullOrEmpty(apiKey)) { Debug.LogError("API Key Missing!"); return; }
            await DisconnectAsync();

            string modelId = GetModelString();
            string uri = $"{GetBaseUrl()}?key={apiKey}";
            
            _webSocket = new ClientWebSocket();
            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                Debug.Log($"Connecting to {modelId} ...");
                await _webSocket.ConnectAsync(new Uri(uri), CancellationToken.None);
                
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

            // SPEECH CONFIG
            // Note: Ensure 'voice' is one of: "Puck", "Charon", "Kore", "Fenrir", "Aoede"
            var speechConfig = new JObject();
            var voiceConfig = new JObject();
            voiceConfig["prebuilt_voice_config"] = new JObject { ["voice_name"] = voice };
            speechConfig["voice_config"] = voiceConfig;
            generationConfig["speech_config"] = speechConfig;

            var setupContent = new JObject
            {
                ["model"] = $"models/{model}",
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
                    ["sliding_window"] = new JObject()
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
                
                // --- SETUP COMPLETE ---
                // Note: Some models might not send this explicitly or structure it differently, 
                // but usually the first server_content indicates readiness.
                if (root["setupComplete"] != null || root["setup_complete"] != null)
                {
                    EnqueueMainThread(() => OnSetupComplete?.Invoke());
                    return;
                }

                // --- TOOL CALLS ---
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
                        }
                    }
                }

                // --- SERVER CONTENT ---
                JToken serverContent = root["serverContent"] ?? root["server_content"];
                if (serverContent != null)
                {
                    // Interruption handling
                    if (serverContent["interrupted"]?.Value<bool>() == true)
                    {
                         // Handle interruption (clear queues)
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
    }
}