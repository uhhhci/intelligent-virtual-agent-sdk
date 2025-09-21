using UnityEditor;

namespace IVH.Core.Utils.Editor
{
    /// <summary>
    /// Contains different static editor helper.
    /// </summary>
    public static class EditorHelper
    {
        /// <summary>
        /// Finds the SerializedProperty for an automatically created property.
        /// </summary>
        /// <param name="obj">SerializedObject to work on.</param>
        /// <param name="propName">Name of the property to find.</param>
        /// <returns>Returns the found SerializedProperty or null.</returns>
        public static SerializedProperty FindPropertyByAutoPropertyName(this SerializedObject obj, string propName)
        {
            return obj.FindProperty(string.Format("<{0}>k__BackingField", propName));
        }
    }
}
