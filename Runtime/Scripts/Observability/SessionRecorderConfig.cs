using UnityEngine;

namespace IVH.Core.Observability
{
    /// <summary>
    /// Configuration for <see cref="SessionRecorder"/>. Create via
    /// <c>Assets → Create → IVA SDK → Session Recorder Config</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "SessionRecorderConfig", menuName = "IVA SDK/Session Recorder Config", order = 120)]
    public class SessionRecorderConfig : ScriptableObject
    {
        [Tooltip("If false, the recorder is inert (no files written, no event queue). Default off for v2.3.3 compat.")]
        public bool enabled = false;

        [Tooltip("Destination directory. Leave blank to use Application.persistentDataPath/IVA/sessions.")]
        public string outputDirectory = "";

        [Tooltip("Scrub transcript and tool-arg strings from the JSONL. Preserves metadata, latency, token counts.")]
        public bool scrubText = false;

        [Tooltip("Emit per-turn events. Off for lower overhead in production.")]
        public bool recordTurnEvents = true;

        [Tooltip("Emit VAD decisions (one event per audio chunk). Very noisy — on only for debugging.")]
        public bool recordVadDecisions = false;
    }
}
