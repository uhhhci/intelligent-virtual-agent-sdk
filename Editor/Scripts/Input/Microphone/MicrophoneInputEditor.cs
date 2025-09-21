using IVH.Core.Utils.Editor;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(IVH.Core.Input.MicrophoneInput))]
public class MicrophoneInputEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Draw the default inspector
        DrawDefaultInspector();

        // Get a reference to the MicrophoneInput script
        IVH.Core.Input.MicrophoneInput microphoneInput = (IVH.Core.Input.MicrophoneInput)target;

        // Add buttons to start and stop capturing
        if (Application.isPlaying)
        {
            if (!microphoneInput.IsStarted)
            {
                if (EditorElementFactory.DrawButton("Start Capturing"))
                {
                    microphoneInput.StartCapturing();
                }
            }
            else
            {
                if (EditorElementFactory.DrawButton("Stop Capturing"))
                {
                    microphoneInput.StopCapturing();
                }
            }
        }
    }
}