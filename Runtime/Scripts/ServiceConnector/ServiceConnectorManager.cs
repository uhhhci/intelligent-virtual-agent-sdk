using IVH.Core.Utils.Patterns;
using IVH.Core.Utils.StaticHelper;

namespace IVH.Core.ServiceConnector
{
    public class ServiceConnectorManager : Singleton<ServiceConnectorManager>
    {
        // Service connector settings
        public string serverIp = "localhost";
        public string localKey = "YOUR_LOCAL_KEY";
        
        // Service connector services
        public SpeechToTextConnection SpeechToTextConnection { get; private set; }
        public LanguageModelConnection LanguageModelConnection { get; private set; }
        public TextToSpeechConnection TextToSpeechConnection { get; private set; }
        public WebsocketConnection Websocket { get; private set; }
        
        /// <summary>
        /// Callback when the singleton awakes.
        /// </summary>
        protected override void OnAwakeSingleton()
        {
            // Initialize UnityMainThreadDispatcher
            UnityMainThreadDispatcher.Instance.InitializeSingleton();
          
            // Set up connection to service connector services
            SetupConnection();
        }

        /// <summary>
        /// Callback when the singleton starts.
        /// </summary>
        protected override void OnStartSingleton()
        {
        }

        /// <summary>
        /// Callback when the singleton updates.
        /// </summary>
        protected override void OnUpdateSingleton()
        {
        }

        /// <summary>
        /// Callback when the singleton is getting destroyed.
        /// </summary>
        protected override void OnDestroySingleton()
        {
        }

        /// <summary>
        /// Resets the connection to all service connector services.
        /// </summary>
        public void ResetConnection()
        {
            if (Websocket.IsNotNull()) DestroyImmediate(Websocket.gameObject);
            if (SpeechToTextConnection.IsNotNull()) DestroyImmediate(SpeechToTextConnection.gameObject);
            if (LanguageModelConnection.IsNotNull()) DestroyImmediate(LanguageModelConnection.gameObject);
            if (TextToSpeechConnection.IsNotNull()) DestroyImmediate(TextToSpeechConnection.gameObject);
            
            SetupConnection();
        }

        /// <summary>
        /// Sets up the connection to all service connector services.
        /// </summary>
        private void SetupConnection()
        {
            // Set up websocket connection component
            Websocket = GetComponentInChildren<WebsocketConnection>();
            if (Websocket.IsNull()) Websocket = gameObject.AddChildGameObject("Websocket").AddComponent<WebsocketConnection>();

            // Set up SST, LLM, and TTS components
            SpeechToTextConnection = GetComponentInChildren<SpeechToTextConnection>();
            if (SpeechToTextConnection.IsNull()) SpeechToTextConnection = gameObject.AddChildGameObject("SpeechToText").AddComponent<SpeechToTextConnection>();
            SpeechToTextConnection.websocketService = Websocket;
            
            TextToSpeechConnection = GetComponentInChildren<TextToSpeechConnection>();
            if (TextToSpeechConnection.IsNull()) TextToSpeechConnection = gameObject.AddChildGameObject("TextToSpeech").AddComponent<TextToSpeechConnection>();

            LanguageModelConnection = GetComponentInChildren<LanguageModelConnection>();
            if (LanguageModelConnection.IsNull()) LanguageModelConnection = gameObject.AddChildGameObject("LanguageModel").AddComponent<LanguageModelConnection>();
        }

        // TODO: This code can be used to store a key in an environment variable.
        // public static class OpenAi
        // {
        //     private const string OpenAiApiKeyEnvironmentVariable = "OPENAI_API_KEY";
        //
        //     /// <summary>
        //     /// Returns the stored OpenAI API key from system environment variables.
        //     /// </summary>
        //     /// <returns></returns>
        //     public static string GetApiKey()
        //     {
        //         string apiKey =
        //             Environment.GetEnvironmentVariable(OpenAiApiKeyEnvironmentVariable, EnvironmentVariableTarget.User);
        //         if (string.IsNullOrEmpty(apiKey))
        //         {
        //             Debug.Log("OpenAI API key is not set in the system environment variables.");
        //         }
        //
        //         return apiKey;
        //     }
        //
        //     /// <summary>
        //     /// Stores the  OpenAI API key as system environment variable.
        //     /// </summary>
        //     /// <param name="apiKey"></param>
        //     public static void SetApiKey(string apiKey)
        //     {
        //         Environment.SetEnvironmentVariable(OpenAiApiKeyEnvironmentVariable, apiKey,
        //             EnvironmentVariableTarget.User);
        //         Debug.Log("OpenAI API Key has been set as a system environment variable.");
        //     }
        //
        //     /// <summary>
        //     ///  Clear API key from system environment variables.
        //     /// </summary>
        //     public static void ClearApiKey()
        //     {
        //         Environment.SetEnvironmentVariable(OpenAiApiKeyEnvironmentVariable, string.Empty,
        //             EnvironmentVariableTarget.User);
        //         Debug.Log("OpenAI API Key has been cleared from the system environment variables.");
        //     }
        // }
    }
}