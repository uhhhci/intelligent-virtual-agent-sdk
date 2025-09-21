using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IVH.Core.Utils.Patterns
{
    /// <summary>
    /// Implements a singleton for Unity. Child classes must not implement Awake(), Start() Update(), OnDestroy().
    /// Please use OnAwakeSingleton(), OnStartSingleton(), OnUpdateSingleton(), and OnDestroySingleton() instead.
    /// </summary>
    /// <typeparam name="T">Type of the singleton class that inherits from MonoBehaviour.</typeparam>
    public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static Lazy<T> _lazyInstance = new Lazy<T>(CreateSingleton);
        public static T Instance => _lazyInstance.Value;
        public static bool HasInstance;
        private static GameObject _ownerObject;

        /// <summary>
        /// Initializes the singleton instance with an existing gameObject or creates a new one. 
        /// If multiple gameObjects of this singleton's type already exist, the first one found will be 
        /// chosen and redundant objects will be destroyed in the singleton's Awake method.
        /// </summary>
        /// <returns>The singleton instance of type T.</returns>
        private static T CreateSingleton()
        {
            T instance;
            var existingInstances = FindObjectsOfType<T>();
            if (existingInstances.Length == 1)
            {
                instance = existingInstances[0];
            }
            else
            {
                var objectName = $"{typeof(T).Name}";

                _ownerObject = new GameObject(objectName);
                instance = _ownerObject.AddComponent<T>();
            }

            if (UnityEngine.Application.isPlaying) DontDestroyOnLoad(instance.gameObject);

            HasInstance = true;

            return instance;
        }

        /// <summary>
        /// Ensures only one instance of the singleton exists. This method should not be overridden by 
        /// child classes. Child classes should override OnAwake() instead.
        /// </summary>
        public virtual void Awake()
        {
            if (HasInstance)
            {
                if (!Instance.Equals(this))
                {
                    DestroyImmediate(gameObject);
                    return;
                }
            }

            OnAwakeSingleton();
        }

        /// <summary>
        /// Method to be implemented by child classes in place of Awake(). Called by the Awake() method.
        /// </summary>
        protected virtual void OnAwakeSingleton() { }

        /// <summary>
        /// Ensures that the Start method is not overridden by child classes. Child classes should override 
        /// OnStart() instead.
        /// </summary>
        public virtual void Start()
        {
            OnStartSingleton();
        }

        /// <summary>
        /// Method to be implemented by child classes in place of Start(). Called by the Start() method.
        /// </summary>
        protected virtual void OnStartSingleton() { }

        /// <summary>
        /// Ensures that the Update method is not overridden by child classes. Child classes should override 
        /// OnUpdate() instead.
        /// </summary>
        public virtual void Update()
        {
            OnUpdateSingleton();
        }

        /// <summary>
        /// Method to be implemented by child classes in place of Update(). Called by the Update() method.
        /// </summary>
        protected virtual void OnUpdateSingleton() { }

        /// <summary>
        /// Ensures proper cleanup of the singleton instance when it is destroyed. This method must not be
        /// overridden by child classes. Child classes should override OnDestroySingleton() instead.
        /// </summary>
        public virtual void OnDestroy()
        {
            if (HasInstance && Instance.Equals(this)) ResetLazyInstance();
            OnDestroySingleton();
        }

        /// <summary>
        /// Method to be implemented by child classes in place of OnDestroy(). Called by the OnDestroy() method.
        /// </summary>
        protected virtual void OnDestroySingleton() { }

        /// <summary>
        /// Ensures the singleton instance is created by accessing its value. Can be called to 
        /// ensure the singleton is initialized.
        /// </summary>
        /// <returns>Returns the singleton object.</returns>
        public T InitializeSingleton()
        { 
            Instance.GetInstanceID();
            return Instance;
        }

        /// <summary>
        /// Removes the singleton instance from `DontDestroyOnLoad` and ensures it is destroyed when the scene unloads.
        /// </summary>
        public void DestroyOnSceneUnLoad(bool destroy = true)
        {
            SceneManager.MoveGameObjectToScene(Instance.gameObject, SceneManager.GetActiveScene());

            switch (destroy)
            {
                case true:
                    SceneManager.sceneUnloaded += OnSceneManagerOnSceneUnloaded;
                    break;
                default:
                    SceneManager.sceneUnloaded -= OnSceneManagerOnSceneUnloaded;
                    break;
            }
        }

        /// <summary>
        /// Resets the singleton instance when a scene is unloaded.
        /// </summary>
        /// <param name="scene"></param>
        private void OnSceneManagerOnSceneUnloaded(Scene scene)
        {
            ResetLazyInstance();
        }

        /// <summary>
        /// Destroys the singleton instance and resets the singleton state.
        /// </summary>
        public static void DestroySingleton()
        {
            if (HasInstance) Destroy(Instance.gameObject);
            if (_ownerObject) Destroy(_ownerObject);
            ResetLazyInstance();
        }

        /// <summary>
        /// Resets the LazyInstance to ensure the singleton can be reinitialized if needed.
        /// </summary>
        private static void ResetLazyInstance()
        {
            HasInstance = false;
            _lazyInstance = new Lazy<T>(CreateSingleton);
        }
    }
}