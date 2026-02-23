using UnityEngine;
using UnityEditor;
using System;

namespace IVH.Core.IntelligentVirtualAgent
{
    [CustomEditor(typeof(GeminiVoiceOnlyAgent))]
    public class GeminiVoiceOnlyAgentEditor : Editor
    {   
        private SerializedProperty voiceNameProp;
        private SerializedProperty microphoneDeviceNameProp;

        // Define the available Gemini voices here
        private readonly string[] geminiVoices = { "Puck", "Charon", "Kore", "Fenrir", "Aoede", "Leda", "Orus", "Zephyr" };

        public void OnEnable()
        {
            // Find properties once when the Inspector is enabled
            voiceNameProp = serializedObject.FindProperty("voiceName");
            microphoneDeviceNameProp = serializedObject.FindProperty("microphoneDeviceName");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Gemini Configuration", EditorStyles.boldLabel);

            int selectedVoice = Array.IndexOf(geminiVoices, voiceNameProp.stringValue);
            if (selectedVoice == -1) selectedVoice = 0; // Fallback to index 0 if the string doesn't match
            
            selectedVoice = EditorGUILayout.Popup("Agent Voice", selectedVoice, geminiVoices);
            voiceNameProp.stringValue = geminiVoices[selectedVoice];

            DrawPropertiesExcluding(serializedObject, "m_Script", "voiceName", "microphoneDeviceName");
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Hardware Config", EditorStyles.boldLabel);

            string[] devices = Microphone.devices;
            
            if (devices.Length > 0)
            {
                int index = Mathf.Max(0, Array.IndexOf(devices, microphoneDeviceNameProp.stringValue));

                int newIndex = EditorGUILayout.Popup("Microphone", index, devices);
                if (newIndex != index || string.IsNullOrEmpty(microphoneDeviceNameProp.stringValue))
                {
                    microphoneDeviceNameProp.stringValue = devices[newIndex];
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No Microphone devices found!", MessageType.Warning);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}