using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;

namespace IVH.Core.ServiceConnector
{
    public class WebsocketConnection : MonoBehaviour
    {
        ClientWebSocket clientWebSocket;
        
        private CancellationTokenSource source;
        public string state = "";
        private CancellationToken token;
        public int websocketPort = 8140;
        private string _websocketAddress;
        private bool AudioModeSet = false;

        private TranscriptionResult lastTranscription = null;

        // Add listener for chat results
        public List<Action<ChatResult>> chatListeners = new List<Action<ChatResult>>();

        // Add listeners for Transcription Results
        public List<Action<TranscriptionResult>> transcriptionListeners = new List<Action<TranscriptionResult>>();

        public bool ReadyToReceiveAudio
        {
            get { return clientWebSocket != null && clientWebSocket.State == WebSocketState.Open && AudioModeSet; }
        }

        void Start()
        {
            _websocketAddress = "ws://" + ServiceConnectorManager.Instance.serverIp + ":" + websocketPort;
            ConnectToWebsocket(() => { Debug.Log("Connected AudioAPI"); });
        }

        public void Connect(Action Connected_CB)
        {
            ConnectToWebsocket(Connected_CB);
        }

        private void ConnectToWebsocket(Action Connected_CB)
        {
            if (clientWebSocket != null && clientWebSocket.State == WebSocketState.Open)
            {
                clientWebSocket.Dispose();
            }

            AudioModeSet = false;
            clientWebSocket = new ClientWebSocket();
            source = new CancellationTokenSource();
            token = source.Token;

            Task connect = new Task(() =>
            {
                var connectingTask =
                    clientWebSocket.ConnectAsync(new System.Uri(_websocketAddress), token);
                Debug.Log("Connecting to SST server");
                connectingTask.Wait();
                Debug.Log("Connected to SST server");
                Task receiving = new Task(async () =>
                {
                    while (!token.IsCancellationRequested)
                    {
                        //Debug.Log($"Token: {token.IsCancellationRequested}");
                        var data = new ArraySegment<byte>(new byte[1024 * 4]);
                        var result = await clientWebSocket.ReceiveAsync(data, token);
                        if (result.Count > 0)
                        {
                            if (result.MessageType == WebSocketMessageType.Text && result.EndOfMessage)
                            {
                                var text = System.Text.Encoding.UTF8.GetString(data);
                                // Assume Transcription result for now
                                if (text.StartsWith("{"))
                                {
                                    // First check type of message
                                    var deserialized =
                                        JsonConvert.DeserializeObject<WebsocketUTF8Message<object>>(text);
                                    if (deserialized.message == "stt")
                                    {
                                        var temp = JsonConvert
                                            .DeserializeObject<WebsocketUTF8Message<TranscriptionResult>>(text,
                                                new JsonSerializerSettings
                                                {
                                                    MissingMemberHandling = MissingMemberHandling.Ignore,
                                                    FloatParseHandling = FloatParseHandling.Decimal,
                                                    Error = (obj, err) => Debug.LogError(err + " " + text)
                                                });
                                        var transcription = temp.contents;

                                        lastTranscription = transcription;

                                        foreach (var listener in transcriptionListeners)
                                        {
                                            listener.Invoke(lastTranscription);
                                        }
                                    }
                                    else if (deserialized.message == "chat")
                                    {
                                        var temp = JsonConvert
                                            .DeserializeObject<WebsocketUTF8Message<ChatResult>>(text);
                                        foreach (var listener in chatListeners)
                                        {
                                            listener.Invoke(temp.contents);
                                        }

                                        Debug.Log("Chat: " + temp.contents.delta + " " + temp.contents.done);
                                    }
                                    else
                                    {
                                        Debug.Log("Message: " + result.MessageType + " " + result.EndOfMessage + " " +
                                                  result.Count + " " + text);
                                    }

                                }
                                else
                                {
                                    Debug.Log("Message: " + result.MessageType + " " + result.EndOfMessage + " " +
                                              result.Count + " " + text);
                                }
                            }
                        }

                    }
                });
                receiving.Start();

                var task = clientWebSocket.SendAsync(GetUTF8BytesForString("{message: \"Hello\"}"),
                    WebSocketMessageType.Text, true, token);
                task.Wait();

                // Callback
                Connected_CB.Invoke();
            });
            connect.Start();

        }

        public bool sendAudioData(ArraySegment<byte> arr)
        {
            if (!ReadyToReceiveAudio)
            {
                return false;
            }

            const int buffer_size = 65536;
            int l = 0;
            while (l < arr.Count)
            {
                int length = Mathf.Min(buffer_size, arr.Count - l);
                clientWebSocket.SendAsync(arr.Slice(l, length), WebSocketMessageType.Binary, l + length >= arr.Count,
                    token);
                l += length;
            }

            return true;
        }


        public static byte[] GetL16(AudioClip clip)
        {
            MemoryStream stream = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(stream);

            float[] samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);

            float divisor = (1 << 15);
            for (int i = 0; i < samples.Length; ++i)
                writer.Write((short)(samples[i] * divisor));

            byte[] data = new byte[samples.Length * 2];
            Array.Copy(stream.GetBuffer(), data, data.Length);

            return data;
        }

        public Task EnableAudioWebsocket(AudioConfiguration config, Action<bool> SpeechRecognitionReady_Callback)
        {
            if (clientWebSocket == null || clientWebSocket.State != WebSocketState.Open)
            {
                // Connection not available
                Debug.LogError("Connection to SST not available");
                SpeechRecognitionReady_Callback.Invoke(false);
                return null;
            }

            lastTranscription = null;

            Task enableAudio = new Task(async () =>
            {
                await clientWebSocket.SendAsync(
                    new WebsocketUTF8Message<AudioConfiguration>("start-audio", config).GetPackage(),
                    WebSocketMessageType.Text, true, token);
                AudioModeSet = true;
                SpeechRecognitionReady_Callback.Invoke(true);
            });
            enableAudio.Start();
            return enableAudio;

        }

        public async Task SendChatGPTRequest(Dictionary<string, string> request)
        {
            if (clientWebSocket == null || 
                clientWebSocket.State != WebSocketState.Open || 
                clientWebSocket.State == WebSocketState.Closed)
            {
                Debug.LogError("WebSocket connection is not available or is closing/closed");
                return;
            }

            try
            {
                var message = new WebsocketUTF8Message<Dictionary<string, string>>("chat-gpt", request);
                await clientWebSocket.SendAsync(
                    message.GetPackage(),
                    WebSocketMessageType.Text,
                    true,
                    token);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error sending WebSocket message: {ex.Message}");
            }
        }

        public Task StopSTT_Task()
        {
            if (clientWebSocket == null || clientWebSocket.State != WebSocketState.Open)
            {
                // Connection not available
                Debug.LogError("Connection to SST not available");
                return null;
            }

            Task stopAudio = new Task(async () =>
            {
                await clientWebSocket.SendAsync(new WebsocketUTF8Message<string>("stop-audio", "").GetPackage(),
                    WebSocketMessageType.Text, true, token);
                AudioModeSet = false;
            });
            stopAudio.Start();
            Debug.Log("Sending Stop Audio to server");
            return stopAudio;
        }

        private void OnApplicationQuit()
        {
            clientWebSocket?.Dispose();
            source?.Cancel();
        }

        static ArraySegment<byte> GetUTF8BytesForString(string str)
        {
            return new ArraySegment<byte>(System.Text.Encoding.UTF8.GetBytes(str));
        }

        // Update is called once per frame
        void Update()
        {
            if (clientWebSocket != null)
                state = clientWebSocket.State.ToString();
        }

        private void OnGUI()
        {
            return; // TODO DISABLED VISUAL STT OUTPUT
            // if (lastTranscription != null && lastTranscription.alternatives.Count > 0)
            // {
            //     if (lastTranscription.isFinal)
            //         GUI.color = Color.green;
            //     else
            //         GUI.color = Color.red;
            //
            //     GUI.Label(new Rect(10, 10, 500, 100), lastTranscription.alternatives[0].transcript);
            // }
        }

        public class AudioConfiguration
        {
            public string encoding;
            public int sampleRateHertz;
            public string languageCode;

            public AudioConfiguration(string encoding, int sampleRateHertz, string languageCode)
            {
                this.encoding = encoding;
                this.sampleRateHertz = sampleRateHertz;
                this.languageCode = languageCode;
            }
        }

        private class WebsocketUTF8Message<T>
        {
            public string message;
            public T contents;

            public WebsocketUTF8Message(string message, T contents)
            {
                this.message = message;
                this.contents = contents;
            }

            public ArraySegment<byte> GetPackage()
            {
                return GetUTF8BytesForString(JsonConvert.SerializeObject(this));
            }
        }

        public class Alternative
        {
            public List<object> words { get; set; }
            public string transcript { get; set; }
            public float confidence { get; set; }
        }

        public class ResultEndTime
        {
            public string seconds { get; set; }
            public int nanos { get; set; }
        }

        public class TranscriptionResult
        {
            public List<Alternative> alternatives { get; set; }
            public bool isFinal { get; set; }

            public float stability { get; set; }
            public ResultEndTime resultEndTime { get; set; }
            public int channelTag { get; set; }
            public string languageCode { get; set; }
        }

        public class ChatResult
        {
            public string message_id { get; set; }
            public string delta { get; set; }
            public bool done { get; set; }
        }
    }
}
