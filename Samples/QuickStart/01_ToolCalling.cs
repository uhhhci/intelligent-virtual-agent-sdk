using UnityEngine;
using IVH.Core.IntelligentVirtualAgent;
using IVH.Core.IntelligentVirtualAgent.Tools;

namespace IVH.Core.Samples.QuickStart
{
    /// <summary>
    /// Shows how to register a dynamic tool Gemini can call during conversation. After starting,
    /// ask the agent "turn the room red" — Gemini will call <see cref="SetLightColor"/> on this
    /// component via the <see cref="GeminiToolManager"/> reflection path.
    /// </summary>
    public class ToolCallingSample : MonoBehaviour
    {
        [SerializeField] private Light targetLight;

        /// <summary>
        /// Called by Gemini. Bind this method to a <see cref="GeminiDynamicTool"/> entry on the
        /// <see cref="GeminiToolManager"/> component, with <c>toolName = "set_light_color"</c> and
        /// parameters <c>{ "r": number, "g": number, "b": number }</c>.
        /// </summary>
        public void SetLightColor(float r, float g, float b)
        {
            if (targetLight == null) return;
            targetLight.color = new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(b));
            Debug.Log($"[ToolCalling] Light set to ({r}, {g}, {b})");
        }

        /// <summary>Sets the light intensity. Bind as tool "set_light_intensity" with { "value": number }.</summary>
        public void SetLightIntensity(float value)
        {
            if (targetLight == null) return;
            targetLight.intensity = Mathf.Max(0f, value);
        }
    }
}
