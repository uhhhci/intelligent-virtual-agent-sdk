using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using IVH.Core.Utils.Logging;

namespace IVH.Core.IntelligentVirtualAgent
{
    /// <summary>
    /// FACS-based emotion handler. Drives ARKit-convention blendshapes on the character mesh.
    /// v2.7.0: emotions now crossfade between states over <see cref="transitionDurationSeconds"/>
    /// and are hold-gated by <see cref="minHoldSeconds"/> to suppress rapid flicker.
    /// Set both to 0 for exact v2.6.0 (instant pop) behavior.
    /// </summary>
    public class EmotionHandler : MonoBehaviour
    {
        public SkinnedMeshRenderer skinnedMeshRenderer;
        public CharacterType characterType = CharacterType.CC4OrDIDIMO;

        [Tooltip("Seconds to lerp between emotional blendshape states. 0 = instant v2.6.0 behavior.")]
        [Range(0f, 2f)] public float transitionDurationSeconds = 0.3f;

        [Tooltip("Minimum seconds a High-priority emotion holds before being replaced by a different one. 0 = no gate.")]
        [Range(0f, 5f)] public float minHoldSeconds = 0.8f;

        private string _activeEmotion = "";
        private float _lastChangeTime = -999f;
        private Coroutine _lerpCoroutine;
        private Coroutine _resetCoroutine;
        private HashSet<int> _ownedIndices = new HashSet<int>();

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

            _activeEmotion = emotion;
            _lastChangeTime = Time.time;
            if (duration == "before")
            {
                if (_resetCoroutine != null) StopCoroutine(_resetCoroutine);
                _resetCoroutine = StartCoroutine(ResetAfter(2.0f));
            }
        }

        private void StartLerp(Dictionary<int, float> targets)
        {
            if (_lerpCoroutine != null) StopCoroutine(_lerpCoroutine);
            _lerpCoroutine = StartCoroutine(LerpBlendshapes(targets, transitionDurationSeconds));
        }

        private IEnumerator LerpBlendshapes(Dictionary<int, float> newTargets, float duration)
        {
            if (skinnedMeshRenderer == null) yield break;

            HashSet<int> touched = new HashSet<int>(_ownedIndices);
            foreach (var idx in newTargets.Keys) touched.Add(idx);

            if (duration <= 0f)
            {
                foreach (var idx in touched)
                {
                    float target = newTargets.TryGetValue(idx, out float t) ? t : 0f;
                    skinnedMeshRenderer.SetBlendShapeWeight(idx, target);
                }
                _ownedIndices = new HashSet<int>(newTargets.Keys);
                yield break;
            }

            Dictionary<int, float> starts = new Dictionary<int, float>();
            foreach (var idx in touched) starts[idx] = skinnedMeshRenderer.GetBlendShapeWeight(idx);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float a = Mathf.Clamp01(elapsed / duration);
                foreach (var idx in touched)
                {
                    float target = newTargets.TryGetValue(idx, out float t) ? t : 0f;
                    skinnedMeshRenderer.SetBlendShapeWeight(idx, Mathf.Lerp(starts[idx], target, a));
                }
                yield return null;
            }
            foreach (var idx in touched)
            {
                float target = newTargets.TryGetValue(idx, out float t) ? t : 0f;
                skinnedMeshRenderer.SetBlendShapeWeight(idx, target);
            }

            _ownedIndices = new HashSet<int>(newTargets.Keys);
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
            StartLerp(new Dictionary<int, float>());
        }

        private Dictionary<int, float> BuildTargets(string emotion, float intensity)
        {
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
            return d;
        }

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
