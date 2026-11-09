using System.Collections.Generic;
using IVH.Core.ServiceConnector.Gemini.Realtime;
using UnityEditor;
using UnityEngine;

namespace IVH.Core.ServiceConnector
{
    /// <summary>
    /// Draws <see cref="GeminiRealtimeWrapper.vertexLocation"/> as a dropdown of the Vertex locations that
    /// serve the wrapper's selected model, with "Default" (empty value) first and "Custom..." last for a
    /// location Google added after <see cref="VertexLiveLocations"/> was written.
    /// </summary>
    /// <remarks>
    /// A stored location the selected model is not listed for (typically after switching the model) stays
    /// as it is and is shown with a warning, since Google refuses such a session during setup.
    /// AI Studio models have one global endpoint, so the field is shown disabled for them.
    /// </remarks>
    [CustomPropertyDrawer(typeof(VertexLocationAttribute))]
    public class VertexLocationDrawer : PropertyDrawer
    {
        private const string ModelField = "selectedModel";

        // Fields switched to "Custom..." whose value is still empty or a listed location.
        private static readonly HashSet<(Object, string)> CustomMode = new HashSet<(Object, string)>();

        private static float Line => EditorGUIUtility.singleLineHeight;
        private static float Gap => EditorGUIUtility.standardVerticalSpacing;

        private struct State
        {
            public bool Plain;      // multi-object edit, or not on a wrapper: plain text field
            public bool AiStudio;
            public GeminiModelType Model;
            public IReadOnlyList<VertexLiveLocations.Location> Locations;
            public string Value;
            public bool Custom;
            public string Warning;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var s = Inspect(property);
            if (s.Plain) return EditorGUI.GetPropertyHeight(property, label, true);

            float height = Line;
            if (s.Custom) height += Gap + Line;
            if (s.Warning != null) height += Gap + WarningHeight(s.Warning);
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var s = Inspect(property);
            var row = new Rect(position.x, position.y, position.width, Line);

            if (s.Plain)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            label = EditorGUI.BeginProperty(position, label, property);

            if (s.AiStudio)
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUI.Popup(row, label, 0, new[] { new GUIContent("Not used: AI Studio has one global endpoint") });
                EditorGUI.EndProperty();
                return;
            }

            var options = new GUIContent[s.Locations.Count + 2];
            options[0] = new GUIContent($"Default: {Describe(s.Locations[0])}");
            for (int i = 0; i < s.Locations.Count; i++)
                options[i + 1] = new GUIContent(Describe(s.Locations[i]));
            int customIndex = options.Length - 1;
            options[customIndex] = new GUIContent("Custom...");

            int index = s.Custom ? customIndex : s.Value.Length == 0 ? 0 : 1 + IndexOf(s.Locations, s.Value);

            EditorGUI.BeginChangeCheck();
            int picked = EditorGUI.Popup(row, label, index, options);
            if (EditorGUI.EndChangeCheck() && picked != index)
            {
                var key = Key(property);
                if (picked == customIndex)
                {
                    CustomMode.Add(key);
                }
                else
                {
                    CustomMode.Remove(key);
                    property.stringValue = picked == 0 ? "" : s.Locations[picked - 1].Code;
                }
            }

            float y = row.yMax;
            if (s.Custom)
            {
                var customRow = new Rect(position.x, y + Gap, position.width, Line);
                var field = EditorGUI.PrefixLabel(customRow, GUIUtility.GetControlID(FocusType.Passive), new GUIContent(" "));
                EditorGUI.BeginChangeCheck();
                string typed = EditorGUI.DelayedTextField(field, property.stringValue);
                if (EditorGUI.EndChangeCheck())
                    property.stringValue = typed.Trim().ToLowerInvariant();
                y = customRow.yMax;
            }

            if (s.Warning != null)
            {
                var box = new Rect(position.x, y + Gap, position.width, WarningHeight(s.Warning));
                EditorGUI.HelpBox(EditorGUI.IndentedRect(box), s.Warning, MessageType.Warning);
            }

            EditorGUI.EndProperty();
        }

        private static State Inspect(SerializedProperty property)
        {
            var s = new State { Value = "" };
            var modelProperty = property.serializedObject.FindProperty(ModelField);
            if (property.propertyType != SerializedPropertyType.String || modelProperty == null
                || property.hasMultipleDifferentValues || modelProperty.hasMultipleDifferentValues)
            {
                s.Plain = true;
                return s;
            }

            s.Model = (GeminiModelType)modelProperty.intValue;
            s.Locations = VertexLiveLocations.For(s.Model);
            if (s.Locations.Count == 0)
            {
                s.AiStudio = true;
                return s;
            }

            s.Value = (property.stringValue ?? "").Trim().ToLowerInvariant();
            bool listed = s.Value.Length == 0 || VertexLiveLocations.IsListed(s.Model, s.Value);
            s.Custom = !listed || CustomMode.Contains(Key(property));
            if (!listed)
                s.Warning = $"'{s.Value}' is not a listed location for {ModelName(s.Model)}. Google will likely refuse " +
                            "the session (1008, model not found). Pick a location from the list unless Google has added this one.";
            return s;
        }

        private static (Object, string) Key(SerializedProperty property) =>
            (property.serializedObject.targetObject, property.propertyPath);

        private static string Describe(VertexLiveLocations.Location location) => $"{location.Code} ({location.Place})";

        private static string ModelName(GeminiModelType model) =>
            model == GeminiModelType.Live38VertexAI ? "Gemini 3.8 Live" : "Gemini 2.5 Live";

        private static int IndexOf(IReadOnlyList<VertexLiveLocations.Location> locations, string code)
        {
            for (int i = 0; i < locations.Count; i++)
                if (locations[i].Code == code) return i;
            return -1;
        }

        private static float WarningHeight(string message)
        {
            // Leave room for the warning icon and the inspector's margins.
            float width = Mathf.Max(120f, EditorGUIUtility.currentViewWidth - 90f);
            return Mathf.Max(Line * 2f + 4f, EditorStyles.helpBox.CalcHeight(new GUIContent(message), width) + 4f);
        }
    }
}
