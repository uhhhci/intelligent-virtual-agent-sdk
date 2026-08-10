# Sample — Long-document retrieval via prompt

A `GeminiLiveAgent` grounded by injecting **the entire document into the prompt** at session start.
No chunking, no embedding, no baking, no retrieval step. Every fact in the document is always in
context.

For corpora that fit the model's context window this is more reliable than top-K retrieval: there is
no ranking step that can miss the relevant passage, and there is zero per-turn latency.

> **The document is fictional.** [`CuratorDossier_AureliaVance.md`](CuratorDossier_AureliaVance.md)
> describes an invented curator at an invented museum. No real person, museum, instrument, or event
> is described, and every named individual is a fictional character.

## Scene

`CuratorLongDocumentAgent.unity` — a Rocketbox character embodying Aurelia Vance, curator of the
Hollowmere Museum of Invented Instruments. The in-game HUD (transcription panel + settings panel) is
already scaffolded in.

### Audio defaults in this scene

**Vocal interruption is off, and echo prevention is on.** These are the safe defaults for a laptop or
desktop with speakers:

| Setting | Value | Why |
|---|---|---|
| `Enable Vocal Interruption` | **off** | Interruption works by listening for your voice *over* the agent. On speakers the agent's own output crosses the threshold and it interrupts itself mid-sentence, repeatedly. |
| `Mute Mic While Talking` | **on** | Mutes the microphone while the agent speaks, so it cannot hear its own echo. |

**If you are wearing headphones**, turn `Enable Vocal Interruption` **on** and `Mute Mic While
Talking` **off** for a much more natural conversation. The two settings are mutually exclusive:
muting the mic while the agent talks means there is nothing to interrupt with. Both are toggleable at
runtime from the HUD's **Conversation** section.

> **Expect slower turn-taking in this configuration.** With interruption off and the mic muted during
> playback, your speech is discarded for the entire time the agent is speaking — Gemini's server-side
> VAD hears silence, so the handover cannot begin until playback fully finishes. That is inherent to
> the speaker-safe setup, not a fault. The headphone configuration above gives you immediate handover.

`Show Speech Transcripts` is **off**, which is the SDK default — it is what gates
`GeminiRealtimeWrapper.enableTranscription`, and that flag is off by default for Live API
compatibility. The HUD's transcription panel therefore stays empty. Tick it on the agent if you want
transcripts, but treat it as a change to the session setup message, not a cosmetic toggle.

### Which backend to run this on

The scene ships on **Google AI Studio** (`Gemini Realtime Wrapper → Selected Model =
Flash25PreviewGoogleAI`) because it needs nothing but an API key. Both backends work here.

**Expect slower, more variable turn-taking on AI Studio than on Vertex.** That is the free tier's
shared preview capacity, not the document: this dossier is ~9 KB, about 2.5k tokens, which is trivial
next to the model's context window. A realtime session streams audio continuously and so consumes
tokens every second it is open; an injected corpus raises the baseline that continuous audio then
builds on, and once you approach the free tier's per-minute ceiling the service throttles instead of
erroring — so it reads as sluggishness rather than a failure. Check your live quota on the
[AI Studio rate-limit dashboard](https://aistudio.google.com/rate-limit).

Switch `Selected Model` to `Flash25VertexAI` for demos or user studies where responsiveness matters.
It bills per session and needs `~/.aiapi/service_account.json` — see the main README's Vertex setup.

> Since v3.0 the SDK sends `thinking_budget = 0` on **both** backends (`Disable Thinking`, on by
> default). Through v2.3.x only Vertex got it, so AI Studio ran with Gemini 2.5's dynamic thinking
> enabled and paused to reason before every spoken reply. If you want that reasoning back, untick
> `Disable Thinking` on the `Gemini Realtime Wrapper`.

## How the components fit together

| Component | Role |
|---|---|
| `FullDocumentContextProvider` | Reads the `TextAsset` list and injects the whole corpus as a prompt prefix. That is the entire mechanism. |

There is no `KnowledgeBase` asset, no `DocumentGroundingComponent`, and no `GeminiToolManager` in
this scene. That is the point — this mode has one component and no build step.

## Setup

1. Set your Google AI Studio key via **`IVA SDK → Setup Wizard → Credentials`** (needed for the
   agent itself, not for grounding — there is no embedding call in this mode).
2. Open the scene and press Play.

That is the whole setup. There is nothing to bake.

## Component settings used in this scene

| Field | Value | Why |
|---|---|---|
| Inject Full Corpus | ✓ | Master switch |
| Knowledge Base | *(empty)* | Not needed — documents are listed directly |
| Documents | `CuratorDossier_AureliaVance` | The single source document |
| Instruction Template | *(default)* | First-person framing, suppresses spoken file names and headings |
| Max Context Chars | 200000 | Safety cap only; this document is ~9 KB |

The default `Instruction Template` matters more than it looks. It tells the model to treat the
document as **its own memory** and answer in the first person, which is why the agent *becomes*
Aurelia rather than describing her. If you swap in a document that is reference material rather than
a persona, rewrite the template accordingly.

## Try asking

Because the whole document is in context, the agent should be equally sharp on details from the
beginning and the end of it — which is exactly what this mode buys you:

- *"What's the strangest thing in your museum?"* → the 1904 bicycle-driven handbells, not the Tower Harp.
- *"Why is the Corrigan Breath Organ your favourite?"* → because the failure is physiological, not musical.
- *"Can I touch anything?"* → no, except the reproduction Corrigan mouthpiece at the entrance.
- *"Tell me about Desmond."* → the conservator of 22 years who thinks she displays too much.
- *"Has anything ever been un-failed?"* → the double-chantered practice pipe.
- *"What's a good investment in the instrument market?"* → should decline; explicitly out of scope
  in the dossier's final section.

That last one tests the "what I do not know" section. A well-behaved run declines rather than
inventing.

## Choosing between the two modes

| | This sample (prompt injection) | [`../KnowledgeRAGRetrievalDemo/`](../KnowledgeRAGRetrievalDemo/) (RAG) |
|---|---|---|
| Build step | None | Bake required, re-bake on every edit |
| Corpus size limit | Must fit context (~200 KB default cap) | Effectively unbounded |
| Per-turn latency | Zero | One embedding call + possible extra model round-trip |
| Recall | Total — every fact always present | Top-K; a bad ranking can miss the answer |
| Cost per session | Larger prompt each session | Smaller prompt, plus embedding calls |
| Best for | Personas, single reports, briefing docs | Manuals, large corpora, many documents |

Rule of thumb: start here. Move to RAG when the corpus stops fitting, not before.

## Combining both

Both providers implement `IContextProvider`, so you can put a `FullDocumentContextProvider` (persona)
and a `DocumentGroundingComponent` (large reference corpus) on the same agent. `AgentBase` aggregates
every provider on the GameObject in component order.

See [`Documentations~/howToGroundAgentInDocuments.md`](../../Documentations~/howToGroundAgentInDocuments.md)
for the full guide.
