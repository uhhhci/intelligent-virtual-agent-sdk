using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;

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

    public static class GeneralModelHelper
    {
        private static Auth _cachedAuth;
        private static bool _hasAttemptedAuthenticationLoad;

        private static readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings
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

        /// <summary>
        /// Loads the AI API autentication for different models. Uses <see cref="LoadAuthenticationFromFile"/> first and only if
        /// no authentication is found there uses <see cref="LoadAuthenticationFromEnvironmentVariables"/> to gather authentication
        /// afterwards.
        /// </summary>
        private static void LoadAuthentication()
        {
            _hasAttemptedAuthenticationLoad = true;

            // Primary method of authentication: auth.json File
            var auth = LoadAuthenticationFromFile();

            if (auth == null)
            {
                auth = LoadAuthenticationFromEnvironmentVariables();
            }

            _cachedAuth = auth;
        }

        /// <summary>
        /// Loads the authentication keys from environment variables corresponding to their names.
        /// Recognizes the following environment variables:
        /// <list type="bullet">
        ///     <item><term><c>OPENAI_API_KEY</c></term></item>
        ///     <item><term><c>GEMINI_API_KEY</c></term></item>
        ///     <item><term><c>AZURE_SUBSCRIPTION_KEY</c></term></item>
        ///     <item><term><c>AZURE_ENDPOINT_ID</c></term></item>
        /// </list>
        ///
        /// If you want to setup your environment variables correctly, see: https://docs.unity3d.com/2023.1/Documentation/Manual/ent-proxy-cmd-file.html
        ///
        /// <returns>A valid auth object if <i>any</i> key has been found, <c>null</c> otherwise.</returns>
        /// </summary>
        private static Auth LoadAuthenticationFromEnvironmentVariables()
        {
            var auth = new Auth
            {
                OpenaiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
                GeminiApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY"),
                AzureSubscriptionKey = Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_KEY"),
                AzureEndpointId = Environment.GetEnvironmentVariable("AZURE_ENDPOINT_ID")
            };
            
            var allEmpty =
                string.IsNullOrEmpty(auth.OpenaiApiKey) &&
                string.IsNullOrEmpty(auth.GeminiApiKey) &&
                string.IsNullOrEmpty(auth.AzureSubscriptionKey) &&
                string.IsNullOrEmpty(auth.AzureEndpointId);

            return allEmpty ? null : auth;
        }

        /// <summary>
        /// Load the authentication keys from a file <c>auth.json</c>.
        /// The file must be located inside <c>.aiapi/</c> for Unity editor runtime <b>OR</b>
        /// in the persistent data path for an embedded device.
        ///
        /// It ignores keys not present in the file and leaving them <c>null</c> in the resulting <c>Auth</c>.
        /// </summary>
        private static Auth LoadAuthenticationFromFile()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            var userPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
#else
            // if we run the application outside of the Unity editor, then we want to load the persistent 
            // data path (for some embedded device)
            var userPath = Application.persistentDataPath;
#endif

            string authPath = Path.Combine(userPath, ".aiapi/auth.json");

            if (!File.Exists(authPath))
            {
                Debug.Log($"auth.json not found. Follow the setup guide: https://github.com/srcnalt/OpenAI-Unity#saving-your-credentials");
                return null;
            }

            try
            {
                var json = File.ReadAllText(authPath);
                return JsonConvert.DeserializeObject<Auth>(json, JsonSerializerSettings);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Fatal error parsing auth.json: {ex.Message}");
            }
            return null;
        }

        public static string GetElevenLabAPIKey() => GetApiKey("ElevenLabsApiKey");

        private static string GetApiKey(string keyName)
        {
            if (!_hasAttemptedAuthenticationLoad)
            {
                LoadAuthentication();
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
                    Debug.LogWarning($"{keyName} is missing or empty in your authentication.");
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