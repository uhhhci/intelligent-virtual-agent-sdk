using System;
using System.IO;
using UnityEngine;
using System.Text;
using Newtonsoft.Json;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using Newtonsoft.Json.Serialization;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace IVH.Core.ServiceConnector
{
    public class OllamaModelHelper
    {
        public static string GetModelName(FoundationModels model)
        {
            switch (model)
            {
                case FoundationModels.Ollama_Deepseek_R1_7B_LLM:
                    return "deepseek-r1";
                case FoundationModels.Ollama_Deepseek_R1_14B_LLM:
                    return "deepseek-r1:14b";
/*                case FoundationModels.Ollama_Deepseek_R1_32B_LLM:
                    return "deepseek-r1:32b";
                case FoundationModels.Ollama_Deepseek_R1_70B_LLM:*/
                    return "deepseek-r1:70b";
                case FoundationModels.Ollama_Llama_3_2_3B_LLM:
                    return "llama3.2";
/*                case FoundationModels.Ollama_Llama_3_3_70B_LLM:
                    return "llama3.3";*/
                case FoundationModels.Ollama_Tinyllama_1B_LLM:
                    return "tinyllama";
                case FoundationModels.Ollama_OpenChat_7B_LLM:
                    return "openchat";
                case FoundationModels.Ollama_llava_7B_VLM:
                    return "llava";
                default:
                    throw new ArgumentOutOfRangeException(nameof(model), model, "Unknown Ollama model.");
            }
        }
    }

    public enum URLType
    {
        Local_Ollama,
        UHAM_VLLM
    }
    public class OllamaWrapper : MonoBehaviour
    {
        /*        [Header("General Setup")]
                [SerializeField] private string model = "deepseek-r1"; // Model name for Ollama's DeepSeek
        */
        [SerializeField] private URLType urlType = URLType.Local_Ollama;
        string ollamaApiUrl = "http://192.168.1.100:11434/api/chat"; // Ollama API endpoint
        //string ollamaApiUrl = "http://localhost:11434/api/chat"; // Ollama API endpoint

        string vllmUHAMApiUrl = "http://134.100.14.194:7788/v1/chat/completions";
        
        private string apiKey = "your-api-key"; // Replace with your API key if required

        private static HttpClient httpClient;

        private void Awake()
        {
            InitializeHttpClient();
        }

        private void InitializeHttpClient()
        {
            if (httpClient == null)
            {
                httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Accept.Clear();
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                if (!string.IsNullOrEmpty(apiKey))
                {
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                }
            }
        }

        public async Task<string> SendTextMessage(
            List<GPTMessage> messages,
            string userMessage,
            FoundationModels modelType,
            Action onRequest = null,
            List<GPTToolItem> tools = null,
            FunctionCallingMode mode = FunctionCallingMode.AUTO)
        {
            Debug.Log("Sending text-based request to Ollama.");
            onRequest?.Invoke();

            messages.Add(new GPTTextMessage(GPTMessageRoles.USER, userMessage));
            return await SendOllamaRequest(messages, modelType);
        }

        private async Task<string> SendOllamaRequest(List<GPTMessage> messages, FoundationModels modelType)
        {
            string model = OllamaModelHelper.GetModelName(modelType);
            var payload = new OllamaRequestPayload
            {
                Model = model,
                Messages = messages,
                Stream = false // Set to true if you want streaming responses
            };

            return await SendRequest(payload);
        }

        private async Task<string> SendRequest(OllamaRequestPayload payload)
        {
            try
            {
                var jsonPayload = JsonConvert.SerializeObject(payload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                if (urlType == URLType.Local_Ollama)
                {
                    var response = await httpClient.PostAsync(ollamaApiUrl, content);
                    var jsonResponse = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        Debug.Log("Ollama Response: " + jsonResponse);
                        return ExtractResponseTextContent(jsonResponse);
                    }
                    Debug.LogError($"Request failed: {response.StatusCode}. Response: {jsonResponse}");

                }
                else if(urlType == URLType.UHAM_VLLM)
                {
                    var response = await httpClient.PostAsync(vllmUHAMApiUrl, content);
                    var jsonResponse = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        Debug.Log("Ollama Response: " + jsonResponse);
                        return ExtractResponseTextContent(jsonResponse);
                    }
                    Debug.LogError($"Request failed: {response.StatusCode}. Response: {jsonResponse}");

                }


            }
            catch (Exception ex)
            {
                Debug.LogError($"Error sending request: {ex.Message}");
            }

            return string.Empty;
        }

        private string ExtractResponseTextContent(string jsonResponse)
        {
            try
            {
                var jsonObj = JObject.Parse(jsonResponse);
                return jsonObj["message"]?["content"]?.ToString() ?? "No content available.";
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error extracting response content: {ex.Message}");
            }

            return "Error parsing response.";
        }

        // Define the payload structure for Ollama API
        private class OllamaRequestPayload
        {
            [JsonProperty("model")]
            public string Model { get; set; }

            [JsonProperty("messages")]
            public List<GPTMessage> Messages { get; set; }

            [JsonProperty("stream")]
            public bool Stream { get; set; }
        }

        public enum FunctionCallingMode
        {
            AUTO,
            NONE
        }

    }
}