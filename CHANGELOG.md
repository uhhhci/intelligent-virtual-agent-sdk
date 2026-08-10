# Intelligent Virtual Human SDK Core (v3.0.0)
All notable changes to this package are documented in this file.

## 3.0.0
First 3.x public release, consolidating everything developed since `v2.3.2`. The headline feature is
**document grounding**: an agent can now answer from a curated corpus you supply instead of from
whatever the model absorbed in pre-training. Alongside it, the developer-facing surface was
substantially reworked — a real in-game HUD, a setup wizard, structured logging, and an exception
hierarchy.

**No API breaks.** Every addition is opt-in; a project that adds none of the new components behaves
exactly as it did on `v2.3.2`.

### Document grounding and knowledge bases

Three grounding strategies ship, covering the range from "one small document" to "corpus larger than
the context window". They all implement the same `IContextProvider` contract and can be combined on
one agent.

- **`KnowledgeBase` (ScriptableObject) + editor-time baking.** Drag Markdown `TextAsset`s into a
  `KnowledgeBase` asset, set chunking and retrieval parameters, and bake via *IVA SDK → Knowledge →
  Bake Selected KnowledgeBase* (or the **Bake Now** button). `KnowledgeBaker` parses, chunks, and
  embeds at editor time and writes a JSON store to `Assets/StreamingAssets/IVA_Knowledge/`, so the
  runtime never calls the embeddings API for ingestion — only to embed the live query. Markdown only
  in this release.
- **Real-time RAG via the Gemini embedding model (`DocumentGroundingComponent`).** Embeds the user's
  query with `gemini-embedding-001`, runs an in-memory cosine search over the baked store, and
  prepends the top-K chunks. `ConversationalAgent` retrieves per turn (~150–300 ms, dominated by the
  LLM call anyway); Gemini Live agents retrieve once at `Connect()`, because their system
  instruction is a one-shot at session setup.
- **Per-turn retrieval as a function tool (`KnowledgeRetrievalTool`).** Exposes the knowledge base to
  Gemini Live as a callable `search_knowledge(query)` function, so a realtime agent retrieves with
  the user's *actual* question every turn rather than a generic seed at connect. This is what makes
  grounding scale to arbitrarily large corpora. Auto-registers with the `GeminiToolManager` on the
  same GameObject — `[DefaultExecutionOrder(-100)]` guarantees the declaration is injected before the
  manager reads its list, so no manual Inspector wiring is needed. Costs one extra model round-trip
  on turns where the model chooses to search; other turns are unaffected.
- **Whole-document injection via prompt (`FullDocumentContextProvider`).** Injects the *entire* text
  of its documents into the prompt — no chunking, embedding, retrieval, or bake step. For corpora
  that fit the context window this is strictly more reliable than top-K retrieval: every fact is
  always present, there is no ranking step that can miss the answer, and per-turn latency is zero.
  `maxContextChars` guards against runaway inputs.
- **Pluggable pipeline.** New `IDocumentReader`, `IChunker`, `IContextProvider`, `IEmbedder`, and
  `IMemoryStore` interfaces, with `MarkdownReader`, `SlidingWindowChunker`, `GeminiEmbedder`, and
  `JsonFileMemoryStore` as the shipped implementations. Swap in a self-hosted embedder or a remote
  vector DB without touching the agent.
- **`AgentBase.BuildContextPrefixAsync(querySeed)`.** New `public virtual` hook that aggregates every
  `IContextProvider` on the agent GameObject in component order. `MemoryManager` now implements the
  same interface, so long-term conversation memory and document grounding compose automatically on a
  single agent with no explicit wiring.
- **Natural-speech citations by default.** `CitationPromptFormatter.DefaultInstructionTemplate`
  treats retrieved chunks as background knowledge and explicitly forbids verbalizing `[source: …]`
  markers — reading citation markers aloud is robotic for a voice agent. Opt into strict, visible
  citations with `StrictCitationInstructionTemplate` for chat-log UIs.
- **`DocumentGroundingComponent.injectAtSessionStart` (default `true`).** Turn off to use the
  component purely as the retrieval backend for a `KnowledgeRetrievalTool`, without also injecting a
  generic one-shot prefix. Also adds `SearchAsync(query)` for on-demand lookups and
  `CitationPromptFormatter.FormatSourcesOnly(...)` for formatting tool results.
- **Key handling.** `DocumentGroundingComponent` and `KnowledgeBaker` read the Gemini key from the
  standard `~/.aiapi/auth.json` used by every other Gemini service in the SDK, with an `EditorPrefs`
  fallback. The per-component Inspector field is an optional override, not a requirement, so keys are
  never committed with a scene.

### Developer experience

- **In-game HUD for `GeminiLiveAgent`.** A dual-panel scaffolder (*UI tab → Create Dual-Panel HUD*)
  generates a transcription panel and a settings panel, each toggled from the top-left. Settings
  covers Reconnect, microphone selection, camera source and live preview, vision on/off,
  stream-frequency slider, vocal-interruption toggle, prevent-echo toggle, and Force Interrupt. Both
  panels are draggable and resizable at runtime. `GeminiLiveAgentUIControls` wires every reference,
  and all of them are optional.
- **Reusable HUD primitives.** `UIDragHandle` and `UIResizeHandle` under `IVH.Core.UI` —
  anchor-agnostic and usable on any panel, not just the generated ones.
- **Camera preview.** Mirrors the webcam or the agent's egocentric `RenderTexture`. A standalone
  *Create Webcam Preview Canvas* button is also available. `AgentBase.EnsureWebCamReady(deviceName)`
  lazy-allocates the agent's `WebCamTexture`.
- **In-game conversation log.** `showSpeechTranscripts`, `logTextDisplay`, and `scrollRect` on the
  agent; enabling it turns on server-side transcription automatically.
- **Setup tooling.** A unified `IVA SDK` menu. The *Setup Wizard* handles dependencies, credentials,
  and sanity checks in one window. `IVADependencyBootstrap` prompts on first launch for the git-URL
  packages UPM cannot resolve transitively; Unity-registry dependencies install automatically via
  `package.json`.
- **Optional local packages no longer break compilation.** `com.gpt4all.unity` and
  `com.whisper.unity` are gated behind `IVA_HAS_GPT4ALL` / `IVA_HAS_WHISPER` versionDefines, so
  projects without them compile cleanly.
- **Structured logging.** `IVALogger` with `LogLevel` and `IVALogConfig` replaces scattered
  `Debug.Log` calls, so SDK output can be filtered by category and severity instead of drowning the
  Console.
- **Exception hierarchy.** `IVAException`, `AuthException`, `ServiceConnectionException`,
  `ToolExecutionException`, and `DisconnectReason` under `IVH.Core.Exceptions`, so failures are
  catchable by type rather than by string-matching log output.
- **Editor test suite.** `Tests/Editor` covers the chunker, the Markdown reader, the citation
  formatter, dynamic tool serialization, and the logger.
- **Full XMLDoc** on the public API surface.

### Runtime additions

- **`GeminiToolManager` now forwards tool return values.** `HandleDynamicToolCall` previously
  discarded each tool method's return value and always replied `{status:"success"}`. It now awaits
  `Task` / `Task<T>` and returns `{status:"success", result:…}` — which is what allows
  `search_knowledge` to return retrieved chunks. Backward compatible: `void` and non-generic `Task`
  tools (the avatar and locomotion tools) still receive the bare acknowledgment.
- **`GeminiLiveAgent.proactiveAnswerStyle` (default `true`).** Appends a conversation-style block
  instructing the agent to answer directly and substantively in the first person rather than ending
  every reply with a clarifying question. This counteracts the "Is there a specific aspect you're
  interested in?" stalling loop. Unlike the other additions this defaults **on**, because it fixes a
  conversational defect; set it `false` to restore the bare prompt.
- **Long-term memory.** `IVH.Core.Memory` with `MemoryManager`, `MemoryItem`, `MemoryConfig`,
  `GeminiEmbedder`, and `JsonFileMemoryStore` for cross-session conversation memory.
- **Session recording and metrics.** `SessionRecorder`, `SessionRecorderConfig`, `SessionEvent`, and
  `MetricsCollector` under `IVH.Core.Observability`.
- **`AgentPreset`.** Serialize a full agent configuration to an asset and apply it to any agent, with
  a generator under the Presets editor menu.
- **Connection lifecycle and resilience.** `GeminiRealtimeWrapper` gained `OnConnected`,
  `OnDisconnected`, `OnReconnecting`, and `OnFatalError` events, plus auto-reconnect with exponential
  backoff.
- **Server-side transcription.** `enableTranscription` and `OnUserTranscriptReceived` on the realtime
  agents.
- **`GeminiVoiceOnlyAgent`** gained transcription support and a substantially reworked interruption
  path.

### Realtime session fixes

Three long-standing defects in `GeminiRealtimeWrapper` surfaced once the knowledge samples exercised
tool calling and large prompts. All three are fixed; none change a public API.

- **Vertex AI could not connect at all when the agent had a tool attached.** `ConnectAsync` performed
  the Vertex service-account handshake, but `ConnectWithDynamicToolsAsync` — the path taken whenever
  a `GeminiToolManager` has any registered tool — built its own URI and skipped it entirely: no OAuth
  token was fetched, so the `Authorization` header was empty, and the model was addressed by its bare
  id instead of the required
  `projects/{project}/locations/{location}/publishers/google/models/{model}` resource path. The
  session never reached `setupComplete` and the agent hung on "Connecting…" forever. This is why the
  RAG sample worked on AI Studio but not on Vertex. Both connect paths now share
  `ResolveEndpointAsync` / `OpenSocketAsync`, so the handshake is identical with or without tools —
  and a reconnect refreshes the Vertex token, which it previously did not.
- **Thinking was only disabled on Vertex, making AI Studio markedly slower.** Gemini 2.5 runs
  *dynamic thinking by default*, inserting a reasoning pass before each spoken reply — the largest
  single source of turn latency in a realtime voice session. `thinking_budget = 0` is now sent on
  both backends, controlled by the new `disableThinking` field (default on). Untick it to get the
  reasoning pass back.
- **Context-window compression was only applied on the no-tools path.** An agent with a tool attached
  silently ran without `context_window_compression` and would hit the session context limit that an
  otherwise identical toolless agent survived. Both paths now build their setup payload through one
  `BuildSetupContent` helper, so compression, thinking, transcription, and voice config can no longer
  diverge. The compression target is exposed as `slidingWindowTargetTokens` (default 12800, unchanged
  behaviour) — raise it if long sessions develop periodic stalls.

- **Connection failures are now visible instead of silent.** Missing credentials, a failed Vertex
  auth, and a refused socket previously only reached the Console, leaving the HUD on "Connecting…".
  They now raise `OnFatalError` with an actionable `AuthException` / `ServiceConnectionException`,
  which `GeminiLiveAgent` and `GeminiVoiceOnlyAgent` render as a red `System: Connection failed` line
  while stopping the mic and vision streams. A new `setupTimeoutSeconds` watchdog (default 20 s)
  covers the remaining case where the server accepts the socket but never acknowledges setup.

### Samples

- **Knowledge Grounding — RAG Retrieval (Gemini Embeddings)** *(new)*. A `GeminiLiveAgent` that calls
  `search_knowledge` per turn against a baked corpus. Ships a fictional two-document corpus
  describing an invented deep-ocean research station, sized so retrieval visibly matters. Requires
  baking.
- **Knowledge Grounding — Long Document via Prompt** *(new)*. A `GeminiLiveAgent` grounded by
  injecting a whole fictional curator dossier into the prompt. No baking, no embedding, one
  component. Demonstrates the zero-setup end of the grounding range.
- **Knowledge Grounding — Reference Corpus.** A Markdown corpus describing the SDK itself, for
  grounding an agent that can explain how it works.
- **Live Stream Agent.** The Gemini Live realtime voice-and-vision scene, now registered as a proper
  sample rather than an unlisted folder.
- **Quick Start (Code-First).** `HelloAgent`, `ToolCalling`, and `CustomCallbacks` — minimal code
  samples complementing the scene-based ones.

Both new knowledge samples use entirely fictional documents. No real person, organisation, place, or
event is described in any sample corpus.

### Choosing a grounding strategy

| Corpus | Use |
|---|---|
| Fits the context window, static | `FullDocumentContextProvider` — total recall, zero latency, no bake |
| Large or growing, realtime agent | `KnowledgeRetrievalTool` + `DocumentGroundingComponent` (`injectAtSessionStart` off) |
| Large, `ConversationalAgent` | `DocumentGroundingComponent` alone — it already retrieves per turn |

Start with whole-document injection and move to retrieval when the corpus stops fitting, not before.

### Notes and known limitations

- **Markdown only.** PDF, plain text, and `.docx` will plug in behind the existing `IDocumentReader`
  interface without architectural change.
- **Embedder dimension is locked at bake time.** Re-baking with a different embedder requires
  re-baking the whole corpus; the runtime refuses to load on mismatch rather than returning garbage.
- **Re-scaffolding the HUD** leaves the previous GameObjects behind for manual removal.
- **Pin `#v2.3.3`** if you need to avoid the HUD redesign.


## 2.3.3
  - added echo prevention in ``GeminiVoiceOnlyAgent``

## 2.3.2
  - enabled toggle vision capability

## 2.3.1
  - added ``muteMicWhileTalkingProp`` to stop the IVA from listening to the echos and allow better experiences when not wearing headphones. 
  
## 2.3.0
  - [EXPERIMENTAL] Integrate ``AgentLocomotion`` with basic locomotion functionalities to ``GeminiLiveAgent``. Currently only support mixamo animations and internal use only. 

## 2.2.1
  - [EXPERIMENTAL] Add generic tool calling to ``GeminiLiveAgent`` as well, this is only supported in ``gemini-2.5-flash-native-audio-preview-12-2025``
  - Make vocal interruption optional parameter set by developer. 
  - Set agent start greeting first. Change system prompt to reduce double response scenarios

## 2.2.0
  - [EXPERIMENTAL] Add generic tool calling to ``voiceOnlyAgent``
  - Only support in ``gemini-2.5-flash-native-audio-preview-12-2025``
  - To be added in embodied IVA later

## 2.1.0

- Update avaliable models in ``GoogleCloudAIWrapper``.
- Add ``voiceOnlyAgent`` and it's editor script.
- Fix json import error in ``GeneralModelHelper``.

## 2.0.0
- **Gemini Live 2.5 Flash API Integration**
  - Integrated **Gemini Live 2.5 Flash** from **Vertex AI** *(paid tier, low latency)* using  
    [`gemini-live-2.5-flash-native-audio`](https://docs.cloud.google.com/vertex-ai/generative-ai/docs/live-api).
  - Integrated **Gemini Live 2.5 Flash** from **Google AI** *(free tier, higher latency)* using  
    [`gemini-2.5-flash-native-audio-preview-12-2025`](https://ai.google.dev/gemini-api/docs/live?example=mic-stream).
  - The previous integration with  
    `gemini-2.0-flash-exp` *(free tier, low latency)* will be **deprecated and terminated by Google AI Studio in March 2026**.

- **Dynamic IVA Interruption**
  - Added support for dynamically interrupting the Intelligent Virtual Agent (IVA) using a **lightweight Voice Activity Detection (VAD)** algorithm.

- **System Prompt Fixes**
  - Fixed typos and resolved bugs in the system prompt.

- **Affective & Multilingual Dialogue**
  - Enabled **affective dialogue** (emotion-aware responses).
  - Added **automatic multilingual language switching**, supported by the  
    `gemini-live-2.5-flash` model family.

- **Extended Context via Sliding Windows**
  - Implemented **sliding window context management** to extend context length during long-running sessions.

- **Multimodal Image Streaming**
  - Added support for **streaming image inputs** for real-time multimodal understanding.

- **Sample Scenes**
  - Added sample scenes with webcame streaming 

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
