using UnityEditor;
using UnityEngine;

namespace IVH.Core.ServiceConnector
{
    [CustomEditor(typeof(ElevenLabTTS))]
    public class ElevenLabTTSEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var voiceTypeProp = serializedObject.FindProperty("voiceType");
            var customVoiceIDProp = serializedObject.FindProperty("customVoiceID");

            EditorGUILayout.PropertyField(voiceTypeProp);

            if ((ElevenLabsVoiceType)voiceTypeProp.enumValueIndex == ElevenLabsVoiceType.Custom)
            {
                EditorGUILayout.PropertyField(customVoiceIDProp);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}