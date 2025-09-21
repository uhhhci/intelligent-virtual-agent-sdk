using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(SimpleChatBehaviour))]
public class SimpleChatBehaviourEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        SimpleChatBehaviour simpleChatBehaviour = (SimpleChatBehaviour)target;

        if (Application.isPlaying)
        {
            if (GUILayout.Button("Start Simple Chat"))
            {
                simpleChatBehaviour.StartSimpleChat();
            }

            if (GUILayout.Button("Stop Simple Chat"))
            {
                simpleChatBehaviour.StopSimpleChat();
            }
        }
    }
}