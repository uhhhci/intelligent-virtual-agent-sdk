# How to ground an agent in reference documents (v3.0+)

By default an agent answers from whatever the LLM saw during pre-training plus its persona system prompt. For research scenarios where the agent must answer accurately from a specific corpus (a proposal, a technical report, a study protocol), v3.0 adds **document grounding** in three flavours.

**Pick the strategy first — it determines whether you need any of the baking steps below.**

| Strategy | Component(s) | Bake? | Corpus limit | Per-turn cost |
|---|---|---|---|---|
| **Whole document via prompt** | `FullDocumentContextProvider` | No | Must fit the context window | None |
| **Real-time RAG, per turn** | `KnowledgeRetrievalTool` + `DocumentGroundingComponent` | Yes | Effectively unbounded | 1 embedding call + a possible extra model round-trip |
| **One-shot RAG at connect** | `DocumentGroundingComponent` alone | Yes | Unbounded | None for realtime agents; per turn for `ConversationalAgent` |

**Start with whole-document injection.** It has no build step, no ranking step that can miss the answer, and zero latency — see [Whole-document injection](#whole-document-injection-no-baking) below, which is a two-minute setup. Move to retrieval only when the corpus stops fitting.

The RAG path works like this:

1. You bundle one or more Markdown documents into a `KnowledgeBase` asset.
2. An editor-time *bake* step parses, chunks, and embeds the corpus into a JSON store that ships with your build.
3. At runtime a `DocumentGroundingComponent` embeds the query, retrieves the top-K most relevant chunks, and prepends them to the agent's prompt with instructions to stay inside the corpus.

Markdown (`.md`, `.markdown`) is the only supported format in v3.0. PDF / `.docx` will plug in behind the existing `IDocumentReader` interface without an architectural change.

---

## Whole-document injection (no baking)

If your corpus fits the model's context window, skip everything below. Add a **`FullDocumentContextProvider`** to the agent GameObject, drag your `.md` TextAssets into its **Documents** list, and press Play. There is no asset to create, nothing to bake, and no embedding key needed for grounding.

Every fact is always in context, so there is no retrieval step that can rank the relevant passage out of the prompt. The `maxContextChars` field (default 200000, roughly 50k tokens) is a safety cap — if the corpus exceeds it the provider truncates and logs a warning, which is your signal to move to the RAG path.

The default `instructionTemplate` frames the corpus as the agent's *own memory* and asks it to answer in the first person, which suits persona documents. For reference material, rewrite the template; the token `{documents}` is replaced with the corpus text.

<img src="./images/knowledge_fulldocument_inspector.png" alt="Full Document Context Provider inspector" width="720" />

*The whole of Method A: one component on the agent GameObject. `Documents` takes the `.md` TextAssets
directly — there is no `KnowledgeBase` asset and nothing to bake.*

Worked example: `Samples/LongDocumentPromptDemo/`.

---

## Prerequisites

- Unity 2022.3 LTS or above.
- A **Google AI Studio API key** with access to `gemini-embedding-001` ([get one here](https://aistudio.google.com/apikey)). The embedding API is used both at bake time (to embed the corpus) and at runtime (to embed each user query). Costs are typically negligible — embeddings are far cheaper than chat completions.
- One or more Markdown files imported into your project as `TextAsset` (Unity auto-imports any `.md` placed under `Assets/`).

---

## Step 1 — Make sure your Gemini API key is configured

Document grounding reads the same `~/.aiapi/auth.json` file that every other Gemini service in this SDK uses — there is no separate key field to manage.

If you already use `GeminiLiveAgent`, you're done — your existing setup works. If not, set the key up via the IVA Setup Wizard:

1. Menu: **`IVA SDK → Setup Wizard`**
2. Open the **Credentials** tab.
3. Paste your Google AI Studio key. The wizard writes `C:\Users\<you>\.aiapi\auth.json` with the correct JSON shape:
   ```json
   {
     "gemini_api_key": "PASTE_YOUR_API_KEY_HERE"
   }
   ```

<img src="./images/knowledge_setup_wizard_credentials.png" alt="IVA SDK Setup Wizard, Credentials tab" width="720" />

*`IVA SDK → Setup Wizard → Credentials`. The API Key field is masked, and the same key covers both
baking and runtime query embedding.*

The file lives outside your Unity project, so it is never committed to source control.

**Fallback (optional):** if you can't use `~/.aiapi/auth.json` for some reason, you can store the key in `EditorPrefs` instead via `IVA SDK → Knowledge → Set Embedder API Key...`. The baker tries `auth.json` first and falls back to `EditorPrefs` if no key is found there.

---

## Step 2 — Create a KnowledgeBase asset

In the Project window, right-click on the folder you want the asset in and choose
**`Create` → `IVA SDK` → `Knowledge Base`**.

**Where to look:** the `Create` submenu is long, and `IVA SDK` is *not* in the alphabetical block near
the top. It sits in the last group at the **bottom of the list**, directly under `Meta` and `Oculus`
and just above `Input Actions`. `Knowledge Base` is then the **last entry** of the `IVA SDK` submenu,
below `Memory Config`. Both are boxed in orange below.

<img src="./images/knowledge_create_asset_menu.png" alt="Create → IVA SDK → Knowledge Base, with both menu entries highlighted" width="720" />

Then:

1. Name it after your corpus, e.g. `ProposalKnowledge`.
2. In the Inspector:
   - Check **Enabled**.
   - Drag your `.md` TextAssets into the **Markdown Documents** list.
   - Tune the **Chunking** and **Retrieval** parameters if you want to deviate from the defaults:

| Field | Default | What it controls |
|---|---|---|
| `chunkCharSize` | 1200 | Target chunk length in characters. Smaller = finer retrieval, more chunks, more embedding cost at bake time. |
| `chunkCharOverlap` | 200 | Characters shared between consecutive chunks. Helps preserve context across boundaries. |
| `retrievalTopK` | 4 | How many top chunks to inject into the prompt per query. |
| `minSimilarity` | 0.0 | Discard hits below this cosine score. **Raise to ~0.45 in practice** — at 0 the agent always gets *something* back, even for a completely off-topic question, and will happily answer from it. |
| `maxContextChars` | 4000 | Hard ceiling on the prefix length. Lower this for small-context local LLMs (Ollama, GPT4All). |
| `citationInstructionTemplate` | empty (uses default) | Override the default instruction prefix. Use the token `{sources}` where the source list should be inserted. |

The default template instructs the model to use **only** the supplied sources, refuse with `"I don't have that in my reference materials."` when the answer isn't there, and cite each claim as `[source: <file> §<section>]`.

<img src="./images/knowledge_base_inspector_unbaked.png" alt="KnowledgeBase inspector before baking" width="720" />

*A configured but not-yet-baked asset. The **Baked store** fields are still empty and the status line
reads `Not baked yet.` — that is what Step 3 fills in.*

---

## Step 3 — Bake the corpus

Baking runs the document parser, chunker, and embedder; produces a JSON store next to your project that the runtime loads directly.

Two ways:

- **One-click**: select the `KnowledgeBase` asset and click **Bake Now** in the Inspector.
- **Menu**: with the asset selected, **`IVA SDK → Knowledge → Bake Selected KnowledgeBase`**.

A progress bar shows each chunk being embedded. A 50-page Markdown document takes roughly 30–60 seconds depending on chunk size and network latency. You can cancel safely at any time — no partial file is written.

<img src="./images/knowledge_bake_progress.png" alt="Baking progress" width="720" />

When the bake completes:

- A file is written to `Assets/StreamingAssets/IVA_Knowledge/<KBname>.knowledge.json`. The StreamingAssets folder ensures the file is included in standalone builds automatically.
- The asset's **Baked Store Path**, **Embedding Dimension**, **Baked Chunk Count**, and **Last Baked At** fields are populated.
- The Inspector status line reads e.g. `42 chunks · dim 768 · baked 2026-05-15 14:23`.

<img src="./images/knowledge_base_inspector_baked.png" alt="KnowledgeBase inspector after baking" width="720" />

*The same asset after a successful bake — compare the **Baked store** block with the screenshot in
Step 2.*

Re-bake whenever you edit a `.md` file or change the chunking parameters.

---

## Step 4 — Attach the grounding component to your agent

On the same GameObject as your agent (`ConversationalAgent`, `GeminiLiveAgent`, or `GeminiVoiceOnlyAgent`):

1. **`Add Component → Document Grounding Component`** (under the `IVH.Core.Knowledge` namespace).
2. Drag your `KnowledgeBase` asset into the **Knowledge Base** field.
3. **Leave the Gemini Api Key field blank.** The runtime reads the same `~/.aiapi/auth.json` you configured in Step 1. The Inspector field is only an override for special cases (multi-tenant builds, per-agent test keys); populating it skips the auth file.
4. Leave **Embedding Model** as `gemini-embedding-001` (must match what the baker used).

That's it — no other agent fields need to change.

<img src="./images/knowledge_agent_components_rag.png" alt="Agent GameObject with the three grounding components" width="720" />

*The full per-turn RAG wiring described in Step 5: `DocumentGroundingComponent` with the
`KnowledgeBase` assigned and **`Inject At Session Start` unticked**, plus `GeminiToolManager` and
`KnowledgeRetrievalTool`. The tool's **Tool Description** is the entire basis on which the model
decides whether to search, so write it to say *when* the corpus is relevant.*

---

## Step 5 — Run

The retrieval timing depends on the agent type:

- **`ConversationalAgent`** (modular STT → LLM → TTS): on every user turn, the user's transcribed message is embedded, the top-K chunks are retrieved, and the citation-instruction prefix is prepended to the user message before the LLM call. Adds ~150–300 ms per turn (the LLM call itself still dominates).
- **`GeminiLiveAgent`** and **`GeminiVoiceOnlyAgent`** (realtime voice): Gemini Live takes its system instruction as a one-shot at session setup, so retrieval happens **once at `Connect()`** with a generic seed. Zero per-turn latency, but the same K chunks remain in context for the whole session, retrieved for a query that was never actually asked.

  **For realtime agents, prefer per-turn retrieval instead.** Add a `KnowledgeRetrievalTool` alongside the `DocumentGroundingComponent` and a `GeminiToolManager`, then **untick `Inject At Session Start`** on the grounding component so it acts purely as the retrieval backend. The model then calls `search_knowledge(query)` with the user's real question whenever it needs the corpus. Costs one extra model round-trip on turns where it chooses to search; other turns are unaffected. Worked example: `Samples/KnowledgeRAGRetrievalDemo/`.

**Try it**: ask a question whose answer is in your document — the agent should answer naturally, as if the information is simply something it knows. With the default citation template the agent does **not** read source markers out loud (no "according to section 2…") so the conversation stays natural. Ask something off-topic and the agent should say so naturally instead of guessing.

### Why doesn't the agent read citations out loud?

For voice agents this is intentional. The default `CitationPromptFormatter.DefaultInstructionTemplate` tells the model to treat the retrieved chunks as background knowledge and explicitly forbids verbalizing the `[source: …]` markers or section numbers. The markers are still in the prompt so the model can answer if you ask "what's your source?" — they just don't appear in normal speech.

If you want strict text-style citations (e.g. for a chat-log UI where users benefit from seeing the source on every claim), assign `CitationPromptFormatter.StrictCitationInstructionTemplate` to the asset's `Citation Instruction Template` field, or write your own.

> **Why not "citations in the transcription but not the audio"?** For Gemini Live native-audio agents the audio *is* the model output — what it generates is what it speaks, there is no separate text-only channel. So spoken and on-screen text are necessarily the same. For `ConversationalAgent` (modular STT→LLM→TTS) a post-processing step could strip citation markers before TTS while keeping them in the chat log; that is not implemented yet.

---

## Choosing a backend: AI Studio vs Vertex AI

Grounding makes the backend choice matter more than it does for an ungrounded agent, because every
grounding strategy enlarges the prompt. Both backends serve the same Gemini 2.5 native-audio model
family and both support function calling (so both work with `KnowledgeRetrievalTool`), but they do
not perform the same.

| | Google AI Studio (`Flash25PreviewGoogleAI`) | Vertex AI (`Flash25VertexAI`) |
| :--- | :--- | :--- |
| Credential | `~/.aiapi/auth.json` (API key) | `~/.aiapi/service_account.json` |
| Cost | Free tier available | Paid, billed per session |
| Endpoint | Global, shared preview capacity | Regional (`us-central1`), provisioned |
| Turn latency, grounded agent | Noticeably higher, and variable | Consistently low |
| Best for | First run, zero-cost prototyping | Demos, user studies, anything timed |

**If turn-taking feels sluggish on AI Studio, that is expected and it is mostly not your corpus.**
The sample corpora are small — the curator dossier is ~9 KB, roughly 2.5k tokens — so document size
alone does not explain multi-second pauses. The real contributors, in order:

1. **Free-tier throughput.** The AI Studio free tier is shared, preview-grade capacity with
   per-minute request and token ceilings. A realtime session streams audio continuously, so it
   consumes tokens every second it is open, and an injected corpus raises the floor that
   continuous audio then builds on. Once you approach the ceiling the service throttles rather than
   erroring, which surfaces as slow replies rather than an obvious failure. Check your live quota on
   the [AI Studio rate-limit dashboard](https://aistudio.google.com/rate-limit).
2. **Model thinking.** Gemini 2.5 runs *dynamic thinking by default*, inserting a reasoning pass
   before each spoken reply. The SDK now disables it on both backends
   (`GeminiRealtimeWrapper.Disable Thinking`, on by default) — this was previously applied only to
   Vertex, which is why AI Studio sessions felt markedly slower. Untick it if you want the model to
   reason before answering and can accept the pause.
3. **Session context compression.** Native-audio sessions cap at 128k tokens. `Context Window
   Sliding` keeps long sessions alive by trimming the oldest context once the session passes
   `Sliding Window Target Tokens` (default 12800). Each trim is a server-side pause. If a long
   session develops periodic stalls, raise that value.

For grading grounding *quality* the backend is irrelevant — retrieval, chunking, and the citation
template behave identically. Prototype on AI Studio, and switch to Vertex when responsiveness starts
to matter.

---

## Verifying it works

A few quick diagnostics:

- **Bake didn't produce a file?** Check the Console for `IVA Knowledge Baker` errors. Common causes: missing API key, empty Markdown documents, or no `.md` assets in the list.
- **Agent ignores the corpus?** Confirm the `KnowledgeBase` asset has **Enabled** ticked and a non-empty **Baked Store Path**. Watch the Console for `DocumentGroundingComponent` warnings on play — they fire if the baked file is missing or the embedder dimension doesn't match.
- **Agent hallucinates anyway?** Lower `retrievalTopK` to 2 or 3, raise `minSimilarity` to 0.5, and re-run. If the agent still drifts, override `citationInstructionTemplate` with `CitationPromptFormatter.StrictCitationInstructionTemplate` (or a custom phrasing) so the model is forced to ground every claim.
- **Agent reads "source", "section", or bracket markers out loud?** Confirm `Citation Instruction Template` is left blank (uses the natural-speech default). The strict template is opt-in; if you set it accidentally the model will dutifully verbalize citations.
- **Want to confirm the prefix is actually being injected?** Open `MemoryManager` / `DocumentGroundingComponent` source and the `Connect()` / `InteractionLoop` paths in the agent — both prepend `await BuildContextPrefixAsync(...)`. Log the result of that call temporarily during development.
- **Agent never connects?** The HUD now reports the reason instead of sitting on "Connecting…". A missing or unauthorised Vertex service account, an absent API key, and a setup message the server rejects each produce a red `System: Connection failed` line with the cause. If nothing arrives at all, the wrapper gives up after `Setup Timeout Seconds` (default 20 s) and reports that too.
- **Slow replies rather than no replies?** See [Choosing a backend](#choosing-a-backend-ai-studio-vs-vertex-ai) — on the AI Studio free tier this is usually throughput, not your corpus.

---

## How it composes with long-term memory

If you already use `MemoryManager` for cross-session conversation memory, it now implements the same `IContextProvider` interface. Both providers' prefixes are aggregated in component order and concatenated into the prompt, so a single agent can have:

- A `MemoryManager` (long-term memory of past conversations with the user), **and**
- A `DocumentGroundingComponent` (factual grounding from a curated corpus).

You do not need to wire anything explicitly — `AgentBase.BuildContextPrefixAsync` picks up every `IContextProvider` on the GameObject.

---

## Advanced

- **Custom embedders**: implement `IVH.Core.Memory.IEmbedder` and assign it from code via `DocumentGroundingComponent.embedderOverride` before the component's `Awake` runs (or call the internal initializer in a custom subclass). Useful for self-hosted embedding models. The runtime refuses to load the baked store if the embedder dimension doesn't match the dimension recorded at bake time.
- **Custom stores**: implement `IVH.Core.Memory.IMemoryStore` and assign via `DocumentGroundingComponent.storeOverride`. Useful when you want to swap the local JSON store for a remote vector DB.
- **Custom prompt template**: set `KnowledgeBase.citationInstructionTemplate` to any string containing the literal token `{sources}` — that token is replaced with the formatted source list at runtime.
- **No API key shipping**: leave the `geminiApiKey` field blank. The component falls back to `~/.aiapi/auth.json` (recommended for development) or you can assign `embedderOverride` programmatically (e.g. read from an environment variable at startup, useful for headless server builds).
- **Multiple knowledge bases**: stack multiple `DocumentGroundingComponent` instances on the same GameObject with different `KnowledgeBase` assets. Each contributes its own prefix in component order.

---

## Known limitations (v3.0)

- **Markdown only.** PDF, plain text, and `.docx` will plug in behind the same `IDocumentReader` interface with no architectural change.
- **Embedder dimension lock-in.** Re-baking with a different embedder requires re-baking the whole corpus. The runtime refuses to load on mismatch rather than returning garbage.
- **Stale bakes are silent.** Editing a source `.md` does not invalidate the baked store and nothing warns you — the agent simply answers from the old one. Re-bake after every edit.
- **Token budget on small-context local LLMs.** Ollama / GPT4All users should keep `retrievalTopK` low (1–2) and `maxContextChars` small (≤ 1500).
