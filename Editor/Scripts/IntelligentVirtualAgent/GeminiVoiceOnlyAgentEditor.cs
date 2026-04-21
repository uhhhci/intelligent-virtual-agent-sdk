using UnityEngine;
using UnityEditor;
using IVH.Core.IntelligentVirtualAgent;

namespace IVH.Core.IntelligentVirtualAgent.EditorScripts
{
    [CustomEditor(typeof(GeminiVoiceOnlyAgent))]
    public class GeminiVoiceOnlyAgentEditor : Editor
    {
        private GeminiVoiceOnlyAgent agent;

        // Configuration
        private SerializedProperty voiceNameProp;
        private SerializedProperty autoConnectProp;
        private SerializedProperty showThinkingProcessProp;
        private SerializedProperty systemInstructionProp;

        // Audio Input
        private SerializedProperty microphoneProp;
        private SerializedProperty inputGainProp;
        
        // VAD
        private SerializedProperty enableVocalInterruptionProp;
        private SerializedProperty muteMicWhileTalkingProp;
        private SerializedProperty echoInterruptionThresholdProp;
        private SerializedProperty voiceDetectionThresholdProp;
        private SerializedProperty useVocalFrequencyFilterProp;
        private SerializedProperty interruptionDebounceTimeProp;

        // UI
        private SerializedProperty logTextDisplayProp;
        private SerializedProperty scrollRectProp;

        private void OnEnable()
        {
            agent = target as GeminiVoiceOnlyAgent;

            voiceNameProp = serializedObject.FindProperty("voiceName");
            autoConnectProp = serializedObject.FindProperty("autoConnectOnStart");
            showThinkingProcessProp = serializedObject.FindProperty("showThinkingProcess");
            systemInstructionProp = serializedObject.FindProperty("systemInstruction");

            microphoneProp = serializedObject.FindProperty("microphoneDeviceName");
            inputGainProp = serializedObject.FindProperty("inputGain");

            enableVocalInterruptionProp = serializedObject.FindProperty("enableVocalInterruption");
            muteMicWhileTalkingProp = serializedObject.FindProperty("muteMicWhileTalking");
            echoInterruptionThresholdProp = serializedObject.FindProperty("echoInterruptionThreshold");
            voiceDetectionThresholdProp = serializedObject.FindProperty("voiceDetectionThreshold");
            useVocalFrequencyFilterProp = serializedObject.FindProperty("useVocalFrequencyFilter");
            interruptionDebounceTimeProp = serializedObject.FindProperty("interruptionDebounceTime");

            logTextDisplayProp = serializedObject.FindProperty("logTextDisplay");
            scrollRectProp = serializedObject.FindProperty("scrollRect");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // 1. Draw standard unhandled properties (hiding the ones we layout custom below)
            DrawPropertiesExcluding(serializedObject, 
                "m_Script", 
                "voiceName", "autoConnectOnStart", "showThinkingProcess", "systemInstruction",
                "microphoneDeviceName", "inputGain", 
                "enableVocalInterruption", "muteMicWhileTalking", "echoInterruptionThreshold", "voiceDetectionThreshold", "useVocalFrequencyFilter", "interruptionDebounceTime",
                "logTextDisplay", "scrollRect");

            EditorGUILayout.Space();

            // 2. Gemini Configuration
            EditorGUILayout.LabelField("Gemini Voice Settings", EditorStyles.boldLabel);
            
            string[] voices = { "Puck", "Charon", "Kore", "Fenrir", "Aoede", "Leda", "Orus", "Zephyr" };
            int selectedVoice = System.Array.IndexOf(voices, voiceNameProp.stringValue);
            if (selectedVoice == -1) selectedVoice = 0;
            selectedVoice = EditorGUILayout.Popup("Agent Voice", selectedVoice, voices);
            voiceNameProp.stringValue = voices[selectedVoice];

            EditorGUILayout.PropertyField(autoConnectProp, new GUIContent("Auto Connect"));
            EditorGUILayout.PropertyField(showThinkingProcessProp, new GUIContent("Show Thinking Process"));
            EditorGUILayout.PropertyField(systemInstructionProp, new GUIContent("System Instruction"));

            EditorGUILayout.Space();

            // 3. Audio Input
            EditorGUILayout.LabelField("Audio & Input", EditorStyles.boldLabel);

            string[] mics = Microphone.devices;
            if (mics.Length > 0)
            {
                int micIndex = System.Array.IndexOf(mics, microphoneProp.stringValue);
                if (micIndex == -1) micIndex = 0;
                micIndex = EditorGUILayout.Popup("Microphone Device", micIndex, mics);
                microphoneProp.stringValue = mics[micIndex];
            }
            else
            {
                EditorGUILayout.HelpBox("No microphones detected by Unity.", MessageType.Warning);
            }

            EditorGUILayout.PropertyField(inputGainProp, new GUIContent("Mic Gain"));

            // 4. VAD & Interruption
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("VAD & Interruption Logic", EditorStyles.boldLabel);
            
            EditorGUILayout.PropertyField(muteMicWhileTalkingProp, new GUIContent("Prevent Echo (Mute Mic While Talking)"));
            EditorGUILayout.PropertyField(enableVocalInterruptionProp, new GUIContent("Enable Vocal Interruption"));

            if (enableVocalInterruptionProp.boolValue || muteMicWhileTalkingProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(voiceDetectionThresholdProp, new GUIContent("Normal Voice Threshold"));

                if (muteMicWhileTalkingProp.boolValue && enableVocalInterruptionProp.boolValue)
                {
                    EditorGUILayout.PropertyField(echoInterruptionThresholdProp, new GUIContent("Echo Interruption Threshold"));
                    EditorGUILayout.HelpBox("Because 'Prevent Echo' is ON, interruption requires a louder voice to overcome the speaker's echo volume.", MessageType.Info);
                }

                EditorGUILayout.PropertyField(useVocalFrequencyFilterProp, new GUIContent("Use Frequency Filter"));
                EditorGUILayout.PropertyField(interruptionDebounceTimeProp, new GUIContent("Debounce Time (s)"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // 5. UI References
            EditorGUILayout.LabelField("UI Elements", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(logTextDisplayProp, new GUIContent("Log Text Display"));
            EditorGUILayout.PropertyField(scrollRectProp, new GUIContent("Scroll Rect"));

            EditorGUILayout.Space(10);

            // 6. Runtime Controls
            if (Application.isPlaying)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Live Controls", EditorStyles.boldLabel);
                
                // Reconnect Button
                GUI.backgroundColor = new Color(1f, 0.6f, 0.2f); // Orange
                if (GUILayout.Button("Force Reconnect (New Session)", GUILayout.Height(30)))
                {
                    agent.Reconnect();
                }
                GUI.backgroundColor = Color.white; // Reset color
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}