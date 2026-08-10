using UnityEngine;

namespace IVH.Core.Utils.Logging
{
    /// <summary>
    /// ScriptableObject configuration for <see cref="IVALogger"/>.
    /// Place an asset at <c>Assets/Resources/IVALogConfig.asset</c> to customize logging at runtime.
    /// Env var <c>IVA_LOG_LEVEL</c> (Trace/Debug/Info/Warn/Error/Off) overrides this asset when present.
    /// </summary>
    [CreateAssetMenu(fileName = "IVALogConfig", menuName = "IVA SDK/Log Config", order = 100)]
    public class IVALogConfig : ScriptableObject
    {
        [Tooltip("Minimum severity emitted by IVALogger. Env var IVA_LOG_LEVEL overrides this.")]
        public LogLevel minLevel = LogLevel.Info;

        [Tooltip("Prefix every log line with the tag (e.g. [GeminiRealtime]). Helps filter console output.")]
        public bool includeTag = true;

        [Tooltip("Prefix every log line with the severity level (e.g. [Info]). Useful while debugging.")]
        public bool includeLevel = false;
    }
}
