using UnityEngine;
using UnityEditor;
using IVH.Core.IntelligentVirtualAgent; // Matches your agent's namespace
using IVH.Core.ServiceConnector;
using UnityEngine.AI; 
namespace IVH.Core.IntelligentVirtualAgent.EditorScripts
{
    [CustomEditor(typeof(GeminiLiveAgent))]
    public class GeminiLiveAgentEditor : Editor
    {
        private GeminiLiveAgent agent;

        // GeminiLiveAgent Properties
        private SerializedProperty voiceNameProp;
        private SerializedProperty autoConnectProp;
        private SerializedProperty microphoneProp;
        private SerializedProperty inputGainProp;
        private SerializedProperty enableVocalInterruptionProp;
        private SerializedProperty voiceDetectionThresholdProp;
        private SerializedProperty useVocalFrequencyFilterProp;
        private SerializedProperty interruptionDebounceTimeProp;
        private SerializedProperty visionUpdateFrequencyProp;

        // Vision Properties (Inherited from AgentBase)
        private SerializedProperty visionProp;
        private SerializedProperty targetCameraTypeProp;
        private SerializedProperty resolutionProp;
        private SerializedProperty rawImageProp;
        private SerializedProperty selectedWebCamNameProp;

        // Basic agent prop. 
        private SerializedProperty characterTypeProp;
        private SerializedProperty enableLocomotionProp;
        // locomotion related
        private bool isNavMeshInScene; 
        private bool CheckForNavMesh()
        {
            // SamplePosition is a very fast, non-allocating way to check if there's NavMesh data.
            // A huge distance (1,000,000) ensures it finds the NavMesh even if Vector3.zero is far away.
            return NavMesh.SamplePosition(Vector3.zero, out _, 1000000f, NavMesh.AllAreas);
        }
        public void OnEnable()
        {
            agent = target as GeminiLiveAgent;
            characterTypeProp = serializedObject.FindProperty("characterType");
            enableLocomotionProp = serializedObject.FindProperty("enableLocomotion");
            // Map GeminiLiveAgent.cs variables
            voiceNameProp = serializedObject.FindProperty("voiceName");
            autoConnectProp = serializedObject.FindProperty("autoConnectOnStart");
            microphoneProp = serializedObject.FindProperty("microphoneDeviceName");
            inputGainProp = serializedObject.FindProperty("inputGain");
            
            enableVocalInterruptionProp = serializedObject.FindProperty("enableVocalInterruption");
            voiceDetectionThresholdProp = serializedObject.FindProperty("voiceDetectionThreshold");
            useVocalFrequencyFilterProp = serializedObject.FindProperty("useVocalFrequencyFilter");
            interruptionDebounceTimeProp = serializedObject.FindProperty("interruptionDebounceTime");
            visionUpdateFrequencyProp = serializedObject.FindProperty("visionUpdateFrequency");

            // Map AgentBase.cs variables
            visionProp = serializedObject.FindProperty("vision");
            targetCameraTypeProp = serializedObject.FindProperty("targetCameraType");
            resolutionProp = serializedObject.FindProperty("resolution");
            rawImageProp = serializedObject.FindProperty("rawImage");
            selectedWebCamNameProp = serializedObject.FindProperty("selectedWebCamName");

            isNavMeshInScene = CheckForNavMesh();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // 1. Draw any standard properties while excluding the ones we are customizing
            DrawPropertiesExcluding(serializedObject,
                "m_Script",
                "voiceName", "autoConnectOnStart",
                "microphoneDeviceName", "inputGain",
                "enableVocalInterruption", "voiceDetectionThreshold", "useVocalFrequencyFilter", "interruptionDebounceTime", "visionUpdateFrequency",
                "vision", "targetCameraType", "resolution", "rawImage", "selectedWebCamName",
                // Exclude unused AgentBase fields to keep it clean
                "TTSService", "STTService", "foundationModel", "triggerPhrases", "wakeupMode", "cloudServiceManagerPrefab", "language");

            if (characterTypeProp.enumNames[characterTypeProp.enumValueIndex] == "CC4OrDIDIMO" && isNavMeshInScene)
            {
                EditorGUILayout.PropertyField(enableLocomotionProp);
            }

            EditorGUILayout.Space();

            // 2. Gemini Configuration
            EditorGUILayout.LabelField("Gemini Live Settings", EditorStyles.boldLabel);

            // Voice Dropdown
            string[] voices = { "Puck", "Charon", "Kore", "Fenrir", "Aoede", "Leda", "Orus", "Zephyr" };
            int selectedVoice = System.Array.IndexOf(voices, voiceNameProp.stringValue);
            if (selectedVoice == -1) selectedVoice = 0;
            selectedVoice = EditorGUILayout.Popup("Agent Voice", selectedVoice, voices);
            voiceNameProp.stringValue = voices[selectedVoice];

            EditorGUILayout.PropertyField(autoConnectProp, new GUIContent("Auto Connect"));

            EditorGUILayout.Space();

            // 3. Audio Input & Interruption
            EditorGUILayout.LabelField("Audio & Interruption", EditorStyles.boldLabel);

            // Microphone Device Dropdown
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

            // --- VAD & Interruption Logic ---
            EditorGUILayout.Space(5);
            EditorGUILayout.PropertyField(enableVocalInterruptionProp, new GUIContent("Enable Vocal Interruption"));

            // Conditionally show advanced VAD settings
            if (enableVocalInterruptionProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(voiceDetectionThresholdProp, new GUIContent("Detection Threshold"));
                EditorGUILayout.PropertyField(useVocalFrequencyFilterProp, new GUIContent("Use Frequency Filter"));
                EditorGUILayout.PropertyField(interruptionDebounceTimeProp, new GUIContent("Debounce Time (s)"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // 4. Vision
            if (visionProp != null)
            {
                EditorGUILayout.PropertyField(visionProp);

                if (visionProp.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(targetCameraTypeProp);

                    if (targetCameraTypeProp != null && 
                        targetCameraTypeProp.enumNames.Length > targetCameraTypeProp.enumValueIndex && 
                        targetCameraTypeProp.enumNames[targetCameraTypeProp.enumValueIndex] == "WebCam")
                    {
                        WebCamDevice[] devices = WebCamTexture.devices;
                        if (devices.Length > 0 && selectedWebCamNameProp != null)
                        {
                            string[] deviceNames = new string[devices.Length];
                            for (int i = 0; i < devices.Length; i++) deviceNames[i] = devices[i].name;

                            int camIndex = System.Array.IndexOf(deviceNames, selectedWebCamNameProp.stringValue);
                            if (camIndex == -1) camIndex = 0;

                            camIndex = EditorGUILayout.Popup("Webcam Device", camIndex, deviceNames);
                            selectedWebCamNameProp.stringValue = deviceNames[camIndex];
                        }
                    }

                    EditorGUILayout.PropertyField(resolutionProp);
                    EditorGUILayout.PropertyField(rawImageProp);
                    EditorGUILayout.PropertyField(visionUpdateFrequencyProp, new GUIContent("Update Frequency (s)"));
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.Space(10);

            // 5. Setup & Runtime Controls
            if (!Application.isPlaying)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Setup Virtual Agent", GUILayout.Height(25))) agent.SetupVirtualAgent();
                // Assumes DestroyVirtualAgent is inherited from AgentBase
                if (GUILayout.Button("Clear Virtual Agent", GUILayout.Height(25))) agent.DestroyVirtualAgent();
                GUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.LabelField("Live Controls", EditorStyles.boldLabel);
                GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
                if (GUILayout.Button("Reconnect Gemini", GUILayout.Height(30))) agent.Connect();
                GUI.backgroundColor = Color.white;
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}