using UnityEngine;
using UnityEditor;
using System;

namespace IVH.Core.IntelligentVirtualAgent
{
    [CustomEditor(typeof(GeminiVoiceOnlyAgent))]
    public class GeminiVoiceOnlyAgentEditor : Editor
    {   
        // Gemini Configuration
        private SerializedProperty voiceNameProp;
        private SerializedProperty autoConnectOnStartProp;
        
        // Settings & Persona
        private SerializedProperty showThinkingProcessProp;
        private SerializedProperty systemInstructionProp;

        // Audio Input
        private SerializedProperty microphoneDeviceNameProp;
        private SerializedProperty inputGainProp;

        // VAD & Interruption
        private SerializedProperty enableVocalInterruptionProp;
        private SerializedProperty voiceDetectionThresholdProp;
        private SerializedProperty useVocalFrequencyFilterProp;
        private SerializedProperty interruptionDebounceTimeProp;

        // UI Interface
        private SerializedProperty logTextDisplayProp;
        private SerializedProperty scrollRectProp;

        // Define the available Gemini voices here
        private readonly string[] geminiVoices = { "Puck", "Charon", "Kore", "Fenrir", "Aoede", "Leda", "Orus", "Zephyr" };

        public void OnEnable()
        {
            // Find properties once when the Inspector is enabled
            voiceNameProp = serializedObject.FindProperty("voiceName");
            autoConnectOnStartProp = serializedObject.FindProperty("autoConnectOnStart");
            
            showThinkingProcessProp = serializedObject.FindProperty("showThinkingProcess");
            systemInstructionProp = serializedObject.FindProperty("systemInstruction");

            microphoneDeviceNameProp = serializedObject.FindProperty("microphoneDeviceName");
            inputGainProp = serializedObject.FindProperty("inputGain");

            enableVocalInterruptionProp = serializedObject.FindProperty("enableVocalInterruption");
            voiceDetectionThresholdProp = serializedObject.FindProperty("voiceDetectionThreshold");
            useVocalFrequencyFilterProp = serializedObject.FindProperty("useVocalFrequencyFilter");
            interruptionDebounceTimeProp = serializedObject.FindProperty("interruptionDebounceTime");

            logTextDisplayProp = serializedObject.FindProperty("logTextDisplay");
            scrollRectProp = serializedObject.FindProperty("scrollRect");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // 1. Draw any standard properties while excluding the ones we are organizing manually
            DrawPropertiesExcluding(serializedObject, 
                "m_Script", 
                "voiceName", "autoConnectOnStart", 
                "showThinkingProcess", "systemInstruction",
                "microphoneDeviceName", "inputGain",
                "enableVocalInterruption", "voiceDetectionThreshold", "useVocalFrequencyFilter", "interruptionDebounceTime",
                "logTextDisplay", "scrollRect");

            EditorGUILayout.Space();

            // 2. Gemini Configuration
            EditorGUILayout.LabelField("Gemini Configuration", EditorStyles.boldLabel);

            int selectedVoice = Array.IndexOf(geminiVoices, voiceNameProp.stringValue);
            if (selectedVoice == -1) selectedVoice = 0; // Fallback to index 0 if the string doesn't match
            
            selectedVoice = EditorGUILayout.Popup("Agent Voice", selectedVoice, geminiVoices);
            voiceNameProp.stringValue = geminiVoices[selectedVoice];

            EditorGUILayout.PropertyField(autoConnectOnStartProp);

            EditorGUILayout.Space();

            // 3. Settings & Persona
            EditorGUILayout.LabelField("Settings & Persona", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(showThinkingProcessProp, new GUIContent("Show Thinking Process"));
            EditorGUILayout.PropertyField(systemInstructionProp, new GUIContent("System Instruction"));

            EditorGUILayout.Space();

            // 4. Audio Input
            EditorGUILayout.LabelField("Audio Input", EditorStyles.boldLabel);

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

            EditorGUILayout.PropertyField(inputGainProp);

            EditorGUILayout.Space();

            // 5. VAD & Interruption
            EditorGUILayout.LabelField("VAD & Interruption", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(enableVocalInterruptionProp, new GUIContent("Enable Vocal Interruption"));

            if (enableVocalInterruptionProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(voiceDetectionThresholdProp, new GUIContent("Detection Threshold"));
                EditorGUILayout.PropertyField(useVocalFrequencyFilterProp, new GUIContent("Use Frequency Filter"));
                EditorGUILayout.PropertyField(interruptionDebounceTimeProp, new GUIContent("Debounce Time (s)"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // 6. UI Interface
            EditorGUILayout.LabelField("UI Interface", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(logTextDisplayProp);
            EditorGUILayout.PropertyField(scrollRectProp);

            serializedObject.ApplyModifiedProperties();
        }
    }
}