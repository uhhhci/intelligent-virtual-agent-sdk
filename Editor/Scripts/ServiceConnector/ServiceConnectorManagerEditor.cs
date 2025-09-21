using IVH.Core.Utils.Editor;
using UnityEngine;
using UnityEditor;

namespace IVH.Core.ServiceConnector
{
    [CustomEditor(typeof(ServiceConnectorManager))]
    public class ServiceConnectorManagerEditor : Editor
    {
        private ServiceConnectorManager _serviceConnectorManager;
        
        private SerializedProperty _serverIp;
        private SerializedProperty _localKey;
        
        /// <summary>
        /// Callback to initialize the inspector.
        /// </summary>
        public void OnEnable()
        {
            // Get a reference to the target script
            _serviceConnectorManager = target as ServiceConnectorManager;

            // Initialize serialized properties
            _serverIp = serializedObject.FindProperty("serverIp");
            _localKey = serializedObject.FindProperty("localKey");
        }

        /// <summary>
        /// Callback to draw the inspector.
        /// </summary>
        public override void OnInspectorGUI()
        {
            EditorElementFactory.DrawPropertyField(_serverIp, "IP Address:");
            EditorElementFactory.DrawPropertyField(_localKey, "Key:");

            if (EditorElementFactory.DrawButton("Reset Connection"))
            {
                _serviceConnectorManager.ResetConnection();
            }
        }
    }
}