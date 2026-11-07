using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace IVH.Core.EditorScripts.AnimationTools
{
    /// <summary>
    /// Builds one animator controller from a <b>body</b> controller (e.g. <c>JawFixActionController</c> from the
    /// Mixamo asset package) and the facial layer of another controller (e.g. the CC4 <c>FacialExpression</c> layer
    /// of <c>DeafultCC4CharacterAnimatorMale</c>), so body actions and facial expressions can live in separate
    /// packages and still drive one Animator.
    /// </summary>
    /// <remarks>
    /// The facial layer is split in two:
    /// <list type="bullet">
    /// <item><c>FacialExpression</c>: the copied states, transitions and triggers, masked to the face mesh only
    /// (no humanoid parts), so it never overrides the body.</item>
    /// <item><c>FacialHeadMotion</c>: a layer synced to it, masked to the humanoid head, carrying the clips' head
    /// motion. <see cref="IVH.Core.Actions.AgentBodyMotionController"/> fades it out while a body action plays, so
    /// Mixamo nods and head shakes stay visible.</item>
    /// </list>
    /// The recipe is stored in the output's importer userData, and the output is recomposed automatically whenever
    /// one of its source controllers changes (keeping its GUID, so scenes stay linked).
    /// </remarks>
    public static class AnimatorControllerComposer
    {
        public const string FacialLayerName = "FacialExpression";
        public const string HeadLayerName = "FacialHeadMotion";
        private const string RecipePrefix = "IVA_COMPOSED_CONTROLLER|";

        internal static bool IsComposing { get; private set; }

        /// <summary>Recipe stored on a composed controller.</summary>
        public struct Recipe
        {
            public string bodyGuid;
            public string facialGuid;
            public string facialLayer;
        }

        /// <summary>
        /// Creates or updates <paramref name="outputPath"/> = all layers of <paramref name="body"/> + the
        /// <paramref name="facialLayer"/> layer of <paramref name="facialSource"/>. Returns the composed controller.
        /// </summary>
        public static AnimatorController Compose(AnimatorController body, AnimatorController facialSource, string outputPath, string facialLayer = FacialLayerName)
        {
            if (body == null || facialSource == null) throw new ArgumentNullException(body == null ? nameof(body) : nameof(facialSource));
            string bodyPath = AssetDatabase.GetAssetPath(body);
            string facialPath = AssetDatabase.GetAssetPath(facialSource);
            AnimatorControllerLayer sourceLayer = facialSource.layers.FirstOrDefault(l => l.name == facialLayer)
                ?? throw new InvalidOperationException($"'{facialSource.name}' has no layer named '{facialLayer}'.");

            IsComposing = true;
            try
            {
                // 1. Start from an exact copy of the body controller. Overwriting the file keeps the output's GUID.
                string folder = Path.GetDirectoryName(outputPath)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(folder) && !AssetDatabase.IsValidFolder(folder)) CreateFolders(folder);
                if (AssetDatabase.LoadAssetAtPath<AnimatorController>(outputPath) != null)
                {
                    File.WriteAllText(FileUtil.GetPhysicalPath(outputPath), File.ReadAllText(FileUtil.GetPhysicalPath(bodyPath)));
                    AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                }
                else if (!AssetDatabase.CopyAsset(bodyPath, outputPath))
                {
                    throw new InvalidOperationException($"Could not copy {bodyPath} to {outputPath}");
                }

                var composed = AssetDatabase.LoadAssetAtPath<AnimatorController>(outputPath);
                composed.name = Path.GetFileNameWithoutExtension(outputPath);

                // 2. Parameters the facial layer uses.
                var used = new HashSet<string>(AllTransitions(sourceLayer.stateMachine).SelectMany(t => t.conditions).Select(c => c.parameter));
                foreach (var p in facialSource.parameters)
                {
                    if (!used.Contains(p.name) || composed.parameters.Any(q => q.name == p.name)) continue;
                    composed.AddParameter(new AnimatorControllerParameter
                    {
                        name = p.name, type = p.type,
                        defaultBool = p.defaultBool, defaultFloat = p.defaultFloat, defaultInt = p.defaultInt,
                    });
                }

                // 3. Face-only copy of the facial layer.
                var stateMachine = new AnimatorStateMachine { name = sourceLayer.stateMachine.name, hideFlags = HideFlags.HideInHierarchy };
                AssetDatabase.AddObjectToAsset(stateMachine, composed);
                CopyStateMachine(sourceLayer.stateMachine, stateMachine);

                AvatarMask faceMask = CreateFaceOnlyMask(sourceLayer.avatarMask);
                AvatarMask headMask = CreateHeadOnlyMask();
                AssetDatabase.AddObjectToAsset(faceMask, composed);
                AssetDatabase.AddObjectToAsset(headMask, composed);

                composed.AddLayer(new AnimatorControllerLayer
                {
                    name = facialLayer,
                    stateMachine = stateMachine,
                    avatarMask = faceMask,
                    blendingMode = sourceLayer.blendingMode,
                    defaultWeight = 1f,
                    iKPass = false,
                });
                int facialIndex = composed.layers.Length - 1;

                // 4. Head motion of the same clips, on a synced layer whose weight the body controller can fade.
                //    Synced layers play the source layer's states; their own state machine stays empty.
                var unusedStateMachine = new AnimatorStateMachine { name = HeadLayerName, hideFlags = HideFlags.HideInHierarchy };
                AssetDatabase.AddObjectToAsset(unusedStateMachine, composed);
                var headLayer = new AnimatorControllerLayer
                {
                    name = HeadLayerName,
                    stateMachine = unusedStateMachine,
                    syncedLayerIndex = facialIndex,
                    syncedLayerAffectsTiming = false,
                    avatarMask = headMask,
                    blendingMode = AnimatorLayerBlendingMode.Override,
                    defaultWeight = 1f,
                    iKPass = true, // head look-at must run after this layer overrides the head
                };
                // A synced layer plays nothing unless each state gets a motion: reuse the facial clips.
                foreach (var child in stateMachine.states)
                    if (child.state.motion != null) headLayer.SetOverrideMotion(child.state, child.state.motion);
                composed.AddLayer(headLayer);

                EditorUtility.SetDirty(composed);
                AssetDatabase.SaveAssets();
                StoreRecipe(outputPath, new Recipe
                {
                    bodyGuid = AssetDatabase.AssetPathToGUID(bodyPath),
                    facialGuid = AssetDatabase.AssetPathToGUID(facialPath),
                    facialLayer = facialLayer,
                });
                Debug.Log($"[IVA] Composed '{outputPath}': {body.name} + {facialSource.name}/{facialLayer} ({stateMachine.states.Length} facial states).");
                return composed;
            }
            finally
            {
                IsComposing = false;
            }
        }

        /// <summary>Recomposes a controller created by <see cref="Compose"/> from its stored recipe.</summary>
        public static bool Recompose(string outputPath)
        {
            if (!TryGetRecipe(outputPath, out Recipe recipe)) return false;
            var body = AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GUIDToAssetPath(recipe.bodyGuid));
            var facial = AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GUIDToAssetPath(recipe.facialGuid));
            if (body == null || facial == null)
            {
                Debug.LogWarning($"[IVA] Cannot recompose '{outputPath}': a source controller is missing.");
                return false;
            }
            Compose(body, facial, outputPath, recipe.facialLayer);
            return true;
        }

        public static bool TryGetRecipe(string controllerPath, out Recipe recipe)
        {
            recipe = default;
            string data = AssetImporter.GetAtPath(controllerPath)?.userData;
            if (string.IsNullOrEmpty(data) || !data.StartsWith(RecipePrefix)) return false;
            string[] parts = data.Substring(RecipePrefix.Length).Split('|');
            if (parts.Length < 3) return false;
            recipe = new Recipe { bodyGuid = parts[0], facialGuid = parts[1], facialLayer = parts[2] };
            return true;
        }

        private static void StoreRecipe(string outputPath, Recipe recipe)
        {
            var importer = AssetImporter.GetAtPath(outputPath);
            string data = $"{RecipePrefix}{recipe.bodyGuid}|{recipe.facialGuid}|{recipe.facialLayer}";
            if (importer == null || importer.userData == data) return;
            importer.userData = data;
            importer.SaveAndReimport();
        }

        // ------------------------------------------------------------------ copying

        private static void CopyStateMachine(AnimatorStateMachine source, AnimatorStateMachine target)
        {
            target.anyStatePosition = source.anyStatePosition;
            target.entryPosition = source.entryPosition;
            target.exitPosition = source.exitPosition;

            if (source.stateMachines.Length > 0)
                Debug.LogWarning($"[IVA] '{source.name}' has sub-state machines; only its top-level states are copied.");

            var map = new Dictionary<AnimatorState, AnimatorState>();
            foreach (var child in source.states)
            {
                AnimatorState s = target.AddState(child.state.name, child.position);
                CopyState(child.state, s);
                map[child.state] = s;
            }
            if (source.defaultState != null && map.TryGetValue(source.defaultState, out var defaultState))
                target.defaultState = defaultState;

            foreach (var child in source.states)
            {
                foreach (var t in child.state.transitions)
                {
                    AnimatorStateTransition copy = t.isExit ? map[child.state].AddExitTransition()
                        : t.destinationState != null && map.TryGetValue(t.destinationState, out var dst) ? map[child.state].AddTransition(dst)
                        : null;
                    if (copy != null) CopyTransition(t, copy);
                }
            }
            foreach (var t in source.anyStateTransitions)
            {
                if (t.destinationState != null && map.TryGetValue(t.destinationState, out var dst))
                    CopyTransition(t, target.AddAnyStateTransition(dst));
            }
            foreach (var t in source.entryTransitions)
            {
                if (t.destinationState != null && map.TryGetValue(t.destinationState, out var dst))
                {
                    var copy = target.AddEntryTransition(dst);
                    copy.conditions = t.conditions;
                }
            }
        }

        private static void CopyState(AnimatorState source, AnimatorState target)
        {
            target.motion = source.motion; // clips are referenced, never copied
            target.speed = source.speed;
            target.speedParameter = source.speedParameter;
            target.speedParameterActive = source.speedParameterActive;
            target.cycleOffset = source.cycleOffset;
            target.cycleOffsetParameter = source.cycleOffsetParameter;
            target.cycleOffsetParameterActive = source.cycleOffsetParameterActive;
            target.mirror = source.mirror;
            target.mirrorParameter = source.mirrorParameter;
            target.mirrorParameterActive = source.mirrorParameterActive;
            target.timeParameter = source.timeParameter;
            target.timeParameterActive = source.timeParameterActive;
            target.iKOnFeet = source.iKOnFeet;
            target.writeDefaultValues = source.writeDefaultValues;
            target.tag = source.tag;
        }

        private static void CopyTransition(AnimatorStateTransition source, AnimatorStateTransition target)
        {
            target.name = source.name;
            target.conditions = source.conditions;
            target.hasExitTime = source.hasExitTime;
            target.exitTime = source.exitTime;
            target.hasFixedDuration = source.hasFixedDuration;
            target.duration = source.duration;
            target.offset = source.offset;
            target.interruptionSource = source.interruptionSource;
            target.orderedInterruption = source.orderedInterruption;
            target.canTransitionToSelf = source.canTransitionToSelf;
            target.mute = source.mute;
            target.solo = source.solo;
        }

        private static IEnumerable<AnimatorTransitionBase> AllTransitions(AnimatorStateMachine sm)
        {
            foreach (var t in sm.anyStateTransitions) yield return t;
            foreach (var t in sm.entryTransitions) yield return t;
            foreach (var child in sm.states)
                foreach (var t in child.state.transitions) yield return t;
        }

        /// <summary>Source mask with every humanoid body part off: the layer drives only face-mesh curves.</summary>
        private static AvatarMask CreateFaceOnlyMask(AvatarMask source)
        {
            var mask = source != null ? UnityEngine.Object.Instantiate(source) : new AvatarMask();
            mask.name = "FaceOnly";
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
            return mask;
        }

        /// <summary>
        /// Humanoid head (neck, head, jaw, eyes) only. Deliberately no transform entries: an inactive root entry
        /// silences the whole layer. The face curves this layer also carries are harmless: it plays the same clips
        /// in sync with the face layer, so they write identical values.
        /// </summary>
        private static AvatarMask CreateHeadOnlyMask()
        {
            var mask = new AvatarMask { name = "HeadOnly" };
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, (AvatarMaskBodyPart)i == AvatarMaskBodyPart.Head);
            return mask;
        }

        private static void CreateFolders(string folder)
        {
            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) CreateFolders(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        // ------------------------------------------------------------------ menu

        [MenuItem("IVA SDK/Animation/Compose Body + Facial Controller...")]
        private static void OpenWindow() => EditorWindow.GetWindow<AnimatorControllerComposerWindow>(true, "Compose Animator Controller");

        [MenuItem("IVA SDK/Animation/Recompose Selected Controller")]
        private static void RecomposeSelected()
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (!Recompose(path)) EditorUtility.DisplayDialog("IVA SDK", "The selection is not a controller composed by the IVA SDK.", "OK");
        }

        [MenuItem("IVA SDK/Animation/Recompose Selected Controller", true)]
        private static bool RecomposeSelectedValidate() => TryGetRecipe(AssetDatabase.GetAssetPath(Selection.activeObject), out _);
    }

    internal class AnimatorControllerComposerWindow : EditorWindow
    {
        private AnimatorController _body;
        private AnimatorController _facial;
        private string _layer = AnimatorControllerComposer.FacialLayerName;

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Body controller: all its layers (e.g. JawFixActionController from the Mixamo package).\n" +
                                    "Facial controller: the layer named below (e.g. FacialExpression of a CC4 controller).\n" +
                                    "The result is recomposed automatically when either source changes.", MessageType.None);
            _body = (AnimatorController)EditorGUILayout.ObjectField("Body Controller", _body, typeof(AnimatorController), false);
            _facial = (AnimatorController)EditorGUILayout.ObjectField("Facial Controller", _facial, typeof(AnimatorController), false);
            _layer = EditorGUILayout.TextField("Facial Layer", _layer);
            using (new EditorGUI.DisabledScope(_body == null || _facial == null))
            {
                if (GUILayout.Button("Compose..."))
                {
                    string path = EditorUtility.SaveFilePanelInProject("Composed controller", _body.name + "+Face", "controller", "Where to save the composed controller");
                    if (!string.IsNullOrEmpty(path))
                    {
                        var composed = AnimatorControllerComposer.Compose(_body, _facial, path, _layer);
                        Selection.activeObject = composed;
                    }
                }
            }
        }
    }

    /// <summary>Recomposes composed controllers when one of their sources is imported or saved.</summary>
    internal class ComposedControllerWatcher : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            ScheduleFor(imported);
        }

        internal static void ScheduleFor(IEnumerable<string> paths)
        {
            if (AnimatorControllerComposer.IsComposing) return;
            var changed = new HashSet<string>(paths.Where(p => p.EndsWith(".controller", StringComparison.OrdinalIgnoreCase))
                                                   .Select(AssetDatabase.AssetPathToGUID));
            if (changed.Count == 0) return;

            foreach (string guid in AssetDatabase.FindAssets("t:AnimatorController"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (changed.Contains(guid)) continue; // never recompose a controller because it changed itself
                if (!AnimatorControllerComposer.TryGetRecipe(path, out var recipe)) continue;
                if (!changed.Contains(recipe.bodyGuid) && !changed.Contains(recipe.facialGuid)) continue;
                EditorApplication.delayCall += () =>
                {
                    Debug.Log($"[IVA] A source of '{path}' changed; recomposing.");
                    AnimatorControllerComposer.Recompose(path);
                };
            }
        }
    }

    /// <summary>Animator-window edits are saved, not re-imported: catch those too.</summary>
    internal class ComposedControllerSaveWatcher : AssetModificationProcessor
    {
        private static string[] OnWillSaveAssets(string[] paths)
        {
            ComposedControllerWatcher.ScheduleFor(paths);
            return paths;
        }
    }
}
