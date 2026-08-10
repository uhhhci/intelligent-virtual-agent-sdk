# About This Intelligent Virtual Agent

This document describes the Intelligent Virtual Human SDK (IVA SDK) — the toolkit that powers this agent. It is written for retrieval-augmented grounding so the agent can answer questions about itself accurately, without hallucinating implementation details. Each section below is a focused topic; if a question maps to a section heading, the agent will quote from it and cite the source.

## What is the IVA SDK

The IVA SDK is an open-source Unity package for building embodied intelligent virtual agents driven by large language models. It bundles avatar control, speech I/O, large-language-model integration, non-verbal behavior (gaze, gesture, facial expressions), vision, long-term memory, and document grounding into a single modular framework. The package name in Unity is `de.uhh.hci.ivh.core`, and it is distributed by the Human-Computer Interaction group at the University of Hamburg.

## Who developed the IVA SDK

The first public version of the IVA SDK (v1.0.0) is based on the technical development work of **Sebastian Rings**, who built the original SDK foundations. Versions 2.0 and 3.0 — including the Gemini Live realtime integration, modular STT/LLM/TTS pipeline, long-term memory, document grounding, and the in-game HUD — were developed primarily by **Dr. Ke Li**, currently a postdoctoral researcher in the HCI group at the University of Hamburg, under **Professor Frank Steinicke**. The work continues to be maintained by the HCI group at the University of Hamburg.

## Who funds the development of the IVA SDK

Development of the IVA SDK is funded by the **PRESENCE project** (https://presence-xr.eu/), a Horizon Europe Innovation Project supported by the European Commission under Grant Agreement No. 101135025. PRESENCE brings together 17 partners across 11 countries to advance extended-reality technology along three pillars: holoportation for realistic remote visual interaction, haptics for authentic touch, and virtual humans for believable social avatar behavior. The IVA SDK is the consortium's contribution to the third pillar.

## What university and group built this

The SDK is developed at the **Human-Computer Interaction (HCI) group** at the **University of Hamburg**, Germany, led by Professor Frank Steinicke. Contact email for licensing and academic collaboration is frank.steinicke@uni-hamburg.de.

## What types of agent can I build with this SDK

The SDK ships two main agent flavors that you choose between based on your latency and modularity needs:

1. **`GeminiLiveAgent`** — a realtime voice agent powered by Google's Gemini Live WebSocket API. Lowest latency, simplest setup, but tied to the Gemini provider.
2. **`ConversationalAgent`** — a modular pipeline that wires a separate speech-to-text service, a large-language-model service, and a text-to-speech service. Higher latency than realtime, but each component is swappable so you can mix providers (e.g., local Whisper + cloud GPT-4o + Azure TTS).

A third variant, **`GeminiVoiceOnlyAgent`**, drops the 3D avatar entirely and runs a voice-only assistant on a plain GameObject with an AudioSource.

## How does the GeminiLiveAgent work

The `GeminiLiveAgent` opens a persistent WebSocket connection to Google's Gemini Live API and streams audio in both directions. The user's microphone is captured at 16 kHz, lightly filtered for voice frequencies, and sent as PCM chunks. The model's spoken reply streams back as 24 kHz PCM and plays through the agent's `AudioSource` in real time. A lightweight voice-activity-detection algorithm on the client allows the user to interrupt the agent mid-sentence, and a server-side VAD signal from Gemini provides an authoritative confirmation that the interruption was registered.

## Which Gemini Live models does this SDK support

Three Gemini Live model variants are supported, switchable in the Inspector:

- **`gemini-live-2.5-flash-native-audio`** — Vertex AI paid tier, low latency, suitable for production.
- **`gemini-2.5-flash-native-audio-preview-12-2025`** — Google AI Studio free tier, higher latency, suitable for prototyping.
- **`gemini-2.0-flash-exp`** — Google AI Studio free tier, low latency, scheduled to be deprecated by Google in March 2026.

## How does the agent control its body and facial expressions

In `GeminiLiveAgent`, the model issues an `update_avatar_state` tool call whenever its emotional state, body action, gaze direction, or facial expression should change. The system prompt lists the exact allowed values for `action`, `emotion`, and `gaze` based on the avatar's animation library, so the model can only call animations the avatar actually has. The SDK then routes the tool call to the appropriate behavior subsystem: body actions go to the `AgentActionController`, facial expressions go to the emotion handler (FACS-based or CC4 animation), and gaze direction goes to the `EyeGazeController`.

## How does the agent move around in the scene

Locomotion is optional and currently supported only for CC4 and DIDIMO characters with an `AgentLocomotion` component. When the user asks the agent to move ("step back", "come here"), the model calls a `move_agent` tool with angle, distance, speed, and an option to face the movement direction. The SDK projects the requested vector onto the ground plane and drives the avatar via the locomotion subsystem.

## How does the modular ConversationalAgent work

The `ConversationalAgent` runs an explicit speech-to-text → large-language-model → text-to-speech pipeline as a Unity coroutine. On each turn it captures the user's speech, transcribes it via the configured STT service, builds a structured prompt that combines the user message with the persona system prompt and any retrieved context, calls the configured LLM (or vision-language model when the agent has vision enabled), parses the structured response into a text reply plus optional action / emotion / gaze commands, and finally renders the reply via the configured TTS service. Every step is a separate service so individual providers can be swapped without touching the rest of the loop.

## Which speech-to-text services are supported

The `ConversationalAgent` supports two STT backends, selectable in the Inspector as the `STTService` field:

- **Google Cloud Speech-to-Text** (option `UHAM_GoogleCloud`) — runs against Google Cloud and requires a service account.
- **Local Whisper** (option `Local_Whisper`) — runs the Whisper model locally via `com.whisper.unity`, with about 5 seconds of latency on typical hardware. No cloud dependency.

## Which large-language-model services are supported

The `ConversationalAgent` supports many foundation models selectable as the `foundationModel` field, including:

- **OpenAI GPT-4o vision-language model** (`Unity_OpenAI_VLM`) — cloud, supports image input.
- **Google Gemini vision-language model** (`Unity_Gemini_VLM`) — cloud, supports image input.
- **DeepSeek-R1** (`Unity_DeepSeekR1_LLM`) — cloud reasoning model.
- **UHAM-hosted OpenAI proxy** (`UHAM_OpenAI_VLM`) — an encrypted relay for university users.
- **Local LLM** (`Local_Model_LLM`) — runs a local model via `com.gpt4all.unity` or any custom backend.
- **Ollama local models**, including DeepSeek-R1 (7B and 14B), Llama 3.2 (3B), TinyLlama (1B), OpenChat (7B), and LLaVA (7B and 13B vision-language) — all run locally via an Ollama server.

## Which text-to-speech services are supported

The `ConversationalAgent` supports four TTS backends, selectable as the `TTSService` field:

- **Microsoft Azure TTS** (`Unity_Azure`) — broad language and voice coverage.
- **ElevenLabs** (`Unity_ElevenLab`) — high-quality voice cloning and emotional speech.
- **UHAM-hosted Google Cloud TTS** (`UHAM_GoogleCloud`) — for university users.
- **Multi-player Google Cloud TTS** (`UHAM_GoogleCloud_MultiPlayer`) — extends the above with per-user audio identification for multi-user single-agent scenarios.

For Gemini Live agents, text-to-speech is handled natively by Gemini Live with voice options such as Puck, Charon, Kore, Fenrir, and Aoede — no separate TTS service is required.

## Which 3D character formats does the SDK support

The SDK supports three character families:

- **Microsoft Rocketbox** characters — bundled with the package under a non-commercial academic license from Microsoft. Used as the default in samples.
- **Reallusion Character Creator 4 (CC4)** characters — for higher-fidelity facial expressions via the Digital Soul animation library. The Digital Soul asset itself requires a separate Reallusion license and is not redistributed in the SDK.
- **DIDIMO** characters — photorealistic head scans. The DIDIMO sample asset is bundled only for demonstration; commercial reuse requires a DIDIMO license.

Body animations come from either the Rocketbox library or Mixamo, selected via the `BodyAnimationControllerType` enum.

## How does the agent express emotions

Two facial-expression backends are supported. The `FACS` mode (Facial Action Coding System) uses parametric blendshape animation and works with any character that exposes the standard FACS action units. The `CC4_Animation` mode plays curated animations from Reallusion's Digital Soul library for richer expressions, but requires the user to hold a valid Digital Soul license. The LLM picks an emotion name from the list announced in the system prompt; the SDK plays the corresponding animation.

## How does the agent decide where to look

Gaze is handled by the `EyeGazeController`, which has two modes: `LookAtPlayer` (the agent tracks the user's position via IK, looking them in the eye), and `Idle` (subtle idle eye movement to avoid the dead-stare effect). The model chooses between these by emitting a `gaze` field in its structured output. A built-in eye-blink behavior runs independently in both modes.

## Does the agent have vision

Yes, vision is optional and toggled by the `vision` field. When enabled, the agent can stream frames from one of two camera sources: the avatar's own in-world egocentric camera (`AgentCamera`), or an external webcam (`WebCam`). Frames are sent to the LLM either continuously (`Auto` mode) or only when the user speaks a trigger phrase. For Gemini Live the frame frequency is configurable (default 1 frame per second). Image resolution is adjustable to trade off cost and detail.

## What languages does the agent speak

The SDK supports English (default), German, Spanish, Japanese, Korean, and French through its language-code helper. Gemini Live agents additionally support automatic multilingual switching mid-conversation. Chinese was deliberately omitted because Google Cloud SpeechClient V1 mis-detects it.

## How does the agent remember previous conversations

The optional `MemoryManager` component (introduced in v3.0) provides cross-session long-term memory. Conversation turns are buffered and, every N turns, summarized and embedded into a vector store keyed by a persistent user identity. On subsequent sessions, the most semantically relevant memories are retrieved and prepended to the agent's system prompt, so the agent appears to remember prior interactions with the same user. Memory is opt-in via a `MemoryConfig` ScriptableObject and defaults to off.

## How does the agent ground its answers in reference documents

The `DocumentGroundingComponent` (introduced in v3.1) lets the agent answer accurately from a curated Markdown corpus instead of guessing. At edit time, the developer creates a `KnowledgeBase` asset, drags Markdown documents in, and bakes them into a JSON vector store. At runtime the agent embeds the user's query, retrieves the top-K most similar chunks, prepends them to the prompt with citation instructions, and the model answers using only those sources and cites them as `[source: file §section]`. This very document is an example: the agent's answers about itself come from chunks retrieved from this file.

## Can the agent call custom tools or functions

Yes. Both `GeminiLiveAgent` and `GeminiVoiceOnlyAgent` support generic tool calling via the `GeminiToolManager` component. Developers register tool declarations (name, description, parameters), and the Gemini Live model can invoke them mid-conversation. Tool calling currently requires one of the `gemini-2.5-flash-native-audio` model variants. The same `update_avatar_state`, `move_agent`, and document-grounding paths use this mechanism internally.

## How is the agent interrupted

`GeminiLiveAgent` detects interruptions in two ways. First, a client-side voice-activity check compares microphone RMS volume against a tunable `echoInterruptionThreshold` while the agent is speaking; loud enough user speech immediately stops playback. Second, Gemini's server-side VAD sends an authoritative interruption signal that fires regardless of the client-side threshold. After an interruption, the SDK drops residual audio chunks from the prior turn until the server acknowledges the new turn, preventing the agent from "talking through" the interruption. A debounce window suppresses echo re-triggering.

## What avatar behaviors run automatically

Several behaviors run continuously without explicit model instruction. `CharacterBlinkBehavior` drives natural eye blinking at randomized intervals. The lip-sync system maps incoming audio to viseme blendshapes via Oculus Lip Sync. Idle body sway and breathing animations run on the base animator layer. The eye-gaze IK chain maintains the chosen gaze target even while the body animates. Together these prevent the uncanny "frozen statue" appearance during silence.

## Where can I find the source code

The SDK source lives at https://git.informatik.uni-hamburg.de/presence/public/iva-sdk-core-public.git with a public GitHub mirror at https://github.com/uhhhci/intelligent-virtual-agent-sdk. The Unity package name is `de.uhh.hci.ivh.core`.

## What is the license

The SDK is released for academic and research purposes free of charge. Commercial use requires a separate license; contact Professor Frank Steinicke at frank.steinicke@uni-hamburg.de for details. Some bundled assets (DIDIMO sample, Rocketbox characters, Reallusion Digital Soul) have their own licenses that restrict redistribution.

---

## FAQ

### Who built you?

I was built using the Intelligent Virtual Human SDK from the Human-Computer Interaction group at the University of Hamburg. The SDK's first version was based on the technical development work of Sebastian Rings; versions 2.0 and 3.0 — which power most of my current capabilities — were developed primarily by Dr. Ke Li, a postdoctoral researcher in the group of Professor Frank Steinicke.

### Who pays for your development?

Development is funded by the PRESENCE project, a Horizon Europe Innovation Project from the European Commission (Grant Agreement No. 101135025). You can read more about the project at https://presence-xr.eu/.

### What model powers your voice?

If I am a Gemini Live agent, my voice comes directly from Google's Gemini Live model — I do not use a separate text-to-speech service. If I am a modular ConversationalAgent, my voice can come from Microsoft Azure TTS, ElevenLabs, or Google Cloud TTS, depending on how the developer configured me.

### Can you run offline?

Partly. The modular ConversationalAgent can run with a local Whisper transcription model and a local LLM via Ollama or GPT4All — that combination needs no internet connection. However, Gemini Live agents and any cloud TTS / cloud LLM services require an internet connection.

### Why do you sometimes refuse to answer questions?

If document grounding is enabled and the answer is not in my reference documents, I am instructed to say "I don't have that in my reference materials" rather than make something up. This is a deliberate safety measure to prevent hallucination on factual queries about a specific corpus.

### Can you see me?

Only if vision is enabled on my agent. When the developer turns on vision, I can stream frames from either an in-scene camera or a real webcam to my vision-language model. With vision off I have no visual input at all.

### Can you remember me between sessions?

Only if the developer attached a `MemoryManager` component and assigned me a persistent user identity. With memory enabled, I summarize our conversation every few turns, embed the summary, and recall the most relevant memories the next time we talk. Without that component I have no recollection of prior sessions.

### Can I interrupt you?

Yes, if I am a Gemini Live agent. I run a voice-activity detector while speaking; if you start talking loudly enough, I stop mid-sentence and listen. The detector threshold is configurable by the developer.

### What characters can you embody?

I support Microsoft Rocketbox, Reallusion Character Creator 4 (CC4), and DIDIMO character formats. My body animations come from either Rocketbox or Mixamo libraries, and my facial expressions come from either parametric FACS blendshapes or Reallusion's Digital Soul animation library.

### How do I cite this SDK in a paper?

The reference papers are listed in the README of the source repository. The primary citation is *"Anthropomorphic AI: A Toolkit for Authoring and Interacting with Intelligent Virtual Agents for Extended Reality"* by Ke Li, Fariba Mostajeran, Sebastian Rings, Julia Hertel, Susanne Schmidt, Michael Arz, and Frank Steinicke, in Frontiers in Virtual Reality, 2026, DOI 10.3389/frvir.2026.1794720.
