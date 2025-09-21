// This script can be useful if you want to have gaze behaviors of IVA using the paid asset from unity: https://assetstore.unity.com/packages/tools/animation/realistic-eye-movements-29168#asset_quality
// If you have already imported the realisticEyeMovement package, please uncomment this script and it's corresponding editor script to use. 

// using RealisticEyeMovements;
// using System;
// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using UnityEditor;
// using IVH.Core.ServiceConnector;
// using IVH.Core.Utils;
// using Newtonsoft.Json;
// using System.Threading.Tasks;
// using TMPro;

// namespace IVH.Core.IntelligentVirtualAgent
// {

//     public class RealisticGazeContingentAgent : AgentBase
//     {

//         protected Coroutine _interactionLoop;
//         protected List<ChatMessage> _conversation; // can be GPTMessage or GeminiMessage
//         protected private string llmQueryResponse = "";

//         [HideInInspector] EyeAndHeadAnimator eyeHeadAnimator;
//         [HideInInspector] LookTargetController lookTargetController;

//         //Transform player;


//         void Start()
//         {
//             FindVRPlayer();
//             eyeHeadAnimator = agentInstance.GetComponent<EyeAndHeadAnimator>();
//             lookTargetController = agentInstance.GetComponent<LookTargetController>();
//         }
//         void Update()
//         {
//             //checkAnimationState();
//         }
//         #region agent interaction
//         public void StartSimpleChat()
//         {
//             // Start interaction loop as coroutine
//             InitializeConversation();
//             _interactionLoop = StartCoroutine(InteractionLoop());
//             LookAtPlayer();
//         }
//         public void StartQuickSpeech(string text)
//         {
//             InitializeConversation();
//             StartCoroutine(QuickSpeech(text));
//         }
//         private IEnumerator QuickSpeech(string text)
//         {
//             QueryLLM_Text("please simply speak the following text while performing the approporiate facial expressions and actions: " +text);

//             yield return new WaitUntil(() => llmQueryResponse != "");

//             StructuredOutput res = StructuredResponseFormatter.ExtractMessageAndFunctionCall(llmQueryResponse);

//             if (ListeningIndicator != null && ThinkingIndicator != null)
//             {
//                 ListeningIndicator.SetActive(false);
//                 ThinkingIndicator.SetActive(false);
//             }

//             if (res.actionFunction != null && res.actionFunction != "none")
//             {
//                 PerformAction(res.actionFunction);
//             }
//             if (res.emotionFunction != null && res.emotionFunction != "none")
//             {
//                 Debug.Log("expressing emotion");
//                 ExpressEmotion(res.emotionFunction);
//             }
//             if (res.gazeFunction != null && res.gazeFunction != "none")
//             {
//                 if (res.gazeFunction == "LookAtUser")
//                 {
//                     LookAtPlayer();
//                 }
//                 if (res.gazeFunction == "LookIdly")
//                 {
//                     LookAwayIdly();
//                 }
//             }
//             if (res.textResponse != "" && res.textResponse != null)
//             {
//                 Debug.Log("response text: " + res.textResponse);
//                 AddToConversation(llmQueryResponse);
//                 yield return cloudServiceManager.TTS(res.textResponse, agentAudioSource, TTSService);
//             }

//             res = null;
//             llmQueryResponse = "";
//         }
//         public void StopSimpleChat()
//         {
//             // Stop interaction loop
//             if (_interactionLoop != null)
//             {
//                 StopCoroutine(_interactionLoop);
//                 _interactionLoop = null;
//                 if (ListeningIndicator != null)
//                 {
//                     ListeningIndicator.SetActive(false);

//                 }
//                 if (ThinkingIndicator != null)
//                 {
//                     ThinkingIndicator.SetActive(false);
//                 }

//             }
//         }

//         // Override the InteractionLoop with a new implementation
//         private IEnumerator InteractionLoop()
//         {
//             while (true)
//             {
//                 // STT
//                 string userMessage = "";
//                 if (ListeningIndicator != null && ThinkingIndicator != null)
//                 {
//                     ListeningIndicator.SetActive(true);
//                     ThinkingIndicator.SetActive(false);
//                 }
//                 Task<string> sttTask = cloudServiceManager.STT(language, STTService);
//                 yield return new WaitUntil(() => sttTask.IsCompleted);
//                 userMessage = sttTask.Result;

//                 if (ListeningIndicator != null && ThinkingIndicator != null)
//                 {
//                     ListeningIndicator.SetActive(false);
//                     ThinkingIndicator.SetActive(true);
//                 }
//                 // Process based on the selected trigger mode
//                 bool shouldProcess = false;

//                 switch (wakeupMode)
//                 {
//                     case AIWakeupMode.Automatic:
//                         shouldProcess = true;
//                         break;

//                     case AIWakeupMode.TriggerAlways:
//                         if (IsTriggerPhrase(userMessage))
//                         {
//                             shouldProcess = true;
//                         }
//                         else
//                         {
//                             Debug.Log("Trigger phrase not detected. Ignoring input.");
//                         }
//                         break;

//                     case AIWakeupMode.TriggerOnce:
//                         if (triggeredOnce)
//                         {
//                             shouldProcess = true;
//                         }
//                         else
//                         {
//                             if (IsTriggerPhrase(userMessage))
//                             {
//                                 shouldProcess = true;
//                                 triggeredOnce = true; // Set the flag to true after the first successful trigger
//                                 Debug.Log("Trigger phrase detected. Entering automatic mode.");
//                             }
//                             else
//                             {
//                                 Debug.Log("Trigger phrase not detected. Ignoring input.");
//                             }
//                         }
//                         break;
//                 }

//                 if (shouldProcess && !String.IsNullOrEmpty(userMessage))
//                 {
//                     if (vision == true)
//                     {
//                         if (imageTriggerMode == ImageTriggerMode.Auto || (imageTriggerMode == ImageTriggerMode.TriggerPhrase && userMessage == triggerPhrase))
//                         {
//                             if (targetCameraType == TargetCameraType.AgentCamera)
//                             {
//                                 egoImageData = null;
//                                 CaptureEgocentricImage(ImageHelper.GetResolution(resolution));
//                                 QueryVLM_Image(userMessage, egoImageData);
//                             }
//                             else if (targetCameraType == TargetCameraType.WebCam)
//                             {
//                                 webCamImageData = null;
//                                 yield return CaptureWebcamImage();
//                                 QueryVLM_Image(userMessage, webCamImageData);
//                             }
//                         }
//                     }
//                     else
//                     {

//                         QueryLLM_Text(userMessage);
//                     }
//                     yield return new WaitUntil(() => llmQueryResponse != "");

//                     StructuredOutput res = StructuredResponseFormatter.ExtractMessageAndFunctionCall(llmQueryResponse);

//                     if (ListeningIndicator != null && ThinkingIndicator != null)
//                     {
//                         ListeningIndicator.SetActive(false);
//                         ThinkingIndicator.SetActive(false);
//                     }

//                     if (res.actionFunction != null && res.actionFunction != "none")
//                     {
//                         PerformAction(res.actionFunction);
//                     }
//                     if (res.emotionFunction != null && res.emotionFunction != "none")
//                     {
//                         Debug.Log("expressing emotion");
//                         ExpressEmotion(res.emotionFunction);
//                     }
//                     if (res.gazeFunction != null && res.gazeFunction != "none")
//                     {
//                         if (res.gazeFunction == "LookAtUser")
//                         {
//                             LookAtPlayer();
//                         }
//                         if (res.gazeFunction == "LookIdly")
//                         {
//                             LookAwayIdly();
//                         }
//                     }
//                     if (res.textResponse != "" && res.textResponse != null)
//                     {
//                         Debug.Log("response text: " + res.textResponse);
//                         AddToConversation(llmQueryResponse);
//                         yield return cloudServiceManager.TTS(res.textResponse, agentAudioSource, TTSService);
//                     }

//                     res = null;
//                     llmQueryResponse = "";
//                 }
//             }
//         }

//         private void AddToConversation(string responseText)
//         {
//             if (foundationModel == FoundationModels.Unity_OpenAI_VLM ||
//                 foundationModel == FoundationModels.Ollama_Deepseek_R1_7B_LLM ||
//                 foundationModel == FoundationModels.Ollama_Deepseek_R1_14B_LLM /*||
//                 foundationModel == FoundationModels.Ollama_Deepseek_R1_32B_LLM ||
//                 foundationModel == FoundationModels.Ollama_Deepseek_R1_70B_LLM*/||
//                 foundationModel == FoundationModels.Ollama_OpenChat_7B_LLM ||
//                 foundationModel == FoundationModels.Ollama_llava_13B_VLM ||
//                 foundationModel == FoundationModels.Ollama_llava_7B_VLM ||
//                 foundationModel == FoundationModels.Ollama_Llama_3_2_3B_LLM ||
//                 foundationModel == FoundationModels.Ollama_Tinyllama_1B_LLM ||
//                 foundationModel == FoundationModels.Unity_DeepSeekR1_LLM)
//             {

//                 _conversation.Add(new GPTTextMessage(GPTMessageRoles.ASSISTANT, responseText));
//             }
//             else if (foundationModel == FoundationModels.Unity_Gemini_VLM)
//             {
//                 _conversation.Add(new GeminiMessage(GeminiMessageRole.MODEL.ToString().ToLower(), responseText));
//             }
//             else if (foundationModel == FoundationModels.UHAM_OpenAI_VLM)
//             {
//                 _conversation.Add(new GPTTextMessage(GPTMessageRoles.ASSISTANT, responseText));

//             }
//             else
//             {
//                 _conversation.Add(new GPTTextMessage(GPTMessageRoles.ASSISTANT, responseText));

//             }
//         }

//         private void InitializeConversation()
//         {
//             if (foundationModel == FoundationModels.Unity_OpenAI_VLM ||
//                 foundationModel == FoundationModels.Ollama_Deepseek_R1_7B_LLM ||
//                 foundationModel == FoundationModels.Ollama_Deepseek_R1_14B_LLM /*||
//                 foundationModel == FoundationModels.Ollama_Deepseek_R1_32B_LLM ||
//                 foundationModel == FoundationModels.Ollama_Deepseek_R1_70B_LLM*/||
//                 foundationModel == FoundationModels.Ollama_OpenChat_7B_LLM ||
//                 foundationModel == FoundationModels.Ollama_llava_13B_VLM ||
//                 foundationModel == FoundationModels.Ollama_llava_7B_VLM ||
//                 foundationModel == FoundationModels.Ollama_Llama_3_2_3B_LLM ||
//                 foundationModel == FoundationModels.Ollama_Tinyllama_1B_LLM ||
//                 foundationModel == FoundationModels.Unity_DeepSeekR1_LLM)
//             {

//                 _conversation = new List<ChatMessage>()
//                 {
//                 new GPTTextMessage(GPTMessageRoles.SYSTEM, systemPrompt)
//                 };
//             }
//             else if (foundationModel == FoundationModels.Unity_Gemini_VLM)
//             {
//                 _conversation = new List<ChatMessage>()
//                 {
//                     new GeminiMessage(GeminiMessageRole.MODEL.ToString().ToLower(), systemPrompt)
//                 };
//             }
//             else if (foundationModel == FoundationModels.UHAM_OpenAI_VLM)
//             {
//                 _conversation = new List<ChatMessage>()
//                 {
//                     new GPTTextMessage(GPTMessageRoles.SYSTEM, systemPrompt)
//                 };
//             }
//             else
//             {
//                 _conversation = new List<ChatMessage>()
//                 {
//                     new GPTTextMessage(GPTMessageRoles.SYSTEM, systemPrompt)
//                 };
//             }
//         }

//         public async void QueryLLM_Text(string userMessage)
//         {
//             try { llmQueryResponse = await cloudServiceManager.QueryLLM(userMessage, _conversation, foundationModel); ; }
//             catch (Exception ex) { Debug.LogError($"Error calling OpenAI: {ex.Message}"); }
//         }

//         public async void QueryVLM_Image(string userMessage, byte[] imageData)
//         {

//             try { llmQueryResponse = await cloudServiceManager.QueryVLM(userMessage, _conversation, foundationModel, imageData); ; }
//             catch (Exception ex) { Debug.LogError($"Error calling OpenAI: {ex.Message}"); }
//         }

//         #endregion


//         #region utils
//         void FindVRPlayer()
//         {
//             // Try to find a VR player (Meta SDK)
//             GameObject vrPlayer = GameObject.Find("OVRCameraRig");
//             if (vrPlayer != null)
//             {
//                 player = vrPlayer.transform.Find("TrackingSpace/CenterEyeAnchor");
//                 if (player != null) return;
//             }

//             // If no VR player, use the main camera as fallback
//             if (Camera.main != null)
//             {
//                 player = Camera.main.transform;
//             }
//         }

//         private void OnGUI()
//         {
// #if UNITY_EDITOR || UNITY_STANDALONE_WIN
//             GUILayout.Space(150); // Add some spacing at the top
//             GUILayout.BeginVertical(GUILayout.ExpandWidth(true));

//             GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
//             labelStyle.fontSize = 20; // Set font size to 20
//             labelStyle.normal.textColor = Color.white; // Ensure text color is visible
//             GUI.color = Color.green;
//             GUILayout.Label("Agent Controls");
//             GUI.color = Color.white;

//             if (GUILayout.Button("Start Simple Chat", GUILayout.Height(50)))
//             {
//                 StartSimpleChat();
//             }

//             if (GUILayout.Button("Stop Simple Chat", GUILayout.Height(50)))
//             {
//                 StopSimpleChat();
//             }

//             if (GUILayout.Button("Look At Player", GUILayout.Height(50)))
//             {
//                 LookAtPlayer();
//             }

            
//             if (GUILayout.Button("Look Idle", GUILayout.Height(50)))
//             {
//                 LookAwayIdly();
//             }
//             GUILayout.EndVertical();
// #endif
//         }
//         private void SetupRealisticEyeMovementCC4()
//         {
//             eyeHeadAnimator = agentInstance.AddComponent<EyeAndHeadAnimator>();
//             lookTargetController = agentInstance.AddComponent<LookTargetController>();
//             eyeHeadAnimator.ImportFromFile("Assets//UHAMInternal//RealisticEyeMovementsIVA//Presets//Reallusion//CC4.json");
//         }

//         public override void SetupVirtualAgent()
//         {

//             if (agentPrefab != null && agentInstance == null)
//             {
//                 agentInstance = Instantiate(agentPrefab, transform.position, transform.rotation);
//                 agentInstance.name = agentName;
//                 agentInstance.transform.SetParent(transform);

//                 // setup serevice manager
//                 cloudServiceManagerInstance = Instantiate(cloudServiceManagerPrefab, transform.position, transform.rotation);
//                 cloudServiceManagerInstance.name = agentName + "_cloudServiceManager";
//                 cloudServiceManagerInstance.transform.SetParent(transform);

//                 AssignAnimatorController();
//                 SetupLipSyncCC4();
//                 SetupAgentActionControllerCC4();
//                 SetupEMotionHandler();
//                 SetupAgentVisionCamera();
//                 SetupSimpleEyeBlink();
//                 SetupAudio();
//                 SetupRealisticEyeMovementCC4();
//                 //SetupUIIndicator();
//             }
//             else
//             {
//                 Debug.LogWarning("Agent prefab is not assigned or agent is already set up.");
//             }
//         }

//         public override string createSystemPrompt()
//         {
//             string bodyLanguageTools = "";
//             string facialExpressionTools = "";
//             string facsFacialExpressionTools = "";

//             if (descriptionMode == ToolDescriptionMode.SIMPLE)
//             {
//                 bodyLanguageTools = JsonConvert.SerializeObject(actionController.GetSimpleActionNameFiltered(bodyActionFilter).ToArray());
//                 facialExpressionTools = JsonConvert.SerializeObject(faceAnimator.GetSimpleFacialExpressionNameFiltered(facialExpressionFilter).ToArray());
//                 facsFacialExpressionTools = JsonConvert.SerializeObject(faceAnimator.GetSimpleDidimoActionName().ToArray());
//             }

//             else if (descriptionMode == ToolDescriptionMode.DETAIL)
//             {
//                 bodyLanguageTools = JsonConvert.SerializeObject(actionController.GetDetailActionsFiltered(bodyActionFilter).ToArray());
//                 facialExpressionTools = JsonConvert.SerializeObject(faceAnimator.GetDetailActionsFiltered(facialExpressionFilter).ToArray());
//                 facsFacialExpressionTools = JsonConvert.SerializeObject(faceAnimator.GetEnabledDidimoActionsAsGPTToolItems().ToArray());
//             }
//             else { }

//             string selectedFacialExpressionTools;
//             switch (emotionHandlerType)
//             {
//                 case EmotionHandlerType.CC4_Animation:
//                     selectedFacialExpressionTools = facialExpressionTools;
//                     break;
//                 case EmotionHandlerType.FACS:
//                     selectedFacialExpressionTools = facsFacialExpressionTools;
//                     break;
//                 default:
//                     Debug.LogWarning($"Unexpected emotion handler type: {emotionHandlerType}");
//                     selectedFacialExpressionTools = facialExpressionTools; // Fallback value
//                     break;
//             }

//             return $"Your name is:{agentName}. " +
//                 $"You are {age} years old. " +
//                 $"Your gender is {gender}. " +
//                 $"Your occupation is {occupation}." +
//                 $"The time is now: [DateTime.Now],"+
//                 "The user will interact with you via a webcam on a laptop or via a VR headset"+
//                 "answer questions in first person persona like: I think... I am seeing..." +
//                 "Choose the follwing approporiate body language, facial expression, and gaze behavior considering the entire conversation history" +
//                 "Possible body language animations:" +
//                  bodyLanguageTools +
//                 "Possible facial expression animations:" +
//                 selectedFacialExpressionTools +
//                 "Possible gaze behavior: " +
//                 "LookAtUser, LookIdly" +

//                 $"First, the microphone of the user's laptop headset will record users' speech and transcribe it via Speech to Text services using {STTService.ToString()}, " +
//                 $"Your text response will then be processed by text to speech services using {TTSService} TTS service, "+
//                 "If the user's query doesn't make sense, this could be a problem with the STT accuracy. If this happens, encourage the user to repeat what they said more clearly, "+
//                 "If there is an image in the query, it is taken from the first person perspective from a camera attached to your virtual body, enabling your vision capability. "+
//                 "Use the image to help produce your response. You don't need to describe what you see if it is not necessary," +

//                 "Return your message structurally with the following template: 'message: your response ||| body action: function name ||| face: function name ||| gaze:functio name'" +
//                 "If no actions needed, then return: 'message:your response ||| body action:none ||| face:none ||| gaze:none' " +
//                 "Only choose one body language and one facial expression in one response" +
//                 "Please be very short in your answer, in 1-2 sentences. " +
                
//                 $"Additional information about you: {additionalDescription}.";
//         }

//         #endregion

//         #region  gaze behavior
//         public void LookAtPlayer()
//         {
//             if (lookTargetController != null)
//             {
//                 lookTargetController.LookAtPlayer();
//             }
//             else
//             {
//                 lookTargetController = FindObjectOfType<LookTargetController>();
//                 lookTargetController.LookAtPlayer();
//             }
//         }

//         public void LookAwayIdly()
//         {
//             if (lookTargetController != null)
//             {
//                 lookTargetController.LookAroundIdly();
//             }
//             else
//             {
//                 lookTargetController = FindObjectOfType<LookTargetController>();
//                 lookTargetController.LookAroundIdly();
//             }
//         }
        
//         private void checkAnimationState()
//         {
//             // detect what animation is being played now
//             Animator animator = getAnimator();
//             // Get current animation clips on the base layer (layer 0)
//             AnimatorClipInfo[] clipInfos = animator.GetCurrentAnimatorClipInfo(0);

//             if (clipInfos.Length > 0)
//             {
//                 AnimationClip currentClip = clipInfos[0].clip;

//                 // Check if the current clip matches the target clip
//                 if (currentClip.name != "StandingIdle")
//                 {
//                     StartCoroutine(SmoothDisableEyeHeadAnimation());
//                 }
//                 else
//                 {
//                     StartCoroutine(SmoothEnableEyeHeadAnimation());
//                 }
//             }
//         }

//         IEnumerator SmoothDisableEyeHeadAnimation()
//         {
//             float t = 0.75f;
//             while (t > 0)
//             {
//                 eyeHeadAnimator.headWeight = t;
//                 eyeHeadAnimator.eyesWeight = t;
//                 t -= Time.deltaTime * 2.5f; // Adjust speed as needed
//                 yield return null;
//             }
//             // headAnimator.enabled = false;
//         }

//         IEnumerator SmoothEnableEyeHeadAnimation()
//         {
//             float t = 0;
//             while (t < 0.75f)
//             {
//                 eyeHeadAnimator.headWeight = t;
//                 eyeHeadAnimator.eyesWeight = t;
//                 t += Time.deltaTime * 2.5f; // Adjust speed as needed
//                 yield return null;
//             }
//         }
//         #endregion
//     }
// }
