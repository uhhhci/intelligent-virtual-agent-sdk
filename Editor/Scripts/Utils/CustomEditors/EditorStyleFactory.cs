using UnityEditor;
using UnityEngine;

namespace IVH.Core.Utils.Editor
{
    /// <summary>
    /// Definition of standard editor styles for different editor elements.
    /// </summary>
    public static class EditorStyleFactory
    {
        /// <summary>
        /// Defines editor style for foldouts.
        /// </summary>
        /// <returns>Returns GUIStyle for foldouts.</returns>
        public static GUIStyle GetFoldoutStyle()
        {
            GUIStyle style = EditorStyles.foldout;
            style.fontStyle = FontStyle.Bold;

            return style;
        }

        /// <summary>
        /// Defines editor style for headings.
        /// </summary>
        /// <returns>Returns GUIStyle for headings.</returns>
        public static GUIStyle GetHeadingStyle()
        {
            GUIStyle style = EditorStyles.label;
            style.fontStyle = FontStyle.Bold;

            return style;
        }
        
        /// <summary>
        /// Defines editor style for labels.
        /// </summary>
        /// <returns>Returns GUIStyle for labels.</returns>
        public static GUIStyle GetLabelStyle()
        {
            return EditorStyles.label;
        }

        /// <summary>
        /// Defines editor style for help text.
        /// </summary>
        /// <returns>Returns GUIStyle for help text.</returns>
        public static GUIStyle GetHelpText()
        {
            return GUI.skin.GetStyle("HelpBox");
        }
    }
}
