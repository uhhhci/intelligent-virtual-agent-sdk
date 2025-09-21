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

namespace IVH.Core.Utils
{
    public struct Auth
    {
        [JsonRequired]
        public string OpenaiApiKey { get; set; }
        public string GeminiApiKey { get; set; }
        public string AzureSubscriptionKey { get; set; }
        public string AzureEndpointId { get; set; }
        public string ElevenLabsApiKey { get; set; }
    }
    // Generic helper for both OpenAI and GoogleCloud Gemini API
    public class GeneralModelHelper
    {
        private readonly static JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CustomNamingStrategy()
            }
        };


        public static string GetOpenAIApiKey() => GetApiKey("OpenaiApiKey");
        public static string GetGeminiApiKey() => GetApiKey("GeminiApiKey");
        public static string GetAzureSubscriptionKey() => GetApiKey("AzureSubscriptionKey");
        public static string GetAzureEndpointId() => GetApiKey("AzureEndpointId");
        public static string GetElevenLabAPIKey() => GetApiKey("ElevenLabsApiKey");

        private static string GetApiKey(string keyName)
        {
            var userPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var authPath = Path.Combine(userPath, ".aiapi/auth.json");

            if (!File.Exists(authPath))
            {
                Debug.LogWarning($"auth.json not found. Follow the setup guide: https://github.com/srcnalt/OpenAI-Unity#saving-your-credentials");
                return null;
            }

            try
            {
                var json = File.ReadAllText(authPath);
                var auth = JsonConvert.DeserializeObject<Auth>(json, jsonSerializerSettings);

                var apiKey = typeof(Auth).GetProperty(keyName)?.GetValue(auth)?.ToString();

                if (string.IsNullOrEmpty(apiKey))
                {
                    Debug.LogWarning($"{keyName} is null. Check your auth.json file.");
                }

                return apiKey;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Error reading {keyName} from auth.json: {ex.Message}");
                return null;
            }
        }

        public static int EstimateTokens(string content)
        {
            return Mathf.CeilToInt(content.Length / 4f); // Approx. 4 characters per token
        }
    }


    public class CustomNamingStrategy : NamingStrategy
    {
        protected override string ResolvePropertyName(string name)
        {
            return Regex.Replace(name, "([A-Z])", match => (match.Index > 0 ? "_" : "") + match.Value.ToLowerInvariant());
        }
    }
}