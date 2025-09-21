//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE.md file in the project root for full license information.
// Adjusted by Lucie Kruse & Ke Li, Universit�t Hamburg
// Original Script: https://github.com/Azure-Samples/cognitive-services-speech-sdk/blob/master/quickstart/csharp/unity/text-to-speech/Assets/Scripts/HelloWorld.cs
using System;
using System.Threading;
using UnityEngine;
using Microsoft.CognitiveServices.Speech;
using IVH.Core.Utils;

namespace IVH.Core.ServiceConnector
{
    public enum AzureVoiceType
    {
        // Chinese (Simplified)
        ZHCN_Xiaochen_Neutral,
        ZHCN_Xiaoxiao_Female,
        ZHCN_Yunxi_Male,
        ZHCN_Yunyang_Professional,
        ZHCN_Xiaoxiao_Storytelling,
        ZHCN_Yunxi_Calm,

        // English (United States)
        ENUS_Aria_Neutral,
        ENUS_Jenny_Female,
        ENUS_Guy_Male,
        ENUS_Davis_Professional,
        ENUS_Aria_Storytelling,
        ENUS_Jenny_Calm,
        ENUS_Guy_Newscast,

        // German (Germany)
        DEDE_Katja_Neutral,
        DEDE_Seraphina_Female,
        DEDE_Florian_Male,
        DEDE_Amala_Professional,
        DEDE_Katja_Storytelling,
        DEDE_Florian_Calm,

        // French (France)
        FRFR_Denise_Neutral,
        FRFR_Vivienne_Female,
        FRFR_Remy_Male,
        FRFR_Henri_Professional,
        FRFR_Denise_Narration,
        FRFR_Vivienne_Storytelling,

        // Japanese (Japan)
        JAJP_Nanami_Neutral,
        JAJP_Nanami_Female,
        JAJP_Keita_Male,
        JAJP_Ayumi_Professional,
        JAJP_Nanami_Calm,
        JAJP_Keita_Storytelling
    }

    public static class AzureVoiceTypeExtensions
    {
        public static string ToAzureVoiceString(this AzureVoiceType voiceType)
        {
            return voiceType switch
            {
                // Chinese
                AzureVoiceType.ZHCN_Xiaochen_Neutral => "zh-CN-XiaochenNeural",
                AzureVoiceType.ZHCN_Xiaoxiao_Female => "zh-CN-XiaoxiaoNeural",
                AzureVoiceType.ZHCN_Yunxi_Male => "zh-CN-YunxiNeural",
                AzureVoiceType.ZHCN_Yunyang_Professional => "zh-CN-YunyangNeural",
                AzureVoiceType.ZHCN_Xiaoxiao_Storytelling => "zh-CN-XiaoxiaoNeural",
                AzureVoiceType.ZHCN_Yunxi_Calm => "zh-CN-YunxiNeural",

                // English
                AzureVoiceType.ENUS_Aria_Neutral => "en-US-AriaNeural",
                AzureVoiceType.ENUS_Jenny_Female => "en-US-JennyNeural",
                AzureVoiceType.ENUS_Guy_Male => "en-US-GuyNeural",
                AzureVoiceType.ENUS_Davis_Professional => "en-US-DavisNeural",
                AzureVoiceType.ENUS_Aria_Storytelling => "en-US-AriaNeural",
                AzureVoiceType.ENUS_Jenny_Calm => "en-US-JennyNeural",
                AzureVoiceType.ENUS_Guy_Newscast => "en-US-GuyNeural",

                // German
                AzureVoiceType.DEDE_Katja_Neutral => "de-DE-KatjaNeural",
                AzureVoiceType.DEDE_Seraphina_Female => "de-DE-SeraphinaNeural",
                AzureVoiceType.DEDE_Florian_Male => "de-DE-FlorianNeural",
                AzureVoiceType.DEDE_Amala_Professional => "de-DE-AmalaNeural",
                AzureVoiceType.DEDE_Katja_Storytelling => "de-DE-KatjaNeural",
                AzureVoiceType.DEDE_Florian_Calm => "de-DE-FlorianNeural",

                // French
                AzureVoiceType.FRFR_Denise_Neutral => "fr-FR-DeniseNeural",
                AzureVoiceType.FRFR_Vivienne_Female => "fr-FR-VivienneNeural",
                AzureVoiceType.FRFR_Remy_Male => "fr-FR-RemyNeural",
                AzureVoiceType.FRFR_Henri_Professional => "fr-FR-HenriNeural",
                AzureVoiceType.FRFR_Denise_Narration => "fr-FR-DeniseNeural",
                AzureVoiceType.FRFR_Vivienne_Storytelling => "fr-FR-VivienneNeural",

                // Japanese
                AzureVoiceType.JAJP_Nanami_Neutral => "ja-JP-NanamiNeural",
                AzureVoiceType.JAJP_Nanami_Female => "ja-JP-NanamiNeural",
                AzureVoiceType.JAJP_Keita_Male => "ja-JP-KeitaNeural",
                AzureVoiceType.JAJP_Ayumi_Professional => "ja-JP-AyumiNeural",
                AzureVoiceType.JAJP_Nanami_Calm => "ja-JP-NanamiNeural",
                AzureVoiceType.JAJP_Keita_Storytelling => "ja-JP-KeitaNeural",

                _ => throw new ArgumentOutOfRangeException(nameof(voiceType), voiceType, null)
            };
        }
    }
    public class AzureSpeech : MonoBehaviour
    {
        private AudioSource currentAudioSource;
        public enum VoiceType { CustomVoice, StandardVoice };
        // en-US-AndrewMultilingualNeural, en-US-AvaMultilingualNeural
        public AzureVoiceType standardVoiceSelection;// en-US-AvaMultilingualNeural find a list of avaliable voice actors here: https://speech.microsoft.com/portal/51d6637236fd4b91970b7ed04fbbeb61/voicegallery
        public static AzureSpeech Instance;

        public VoiceType currentVoiceType;

        public string Region = "westeurope";
        private string fallbackVoiceName = "en-US-NovaTurboMultilingualNeural";
        public string customVoiceName = "";

        private string endpointID = "";
        private string SubscriptionKey = "";

        private const int SampleRate = 24000;
        private float lastAudioChunkLength = 0;
        private string message;

        private object threadLocker = new object();
        private bool audioSourceNeedStop = false;

        private SpeechConfig speechConfig;
        private SpeechSynthesizer synthesizer;
        bool isSpeechCompleted = false;

        void Start()
        {
            endpointID = GeneralModelHelper.GetAzureEndpointId();
            SubscriptionKey = GeneralModelHelper.GetAzureSubscriptionKey();

            Instance = this;
            // Creates an instance of a speech config with specified subscription key and service region.
            speechConfig = SpeechConfig.FromSubscription(SubscriptionKey, Region);

            if (currentVoiceType == VoiceType.CustomVoice)
            {
                speechConfig.EndpointId = endpointID;
                speechConfig.SpeechSynthesisVoiceName = customVoiceName;
            }
            else
            {
                speechConfig.SpeechSynthesisVoiceName = AzureVoiceTypeExtensions.ToAzureVoiceString(standardVoiceSelection);
            }


            // The default format is RIFF, which has a riff header.
            // We are playing the audio in memory as audio clip, which doesn't require riff header.
            // So we need to set the format to raw (24KHz for better quality).
            speechConfig.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Raw24Khz16BitMonoPcm);

            // Creates a speech synthesizer.
            // Make sure to dispose the synthesizer after use!
            synthesizer = new SpeechSynthesizer(speechConfig, null);

            synthesizer.SynthesisCanceled += (s, e) =>
            {
                var cancellation = SpeechSynthesisCancellationDetails.FromResult(e.Result);
                message = $"CANCELED:\nReason=[{cancellation.Reason}]\nErrorDetails=[{cancellation.ErrorDetails}]\nDid you update the subscription info?";
                Debug.Log(message);
            };
        }
        public bool hasSpeechCompleted()
        {
            return isSpeechCompleted;
        }

        public void Speak(string t, AudioSource audioSource, Action<float> latencyCallback = null)
        {
            currentAudioSource = audioSource;
            string newMessage = null;
            var startTime = DateTime.Now;

            using (var result = synthesizer.StartSpeakingTextAsync(t).Result)
            {
                var audioDataStream = AudioDataStream.FromResult(result);
                var isFirstAudioChunk = true;
                isSpeechCompleted = false;
                var audioClip = AudioClip.Create(
                    "Speech",
                    SampleRate * 600, // Can speak 10mins audio as maximum
                    1,
                    SampleRate,
                    true,
                    (float[] audioChunk) =>
                    {
                        var chunkSize = audioChunk.Length;
                        var audioChunkBytes = new byte[chunkSize * 2];
                        var readBytes = audioDataStream.ReadData(audioChunkBytes);

                        // Calculate audio length
                        int audioDataSize = (int)readBytes;
                        int bytesPerSample = 2; // 2 bytes per sample for 16-bit audio
                        int channels = 1; // Mono audio
                        float audioStreamLengthInSeconds = (float)audioDataSize / (SampleRate * bytesPerSample * channels);
                        if (isFirstAudioChunk && readBytes > 0)
                        {
                            var endTime = DateTime.Now;
                            var latency = endTime.Subtract(startTime).TotalMilliseconds / 1000;
                            if (latencyCallback != null)
                            {
                                latencyCallback?.Invoke((float)latency);
                            }
                            newMessage = $"Speech synthesis succeeded!\nLatency: {latency} ms.";
                            isFirstAudioChunk = false;
                        }

                        for (int i = 0; i < chunkSize; ++i)
                        {
                            if (i < readBytes / 2)
                            {
                                audioChunk[i] = (short)(audioChunkBytes[i * 2 + 1] << 8 | audioChunkBytes[i * 2]) / 32768.0F;
                            }
                            else
                            {
                                audioChunk[i] = 0.0f;
                            }
                        }

                        if (readBytes == 0)
                        {

                            Thread.Sleep((int)lastAudioChunkLength * 100 + 300); // Leave some time for the audioSource to finish playback
                            audioSourceNeedStop = true;
                            isSpeechCompleted = true;

                        }

                        lastAudioChunkLength = audioStreamLengthInSeconds;
                    });
                audioSource.clip = audioClip;
                audioSource.Play();
            }

            lock (threadLocker)
            {
                if (newMessage != null)
                {
                    message = newMessage;
                }
            }
        }



        void Update()
        {
            lock (threadLocker)
            {
                if (audioSourceNeedStop)
                {
                    currentAudioSource.Stop();
                    audioSourceNeedStop = false;

                }
            }
        }

        void OnDestroy()
        {
            if (synthesizer != null)
            {
                synthesizer.Dispose();
            }
        }
    }

}