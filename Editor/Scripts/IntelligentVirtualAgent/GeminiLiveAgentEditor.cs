using UnityEngine;
using UnityEditor;
using IVH.Core.IntelligentVirtualAgent; // Ensure namespace matches Agent
using IVH.Core.ServiceConnector;

namespace IVH.Core.IntelligentVirtualAgent.EditorScripts
{
    [CustomEditor(typeof(GeminiLiveAgent))]
    public class GeminiLiveAgentEditor : Editor
    {
        private GeminiLiveAgent agent;
        
        // Properties specific to GeminiLiveAgent
        private SerializedProperty voiceNameProp;
        private SerializedProperty autoConnectProp;
        private SerializedProperty microphoneProp;
        private SerializedProperty inputGainProp;
        
        // Vision Properties (Inherited from AgentBase)
        private SerializedProperty visionProp;
        private SerializedProperty targetCameraTypeProp;
        private SerializedProperty resolutionProp;
        private SerializedProperty rawImageProp;

        private SerializedProperty selectedWebCamNameProp; 
        private SerializedProperty visionUpdateFrequencyProp; 

        private SerializedProperty cloudServiceManagerPrefabProp; 

        private SerializedProperty agentLanguageProp; 
        private SerializedProperty simpleActorHeaderProp; 
        public void OnEnable()
        {
            agent = target as GeminiLiveAgent;
            
            // Map to variables in GeminiLiveAgent.cs
            voiceNameProp = serializedObject.FindProperty("voiceName");
            autoConnectProp = serializedObject.FindProperty("autoConnectOnStart");
            microphoneProp = serializedObject.FindProperty("microphoneDeviceName");
            inputGainProp = serializedObject.FindProperty("inputGain");

            // Map to variables in AgentBase.cs
            visionProp = serializedObject.FindProperty("vision");
            targetCameraTypeProp = serializedObject.FindProperty("targetCameraType");
            resolutionProp = serializedObject.FindProperty("resolution");
            rawImageProp = serializedObject.FindProperty("rawImage");
            selectedWebCamNameProp = serializedObject.FindProperty("selectedWebCamName"); 
            visionUpdateFrequencyProp = serializedObject.FindProperty("visionUpdateFrequency");

            cloudServiceManagerPrefabProp = serializedObject.FindProperty("cloudServiceManager");
            agentLanguageProp = serializedObject.FindProperty("language");

            simpleActorHeaderProp = serializedObject.FindProperty("SimpleText");
            
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // 1. Draw Default Properties (excluding ones we customize below)
            DrawPropertiesExcluding(serializedObject, 
                "m_Script", 
                "voiceName", "autoConnectOnStart", 
                "microphoneDeviceName", "inputGain", 
                "vision", "targetCameraType", "resolution", "rawImage", 
                // Exclude other AgentBase props you don't want clogging the UI
                "TTSService", "STTService", "foundationModel", "triggerPhrases", "wakeupMode", "cloudServiceManagerPrefab", "language");

            EditorGUILayout.Space();

            // 2. Gemini Configuration
            EditorGUILayout.LabelField("Gemini Live Settings", EditorStyles.boldLabel);
            
            // Voice Selection
            string[] voices = { "Puck", "Charon", "Kore", "Fenrir", "Aoede", "Leda", "Orus", "Zephyr" };
            int selectedVoice = System.Array.IndexOf(voices, voiceNameProp.stringValue);
            if (selectedVoice == -1) selectedVoice = 0;
            
            selectedVoice = EditorGUILayout.Popup("Agent Voice", selectedVoice, voices);
            voiceNameProp.stringValue = voices[selectedVoice];

            EditorGUILayout.PropertyField(autoConnectProp, new GUIContent("Auto Connect"));

            // 3. Audio Input
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Audio Input", EditorStyles.boldLabel);

            // Microphone Dropdown
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
                EditorGUILayout.HelpBox("No Microphones Detected!", MessageType.Error);
            }

            EditorGUILayout.PropertyField(inputGainProp, new GUIContent("Mic Gain"));

            // 4. Vision
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(visionProp);
            if (visionProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(targetCameraTypeProp);
                
                // --- NEW: Webcam Device Selection Logic ---
                // Only show this dropdown if the Target Camera Type is WebCam
                // Note: We need to cast the enum properly or check the int value. 
                // Assuming TargetCameraType.WebCam is an enum.
                
                // We access the enum value index. You might need to check your enum definition index.
                // Assuming WebCam is not index 0. If TargetCameraType is AgentCamera, WebCam, etc.
                // It is safer to check the enum name if possible, or just check the int value.
                // Let's assume you check the enum text:
                if (targetCameraTypeProp.enumNames[targetCameraTypeProp.enumValueIndex] == "WebCam") 
                {
                    WebCamDevice[] devices = WebCamTexture.devices;
                    if (devices.Length > 0)
                    {
                        // Create a list of names
                        string[] deviceNames = new string[devices.Length];
                        for (int i = 0; i < devices.Length; i++) deviceNames[i] = devices[i].name;

                        // Find current index
                        int camIndex = System.Array.IndexOf(deviceNames, selectedWebCamNameProp.stringValue);
                        if (camIndex == -1) camIndex = 0;

                        // Draw Popup
                        camIndex = EditorGUILayout.Popup("Webcam Device", camIndex, deviceNames);

                        // Save selection
                        selectedWebCamNameProp.stringValue = deviceNames[camIndex];
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("No Webcam devices found!", MessageType.Warning);
                    }
                }

                EditorGUILayout.PropertyField(resolutionProp);
                EditorGUILayout.PropertyField(rawImageProp);
                EditorGUILayout.PropertyField(visionUpdateFrequencyProp, new GUIContent("Update Frequency (s)"));

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);

            // 5. Setup Controls (Editor Mode)
            if (!Application.isPlaying)
            {
               // EditorGUILayout.LabelField("Agent Structure", EditorStyles.boldLabel);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Setup Virtual Agent", GUILayout.Height(25))) agent.SetupVirtualAgent();
                if (GUILayout.Button("Clear Virtual Agent", GUILayout.Height(25))) agent.DestroyVirtualAgent();
                GUILayout.EndHorizontal();
            }

            // 6. Runtime Controls (Play Mode)
            if (Application.isPlaying)
            {
                EditorGUILayout.LabelField("Live Controls", EditorStyles.boldLabel);
                GUI.backgroundColor = new Color(0.7f, 1f, 0.7f); // Light Green

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Reconnect / Reset", GUILayout.Height(30)))
                {
                    agent.Connect();
                }
                
                GUILayout.EndHorizontal();

                if (visionProp.boolValue)
                {

                   EditorGUILayout.LabelField($"Vision Auto-Send: {(agent.IsSessionReady() ? "Active" : "Waiting...")}", EditorStyles.miniLabel);
                
                }

                GUI.backgroundColor = Color.white;
            }
            
        
            serializedObject.ApplyModifiedProperties();
        }
    }
}