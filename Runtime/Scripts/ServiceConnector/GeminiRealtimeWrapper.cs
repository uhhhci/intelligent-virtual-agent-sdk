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
    public class GeminiRealtimeWrapper : MonoBehaviour
    {
        [Header("Connection Settings")]
        private string apiKey;
        public string modelName = "gemini-2.0-flash-exp";

        // Events
        public Action OnSetupComplete; 
        public Action<byte[]> OnAudioReceived;
        public Action<string> OnTextReceived;
        public Action<string, string, string> OnCommandReceived;
        
        public bool verboseLogging = false;
        public bool IsConnected => _webSocket != null && _webSocket.State == WebSocketState.Open;

        private ClientWebSocket _webSocket;
        private CancellationTokenSource _cancellationTokenSource;
        
        // Thread Safety
        private readonly Queue<Action> _mainThreadQueue = new Queue<Action>();
        private readonly object _queueLock = new object();
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1); // CRITICAL FIX

        private const string BASE_URL = "wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1alpha.GenerativeService.BidiGenerateContent";
        private void Awake()
        {
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

            string uri = $"{BASE_URL}?key={apiKey}";
            _webSocket = new ClientWebSocket();
            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                Debug.Log($"Connecting to {modelName}...");
                await _webSocket.ConnectAsync(new Uri(uri), CancellationToken.None);
                
                _ = ReceiveLoop();
                await SendSetupWithGenericTool(modelName, systemInstruction, voiceName);
            }
            catch (Exception e) { Debug.LogError($"Connection Error: {e.Message}"); }
        }

        public async Task DisconnectAsync()
        {
            if (_webSocket != null)
            {
                _cancellationTokenSource?.Cancel();
                // Release any pending locks
                if (_sendLock.CurrentCount == 0) _sendLock.Release();
                
                try { await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None); }
                catch { }
                
                _webSocket.Dispose();
                _webSocket = null;
            }
        }

        private async Task SendSetupWithGenericTool(string model, string systemPrompt, string voice)
        {
            var setupMsg = new
            {
                setup = new
                {
                    model = $"models/{model}",
                    generation_config = new
                    {
                        response_modalities = new List<string> { "AUDIO" },
                        speech_config = new { voice_config = new { prebuilt_voice_config = new { voice_name = voice } } }
                    },
                    system_instruction = new { parts = new[] { new { text = systemPrompt } } },
                    tools = new[] 
                    { 
                        new 
                        { 
                            function_declarations = new[] 
                            {
                                new 
                                {
                                    name = "update_avatar_state",
                                    description = "Call this function to change the avatar's physical behavior.",
                                    parameters = new 
                                    {
                                        type = "object",
                                        properties = new 
                                        {
                                            action = new { type = "string", description = "Body animation name" },
                                            emotion = new { type = "string", description = "Facial expression name" },
                                            gaze = new { type = "string", description = "Target: 'User' or 'Idle'" }
                                        },
                                        required = new[] { "action", "emotion", "gaze" }
                                    }
                                }
                            }
                        } 
                    },
                    tool_config = new { function_calling_config = new { mode = "AUTO" } }
                }
            };

            await SendJsonAsync(setupMsg);
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
            // High frequency call - strictly thread safe now
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
            // Crucial: This response MUST reach the server or the agent hangs forever
            var msg = new { tool_response = new { function_responses = new[] { new { id = id, name = "update_avatar_state", response = new { status = "ok" } } } } };
            await SendJsonAsync(msg);
        }

        // --- THREAD SAFE SENDER ---
        private async Task SendJsonAsync(object data)
        {
            if (!IsConnected) return;
            
            // Wait for the lock
            await _sendLock.WaitAsync();

            try 
            {
                if (!IsConnected) return; // Re-check after wait
                string json = JsonConvert.SerializeObject(data, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                await _webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(json)), WebSocketMessageType.Text, true, CancellationToken.None); 
            }
            catch(Exception ex) 
            { 
                Debug.LogError($"Send Error: {ex.Message}"); 
            }
            finally
            {
                _sendLock.Release();
            }
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
                        Debug.LogWarning($"Server Closed Connection. Status: {result.CloseStatus} / {result.CloseStatusDescription}");
                        break;
                    }

                    ProcessMessage(Encoding.UTF8.GetString(ms.ToArray()));
                }
            }
            catch (Exception ex) 
            { 
                // Only log if it's not a deliberate cancellation
                if(!_cancellationTokenSource.IsCancellationRequested)
                    Debug.Log($"Receive Loop Stopped: {ex.Message}"); 
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

                // 1. Tool Calls
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

                // 2. Content
                JToken serverContent = root["serverContent"] ?? root["server_content"];
                if (serverContent != null)
                {
                    // Handle Interruption (Clear buffer if the server says the user interrupted)
                    if (root["serverContent"]?["interrupted"]?.Value<bool>() == true)
                    {
                         // Optional: You could trigger an event here to clear the Agent's audio buffer
                    }

                    JToken parts = serverContent["modelTurn"]?["parts"] ?? serverContent["model_turn"]?["parts"];
                    if (parts != null)
                    {
                        foreach (var part in parts)
                        {
                            if (part["text"] != null) EnqueueMainThread(() => OnTextReceived?.Invoke(part["text"].ToString()));
                            
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