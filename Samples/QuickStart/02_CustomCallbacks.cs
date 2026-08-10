using UnityEngine;
using IVH.Core.IntelligentVirtualAgent;
using IVH.Core.ServiceConnector.Gemini.Realtime;
using IVH.Core.Exceptions;

namespace IVH.Core.Samples.QuickStart
{
    /// <summary>
    /// Demonstrates the full set of lifecycle callbacks exposed by <see cref="GeminiRealtimeWrapper"/>.
    /// Attach to the same GameObject as a Gemini agent to get a live trace of connection state,
    /// incoming transcript, and reconnect attempts.
    /// </summary>
    [RequireComponent(typeof(GeminiRealtimeWrapper))]
    public class CustomCallbacksSample : MonoBehaviour
    {
        private GeminiRealtimeWrapper _wrapper;

        private void Awake()
        {
            _wrapper = GetComponent<GeminiRealtimeWrapper>();

            _wrapper.OnConnected += () => Debug.Log("[Callbacks] Session ready.");
            _wrapper.OnDisconnected += reason => Debug.Log($"[Callbacks] Session closed: {reason}");
            _wrapper.OnReconnecting += attempt => Debug.Log($"[Callbacks] Reconnect attempt #{attempt}");
            _wrapper.OnFatalError += (IVAException ex) => Debug.LogError($"[Callbacks] Fatal: {ex.Message}");

            _wrapper.OnTextReceived += text => Debug.Log($"[Callbacks] Gemini says: {text}");
            _wrapper.OnAudioReceived += audio => Debug.Log($"[Callbacks] {audio.Length} bytes of audio");
            _wrapper.OnCommandReceived += (action, emotion, gaze) =>
                Debug.Log($"[Callbacks] Avatar state → action={action}, emotion={emotion}, gaze={gaze}");
        }
    }
}
