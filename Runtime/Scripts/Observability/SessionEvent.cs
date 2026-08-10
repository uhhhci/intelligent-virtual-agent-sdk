using System;

namespace IVH.Core.Observability
{
    /// <summary>
    /// One row of the JSONL session log. Fields are serialized in a shape that's friendly to
    /// downstream analysis tools (jq, pandas, Grafana).
    /// </summary>
    [Serializable]
    public class SessionEvent
    {
        public string t;          // ISO-8601 UTC timestamp
        public string session;    // session id
        public string type;       // event kind
        public string speaker;    // "user" | "agent" | null
        public string reason;     // for interruption/disconnect
        public string name;       // for tool calls
        public string model;      // for setup / first_token
        public int? latency_ms;
        public int? tokens_in;
        public int? tokens_out;
        public int? bytes;
        public float? energy;
        public float? valence;
        public float? arousal;
        public string classified; // "speech" | "silence"
        public string text;       // short transcript fragment (may be scrubbed)
        public string args;       // JSON-encoded tool args (may be scrubbed)

        // v2.8.0 — additive group-conversation fields. All nullable; SessionRecorder never sets
        // them so existing single-agent JSONLs are unchanged. Group recorder populates them.
        public string participant_id;   // group participant id (replaces "agent" generic speaker)
        public string target_id;        // for cross-agent interruption: who was interrupted
        public string topic;            // session_start / topic_set
        public string pov;              // participant_joined: stored point-of-view fragment
        public string state;            // listening | speaking | interrupting | silent
        public string emotion;          // explicit emotion (separate from tool_call args)
        public string action;           // explicit body action
        public string gaze_target;      // explicit gaze target (user / participant id / none)
    }
}
