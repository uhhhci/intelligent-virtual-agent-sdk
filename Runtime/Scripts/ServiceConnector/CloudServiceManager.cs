using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using IVH.Core.IntelligentVirtualAgent;
using IVH.Core.ServiceConnector;
using Microsoft.CognitiveServices.Speech.Transcription;
using System;
using System.Threading.Tasks;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor.VersionControl;
#endif

public class CloudServiceManager : MonoBehaviour
{
    // Connect to Services, make sure to the "IntegratedServiceTemplate" prefab is in the scene
    private ServiceConnectorManager _serviceConnectorManager;
    public ServiceConnectorManager ServiceConnectorManager => _serviceConnectorManager;
    private AzureSpeech azureSpeech;
    private ElevenLabTTS elevenLabTTS;
    private OpenAIWrapper openAIWrapper;
    private GoogleCloudAIWrapper googleCloudWrapper;
    //private LocalLLMWrapper localLLM;
    private WhisperSTT whisperSTT;
    private OllamaWrapper ollamaWrapper;
    
    protected static event Action<string> OnResponse;
    protected static event Action OnRequest;

    // Start is called before the first frame update
    void Awake()
    {
        _serviceConnectorManager = ServiceConnectorManager.Instance.InitializeSingleton();
        // this is temporary solution. later on will port all services to the server. 
        azureSpeech = FindObjectOfType<AzureSpeech>();
        elevenLabTTS = FindObjectOfType<ElevenLabTTS>();
        openAIWrapper = FindObjectOfType<OpenAIWrapper>();
        googleCloudWrapper = FindObjectOfType<GoogleCloudAIWrapper>();
        //localLLM = FindObjectOfType<LocalLLMWrapper>();
        whisperSTT = FindObjectOfType<WhisperSTT>();
        ollamaWrapper = FindObjectOfType<OllamaWrapper>();
    }
    
    public async Task<string> STT(AgentLanguage language, VoiceRecognitionService STTService)
    {
        var tcs = new TaskCompletionSource<string>();

        if (STTService == VoiceRecognitionService.UHAM_GoogleCloud)
        {
            if (_serviceConnectorManager != null)
            {
                StartCoroutine(_serviceConnectorManager.SpeechToTextConnection.StartRecordingCoroutine(language,
                    (interim) => { Debug.Log("Interim: " + interim); },
                    (final_result) => { tcs.TrySetResult(final_result); }
                ));
            }
            else
            {
                Debug.LogWarning("UHAM STT cloud service is not set up.");
                tcs.TrySetResult(null);
            }
        }
        else if (STTService == VoiceRecognitionService.Local_Whisper)
        {
            if (whisperSTT != null)
            {
                whisperSTT.GetSpeechToText((final_result) =>
                {
                    Debug.Log("Final: " + final_result);
                    tcs.TrySetResult(final_result);
                });
            }
            else
            {
                Debug.LogWarning("Local STT service (Whisper) is not set up.");
                tcs.TrySetResult(null);
            }
        }
        else
        {
            Debug.LogWarning("Unsupported STT service.");
            tcs.TrySetResult(null);
        }

        return await tcs.Task;
    }

    public IEnumerator TTSGeneratePlaybackCommand(StructuredOutput structuredOutput, VoiceService TTSService, Action<AvatarPlaybackCommand> callback = null)
    {
        switch (TTSService)
        {
            case VoiceService.UHAM_GoogleCloud_MultiPlayer:
                if (_serviceConnectorManager)
                {
                    var audioID ="";
                    // Generate audio asynchronously
                    yield return _serviceConnectorManager.TextToSpeechConnection.GetAudioFileID(structuredOutput.textResponse, (_audioID) =>
                    {
                        audioID = _audioID;
                    });
                    
                    // Let caller broadcast info to other peers.
                    callback?.Invoke(new AvatarPlaybackCommand
                    {
                        audioId = audioID,
                        actionFunction = structuredOutput.actionFunction,
                        emotionFunction = structuredOutput.emotionFunction,
                    });
                }
                else
                {
                    Debug.LogWarning("UHAM TTS cloud service is not set up.");
                }
                break;
            default:
                Debug.LogWarning("Unsupported TTS service.");
                break;
        }
    }

    public IEnumerator PlayAvatarPlaybackCommand(AvatarPlaybackCommand avatarPlaybackCommand, AudioSource audioSource, Action<string, string> performActionEmotionCallback = null)
    {
        var audioID = avatarPlaybackCommand.audioId;
        if (audioID != null) {
            yield return _serviceConnectorManager.TextToSpeechConnection.GetAudioWithId(audioID, (audio) =>
            {
                if (audio != null)
                {
                    audioSource.clip = audio;
                    audioSource.Play();
                }
            });
            performActionEmotionCallback?.Invoke(avatarPlaybackCommand.actionFunction, avatarPlaybackCommand.emotionFunction);
            // Wait for audio to finish playing
            if (audioSource.clip != null)
            {
                yield return new WaitForSeconds(audioSource.clip.length);
            }
        }
    }

    // Functional Capabilities
    public IEnumerator TTS(string text, AudioSource audioSource, VoiceService TTSService, Action<float> latencyMeasurement = null)
    {
        //if (string.IsNullOrEmpty(text) yield break; // Skip if text is empty

        float ttsStartTime = Time.time;

        switch (TTSService)
        {
            case VoiceService.UHAM_GoogleCloud_MultiPlayer:
                if (_serviceConnectorManager != null)
                {
                    string audioID ="";
                    // Generate audio asynchronously
                    yield return _serviceConnectorManager.TextToSpeechConnection.GetAudioFileID(text, (_audioID) =>
                    {
                        audioID = _audioID;
 
                    });

                    if (audioID != null) {
                        Debug.Log("received audio id:" + audioID);
                        yield return _serviceConnectorManager.TextToSpeechConnection.GetAudioWithId(audioID, (audio) =>
                        {
                            if (audio != null)
                            {
                                audioSource.clip = audio;
                                audioSource.Play();
                            }
                        });

                        // Wait for audio to finish playing
                        if (audioSource.clip != null)
                        {
                            yield return new WaitForSeconds(audioSource.clip.length);
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("UHAM TTS cloud service is not set up.");
                }
                break;
            case VoiceService.UHAM_GoogleCloud:
                if (_serviceConnectorManager != null)
                {
                    // Generate audio asynchronously
                    yield return _serviceConnectorManager.TextToSpeechConnection.GenerateAudioFile(text, (audio) =>
                    {
                        if (audio != null)
                        {
                            audioSource.clip = audio;
                            audioSource.Play();
                        }
                    });

                    // Measure latency
                    float ttsLatency = Time.time - ttsStartTime;
                    latencyMeasurement?.Invoke(ttsLatency);

                    // Wait for audio to finish playing
                    if (audioSource.clip != null)
                    {
                        yield return new WaitForSeconds(audioSource.clip.length);
                    }
                }
                else
                {
                    Debug.LogWarning("UHAM TTS cloud service is not set up.");
                }
                break;

            case VoiceService.Unity_Azure:
                if (azureSpeech != null)
                {
                    // Use Azure TTS
                    azureSpeech.Speak(text, audioSource, latencyMeasurement);
                    yield return new WaitUntil(() => azureSpeech.hasSpeechCompleted());
                }
                else
                {
                    Debug.LogWarning("Azure TTS cloud service is not set up.");
                }
                break;

            case VoiceService.Unity_ElevenLab:
                if (elevenLabTTS != null)
                {
                    // Use Eleven Labs TTS
                    yield return elevenLabTTS.SendTextToSpeechRequest(text, audioSource);
                }
                else
                {
                    Debug.LogWarning("Eleven Labs TTS cloud service is not set up.");
                }
                break;

            default:
                Debug.LogWarning("Unsupported TTS service.");
                break;
        }
    }

    public async Task<string> SendOllamaTextRequest(string userMessage, List<GPTMessage> _conversation, FoundationModels modelType)
    {
        if (ollamaWrapper != null)
        {
            try
            {
                string response = await ollamaWrapper.SendTextMessage(_conversation, userMessage, modelType, OnRequest);
                return response;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error calling OpenAI: {ex.Message}");
                return null; // Return null or handle the error appropriately
            }
        }
        else
        {
            Debug.LogWarning("You are trying to use Ollama deep seek model, but you have not set up yet in the scene!");
            return null;
        }
    }

    public async Task<string> SendOpenAITextRequest(string userMessage, List<GPTMessage> _conversation)
    {

        if (openAIWrapper != null)
        {
            try
            {
                string response = await openAIWrapper.SendTextMessage(_conversation, userMessage, OnRequest);
                return response;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error calling OpenAI: {ex.Message}");
                return null; // Return null or handle the error appropriately
            }
        }
        else
        {
            Debug.LogWarning("you are trying to use OpenAI wrapper from unity, but you have not set it up yet in the scene!");
            return null;
        }
    }

    public async Task<string> SendOpenAIImageRequest(string userMessage, List<GPTMessage> _conversation, byte[] imageData)
    {
        Debug.Log(_conversation);

        if (openAIWrapper != null)
        {
            try
            {
                string response = await openAIWrapper.MakeImageRequest(
                    _conversation,
                    Convert.ToBase64String(imageData),
                    OnRequest,
                    userMessage
                );
                return response;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error calling OpenAI: {ex.Message}");
                return null; // Return null or handle the error appropriately
            }
        }
        else
        {
            Debug.LogWarning("you are trying to use OpenAI wrapper from unity, but you have not set it up yet in the scene!");
            return null;
        }
    }

    public async Task<string> SendGeminiTextRequest(string userMessage, List<GeminiMessage> _conversation, List<GeminiTool> tools=null)
    {
        if (googleCloudWrapper != null)
        {
            try
            {
                string response = await googleCloudWrapper.SendTextMessage(
                    _conversation,
                    userMessage,
                    OnRequest,
                    tools);
                return response;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error calling OpenAI: {ex.Message}");
                return null;
            }
        }
        else
        {
            Debug.LogWarning("you are trying to use Google Cloud gemini wrapper from unity, but you have not set it up yet in the scene!");
            return null;
        }
    }


    public async Task<string> SendGeminiImageRequest(string userMessage, List<GeminiMessage> _conversation, byte[] imageData, List<GeminiTool> tools = null)
    {
        if (googleCloudWrapper != null)
        {
            try
            {
                string response = await googleCloudWrapper.MakeImageRequest(
                    _conversation,
                    Convert.ToBase64String(imageData),
                    OnRequest,
                    userMessage,
                    tools);
                return response;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error calling OpenAI: {ex.Message}");
                return null;
            }
        }
        else
        {
            Debug.LogWarning("you are trying to use Google Cloud gemini wrapper from unity, but you have not set it up yet in the scene!");
            return null;
        }
    }

    public async Task<List<GeminiBoundingBoxResponse>> SendGeminiBoundingBoxRequest(
        string userMessage,
        List<GeminiMessage> _conversation,
        byte[] imageData,
        List<GeminiTool> tools = null)
    {
        if (googleCloudWrapper != null)
        {
            try
            {
                // Create a clean conversation context.
                // We intentionally ignore '_conversation' here to prevent the Agent's System Prompt
                // (e.g., "Answer in 1-2 sentences") from conflicting with the strict JSON requirement.
                List<GeminiMessage> tempConversation = new List<GeminiMessage>();

                // Strict system instruction for computer vision tasks.
                string jsonPrompt = @"Detect objects in the image. Return a JSON array. 
                Each item must have strictly these two fields: 
                1. 'label' (string) 
                2. 'box_2d' (array of 4 integers: [ymin, xmin, ymax, xmax]). 
                Do not use markdown. Just raw JSON.";

                // Send request using the clean context and strict prompt
                string responseText = await googleCloudWrapper.MakeBoundingBoxesRequest(
                    tempConversation,
                    Convert.ToBase64String(imageData),
                    OnRequest,
                    jsonPrompt,
                    tools);

                // Deserialize and return
                var boundingBoxes = googleCloudWrapper.ParseBoundingBoxesFromJson(responseText);
                return boundingBoxes;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CloudServiceManager] Error calling Gemini bounding box API: {ex.Message}");
                return new List<GeminiBoundingBoxResponse>();
            }
        }
        else
        {
            Debug.LogWarning("[CloudServiceManager] Google Cloud Gemini wrapper is not set up in the scene!");
            return new List<GeminiBoundingBoxResponse>();
        }
    }

    // public async Task<string> SendLocalLLMTextRequest(string userMessage)// TODO: Implement local model
    // {
    //     return await localLLM.SendPrompt(userMessage);
    // }

    // Currently only accomodate gemini tools, because OpenAI doesn't support tool calling along with text response
    // In case of future support of OpenAI, needs to make tool calling more generic. 
    public async Task<object> QueryVLM(
        string userMessage,
        List<ChatMessage> _conversation,
        FoundationModels serviceType,
        byte[] imageData,
        List<GeminiTool> geminiTools = null,
        bool requestBoundingBoxes = false)
    {
        if (serviceType == FoundationModels.Unity_Gemini_VLM)
        {
            var geminiConversation = _conversation.OfType<GeminiMessage>().ToList();
            if (requestBoundingBoxes)
                return await SendGeminiBoundingBoxRequest(userMessage, geminiConversation, imageData, tools: geminiTools);
            else
                return await SendGeminiImageRequest(userMessage, geminiConversation, imageData, geminiTools);
        }
        else if (serviceType == FoundationModels.Unity_OpenAI_VLM)
        {
            var openAIConversation = _conversation
                .OfType<GPTMessage>()
                .ToList();
            return await SendOpenAIImageRequest(userMessage, openAIConversation, imageData);
        }
        else if (serviceType == FoundationModels.UHAM_OpenAI_VLM)
        {
            // not working yet for some reason!
            ImageDetail imageDetail = ImageDetail.Low;
            _conversation.Add(GPTRequestPayload.GPTImagePayloadConstructor(userMessage, Convert.ToBase64String(imageData), imageDetail));

            var openAIConversation = _conversation
                                    .OfType<GPTMessage>()
                                    .ToArray();

            var response = await _serviceConnectorManager.LanguageModelConnection.GetChat_NLPResponseStreamedAsync(
                                openAIConversation,
                                LanguageModelConnection.GPT_Models.GPT_4o,
                                (streamMessage) =>
                                {
                                    Debug.Log("Streamed response: " + streamMessage);
                                });

            Debug.Log("Final response: " + response.content);

            return response.content;
        }
        else if (serviceType == FoundationModels.Ollama_Deepseek_R1_7B_LLM ||
                serviceType == FoundationModels.Ollama_Deepseek_R1_14B_LLM /*|| 
                serviceType == FoundationModels.Ollama_Deepseek_R1_32B_LLM || 
                serviceType == FoundationModels.Ollama_Deepseek_R1_70B_LLM*/ ||
                serviceType == FoundationModels.Ollama_Llama_3_2_3B_LLM ||
                serviceType == FoundationModels.Ollama_Tinyllama_1B_LLM)
        {
            Debug.LogWarning("Ollama deep seek R1 does not support vision based query!");
            return "";
        }
        else
        {
            throw new ArgumentException($"Unsupported service type: {serviceType}");
        }
    }

    public async Task<string> QueryLLM(string userMessage, List<ChatMessage> _conversation, FoundationModels serviceType, List<GeminiTool> geminiTools =null)
    {
        if (serviceType == FoundationModels.Unity_Gemini_VLM)
        {
            // Convert List<ChatMessage> to List<GeminiMessage>
            // Careful, this can be slow whenm, think about a better future solution
            var geminiConversation = _conversation
                .OfType<GeminiMessage>() // Filters and casts to GeminiMessage
                .ToList();
            return await SendGeminiTextRequest(userMessage, geminiConversation, geminiTools);
        }
        else if (serviceType == FoundationModels.Unity_OpenAI_VLM)
        {
            var openAIConversation = _conversation
                .OfType<GPTMessage>()
                .ToList();
            return await SendOpenAITextRequest(userMessage, openAIConversation);
        }
        else if (serviceType == FoundationModels.UHAM_OpenAI_VLM)
        {
            _conversation.Add(new GPTTextMessage(GPTMessageRoles.USER, userMessage));
            
            var openAIConversation = _conversation
                                    .OfType<GPTMessage>()
                                    .ToArray();

            var response = await _serviceConnectorManager.LanguageModelConnection.GetChat_NLPResponseStreamedAsync(
                                openAIConversation,
                                LanguageModelConnection.GPT_Models.GPT_4o,
                                (streamMessage) =>
                                {
                                    Debug.Log("Streamed response: " + streamMessage);
                                });

           Debug.Log("Final response: " + response.content);

            return response.content;
        }
        // else if (serviceType == FoundationModels.Local_Model_LLM)
        // {
        //     return null;//await localLLM.SendPrompt(userMessage);
        // }
        else if(serviceType == FoundationModels.Ollama_Deepseek_R1_7B_LLM  || 
                serviceType == FoundationModels.Ollama_Deepseek_R1_14B_LLM /*|| 
                serviceType == FoundationModels.Ollama_Deepseek_R1_32B_LLM || 
                serviceType == FoundationModels.Ollama_Deepseek_R1_70B_LLM*/ ||
                serviceType == FoundationModels.Ollama_llava_13B_VLM ||
                serviceType == FoundationModels.Ollama_llava_7B_VLM ||
                serviceType == FoundationModels.Ollama_Llama_3_2_3B_LLM ||
                serviceType == FoundationModels.Ollama_Tinyllama_1B_LLM)
        {
            var openAIConversation = _conversation
                .OfType<GPTMessage>()
                .ToList();
            return await SendOllamaTextRequest(userMessage, openAIConversation, serviceType);
        }
        else
        {
            throw new ArgumentException($"Unsupported service type: {serviceType}");
        }
    }
}