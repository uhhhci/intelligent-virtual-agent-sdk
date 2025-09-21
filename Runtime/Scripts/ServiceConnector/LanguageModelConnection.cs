using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using IVH.Core.Utils.Patterns;
using UnityEngine;
using System.Threading.Tasks;
using System.Threading;
namespace IVH.Core.ServiceConnector
{


    public class LanguageModelConnection : MonoBehaviour
    {
        private WebsocketConnection websocketService;

        public void Start()
        {
            websocketService = ServiceConnectorManager.Instance.Websocket;
        }

        public async Task<GPTTextMessage> GetChat_NLPResponseStreamedAsync(
            GPTMessage[] messages,
            GPT_Models model,
            Action<GPTStreamMessage> stream_callback,
            int max_tokens = 10000,
            float temperature = 0.35f,
            bool stream = true,
            string study_id = "")
        {
            var dict = new Dictionary<string, string>
                    {
                        { "model", GPT_Models_String.GetModelString(model) },
                        { "input", "" },
                        { "chat", "true" },
                        { "messages", JsonConvert.SerializeObject(messages, Formatting.Indented).ToString() },
                        { "key", ServiceConnectorManager.Instance.localKey },
                        { "maxtokens", max_tokens.ToString() },
                        { "temperature", temperature.ToString() }
                    };
            //Debug.Log(JsonConvert.SerializeObject(messages, Formatting.Indented));
            //Debug.Log($"Payload size: {JsonConvert.SerializeObject(messages).Length} bytes");

            if (stream)
            {
                string message_id = GenerateUniqueID();
                dict.Add("streamOn", "true");
                dict.Add("sendResultsOn", ".:!?");
                dict.Add("message_id", message_id);
/*                string json = @"[
                            {
                                ""role"": ""user"",
                                ""content"": [
                                    { ""type"": ""text"", ""text"": ""What�s in this image?"" },
                                    {
                                        ""type"": ""image_url"",
                                        ""image_url"": {
                                            ""url"": ""data:image/png;base64,"",
                                            ""detail"": ""low""
 
                        }
                                    }
                                ]
                            }]";

                dict.Add("messages", json);*/

                string accumulated = "";
                bool finished = false;

                var listener = new Action<WebsocketConnection.ChatResult>(
                    chatResult =>
                    {
                        if (chatResult.message_id == message_id)
                        {
                            accumulated += chatResult.delta;
                            stream_callback?.Invoke(new GPTStreamMessage(chatResult.delta, chatResult.done));

                            if (chatResult.done)
                            {
                                finished = true;
                            }
                        }
                    });

                // Add listener for incoming WebSocket results
                websocketService.chatListeners.Add(listener);

                try
                {
                    // Send the request via WebSocket
                    await websocketService.SendChatGPTRequest(dict);
                    // Wait until the response is fully received
                    await Task.Run(() => SpinWait.SpinUntil(() => finished));

                    // Return the accumulated response
                    return new GPTTextMessage(GPTMessageRoles.ASSISTANT, accumulated);
                }
                finally
                {
                    // Ensure the listener is removed to avoid memory leaks
                    websocketService.chatListeners.Remove(listener);
                }
            }
            else
            {
                // Handle non-streaming case (if needed)
                throw new NotImplementedException("Non-streaming mode is not implemented yet.");
            }
        }

        public Coroutine GetChat_NLPResponseStreamed(GPTMessage[] input, GPT_Models model, Action<GPTTextMessage> callback,
            Action<GPTStreamMessage> stream_callback)
        {
            return StartCoroutine(GetChatGPTCompletion(input, model, callback, 400, 0.35f, true, stream_callback));
        }

        private IEnumerator GetChatGPTCompletion(GPTMessage[] messages, GPT_Models model, Action<GPTTextMessage> callback, int max_tokens,
            float temperature, bool stream = false, Action<GPTStreamMessage> stream_callback = null,
            string study_id = "")
        {
            var data = new WWWForm();
            var dict = new Dictionary<string, string>();
            dict.Add("model", GPT_Models_String.GetModelString(model));
            dict.Add("input", "");
            dict.Add("chat", "true");
            dict.Add("messages", Newtonsoft.Json.JsonConvert.SerializeObject(messages));

            dict.Add("key", ServiceConnectorManager.Instance.localKey);
            dict.Add("maxtokens", max_tokens.ToString());
            dict.Add("temperature", temperature.ToString());

            if (stream)
            {
                string message_id = GenerateUniqueID();
                dict.Add("streamOn", "true");
                dict.Add("sendResultsOn", ".:!?");
                dict.Add("message_id", message_id);

                string accumulated = "";

                bool finished = false;
                var listener = new Action<WebsocketConnection.ChatResult>(
                    (WebsocketConnection.ChatResult chatResult) =>
                    {
                        UnityMainThreadDispatcher.Instance.Enqueue(() =>
                        {
                            if (chatResult.message_id == message_id)
                            {
                                accumulated += chatResult.delta;
                                stream_callback.Invoke(new GPTStreamMessage(chatResult.delta, chatResult.done));
                                if (chatResult.done)
                                {
                                    finished = true;
                                }
                            }
                        });
                    });

                // Listener will be called every time a sendResultsOn character is found
                websocketService.chatListeners.Add(listener);

                var task = websocketService.SendChatGPTRequest(dict);

                yield return new WaitUntil(() => finished);
                callback.Invoke(new GPTTextMessage(GPTMessageRoles.ASSISTANT, accumulated));
                websocketService.chatListeners.Remove(listener);

                Debug.Log("Chat GPT Request Finished");
            }

            yield break;
        }

        public enum GPT_Models
        {
            Chat_GPT_35,
            Chat_GPT_4_NEW,
            GPT_4o
        }

        private class GPT_Models_String
        {

            public static string GetModelString(GPT_Models model)
            {
                switch (model)
                {
                    case GPT_Models.Chat_GPT_35: return "gpt-3.5-turbo";
                    case GPT_Models.Chat_GPT_4_NEW: return "gpt-4o";
                    case GPT_Models.GPT_4o: return "gpt-4o";
                }

                return "gpt-3.5-turbo";
            }
        }

        private string GenerateUniqueID()
        {
            return Guid.NewGuid().ToString();
        }

        // Classes for the API Connection

/*        public enum GPTMessageRoles
        {
            SYSTEM,
            ASSISTANT,
            USER
        }

        [Serializable]
        public class GPTMessage: ChatMessage
        {
            public string role;
            public string content;

            [JsonConstructor]
            public GPTMessage(string role, string content)
            {
                this.role = role;
                this.content = content;
            }

            public GPTMessage(GPTMessageRoles messageRole, params string[] content)
            {
                switch (messageRole)
                {
                    case GPTMessageRoles.SYSTEM:
                        role = "system";
                        break;
                    case GPTMessageRoles.ASSISTANT:
                        role = "assistant";
                        break;
                    case GPTMessageRoles.USER:
                        role = "user";
                        break;
                }

                this.content = string.Join("", content);
            }
        }*/

        public class GPTStreamMessage
        {
            public string delta { get; set; }
            public bool finished { get; set; }

            public GPTStreamMessage(string delta, bool finished)
            {
                this.delta = delta;
                this.finished = finished;
            }
        }

    }
}