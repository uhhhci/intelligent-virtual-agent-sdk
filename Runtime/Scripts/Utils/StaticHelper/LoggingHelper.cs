using UnityEngine;

namespace IVH.Core.Utils.StaticHelper
{
    /// <summary>
    /// Helper to improve logging.
    /// </summary>
    public static class LoggingHelper
    {
        /// <summary>
        /// Helper to provide accurate logging for a Vector3.
        /// </summary>
        public static void LogVector3(string label, Vector3 vector)
        {
            Debug.Log($"{label}: ({vector.x}, {vector.y}, {vector.z})");
        }
    }
}
