using UnityEngine;
using UnityEditor;
using System;
using IVH.Core.IntelligentVirtualAgent; 

namespace IVH.Core.IntelligentVirtualAgent{

    [CustomEditor(typeof(GeminiVoiceOnlyAgent))]
    public class GeminiVoiceOnlyAgentEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            // 1. Update the serialized object
            serializedObject.Update();

            // 2. Draw all properties EXCEPT the microphone string (we will draw that manually)
            DrawPropertiesExcluding(serializedObject, "microphoneDeviceName");

            // 3. Get the microphone property
            SerializedProperty micProperty = serializedObject.FindProperty("microphoneDeviceName");

            // 4. Create the Dropdown
            string[] devices = Microphone.devices;
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Hardware Config", EditorStyles.boldLabel);

            if (devices.Length > 0)
            {
                // Find current index, default to 0
                int index = Mathf.Max(0, Array.IndexOf(devices, micProperty.stringValue));

                // Draw the popup
                int newIndex = EditorGUILayout.Popup("Microphone", index, devices);

                // If changed, update the string property
                if (newIndex != index || string.IsNullOrEmpty(micProperty.stringValue))
                {
                    micProperty.stringValue = devices[newIndex];
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No Microphone devices found!", MessageType.Warning);
            }

            // 5. Apply changes
            serializedObject.ApplyModifiedProperties();
        }
    }
}