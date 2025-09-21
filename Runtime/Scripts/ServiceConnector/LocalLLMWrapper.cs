// using System.IO;
// using UnityEngine;
// using System.Text;
// using Newtonsoft.Json;
// using System.Net.Http;
// using Newtonsoft.Json.Linq;
// using System.Threading.Tasks;
// using System.Net.Http.Headers;
// using Newtonsoft.Json.Serialization;
// using System.Text.RegularExpressions;
// using System.Collections.Generic;
// using IVH.Core.Utils;
// using Gpt4All;

// public class LocalLLMWrapper : MonoBehaviour
// {
//     public LlmManager manager;
//     private StringBuilder _conversationHistory;

//     private void Awake()
//     {
//         manager.OnResponseUpdated += OnResponseHandler;
//         _conversationHistory = new StringBuilder();
//     }

//     public async Task<string> SendPrompt(string prompt)
//     {
//         if (string.IsNullOrEmpty(prompt))
//             return null;

//         if (!manager.IsLoaded)
//         {
//             Debug.LogError("Failed to load LLM Make sure you placed the LLM file, that you specified, in the StreamingAssets/Gpt4All folder.");
//             return null;
//         }
//         // Append the user's message to the conversation history
//         _conversationHistory.AppendLine($"User: {prompt}");

//         // Include the conversation history in the prompt
//         string fullPrompt = _conversationHistory.ToString();

//         // Get the response from the model
//         string response = await manager.Prompt(fullPrompt);

//         // Append the model's response to the conversation history
//         _conversationHistory.AppendLine($"Answer: {response}");

//         return response;
//     }

//     private void OnResponseHandler(string response)
//     {
//         Debug.Log(_conversationHistory.ToString() + response);
//     }
// }