# Knowledge Grounding Demo

A reference Markdown document the agent can be grounded in so it can answer questions about itself accurately ("how do you work?", "who built you?", "what models do you support?").

## Files

- [`AboutThisSDK.md`](AboutThisSDK.md) — describes the IVA SDK in topic-headed sections plus a FAQ block, formatted for RAG retrieval.

## How to use it

1. In Unity, create a `KnowledgeBase` asset (Right-click → Create → IVA SDK → Knowledge Base).
2. Drag `AboutThisSDK.md` into the asset's **Markdown Documents** list.
3. Tick **Enabled** on the asset.
4. Set your Gemini embedding key via *IVA SDK → Knowledge → Set Embedder API Key…* (one-time per machine).
5. Click **Bake Now** on the asset's Inspector.
6. Add a `DocumentGroundingComponent` to your agent GameObject and assign the `KnowledgeBase`.
7. Play the scene and ask the agent "how do you work?" or "who built you?". It will quote and cite from the document.

For the full setup walkthrough see [`Documentations~/howToGroundAgentInDocuments.md`](../../Documentations~/howToGroundAgentInDocuments.md).
