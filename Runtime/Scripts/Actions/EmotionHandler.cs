using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using IVH.Core.Actions;
using IVH.Core.Utils.Logging;

namespace IVH.Core.IntelligentVirtualAgent
{
    /// <summary>
    /// FACS-based emotion handler. Drives ARKit-convention blendshapes on the character mesh.
    /// v2.7.0: emotions now crossfade between states over <see cref="transitionDurationSeconds"/>
    /// and are hold-gated by <see cref="minHoldSeconds"/> to suppress rapid flicker.
    /// Set both to 0 for exact v2.6.0 (instant pop) behavior.
    /// </summary>
    /// <remarks>
    /// CC4/DIDIMO and Rocketbox use fixed blendshape indices on <see cref="skinnedMeshRenderer"/>.
    /// <see cref="CharacterType.AvatarSDK"/> resolves ARKit blendshape <b>names</b> on
    /// <see cref="skinnedMeshRenderer"/> and every mesh in <see cref="additionalRenderers"/> (eyelashes,
    /// lower teeth), so all face parts move together.
    /// </remarks>
    public class EmotionHandler : MonoBehaviour
    {
        public SkinnedMeshRenderer skinnedMeshRenderer;
        public CharacterType characterType = CharacterType.CC4OrDIDIMO;

        [Tooltip("Other face meshes that carry copies of the facial blendshapes (e.g. eyelashes, teeth). Only used by name-based rigs such as Avatar SDK.")]
        public List<SkinnedMeshRenderer> additionalRenderers = new List<SkinnedMeshRenderer>();

        [Tooltip("Seconds to lerp between emotional blendshape states. 0 = instant v2.6.0 behavior.")]
        [Range(0f, 2f)] public float transitionDurationSeconds = 0.3f;

        [Tooltip("Minimum seconds a High-priority emotion holds before being replaced by a different one. 0 = no gate.")]
        [Range(0f, 5f)] public float minHoldSeconds = 0.8f;

        [Tooltip("Avatar SDK: seconds an expression takes to soften from its peak to its sustained level.")]
        [Range(0.1f, 3f)] public float relaxDurationSeconds = 1.2f;

        /// <summary>Intensity <see cref="AgentBase.ExpressEmotion"/> passes; the Avatar SDK table is authored for it.</summary>
        public const float DefaultIntensity = 0.5f;

        private string _activeEmotion = "";
        private float _lastChangeTime = -999f;
        private Coroutine _lerpCoroutine;
        private Coroutine _resetCoroutine;
        private Coroutine _relaxCoroutine;
        private HashSet<ShapeKey> _ownedShapes = new HashSet<ShapeKey>();

        /// <summary>One blendshape on one mesh.</summary>
        private readonly struct ShapeKey : System.IEquatable<ShapeKey>
        {
            public readonly SkinnedMeshRenderer Renderer;
            public readonly int Index;
            public ShapeKey(SkinnedMeshRenderer renderer, int index) { Renderer = renderer; Index = index; }
            public bool Equals(ShapeKey other) => Renderer == other.Renderer && Index == other.Index;
            public override bool Equals(object obj) => obj is ShapeKey other && Equals(other);
            public override int GetHashCode() => ((Renderer != null ? Renderer.GetHashCode() : 0) * 397) ^ Index;
        }

        /// <summary>Name of the emotion currently shown ("" when none).</summary>
        public string ActiveEmotion => _activeEmotion;

        /// <summary>
        /// Triggers a facial emotion. Subject to <see cref="minHoldSeconds"/> hold gate — rapid
        /// repeat calls for different emotions inside the hold window are dropped.
        /// </summary>
        public void HandleEmotion(string emotion, float intensity, string duration)
        {
            if (!string.IsNullOrEmpty(_activeEmotion)
                && emotion != _activeEmotion
                && Time.time - _lastChangeTime < minHoldSeconds)
            {
                IVALogger.Debug("EmotionHandler", $"Drop '{emotion}' — still holding '{_activeEmotion}' ({minHoldSeconds - (Time.time - _lastChangeTime):0.0}s left)");
                return;
            }

            var targets = BuildTargets(emotion, intensity);
            StartLerp(targets);

            // Name-based (Avatar SDK) expressions peak, then relax to a subtle version: a face frozen at
            // full strength, and especially an open jaw, looks unnatural within seconds.
            if (_relaxCoroutine != null) { StopCoroutine(_relaxCoroutine); _relaxCoroutine = null; }
            if (characterType == CharacterType.AvatarSDK && ArKitDynamics.TryGetValue(emotion ?? "", out var dynamics) && dynamics.apexSeconds > 0f)
                _relaxCoroutine = StartCoroutine(RelaxAfter(emotion, intensity, dynamics.apexSeconds, dynamics.sustain));

            _activeEmotion = emotion;
            _lastChangeTime = Time.time;
            if (duration == "before")
            {
                if (_resetCoroutine != null) StopCoroutine(_resetCoroutine);
                _resetCoroutine = StartCoroutine(ResetAfter(2.0f));
            }
        }

        private void StartLerp(Dictionary<ShapeKey, float> targets) => StartLerp(targets, transitionDurationSeconds);

        private void StartLerp(Dictionary<ShapeKey, float> targets, float duration)
        {
            if (_lerpCoroutine != null) StopCoroutine(_lerpCoroutine);
            _lerpCoroutine = StartCoroutine(LerpBlendshapes(targets, duration));
        }

        private IEnumerator RelaxAfter(string emotion, float intensity, float apexSeconds, float sustain)
        {
            yield return new WaitForSeconds(transitionDurationSeconds + apexSeconds);
            StartLerp(BuildNamedTargets(emotion, intensity, sustain), relaxDurationSeconds);
            _relaxCoroutine = null;
        }

        private IEnumerator LerpBlendshapes(Dictionary<ShapeKey, float> newTargets, float duration)
        {
            if (skinnedMeshRenderer == null) yield break;

            HashSet<ShapeKey> touched = new HashSet<ShapeKey>(_ownedShapes);
            foreach (var key in newTargets.Keys) touched.Add(key);

            if (duration <= 0f)
            {
                foreach (var key in touched)
                {
                    float target = newTargets.TryGetValue(key, out float t) ? t : 0f;
                    SetWeight(key, target);
                }
                _ownedShapes = new HashSet<ShapeKey>(newTargets.Keys);
                yield break;
            }

            Dictionary<ShapeKey, float> starts = new Dictionary<ShapeKey, float>();
            foreach (var key in touched) starts[key] = key.Renderer != null ? key.Renderer.GetBlendShapeWeight(key.Index) : 0f;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float a = Mathf.Clamp01(elapsed / duration);
                foreach (var key in touched)
                {
                    float target = newTargets.TryGetValue(key, out float t) ? t : 0f;
                    SetWeight(key, Mathf.Lerp(starts[key], target, a));
                }
                yield return null;
            }
            foreach (var key in touched)
            {
                float target = newTargets.TryGetValue(key, out float t) ? t : 0f;
                SetWeight(key, target);
            }

            _ownedShapes = new HashSet<ShapeKey>(newTargets.Keys);
        }

        private static void SetWeight(ShapeKey key, float weight)
        {
            if (key.Renderer != null) key.Renderer.SetBlendShapeWeight(key.Index, weight);
        }

        private IEnumerator ResetAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            ResetBlendShapes();
            _activeEmotion = "";
        }

        /// <summary>Hard-clear all blendshapes we own. Public for backward-compat callers.</summary>
        public void ResetBlendShapes()
        {
            if (_relaxCoroutine != null) { StopCoroutine(_relaxCoroutine); _relaxCoroutine = null; }
            StartLerp(new Dictionary<ShapeKey, float>());
        }

        private Dictionary<ShapeKey, float> BuildTargets(string emotion, float intensity)
        {
            if (characterType == CharacterType.AvatarSDK) return BuildNamedTargets(emotion, intensity);

            // Index-based rigs: the tables below address skinnedMeshRenderer directly.
            var d = new Dictionary<int, float>();
            switch (emotion)
            {
                case "happy":     AddHappiness(d, intensity); break;
                case "sad":       AddSadness(d, intensity); break;
                case "angry":     AddAnger(d, intensity); break;
                case "scared":    AddFear(d, intensity); break;
                case "disgusted": AddDisgust(d, intensity); break;
                case "surprised": AddSurprise(d, intensity); break;
                case "attentive": AddAttentive(d, intensity); break;
                case "concerned": AddConcerned(d, intensity); break;
                case "confused":  AddConfused(d, intensity); break;
                case "neutral":   break;
                default:
                    IVALogger.Debug("EmotionHandler", $"Unknown emotion '{emotion}', fading to neutral.");
                    break;
            }
            var targets = new Dictionary<ShapeKey, float>();
            foreach (var kv in d) targets[new ShapeKey(skinnedMeshRenderer, kv.Key)] = kv.Value;
            return targets;
        }

        #region Supported vocabulary

        private static readonly string[] IndexRigEmotions =
            { "neutral", "happy", "sad", "angry", "scared", "disgusted", "surprised", "attentive", "concerned", "confused" };

        /// <summary>
        /// Emotion names <see cref="HandleEmotion"/> renders for <paramref name="type"/>. Anything else
        /// fades the face to neutral.
        /// </summary>
        public static IReadOnlyList<string> GetSupportedEmotions(CharacterType type)
        {
            if (type == CharacterType.AvatarSDK) return new List<string>(ArKitExpressions.Keys);
            return IndexRigEmotions;
        }

        /// <summary>Short, honest description of what the face does for <paramref name="emotion"/>.</summary>
        public static string DescribeEmotion(string emotion)
        {
            switch (emotion)
            {
                case "neutral":      return "relaxed resting face";
                case "happy":        return "warm smile with raised cheeks";
                case "excited":      return "big smile with raised brows and bright, wide eyes";
                case "amused":       return "lopsided half-smile (smirk)";
                case "proud":        return "small closed-mouth smile, chin slightly raised";
                case "relieved":     return "soft smile with relaxed, slightly raised inner brows";
                case "sympathetic":  return "gentle smile with softly raised inner brows";
                case "embarrassed":  return "awkward pressed-lip smile with raised inner brows";
                case "attentive":    return "subtle interested look with a light smile";
                case "curious":      return "raised brows, one slightly higher";
                case "impressed":    return "raised brows with lips briefly rounded as if saying 'oh'";
                case "thinking":     return "slightly lowered brows, narrowed eyes, pressed lips";
                case "skeptical":    return "one brow raised, the other lowered, pressed lips";
                case "confused":     return "asymmetric brows, one up and one down";
                case "surprised":    return "brief raised brows, wide eyes and slightly dropped jaw that fade back";
                case "concerned":    return "worried brows, slightly pressed lips";
                case "sad":          return "inner brows raised, mouth corners pulled down";
                case "disappointed": return "mouth corners down, lips pressed, inner brows slightly raised";
                case "bored":        return "flat face, lips pressed and pulled to one side";
                case "frustrated":   return "lowered brows and tightly pressed lips";
                case "determined":   return "lowered brows, firm pressed lips";
                case "angry":        return "lowered brows, narrowed eyes, pressed lips";
                case "contempt":     return "one-sided lip raise with a slight sneer";
                case "disgusted":    return "wrinkled nose and raised upper lip";
                case "scared":       return "raised brows, wide eyes, stretched lips";
                default:             return emotion;
            }
        }

        #endregion

        #region Avatar SDK (ARKit names)

        // Peak weights (0-100) at DefaultIntensity. Shapes are ARKit names, resolved on every face mesh.
        // eyeBlink* and eyeLook* are deliberately absent: they belong to CharacterBlinkBehavior and
        // EyeGazeController, which would overwrite them every frame. Visemes belong to lip sync.
        private static readonly Dictionary<string, Dictionary<string, float>> ArKitExpressions =
            new Dictionary<string, Dictionary<string, float>>
        {
            ["neutral"] = new Dictionary<string, float>(),
            ["happy"] = new Dictionary<string, float>
            {
                ["mouthSmileLeft"] = 70, ["mouthSmileRight"] = 70,
                ["cheekSquintLeft"] = 45, ["cheekSquintRight"] = 45,
                ["eyeSquintLeft"] = 25, ["eyeSquintRight"] = 25,
                ["mouthDimpleLeft"] = 15, ["mouthDimpleRight"] = 15,
            },
            ["excited"] = new Dictionary<string, float>
            {
                ["mouthSmileLeft"] = 80, ["mouthSmileRight"] = 80,
                ["cheekSquintLeft"] = 40, ["cheekSquintRight"] = 40,
                ["browInnerUp"] = 40,
                ["browOuterUpLeft"] = 45, ["browOuterUpRight"] = 45,
                ["eyeWideLeft"] = 25, ["eyeWideRight"] = 25,
                ["mouthUpperUpLeft"] = 15, ["mouthUpperUpRight"] = 15,
            },
            ["amused"] = new Dictionary<string, float>
            {
                ["mouthSmileLeft"] = 55, ["mouthSmileRight"] = 20,
                ["cheekSquintLeft"] = 30,
                ["eyeSquintLeft"] = 15,
                ["browOuterUpLeft"] = 20,
            },
            ["proud"] = new Dictionary<string, float>
            {
                ["mouthSmileLeft"] = 35, ["mouthSmileRight"] = 35,
                ["mouthPressLeft"] = 20, ["mouthPressRight"] = 20,
                ["mouthShrugLower"] = 20,
                ["eyeSquintLeft"] = 15, ["eyeSquintRight"] = 15,
                ["cheekSquintLeft"] = 15, ["cheekSquintRight"] = 15,
            },
            ["relieved"] = new Dictionary<string, float>
            {
                ["mouthSmileLeft"] = 35, ["mouthSmileRight"] = 35,
                ["browInnerUp"] = 25,
                ["eyeSquintLeft"] = 10, ["eyeSquintRight"] = 10,
                ["mouthFunnel"] = 10,
            },
            ["sympathetic"] = new Dictionary<string, float>
            {
                ["browInnerUp"] = 50,
                ["mouthSmileLeft"] = 20, ["mouthSmileRight"] = 20,
                ["mouthPressLeft"] = 15, ["mouthPressRight"] = 15,
                ["eyeSquintLeft"] = 10, ["eyeSquintRight"] = 10,
            },
            ["embarrassed"] = new Dictionary<string, float>
            {
                ["mouthSmileLeft"] = 30, ["mouthSmileRight"] = 30,
                ["mouthPressLeft"] = 25, ["mouthPressRight"] = 25,
                ["browInnerUp"] = 35,
                ["eyeSquintLeft"] = 15, ["eyeSquintRight"] = 15,
                ["mouthRollLower"] = 15,
            },
            ["attentive"] = new Dictionary<string, float>
            {
                ["browInnerUp"] = 25,
                ["browOuterUpLeft"] = 15, ["browOuterUpRight"] = 15,
                ["mouthSmileLeft"] = 20, ["mouthSmileRight"] = 20,
            },
            ["curious"] = new Dictionary<string, float>
            {
                ["browInnerUp"] = 35,
                ["browOuterUpLeft"] = 40, ["browOuterUpRight"] = 25,
                ["eyeWideLeft"] = 15, ["eyeWideRight"] = 15,
                ["mouthPressLeft"] = 10, ["mouthPressRight"] = 10,
            },
            ["impressed"] = new Dictionary<string, float>
            {
                ["browInnerUp"] = 45,
                ["browOuterUpLeft"] = 45, ["browOuterUpRight"] = 45,
                ["mouthPucker"] = 25,
                ["mouthFunnel"] = 10,
                ["mouthPressLeft"] = 10, ["mouthPressRight"] = 10,
            },
            ["thinking"] = new Dictionary<string, float>
            {
                ["browDownLeft"] = 30, ["browDownRight"] = 30,
                ["browInnerUp"] = 20,
                ["eyeSquintLeft"] = 20, ["eyeSquintRight"] = 20,
                ["mouthPressLeft"] = 30, ["mouthPressRight"] = 30,
                ["mouthRollLower"] = 15,
            },
            ["skeptical"] = new Dictionary<string, float>
            {
                ["browOuterUpLeft"] = 55,
                ["browDownRight"] = 30,
                ["eyeSquintRight"] = 25,
                ["mouthPressLeft"] = 25, ["mouthPressRight"] = 25,
                ["mouthLeft"] = 10,
            },
            ["confused"] = new Dictionary<string, float>
            {
                ["browDownLeft"] = 45,
                ["browOuterUpRight"] = 55,
                ["browInnerUp"] = 15,
                ["mouthLeft"] = 15,
                ["mouthPressLeft"] = 15, ["mouthPressRight"] = 15,
            },
            ["surprised"] = new Dictionary<string, float>
            {
                ["browInnerUp"] = 75,
                ["browOuterUpLeft"] = 75, ["browOuterUpRight"] = 75,
                ["eyeWideLeft"] = 55, ["eyeWideRight"] = 55,
                ["jawOpen"] = 20,
            },
            ["concerned"] = new Dictionary<string, float>
            {
                ["browInnerUp"] = 60,
                ["browDownLeft"] = 25, ["browDownRight"] = 25,
                ["mouthPressLeft"] = 20, ["mouthPressRight"] = 20,
                ["mouthFrownLeft"] = 15, ["mouthFrownRight"] = 15,
            },
            ["sad"] = new Dictionary<string, float>
            {
                ["browInnerUp"] = 70,
                ["browDownLeft"] = 15, ["browDownRight"] = 15,
                ["mouthFrownLeft"] = 55, ["mouthFrownRight"] = 55,
                ["mouthShrugLower"] = 30,
            },
            ["disappointed"] = new Dictionary<string, float>
            {
                ["mouthFrownLeft"] = 45, ["mouthFrownRight"] = 45,
                ["mouthPressLeft"] = 25, ["mouthPressRight"] = 25,
                ["browInnerUp"] = 40,
                ["browDownLeft"] = 10, ["browDownRight"] = 10,
                ["mouthShrugLower"] = 20,
            },
            ["bored"] = new Dictionary<string, float>
            {
                ["browDownLeft"] = 15, ["browDownRight"] = 15,
                ["eyeSquintLeft"] = 10, ["eyeSquintRight"] = 10,
                ["mouthPressLeft"] = 20, ["mouthPressRight"] = 20,
                ["mouthLeft"] = 15,
                ["mouthShrugLower"] = 10,
            },
            ["frustrated"] = new Dictionary<string, float>
            {
                ["browDownLeft"] = 55, ["browDownRight"] = 55,
                ["mouthPressLeft"] = 50, ["mouthPressRight"] = 50,
                ["noseSneerLeft"] = 15, ["noseSneerRight"] = 15,
                ["eyeSquintLeft"] = 25, ["eyeSquintRight"] = 25,
            },
            ["determined"] = new Dictionary<string, float>
            {
                ["browDownLeft"] = 40, ["browDownRight"] = 40,
                ["mouthPressLeft"] = 40, ["mouthPressRight"] = 40,
                ["eyeSquintLeft"] = 20, ["eyeSquintRight"] = 20,
                ["mouthShrugLower"] = 15,
            },
            ["angry"] = new Dictionary<string, float>
            {
                ["browDownLeft"] = 75, ["browDownRight"] = 75,
                ["eyeSquintLeft"] = 35, ["eyeSquintRight"] = 35,
                ["noseSneerLeft"] = 25, ["noseSneerRight"] = 25,
                ["mouthPressLeft"] = 40, ["mouthPressRight"] = 40,
                ["mouthFrownLeft"] = 20, ["mouthFrownRight"] = 20,
            },
            ["contempt"] = new Dictionary<string, float>
            {
                ["mouthDimpleLeft"] = 50,
                ["mouthSmileLeft"] = 25,
                ["noseSneerLeft"] = 20,
                ["browDownRight"] = 15,
                ["mouthPressRight"] = 15,
            },
            ["disgusted"] = new Dictionary<string, float>
            {
                ["noseSneerLeft"] = 65, ["noseSneerRight"] = 65,
                ["mouthUpperUpLeft"] = 45, ["mouthUpperUpRight"] = 45,
                ["browDownLeft"] = 35, ["browDownRight"] = 35,
                ["cheekSquintLeft"] = 25, ["cheekSquintRight"] = 25,
                ["eyeSquintLeft"] = 20, ["eyeSquintRight"] = 20,
                ["mouthFrownLeft"] = 20, ["mouthFrownRight"] = 20,
            },
            ["scared"] = new Dictionary<string, float>
            {
                ["browInnerUp"] = 80,
                ["browOuterUpLeft"] = 45, ["browOuterUpRight"] = 45,
                ["eyeWideLeft"] = 60, ["eyeWideRight"] = 60,
                ["mouthStretchLeft"] = 45, ["mouthStretchRight"] = 45,
                ["jawOpen"] = 12,
            },
        };

        /// <summary>
        /// Seconds an expression stays at its peak, and the fraction it keeps afterwards. Real expressions are
        /// brief: a surprise lasts about a second, a smile softens but lingers. 0 seconds = no relaxing.
        /// </summary>
        private static readonly Dictionary<string, (float apexSeconds, float sustain)> ArKitDynamics =
            new Dictionary<string, (float apexSeconds, float sustain)>
        {
            ["happy"] = (2.5f, 0.5f), ["excited"] = (1.8f, 0.45f), ["amused"] = (2.5f, 0.4f),
            ["proud"] = (3f, 0.6f), ["relieved"] = (2.5f, 0.4f), ["sympathetic"] = (3f, 0.6f),
            ["embarrassed"] = (2.5f, 0.4f), ["attentive"] = (0f, 1f), ["curious"] = (2.5f, 0.5f),
            ["impressed"] = (1.8f, 0.35f), ["thinking"] = (4f, 0.8f), ["skeptical"] = (2.5f, 0.5f),
            ["confused"] = (2f, 0.4f), ["surprised"] = (1f, 0f), ["concerned"] = (3f, 0.6f),
            ["sad"] = (3f, 0.6f), ["disappointed"] = (3f, 0.5f), ["bored"] = (3f, 0.7f),
            ["frustrated"] = (2.5f, 0.45f), ["determined"] = (3f, 0.6f), ["angry"] = (2.5f, 0.5f),
            ["contempt"] = (2f, 0.35f), ["disgusted"] = (1.5f, 0.2f), ["scared"] = (1.8f, 0.3f),
        };

        /// <summary>Shapes that only belong to the peak of an expression (open mouth, wide eyes) and always fade out.</summary>
        private static readonly HashSet<string> TransientShapes = new HashSet<string>
        {
            "jawOpen", "mouthStretchLeft", "mouthStretchRight", "mouthLowerDownLeft", "mouthLowerDownRight",
            "mouthUpperUpLeft", "mouthUpperUpRight", "eyeWideLeft", "eyeWideRight", "mouthPucker", "mouthFunnel", "cheekPuff",
        };

        /// <param name="sustain">1 = peak weights; below 1 the expression is scaled down and transient shapes are dropped.</param>
        private Dictionary<ShapeKey, float> BuildNamedTargets(string emotion, float intensity, float sustain = 1f)
        {
            var targets = new Dictionary<ShapeKey, float>();
            if (!ArKitExpressions.TryGetValue(emotion ?? "", out var shapes))
            {
                IVALogger.Debug("EmotionHandler", $"Unknown emotion '{emotion}', fading to neutral.");
                return targets;
            }

            float scale = intensity / DefaultIntensity;
            foreach (var kv in shapes)
            {
                float factor = sustain >= 1f ? 1f : (TransientShapes.Contains(kv.Key) ? 0f : sustain);
                foreach (var key in ResolveNamed(kv.Key))
                    targets[key] = Mathf.Clamp(kv.Value * scale * factor, 0f, 100f);
            }
            return targets;
        }

        private IEnumerable<ShapeKey> ResolveNamed(string shapeName)
        {
            var renderers = new List<SkinnedMeshRenderer> { skinnedMeshRenderer };
            if (additionalRenderers != null) renderers.AddRange(additionalRenderers);

            foreach (var smr in renderers)
            {
                if (smr == null || smr.sharedMesh == null) continue;
                int index = FacialBlendshapeRig.FindIndex(smr.sharedMesh, shapeName);
                if (index >= 0) yield return new ShapeKey(smr, index);
            }
        }

        #endregion

        // All blendshape indices follow ARKit convention per character.
        private void AddHappiness(Dictionary<int, float> d, float i)
        {
            switch (characterType)
            {
                case CharacterType.CC4OrDIDIMO:
                    d[37] = 100 * i; d[38] = 100 * i; // cheek squint L/R
                    d[41] = 100 * i; d[42] = 100 * i; // mouth smile L/R
                    break;
                case CharacterType.Rocketbox:
                    d[21] = 100 * i; d[22] = 100 * i;
                    d[58] = 100 * i; d[59] = 100 * i;
                    break;
            }
        }

        private void AddSadness(Dictionary<int, float> d, float i)
        {
            switch (characterType)
            {
                case CharacterType.CC4OrDIDIMO:
                    d[19] = 100 * i; d[20] = 100 * i;
                    d[15] = 100 * i; d[16] = 100 * i;
                    d[21] = 20 * i;  d[22] = 20 * i;
                    d[33] = 50 * i;  d[34] = 50 * i;
                    d[43] = 100 * i; d[44] = 100 * i;
                    break;
                case CharacterType.Rocketbox:
                    d[15] = 100 * i; d[16] = 100 * i;
                    d[17] = 100 * i; d[18] = 100 * i;
                    d[23] = 20 * i;  d[24] = 20 * i;
                    d[25] = 50 * i;  d[26] = 50 * i;
                    d[44] = 100 * i; d[45] = 100 * i;
                    break;
            }
        }

        private void AddAnger(Dictionary<int, float> d, float i)
        {
            switch (characterType)
            {
                case CharacterType.CC4OrDIDIMO:
                    d[19] = 100 * i; d[20] = 100 * i;
                    d[17] = 100 * i; d[18] = 100 * i;
                    d[23] = 100 * i; d[24] = 100 * i;
                    d[25] = 70 * i;  d[26] = 70 * i;
                    d[43] = 100 * i; d[44] = 100 * i;
                    d[57] = 60 * i;  d[58] = 60 * i;
                    break;
                case CharacterType.Rocketbox:
                    d[15] = 100 * i; d[16] = 100 * i;
                    d[18] = 100 * i; d[19] = 100 * i;
                    d[33] = 100 * i; d[34] = 100 * i;
                    d[35] = 70 * i;  d[36] = 70 * i;
                    d[44] = 100 * i; d[45] = 100 * i;
                    d[54] = 40 * i;  d[55] = 40 * i;
                    d[56] = 60 * i;  d[57] = 60 * i;
                    break;
            }
        }

        private void AddDisgust(Dictionary<int, float> d, float i)
        {
            switch (characterType)
            {
                case CharacterType.CC4OrDIDIMO:
                    d[19] = 100 * i; d[20] = 100 * i;
                    d[17] = 100 * i; d[18] = 100 * i;
                    d[37] = 100 * i; d[38] = 100 * i;
                    d[43] = 100 * i; d[44] = 100 * i;
                    d[57] = 60 * i;  d[58] = 60 * i;
                    d[59] = 30 * i;  d[60] = 30 * i;
                    d[35] = 100 * i; d[36] = 100 * i;
                    break;
                case CharacterType.Rocketbox:
                    d[15] = 100 * i; d[18] = 100 * i;
                    d[17] = 100 * i; d[19] = 100 * i;
                    d[21] = 100 * i; d[22] = 100 * i;
                    d[44] = 100 * i; d[45] = 100 * i;
                    d[56] = 60 * i;  d[57] = 60 * i;
                    d[62] = 30 * i;  d[63] = 30 * i;
                    d[64] = 100 * i; d[65] = 100 * i;
                    break;
            }
        }

        private void AddSurprise(Dictionary<int, float> d, float i)
        {
            switch (characterType)
            {
                case CharacterType.CC4OrDIDIMO:
                    d[15] = 100 * i; d[16] = 100 * i;
                    d[17] = 100 * i; d[18] = 100 * i;
                    d[25] = 50 * i;  d[26] = 50 * i;
                    d[66] = 50 * i;
                    break;
                case CharacterType.Rocketbox:
                    d[17] = 100 * i;
                    d[18] = 100 * i; d[19] = 100 * i;
                    d[35] = 50 * i;  d[36] = 50 * i;
                    d[39] = 50 * i;
                    break;
            }
        }

        private void AddFear(Dictionary<int, float> d, float i)
        {
            switch (characterType)
            {
                case CharacterType.CC4OrDIDIMO:
                    d[15] = 100 * i; d[16] = 100 * i;
                    d[37] = 100 * i; d[38] = 100 * i;
                    d[25] = 100 * i; d[26] = 100 * i;
                    d[66] = 50 * i;
                    d[45] = 30 * i;  d[46] = 30 * i;
                    break;
                case CharacterType.Rocketbox:
                    d[17] = 100 * i;
                    d[21] = 100 * i; d[22] = 100 * i;
                    d[35] = 100 * i; d[36] = 100 * i;
                    d[39] = 50 * i;
                    d[60] = 30 * i;  d[61] = 30 * i;
                    break;
            }
        }

        // Subtle positive-listening expression. Reachable via ExpressEmotion("attentive").
        private void AddAttentive(Dictionary<int, float> d, float i)
        {
            switch (characterType)
            {
                case CharacterType.CC4OrDIDIMO:
                    d[15] = 50 * i; d[16] = 50 * i;  // brow inner up (subtle)
                    d[41] = 30 * i; d[42] = 30 * i;  // gentle smile
                    break;
                case CharacterType.Rocketbox:
                    d[17] = 50 * i;
                    d[58] = 30 * i; d[59] = 30 * i;
                    break;
            }
        }

        // New in v2.7.0 — used by interrupt-reaction and facepalm-mapped gestures.
        private void AddConcerned(Dictionary<int, float> d, float i)
        {
            switch (characterType)
            {
                case CharacterType.CC4OrDIDIMO:
                    d[15] = 70 * i; d[16] = 70 * i;  // brow inner up
                    d[19] = 40 * i; d[20] = 40 * i;  // brow down (mixed)
                    break;
                case CharacterType.Rocketbox:
                    d[17] = 70 * i;
                    d[15] = 40 * i; d[16] = 40 * i;
                    break;
            }
        }

        private void AddConfused(Dictionary<int, float> d, float i)
        {
            switch (characterType)
            {
                case CharacterType.CC4OrDIDIMO:
                    d[17] = 80 * i;  // brow outer up left only (asymmetry = confused)
                    d[19] = 30 * i;  // brow down opposite side
                    break;
                case CharacterType.Rocketbox:
                    d[18] = 80 * i;
                    d[15] = 30 * i;
                    break;
            }
        }
    }
}
