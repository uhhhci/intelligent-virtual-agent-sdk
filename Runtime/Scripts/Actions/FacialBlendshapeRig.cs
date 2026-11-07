using System;
using System.Collections.Generic;
using UnityEngine;

namespace IVH.Core.Actions
{
    /// <summary>
    /// One logical blendshape (e.g. "eyeBlinkLeft") resolved on every face mesh that carries it.
    /// Rigs such as Avatar SDK MetaPerson split the face over several meshes (head, eyelashes, lower
    /// teeth) that each carry a copy of the same shape; they must be driven together or e.g. the
    /// eyelashes stay open while the eyelid blinks.
    /// </summary>
    public sealed class BlendshapeBinding
    {
        public readonly string Name;
        private readonly List<SkinnedMeshRenderer> _renderers = new List<SkinnedMeshRenderer>();
        private readonly List<int> _indices = new List<int>();

        public BlendshapeBinding(string name) { Name = name; }

        /// <summary>True when at least one mesh carries the shape.</summary>
        public bool IsValid => _indices.Count > 0;

        internal void Add(SkinnedMeshRenderer renderer, int index)
        {
            _renderers.Add(renderer);
            _indices.Add(index);
        }

        /// <summary>Sets the weight (0-100) on every mesh carrying the shape.</summary>
        public void Set(float weight)
        {
            for (int i = 0; i < _indices.Count; i++)
            {
                if (_renderers[i] != null) _renderers[i].SetBlendShapeWeight(_indices[i], weight);
            }
        }

        /// <summary>Weight on the first mesh carrying the shape (0 when unresolved).</summary>
        public float Get()
        {
            return _indices.Count > 0 && _renderers[0] != null ? _renderers[0].GetBlendShapeWeight(_indices[0]) : 0f;
        }
    }

    /// <summary>Name-based blendshape lookup across the face meshes of an avatar.</summary>
    public static class FacialBlendshapeRig
    {
        /// <summary>
        /// Returns <paramref name="primary"/> first, followed by every other <see cref="SkinnedMeshRenderer"/>
        /// under <paramref name="root"/> that carries at least one of <paramref name="requireAnyOf"/> (or, when
        /// none are given, any blendshape). Use the filter to skip body, clothing and eyeball meshes, whose
        /// identity morphs never match facial shape names.
        /// </summary>
        public static List<SkinnedMeshRenderer> FindFaceRenderers(GameObject root, SkinnedMeshRenderer primary, params string[] requireAnyOf)
        {
            var result = new List<SkinnedMeshRenderer>();
            if (primary != null) result.Add(primary);
            if (root == null) return result;
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr == null || smr == primary || smr.sharedMesh == null || smr.sharedMesh.blendShapeCount == 0) continue;
                bool required = requireAnyOf == null || requireAnyOf.Length == 0;
                foreach (string name in requireAnyOf ?? Array.Empty<string>())
                {
                    if (FindIndex(smr.sharedMesh, name) >= 0) { required = true; break; }
                }
                if (required) result.Add(smr);
            }
            return result;
        }

        /// <summary>
        /// Index of the first candidate name present on <paramref name="mesh"/>. A candidate matches a
        /// blendshape with the exact name or with a "deformer." prefix (e.g. "mobile_51.eyeBlinkLeft"), so
        /// the lookup works regardless of how the FBX exporter named the channels.
        /// </summary>
        public static int FindIndex(Mesh mesh, params string[] candidateNames)
        {
            if (mesh == null || candidateNames == null) return -1;
            foreach (string name in candidateNames)
            {
                if (string.IsNullOrEmpty(name)) continue;
                int index = mesh.GetBlendShapeIndex(name);
                if (index >= 0) return index;
            }
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                string shape = mesh.GetBlendShapeName(i);
                int dot = shape.LastIndexOf('.');
                if (dot < 0) continue;
                string suffix = shape.Substring(dot + 1);
                foreach (string name in candidateNames)
                {
                    if (string.Equals(suffix, name, StringComparison.Ordinal)) return i;
                }
            }
            return -1;
        }

        /// <summary>Resolves one logical blendshape on every renderer that has one of the candidate names.</summary>
        public static BlendshapeBinding Bind(IEnumerable<SkinnedMeshRenderer> renderers, params string[] candidateNames)
        {
            var binding = new BlendshapeBinding(candidateNames != null && candidateNames.Length > 0 ? candidateNames[0] : "");
            if (renderers == null) return binding;
            foreach (var smr in renderers)
            {
                if (smr == null || smr.sharedMesh == null) continue;
                int index = FindIndex(smr.sharedMesh, candidateNames);
                if (index >= 0) binding.Add(smr, index);
            }
            return binding;
        }
    }

    /// <summary>Naming conventions of Avatar SDK MetaPerson exports (FBX/GLB).</summary>
    public static class AvatarSdkRig
    {
        /// <summary>Mesh carrying the full ARKit + viseme set; used as the agent's primary face renderer.</summary>
        public const string HeadMesh = "AvatarHead";
        public const string EyelashesMesh = "AvatarEyelashes";
        public const string LowerTeethMesh = "AvatarTeethLower";

        public const string EyeBlinkLeft = "eyeBlinkLeft";
        public const string EyeBlinkRight = "eyeBlinkRight";

        /// <summary>Viseme blendshape names in <c>OVRLipSync.Viseme</c> order (sil, PP, FF, ... ou).</summary>
        public static readonly string[] OculusVisemes =
        {
            "sil", "PP", "FF", "TH", "DD", "kk", "CH", "SS", "nn", "RR", "aa", "E", "ih", "oh", "ou"
        };

        /// <summary>Eye bone names, used to place the agent's vision camera.</summary>
        public const string LeftEyeBone = "LeftEye";

        /// <summary>
        /// Shapes that mark a mesh as part of the animated face (head, eyelashes, lower teeth), as opposed to
        /// body, clothing, glasses or eyeball meshes that only carry identity morphs.
        /// </summary>
        public static readonly string[] FacialMarkerShapes =
        {
            EyeBlinkLeft, "browInnerUp", "mouthSmileLeft", "jawOpen", "aa"
        };
    }
}
