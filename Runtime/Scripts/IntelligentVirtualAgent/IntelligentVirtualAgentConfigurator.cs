using IVH.Core.Utils.StaticHelper;
using UnityEngine;
using System.Collections.Generic;
using uLipSync;

namespace IVH.Core.IntelligentVirtualAgent
{
    
    public class IntelligentVirtualAgentConfigurator : MonoBehaviour
    {
        // Start is called before the first frame update
        public GameObject agentPrefab;

        // Public field to assign the Animator Controller
        public RuntimeAnimatorController animatorController;

        // Reference to the instantiated virtual agent
        [SerializeField, HideInInspector] private GameObject agentInstance;

        private SimpleChatBehaviour simpleChatBehavior;

        [Tooltip("uLipSync Profile asset.")]
        public Profile lipSyncProfile;
        [Tooltip("Phoneme → blendshape mappings. The SkinnedMeshRenderer is assigned automatically at runtime.")]
        public List<uLipSyncBlendShape.BlendShapeInfo> lipSyncBlendShapes = new();

        public void SetupVirtualAgent()
        {
            if (agentPrefab != null && agentInstance == null)
            {
                // Instantiate the agent prefab at the position and rotation of the AGENT
                agentInstance = Instantiate(agentPrefab, transform.position, transform.rotation);
                agentInstance.name = "Agent";
                // Set the agentInstance's parent to the AGENT
                agentInstance.transform.SetParent(transform);

                // Assign the Animator controller
                AssignAnimatorController();


                // Setup Oculus LipSync
                SetupLipSync();

                //Assign audio source from text to speech to the agent
                SetupAudio();

                // Setup EmotionHandler
                SetupEMotionHandler();
            }
            else
            {
                Debug.LogWarning("Agent prefab is not assigned or agent is already set up.");
            }
        }

        private void SetupAudio()
        {
            // Get the SimpleChatBehavior component from this GameObject
            simpleChatBehavior = GetComponent<SimpleChatBehaviour>();

            // Ensure we have a reference
            if (simpleChatBehavior != null)
            {
                simpleChatBehavior.agentAudioSource = agentInstance.GetComponent<AudioSource>();
            }
            else
            {
                Debug.LogError("simpleChatBehavior GameObject not assigned!");
            }
        }


        // Assign the animator controller to the agent
        private void AssignAnimatorController()
        {
            // Check if the agentInstance has an Animator component
            Animator animator = agentInstance.GetComponent<Animator>();
            if (animator != null && animatorController != null)
            {
                animator.runtimeAnimatorController = animatorController;
            }
            else
            {
                Debug.LogWarning("No Animator found on the agent instance or Animator Controller is not assigned.");
            }
        }

        private void SetupLipSync()
        {
            Debug.Log("Setting up LipSync");
            if (agentInstance == null)
            {
                Debug.LogWarning("IntelligentVirtualAgentConfigurator.SetupLipSync: agentInstance is null.");
                return;
            }

            var lipSync = agentInstance.AddComponent<uLipSync.uLipSync>();
            if (lipSyncProfile != null)
                lipSync.profile = lipSyncProfile;

            var blendShapeComp = agentInstance.AddComponent<uLipSyncBlendShape>();

            var smr = FindFaceSkinnedMeshRenderer(agentInstance);
            if (smr == null)
                Debug.LogWarning("IntelligentVirtualAgentConfigurator.SetupLipSync: no SkinnedMeshRenderer with blend shapes found on the agent.");
            blendShapeComp.skinnedMeshRenderer = smr;
            Debug.LogError(smr.sharedMesh.blendShapeCount);

            foreach (var blendShape in lipSyncBlendShapes)
            {
                blendShapeComp.blendShapes.Add(blendShape);
            }

            Debug.Log("Added blend shapes: " + blendShapeComp.blendShapes.Count);

#if UNITY_EDITOR
            UnityEditor.Events.UnityEventTools.AddPersistentListener(
                lipSync.onLipSyncUpdate, blendShapeComp.OnLipSyncUpdate);
#else
            lipSync.onLipSyncUpdate.AddListener(blendShapeComp.OnLipSyncUpdate);
#endif
        }

        private static SkinnedMeshRenderer FindFaceSkinnedMeshRenderer(GameObject root)
        {
            SkinnedMeshRenderer best = null;
            int bestCount = 0;
            foreach (var s in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = s.sharedMesh;
                if (mesh == null) continue;
                if (mesh.blendShapeCount > bestCount)
                {
                    best = s;
                    bestCount = mesh.blendShapeCount;
                }
            }
            return best;
        }

        // Function to attach the EmotionHandler script
        private void SetupEMotionHandler()
        {
            // check if the agentinstance is not null
            if (agentInstance != null)
            {
                // Attach EmotionHandler script to the agent instance
                EmotionHandler emotionHandler = agentInstance.AddComponent<EmotionHandler>();

                emotionHandler.skinnedMeshRenderer = agentInstance.transform.GetChild(1).GetComponent<SkinnedMeshRenderer>(); //Assign the skinned Mesh Renderer of the Body 
            }
            else
            {
                Debug.LogWarning("agent instance is null; cannot attach EmotionHandler script.");
            }
        }

        public void DestroyVirtualAgent()
        {
            if (agentInstance.IsNotNull())
            {
                // Destroy the agent instance
                DestroyImmediate(agentInstance);
                agentInstance = null;
            }
            else
            {
                Debug.LogWarning("No agent instance to clear.");
            }
        }
    }
}