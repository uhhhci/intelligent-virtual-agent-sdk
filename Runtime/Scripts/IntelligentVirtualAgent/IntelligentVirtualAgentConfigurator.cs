using IVH.Core.Utils.StaticHelper;
using UnityEngine;


namespace IVH.Core.IntelligentVirtualAgent
{
    public class IntelligentVirtualAgentConfigurator : MonoBehaviour
    {
        // Start is called before the first frame update
        public GameObject agentPrefab;

        // Public field to assign the Animator Controller
        public RuntimeAnimatorController animatorController;

        // Reference to the instantiated virtual agent
        [SerializeField,HideInInspector] private GameObject agentInstance;

        private SimpleChatBehaviour simpleChatBehavior;

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

        // Function to attach the OVRLipSyncContextMorphTarget script
        private void SetupLipSync()
        {
            // check if the agentinstance is not null
            if (agentInstance != null)
            {
                // Attach Oculus LipSync scripts to the agent instance
                OVRLipSync ovrLipSync = agentInstance.AddComponent<OVRLipSync>();
                
                OVRLipSyncContext ovrLipSyncContext = agentInstance.AddComponent<OVRLipSyncContext>();
                ovrLipSyncContext.audioLoopback = true;
                ovrLipSyncContext.audioSource= agentInstance.GetComponent<AudioSource>();


                OVRLipSyncContextMorphTarget ovrLipSyncContextMorphTarget = agentInstance.AddComponent<OVRLipSyncContextMorphTarget>();
                ovrLipSyncContextMorphTarget.skinnedMeshRenderer= agentInstance.transform.Find("Body").GetComponent<SkinnedMeshRenderer>();//Assign the skinned Mesh Renderer of the Body 
                for (int i = 0; i < 15; ovrLipSyncContextMorphTarget.visemeToBlendTargets[i] = 52 + i++) ;//Assign Didimo character viseme blendshapes for oculus lipsync that range from body_blendshapes.shp_52_sil,..., body_blendshapes.shp_66_U and carry their indices inside their names: i.e., 52-66 to the morph target viseme to blend targets

            }
            else
            {
                Debug.LogWarning("agent instance is null; cannot attach ovrlipsyn scripts.");
            }
        }

        // Function to attach the EmotionHandler script
        private void SetupEMotionHandler()
        {
            // check if the agentinstance is not null
            if (agentInstance != null)
            {
                // Attach EmotionHandler script to the agent instance
                EmotionHandler emotionHandler = agentInstance.AddComponent<EmotionHandler>();

                emotionHandler.skinnedMeshRenderer= agentInstance.transform.GetChild(1).GetComponent<SkinnedMeshRenderer>();//Assign the skinned Mesh Renderer of the Body 
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

