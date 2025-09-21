using UnityEngine;

namespace IVH.Core.Utils.StaticHelper
{
    public static class GameObjectHelper
    {
        /// <summary>
        /// Adds a child game object to a game object.
        /// </summary>
        /// <returns></returns>
        public static GameObject AddChildGameObject(this GameObject myObject, string name, bool worldPositionStays = false)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(myObject.transform,worldPositionStays);
            
            return go;
        }
        
        /// <summary>
        /// Finds a child games object by name.
        /// </summary>
        /// <param name="myObject">The given game object.</param>
        /// <param name="childName">The name of the child to be found.</param>
        /// <param name="recursive">Enables recursive search.</param>
        /// <returns></returns>
        public static GameObject FindChildGameObject(this GameObject myObject, string childName, bool recursive = false)
        {
            // Try to find the child directly under the parent
            Transform childTransform = myObject.transform.Find(childName);
            if (childTransform != null)
            {
                return childTransform.gameObject;
            }

            // Recursively search through children
            foreach (Transform child in myObject.transform)
            {
                GameObject found = child.gameObject.FindChildGameObject(childName);
                if (found != null)
                {
                    return found;
                }
            }

            // Return null if no child with the specified name is found
            return null;
        }
        
    }
}