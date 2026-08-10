using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEditor;
using IVH.Core.ServiceConnector;
using IVH.Core.Actions;
using Newtonsoft.Json;
using IVH.Core.Utils.StaticHelper;
using IVH.Core.IntelligentVirtualAgent.Presets;
using IVH.Core.Knowledge;
using UnityEngine.UI;
using System.Runtime.Versioning;
using System.ComponentModel.Design.Serialization;
using System.Threading.Tasks;
using UnityEngine.AI;

namespace IVH.Core.IntelligentVirtualAgent
{
    /// <summary>
    /// Abstract base class for all intelligent virtual agents. Owns the avatar instance,
    /// cloud-service wiring, demographics used in prompt construction, and the non-verbal
    /// behavior subsystems (expressions, body motion, gaze, locomotion).
    /// Subclasses add conversation loops — e.g. <see cref="GeminiLiveAgent"/> for realtime voice,
    /// <see cref="ConversationalAgent"/> for modular STT/LLM/TTS pipelines.
    /// </summary>
    public abstract class AgentBase : MonoBehaviour
    {
        [Header("Preset (optional)")]
        /// <summary>
        /// Optional <see cref="AgentPreset"/> applied in <c>Awake()</c> before any other setup. Fields
        /// on this preset override matching fields on the agent. Leave empty for legacy behavior.
        /// </summary>
        public AgentPreset preset;

        [Header("General Agent Attributes")]
        /// <summary>Prefab of the 3D avatar this agent will instantiate at setup time.</summary>
        public GameObject agentPrefab;

        /// <summary>Prefab containing the <see cref="CloudServiceManager"/>. Only required for non-Gemini-Live agents.</summary>
        [Tooltip("Choose your cloud service manager prefab, which can be found in the IVA-SDK-Core>Runtime>Prefab folder. ")]
        public GameObject cloudServiceManagerPrefab;

        /// <summary>Which facial-emotion backend to use: FACS-based blendshapes or CC4 animation database.</summary>
        public EmotionHandlerType emotionHandlerType;

        /// <summary>Avatar rig family. Determines blendshape naming conventions and available animations.</summary>
        public CharacterType characterType = CharacterType.CC4OrDIDIMO;

        /// <summary>Source of body animations. Rocketbox and Mixamo have different clip sets.</summary>
        public BodyAnimationControllerType bodyAnimationControllerType = BodyAnimationControllerType.Rocketbox;

        /// <summary>Animator controller applied to the avatar at runtime.</summary>
        public RuntimeAnimatorController animatorController;

        [Header("Agent Demographics")]
        //public MBTI personality;
        /// <summary>Display name of the agent. Included in the generated system prompt.</summary>
        public string agentName = "";

        /// <summary>Agent's age in years. Included in the generated system prompt.</summary>
        public int age = 30;

        /// <summary>Agent's gender. Included in the generated system prompt and used for gender-specific animations.</summary>
        public Gender gender = Gender.Nonbinary;

        /// <summary>Agent's occupation (e.g. "tutor", "therapist"). Included in the generated system prompt.</summary>
        public string occupation = "";

        //public MBTI personality;
        /// <summary>Primary language the agent speaks. Affects STT model selection and TTS voice defaults.</summary>
        public AgentLanguage language;

        /// <summary>Free-form additional persona description appended to the generated system prompt.</summary>
        public string additionalDescription="";
        protected float agentHeight;

        [Header("Cloud Service Settings")]
        // Connect to Services
        protected CloudServiceManager cloudServiceManager;

        /// <summary>TTS provider to use for speech synthesis (Azure, ElevenLabs, Gemini native, etc.).</summary>
        public VoiceService TTSService;

        /// <summary>Foundation LLM for non-realtime conversation flows. Ignored for Gemini Live agents which use their own wrapper.</summary>
        public FoundationModels foundationModel;

        /// <summary>STT provider to use for speech recognition (Azure, Whisper local, Google, etc.).</summary>
        public VoiceRecognitionService STTService;
        protected string systemPrompt = "";

        [Header("Agent Vision Settings")]
        /// <summary>Master toggle for visual input. When true, the agent streams frames to a vision-capable LLM.</summary>
        [HideInInspector] public bool vision = false;

        /// <summary>Which camera feed to use for vision: the avatar's in-world camera, or an external webcam.</summary>
        [HideInInspector] public TargetCameraType targetCameraType = TargetCameraType.AgentCamera;

        /// <summary>When vision frames are captured: continuously (Auto) or only when a trigger phrase is spoken.</summary>
        [HideInInspector] public ImageTriggerMode imageTriggerMode = ImageTriggerMode.Auto;

        /// <summary>Image resolution sent to the LLM. Higher = more tokens and cost.</summary>
        [HideInInspector] public ImageResolution resolution = ImageResolution.VGA;

        /// <summary>Device name of the webcam to use when <see cref="targetCameraType"/> is <c>WebCam</c>.</summary>
        [HideInInspector] public string selectedWebCamName = "";
        protected string triggerPhrase = "what are you seeing";
        protected WebCamTexture webCamTexture;

        /// <summary>
        /// Lazy-allocates and starts the agent's <c>WebCamTexture</c> so callers (typically the in-game
        /// settings panel) can show a live preview or switch <see cref="targetCameraType"/> to
        /// <c>WebCam</c> at runtime — even if the agent was launched in <c>AgentCamera</c> mode and
        /// never allocated a webcam in <c>Awake</c>. If a device name is supplied and differs from the
        /// currently-running one, the old texture is stopped and replaced.
        /// </summary>
        /// <param name="deviceName">Optional <c>WebCamDevice</c> name. <c>null</c> or empty falls back
        /// to <see cref="selectedWebCamName"/>, then to the platform default.</param>
        /// <returns>The active <c>WebCamTexture</c>, or <c>null</c> if no camera devices are available.</returns>
        public WebCamTexture EnsureWebCamReady(string deviceName = null)
        {
            if (WebCamTexture.devices.Length == 0) return null;

            string target = !string.IsNullOrEmpty(deviceName)
                ? deviceName
                : (!string.IsNullOrEmpty(selectedWebCamName) ? selectedWebCamName : null);

            if (webCamTexture != null && webCamTexture.isPlaying
                && (target == null || webCamTexture.deviceName == target))
            {
                return webCamTexture;
            }

            if (webCamTexture != null)
            {
                if (webCamTexture.isPlaying) webCamTexture.Stop();
            }

            webCamTexture = target != null ? new WebCamTexture(target) : new WebCamTexture();
            webCamTexture.Play();
            if (rawImage != null)
            {
                rawImage.texture = webCamTexture;
                if (rawImage.material != null) rawImage.material.mainTexture = webCamTexture;
            }
            return webCamTexture;
        }

        /// <summary>Optional UI target that will display the webcam preview.</summary>
        [HideInInspector] public RawImage rawImage;  // Drag a RawImage UI element here to display the webcam feed

        // vision module
        /// <summary>Runtime reference to the in-world camera attached to the avatar's head.</summary>
        [HideInInspector] public Camera agentVisionCamera;
        protected byte[] egoImageData;
        protected byte[] egoDepthData;
        protected byte[] webCamImageData;

        //[Header("Physics Based Animations")]
        //[HideInInspector] public bool physicsBasedAnimation = false;
        // add components and settings for the animation package

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
        [Tooltip("Enable basic locomotion of the IVA, only available for Mixamo and CC4 characters. A nav mesh object needs to be available in the scene ")]
        [HideInInspector] public bool enableLocomotion = false; 
        [HideInInspector] public AgentLocomotion agentLocomotion;

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
            if (preset != null) preset.ApplyTo(this);

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

        /// <summary>
        /// Synthesizes and plays speech through the configured TTS service. Override in subclasses
        /// to add lip-sync gating or alternate audio routing.
        /// </summary>
        /// <param name="text">The text to speak. Must not be null.</param>
        public virtual IEnumerator Speak(string text) { yield return cloudServiceManager.TTS(text, agentAudioSource, TTSService); }

        /// <summary>
        /// Captures microphone input and returns the recognized transcript via the configured STT service.
        /// </summary>
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

        /// <summary>
        /// Triggers a facial expression by name. The handler used depends on <see cref="emotionHandlerType"/>:
        /// FACS drives blendshapes directly; CC4_Animation plays a named facial animation clip.
        /// </summary>
        /// <param name="emotion">Expression name (e.g. "happy", "sad"). Case-sensitive to the handler's vocabulary.</param>
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

        /// <summary>
        /// Plays a named body animation (wave, dance, greet, etc.) via <see cref="AgentBodyMotionController"/>.
        /// </summary>
        /// <param name="action">Animation name, as defined in the active body animation controller.</param>
        public void PerformAction(string action) { actionController.TriggerActionViaActionName(action); }

        /// <summary>
        /// Builds the default system prompt from demographics and available non-verbal tool vocabulary.
        /// Override to inject custom persona instructions, domain-specific rules, or alternate output formats.
        /// </summary>
        /// <returns>The constructed system-prompt string.</returns>
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

        #region agent vision
        protected IEnumerator CaptureEgocentricImageCoroutine()
        {
            Vector2Int res= new Vector2Int(512, 512);
            // Wait for rendering to complete so we don't get a blank image
            yield return new WaitForEndOfFrame();

            if (agentVisionCamera == null)
            {
                Debug.LogWarning("Agent Vision Camera is missing!");
                yield break;
            }

            // 1. Use GetTemporary for better memory management than "new RenderTexture"
            RenderTexture rt = RenderTexture.GetTemporary(res.x, res.y, 24);
            
            // 2. Render the Agent's camera view to this texture
            agentVisionCamera.targetTexture = rt;
            agentVisionCamera.Render();

            // 3. Read pixels
            RenderTexture.active = rt;
            Texture2D texture2D = new Texture2D(res.x, res.y, TextureFormat.RGB24, false);
            texture2D.ReadPixels(new Rect(0, 0, res.x, res.y), 0, 0);
            texture2D.Apply();

            // 4. Cleanup
            agentVisionCamera.targetTexture = null;
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt); // Release back to pool

            // 5. Encode to JPG (Much faster and lighter than PNG for LLM streaming)
            // Quality 50 is usually sufficient for Gemini Vision
            egoImageData = texture2D.EncodeToJPG(50); 

            // 6. Memory cleanup
            Destroy(texture2D);
        }
        protected void CaptureEgocentricImage(Vector2Int res)
        {
            // Take a snapshot of an image from egocentric agent's perspective
            //targetCamera.fieldOfView = FoV;
            RenderTexture renderTexture = new RenderTexture(res.x, res.y, 24);
            agentVisionCamera.targetTexture = renderTexture;
            agentVisionCamera.Render();

            RenderTexture.active = renderTexture;
            Texture2D texture2D = new Texture2D(res.x, res.y, TextureFormat.RGB24, false);
            texture2D.ReadPixels(new Rect(0, 0, res.x, res.y), 0, 0);
            texture2D.Apply();

            agentVisionCamera.targetTexture = null;
            RenderTexture.active = null;
            Destroy(renderTexture);

            egoImageData = texture2D.EncodeToPNG();
            Destroy(texture2D);
        }
        protected IEnumerator CaptureWebcamImage()
        {
            if (webCamTexture == null || !webCamTexture.isPlaying) yield break;

            yield return new WaitForEndOfFrame();

            // Cap width at 512px to save tokens/bandwidth.
            float aspect = (float)webCamTexture.width / webCamTexture.height;
            int targetWidth = 512;
            int targetHeight = Mathf.RoundToInt(targetWidth / aspect);

            // Resize on the GPU via a temporary RenderTexture + Blit.
            RenderTexture rt = RenderTexture.GetTemporary(targetWidth, targetHeight, 0);
            Graphics.Blit(webCamTexture, rt);

            RenderTexture.active = rt;
            Texture2D resultTex = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false);
            resultTex.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
            resultTex.Apply();

            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            // JPG quality 50 is sufficient for AI vision.
            webCamImageData = resultTex.EncodeToJPG(50);

            Destroy(resultTex);
        }

        protected void CaptureDepth(Vector2Int res)
        {
            RenderTexture renderTexture = new RenderTexture(res.x, res.y, 24, RenderTextureFormat.Depth);
            agentVisionCamera.targetTexture = renderTexture;

            agentVisionCamera.RenderWithShader(Shader.Find("Hidden/Internal-DepthNormalsTexture"), null);

            RenderTexture.active = renderTexture;
            Texture2D depthTexture = new Texture2D(res.x, res.y, TextureFormat.RFloat, false);
            depthTexture.ReadPixels(new Rect(0, 0, res.x, res.y), 0, 0);
            depthTexture.Apply();

            agentVisionCamera.targetTexture = null;
            RenderTexture.active = null;
            Destroy(renderTexture);

            egoDepthData = depthTexture.EncodeToPNG();
            Destroy(depthTexture);

        }

        #endregion

        #region Agent Configuration and initialization

        /// <summary>Returns the agent's <see cref="CloudServiceManager"/> instance. Null for Gemini Live agents.</summary>
        public CloudServiceManager getCloudServiceManager() { return cloudServiceManager; }

        /// <summary>Returns the currently-active system prompt (after any runtime overrides).</summary>
        public string getSystemPrompt() { return systemPrompt; }

        /// <summary>Overrides the auto-generated system prompt with a custom one. Call before <see cref="SetupVirtualAgent"/>.</summary>
        public void setSystemPrompt(string prompt) { systemPrompt = prompt; }

        /// <summary>
        /// Aggregates context-prefix output from every <see cref="IContextProvider"/> attached to
        /// this GameObject (e.g. <see cref="Knowledge.DocumentGroundingComponent"/>, long-term
        /// <see cref="Memory.MemoryManager"/>) and returns a single concatenated string the
        /// subclass agent prepends to its prompt before each LLM call.
        /// </summary>
        /// <param name="querySeed">
        /// The current user utterance, or any short topical seed. May be null for realtime agents
        /// that retrieve once at session start.
        /// </param>
        /// <returns>The concatenated prefix, or empty string when no provider returns content.</returns>
        /// <remarks>
        /// Default implementation aggregates over <c>GetComponents&lt;IContextProvider&gt;()</c> in
        /// component order. Override only when you need custom ordering or composition. Returning
        /// the empty string is bit-identical to v3.0 behavior.
        /// </remarks>
        public virtual Task<string> BuildContextPrefixAsync(string querySeed)
        {
            return ContextProviderAggregator.BuildPrefixAsync(gameObject, querySeed);
        }

        /// <summary>
        /// Instantiates the avatar prefab and wires up all default subsystems: animator, lip-sync,
        /// body-motion controller, facial expression animator, emotion handler, vision camera,
        /// blink behavior, eye-gaze controller, and audio source. Safe to call once per agent lifetime.
        /// </summary>
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
                //SetupAgentLocomotion();
            }
            else
            {
                Debug.LogWarning("Agent prefab is not assigned or agent is already set up.");
            }
        }

        /// <summary>
        /// Tears down the avatar instance and cloud service manager. Safe to call multiple times.
        /// Does not destroy the <see cref="AgentBase"/> component itself.
        /// </summary>
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

            characterController.height = agentHeight;
            characterController.center = new Vector3(0, agentHeight / 2, 0);
        }

        public void AssignAnimatorController()
        {
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

        public void SetupLipSync()
        {
            if (agentInstance != null)
            {
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

        public void SetupAgentLocomotion()
        {
            if (agentInstance != null)
            {
                agentLocomotion = agentInstance.AddComponent<AgentLocomotion>();
                NavMeshAgent meshAgent = agentInstance.AddComponent<NavMeshAgent>();
            }
            else
            {
                Debug.LogWarning("agent instance is null; cannot attach NavMeshAgent script.");
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