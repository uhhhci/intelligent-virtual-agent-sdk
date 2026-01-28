using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using IVH.Core.ServiceConnector;

namespace IVH.Core.ServiceConnector
{
    public enum GeminiModels
    {
        // please get to know model capabilities here, including vision, structural output, audio, and caching here
        // https://ai.google.dev/gemini-api/docs/models/gemini#gemini-2.0-flash
        // Gemini_1_5_Pro,// gemini-1.5-pro
        // Gemini_2_Exp,  // gemini-2.0-flash-exp
        Gemini2_5_Flash_Lite //gemini-2.5-flash-lite
    }

    public enum GeminiMessageRole
    {
        MODEL,
        USER
    }

    // Supporting Classes and Enums
    public class GeminiRequestPayload
    {
        [JsonProperty("contents")]
        public List<GeminiMessage> Contents { get; set; }

        [JsonProperty("tools")]
        public List<GeminiTool> tools { get; set; }
    }
    public class InlineData
    {
        [JsonProperty("mime_type")]
        public string MimeType { get; set; }

        [JsonProperty("data")]
        public string Data { get; set; }
    }

    public class GeminiMessagePart
    {
        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("inline_data")]
        public InlineData InlineData { get; set; }
    }

    public class GeminiMessage : ChatMessage
    {
        [JsonProperty("role")]
        public string Role { get; set; }

        [JsonProperty("parts")]
        public List<GeminiMessagePart> Parts { get; set; }

        public GeminiMessage() { }

        public GeminiMessage(string role, string text)
        {
            Role = role;
            Parts = new List<GeminiMessagePart>
        {
            new GeminiMessagePart { Text = text }
        };
        }
    }

    public class GeminiModelHelper
    {
        public static string GetModelString(GeminiModels model)
        {
            switch (model)
            {
                // case GeminiModels.Gemini_1_5_Pro:
                //     return "gemini-1.5-pro";
                // case GeminiModels.Gemini_2_Exp:
                //     return "gemini-2.0-flash-exp";
                case GeminiModels.Gemini2_5_Flash_Lite:
                    return "gemini-2.5-flash-lite";
                default:
                    throw new ArgumentException($"Unsupported model: {model}");
            }
        }
    }


[Serializable]
public class GeminiTool
{
    // The Gemini API expects 'function_declarations' to be a list.
    // Even if we're often only adding one, it must be a list.
    [JsonProperty("function_declarations")]
    public List<GeminiFunctionDeclaration> FunctionDeclarations { get; set; } = new List<GeminiFunctionDeclaration>();

    // Constructor to easily create a tool with a single function declaration
    public GeminiTool(GeminiFunctionDeclaration func)
    {
        FunctionDeclarations.Add(func);
    }

    public GeminiTool() { } // Parameterless constructor for serialization
}

// Represents a single function declaration within a GeminiTool
[Serializable]
public class GeminiFunctionDeclaration
{
    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("description")]
    public string Description { get; set; }

    [JsonProperty("parameters", NullValueHandling = NullValueHandling.Ignore)]
    public GeminiFunctionParameters Parameters { get; set; }

    public GeminiFunctionDeclaration(string name, string description, GeminiFunctionParameters parameters = null)
    {
        Name = name;
        Description = description;
        Parameters = parameters;
    }
}

// Defines the parameters for a Gemini function
[Serializable]
public class GeminiFunctionParameters
{
    [JsonProperty("type")]
    public string Type { get; set; } = "object"; // Always "object" for function parameters

    [JsonProperty("properties", NullValueHandling = NullValueHandling.Ignore)]
    public Dictionary<string, GeminiFunctionProperty> Properties { get; set; } = new Dictionary<string, GeminiFunctionProperty>();

    [JsonProperty("required", NullValueHandling = NullValueHandling.Ignore)]
    public List<string> Required { get; set; } = new List<string>();

    public GeminiFunctionParameters() { }
}

// Defines a single property within the function's parameters
[Serializable]
public class GeminiFunctionProperty
{
    [JsonProperty("type")]
    public string Type { get; set; }

    [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
    public string Description { get; set; }

    // For array types, you need to specify the item type using 'items'.
    [JsonProperty("items", NullValueHandling = NullValueHandling.Ignore)]
    public GeminiFunctionProperty Items { get; set; }

    // Enums are handled by providing an 'enum' array.
    [JsonProperty("enum", NullValueHandling = NullValueHandling.Ignore)]
    public List<string> Enum { get; set; }

    public GeminiFunctionProperty(string type, string description = null, List<string> @enum = null)
    {
        Type = type;
        Description = description;
        Enum = @enum;
    }
}

// (Optional) This class helps you manage the entire list of tools
// that you send in a single Gemini API request.
// You would serialize an instance of this class to get the final JSON
// for the "tools" field in your API request body.
[Serializable]
public class GeminiToolListContainer
{
    [JsonProperty("tools")]
    public List<GeminiTool> Tools { get; set; } = new List<GeminiTool>();

    public GeminiToolListContainer() { }

    public GeminiToolListContainer(List<GeminiTool> tools)
    {
        Tools = tools;
    }
}



    // You would then send this JSON in the 'tools' array of your API request payload.
    // Example of how it would look in the overall request payload:
    /*
    {
        "contents": [
            {
                "parts": [
                    {
                        "text": "Please schedule a meeting with Alice and Bob for tomorrow at 3 PM about the Q3 report."
                    }
                ]
            }
        ],
        "tools": [
            {
                "function_declarations": [
                    {
                        "name": "schedule_meeting",
                        "description": "Schedules a meeting with specified attendees at a given time and date.",
                        "parameters": {
                            "type": "object",
                            "properties": {
                                "attendees": {
                                    "type": "array",
                                    "items": { "type": "string" },
                                    "description": "List of people attending the meeting."
                                },
                                "date": {
                                    "type": "string",
                                    "description": "Date of the meeting (e.g., '2024-07-29')"
                                },
                                "time": {
                                    "type": "string",
                                    "description": "Time of the meeting (e.g., '15:00')"
                                },
                                "topic": {
                                    "type": "string",
                                    "description": "The subject or topic of the meeting."
                                }
                            },
                            "required": ["attendees", "date", "time", "topic"]
                        }
                    }
                ]
            }
        ]
    }
    */
    
}