# Intelligent Virtual Human SDK Core
All notable changes to this package are documented in this file.

## 1.1.0 
- Integrate Google Streaming API, where video, image, and audio is streamed together to Google Cloud. All STT, LLM and TTS are integrated altogether. This is less modular and flexible, but ensure fast low latency real-time response. Developers can see more info [here](https://docs.cloud.google.com/free/docs/free-cloud-features#free-tier).

## 1.0.3
- add scripting define symbols for internal mixamo animation pack support. 
- add ``BodyAnimationControllerType`` to distinguish and support different type of animation controllers (e.g. mixamo vs. rocketbox)
- apply ``Mixamo`` & ``Rocketbox`` body animation filters in ``AgentBodyMotionController``

## 1.0.2
- add more language options to azure TTS.

## 1.0.1
- hotfix mic UI in conversation with agent

## 1.0.0
- rocketbox full integration
- public release

## 0.7.9
- Add instant actor capability. 
- Add more voice options for Azure text to speech.. 
- Next release: add depth perception capability and navigation skills. 

## 0.7.8
- Implemented "Hello AI" wake up mode for bringing user awareness that they are interacting with an AI mode, not a real human
- Project clean up
- added and tested new language options : spanish, korean, japanese, and french. 

## 0.7.7
- Hotfix for physics based animation triggers

## 0.7.6
- TTS elevenlab connection hotfix

## 0.7.5
- fix STT audio leads to frame jitter issues
- microphone now starts and writes to a 2 second buffer instead of start and end recording within an asychronous process, which blocks the main thread.
- update ```SpeechToTextConnection``` and ```MicrophoneManager``` accordingly
- Add speaking and listening indicator setup
## 0.7.4
- put gaze behavior change before TTS reponse to reduce perceived latency
## 0.7.3
- add custom-made ```EyeGazeController``` with IK in case the paid asset ```RealisticEyeMovement``` package is not avaliable
- integrate custom ```EyeGazeController``` into the conversational agent configuration script
- The ```EyeGazeController``` allows the IVA to look at the user or look idly. 
- Update animation controller to enable IK pass for head eye coordination. 

##  0.7.2
- update license information regarding mixamo and rocketbox
- add sample rocket box characters and animations
- hotfix for compatible with realistic eye movement package
- add more body animation options to IVAs
- add behavior filter 
- minor bug fix for windows build
- comment out GPT4All for supporting android build
- add gemini tool calling support

## 0.5.4
- update agent setup script to new DIDIMO character blendshape names to support CC4 animations on didimo characters
- use ```EmotionHandlerType- FACs``` for basic FACS based animation; use ```EmotionHandlerType-CC4_Animation``` if you have a CC4 animator for more realistic facial expression animation. 
- added support German language
## 0.5.3
- support physics based animation data types
## 0.5.2
- hot fix in ``AgentBase`` class to increase agent setup robustness
## 0.5.1 
- hot fixes in agent setup script
## 0.5.0
- Added ``GetAudioWithId()`` and ``GetAudioFileID()`` functions in ``TextToSpeechConnection`` class for supporting multiplayer ( multi-user, single agnet) scenarios.
- Added an example service type: ``UHAM_GoogleCloud_MultiPlayer`` for demonstrating the new functions. 
## 0.4.2
- Hotfix
## 0.4.1
- Minor bug fixes
## 0.4.0
- Major code refactoring
- Added new VLM/LLM options in ``ServiceConnector``
  - Unity_Gemini_VLM
  - Unity_OpenAI_VLM
  - Unity_Local_LLM (GPT4all)
- Added new TTS options in ``ServiceConnector``
  - Unity_ElevenLabs 
  - Unity_Azure
- Added new STT option in ``ServiceConnector``
  - Unity_Whisper (local whisper model, latency around 5 s)
- Created ``agentBase`` class as baseline for developing different types of agents
- Created ``ConversationalAgent`` class which inherits  ``agentBase`` enabling basic conversational agent capabilities.
- Added vision capability (optional, and currently only supported when using ``Unity_Gemini_VLM``, and ``Unity_OpenAI_VLM`` services)
- Added ``AgentActionController`` for agent's non-verbal behavior (e.g. body languages)
- Added ``CharacterBlinkBehavior`` for basic automatic blinking behaviors
- Added additional support for **CC4 characters**
- Added details documentation
  
## 0.2.0
- Added lip sync for Smart Avatars
- Added selection of text-to-speech voice to ServiceConnectorManager
- Added setting for ServiceConnector IP and key to ServiceConnectorManager
- Added finger animation for Smart Avatars
- Added user microphone input
- Improved Smart Avatar body rotation
- Added pose retargeting for virtual humans

## 0.1.0
- Implemented first version of the interface to the ServiceConnector for realizing STT, TTS, LLM
- Realized a very basic version of a simple chat behavior for the Intelligent Virtual Agents
- Added animation profiles to Smart Avatars
- Added further helper for realizing custom inspectors

## 0.0.1
- Implemented very basic Smart Avatars
- Added first static helper to utils

## 0.0.0
- Set up package structure