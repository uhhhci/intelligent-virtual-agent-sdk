using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;


#region generic service

public enum AIWakeupMode
{
    Automatic,
    TriggerAlways,
    TriggerOnce
}

public enum CharacterType
{
    CC4OrDIDIMO,
    Rocketbox
}
public enum BodyAnimationControllerType
{
    Rocketbox,
    Mixamo
}
public enum BodyActionFilter
{
    NONE,
    Positive,
    Negative
}

public enum FacialExpressionFilter
{
    NONE,
    Positive,
    Negative
}
public enum ElevenLabsVoiceType
{
    Cassidy,
    Callum,
    Custom
}

public enum ImageTriggerMode
{
    TriggerPhrase,
    Auto,
    None
}

public enum FoundationModels
{
    Unity_Gemini_VLM,
    Unity_OpenAI_VLM,
    Unity_DeepSeekR1_LLM,
    UHAM_OpenAI_VLM, // basically encrypted OpenAI
    Local_Model_LLM, // Local LLM (can be chosen via Object in Unity Scene)
    Ollama_Deepseek_R1_7B_LLM,
    Ollama_Deepseek_R1_14B_LLM,
    //Ollama_Deepseek_R1_32B_LLM,
    //Ollama_Deepseek_R1_70B_LLM, // 70B is too much even for 3090 for in
    //Ollama_Llama_3_3_70B_LLM,
    Ollama_Llama_3_2_3B_LLM,
    Ollama_Tinyllama_1B_LLM,
    Ollama_OpenChat_7B_LLM,
    Ollama_llava_7B_VLM,
    Ollama_llava_13B_VLM
}

public enum VoiceService
{
    Unity_Azure,
    Unity_ElevenLab,
    UHAM_GoogleCloud,
    UHAM_GoogleCloud_MultiPlayer,
}

public enum VoiceRecognitionService
{
    UHAM_GoogleCloud,
    //Unity_GoogleCloud,
    //Unity_Azure, // TODO
    Local_Whisper
}

public enum ToolDescriptionMode
{
    NONE, // DON't send any tool, can be used to save tokens during debug and development phases. 
    SIMPLE, // only send name of the fuction: estimated token consumption without cache hit: 551
    DETAIL  // send detailed description of the function: estimated token consumption without cache hit: 2300 per request
}

public class StructuredOutput
{
    public string textResponse;
    public string actionFunction;
    public string emotionFunction;
    public string gazeFunction;
    public string physicsAction;

    public string physicsActionParameter;

    public StructuredOutput(string textResponse, string actionFunction = "none", string emotionFunction = "none",
                            string gazeFunction = "none", string physicsAction = "none", string physicsActionParamter = "none")
    {
        this.textResponse = textResponse;
        this.actionFunction = actionFunction;
        this.emotionFunction = emotionFunction;
        this.gazeFunction = gazeFunction;
        this.physicsAction = physicsAction;
        this.physicsActionParameter = physicsActionParamter;
    }
}

#endregion 

#region generic agent

public enum TargetCameraType
{
    AgentCamera,
    WebCam
}
public enum EmotionHandlerType
{
    FACS,
    CC4_Animation  // character creator 4
}
public enum Gender
{
    Female,
    Male,
    Nonbinary
}

public enum Occupation
{
    Psychologist,
    Nurse,
    Doctor,
    Manufacture,
    MusuemGuide
}

public static class LanguageHelper
{
    private static readonly Dictionary<AgentLanguage, string> LanguageCodes = new()
    {
        { AgentLanguage.english, "en-US" },
        { AgentLanguage.german, "de-DE" },
        //{ AgentLanguage.chinese, "cmn-Hans-CN" },
        { AgentLanguage.spanish, "es-ES" },
        { AgentLanguage.japanese, "ja-JP" },
        { AgentLanguage.korean, "ko-KR" },
        {AgentLanguage.french, "fr-FR"}
    };

    public static string GetLanguageCode(AgentLanguage language)
    {
        return LanguageCodes.TryGetValue(language, out var code) ? code : "en-US"; // default fallback
    }
}
public enum AgentLanguage
{
    english,
    german,
   // chinese, Google cloud SpeechClient V1 has bug for chinese language detection 
    spanish,
    japanese,
    korean,
    french
}
// characterize agent's personality
public enum MBTI
{
    ISTJ, ISFJ, INFJ, INTJ,
    ISTP, ISFP, INFP, INTP,
    ESTP, ESFP, ENFP, ENTP,
    ESTJ, ESFJ, ENFJ, ENTJ
}
// TODO for student co-worker, correctly label the facial expression animations with these emotional states
public enum EmotionalState
{
    // Primary Emotions
    Happiness,
    Sadness,
    Fear,
    Anger,
    Surprise,
    Disgust,

    // Secondary/Complex Emotions
    Embarrassment,
    Pride,
    Shame,
    Jealousy,
    Empathy,
    Frustration,
    Relief,
    Curiosity,

    // Neutral States
    Calm,
    Focused,

    // Situational/Contextual Emotions
    Boredom,
    Excitement,
    Anticipation,

    // Advanced Emotional States (Optional AI-Specific)
    AdaptationFatigue,
    Trust,
    Skepticism
}
#endregion
/*    Primary Emotions 
    Paul Ekman�s Basic Emotions
    Reference: Ekman, P. (1992). An Argument for Basic Emotions.Cognition and Emotion, 6(3-4), 169-200.
    Summary: Ekman identified six universal emotions(happiness, sadness, anger, fear, surprise, and disgust) based on cross-cultural studies of facial expressions.
    Secondary/Complex Emotions
    Plutchik�s Wheel of Emotions

    Reference: Plutchik, R. (2001). The Nature of Emotions.American Scientist, 89(4), 344-350.
    Summary: Plutchik proposed eight primary emotions and described how they combine to form complex emotional states(e.g., jealousy, embarrassment).
    Online Resource: Plutchik's Emotion Wheel
    Cognitive Appraisal Theories

    Reference: Lazarus, R.S. (1991). Emotion and Adaptation.Oxford University Press.
    Summary: Lazarus described how emotions arise from the individual's appraisal of their environment and situation.
    Neutral States
    Dimensional Models of Emotion(Circumplex Model)
    Reference: Russell, J.A. (1980). A Circumplex Model of Affect.Journal of Personality and Social Psychology, 39(6), 1161-1178.
    Summary: Emotions can be placed on a two-dimensional plane of arousal(high/low) and valence(positive/negative). States like calm and focus are low-arousal positive states.
    Situational/Contextual Emotions
    Social and Cultural Influence on Emotions
    Reference: Kitayama, S., & Markus, H.R. (1994). Emotion and Culture: Empirical Studies of Mutual Influence.American Psychological Association.
    Summary: Explores how emotions such as anticipation and embarrassment are shaped by cultural norms and expectations.
    Advanced Emotional States
    Trust and Skepticism in Human-Agent Interaction

    Reference: Nass, C., & Moon, Y. (2000). Machines and Mindlessness: Social Responses to Computers.Journal of Social Issues, 56(1), 81-103.
    Summary: Describes how humans attribute social and emotional qualities(e.g., trust, skepticism) to artificial agents.
    Application: Trust-building in IVH can enhance user interaction and engagement.
    Emotional Fatigue in AI

    Reference: Picard, R.W. (1997). Affective Computing.MIT Press.
    Summary: Discusses how artificial systems can mimic emotions, including advanced states like adaptation fatigue, relevant for AI-human interaction.
*/

