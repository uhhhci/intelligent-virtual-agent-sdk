# IVA SDK — Quick Start (Code-First)

Three minimal code samples demonstrating the developer-facing API surface of the SDK. Unlike the scene-based samples (BasicConversationalAgent, AdvanceAgent), these show the shortest path from C# to a working voice agent.

## Prerequisites

1. Run **IVA SDK → Setup Wizard** (from the top menu) and confirm dependencies are installed.
2. Paste your Google AI Studio API key in the Credentials tab. The wizard writes `~/.aiapi/auth.json` for you.

## 00_HelloAgent.cs

Smallest possible voice agent. Creates a `GeminiVoiceOnlyAgent` at runtime, sets a persona, and auto-connects.

Setup:
1. Create an empty GameObject in a scene.
2. Add an `AudioSource` component.
3. Add `HelloAgent` from this sample.
4. Press Play — you'll hear the agent greet you.

## 01_ToolCalling.cs

Registers two methods (`SetLightColor`, `SetLightIntensity`) that Gemini can call during a conversation.

Setup:
1. Start from the HelloAgent setup.
2. Drop a `Light` into the scene and wire it to the `targetLight` field on `ToolCallingSample`.
3. Add a `GeminiToolManager` component.
4. In the Inspector, add two entries to `definedTools`:
   - Tool 1: `toolName = "set_light_color"`, description "Changes the light color. r/g/b are 0–1.", target method `SetLightColor`, parameters JSON `{"type":"object","properties":{"r":{"type":"number"},"g":{"type":"number"},"b":{"type":"number"}},"required":["r","g","b"]}`.
   - Tool 2: `toolName = "set_light_intensity"`, target method `SetLightIntensity`, parameters `{"type":"object","properties":{"value":{"type":"number"}},"required":["value"]}`.
5. Ask: "Make the light red and dim" — Gemini will call both tools.

## 02_CustomCallbacks.cs

Subscribes to the full lifecycle + content event set on `GeminiRealtimeWrapper`. Good reference for integrating the SDK into custom UIs, logging pipelines, or observability tooling.

Setup:
1. Add `GeminiRealtimeWrapper` to any GameObject with a Gemini agent.
2. Add `CustomCallbacksSample` — it wires up all callbacks in `Awake()`.
3. Watch the Console while the agent runs.

## Presets

All three samples work with any `AgentPreset` asset. Run **IVA SDK → Generate Sample Presets** to produce four ready-made personas (tutor, therapist, museum guide, research study) and drag one onto the agent's `preset` slot.
