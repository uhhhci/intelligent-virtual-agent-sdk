using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using IVH.Core.ServiceConnector;
using IVH.Core.Utils;
using System.Threading.Tasks;
using System.IO;

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
                                if (boundingBoxes == true)
                                {
                                    QueryVLM_Image(userMessage, egoImageData, true);
                                }
                                else
                                {
                                    QueryVLM_Image(userMessage, egoImageData);
                                }
                            }
                            else if (targetCameraType == TargetCameraType.WebCam)
                            {
                                webCamImageData = null;
                                yield return CaptureWebcamImage();
                                if (boundingBoxes == true)
                                {
                                    QueryVLM_Image(userMessage, webCamImageData, true);
                                    SaveImageToFile(webCamImageData);
                                    
                                }
                                else
                                {
                                QueryVLM_Image(userMessage, webCamImageData);
                                }
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

        public async void QueryVLM_Image(string userMessage, byte[] imageData, bool requestBoundingBoxes = false)
        {
            try
            {
                // 1. Primary Request: Conversational Response
                // We force 'requestBoundingBoxes' to false to ensure we get a text response 
                // that respects the System Prompt (persona, brevity, etc.).
                var textResult = await cloudServiceManager.QueryVLM(
                    userMessage,
                    _conversation,
                    foundationModel,
                    imageData,
                    null,
                    false 
                );

                // Handle the conversational output (TTS, Animation, etc.)
                if (textResult is string textResponse)
                {
                    llmQueryResponse = textResponse; 
                }

                // 2. Secondary Request: Spatial Analysis (Optional)
                // If requested, we send a parallel/sequential request strictly for data extraction.
                // This runs independently so the JSON format doesn't bleed into the chat.
                if (requestBoundingBoxes)
                {
                    var boxResult = await cloudServiceManager.QueryVLM(
                        userMessage,
                        _conversation,
                        foundationModel,
                        imageData,
                        null,
                        true // Force true to trigger the specialized JSON path
                    );

                    if (boxResult is List<GeminiBoundingBoxResponse> boxes)
                    {
                        Debug.Log($"[Spatial] Received {boxes.Count} bounding boxes.");
                        
                        // Process boxes (e.g., draw UI, update internal world model)
                        foreach (var box in boxes)
                        {
                            // Debug visualization or logic hook
                            Debug.Log($"Detected: {box.Label} at {string.Join(",", box.Box2D)}");
                            AnnotateAndSaveImage(imageData, boxes);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ConversationalAgent] Error calling Gemini VLM: {ex.Message}");
            }
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

        private void AnnotateAndSaveImage(byte[] sourceImageData, List<GeminiBoundingBoxResponse> boxes)
        {
            Texture2D texture = new Texture2D(2, 2);
            if (!texture.LoadImage(sourceImageData)) return;

            Color boxColor = Color.red;
            int thickness = 3; 
            int w = texture.width;
            int h = texture.height;

            foreach (var box in boxes)
            {
                if (box.Box2D == null || box.Box2D.Count < 4) continue;

                float yMinRaw = box.Box2D[0];
                float xMinRaw = box.Box2D[1];
                float yMaxRaw = box.Box2D[2];
                float xMaxRaw = box.Box2D[3];

                // Horizontal and VerticalCalculation
                // Note: Gemini VLM uses a 1000x1000 coordinate system
                int xMin = (int)(xMinRaw / 1000f * w);
                int xMax = (int)(xMaxRaw / 1000f * w);
                int yMin = (int)((1f - yMaxRaw / 1000f) * h);
                int yMax = (int)((1f - yMinRaw / 1000f) * h);

                // Clamp to image bounds
                xMin = Mathf.Clamp(xMin, 0, w - 1);
                xMax = Mathf.Clamp(xMax, 0, w - 1);
                yMin = Mathf.Clamp(yMin, 0, h - 1);
                yMax = Mathf.Clamp(yMax, 0, h - 1);

                DrawLine(texture, xMin, yMin, xMax, yMin, boxColor, thickness); // Bottom
                DrawLine(texture, xMin, yMax, xMax, yMax, boxColor, thickness); // Top
                DrawLine(texture, xMin, yMin, xMin, yMax, boxColor, thickness); // Left
                DrawLine(texture, xMax, yMin, xMax, yMax, boxColor, thickness); // Right
            }

            texture.Apply();
            byte[] annotatedData = texture.EncodeToPNG();
            
            string filename = $"annotated_{System.DateTime.Now:HHmmss}.png";
            string filePath = Path.Combine(Application.persistentDataPath, filename);
            File.WriteAllBytes(filePath, annotatedData);
            Destroy(texture);
        }

        // Simple helper to draw a line of pixels
        private void DrawLine(Texture2D tex, int x1, int y1, int x2, int y2, Color col, int thickness)
        {
            // Simple bounding box for the line segments
            int xMin = Mathf.Min(x1, x2) - thickness / 2;
            int xMax = Mathf.Max(x1, x2) + thickness / 2;
            int yMin = Mathf.Min(y1, y2) - thickness / 2;
            int yMax = Mathf.Max(y1, y2) + thickness / 2;

            for (int x = xMin; x <= xMax; x++)
            {
                for (int y = yMin; y <= yMax; y++)
                {
                    if (x >= 0 && x < tex.width && y >= 0 && y < tex.height)
                    {
                        tex.SetPixel(x, y, col);
                    }
                }
            }
        }

        // Saves image data to a file for debugging
        protected void SaveImageToFile(byte[] imageData)
        {
            string filename = $"captured_image_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";
            string filePath = Path.Combine(Application.persistentDataPath, filename);
    
            File.WriteAllBytes(filePath, imageData);
        }

        #endregion
    }
}