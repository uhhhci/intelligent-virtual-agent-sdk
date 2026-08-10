using UnityEngine;
using UnityEditor;
using IVH.Core.ServiceConnector;
using System;
using System.Linq;
using System.Collections.Generic;

namespace IVH.Core.IntelligentVirtualAgent
{
    [CustomEditor(typeof(ConversationalAgent))]
    public class ConversationalAgentEditor : Editor
    {
        private ConversationalAgent agent;
        private CloudServiceManager cloudServiceManager;

        private SerializedProperty visionProp;
        private SerializedProperty targetCameraTypeProp;
        private SerializedProperty imageTriggerModeProp;
        private SerializedProperty resolutionProp;
        private SerializedProperty rawImageProp;
        private SerializedProperty simpleTextProp;

        public void OnEnable()
        {
            agent = target as ConversationalAgent;
            cloudServiceManager = agent.cloudServiceManagerInstance.GetComponent<CloudServiceManager>();

            visionProp = serializedObject.FindProperty("vision");
            targetCameraTypeProp = serializedObject.FindProperty("targetCameraType");
            imageTriggerModeProp = serializedObject.FindProperty("imageTriggerMode");
            resolutionProp = serializedObject.FindProperty("resolution");
            rawImageProp = serializedObject.FindProperty("rawImage");
            simpleTextProp = serializedObject.FindProperty("SimpleText");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "TTSService", "STTService", "foundationModel");


            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Cloud Service Settings", EditorStyles.boldLabel);

            List<string> availableSTTServices = new List<string>();
            List<string> availableTTServices = new List<string>();
            List<string> availableLMMServices = new List<string>();

            availableSTTServices.AddRange(Enum.GetNames(typeof(VoiceRecognitionService)).Where(n => n.StartsWith("UHAM")));
            availableTTServices.AddRange(Enum.GetNames(typeof(VoiceService)).Where(n => n.StartsWith("UHAM")));
            availableLMMServices.AddRange(Enum.GetNames(typeof(FoundationModels)).Where(n => n.StartsWith("UHAM")));

            if (cloudServiceManager != null)
            {
#if IVA_HAS_WHISPER
                if (cloudServiceManager.GetComponentsInChildren<WhisperSTT>().Length > 0)
                {
                    availableSTTServices.Add("Local_Whisper");
                }
#endif
                if (cloudServiceManager.GetComponentsInChildren<AzureSpeech>().Length > 0)
                {
                    availableTTServices.Add("Unity_Azure");
                }
                if (cloudServiceManager.GetComponentsInChildren<ElevenLabTTS>().Length > 0)
                {
                    availableTTServices.Add("Unity_ElevenLab");
                }
                if (cloudServiceManager.GetComponentsInChildren<GoogleCloudAIWrapper>().Length > 0)
                {
                    availableLMMServices.Add("Unity_Gemini_VLM");
                }
                if (cloudServiceManager.GetComponentsInChildren<OpenAIWrapper>().Length > 0)
                {
                    availableLMMServices.Add("Unity_OpenAI_VLM");
                }
            }

            if (availableSTTServices.Count > 0)
            {
                int selectedSTTIndex = availableSTTServices.IndexOf(agent.STTService.ToString());
                if (selectedSTTIndex == -1) selectedSTTIndex = 0;
                selectedSTTIndex = EditorGUILayout.Popup("STT Service", selectedSTTIndex, availableSTTServices.ToArray());
                agent.STTService = (VoiceRecognitionService)Enum.Parse(typeof(VoiceRecognitionService), availableSTTServices[selectedSTTIndex]);
            }
            else
            {
                EditorGUILayout.LabelField("No available STT services found.");
            }

            if (availableTTServices.Count > 0)
            {
                int selectedTTSIndex = availableTTServices.IndexOf(agent.TTSService.ToString());
                if (selectedTTSIndex == -1) selectedTTSIndex = 0;
                selectedTTSIndex = EditorGUILayout.Popup("TTS Service", selectedTTSIndex, availableTTServices.ToArray());
                agent.TTSService = (VoiceService)Enum.Parse(typeof(VoiceService), availableTTServices[selectedTTSIndex]);
            }
            else
            {
                EditorGUILayout.LabelField("No available TTS services found.");
            }

            if (availableLMMServices.Count > 0)
            {
                int selectedLMMIndex = availableLMMServices.IndexOf(agent.foundationModel.ToString());
                if (selectedLMMIndex == -1) selectedLMMIndex = 0;
                selectedLMMIndex = EditorGUILayout.Popup("Foundation Model", selectedLMMIndex, availableLMMServices.ToArray());
                agent.foundationModel = (FoundationModels)Enum.Parse(typeof(FoundationModels), availableLMMServices[selectedLMMIndex]);
            }
            else
            {
                EditorGUILayout.LabelField("No available Foundation Models found.");
            }

            EditorGUILayout.Space();

            if (cloudServiceManager != null)
            {
                if (agent.foundationModel==FoundationModels.Unity_Gemini_VLM || agent.foundationModel == FoundationModels.Unity_OpenAI_VLM)
                {
                    EditorGUILayout.PropertyField(visionProp);
                    EditorGUILayout.PropertyField(targetCameraTypeProp);
                    EditorGUILayout.PropertyField(imageTriggerModeProp);
                    EditorGUILayout.PropertyField(resolutionProp);
                    EditorGUILayout.PropertyField(rawImageProp);
                }
                else
                {
                    visionProp.boolValue = false;
                }
            }

            EditorGUILayout.Space();

            if (simpleTextProp != null)
            {
                EditorGUILayout.PropertyField(simpleTextProp, new GUIContent("Simple Text"));
            }

            EditorGUILayout.Space();

            if (!Application.isPlaying)
            {
                if (GUILayout.Button("Setup Agent"))
                {
                    agent.SetupVirtualAgent();
                }

                if (GUILayout.Button("Clear Agent"))
                {
                    agent.DestroyVirtualAgent();
                }
            }
            if (Application.isPlaying)
            {
                if (GUILayout.Button("Start Simple Chat"))
                {
                    agent.StartSimpleChat();
                }

                if (GUILayout.Button("Stop Simple Chat"))
                {
                    agent.StopSimpleChat();
                }
                if (GUILayout.Button("Instant Actor"))
                {

                    agent.StartQuickSpeech(agent.SimpleText);
                }

            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}