// This script can be useful if you want to have gaze behaviors of IVA using the paid asset from unity: https://assetstore.unity.com/packages/tools/animation/realistic-eye-movements-29168#asset_quality
// If you have already imported the realisticEyeMovement package, please uncomment this script to use. 
// using RealisticEyeMovements;
// using UnityEngine;
// using UnityEditor;
// using IVH.Core.ServiceConnector;
// using System;
// using System.Linq;
// using System.Collections.Generic;

// namespace IVH.Core.IntelligentVirtualAgent
// {
//     [CustomEditor(typeof(RealisticGazeContingentAgent))]
//     public class RealisticGazeContingentAgentEditor : Editor
//     {
//         private RealisticGazeContingentAgent agent;
//         private CloudServiceManager cloudServiceManager;

//         private SerializedProperty visionProp;
//         private SerializedProperty targetCameraTypeProp;
//         private SerializedProperty imageTriggerModeProp;
//         private SerializedProperty resolutionProp;
//         private SerializedProperty rawImageProp;

//         public void OnEnable()
//         {
//             // Get a reference to the target script
//             agent = target as RealisticGazeContingentAgent;
//             cloudServiceManager = agent.cloudServiceManagerInstance.GetComponent<CloudServiceManager>();

//             visionProp = serializedObject.FindProperty("vision");
//             targetCameraTypeProp = serializedObject.FindProperty("targetCameraType");
//             imageTriggerModeProp = serializedObject.FindProperty("imageTriggerMode");
//             resolutionProp = serializedObject.FindProperty("resolution");
//             rawImageProp = serializedObject.FindProperty("rawImage");
//         }

//         public override void OnInspectorGUI()
//         {
//             // Draw the inspector
//             serializedObject.Update();
//             DrawPropertiesExcluding(serializedObject, "TTSService", "STTService", "foundationModel");


//             // Add a space in the inspector
//             EditorGUILayout.Space();

//             EditorGUILayout.LabelField("Cloud Service Settings", EditorStyles.boldLabel);

//             // Combine UHAM services and dynamically detected services
//             List<string> availableSTTServices = new List<string>();
//             List<string> availableTTServices = new List<string>();
//             List<string> availableLMMServices = new List<string>();

//             // Add UHAM services
//             availableSTTServices.AddRange(Enum.GetNames(typeof(VoiceRecognitionService)).Where(n => n.StartsWith("UHAM")));
//             availableTTServices.AddRange(Enum.GetNames(typeof(VoiceService)).Where(n => n.StartsWith("UHAM")));
//             availableLMMServices.AddRange(Enum.GetNames(typeof(FoundationModels)).Where(n => n.StartsWith("UHAM")));

//             // Check for specific components or configurations and add to the list
//             if (cloudServiceManager != null)
//             {
//                 // Example: Check for locally running services in children
//                 if (cloudServiceManager.GetComponentsInChildren<WhisperSTT>().Length > 0)
//                 {
//                     availableSTTServices.Add("Local_Whisper");
//                 }
//                 if (cloudServiceManager.GetComponentsInChildren<AzureSpeech>().Length > 0)
//                 {
//                     availableTTServices.Add("Unity_Azure");
//                 }
//                 if (cloudServiceManager.GetComponentsInChildren<ElevenLabTTS>().Length > 0)
//                 {
//                     availableTTServices.Add("Unity_ElevenLab");
//                 }
//                 // if (cloudServiceManager.GetComponentsInChildren<LocalLLMWrapper>().Length > 0)
//                 // {
//                 //     availableLMMServices.Add("Local_Model_LLM");
//                 // }
//                 if (cloudServiceManager.GetComponentsInChildren<GoogleCloudAIWrapper>().Length > 0)
//                 {
//                     availableLMMServices.Add("Unity_Gemini_VLM");
//                 }
//                 if (cloudServiceManager.GetComponentsInChildren<OpenAIWrapper>().Length > 0)
//                 {
//                     availableLMMServices.Add("Unity_OpenAI_VLM");
//                 }
//             }

//             // Display the combined list of available options
//             if (availableSTTServices.Count > 0)
//             {
//                 int selectedSTTIndex = availableSTTServices.IndexOf(agent.STTService.ToString());
//                 if (selectedSTTIndex == -1) selectedSTTIndex = 0; // Default to the first option if the current value is not in the list
//                 selectedSTTIndex = EditorGUILayout.Popup("STT Service", selectedSTTIndex, availableSTTServices.ToArray());
//                 agent.STTService = (VoiceRecognitionService)Enum.Parse(typeof(VoiceRecognitionService), availableSTTServices[selectedSTTIndex]);
//             }
//             else
//             {
//                 EditorGUILayout.LabelField("No available STT services found.");
//             }

//             if (availableTTServices.Count > 0)
//             {
//                 int selectedTTSIndex = availableTTServices.IndexOf(agent.TTSService.ToString());
//                 if (selectedTTSIndex == -1) selectedTTSIndex = 0; // Default to the first option if the current value is not in the list
//                 selectedTTSIndex = EditorGUILayout.Popup("TTS Service", selectedTTSIndex, availableTTServices.ToArray());
//                 agent.TTSService = (VoiceService)Enum.Parse(typeof(VoiceService), availableTTServices[selectedTTSIndex]);
//             }
//             else
//             {
//                 EditorGUILayout.LabelField("No available TTS services found.");
//             }

//             if (availableLMMServices.Count > 0)
//             {
//                 int selectedLMMIndex = availableLMMServices.IndexOf(agent.foundationModel.ToString());
//                 if (selectedLMMIndex == -1) selectedLMMIndex = 0; // Default to the first option if the current value is not in the list
//                 selectedLMMIndex = EditorGUILayout.Popup("Foundation Model", selectedLMMIndex, availableLMMServices.ToArray());
//                 agent.foundationModel = (FoundationModels)Enum.Parse(typeof(FoundationModels), availableLMMServices[selectedLMMIndex]);
//             }
//             else
//             {
//                 EditorGUILayout.LabelField("No available Foundation Models found.");
//             }

//             // Add a space in the inspector
//             EditorGUILayout.Space();

//             if (cloudServiceManager != null)
//             {
//                 if (agent.foundationModel==FoundationModels.Unity_Gemini_VLM || agent.foundationModel == FoundationModels.Unity_OpenAI_VLM)
//                 {
//                     EditorGUILayout.PropertyField(visionProp);
//                     EditorGUILayout.PropertyField(targetCameraTypeProp);
//                     EditorGUILayout.PropertyField(imageTriggerModeProp);
//                     EditorGUILayout.PropertyField(resolutionProp);
//                     EditorGUILayout.PropertyField(rawImageProp);
//                 }
//                 else
//                 {
//                     visionProp.boolValue = false;
//                 }
//             }

//             // Add a space in the inspector
//             EditorGUILayout.Space();

//             if (!Application.isPlaying)
//             {
//                 // Create a button that says "Setup Agent"
//                 if (GUILayout.Button("Setup Agent"))
//                 {
//                     agent.SetupVirtualAgent();
//                 }

//                 if (GUILayout.Button("Clear Agent"))
//                 {
//                     agent.DestroyVirtualAgent();
//                 }

//             }
//             if (Application.isPlaying)
//             {
//                 if (GUILayout.Button("Start Simple Chat"))
//                 {
//                     agent.StartSimpleChat();
//                 }

//                 if (GUILayout.Button("Stop Simple Chat"))
//                 {
//                     agent.StopSimpleChat();
//                 }

//                 if (GUILayout.Button("Instant Actor"))
//                 {
                    
//                     agent.StartQuickSpeech(agent.SimpleText);
//                 }
//             }

//             // Apply changes to the serialized object
//             serializedObject.ApplyModifiedProperties();
//         }
//     }
// }