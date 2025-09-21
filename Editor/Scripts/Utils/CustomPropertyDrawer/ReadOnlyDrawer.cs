using UnityEditor;
using UnityEngine;

namespace IVH.Core.Utils.Editor
{
    /// <summary>
    /// Attribute to make fields in the Unity Editor read-only. 
    /// Usage: [ReadOnly] public string a;
    /// </summary>

    [CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
    public class ReadOnlyDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property,
            GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            GUI.enabled = false;
            EditorGUI.PropertyField(position, property, label, true);
            GUI.enabled = true;
        }
    }
}