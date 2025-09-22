using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using IVH.Core.ServiceConnector;
using IVH.Core.Utils;
using System.Threading.Tasks;

namespace IVH.Core.IntelligentVirtualAgent
{

    public class ConversationalAgent : AgentBase
    {

        protected Coroutine _interactionLoop;
        protected List<ChatMessage> _conversation; // can be GPTMessage or GeminiMessage
        protected private string llmQueryResponse = "";

        #region agent interaction
        public void StartSimpleChat()
        {
            // Start interaction loop as coroutine
            InitializeConversation();
            _interactionLoop = StartCoroutine(InteractionLoop());
        }

        public void StopSimpleChat()
        {
            // Stop interaction loop
            if (_interactionLoop != null)
            {
                StopCoroutine(_interactionLoop);
                _interactionLoop = null;
            }
        }

        private IEnumerator InteractionLoop()
        {
            while (true)
            {
                // STT
                string userMessage = "";

                Task<string> sttTask = cloudServiceManager.STT(language, STTService);
                yield return new WaitUntil(() => sttTask.IsCompleted);
                userMessage = sttTask.Result;


                // Process based on the selected trigger mode
                string trimmedMessage = "";
                bool shouldProcess = false;
                // check if user speaks trigger phrase
                switch (wakeupMode)
                {
                    case AIWakeupMode.Automatic:
                        shouldProcess = true;
                        trimmedMessage = userMessage;
                        break;

                    case AIWakeupMode.TriggerAlways:
                        if (IsTriggerPhrase(userMessage))
                        {
                            shouldProcess = true;
                        }
                        else
                        {
                            Debug.Log("Trigger phrase not detected. Ignoring input.");
                        }
                        break;

                    case AIWakeupMode.TriggerOnce:
                        if (triggeredOnce)
                        {
                            shouldProcess = true;
                            trimmedMessage = userMessage;
                        }
                        else
                        {
                            if (IsTriggerPhrase(userMessage))
                            {
                                shouldProcess = true;
                                triggeredOnce = true; // Set the flag to true after the first successful trigger
                                Debug.Log("Trigger phrase detected. Entering automatic mode.");
                            }
                            else
                            {
                                Debug.Log("Trigger phrase not detected. Ignoring input.");
                            }
                        }
                        break;
                }

                if (shouldProcess && !String.IsNullOrEmpty(userMessage))
                {
                    if (vision == true)
                    {
                        if (imageTriggerMode == ImageTriggerMode.Auto || (imageTriggerMode == ImageTriggerMode.TriggerPhrase && userMessage == triggerPhrase))
                        {
                            if (targetCameraType == TargetCameraType.AgentCamera)
                            {
                                egoImageData = null;
                                CaptureEgocentricImage(ImageHelper.GetResolution(resolution));
                                QueryVLM_Image(userMessage, egoImageData);
                            }
                            else if (targetCameraType == TargetCameraType.WebCam)
                            {
                                webCamImageData = null;
                                yield return CaptureWebcamImage();
                                QueryVLM_Image(userMessage, webCamImageData);
                            }
                        }
                    }
                    else
                    {

                        QueryLLM_Text(userMessage);
                    }
                    yield return new WaitUntil(() => llmQueryResponse != "");

                    StructuredOutput res = StructuredResponseFormatter.ExtractMessageAndFunctionCall(llmQueryResponse);

                    if (res.actionFunction != null && res.actionFunction != "none")
                    {
                        PerformAction(res.actionFunction);
                    }
                    if (res.emotionFunction != null && res.emotionFunction != "none")
                    {
                        ExpressEmotion(res.emotionFunction);
                    }
                    if (res.gazeFunction != null && res.gazeFunction != "none")
                    {
                        if (res.gazeFunction == "LookAtUser")
                        {
                            if (player == null)
                            {
                                FindPlayer();
                                eyeGazeController.playerTarget = player;
                            }
                            eyeGazeController.currentGazeMode = Actions.EyeGazeController.GazeMode.LookAtPlayer;
                        }
                        if (res.gazeFunction == "LookIdly")
                        {
                            eyeGazeController.currentGazeMode = Actions.EyeGazeController.GazeMode.Idle;
                        }
                    }
                    if (res.textResponse != "" && res.textResponse != null)
                    {
                        Debug.Log("response text: " + res.textResponse);
                        AddToConversation(llmQueryResponse);
                        yield return cloudServiceManager.TTS(res.textResponse, agentAudioSource, TTSService);
                    }
                    res = null;
                    llmQueryResponse = "";
                }
            }
        }

        private void AddToConversation(string responseText)
        {
            if (foundationModel == FoundationModels.Unity_OpenAI_VLM ||
                foundationModel == FoundationModels.Ollama_Deepseek_R1_7B_LLM ||
                foundationModel == FoundationModels.Ollama_Deepseek_R1_14B_LLM /*||
                foundationModel == FoundationModels.Ollama_Deepseek_R1_32B_LLM ||
                foundationModel == FoundationModels.Ollama_Deepseek_R1_70B_LLM*/||
                foundationModel == FoundationModels.Ollama_OpenChat_7B_LLM ||
                foundationModel == FoundationModels.Ollama_llava_13B_VLM ||
                foundationModel == FoundationModels.Ollama_llava_7B_VLM ||
                foundationModel == FoundationModels.Ollama_Llama_3_2_3B_LLM ||
                foundationModel == FoundationModels.Ollama_Tinyllama_1B_LLM ||
                foundationModel == FoundationModels.Unity_DeepSeekR1_LLM)
            {

                _conversation.Add(new GPTTextMessage(GPTMessageRoles.ASSISTANT, responseText));
            }
            else if (foundationModel == FoundationModels.Unity_Gemini_VLM)
            {
                _conversation.Add(new GeminiMessage(GeminiMessageRole.MODEL.ToString().ToLower(), responseText));
            }
            else if (foundationModel == FoundationModels.UHAM_OpenAI_VLM)
            {
                _conversation.Add(new GPTTextMessage(GPTMessageRoles.ASSISTANT, responseText));

            }
            else
            {
                _conversation.Add(new GPTTextMessage(GPTMessageRoles.ASSISTANT, responseText));

            }
        }

        private void InitializeConversation()
        {
            if (foundationModel == FoundationModels.Unity_OpenAI_VLM ||
                foundationModel == FoundationModels.Ollama_Deepseek_R1_7B_LLM ||
                foundationModel == FoundationModels.Ollama_Deepseek_R1_14B_LLM /*||
                foundationModel == FoundationModels.Ollama_Deepseek_R1_32B_LLM ||
                foundationModel == FoundationModels.Ollama_Deepseek_R1_70B_LLM*/||
                foundationModel == FoundationModels.Ollama_OpenChat_7B_LLM ||
                foundationModel == FoundationModels.Ollama_llava_13B_VLM ||
                foundationModel == FoundationModels.Ollama_llava_7B_VLM ||
                foundationModel == FoundationModels.Ollama_Llama_3_2_3B_LLM ||
                foundationModel == FoundationModels.Ollama_Tinyllama_1B_LLM ||
                foundationModel == FoundationModels.Unity_DeepSeekR1_LLM)
            {

                _conversation = new List<ChatMessage>()
                {
                new GPTTextMessage(GPTMessageRoles.SYSTEM, systemPrompt)
                };
            }
            else if (foundationModel == FoundationModels.Unity_Gemini_VLM)
            {
                _conversation = new List<ChatMessage>()
                {
                    new GeminiMessage(GeminiMessageRole.MODEL.ToString().ToLower(), systemPrompt)
                };
            }
            else if (foundationModel == FoundationModels.UHAM_OpenAI_VLM)
            {
                _conversation = new List<ChatMessage>()
                {
                    new GPTTextMessage(GPTMessageRoles.SYSTEM, systemPrompt)
                };
            }
            else
            {
                _conversation = new List<ChatMessage>()
                {
                    new GPTTextMessage(GPTMessageRoles.SYSTEM, systemPrompt)
                };
            }
        }

        public async void QueryLLM_Text(string userMessage)
        {
            try { llmQueryResponse = await cloudServiceManager.QueryLLM(userMessage, _conversation, foundationModel); ; }
            catch (Exception ex) { Debug.LogError($"Error calling OpenAI: {ex.Message}"); }
        }

        public async void QueryVLM_Image(string userMessage, byte[] imageData)
        {

            try { llmQueryResponse = await cloudServiceManager.QueryVLM(userMessage, _conversation, foundationModel, imageData); ; }
            catch (Exception ex) { Debug.LogError($"Error calling OpenAI: {ex.Message}"); }
        }

        #endregion

        #region utils
        private void OnGUI()
        {
#if UNITY_EDITOR || UNITY_STANDALONE_WIN
            GUILayout.Space(150); // Add some spacing at the top
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));

            GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = 20; // Set font size to 20
            labelStyle.normal.textColor = Color.white; // Ensure text color is visible
            GUI.color = Color.green;
            GUILayout.Label("Agent Controls");
            GUI.color = Color.white;

            if (GUILayout.Button("Start Simple Chat", GUILayout.Height(50)))
            {
                StartSimpleChat();
            }

            if (GUILayout.Button("Stop Simple Chat", GUILayout.Height(50)))
            {
                StopSimpleChat();
            }

            if (GUILayout.Button("Look At Player", GUILayout.Height(50)))
            {
                eyeGazeController.currentGazeMode = Actions.EyeGazeController.GazeMode.LookAtPlayer;
            }

            if (GUILayout.Button("Look Idle", GUILayout.Height(50)))
            {
                eyeGazeController.currentGazeMode = Actions.EyeGazeController.GazeMode.Idle;
            }
            GUILayout.EndVertical();
#endif
        }

        public void StartQuickSpeech(string text)
        {
            InitializeConversation();
            StartCoroutine(QuickSpeech(text));
        }
        private IEnumerator QuickSpeech(string text)
        {
            QueryLLM_Text("please simply speak the following text while performing the approporiate facial expressions and actions: " +text);

            yield return new WaitUntil(() => llmQueryResponse != "");

            StructuredOutput res = StructuredResponseFormatter.ExtractMessageAndFunctionCall(llmQueryResponse);

            if (ListeningIndicator != null && ThinkingIndicator != null)
            {
                ListeningIndicator.SetActive(false);
                ThinkingIndicator.SetActive(false);
            }

            if (res.actionFunction != null && res.actionFunction != "none")
            {
                PerformAction(res.actionFunction);
            }
            if (res.emotionFunction != null && res.emotionFunction != "none")
            {
                Debug.Log("expressing emotion");
                ExpressEmotion(res.emotionFunction);
            }
            if (res.gazeFunction != null && res.gazeFunction != "none")
            {
                if (res.gazeFunction == "LookAtUser")
                {
                    if (player == null)
                    {
                        FindPlayer();
                        eyeGazeController.playerTarget = player;
                    }
                    eyeGazeController.currentGazeMode = Actions.EyeGazeController.GazeMode.LookAtPlayer;
                }
                if (res.gazeFunction == "LookIdly")
                {
                    eyeGazeController.currentGazeMode = Actions.EyeGazeController.GazeMode.Idle;
                }
            }
            if (res.textResponse != "" && res.textResponse != null)
            {
                Debug.Log("response text: " + res.textResponse);
                AddToConversation(llmQueryResponse);
                yield return cloudServiceManager.TTS(res.textResponse, agentAudioSource, TTSService);
            }

            res = null;
            llmQueryResponse = "";
        }

        #endregion
    }
}