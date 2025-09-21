using UnityEditor;
using UnityEngine;

namespace IVH.Core.IntelligentVirtualAgent
{
    [CustomEditor(typeof(IntelligentVirtualAgentConfigurator))]
    public class IntelligentVirtualAgentConfiguratorEditor : Editor
    {
        private IntelligentVirtualAgentConfigurator _intelligentVirtualAgentConfigurator;
        
        public void OnEnable()
        {
            // Get a reference to the target script
            _intelligentVirtualAgentConfigurator = target as IntelligentVirtualAgentConfigurator;

        }
        
        public override void OnInspectorGUI()
        {
            // Draw the default inspector
            DrawDefaultInspector();

            // Add a space in the inspector
            EditorGUILayout.Space();

            // Create a button that says "Setup Smart Avatar"
            if (GUILayout.Button("Setup Virtual Agent"))
            {
                _intelligentVirtualAgentConfigurator.SetupVirtualAgent();
            }
            
            if (GUILayout.Button("Clear Virtual Agent"))
            {
                _intelligentVirtualAgentConfigurator.DestroyVirtualAgent();
            }
        }
    }
}