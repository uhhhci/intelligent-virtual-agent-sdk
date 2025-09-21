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
using IVH.Core.Utils;

namespace IVH.Core.ServiceConnector
{
    public class OpenAIWrapper : MonoBehaviour
    {
        [Header("General Setup")]
        private string openAIKey = ""; // Keep your OpenAI API key secure.
        [SerializeField] private GPTModels model = GPTModels.GPT_4o;
        [SerializeField] private int maxTokens = 1000000;
        [SerializeField, Range(0f, 1f)] private float samplingTemperature = 0.5f;

        [Header("Image Understanding Setup")]
        [SerializeField] private ImageDetail imageDetail = ImageDetail.Auto;
        [SerializeField] private string basePrompt = "What are you seeing";


        private static HttpClient httpClient;

        private void Awake()
        {
            InitializeHttpClient();
            openAIKey = GeneralModelHelper.GetOpenAIApiKey();
        }

        private void InitializeHttpClient()
        {
            if (httpClient == null)
            {
                httpClient = new HttpClient();
            }
        }

        public List<GPTMessage> TruncateHistory(List<GPTMessage> conversation)
        {
            int tokenCount = 0;

            // Iterate through the conversation from the start
            for (int i = 0; i < conversation.Count; i++)
            {
                var message = conversation[i];
                int messageTokens = 0;

                if (message is GPTTextMessage textMessage)
                {
                    messageTokens = GeneralModelHelper.EstimateTokens(textMessage.ToString());
                }
                else if (message is GPTImageMessage imageMessage)
                {
                    messageTokens = GeneralModelHelper.EstimateTokens(imageMessage.ToString());
                }

                tokenCount += messageTokens;

                // If token count exceeds maxTokens, remove the earliest message
                if (tokenCount > maxTokens)
                {
                    conversation.RemoveAt(0);
                    tokenCount -= messageTokens; // Adjust token count accordingly
                    i--; // Adjust the index to account for removed element
                }
            }

            return conversation;
        }

        public async Task<string> SendTextMessage(
            List<GPTMessage> messages,
            string userMessage,
            Action onRequest = null,
            List<GPTToolItem> tools = null,
            FunctionCallingMode mode = FunctionCallingMode.AUTO)
        {
            Debug.Log("Sending text-based request to OpenAI.");
            onRequest?.Invoke();

            messages.Add(new GPTTextMessage(GPTMessageRoles.USER, userMessage));
            return await SendGPTRequest(messages, tools, mode);
        }

        public async Task<string> MakeImageRequest(
            List<GPTMessage> messages,
            string base64Image,
            Action onRequest = null,
            string prompt = "",
            List<GPTToolItem> tools = null,
            FunctionCallingMode mode = FunctionCallingMode.AUTO)
        {
            Debug.Log("Sending image-based request to OpenAI.");
            onRequest?.Invoke();

            var imagePrompt = string.IsNullOrEmpty(prompt) ? basePrompt : prompt;
            messages.Add(GPTRequestPayload.GPTImagePayloadConstructor(imagePrompt, base64Image, imageDetail));
            return await SendGPTRequest(messages, tools, mode);
        }

        private async Task<string> SendGPTRequest(
            List<GPTMessage> messages,
            List<GPTToolItem> tools,
            FunctionCallingMode mode)
        {
            var modelName = GPTModelHelper.GetModelName(model);
            var payload = tools == null
                ? new GPTRequestPayload(modelName, messages, maxTokens, samplingTemperature)
                : new GPTFunctionMessagePayload(
                    modelName,
                    messages,
                    maxTokens,
                    samplingTemperature,
                    tools.ToArray(),
                    mode.ToString().ToLower()
                );
            return await SendRequest(payload, messages);
        }

        private async Task<string> SendRequest(GPTRequestPayload payload, List<GPTMessage> messages)
        {
            try
            {
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", openAIKey);

                var response = await httpClient.PostAsync(
                    "https://api.openai.com/v1/chat/completions",
                    new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json")
                );

                var jsonResponse = await response.Content.ReadAsStringAsync();
                
                if (response.IsSuccessStatusCode)
                {
                    Debug.Log("GPT Response: " + jsonResponse);

                    // First, try to extract the message content
                    var textResponse = ExtractResponseTextContent(jsonResponse);
                    if (!string.IsNullOrEmpty(textResponse))
                    {
                        messages.Add(new GPTTextMessage(GPTMessageRoles.ASSISTANT, textResponse));
                        return textResponse;
                    }

                    // If no message content is found, check for function call details
                    var functionResponse = ExtractResponseFunctionContent(jsonResponse);
                    if (!string.IsNullOrEmpty(functionResponse))
                    {
                        messages.Add(new GPTTextMessage(GPTMessageRoles.ASSISTANT, functionResponse));
                        return functionResponse;
                    }

                    return "No valid content found.";
                }

                Debug.LogError($"Request failed: {response.StatusCode}. Response: {jsonResponse}");
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
                return jsonObj["choices"]?[0]?["message"]?["content"]?.ToString() ?? "No content available.";
            }
/*            catch (JsonException ex)
            {
                //Debug.LogError($"JSON Parsing Error: {ex.Message}");
            }*/
            catch (Exception ex)
            {
                Debug.LogError($"Error extracting response content: {ex.Message}");
            }

            return "Error parsing response.";
        }

        private string ExtractResponseFunctionContent(string jsonResponse)
        {
            try
            {
                var jsonObj = JObject.Parse(jsonResponse);
                var toolCalls = jsonObj["choices"]?[0]?["message"]?["tool_calls"];

                if (toolCalls != null && toolCalls.HasValues)
                {
                    // Extract the function name and arguments
                    var toolCall     = toolCalls[0];
                    var functionName = toolCall["function"]?["name"]?.ToString();
                    var arguments    = toolCall["function"]?["arguments"]?.ToString();

                    if (!string.IsNullOrEmpty(functionName))
                    {
                        return $"Function call: {functionName}, Arguments: {arguments ?? "None"}";
                    }
                }
            }
  /*          catch (JsonException ex)
            {
                //Debug.LogError($"JSON Parsing Error in function content: {ex.Message}");
            }*/
            catch (Exception ex)
            {
                Debug.LogError($"Error extracting function content: {ex.Message}");
            }

            return null;
        }

    }
}