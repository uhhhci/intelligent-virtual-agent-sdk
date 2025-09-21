using System;
using UnityEngine;

namespace IVH.Core.Utils
{
    public static class StructuredResponseFormatter
    {
        public static StructuredOutput ExtractMessageAndFunctionCall(string input)
        {
            // Split the string by the "|||" delimiter
            string[] parts = input.Split(new string[] { "|||" }, StringSplitOptions.None);

            // Initialize the output variables
            string message = "";
            string function = "";
            string face = "";
            string gaze = "";
            string physicsAction = "";
            string physicsActionParameter = "";
            
            foreach (string part in parts)
            {
                string trimmedPart = part.Trim(); // Remove leading/trailing whitespace

                if (trimmedPart.StartsWith("message:"))
                {
                    message = trimmedPart.Substring("message:".Length).Trim();
                }
                else if (trimmedPart.StartsWith("body action:"))
                {
                    Debug.Log("action " + part);
                    function = trimmedPart.Substring("body action:".Length).Trim();
                }
                else if (trimmedPart.StartsWith("face:"))
                {

                    Debug.Log("face" + part);
                    face = trimmedPart.Substring("face:".Length).Trim();
                }
                else if (trimmedPart.StartsWith("gaze:"))
                {
                    Debug.Log("gaze" + part);
                    gaze = trimmedPart.Substring("gaze:".Length).Trim();
                }
                else if (trimmedPart.StartsWith("physics action:"))
                {
                    Debug.Log("physics action:" + part);
                    physicsAction = trimmedPart.Substring("physics action:".Length).Trim();
                }
                else if (trimmedPart.StartsWith(""))
                {
                    Debug.Log("physics action parameter:" + part);
                    physicsActionParameter = trimmedPart.Substring("physics action parameter:".Length).Trim();
                }
                else
                {
                    message = trimmedPart;
                    Debug.Log("message: " + part);
                }
            }

            return new StructuredOutput(message, function, face, gaze, physicsAction, physicsActionParameter);
        }
    }
}