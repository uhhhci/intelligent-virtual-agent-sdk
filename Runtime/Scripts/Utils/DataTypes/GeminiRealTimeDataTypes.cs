using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace IVH.Core.ServiceConnector.Gemini.Realtime
{
    // --- TOP LEVEL MESSAGES ---

    public class RealtimeClientMessage
    {
        [JsonProperty("setup", NullValueHandling = NullValueHandling.Ignore)]
        public RealtimeSetup Setup { get; set; }

        [JsonProperty("client_content", NullValueHandling = NullValueHandling.Ignore)]
        public ClientContent ClientContent { get; set; }

        [JsonProperty("tool_response", NullValueHandling = NullValueHandling.Ignore)]
        public ToolResponseContainer ToolResponse { get; set; }
    }

    public class RealtimeServerMessage
    {
        [JsonProperty("server_content")]
        public ServerContent ServerContent { get; set; }

        [JsonProperty("tool_call")]
        public ToolCallContainer ToolCall { get; set; }
    }

    // --- SETUP STRUCTURE ---

    public class RealtimeSetup
    {
        [JsonProperty("model")]
        public string Model { get; set; }

        [JsonProperty("generation_config", NullValueHandling = NullValueHandling.Ignore)]
        public GenerationConfig GenerationConfig { get; set; }

        // CRITICAL FIX: The API rejects empty lists []. It must be null if no tools are used.
        [JsonProperty("tools", NullValueHandling = NullValueHandling.Ignore)]
        public List<GeminiTool> Tools { get; set; }

        // CRITICAL FIX: The Realtime API uses "system_instruction" (singular), unlike the REST API.
        [JsonProperty("system_instruction", NullValueHandling = NullValueHandling.Ignore)]
        public ContentSystemInstruction SystemInstruction { get; set; }
    }

    public class ContentSystemInstruction
    {
        [JsonProperty("parts")]
        public List<SimpleTextPart> Parts { get; set; }
    }


    public class GenerationConfig
    {
        // CHANGE THIS: Remove "TEXT". The API struggles with both simultaneously in setup.
        [JsonProperty("response_modalities")]
        public List<string> ResponseModalities { get; set; } = new List<string> { "AUDIO" };

        [JsonProperty("speech_config")]
        public SpeechConfig SpeechConfig { get; set; }
    }

    public class SpeechConfig
    {
        [JsonProperty("voice_config")]
        public VoiceConfig VoiceConfig { get; set; }
    }

    public class VoiceConfig
    {
        [JsonProperty("prebuilt_voice_config")]
        public PrebuiltVoiceConfig PrebuiltVoiceConfig { get; set; }
    }

    public class PrebuiltVoiceConfig
    {
        [JsonProperty("voice_name")]
        public string VoiceName { get; set; }
    }

    // --- CONTENT STRUCTURE ---

    public class ClientContent
    {
        [JsonProperty("turns")]
        public List<ClientTurn> Turns { get; set; }

        [JsonProperty("turn_complete")]
        public bool TurnComplete { get; set; }
    }

    public class ClientTurn
    {
        [JsonProperty("role")]
        public string Role { get; set; } = "user";

        [JsonProperty("parts")]
        public List<InputPart> Parts { get; set; }
    }

    public class InputPart
    {
        [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)]
        public string Text { get; set; }

        [JsonProperty("inline_data", NullValueHandling = NullValueHandling.Ignore)]
        public InlineData InlineData { get; set; }
    }

    public class SimpleTextPart
    {
        [JsonProperty("text")]
        public string Text { get; set; }
    }

    public class ServerContent
    {
        [JsonProperty("model_turn")]
        public ModelTurn ModelTurn { get; set; }

        [JsonProperty("turn_complete")]
        public bool TurnComplete { get; set; }
    }

    public class ModelTurn
    {
        [JsonProperty("parts")]
        public List<ServerPart> Parts { get; set; }
    }

    public class ServerPart
    {
        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("inline_data")]
        public InlineData InlineData { get; set; }
    }

    // --- TOOLS STRUCTURE ---

    public class ToolCallContainer
    {
        [JsonProperty("function_calls")]
        public List<FunctionCallItem> FunctionCalls { get; set; }
    }

    public class FunctionCallItem
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("args")]
        public Dictionary<string, object> Args { get; set; }
    }

    public class ToolResponseContainer
    {
        [JsonProperty("function_responses")]
        public List<FunctionResponseItem> FunctionResponses { get; set; }
    }

    public class FunctionResponseItem
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("response")]
        public object Response { get; set; }
    }
}