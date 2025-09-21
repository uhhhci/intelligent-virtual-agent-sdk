using System;
using IVH.Core.ServiceConnector;
using IVH.Core.Utils.Editor;
using UnityEditor;

[CustomEditor(typeof(TextToSpeechConnection))]
public class TextToSpeechConnectionEditor : Editor
{
    private TextToSpeechConnection _textToSpeechConnection;
    
    private SerializedProperty _textToSpeechPort;
    
    /// <summary>
    /// Callback to initialize the inspector.
    /// </summary>
    public void OnEnable()
    {
        // Get a reference to the target script
        _textToSpeechConnection = target as TextToSpeechConnection;

        // Initialize serialized properties
        _textToSpeechPort = serializedObject.FindProperty("textToSpeechPort");
    }

    /// <summary>
    /// Callback to draw the inspector.
    /// </summary>
    public override void OnInspectorGUI()
    {
        EditorElementFactory.DrawPropertyField(_textToSpeechPort, "ServiceConnector Port:");
        
        int selectedVoiceIndex = Array.IndexOf(_textToSpeechConnection.AvailableVoices, _textToSpeechConnection.selectedVoice);
        _textToSpeechConnection.selectedVoice = _textToSpeechConnection.AvailableVoices[EditorElementFactory.DrawDropdown(_textToSpeechConnection.AvailableVoices,selectedVoiceIndex, "Voice:")];
    }
}
