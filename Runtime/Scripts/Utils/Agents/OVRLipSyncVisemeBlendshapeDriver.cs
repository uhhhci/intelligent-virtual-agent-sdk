using System.Collections.Generic;
using UnityEngine;

namespace IVH.Core.Actions
{
    /// <summary>
    /// Drives Oculus Lipsync visemes onto blendshapes that are looked up by <b>name</b> on several face
    /// meshes at once. Used for rigs that ship the 15 Oculus visemes as named blendshapes split over
    /// multiple meshes, e.g. Avatar SDK MetaPerson ("sil", "PP", ... "ou" on both AvatarHead and
    /// AvatarTeethLower, so the lower teeth follow the jaw).
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="OVRLipSyncContextMorphTargetExtended"/> (CC4), this component never writes to
    /// blendshape indices it did not resolve by name, so it is safe on meshes whose first blendshapes are
    /// identity morphs (body shape, face modifications).
    /// </remarks>
    public class OVRLipSyncVisemeBlendshapeDriver : MonoBehaviour
    {
        [Tooltip("Face meshes to drive. Every mesh that carries a viseme blendshape receives it.")]
        public List<SkinnedMeshRenderer> renderers = new List<SkinnedMeshRenderer>();

        [Tooltip("Blendshape name per viseme, in OVRLipSync.Viseme order: sil, PP, FF, TH, DD, kk, CH, SS, nn, RR, aa, E, ih, oh, ou. Leave an entry empty to skip that viseme.")]
        public string[] visemeBlendshapeNames = (string[])AvatarSdkRig.OculusVisemes.Clone();

        [Tooltip("Scales the viseme weights (1 = full shapes). Lower values give a calmer mouth.")]
        [Range(0f, 1.5f)] public float visemeGain = 0.85f;

        [Tooltip("Do not drive the 'sil' (silence) shape. On most rigs it only presses the lips together.")]
        public bool skipSilenceViseme = true;

        [Tooltip("Smoothing passed to the OVRLipSync context (1 = none, 100 = very smooth).")]
        [Range(1, 100)] public int smoothAmount = 35;

        [Tooltip("Additional per-frame smoothing of the output weights. 0 = none.")]
        [Range(0f, 0.95f)] public float outputSmoothing = 0.35f;

        private OVRLipSyncContextBase _context;
        private BlendshapeBinding[] _bindings;
        private float[] _weights;

        /// <summary>
        /// How strongly the agent is speaking right now (0 = silent, 1 = full speech), from the non-silence viseme
        /// probabilities with a fast attack and slower release. Lets other face drivers yield the mouth to lip sync.
        /// </summary>
        public float SpeechActivity { get; private set; }

        private void Start()
        {
            _context = GetComponent<OVRLipSyncContextBase>();
            if (_context == null)
            {
                Debug.LogError("[OVRLipSyncVisemeBlendshapeDriver] No OVRLipSyncContext on this GameObject; lip sync disabled.", this);
                enabled = false;
                return;
            }
            _context.Smoothing = smoothAmount;
            ResolveBindings();
        }

        /// <summary>Re-resolves the viseme names on <see cref="renderers"/>. Call after changing either list.</summary>
        public void ResolveBindings()
        {
            int count = Mathf.Min(visemeBlendshapeNames.Length, OVRLipSync.VisemeCount);
            _bindings = new BlendshapeBinding[count];
            _weights = new float[count];
            int resolved = 0;
            for (int i = 0; i < count; i++)
            {
                if (string.IsNullOrEmpty(visemeBlendshapeNames[i])) continue;
                _bindings[i] = FacialBlendshapeRig.Bind(renderers, visemeBlendshapeNames[i]);
                if (_bindings[i].IsValid) resolved++;
            }
            if (resolved == 0)
                Debug.LogWarning("[OVRLipSyncVisemeBlendshapeDriver] None of the viseme blendshapes were found on the assigned renderers; the mouth will not move.", this);
        }

        private void LateUpdate()
        {
            if (_context == null || _bindings == null) return;
            if (_context.Smoothing != smoothAmount) _context.Smoothing = smoothAmount;

            OVRLipSync.Frame frame = _context.GetCurrentPhonemeFrame();
            if (frame == null) return;

            float speech = 0f;
            for (int i = 0; i < frame.Visemes.Length; i++)
                if (i != (int)OVRLipSync.Viseme.sil) speech += frame.Visemes[i];
            speech = Mathf.Clamp01(speech);
            float timeConstant = speech > SpeechActivity ? 0.05f : 0.35f;
            SpeechActivity = Mathf.Lerp(SpeechActivity, speech, 1f - Mathf.Exp(-Time.deltaTime / timeConstant));

            for (int i = 0; i < _bindings.Length; i++)
            {
                var binding = _bindings[i];
                if (binding == null || !binding.IsValid) continue;
                if (i == (int)OVRLipSync.Viseme.sil && skipSilenceViseme) continue;

                float target = Mathf.Clamp(frame.Visemes[i] * 100f * visemeGain, 0f, 100f);
                _weights[i] = Mathf.Lerp(target, _weights[i], outputSmoothing);
                binding.Set(_weights[i]);
            }
        }

        private void OnDisable()
        {
            if (_bindings == null) return;
            foreach (var binding in _bindings) binding?.Set(0f);
        }
    }
}
