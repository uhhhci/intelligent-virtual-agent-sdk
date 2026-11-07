using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace IVH.Core.IntelligentVirtualAgent.EditorScripts
{
    /// <summary>
    /// Refreshes every agent's cached list of playable animations when its scene is saved, so player builds (which
    /// cannot inspect animator controllers) offer the same animations as Play mode after a controller was edited.
    /// </summary>
    [InitializeOnLoad]
    internal static class PlayableAnimationCacheRefresher
    {
        static PlayableAnimationCacheRefresher()
        {
            EditorSceneManager.sceneSaving += OnSceneSaving;
        }

        private static void OnSceneSaving(Scene scene, string path)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var agent in root.GetComponentsInChildren<AgentBase>(true))
                    agent.RefreshPlayableAnimationCache();
        }
    }
}
