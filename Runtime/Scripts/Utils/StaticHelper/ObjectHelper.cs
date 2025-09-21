using UnityEngine;

namespace IVH.Core.Utils.StaticHelper
{
    public static class ObjectHelper
    {
        // ReSharper disable Unity.PerformanceAnalysis
        /// <summary>
        /// Checks any class object if it is null and if a UnityEngine.Object has been assigned.
        /// </summary>
        /// <returns></returns>
        public static bool IsNull<T>(this T myObject, string message = "", bool showError = false) where T : class
        {
            switch (myObject)
            {
                case Object obj when !obj:
                    if (showError) Debug.LogError("The object is null. " + message);
                    return true;
                case null:
                    if (showError) Debug.LogError("The object is null. " + message);
                    return true;
                default:
                    return false;
            }
        }


        /// <summary>
        /// Checks any class object if it is null and if a UnityEngine.Object has been assigned.
        /// </summary>
        /// <returns>True if the object is not null</returns>
        public static bool IsNotNull<T>(this T myObject, string message = "", bool showError = false) where T : class
        {
            return !myObject.IsNull(message, showError);
        }

        /// <summary>
        /// Checks any class object if it is null and if a UnityEngine.Object has been assigned.
        /// </summary>
        /// <returns>True if the object is not null</returns>
        public static bool IsNotNull<T>(this T myObject) where T : class
        {
            return myObject.IsNotNull("", false);
        }
    }
}