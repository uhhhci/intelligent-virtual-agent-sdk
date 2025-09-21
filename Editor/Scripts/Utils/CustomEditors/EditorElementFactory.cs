using System;
using UnityEditor;
using UnityEngine;

namespace IVH.Core.Utils.Editor
{
    public static class EditorElementFactory
    {

        /// <summary>
        /// Draws content into a foldout.
        /// </summary>
        /// <param name="foldoutContent"></param>
        /// <param name="foldoutState"></param>
        /// <param name="foldoutTitle"></param>
        public static void DrawFoldout(Action foldoutContent, ref bool foldoutState, GUIContent foldoutTitle)
        {
            foldoutState = EditorGUILayout.Foldout(foldoutState, foldoutTitle, true, EditorStyleFactory.GetFoldoutStyle());
            if (foldoutState)
            {
                EditorGUI.indentLevel++;
                foldoutContent();
                EditorGUI.indentLevel--;
            }
        }

        /// <summary>
        /// Draws content into a foldout.
        /// </summary>
        /// <param name="foldoutContent"></param>
        /// <param name="foldoutState"></param>
        /// <param name="foldoutTitle"></param>
        /// <returns>The state of the foldout.</returns>
        public static bool DrawFoldout(Action foldoutContent, bool foldoutState, GUIContent foldoutTitle)
        {
            foldoutState = EditorGUILayout.Foldout(foldoutState, foldoutTitle, true, EditorStyleFactory.GetFoldoutStyle());
            if (foldoutState)
            {
                EditorGUI.indentLevel++;
                foldoutContent();
                EditorGUI.indentLevel--;
            }

            return foldoutState;
        }
        
        /// <summary>
        /// Draws content into a foldout.
        /// </summary>
        /// <param name="foldoutContent"></param>
        /// <param name="foldoutState"></param>
        /// <param name="foldoutTitle"></param>
        /// <returns>The state of the foldout.</returns>
        public static bool DrawFoldout(Action foldoutContent, bool foldoutState, string foldoutTitle)
        {
            return DrawFoldout(foldoutContent, foldoutState, new GUIContent(foldoutTitle));
        }

        /// <summary>
        /// Draws content into a foldout.
        /// </summary>
        /// <param name="foldoutContent"></param>
        /// <param name="foldoutState"></param>
        /// <param name="foldoutTitle"></param>
        public static void DrawFoldout(Action foldoutContent, SerializedProperty foldoutState, GUIContent foldoutTitle)
        {
            foldoutState.isExpanded = EditorGUILayout.Foldout(foldoutState.isExpanded, foldoutTitle, true, EditorStyleFactory.GetFoldoutStyle());
            foldoutState.serializedObject.ApplyModifiedProperties();
            if (foldoutState.isExpanded)
            {
                EditorGUI.indentLevel++;
                foldoutContent();
                EditorGUI.indentLevel--;
            }
        }
        
        /// <summary>
        /// Draws content into a foldout.
        /// </summary>
        /// <param name="foldoutContent"></param>
        /// <param name="foldoutState"></param>
        /// <param name="foldoutTitle"></param>
        public static void DrawFoldout(Action foldoutContent, SerializedProperty foldoutState, string foldoutTitle)
        {
            DrawFoldout(foldoutContent, foldoutState, new GUIContent(foldoutTitle));
        }
        
        /// <summary>
        /// Draws an editor heading.
        /// </summary>
        /// <param name="headingTitle"></param>
        public static void DrawHeading(GUIContent headingTitle)
        {
            EditorGUILayout.LabelField(headingTitle, EditorStyleFactory.GetHeadingStyle());
        }

        /// <summary>
        /// Draws an editor label.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="value"></param>
        public static void DrawLabel(string text, string value)
        {
            EditorGUILayout.LabelField(text, value, EditorStyleFactory.GetLabelStyle());
        }

        /// <summary>
        /// Draws an editor slider for serialized properties (float).
        /// </summary>
        /// <param name="property"></param>
        /// <param name="leftValue"></param>
        /// <param name="rightValue"></param>
        /// <param name="label"></param>
        public static void DrawPropertySlider(SerializedProperty property, float leftValue, float rightValue, GUIContent label)
        {
           PropertyEditors.FloatPropertySlider(property,leftValue,rightValue,label);
        }

        /// <summary>
        /// Draws an editor slider.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="leftValue"></param>
        /// <param name="rightValue"></param>
        /// <param name="label"></param>
        /// <returns></returns>
        public static float DrawSlider(float value, float leftValue, float rightValue, GUIContent label)
        {
            return EditorGUILayout.Slider(label, value, leftValue, rightValue);
        }

        /// <summary>
        /// Draws a button.
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static bool DrawButton(string text)
        {
            return GUILayout.Button(text);
        }

        /// <summary>
        /// Draws a button.
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static bool DrawButton(GUIContent text)
        {
            return GUILayout.Button(text);
        }

        /// <summary>
        /// Draws a button.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public static bool DrawButton(GUIContent text, params GUILayoutOption[] options)
        {
            return GUILayout.Button(text, options);
        }

        /// <summary>
        /// Draws a help text.
        /// </summary>
        /// <param name="text"></param>
        public static void DrawHelpText(string text)
        {
            EditorGUILayout.TextArea(text, EditorStyleFactory.GetHelpText());
        }

        /// <summary>
        /// Draws a dropdown field.
        /// </summary>
        /// <param name="options"></param>
        /// <param name="selected"></param>
        /// <param name="label"></param>
        /// <returns></returns>
        public static int DrawDropdown(string[] options, int selected, string label)
        {
            return EditorGUILayout.Popup(label, selected, options);
        }

        /// <summary>
        /// Draws a property field.
        /// </summary>
        /// <param name="property"></param>
        /// <param name="label"></param>
        /// <returns>True when property has changed.</returns>
        public static bool DrawPropertyField(SerializedProperty property, string label, bool applyModifiedProperties = true)
        {
            return DrawPropertyField(property, new GUIContent(label), applyModifiedProperties);
        }

        /// <summary>
        /// Draws a property field.
        /// </summary>
        /// <param name="property">Property to show.</param>
        /// <param name="label">Text to show.</param>
        /// <param name="applyModifiedProperties">Apply changes to property (default = true).</param>
        /// <returns>True when property has changed.</returns>
        public static bool DrawPropertyField(SerializedProperty property, GUIContent label, bool applyModifiedProperties = true)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(property, new GUIContent(label));
            if (EditorGUI.EndChangeCheck())
            {
                if (applyModifiedProperties) property.serializedObject.ApplyModifiedProperties();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Draws a space between to elements.
        /// </summary>
        public static void DrawSpace()
        {
            EditorGUILayout.Space();
        }
    }
}
