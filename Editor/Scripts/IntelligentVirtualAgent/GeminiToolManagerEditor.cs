using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.Linq;
using IVH.Core.IntelligentVirtualAgent;

namespace IVH.Core.IntelligentVirtualAgent.EditorScripts
{
    [CustomEditor(typeof(GeminiToolManager))]
    public class GeminiToolManagerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Attribut-basierte Tools: Komponenten, deren mit [GeminiTool] markierte Methoden
            // automatisch registriert werden (Schema wird aus der Signatur generiert).
            EditorGUILayout.LabelField("Attribute-based Tools", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Komponente hier reinziehen, um alle ihre [GeminiTool]-Methoden automatisch zu " +
                "registrieren. Kein manuelles JSON, Parameternamen stimmen immer.", MessageType.None);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("toolProviders"), true);
            EditorGUILayout.Space();

            SerializedProperty toolsProp = serializedObject.FindProperty("definedTools");

            EditorGUILayout.LabelField("Dynamic Tools Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            for (int i = 0; i < toolsProp.arraySize; i++)
            {
                SerializedProperty toolProp = toolsProp.GetArrayElementAtIndex(i);
                
                EditorGUILayout.BeginVertical("box");
                
                EditorGUILayout.PropertyField(toolProp.FindPropertyRelative("toolName"));
                EditorGUILayout.PropertyField(toolProp.FindPropertyRelative("description"));
                
                SerializedProperty targetCompProp = toolProp.FindPropertyRelative("targetComponent");
                EditorGUILayout.PropertyField(targetCompProp, new GUIContent("Target Component"));

                if (targetCompProp.objectReferenceValue != null)
                {
                    MonoBehaviour mb = (MonoBehaviour)targetCompProp.objectReferenceValue;
                    
                    MethodInfo[] methods = mb.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
                    string[] methodNames = methods.Select(m => m.Name).ToArray();

                    if (methodNames.Length > 0)
                    {
                        SerializedProperty methodNameProp = toolProp.FindPropertyRelative("targetMethodName");
                        
                        int currentIndex = System.Array.IndexOf(methodNames, methodNameProp.stringValue);
                        if (currentIndex == -1) currentIndex = 0;

                        currentIndex = EditorGUILayout.Popup("Callable Function", currentIndex, methodNames);
                        methodNameProp.stringValue = methodNames[currentIndex];

                        MethodInfo selectedMethod = methods[currentIndex];
                        bool hasParameters = selectedMethod.GetParameters().Length > 0;

                        if (hasParameters)
                        {
                            EditorGUILayout.PropertyField(toolProp.FindPropertyRelative("parametersJson"), new GUIContent("Parameters Schema (JSON)"));
                        }
                        else
                        {
                            EditorGUILayout.HelpBox("This function takes no arguments. No JSON schema needed!", MessageType.Info);
                            toolProp.FindPropertyRelative("parametersJson").stringValue = ""; 
                        }
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("No public methods found on this component.", MessageType.Warning);
                    }
                }

                // REMOVED THE REDUNDANT PropertyField FROM HERE

                if (GUILayout.Button("Remove Tool", GUILayout.Width(100)))
                {
                    toolsProp.DeleteArrayElementAtIndex(i);
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }

            if (GUILayout.Button("Add New Tool"))
            {
                toolsProp.InsertArrayElementAtIndex(toolsProp.arraySize);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}