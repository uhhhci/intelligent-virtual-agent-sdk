using System.Reflection;
using UnityEngine;
using IVH.Core.Actions;
using IVH.Core.Utils.Logging;

namespace IVH.Core.IntelligentVirtualAgent.Presets
{
    /// <summary>
    /// ScriptableObject bundle of persona + behavior defaults. Drop onto <see cref="AgentBase.preset"/>
    /// to apply a pre-authored persona (e.g. tutor, therapist, museum guide) without manually filling
    /// the Inspector. Any fields left at their sentinel "unset" values are not applied to the target.
    /// </summary>
    /// <remarks>
    /// Opt-in: existing scenes with no preset assigned behave exactly as they did in v2.3.3.
    /// </remarks>
    [CreateAssetMenu(fileName = "AgentPreset", menuName = "IVA SDK/Agent Preset", order = 50)]
    public class AgentPreset : ScriptableObject
    {
        [Header("Persona (empty string = do not override)")]
        public string agentName = "";

        [Tooltip("0 = do not override")]
        public int age = 0;

        public bool overrideGender = false;
        public Gender gender = Gender.Nonbinary;

        public string occupation = "";

        public bool overrideLanguage = false;
        public AgentLanguage language = AgentLanguage.english;

        [TextArea(3, 6)] public string additionalDescription = "";

        [Header("Voice (Gemini Live only; empty = do not override)")]
        public string voiceName = "";

        [Header("System Prompt Override (empty = do not override)")]
        [TextArea(5, 15)] public string systemPromptOverride = "";

        /// <summary>
        /// Copies non-empty preset values onto the given agent. Uses reflection so the preset works
        /// with any <see cref="AgentBase"/> subclass that has matching field names.
        /// </summary>
        public void ApplyTo(AgentBase target)
        {
            if (target == null) return;
            var type = target.GetType();

            if (!string.IsNullOrEmpty(agentName)) SetField(type, target, nameof(agentName), agentName);
            if (age > 0) SetField(type, target, nameof(age), age);
            if (overrideGender) SetField(type, target, nameof(gender), gender);
            if (!string.IsNullOrEmpty(occupation)) SetField(type, target, nameof(occupation), occupation);
            if (overrideLanguage) SetField(type, target, nameof(language), language);
            if (!string.IsNullOrEmpty(additionalDescription)) SetField(type, target, nameof(additionalDescription), additionalDescription);

            // voiceName is optional — only exists on Gemini agents. SetField silently skips if absent.
            if (!string.IsNullOrEmpty(voiceName)) SetField(type, target, "voiceName", voiceName);

            if (!string.IsNullOrEmpty(systemPromptOverride)) target.setSystemPrompt(systemPromptOverride);

            IVALogger.Info("AgentPreset", $"Applied preset '{name}' to {target.name}.");
        }

        private static void SetField(System.Type type, object target, string fieldName, object value)
        {
            FieldInfo f = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f == null) return;
            try { f.SetValue(target, value); }
            catch (System.Exception ex) { IVALogger.Warn("AgentPreset", $"Could not set {fieldName}: {ex.Message}"); }
        }
    }
}
