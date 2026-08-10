using System;
using UnityEngine;

namespace IVH.Core.Utils.Logging
{
    /// <summary>
    /// Static, leveled logger for the IVA SDK. Wraps <see cref="UnityEngine.Debug"/> so existing
    /// console behavior is preserved, but adds severity filtering so users and CI can quiet output.
    /// </summary>
    /// <remarks>
    /// Resolution order for the effective level:
    /// <list type="number">
    ///   <item>Env var <c>IVA_LOG_LEVEL</c> (case-insensitive) — highest priority, useful for CI.</item>
    ///   <item>Runtime override via <see cref="SetMinLevel"/>.</item>
    ///   <item><see cref="IVALogConfig"/> asset loaded from <c>Resources/IVALogConfig</c>.</item>
    ///   <item>Built-in default: <see cref="LogLevel.Info"/>.</item>
    /// </list>
    /// Calls below the effective level are fully no-op'd (no string formatting cost).
    /// </remarks>
    public static class IVALogger
    {
        private const string DefaultResourcePath = "IVALogConfig";
        private const string EnvVarName = "IVA_LOG_LEVEL";

        private static IVALogConfig _config;
        private static LogLevel? _runtimeOverride;
        private static bool _envChecked;
        private static LogLevel? _envLevel;

        /// <summary>
        /// Currently effective minimum level. Computed each access to stay in sync with env/config changes.
        /// </summary>
        public static LogLevel EffectiveMinLevel
        {
            get
            {
                if (!_envChecked)
                {
                    _envChecked = true;
                    try
                    {
                        string env = Environment.GetEnvironmentVariable(EnvVarName);
                        if (!string.IsNullOrEmpty(env) && Enum.TryParse(env, true, out LogLevel parsed))
                        {
                            _envLevel = parsed;
                        }
                    }
                    catch
                    {
                        // Some platforms (WebGL) throw on env var access — silently fall through.
                    }
                }

                if (_envLevel.HasValue) return _envLevel.Value;
                if (_runtimeOverride.HasValue) return _runtimeOverride.Value;

                var cfg = Config;
                return cfg != null ? cfg.minLevel : LogLevel.Info;
            }
        }

        private static IVALogConfig Config
        {
            get
            {
                if (_config != null) return _config;
                try
                {
                    _config = Resources.Load<IVALogConfig>(DefaultResourcePath);
                }
                catch
                {
                    _config = null;
                }
                return _config;
            }
        }

        /// <summary>
        /// Override the configured level for this session. Env var still takes priority if set.
        /// Pass <c>null</c> to clear the override and fall back to config/default.
        /// </summary>
        public static void SetMinLevel(LogLevel? level) => _runtimeOverride = level;

        /// <summary>
        /// Replace the active config (useful for tests). Pass <c>null</c> to re-read from Resources.
        /// </summary>
        public static void SetConfig(IVALogConfig config)
        {
            _config = config;
            _envChecked = false;
            _envLevel = null;
        }

        public static bool IsEnabled(LogLevel level) => level >= EffectiveMinLevel;

        public static void Trace(string tag, string message, string colorTag = null)
        {
            if (LogLevel.Trace < EffectiveMinLevel) return;
            Emit(LogLevel.Trace, tag, message, colorTag, null);
        }

        public static void Debug(string tag, string message, string colorTag = null)
        {
            if (LogLevel.Debug < EffectiveMinLevel) return;
            Emit(LogLevel.Debug, tag, message, colorTag, null);
        }

        public static void Info(string tag, string message, string colorTag = null)
        {
            if (LogLevel.Info < EffectiveMinLevel) return;
            Emit(LogLevel.Info, tag, message, colorTag, null);
        }

        public static void Warn(string tag, string message, string colorTag = null)
        {
            if (LogLevel.Warn < EffectiveMinLevel) return;
            Emit(LogLevel.Warn, tag, message, colorTag, null);
        }

        public static void Error(string tag, string message, Exception exception = null, string colorTag = null)
        {
            if (LogLevel.Error < EffectiveMinLevel) return;
            Emit(LogLevel.Error, tag, message, colorTag, exception);
        }

        private static void Emit(LogLevel level, string tag, string message, string colorTag, Exception exception)
        {
            var cfg = Config;
            bool includeTag = cfg == null || cfg.includeTag;
            bool includeLevel = cfg != null && cfg.includeLevel;

            string prefix = "";
            if (includeLevel) prefix += $"[{level}] ";
            if (includeTag && !string.IsNullOrEmpty(tag)) prefix += $"[{tag}] ";

            string body = string.IsNullOrEmpty(colorTag)
                ? $"{prefix}{message}"
                : $"<color={colorTag}>{prefix}</color>{message}";

            if (exception != null) body += $"\n{exception}";

            switch (level)
            {
                case LogLevel.Error:
                    UnityEngine.Debug.LogError(body);
                    break;
                case LogLevel.Warn:
                    UnityEngine.Debug.LogWarning(body);
                    break;
                default:
                    UnityEngine.Debug.Log(body);
                    break;
            }
        }
    }
}
