#if (UNITY_EDITOR)
using UnityEditor;
using UnityEngine;

namespace IVH.Core.Utils.Editor
{
    /// <summary>
    /// Provides different custom editors for serialized properties.
    /// </summary>
    public class PropertyEditors
    {
        /// <summary>
        /// Provides a slider element for serialized float properties.
        /// </summary>
        /// <param name="property"></param>
        /// <param name="leftValue"></param>
        /// <param name="rightValue"></param>
        /// <param name="label"></param>
        public static void FloatPropertySlider(SerializedProperty property, float leftValue, float rightValue, GUIContent label)
        {
            EditorGUI.BeginChangeCheck();
            var newValue = EditorGUILayout.Slider(label, property.floatValue, leftValue, rightValue);
            if (EditorGUI.EndChangeCheck()) property.floatValue = newValue;
        }
    }
}
#endif
