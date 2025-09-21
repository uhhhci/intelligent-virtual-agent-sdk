using System;
using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using IVH.Core.ServiceConnector;

namespace IVH.Core.ServiceConnector
{
    public class ChatMessage { }


    public enum GPTModels
    {
        GPT_4o,
        GPT_4o_mini,
        GPT_4_turbo,
        GPT_4,
        GPT_3_5_turbo
    }

    public class GPTModelHelper
    {
        public static string GetModelName(GPTModels model)
        {
            switch (model)
            {
                case GPTModels.GPT_4o:
                    return "gpt-4o";
                case GPTModels.GPT_4o_mini:
                    return "gpt-4o-mini";
                case GPTModels.GPT_4_turbo:
                    return "gpt-4-turbo";
                case GPTModels.GPT_4:
                    return "gpt-4";
                case GPTModels.GPT_3_5_turbo:
                    return "gpt-3.5-turbo";
                default:
                    throw new ArgumentOutOfRangeException(nameof(model), model, "Unknown GPT model.");
            }
        }
    }

    // Classes for the API Connection

    public enum GPTMessageRoles
    {
        SYSTEM,
        ASSISTANT,
        USER
    }

    [Serializable]
    public class GPTRequestPayload
    {
        public string model;
        public List<GPTMessage> messages;
        public int max_tokens;
        public float temperature;

        [JsonConstructor]
        public GPTRequestPayload(string model, List<GPTMessage> messages, int max_tokens = 300, float temperature = 0.5f)
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(model))
                throw new ArgumentException("Model cannot be null or empty.", nameof(model));
            if (messages == null || messages.Count == 0)
                throw new ArgumentException("Messages cannot be null or empty.", nameof(messages));
            if (max_tokens <= 0)
                throw new ArgumentOutOfRangeException(nameof(max_tokens), "Max tokens must be greater than 0.");
            if (temperature < 0.0f || temperature > 1.0f)
                throw new ArgumentOutOfRangeException(nameof(temperature), "Temperature must be between 0.0 and 1.0.");

            // Assign properties
            this.model = model;
            this.messages = messages;
            this.max_tokens = max_tokens;
            this.temperature = temperature;
        }

        public static GPTImageMessage GPTImagePayloadConstructor(string basePrompt, string base64Image, ImageDetail imageDetail = ImageDetail.Auto)
        {
            ImageMessagePrompt imageMessagePrompt = new ImageMessagePrompt(basePrompt);
            ImageDetailContent imageDetailContent = new ImageDetailContent(base64Image, imageDetail);
            ImageMessageContent imageMessageContent = new ImageMessageContent(imageDetailContent);
            return new GPTImageMessage(GPTMessageRoles.USER, imageMessagePrompt, imageMessageContent);
        }


    }


    [Serializable]
    public abstract class GPTMessage: ChatMessage
    {
        [JsonProperty("role")]
        public string role;
        //public abstract object Content { get; }
    }

    [Serializable]
    public class GPTTextMessage : GPTMessage
    {
        [JsonProperty("content")]
        public string content { get; set; }
        
        [JsonConstructor]
        public GPTTextMessage(GPTMessageRoles messageRole, params string[] content)
        {
            role = messageRole.ToString().ToLower();
            this.content = string.Join("", content);
        }
    }


    #region GPT Tools 

    [Serializable]
    public class GPTFunctionMessagePayload : GPTRequestPayload
    {
        public string tool_choice { get; set; }
        public object[] tools { get; set; } // should store a list of GPTToolItem

        [JsonConstructor]
        public GPTFunctionMessagePayload(
            string model,
            List<GPTMessage> messages,
            int max_tokens = 300,
            float temperature = 0.5f,
            object[] tools = null,
            string tool_choice = "auto")
            : base(model, messages, max_tokens, temperature) // Reuse base class logic
        {
            this.tools = tools ?? new object[] { }; // Assign default tools if null
            this.tool_choice = tool_choice;       // Set tool_choice
        }

        public static GPTToolItem CreateFunctionTool(
            string functionName,
            string description,
            Dictionary<string, (string type, string propertyDescription, string[] enumValues)> parameters,
            List<string> requiredFields)
        {
            // Construct properties for the function parameters
            var properties = new Dictionary<string, GPTFunctionParameter>();
            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    properties[param.Key] = new GPTFunctionParameter
                    {
                        type = param.Value.type,
                        description = param.Value.propertyDescription,
                        Enum = param.Value.enumValues
                    };
                }
            }

            // Create the function tool
            var tool = new GPTToolItem
            {
                function = new GPTFunctionDescription
                {
                    name = functionName,
                    description = description,
                    parameters = properties.Count > 0 || (requiredFields != null && requiredFields.Count > 0)
                        ? new GPTFunctionParameters
                        {
                            properties = properties,
                            required = requiredFields != null && requiredFields.Count > 0 ? requiredFields : null
                        }
                        : null
                }
            };

            return tool;
        }
    }

    [Serializable]
    public class GPTToolItem
    {
        public string type = "function";
        public GPTFunctionDescription function;
    }

    [Serializable]
    public class GPTFunctionDescription
    {
        public string name { get; set; }
        public string description { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public GPTFunctionParameters parameters { get; set; } // Exclude from serialization if null
    }

    [Serializable]
    public class GPTFunctionParameters
    {
        public string Type { get; set; } = "object";

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, GPTFunctionParameter> properties { get; set; } = new();

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<string> required { get; set; } = new();
    }

    [Serializable]
    public class GPTFunctionParameter
    {
        public string type { get; set; }
        public string description { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string[] Enum { get; set; } = null; // Optional for enum values
    }

    public enum FunctionCallingMode
    {
        AUTO,
        REQUIRED
    }

    #endregion

    #region GPTImage
    [Serializable]
    public class GPTImageMessage : GPTMessage
    {
        [JsonProperty("content")]
        public object[] content { get; set; }

        [JsonConstructor]
        public GPTImageMessage(GPTMessageRoles messageRole, ImageMessagePrompt messagePrompt, ImageMessageContent imgContent)
        {
            role = messageRole.ToString().ToLower();
            content = new object[2];
            content[0] = messagePrompt;
            content[1] = imgContent;
        }
    }

    [Serializable]
    public class ImageMessageContent
    {
        [JsonProperty("type")]
        public string type = "image_url";
        [JsonProperty("image_url")]
        public ImageDetailContent image_url;
        
        [JsonConstructor]
        public ImageMessageContent(ImageDetailContent image_url)
        {
            this.image_url = image_url;
        }
    }

    [Serializable]
    public class ImageDetailContent
    {
        [JsonProperty("url")]
        public string url;
        [JsonProperty("detail")]
        public string detail = "low";

        [JsonConstructor]
        public ImageDetailContent(string url, ImageDetail imageDetail = ImageDetail.Low)
        {
            this.url = "data:image/png;base64," + url;
            this.detail = imageDetail.ToString().ToLower();
        }
    }

    [Serializable]
    public class ImageMessagePrompt
    {
        [JsonProperty("type")]
        public string type = "text";
        [JsonProperty("text")]
        public string text;

        [JsonConstructor]
        public ImageMessagePrompt(string imagePrompt)
        {
            this.text = imagePrompt;
        }
    }

    public enum ImageDetail
    {
        Auto,
        Low,
        High
    }

    public enum ImageResolution
    {
        HD720, // 720*1080
        HD1080, // 1080*1920
        VGA //640*480
    }

    public class ImageHelper
    {
        public static Vector2Int GetResolution(ImageResolution resolution)
        {
            switch (resolution)
            {
                case ImageResolution.HD720:
                    return new Vector2Int(1280, 720); // Note: Adjusted to standard HD 720p dimensions (width x height).
                case ImageResolution.HD1080:
                    return new Vector2Int(1920, 1080); // Standard HD 1080p dimensions.
                case ImageResolution.VGA:
                    return new Vector2Int(640, 480); // Standard VGA dimensions.
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(resolution), "Unsupported resolution type.");
            }
        }
    }
    #endregion

}