using System.Collections.Generic;
using System.Text;

namespace IVH.Core.IntelligentVirtualAgent
{
    /// <summary>
    /// What an agent's avatar can actually do at runtime. Collected from the live setup (animator
    /// controller, rig type, enabled subsystems) by the agent, so the description can never promise an
    /// animation that is not wired up.
    /// </summary>
    public sealed class EmbodimentCapabilities
    {
        public CharacterType characterType;
        public EmotionHandlerType emotionHandlerType;
        public BodyAnimationControllerType bodyAnimationType;

        /// <summary>Body motions the animator can play: (exact action name, description).</summary>
        public List<KeyValuePair<string, string>> bodyMotions = new List<KeyValuePair<string, string>>();

        /// <summary>Facial expressions the face can show: (exact emotion name, description or null).</summary>
        public List<KeyValuePair<string, string>> facialExpressions = new List<KeyValuePair<string, string>>();

        /// <summary>FACS expressions peak and then relax to a subtle version (Avatar SDK) instead of holding at full strength.</summary>
        public bool facialExpressionsRelax;

        public bool hasLipSync;
        public bool hasBlinking;
        public bool hasGaze;
        public bool canWalk;

        public bool visionEnabled;
        public TargetCameraType visionSource;
        public float visionIntervalSeconds;

        /// <summary>What the camera shows, in the agent's words (e.g. "a webcam on your screen that looks at the people in front of you"). Empty = default wording.</summary>
        public string visionSourceDescription;

        /// <summary>The voice is a native-audio voice that can change speaking style on request (whisper, sing, emotions, robot voice).</summary>
        public bool canVaryVoiceStyle;

        /// <summary>True when the user can cut the agent off by speaking.</summary>
        public bool userCanInterruptByVoice;

        /// <summary>Free text appended verbatim (persona-specific facts about the avatar).</summary>
        public string additionalNotes;

        /// <summary>Ask the agent to mention in its first greeting that it can explain its abilities.</summary>
        public bool mentionInGreeting;
    }

    /// <summary>
    /// Turns <see cref="EmbodimentCapabilities"/> into a system-prompt section that tells the model,
    /// truthfully, which facial expressions and body motions its avatar can perform and what it cannot
    /// do, and how to talk about it. Users who know the limits up front are not surprised or frustrated
    /// when e.g. "can you sit down?" is not possible.
    /// </summary>
    public static class AgentCapabilityDescriber
    {
        private const int MaxDescriptionLength = 160;

        public static string BuildPrompt(EmbodimentCapabilities c)
        {
            var sb = new StringBuilder();
            sb.AppendLine("YOUR EMBODIMENT — what your 3D avatar can and cannot do. This list is generated from your actual animation setup and is accurate; never contradict it.");
            sb.AppendLine("Technical setup: " + DescribeTechnicalSetup(c));

            // Facial expressions
            sb.AppendLine();
            if (c.facialExpressions.Count > 0)
            {
                sb.AppendLine("FACIAL EXPRESSIONS you can show (pass exactly one name as 'emotion'):");
                foreach (var kv in c.facialExpressions)
                    sb.AppendLine(string.IsNullOrEmpty(kv.Value) ? $"- {kv.Key}" : $"- {kv.Key}: {Shorten(kv.Value)}");
                if (c.emotionHandlerType == EmotionHandlerType.CC4_Animation)
                    sb.AppendLine("These are short pre-recorded facial performance clips. They move only your face and head, never your arms or body, and play for a few seconds.");
                else if (c.facialExpressionsRelax)
                    sb.AppendLine("An expression builds up within about a third of a second, peaks for one to four seconds, then softens to a subtle version that stays until you choose another one (a surprise fades completely; 'neutral' relaxes the face). The intensity is fixed: you cannot make an expression stronger or weaker.");
                else
                    sb.AppendLine("An expression blends in within about a third of a second and stays on your face until you choose another one ('neutral' relaxes the face). The intensity is fixed: you cannot make an expression stronger, weaker or partial.");
            }
            else
            {
                sb.AppendLine("FACIAL EXPRESSIONS: none are available; your face stays neutral apart from lip sync and blinking.");
            }

            // Body motions
            sb.AppendLine();
            if (c.bodyMotions.Count > 0)
            {
                sb.AppendLine("BODY MOTIONS you can perform (pass exactly one name as 'action', or 'none'):");
                foreach (var kv in c.bodyMotions)
                    sb.AppendLine(string.IsNullOrEmpty(kv.Value) ? $"- {kv.Key}" : $"- {kv.Key}: {Shorten(kv.Value)}");
                sb.AppendLine("Each body motion is a pre-recorded motion-capture clip of a few seconds. It plays once, then you return to your standing idle pose. Only one motion plays at a time: you cannot combine, chain, loop, hold, slow down or speed up motions, and a motion may start about half a second after you choose it, so it will not be exactly in sync with your words.");
            }
            else
            {
                sb.AppendLine("BODY MOTIONS: none are available; you stay in your standing idle pose.");
            }

            // Other channels
            sb.AppendLine();
            sb.AppendLine("OTHER NON-VERBAL BEHAVIOUR:");
            if (c.hasLipSync) sb.AppendLine("- Your lips move automatically with your voice (audio-driven lip sync). It approximates speech; it is not exact articulation.");
            if (c.hasBlinking) sb.AppendLine("- You blink automatically at random intervals; blinking is not under your control.");
            if (c.hasGaze) sb.AppendLine("- Gaze: you can look at the user (your eyes, and slightly your head, turn toward the user's viewpoint) or let your gaze wander idly ('User' or 'Idle' as 'gaze'). You cannot look at specific objects or people other than the user.");
            if (c.canVaryVoiceStyle) sb.AppendLine("- Voice: your voice keeps its timbre, but you can change how you speak whenever asked: whisper, speak louder or softer, faster or slower, with emotion (happy, sad, excited, nervous, angry), like a robot, with an accent, or sing a few lines.");
            sb.AppendLine(c.canWalk
                ? "- Locomotion: you can walk, step back or step aside a few meters when asked (move_agent). You cannot climb, sit, lie down or leave the scene."
                : "- Locomotion: none. You stand in one place; you cannot walk, step closer, turn around or move away.");

            // Senses
            sb.AppendLine();
            sb.AppendLine("PERCEPTION:");
            if (c.visionEnabled)
            {
                string source = !string.IsNullOrWhiteSpace(c.visionSourceDescription)
                    ? c.visionSourceDescription.Trim()
                    : c.visionSource == TargetCameraType.WebCam
                        ? "the user's webcam"
                        : "a virtual camera at your eyes inside the 3D scene (you see the virtual scene, not the real world)";
                sb.AppendLine($"- Vision: you receive a still image from {source} roughly every {c.visionIntervalSeconds:0.#} s. You see coarse snapshots, not continuous video: you miss fast movements and small or distant details.");
            }
            else
            {
                sb.AppendLine("- Vision: your camera is switched off, so you cannot see the user, their gestures or their surroundings. Never pretend to see anything. If images start arriving during the conversation, your vision has been switched on.");
            }
            sb.AppendLine(c.userCanInterruptByVoice
                ? "- Hearing: you hear the user through a microphone, and they can interrupt you by speaking."
                : "- Hearing: you hear the user through a microphone. While you speak, the microphone is muted to prevent echo, so the user cannot interrupt you by voice; keep answers reasonably short.");

            // Hard limits
            sb.AppendLine();
            sb.AppendLine("THINGS YOU CANNOT DO (unless a matching motion is listed above): touch, grab, hold or hand over objects; point at specific things in the user's room; sit, lie down, kneel or jump; write, type, use sign language or make individual finger gestures; dance or do sports moves; " +
                (c.canVaryVoiceStyle
                    ? "change your appearance or clothes, or switch to a different person's voice (your speaking style can change, see Voice above); "
                    : "change your appearance, clothes or voice; ") +
                "show tears, blushing or sweat.");

            if (!string.IsNullOrWhiteSpace(c.additionalNotes))
            {
                sb.AppendLine();
                sb.AppendLine("MORE ABOUT YOUR AVATAR: " + c.additionalNotes.Trim());
            }

            // Conversational policy
            sb.AppendLine();
            sb.AppendLine("HOW TO TALK ABOUT YOUR ABILITIES:");
            sb.AppendLine("- If asked what you can do or how you work, give a short spoken summary (2-4 sentences) grouped by kind, e.g. facial expressions, greetings, nods and head shakes, conversational gestures, emotional reactions, and mention the main limits. Do not read the lists out name by name unless explicitly asked.");
            sb.AppendLine("- If asked to perform a specific expression or motion: if it is listed, do it by calling update_avatar_state with that exact name; if it is not listed, say plainly that your avatar cannot do that yet and offer the closest listed alternative.");
            sb.AppendLine("- Never claim to perform or to have performed anything that is not listed, and never narrate actions you did not trigger.");
            if (c.mentionInGreeting)
                sb.AppendLine("- In your first greeting, add one short sentence inviting the user to ask what you can and cannot do as an avatar.");

            return sb.ToString();
        }

        private static string DescribeTechnicalSetup(EmbodimentCapabilities c)
        {
            string body;
            switch (c.characterType)
            {
                case CharacterType.AvatarSDK:
                    body = "You appear as a realistic 3D avatar created with Avatar SDK (MetaPerson) and rendered live in the Unity engine.";
                    break;
                case CharacterType.Rocketbox:
                    body = "You appear as a Microsoft Rocketbox 3D character rendered live in the Unity engine.";
                    break;
                default:
                    body = "You appear as a realistic 3D character (Character Creator 4 / DIDIMO) rendered live in the Unity engine.";
                    break;
            }

            string face;
            if (c.emotionHandlerType == EmotionHandlerType.CC4_Animation)
                face = c.characterType == CharacterType.AvatarSDK
                    ? " Your facial expressions are pre-recorded facial performance clips made for Character Creator 4 characters, retargeted in real time onto your ARKit-style facial blendshapes."
                    : " Your facial expressions are pre-recorded facial performance clips.";
            else
                face = c.characterType == CharacterType.AvatarSDK
                    ? " Your facial expressions are composed in real time from ARKit-style facial blendshapes."
                    : " Your facial expressions are composed in real time from facial blendshapes.";

            string lips = c.hasLipSync ? " Your lip movements are generated from your own voice by Oculus Lipsync." : "";
            string eyes = c.hasGaze ? " Your eyes and head turn toward what you look at." : "";

            string motions = c.bodyAnimationType == BodyAnimationControllerType.Mixamo
                ? " Your body motions are Mixamo motion-capture clips retargeted to your skeleton by Unity's humanoid animation system."
                : " Your body motions are Rocketbox motion-capture animations.";

            return body + face + lips + eyes + motions +
                   " You run on the open-source Intelligent Virtual Agent (IVA) SDK; your voice and language understanding come from Google's Gemini Live API.";
        }

        private static string Shorten(string text)
        {
            text = text.Replace("\n", " ").Trim();
            if (text.Length <= MaxDescriptionLength) return text;
            int cut = text.LastIndexOf(' ', MaxDescriptionLength);
            return text.Substring(0, cut > 40 ? cut : MaxDescriptionLength).TrimEnd(',', ';', '.') + "...";
        }
    }
}
