using System;
using System.Collections;
using UnityEngine;
using UnityEditor;
using IVH.Core.ServiceConnector;
using IVH.Core.Actions;
using Newtonsoft.Json;
using IVH.Core.Utils.StaticHelper;
using UnityEngine.UI;
using System.Runtime.Versioning;
using System.ComponentModel.Design.Serialization;
using System.Threading.Tasks;

namespace IVH.Core.IntelligentVirtualAgent
{
    // similar to basic social interactions
    public abstract class AgentBase : MonoBehaviour
    {
        [Header("General Agent Attributes")]
        public GameObject agentPrefab;

        [Tooltip("Choose your cloud service manager prefab, which can be found in the IVA-SDK-Core>Runtime>Prefab folder. ")]
        public GameObject cloudServiceManagerPrefab;
        public EmotionHandlerType emotionHandlerType;
        public CharacterType characterType = CharacterType.CC4OrDIDIMO;
        public BodyAnimationControllerType bodyAnimationControllerType = BodyAnimationControllerType.Rocketbox;
        public RuntimeAnimatorController animatorController;
        
        [Header("Agent Demographics")]
        //public MBTI personality; 
        public string agentName = "";
        public int age = 30;
        public Gender gender = Gender.Nonbinary;
        public string occupation = "";
        //public MBTI personality;
        public AgentLanguage language;
        public string additionalDescription="";
        protected float agentHeight;

        [Header("Cloud Service Settings")]
        // Connect to Services
        protected CloudServiceManager cloudServiceManager;
        public VoiceService TTSService;
        public FoundationModels foundationModel;
        public VoiceRecognitionService STTService;
        protected string systemPrompt = "";

        [Header("Agent Vision Settings")]
        [HideInInspector] public bool vision = false;
        [HideInInspector] public TargetCameraType targetCameraType = TargetCameraType.AgentCamera;
        [HideInInspector] public ImageTriggerMode imageTriggerMode = ImageTriggerMode.Auto;
        [HideInInspector] public ImageResolution resolution = ImageResolution.VGA;
        [HideInInspector] public string selectedWebCamName = "";
        protected string triggerPhrase = "what are you seeing";
        protected WebCamTexture webCamTexture;
        [HideInInspector] public RawImage rawImage;  // Drag a RawImage UI element here to display the webcam feed

        // vision module
        [HideInInspector] public Camera agentVisionCamera;
        protected byte[] egoImageData;
        protected byte[] egoDepthData;
        protected byte[] webCamImageData;

        //[Header("Physics Based Animations")]
        //[HideInInspector] public bool physicsBasedAnimation = false;
        // add components and settings for the animation package

        [Header("Action Recognition")]
        [HideInInspector] public bool actionRecognition = false;
        // add some settings here when integrating JRS package

        // Service Selection
        [Header("Agent Non Verbal Cues")]
        [Tooltip("Optimize your token consumption based on the description mode of actions avaliable for IVAs. In Simple mode, only name of the actions are sent. In Detail mode, whole description is sent, including few shot learning samples. ")]
        public ToolDescriptionMode descriptionMode;
        public BodyActionFilter bodyActionFilter;
        public FacialExpressionFilter facialExpressionFilter;
        [HideInInspector] public AudioSource agentAudioSource;
        [HideInInspector] public AgentBodyMotionController actionController;
        [HideInInspector] public AgentFacialExpressionAnimator faceAnimator;
        [HideInInspector] public EmotionHandler emotionHandler;
        [HideInInspector] public EyeGazeController eyeGazeController;

        [Header("STT Trigger Settings")]
        [Tooltip("Choose automatic, then the IVA will respond to any STT input. Choose Triggerphase, then the IVA will only respond when hearing trigger phrases such as: hello AI, hey AI, etc. ")]
        public AIWakeupMode wakeupMode = AIWakeupMode.Automatic;

        [Tooltip("Trigger phrase has to be all lower cases")]
        public string[] triggerPhrases = { "hey ai", "hello ai", "hi ai" }; // Customize your trigger phrases here
        protected bool triggeredOnce = false;

        // Locomotion
        //[Header("Locomotion")]
        [HideInInspector] public CharacterController characterController;
        private Animator animator;

        [SerializeField, HideInInspector] protected GameObject agentInstance;
        [SerializeField, HideInInspector] public GameObject cloudServiceManagerInstance;

        [HideInInspector] public Transform player;

        // UI indicator
        [HideInInspector] public GameObject ListeningIndicator;
        [HideInInspector] public GameObject ThinkingIndicator;
        
        [HideInInspector][Header("Instant Actor")]
        [Tooltip("This text will mostly be used by the 'QuickSpeech' function for an LLM to quickly create a TTS response without going into the interaction loop. ")]
        public string SimpleText = "";

        protected virtual void Awake()
        {
            if (cloudServiceManagerInstance != null)
            {
                cloudServiceManager = cloudServiceManagerInstance.GetComponent<CloudServiceManager>();
            }
            else
            { Debug.LogWarning("Cloud service manager instance cloudn't be found! IVA won't work correct unless you are using the gemini live agent.. "); }

            systemPrompt = createSystemPrompt();

            //Debug.Log(systemPrompt);
            animator = agentInstance.GetComponent<Animator>();

            if (animator == null)
            {
                animator = agentInstance.GetComponentInChildren<Animator>();
            }

            if (animator == null)
            {
                Debug.LogError("Animator component is missing on the humanoid avatar.");
            }

            else
            {
                animator.applyRootMotion = true;
            }

            if (vision && targetCameraType == TargetCameraType.WebCam)
            {
                if (vision && targetCameraType == TargetCameraType.WebCam)
                {
                    // --- MODIFIED: Webcam Initialization ---
                    if (!string.IsNullOrEmpty(selectedWebCamName))
                    {
                        // Initialize specific camera requested by Editor
                        webCamTexture = new WebCamTexture(selectedWebCamName);
                    }
                    else
                    {
                        // Fallback to default
                        webCamTexture = new WebCamTexture();
                    }

                    webCamTexture.Play(); 
                    
                    if (rawImage != null)
                    {
                        rawImage.texture = webCamTexture;
                        rawImage.material.mainTexture = webCamTexture;
                    }
                }
                // // Find and start the webcam
                // webCamTexture = new WebCamTexture();
                // webCamTexture.Play();  // Start the webcam
                // if (rawImage != null)
                // {

                //     rawImage.texture = webCamTexture;
                //     rawImage.material.mainTexture = webCamTexture;

                // }
            }

        }
        void Start()
        {
            FindPlayer();
            eyeGazeController.playerTarget = player;       
        }
        public Animator getAnimator()
        {
            return animator;
        }
        #region agent features
        public virtual IEnumerator Speak(string text) { yield return cloudServiceManager.TTS(text, agentAudioSource, TTSService); }

        public virtual async Task Listen()
        {
            // Await the result directly from the async STT method.
            string result = await cloudServiceManager.STT(language, STTService);

            // The code will pause here without blocking the game until a result is returned.
            if (!string.IsNullOrEmpty(result))
            {
                Debug.Log("Detected speech results: " + result);
            }
            else
            {
                Debug.LogWarning("STT returned no result.");
            }
        }

        public void ExpressEmotion(string emotion)
            {
                if (emotionHandlerType == EmotionHandlerType.FACS)
                {
                    Debug.Log("expressing emotion: " + emotion);
                    emotionHandler.HandleEmotion(emotion, 0.5f, "during");
                }
                if (emotionHandlerType == EmotionHandlerType.CC4_Animation)
                {
                    faceAnimator.TriggerActionViaActionName(emotion);
                }
            }

        public void PerformAction(string action) { actionController.TriggerActionViaActionName(action); }

        public virtual string createSystemPrompt()
        {
            string bodyLanguageTools = "";
            string facialExpressionTools = "";
            string facsFacialExpressionTools = "";

            if (descriptionMode == ToolDescriptionMode.SIMPLE)
            {
                bodyLanguageTools = JsonConvert.SerializeObject(actionController.GetSimpleActionNameFiltered(bodyActionFilter, gender, bodyAnimationControllerType).ToArray());
                facialExpressionTools = JsonConvert.SerializeObject(faceAnimator.GetSimpleFacialExpressionNameFiltered(facialExpressionFilter).ToArray());
                facsFacialExpressionTools = JsonConvert.SerializeObject(faceAnimator.GetSimpleDidimoActionName().ToArray());
            }

            else if (descriptionMode == ToolDescriptionMode.DETAIL)
            {
                bodyLanguageTools = JsonConvert.SerializeObject(actionController.GetDetailActionsFiltered(bodyActionFilter, gender, bodyAnimationControllerType).ToArray());
                facialExpressionTools = JsonConvert.SerializeObject(faceAnimator.GetDetailFacialExpressionFiltered(facialExpressionFilter).ToArray());
                facsFacialExpressionTools = JsonConvert.SerializeObject(faceAnimator.GetEnabledDidimoActionsAsGPTToolItems().ToArray());
            }
            else { }

            string selectedFacialExpressionTools;
            switch (emotionHandlerType)
            {
                case EmotionHandlerType.CC4_Animation:
                    selectedFacialExpressionTools = facialExpressionTools;
                    break;
                case EmotionHandlerType.FACS:
                    selectedFacialExpressionTools = facsFacialExpressionTools;
                    break;
                default:
                    Debug.LogWarning($"Unexpected emotion handler type: {emotionHandlerType}");
                    selectedFacialExpressionTools = facialExpressionTools; // Fallback value
                    break;
            }

            return $"Your name is:{agentName}. " +
                $"You are {age} years old. " +
                $"Your gender is {gender}. " +
                $"Your occupation is {occupation}." +
                "answer questions in first person persona like: I think... I am seeing..." +
                "Choose the follwing approporiate body language, facial expression, and gaze behavior considering the entire conversation history" +
                "Possible body language animations:" +
                 bodyLanguageTools +
                "Possible facial expression animations:" +
                selectedFacialExpressionTools +
                "Possible gaze behavior: " +
                "(LookAtUser, LookIdly)." +
                "Return your message structurally exactly with the following template: 'message: your response ||| body action: function name ||| face: function name ||| gaze:function name'" +
                "If no actions needed, then return: 'message:your response ||| body action:none ||| face:none ||| gaze:none' " +
                "Only choose one body language and one facial expression in one response" +
                "Please be very short in your answer, in 1-2 sentences. " +
                $"Additional information about you: {additionalDescription}.";
        }
        #endregion
        #region agent locomotion
        public void WalkTowards(Transform target, float stopDistance, float speed)
        {
            Vector3 targetPosition = target.position + target.forward * -stopDistance; // Move in front of target
            StartCoroutine(MoveToPosition(targetPosition, speed));
        }

        private IEnumerator MoveToPosition(Vector3 targetPosition, float speed)
        {
            if (animator != null)
            {
                animator.SetBool("isWalking", true);

                while (Vector3.Distance(transform.position, targetPosition) > 0.1f)
                {
                    Vector3 direction = (targetPosition - transform.position).normalized;
                    characterController.Move(direction * speed * Time.deltaTime);
                    transform.LookAt(new Vector3(targetPosition.x, transform.position.y, targetPosition.z)); // Keep rotation level

                    yield return null;
                }

                animator.SetBool("isWalking", false);
            }
            else
            {
                Debug.LogWarning("no animator found!");
                yield return null;
            }
        }

        #endregion

        #region agent vision

        protected void CaptureEgocentricImage(Vector2Int res)
        {
            // Take a snapshot of an image from egocentric agent's perspective
            //targetCamera.fieldOfView = FoV;
            RenderTexture renderTexture = new RenderTexture(res.x, res.y, 24);
            agentVisionCamera.targetTexture = renderTexture;
            agentVisionCamera.Render();

            // Read pixels from the RenderTexture
            RenderTexture.active = renderTexture;
            Texture2D texture2D = new Texture2D(res.x, res.y, TextureFormat.RGB24, false);
            texture2D.ReadPixels(new Rect(0, 0, res.x, res.y), 0, 0);
            texture2D.Apply();

            // Cleanup
            agentVisionCamera.targetTexture = null;
            RenderTexture.active = null;
            Destroy(renderTexture);

            // Encode the texture to PNG
            egoImageData = texture2D.EncodeToPNG();
            Destroy(texture2D);
        }
        protected IEnumerator CaptureWebcamImage()
        {
            if (webCamTexture == null || !webCamTexture.isPlaying) yield break;

            yield return new WaitForEndOfFrame();

            // 1. Calculate dimensions (Max 512px width to save tokens/bandwidth)
            float aspect = (float)webCamTexture.width / webCamTexture.height;
            int targetWidth = 512; 
            int targetHeight = Mathf.RoundToInt(targetWidth / aspect);

            // 2. Use a Temporary RenderTexture for GPU-based resizing (Fast!)
            RenderTexture rt = RenderTexture.GetTemporary(targetWidth, targetHeight, 0);
            
            // 3. Blit copies and resizes the webcam texture into the small RenderTexture
            Graphics.Blit(webCamTexture, rt);

            // 4. Read the pixels from the RenderTexture into a Texture2D
            RenderTexture.active = rt;
            Texture2D resultTex = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false);
            resultTex.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
            resultTex.Apply();

            // 5. Cleanup GPU memory immediately
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            // 6. Encode to JPG (Quality 50 is sufficient for AI vision)
            // This creates the final byte[] array ready for Gemini
            webCamImageData = resultTex.EncodeToJPG(50); 

            // 7. Cleanup CPU memory
            Destroy(resultTex);
        }
        // protected IEnumerator CaptureWebcamImage()
        // {
        //     // Wait for the end of the frame to capture
        //     yield return new WaitForEndOfFrame();

        //     // Original webcam texture dimensions
        //     int originalWidth = webCamTexture.width;
        //     int originalHeight = webCamTexture.height;

        //     // Reduced resolution (30% of the original)
        //     int reducedWidth = Mathf.RoundToInt(originalWidth * 0.3f);
        //     int reducedHeight = Mathf.RoundToInt(originalHeight * 0.3f);

        //     // Create a RenderTexture for downscaling
        //     RenderTexture renderTexture = new RenderTexture(reducedWidth, reducedHeight, 24);
        //     // Copy the webcam texture to the RenderTexture
        //     Graphics.Blit(webCamTexture, renderTexture);

        //     // Set the RenderTexture as active
        //     RenderTexture.active = renderTexture;

        //     // Create a Texture2D to read the downscaled RenderTexture
        //     Texture2D reducedPhoto = new Texture2D(reducedWidth, reducedHeight, TextureFormat.RGB24, false);
        //     reducedPhoto.ReadPixels(new Rect(0, 0, reducedWidth, reducedHeight), 0, 0);
        //     reducedPhoto.Apply();

        //     // Encode the texture to PNG
        //     webCamImageData = reducedPhoto.EncodeToPNG();

        //     // Cleanup
        //     RenderTexture.active = null;
        //     renderTexture.Release();
        //     Destroy(renderTexture);
        //     Destroy(reducedPhoto);
        // }


        protected void CaptureDepth(Vector2Int res)
        {
            // Create a RenderTexture for depth capture
            RenderTexture renderTexture = new RenderTexture(res.x, res.y, 24, RenderTextureFormat.Depth);
            agentVisionCamera.targetTexture = renderTexture;

            // Set the camera to render with a depth shader
            agentVisionCamera.RenderWithShader(Shader.Find("Hidden/Internal-DepthNormalsTexture"), null);

            // Read pixels from the RenderTexture
            RenderTexture.active = renderTexture;
            Texture2D depthTexture = new Texture2D(res.x, res.y, TextureFormat.RFloat, false);
            depthTexture.ReadPixels(new Rect(0, 0, res.x, res.y), 0, 0);
            depthTexture.Apply();

            // Cleanup
            agentVisionCamera.targetTexture = null;
            RenderTexture.active = null;
            Destroy(renderTexture);

            // Encode the depth texture to a format if needed (e.g., PNG or custom raw format)
            egoDepthData = depthTexture.EncodeToPNG(); // Optional, depends on use case
            Destroy(depthTexture);

        }

        #endregion

        #region Agent Configuration and initialization
        public CloudServiceManager getCloudServiceManager() { return cloudServiceManager; } 
        public string getSystemPrompt() { return systemPrompt; }

        public void setSystemPrompt(string prompt) { systemPrompt = prompt; }  
        public virtual void SetupVirtualAgent()
        {

            if (agentPrefab != null && agentInstance == null)
            {
                agentInstance = Instantiate(agentPrefab, transform.position, transform.rotation);
                agentInstance.name = agentName;
                agentInstance.transform.SetParent(transform);

                if (cloudServiceManagerPrefab != null) 
                {
                    cloudServiceManagerInstance = Instantiate(cloudServiceManagerPrefab, transform.position, transform.rotation);
                    cloudServiceManagerInstance.name = agentName + "_cloudServiceManager";
                    cloudServiceManagerInstance.transform.SetParent(transform);
                }

                AssignAnimatorController();
                //AssignCharacterController();
                SetupLipSync();
                SetupAgentActionController();
                SetupEMotionHandler();
                SetupAgentVisionCamera();
                SetupSimpleEyeBlink();
                SetupEyeGazeController();
                SetupAudio();
                //SetupUIIndicator();
            }
            else
            {
                Debug.LogWarning("Agent prefab is not assigned or agent is already set up.");
            }
        }

        public void DestroyVirtualAgent()
        {
            if (ListeningIndicator != null && ThinkingIndicator!=null)
            {
                ListeningIndicator.transform.SetParent(this.transform);
                ThinkingIndicator.transform.SetParent(this.transform);
            }
            if (agentInstance != null)
            {
                // Destroy the agent instance
                DestroyImmediate(agentInstance);
                agentInstance = null;
            }
            else
            {
                Debug.LogWarning("No agent instance to clear.");
            }

            if (cloudServiceManagerInstance != null)
            {
                DestroyImmediate(cloudServiceManagerInstance);
            }

        }

        public void AssignCharacterController()
        {
            characterController = agentInstance.AddComponent<CharacterController>();
            // Calculate the height based on the model
            agentHeight = CalculateCharacterHeight();
            //float height = CalculateCharacterHeight();

            // Configure the CharacterController
            characterController.height = agentHeight;
            characterController.center = new Vector3(0, agentHeight / 2, 0);
        }
        // Assign the animator controller to the agent
        public void AssignAnimatorController()
        {
            // Check if the agentInstance has an Animator component
            animator = agentInstance.GetComponentInChildren<Animator>();
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
        public void SetupLipSync()
        {
            // check if the agentinstance is not null
            if (agentInstance != null)
            {
                // Attach Oculus LipSync scripts to the agent instance
                OVRLipSync ovrLipSync = agentInstance.AddComponent<OVRLipSync>();

                OVRLipSyncContext ovrLipSyncContext = agentInstance.AddComponent<OVRLipSyncContext>();
                ovrLipSyncContext.audioLoopback = true;
                ovrLipSyncContext.audioSource = agentInstance.GetComponent<AudioSource>();


                OVRLipSyncContextMorphTargetExtended ovrLipSyncContextMorphTarget = agentInstance.AddComponent<OVRLipSyncContextMorphTargetExtended>();

                ovrLipSyncContextMorphTarget.skinnedMeshRenderer = FindSkinnedMeshRenderer(agentInstance);

            }
            else
            {
                Debug.LogWarning("agent instance is null; cannot attach ovrlipsyn scripts.");
            }
        }

        public void SetupAgentVisionCamera()
        {

            Transform eyeTransform = FindEyeTransform(agentInstance);

            GameObject agentCameraGameObj = new GameObject("AgentVisionCamera");
            agentCameraGameObj.transform.SetParent(agentInstance.transform);
            agentCameraGameObj.transform.position = eyeTransform.position + new Vector3(0, 0, 0.05f);
            agentCameraGameObj.transform.localPosition = new Vector3(0, agentCameraGameObj.transform.localPosition.y, agentCameraGameObj.transform.localPosition.z);
            agentCameraGameObj.transform.localRotation = Quaternion.Euler(Vector3.one);
            agentCameraGameObj.transform.localScale = Vector3.one;

            agentVisionCamera = agentCameraGameObj.AddComponent<Camera>();

            agentVisionCamera.depth = -1;
            // Set general camera settings
            agentVisionCamera.stereoTargetEye = StereoTargetEyeMask.None; // Disable VR rendering for this camera
            // Get the layer index of "MirroredCharacter"
            int layerToExclude = LayerMask.NameToLayer("MirroredCharacter");

            if (layerToExclude != -1) // Ensure the layer exists
            {
                // Exclude the layer by using bitwise operations
                agentVisionCamera.cullingMask &= ~(1 << layerToExclude);
            }
            else
            {
                Debug.LogWarning("Layer 'MirroredCharacter' not found!");
            }
        }

        public void SetupUIIndicator()
        {
            ListeningIndicator = FindChildByName(this.gameObject, "ListeningText");
            ThinkingIndicator = FindChildByName(this.gameObject, "ThinkingText");
            Transform characterHead;
            characterHead = FindChildByName(this.gameObject, "CC_Base_Head").transform;
            if (characterHead == null)
            {
                characterHead = FindChildByName(this.gameObject, "Head").transform;
            }

            if (characterHead != null)
            {
                ListeningIndicator.transform.SetParent(characterHead);
                ThinkingIndicator.transform.SetParent(characterHead);
            }
        }
        public void SetupAudio()
        {
            // Get the SimpleChatBehavior component from this GameObject
            agentAudioSource = agentInstance.GetComponent<AudioSource>();
        }


        // Function to attach the EmotionHandler script
        public void SetupEMotionHandler()
        {
            if (agentInstance != null)
            {
                emotionHandler = agentInstance.AddComponent<EmotionHandler>();
                emotionHandler.skinnedMeshRenderer = FindSkinnedMeshRenderer(agentInstance);
                emotionHandler.characterType = characterType; 
            }
            else
            {
                Debug.LogWarning("agent instance is null; cannot attach EmotionHandler script.");
            }
        }

        public void SetupAgentActionController()
        {
            SkinnedMeshRenderer agentBlendshape = FindSkinnedMeshRenderer(agentInstance);
            
            if (agentBlendshape != null)
            {
                AgentStaticExpressionManager expressionManager = agentInstance.AddComponent<AgentStaticExpressionManager>();
                expressionManager.skinnedMeshRenderer = agentBlendshape;
                expressionManager.SetFacialExpression(FacialExpressionType.HAPPY);
            }
            
            actionController = agentInstance.AddComponent<AgentBodyMotionController>();
            faceAnimator = agentInstance.AddComponent<AgentFacialExpressionAnimator>();
            imageTriggerMode = ImageTriggerMode.Auto;
            descriptionMode = ToolDescriptionMode.SIMPLE;
        }
        public void SetupSimpleEyeBlink()
        {
            SkinnedMeshRenderer agentBlendshape = FindSkinnedMeshRenderer(agentInstance);

            if (agentBlendshape != null)
            {
                CharacterBlinkBehavior characterBlinkBehavior = agentInstance.AddComponent<CharacterBlinkBehavior>();
                if (characterType == CharacterType.CC4OrDIDIMO)
                {
                    characterBlinkBehavior.leftEyeBlendShapeName = "Eye_Blink_L";
                    characterBlinkBehavior.rightEyeBlendShapeName = "Eye_Blink_R";

                }
                if (characterType == CharacterType.Rocketbox)
                {
                    characterBlinkBehavior.leftEyeBlendShapeName = "blendShape1.AK_09_EyeBlinkLeft";
                    characterBlinkBehavior.rightEyeBlendShapeName = "blendShape1.AK_10_EyeBlinkRight";

                }
                characterBlinkBehavior.faceRenderer = agentBlendshape;
            }
        }
        public void SetupEyeGazeController()
        {
            eyeGazeController = agentInstance.AddComponent<EyeGazeController>();
            eyeGazeController.characterSkinnedMeshRenderer = FindSkinnedMeshRenderer(agentInstance);
        }
        public float CalculateCharacterHeight()
        {
            /*            Animator animator = GetComponent<Animator>();

                        // Use bone positions for humanoid characters
                        if (animator != null && animator.isHuman)
                        {
                            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
                            Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                            Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);

                            if (head != null && leftFoot != null && rightFoot != null)
                            {
                                Vector3 averageFootPos = (leftFoot.position + rightFoot.position) / 2;
                                return head.position.y - averageFootPos.y;
                            }
                        }
            */
            // Fallback to renderer bounds
            Bounds bounds = GetTotalBounds();
            return bounds.size.y;
        }

        public Bounds GetTotalBounds()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(transform.position, Vector3.zero);

            Bounds totalBounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
            {
                totalBounds.Encapsulate(renderer.bounds);
            }
            return totalBounds;
        }

        #endregion

        #region utilis

        public bool IsTriggerPhrase(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            string lowerMessage = message.ToLower();
            foreach (string phrase in triggerPhrases)
            {
                if (lowerMessage.Contains(phrase.ToLower()))
                {
                    return true;
                }
            }
            return false;
        }
        
        // A helper method to safely find a child by multiple possible names
        private Transform FindEyeTransform(GameObject agentInstance)
        {
            string[] possibleNames =
            {
                "CC_Base_L_Eye",  // CC4 eye name
                "Left_Eye",       // Alternative naming
                "Bip01 LEye"      // Additional fallback
            };

            foreach (var name in possibleNames)
            {
                var child = FindChildByName(agentInstance, name);
                if (child != null)
                    return child.transform;
            }

            return null; // Not found
        }

        private SkinnedMeshRenderer FindSkinnedMeshRenderer(GameObject parentObject)
        {
            // Define the possible exact name
            string exactName = "CC_Base_Body";
            // Define the partial name (the part that must be contained)
            string partialName = "_hipoly_81_bones_opacity";

            // First, try to find the exact name
            GameObject skinnedMeshTransform = FindChildByName(parentObject, exactName);
            if (skinnedMeshTransform != null)
            {
                return skinnedMeshTransform.GetComponent<SkinnedMeshRenderer>();
            }

            // If the exact name is not found, search for the partial name
            // This finds the first child with a name containing the partial string
            skinnedMeshTransform = FindChildWithPartialName(parentObject, partialName);
            if (skinnedMeshTransform != null)
            {
                return skinnedMeshTransform.GetComponent<SkinnedMeshRenderer>();
            }

            // Log an error if neither is found
            Debug.LogError("Skinned Mesh Renderer not found on the agent instance. Tried: " + exactName + " or a name containing " + partialName);
            return null;
        }

        // You will need a new helper function to find a child by a partial name
        private GameObject FindChildWithPartialName(GameObject parentObject, string partialName)
        {
            // A simple recursive search or a more efficient search can be used here.
            // This example uses GetComponentsInChildren to search all children.
            foreach (Transform child in parentObject.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.Contains(partialName))
                {
                    return child.gameObject;
                }
            }
            return null;
        }

        GameObject FindChildByName(GameObject parent, string childName)
        {
            foreach (Transform child in parent.transform)
            {
                if (child.name == childName)
                    return child.gameObject;

                // Recursive call to check deeper levels
                GameObject result = FindChildByName(child.gameObject, childName);
                if (result != null)
                    return result;
            }
            return null; // Child not found
        }
        public void FindPlayer()
        {
            // Try to find a VR player (Meta SDK)
            GameObject vrPlayer = GameObject.Find("OVRCameraRig");
            if (vrPlayer != null)
            {
                player = vrPlayer.transform.Find("TrackingSpace/CenterEyeAnchor");
                if (player != null) return;
            }

            // If no VR player, use the main camera as fallback
            if (Camera.main != null)
            {
                player = Camera.main.transform;
            }
        }

        #endregion
    }

}