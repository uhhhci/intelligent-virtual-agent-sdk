using System.Collections;
using System.Collections.Generic;
using IVH.Core.ServiceConnector;
using UnityEngine;

public class SimpleChatBehaviour : MonoBehaviour
{
    private ServiceConnectorManager _serviceConnectorManager;

    public AudioSource agentAudioSource;

    private LanguageModelConnection.GPT_Models _model = LanguageModelConnection.GPT_Models.Chat_GPT_35;

    public string contextMessage = "A discussion about Soccer.";
    public string userMessage = "Hello, I am Erik!";
    public string agentMessage= "Hi Erik, now that you are here, let's discuss Soccer!";
    public string interactionUserMessage = "I like goals!";

    private List<GPTMessage> _conversation;

    private Coroutine _interactionLoop;

    void Awake()
    {
        _serviceConnectorManager = ServiceConnectorManager.Instance.InitializeSingleton();
    }

    private void InitializeConversation()
    {
        _conversation = new List<GPTMessage>()
        {
            new GPTTextMessage(GPTMessageRoles.SYSTEM, contextMessage),
            new GPTTextMessage(GPTMessageRoles.USER, userMessage),
            new GPTTextMessage(GPTMessageRoles.ASSISTANT, agentMessage),
        };
    }

    /// <summary>
    /// Starts the simple chat behaviour of the agent.
    /// </summary>
    public void StartSimpleChat()
    {
        InitializeConversation();
        _interactionLoop = StartCoroutine(InteractionLoop());
    }

    /// <summary>
    /// Stops the simple chat behaviour of the agent.
    /// </summary>
    public void StopSimpleChat()
    {
        if (_interactionLoop != null)
        {
            StopCoroutine(_interactionLoop);
            _interactionLoop = null;
            _conversation = null;
        }
    }

    /// <summary>
    /// Allows User to set the used ChatGPT model.
    /// </summary>
    public void SetChatGPTModel(LanguageModelConnection.GPT_Models model)
    {
        _model = model;
    }

    private IEnumerator InteractionLoop()
    {
        yield return new WaitForSeconds(1f);

        while (true)
        {
            // 1. Listen to the user STT
            string userMessage = "";
            yield return _serviceConnectorManager.SpeechToTextConnection.StartRecordingCoroutine(AgentLanguage.english, (interim) => { Debug.Log("Interim: " + interim); }, (final_result) =>
            {
                Debug.Log("Final: " + final_result);
                userMessage = final_result;
            });

            _conversation.Add(new GPTTextMessage(GPTMessageRoles.USER, userMessage));

            // 2. Process the user input OPENAI
            string responseText = "";
            bool isDone = false;
            GPTTextMessage result = null;
            List<string> toPlay = new List<string>();

            yield return _serviceConnectorManager.LanguageModelConnection.GetChat_NLPResponseStreamed(_conversation.ToArray(), _model, (response) =>
            {
                isDone = true;
                result = response;
            }, (streamResponse) =>
            {
                if (!streamResponse.finished)
                {
                    responseText += " " + streamResponse.delta;
                    toPlay.Add(responseText);
                    responseText = "";
                }
            });

            if (isDone)
            {
                Debug.Log("Chat GPT finished with model: " + _model);
            }

            _conversation.Add(new GPTTextMessage(GPTMessageRoles.ASSISTANT, result.content));

            // 3. Generate TTS
            yield return _serviceConnectorManager.TextToSpeechConnection.GenerateAudioFile(result.content, (audio) =>
            {
                // 4. Play TTS
                agentAudioSource.clip = audio;
                agentAudioSource.Play();
            });

            yield return new WaitForSeconds(agentAudioSource.clip.length);
        }
    }
}