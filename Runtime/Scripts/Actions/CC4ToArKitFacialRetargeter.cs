using System.Collections.Generic;
using UnityEngine;

namespace IVH.Core.Actions
{
    /// <summary>
    /// Plays Character Creator 4 facial animation clips (e.g. Reallusion Digital Soul, CC4 "Standard"
    /// blendshape profile) on rigs with ARKit blendshapes, such as Avatar SDK MetaPerson.
    /// </summary>
    /// <remarks>
    /// CC4 facial clips animate blendshapes on a mesh at the path <c>CC_Base_Body</c>. This component
    /// creates an invisible proxy <see cref="SkinnedMeshRenderer"/> with that name and the CC4 blendshape
    /// names, so the Animator plays the clips unchanged (same controller, triggers and avatar mask as on a
    /// CC4 character), and every frame maps the proxy's weights onto the ARKit blendshapes of
    /// <see cref="targetRenderers"/>. Head and neck motion in humanoid clips needs no retargeting.
    /// No clip data is copied or converted, so licensed animation assets stay untouched.
    /// <para>Eye-look shapes are left to <see cref="EyeGazeController"/>, visemes to lip sync, and blinks
    /// are combined with <see cref="CharacterBlinkBehavior"/> (the stronger of the two wins).</para>
    /// </remarks>
    [DefaultExecutionOrder(50)]
    public class CC4ToArKitFacialRetargeter : MonoBehaviour
    {
        /// <summary>Mesh path the CC4 facial clips animate.</summary>
        public const string ProxyName = "CC_Base_Body";

        [Tooltip("Face meshes with ARKit blendshapes (e.g. AvatarHead, AvatarEyelashes, AvatarTeethLower).")]
        public List<SkinnedMeshRenderer> targetRenderers = new List<SkinnedMeshRenderer>();

        [Tooltip("Procedural blinking to combine with the clips' blinks. Optional.")]
        public CharacterBlinkBehavior blink;

        [Tooltip("Scales all retargeted expression weights.")]
        [Range(0f, 1.5f)] public float expressionGain = 1f;

        [Tooltip("Scales the jaw shapes (jawOpen, jawForward, jawLeft/Right) separately; ARKit jaw shapes open wider than CC4's.")]
        [Range(0f, 1.5f)] public float jawGain = 0.8f;

        [Tooltip("Lip sync whose speech activity damps the expression's mouth shapes. Optional.")]
        public OVRLipSyncVisemeBlendshapeDriver lipSync;

        [Tooltip("While the agent speaks, mouth-shape expressions (jaw, press, roll, pucker, stretch, ...) are reduced by this fraction so the visemes own the mouth. Smiles, frowns, brows, eyes and cheeks are kept.")]
        [Range(0f, 1f)] public float mouthDampingWhileSpeaking = 0.75f;

        /// <summary>Mouth shapes that fight visemes; damped while speaking. Smile/frown/dimple combine well and are kept.</summary>
        private static readonly HashSet<string> SpeechDampedShapes = new HashSet<string>
        {
            "jawOpen", "jawForward", "jawLeft", "jawRight", "mouthClose", "mouthFunnel", "mouthPucker",
            "mouthRollLower", "mouthRollUpper", "mouthPressLeft", "mouthPressRight", "mouthShrugLower", "mouthShrugUpper",
            "mouthStretchLeft", "mouthStretchRight", "mouthLowerDownLeft", "mouthLowerDownRight",
            "mouthUpperUpLeft", "mouthUpperUpRight", "mouthLeft", "mouthRight", "cheekPuff",
        };

        private enum Kind { Normal, Jaw, Blink, MouthClose }

        private sealed class Mapping
        {
            public readonly string Target;
            public readonly Kind Kind;
            public readonly KeyValuePair<string, float>[] Sources;
            public BlendshapeBinding Binding;
            public int[] SourceIndices;
            public bool SpeechDamped;

            public Mapping(string target, Kind kind, params (string source, float weight)[] sources)
            {
                Target = target;
                Kind = kind;
                Sources = new KeyValuePair<string, float>[sources.Length];
                for (int i = 0; i < sources.Length; i++) Sources[i] = new KeyValuePair<string, float>(sources[i].source, sources[i].weight);
            }
        }

        // CC4 Standard profile -> ARKit. Both name sides anatomically (the character's own left/right).
        private static Mapping[] CreateMappings() => new[]
        {
            new Mapping("browDownLeft", Kind.Normal, ("Brow_Drop_L", 1f)),
            new Mapping("browDownRight", Kind.Normal, ("Brow_Drop_R", 1f)),
            new Mapping("browInnerUp", Kind.Normal, ("Brow_Raise_Inner_L", 0.5f), ("Brow_Raise_Inner_R", 0.5f)),
            new Mapping("browOuterUpLeft", Kind.Normal, ("Brow_Raise_Outer_L", 1f)),
            new Mapping("browOuterUpRight", Kind.Normal, ("Brow_Raise_Outer_R", 1f)),
            new Mapping("cheekPuff", Kind.Normal, ("Cheek_Puff_L", 0.5f), ("Cheek_Puff_R", 0.5f)),
            new Mapping("cheekSquintLeft", Kind.Normal, ("Cheek_Raise_L", 1f)),
            new Mapping("cheekSquintRight", Kind.Normal, ("Cheek_Raise_R", 1f)),
            new Mapping("eyeBlinkLeft", Kind.Blink, ("Eye_Blink_L", 1f)),
            new Mapping("eyeBlinkRight", Kind.Blink, ("Eye_Blink_R", 1f)),
            new Mapping("eyeSquintLeft", Kind.Normal, ("Eye_Squint_L", 1f)),
            new Mapping("eyeSquintRight", Kind.Normal, ("Eye_Squint_R", 1f)),
            new Mapping("eyeWideLeft", Kind.Normal, ("Eye_Wide_L", 1f)),
            new Mapping("eyeWideRight", Kind.Normal, ("Eye_Wide_R", 1f)),
            new Mapping("jawOpen", Kind.Jaw, ("Jaw_Open", 1f)),
            new Mapping("jawForward", Kind.Jaw, ("Jaw_Forward", 0.5f)),
            new Mapping("jawLeft", Kind.Jaw, ("Jaw_L", 0.5f)),
            new Mapping("jawRight", Kind.Jaw, ("Jaw_R", 0.5f)),
            new Mapping("mouthClose", Kind.MouthClose, ("Mouth_Close", 1f)),
            new Mapping("mouthDimpleLeft", Kind.Normal, ("Mouth_Dimple_L", 1f)),
            new Mapping("mouthDimpleRight", Kind.Normal, ("Mouth_Dimple_R", 1f)),
            new Mapping("mouthFrownLeft", Kind.Normal, ("Mouth_Frown_L", 1f)),
            new Mapping("mouthFrownRight", Kind.Normal, ("Mouth_Frown_R", 1f)),
            new Mapping("mouthFunnel", Kind.Normal, ("Mouth_Funnel", 1f), ("Mouth_Funnel_Up_L", 0.25f), ("Mouth_Funnel_Up_R", 0.25f), ("Mouth_Funnel_Down_L", 0.25f), ("Mouth_Funnel_Down_R", 0.25f)),
            new Mapping("mouthLeft", Kind.Normal, ("Mouth_L", 1f)),
            new Mapping("mouthRight", Kind.Normal, ("Mouth_R", 1f)),
            new Mapping("mouthLowerDownLeft", Kind.Normal, ("Mouth_Down_Lower_L", 1f)),
            new Mapping("mouthLowerDownRight", Kind.Normal, ("Mouth_Down_Lower_R", 1f)),
            new Mapping("mouthPressLeft", Kind.Normal, ("Mouth_Press_L", 1f)),
            new Mapping("mouthPressRight", Kind.Normal, ("Mouth_Press_R", 1f)),
            new Mapping("mouthPucker", Kind.Normal, ("Mouth_Pucker", 1f), ("Mouth_Pucker_Up_L", 0.25f), ("Mouth_Pucker_Up_R", 0.25f), ("Mouth_Pucker_Down_L", 0.25f), ("Mouth_Pucker_Down_R", 0.25f)),
            new Mapping("mouthRollLower", Kind.Normal, ("Mouth_Roll_In_Lower", 1f), ("Mouth_Roll_In_Lower_L", 0.5f), ("Mouth_Roll_In_Lower_R", 0.5f)),
            new Mapping("mouthRollUpper", Kind.Normal, ("Mouth_Roll_In_Upper", 1f), ("Mouth_Roll_In_Upper_L", 0.5f), ("Mouth_Roll_In_Upper_R", 0.5f)),
            new Mapping("mouthShrugLower", Kind.Normal, ("Mouth_Shrug_Lower", 1f)),
            new Mapping("mouthShrugUpper", Kind.Normal, ("Mouth_Shrug_Upper", 1f)),
            new Mapping("mouthSmileLeft", Kind.Normal, ("Mouth_Smile_L", 1f), ("Mouth_Smile_Sharp_L", 0.5f)),
            new Mapping("mouthSmileRight", Kind.Normal, ("Mouth_Smile_R", 1f), ("Mouth_Smile_Sharp_R", 0.5f)),
            new Mapping("mouthStretchLeft", Kind.Normal, ("Mouth_Stretch_L", 1f)),
            new Mapping("mouthStretchRight", Kind.Normal, ("Mouth_Stretch_R", 1f)),
            new Mapping("mouthUpperUpLeft", Kind.Normal, ("Mouth_Up_Upper_L", 1f)),
            new Mapping("mouthUpperUpRight", Kind.Normal, ("Mouth_Up_Upper_R", 1f)),
            new Mapping("noseSneerLeft", Kind.Normal, ("Nose_Sneer_L", 1f)),
            new Mapping("noseSneerRight", Kind.Normal, ("Nose_Sneer_R", 1f)),
        };

        private Mapping[] _mappings;
        private SkinnedMeshRenderer _proxy;
        private Mapping _jawOpen;

        /// <summary>The proxy renderer the Animator writes the CC4 weights to.</summary>
        public SkinnedMeshRenderer Proxy => _proxy;

        private void Awake()
        {
            _mappings = CreateMappings();
            _proxy = CreateProxy();

            foreach (var m in _mappings)
            {
                m.Binding = FacialBlendshapeRig.Bind(targetRenderers, m.Target);
                m.SpeechDamped = SpeechDampedShapes.Contains(m.Target);
                m.SourceIndices = new int[m.Sources.Length];
                for (int i = 0; i < m.Sources.Length; i++)
                    m.SourceIndices[i] = _proxy.sharedMesh.GetBlendShapeIndex(m.Sources[i].Key);
                if (m.Target == "jawOpen") _jawOpen = m;
            }

            // The Animator resolved its curve bindings before the proxy existed.
            var animator = GetComponent<Animator>();
            if (animator != null) animator.Rebind();
        }

        private SkinnedMeshRenderer CreateProxy()
        {
            Transform existing = transform.Find(ProxyName);
            var go = existing != null ? existing.gameObject : new GameObject(ProxyName);
            go.transform.SetParent(transform, false);

            var smr = go.GetComponent<SkinnedMeshRenderer>();
            if (smr == null) smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = BuildProxyMesh();
            smr.forceRenderingOff = true;
            smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            smr.updateWhenOffscreen = true;
            return smr;
        }

        /// <summary>A single invisible triangle carrying every CC4 blendshape name the mappings read.</summary>
        private Mesh BuildProxyMesh()
        {
            var mesh = new Mesh { name = "CC4FacialProxy" };
            mesh.vertices = new[] { Vector3.zero, Vector3.right * 0.001f, Vector3.up * 0.001f };
            mesh.triangles = new[] { 0, 1, 2 };
            var zero = new Vector3[3];
            var added = new HashSet<string>();
            foreach (var m in _mappings)
            {
                foreach (var source in m.Sources)
                {
                    if (added.Add(source.Key)) mesh.AddBlendShapeFrame(source.Key, 100f, zero, null, null);
                }
            }
            return mesh;
        }

        private float _mouthFactor = 1f;

        /// <summary>Current multiplier on mouth shapes (1 = silent, lower while speaking).</summary>
        public float MouthFactor => _mouthFactor;

        private void LateUpdate()
        {
            if (_proxy == null) return;
            float speaking = lipSync != null ? Mathf.Clamp01(lipSync.SpeechActivity / 0.35f) : 0f;
            _mouthFactor = 1f - mouthDampingWhileSpeaking * speaking;
            float jawOpen = 0f;

            // jawOpen first: mouthClose is only meaningful while the jaw is open.
            if (_jawOpen != null) jawOpen = Apply(_jawOpen, 0f);
            foreach (var m in _mappings)
            {
                if (m != _jawOpen) Apply(m, jawOpen);
            }
        }

        private float Apply(Mapping m, float jawOpen)
        {
            if (!m.Binding.IsValid) return 0f;
            float value = 0f;
            for (int i = 0; i < m.SourceIndices.Length; i++)
            {
                int index = m.SourceIndices[i];
                if (index >= 0) value += _proxy.GetBlendShapeWeight(index) * m.Sources[i].Value;
            }

            switch (m.Kind)
            {
                case Kind.Jaw: value *= jawGain; break;
                case Kind.MouthClose: value = Mathf.Min(value * expressionGain, jawOpen); break;
                case Kind.Blink:
                    value *= expressionGain;
                    if (blink != null) value = Mathf.Max(value, blink.CurrentWeight);
                    break;
                default: value *= expressionGain; break;
            }
            if (m.SpeechDamped) value *= _mouthFactor;

            value = Mathf.Clamp(value, 0f, 100f);
            m.Binding.Set(value);
            return value;
        }

        private void OnDestroy()
        {
            if (_proxy != null && _proxy.sharedMesh != null) Destroy(_proxy.sharedMesh);
        }
    }
}
