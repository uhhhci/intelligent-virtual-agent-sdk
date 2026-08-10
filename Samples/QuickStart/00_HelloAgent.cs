using UnityEngine;
using IVH.Core.IntelligentVirtualAgent;
using IVH.Core.ServiceConnector.Gemini.Realtime;

namespace IVH.Core.Samples.QuickStart
{
    /// <summary>
    /// The smallest possible voice-agent bootstrap. Attach this script to any empty GameObject
    /// (add an <see cref="AudioSource"/>) and hit Play — you'll hear Gemini greet you over your
    /// speakers once the API key in ~/.aiapi/auth.json is valid.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class HelloAgent : MonoBehaviour
    {
        [SerializeField] private string systemPrompt = "You are a friendly voice assistant. Greet the user briefly when the session starts.";
        [SerializeField] private string voiceName = "Puck";

        private GeminiVoiceOnlyAgent _agent;

        private void Awake()
        {
            // Add the required realtime wrapper + voice-only agent at runtime.
            if (!TryGetComponent(out GeminiRealtimeWrapper _)) gameObject.AddComponent<GeminiRealtimeWrapper>();
            _agent = GetComponent<GeminiVoiceOnlyAgent>() ?? gameObject.AddComponent<GeminiVoiceOnlyAgent>();
            _agent.systemInstruction = systemPrompt;
            _agent.voiceName = voiceName;
            _agent.autoConnectOnStart = true;
        }
    }
}
