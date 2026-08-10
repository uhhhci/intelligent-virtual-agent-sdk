using UnityEngine;
using IVH.Core.Utils.Logging;

namespace IVH.Core.Utils.StaticHelper
{
    /// <summary>
    /// Convenience wrappers over <see cref="IVALogger"/>. New code should prefer
    /// <see cref="IVALogger"/> directly for leveled logging.
    /// </summary>
    public static class LoggingHelper
    {
        /// <summary>
        /// Logs a labelled <see cref="Vector3"/> at Info level so it respects the configured log level.
        /// </summary>
        public static void LogVector3(string label, Vector3 vector)
        {
            IVALogger.Info("Vector", $"{label}: ({vector.x}, {vector.y}, {vector.z})");
        }
    }
}
