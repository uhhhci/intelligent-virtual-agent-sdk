using System;
using System.IO;
using UnityEngine;
using System.Text;
using Newtonsoft.Json;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using IVH.Core.Utils;

namespace IVH.Core.ServiceConnector
{


    public class GoogleCloudAIWrapper : MonoBehaviour
    {
        [Header("General Setup")]
        private string geminiAPIKey = ""; // Keep your OpenAI API key secure.
        [SerializeField] private GeminiModels model = GeminiModels.Gemini_2_Exp;
        private int maxTokens = 1000000; // dont restrict this too much otherwise the conversation will get truncated
        [SerializeField, Range(0f, 1f)] private float samplingTemperature = 0.5f;

        [Header("Image Understanding Setup")]
        [SerializeField] private ImageDetail imageDetail = ImageDetail.Auto;
        [SerializeField] private string basePrompt = "What are you seeing? Give only 1 short sentence as a description.";

        protected static HttpClient httpClient;

        private void InitializeHttpClient()
        {
            if (httpClient == null)
            {
                httpClient = new HttpClient();
            }
        }

        private void Awake()
        {
            InitializeHttpClient();
            geminiAPIKey = GeneralModelHelper.GetGeminiApiKey();
        }


        public async Task<string> SendTextMessage(
            List<GeminiMessage> messages,
            string userMessage,
            Action onRequest = null,
            List<GeminiTool> tools = null)
        {
            Debug.Log("Sending text-based request to Gemini API.");
            onRequest?.Invoke();

            // Add user's message
            messages.Add(new GeminiMessage(GeminiMessageRole.USER.ToString().ToLower(), userMessage));

            if (tools == null)
            {
                // Create payload and send the request
                var payload = new GeminiRequestPayload
                {
                    Contents = messages
                };
                return await SendRequest(payload, messages);

            }
            else
            {
                // Create payload and send the request
                var payload = new GeminiRequestPayload
                {
                    Contents = messages,

                    tools = tools
                };
                return await SendRequest(payload, messages);
            }
        }

        public async Task<string> MakeImageRequest(
            List<GeminiMessage> messages,
            string base64Image,
            Action onRequest = null,
            string prompt = "Caption this image.",
            List<GeminiTool> tools = null)
        {
            Debug.Log("Sending image-based request to Gemini API.");
            onRequest?.Invoke();

            // Construct the message with the prompt and the base64 image
            var imageMessage = new GeminiMessage
            {
                Role = GeminiMessageRole.USER.ToString().ToLower(),
                Parts = new List<GeminiMessagePart>
            {
                new GeminiMessagePart { Text = prompt }, // Text prompt
                new GeminiMessagePart
                {
                    InlineData = new InlineData
                    {
                        MimeType = "image/jpeg",
                        Data = base64Image
                    }
                }
            }
            };

            messages.Add(imageMessage);

            if (tools == null)
            {
                // Create payload and send the request
                var payload = new GeminiRequestPayload
                {
                    Contents = messages
                };
                return await SendRequest(payload, messages);

            }
            else
            {
                // Create payload and send the request
                var payload = new GeminiRequestPayload
                {
                    Contents = messages,

                    tools = tools
                };
                return await SendRequest(payload, messages);
            }
        }

        public async Task<string> MakeBoundingBoxesRequest(
            List<GeminiMessage> messages,
            string base64Image,
            Action onRequest = null,
            string prompt = @"Detect objects in the image. Return a JSON array. 
            Each item must have strictly these two fields: 
            1. 'label' (string) 
            2. 'box_2d' (array of 4 integers: [ymin, xmin, ymax, xmax]). 
            Do not use markdown. just raw JSON.",
            List<GeminiTool> tools = null)
        {
            Debug.Log("Sending image-based bounding box request to Gemini API.");
            onRequest?.Invoke();

        // Construct the message with the prompt and the base64 image
        var imageMessage = new GeminiMessage
        {
            Role = GeminiMessageRole.USER.ToString().ToLower(),
            Parts = new List<GeminiMessagePart>
            {
                new GeminiMessagePart { Text = prompt }, // Text prompt
                new GeminiMessagePart
                {
                    InlineData = new InlineData
                    {
                        MimeType = "image/jpeg",
                        Data = base64Image
                    }
                }
            }
        };

        messages.Add(imageMessage);

            if (tools == null)
            {
                // Create payload and send the request
                var payload = new GeminiRequestPayload
                {
                    Contents = messages
                };
                return await SendRequest(payload, messages);

    }
            else
            {
                // Create payload and send the request
                var payload = new GeminiRequestPayload
                {
                    Contents = messages,

                    tools = tools
                };
                return await SendRequest(payload, messages);
}
        }

        private async Task<string> SendRequest(GeminiRequestPayload payload, List<GeminiMessage> messages)
{
    try
    {
        var requestUri = $"https://generativelanguage.googleapis.com/v1beta/models/{GeminiModelHelper.GetModelString(model)}:generateContent?key={geminiAPIKey}";
        httpClient.DefaultRequestHeaders.Authorization = null; // No `Authorization` header, key is in URL
        var requestBody = JsonConvert.SerializeObject(payload);
        var response = await httpClient.PostAsync(
            requestUri,
            new StringContent(requestBody, Encoding.UTF8, "application/json")
        );

        var jsonResponse = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            Debug.Log("Gemini Response: " + jsonResponse);

            // Extract response text content
            var textResponse = ExtractResponseTextContent(jsonResponse);
            if (!string.IsNullOrEmpty(textResponse))
            {
                messages.Add(new GeminiMessage(GeminiMessageRole.MODEL.ToString().ToLower(), textResponse));
                return textResponse;
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
        return jsonObj["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString() ?? "No content available.";
    }
    catch (Exception ex)
    {
        Debug.LogError($"Error extracting response content: {ex.Message}");
    }

    return "Error parsing response.";
}

public List<GeminiBoundingBoxResponse> ParseBoundingBoxesFromJson(string json)
{
    try
    {
        // 1. Remove Markdown code block syntax if present
        string cleanedJson = json.Replace("```json", "").Replace("```", "").Trim();

        Debug.Log("Cleaned JSON for bounding boxes: " + cleanedJson);

        // 2. Deserialize
        return JsonConvert.DeserializeObject<List<GeminiBoundingBoxResponse>>(cleanedJson);
    }
    catch (Exception ex)
    {
        Debug.LogError($"Error parsing bounding boxes. Raw content was: {json}. Error: {ex.Message}");
        return new List<GeminiBoundingBoxResponse>();
    }
}

public async Task<List<GeminiBoundingBoxResponse>> GetBoundingBoxes(
    List<GeminiMessage> messages,
    string base64Image,
    Action onRequest = null,
    string prompt = null,
    List<GeminiTool> tools = null)
{
    // Use the bounding box request method
    string responseText = await MakeBoundingBoxesRequest(messages, base64Image, onRequest, prompt, tools);

    // Try to parse bounding boxes from the response text
    var boundingBoxes = ParseBoundingBoxesFromJson(responseText);

    // Log results for debugging
    foreach (var box in boundingBoxes)
    {
        Debug.Log($"Label: {box.Label}, Box2D: [{string.Join(", ", box.Box2D)}]");
    }

    return boundingBoxes;
}

        // Sample Response from Gemini API: 
        /*       {
         "candidates": [
           {
             "content": {
               "parts": [
                 {
                   "text": "The capital of France is Paris."
                 }
               ]
             },
             "finishReason": "STOP",
             "safetyRatings": [
                {
                   "category": "HARM_CATEGORY_DEROGATORY",
                   "probability": "NEGLIGIBLE"
               },
               {
           "category": "HARM_CATEGORY_TOXICITY",
                   "probability": "NEGLIGIBLE"
               },
                {
           "category": "HARM_CATEGORY_VIOLENCE",
                   "probability": "NEGLIGIBLE"
               },
                {
           "category": "HARM_CATEGORY_SEXUAL",
                   "probability": "NEGLIGIBLE"
               },
               {
           "category": "HARM_CATEGORY_MEDICAL",
                   "probability": "NEGLIGIBLE"
               }
           ]
           }
         ],
         "usageMetadata": {
           "promptTokenCount": 7,
           "candidatesTokenCount": 7,
           "totalTokenCount": 14
           }
       }*/

    }
}