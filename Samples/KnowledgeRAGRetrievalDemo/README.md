# Sample — Real-time RAG retrieval (Gemini embedding model)

A `GeminiLiveAgent` that answers from a baked document corpus by **calling a retrieval tool on every
turn that needs it**. The user's actual question is embedded with `gemini-embedding-001`, the closest
chunks are retrieved from the baked store, and only those chunks enter the prompt.

This is the scalable grounding mode. The corpus can be far larger than the context window, because
nothing is injected until the model asks for it.

> **The corpus is fictional.** [`MeridianStation_Operations.md`](MeridianStation_Operations.md) and
> [`MeridianStation_Research.md`](MeridianStation_Research.md) describe an invented deep-ocean
> research station. No real place, organisation, or person is described. Every named individual is
> a fictional character.

## Scene

`MeridianStationRAGAgent.unity` — a Rocketbox character acting as Meridian Station's operations
assistant. The in-game HUD (transcription panel + settings panel) is already scaffolded in.

### Audio defaults in this scene

**Vocal interruption is off, and echo prevention is on.** These are the safe defaults for a laptop or
desktop with speakers:

| Setting | Value | Why |
|---|---|---|
| `Enable Vocal Interruption` | **off** | Interruption works by listening for your voice *over* the agent. On speakers the agent's own output crosses the threshold and it interrupts itself mid-sentence, repeatedly. |
| `Mute Mic While Talking` | **on** | Mutes the microphone while the agent speaks, so it cannot hear its own echo. |

**If you are wearing headphones**, turn `Enable Vocal Interruption` **on** and `Mute Mic While
Talking` **off** for a much more natural conversation — you can then cut the agent off mid-sentence.
The two settings are mutually exclusive: muting the mic while the agent talks means there is nothing
to interrupt with. Both are also toggleable at runtime from the HUD's **Conversation** section, so
you can try each without leaving Play mode.

> **Expect slower turn-taking in this configuration.** With interruption off and the mic muted during
> playback, your speech is discarded for the entire time the agent is speaking — Gemini's server-side
> VAD hears silence, so the handover cannot begin until playback fully finishes. That is inherent to
> the speaker-safe setup, not a fault. On headphones, the headphone configuration above gives you
> immediate handover.

`Show Speech Transcripts` is **off**, which is the SDK default — it is what gates
`GeminiRealtimeWrapper.enableTranscription`, and that flag is off by default for Live API
compatibility. The HUD's transcription panel therefore stays empty. Tick it on the agent if you want
transcripts, but treat it as a change to the session setup message, not a cosmetic toggle.

### Which backend to run this on

The scene ships on **Google AI Studio** (`Gemini Realtime Wrapper → Selected Model =
Flash25PreviewGoogleAI`), which needs only an API key — the same key the bake step uses.

**Both backends support function calling, so `search_knowledge` works on either.** If you tried this
scene on Vertex on an earlier build and it hung on "Connecting…", that was an SDK bug, not a Vertex
limitation: the connect path used for agents with tools skipped Vertex authentication entirely, so
the session never started. Fixed in v3.0 — see the changelog. Vertex is the better choice here, because RAG
adds an embedding call and a tool round-trip on top of normal turn latency, and Vertex absorbs that
far better than the free tier does.

Switch `Selected Model` to `Flash25VertexAI` and put a service-account key at
`~/.aiapi/service_account.json` — see the main README's Vertex setup. Note that the **query embedding
call always uses the AI Studio API key** from `~/.aiapi/auth.json`, whichever realtime backend you
pick, so keep that file in place.

On AI Studio, expect slower and more variable turn-taking. The corpus is not the cause — only the
retrieved top-K chunks ever enter the prompt, which is the point of this mode — it is the free tier's
shared preview capacity and per-minute token ceiling. Check your live quota on the
[AI Studio rate-limit dashboard](https://aistudio.google.com/rate-limit).

## How the components fit together

| Component | Role |
|---|---|
| `KnowledgeBase` (asset) | Lists the two markdown documents and holds chunking/retrieval settings. Must be **baked**. |
| `DocumentGroundingComponent` | The retrieval backend. `Inject At Session Start` is **off** — it does not push a one-shot prefix, it only answers `SearchAsync` calls. |
| `KnowledgeRetrievalTool` | Registers a `search_knowledge` function with Gemini and forwards calls to the grounding component. |
| `GeminiToolManager` | The function-calling plumbing Gemini Live uses to invoke the tool. |

The key detail is `Inject At Session Start = false`. Leaving it on would *also* inject a generic
one-shot prefix at `Connect()`, which is wasteful here — the whole point of this sample is per-turn
retrieval driven by the real question.

## Setup

1. Set your Google AI Studio key via **`IVA SDK → Setup Wizard → Credentials`** (writes
   `~/.aiapi/auth.json`). The same key is used for baking and for runtime query embedding.
2. Select `MeridianStationKnowledgeBase.asset` in the Project window.
3. Click **Bake Now**. This embeds every chunk and writes
   `Assets/StreamingAssets/IVA_Knowledge/MeridianStationKnowledgeBase.knowledge.json`.
   Expect 20–40 seconds for this corpus.
4. Confirm the Inspector status line reads something like `31 chunks · dim 768 · baked …`.
5. Open the scene and press Play.

Re-bake whenever you edit either markdown file or change the chunking parameters.

## Recommended KnowledgeBase settings

| Field | Value | Why |
|---|---|---|
| Chunk Char Size | 1200 | Both documents use short, topic-headed sections |
| Chunk Char Overlap | 200 | ~15 % overlap preserves context across boundaries |
| Retrieval Top K | 4 | Enough to cover a question spanning both documents |
| Min Similarity | 0.45 | Filters off-topic queries so the agent says "I don't know" cleanly |
| Max Context Chars | 4000 | Comfortable for Gemini Live |
| Citation Instruction Template | *(blank)* | Default = natural speech, no spoken citation markers |

## Try asking

Questions whose answers sit in different sections, so you can watch retrieval pick the right chunk:

- *"How long can the station run without the umbilical?"* → 34 hours full load, ~96 hours reduced.
- *"Who authorises a dive?"* → Sam Okonjo, and only with a verified acoustic modem link.
- *"Which research programme uses the most bandwidth, and why is that a problem?"* → LUME, 240 GB/day,
  which is why the nightly data push runs during quiet hours.
- *"What happens to the cold store during a load shed?"* → 11 hours sealed, under 90 minutes if
  repeatedly opened.
- *"What's the weather like in Hamburg?"* → should decline; it is not in the corpus.

The last one is the important test. If the agent answers it confidently, raise `Min Similarity`.

## Watching the tool fire

Enable verbose logging (**`IVA SDK → Logging`**, or set `IVALogConfig` to `Debug`) and you will see a
`search_knowledge` tool call in the Console on turns where the model decides it needs the corpus.
Turns it can answer from conversation alone will not trigger a call — that is correct behaviour, not
a fault.

If the agent never connects at all, the HUD now shows a red `System: Connection failed` line with the
cause rather than sitting on "Connecting…" — a missing key, an unauthorised service account, or a
setup message the server rejected. The wrapper gives up after `Setup Timeout Seconds` (default 20 s).

## When to prefer the other mode

If your corpus comfortably fits the context window and you want *every* fact guaranteed present with
zero per-turn latency, use [`../LongDocumentPromptDemo/`](../LongDocumentPromptDemo/) instead.

See [`Documentations~/howToGroundAgentInDocuments.md`](../../Documentations~/howToGroundAgentInDocuments.md)
for the full guide.
