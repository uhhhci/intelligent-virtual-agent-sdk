using System;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace IVH.Core.Utils
{
    public class Auth
    {
        public string OpenaiApiKey { get; set; }
        public string GeminiApiKey { get; set; }
        public string AzureSubscriptionKey { get; set; }
        public string AzureEndpointId { get; set; }
        public string ElevenLabsApiKey { get; set; }
    }

    public class GeneralModelHelper
    {
        private static Auth _cachedAuth = null;
        private static bool _hasAttemptedLoad = false;

        private readonly static JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new SnakeCaseNamingStrategy() 
            }
        };

        public static string GetOpenAIApiKey() => GetApiKey("OpenaiApiKey");
        public static string GetGeminiApiKey() => GetApiKey("GeminiApiKey");
        public static string GetAzureSubscriptionKey() => GetApiKey("AzureSubscriptionKey");
        public static string GetAzureEndpointId() => GetApiKey("AzureEndpointId");

        private static void LoadAuthFile()
        {
            Debug.Log("Auth path: " + Path.Combine(Application.persistentDataPath, ".aiapi", "auth.json"));

            _hasAttemptedLoad = true;
            
#if UNITY_EDITOR || UNITY_STANDALONE
            var userPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
#else
            var userPath = Application.persistentDataPath;
#endif
            
            string authPath = Path.Combine(userPath, ".aiapi/auth.json");

            if (!File.Exists(authPath))
            {
                Debug.LogWarning($"auth.json not found. Follow the setup guide: https://github.com/srcnalt/OpenAI-Unity#saving-your-credentials");
                return;
            }

            try
            {
                var json = File.ReadAllText(authPath);
                _cachedAuth = JsonConvert.DeserializeObject<Auth>(json, jsonSerializerSettings);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Fatal error parsing auth.json: {ex.Message}");
            }
        }

        public static string GetElevenLabAPIKey() => GetApiKey("ElevenLabsApiKey");

        private static string GetApiKey(string keyName)
        {
            if (!_hasAttemptedLoad)
            {
                LoadAuthFile();
            }

            if (_cachedAuth == null)
            {
                return null;
            }

            try
            {
                var apiKey = typeof(Auth).GetProperty(keyName)?.GetValue(_cachedAuth)?.ToString();

                if (string.IsNullOrEmpty(apiKey))
                {
                    Debug.LogWarning($"{keyName} is missing or empty in your auth.json file.");
                }

                return apiKey;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Error retrieving {keyName} from cached auth data: {ex.Message}");
                return null;
            }
        }

        public static int EstimateTokens(string content)
        {
            if (string.IsNullOrEmpty(content)) return 0;
            return Mathf.CeilToInt(content.Length / 4f); // Approx. 4 characters per token
        }
    }
}