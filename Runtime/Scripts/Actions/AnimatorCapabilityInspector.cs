using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor.Animations;
#endif

namespace IVH.Core.Actions
{
    /// <summary>
    /// Determines which animator triggers actually play something, so the agent is only offered (and
    /// only describes) body motions and facial clips its animator controller can perform. An action
    /// class whose trigger is missing from the controller, or whose trigger has no transition into a
    /// state with a clip, silently does nothing when the model picks it.
    /// </summary>
    public static class AnimatorCapabilityInspector
    {
        /// <summary>
        /// Trigger names that start a transition into a state with a motion. Editor only: returns
        /// <c>null</c> in player builds or when the controller cannot be inspected.
        /// </summary>
        public static HashSet<string> GetPlayableTriggers(RuntimeAnimatorController controller)
        {
#if UNITY_EDITOR
            if (controller is AnimatorOverrideController overrideController)
                controller = overrideController.runtimeAnimatorController; // overrides swap clips, not transitions
            if (!(controller is AnimatorController editorController)) return null;

            var triggers = new HashSet<string>();
            foreach (var p in editorController.parameters)
                if (p.type == AnimatorControllerParameterType.Trigger) triggers.Add(p.name);

            var playable = new HashSet<string>();
            foreach (var layer in editorController.layers)
            {
                if (layer.stateMachine != null) Visit(layer.stateMachine, triggers, playable);
            }
            return playable;
#else
            return null;
#endif
        }

        /// <summary>All trigger parameters of the animator's controller (runtime fallback; no transition check).</summary>
        public static HashSet<string> GetTriggerParameters(Animator animator)
        {
            var result = new HashSet<string>();
            if (animator == null || animator.runtimeAnimatorController == null) return result;
            foreach (var p in animator.parameters)
                if (p.type == AnimatorControllerParameterType.Trigger) result.Add(p.name);
            return result;
        }

#if UNITY_EDITOR
        private static void Visit(AnimatorStateMachine stateMachine, HashSet<string> triggers, HashSet<string> playable)
        {
            foreach (var t in stateMachine.anyStateTransitions) Check(t, triggers, playable);
            foreach (var t in stateMachine.entryTransitions) Check(t, triggers, playable);
            foreach (var child in stateMachine.states)
            {
                if (child.state == null) continue;
                foreach (var t in child.state.transitions) Check(t, triggers, playable);
            }
            foreach (var child in stateMachine.stateMachines)
            {
                if (child.stateMachine == null) continue;
                foreach (var t in stateMachine.GetStateMachineTransitions(child.stateMachine)) Check(t, triggers, playable);
                Visit(child.stateMachine, triggers, playable);
            }
        }

        private static void Check(AnimatorTransitionBase transition, HashSet<string> triggers, HashSet<string> playable)
        {
            if (transition == null || transition.conditions == null) return;
            bool leadsToMotion =
                (transition.destinationState != null && transition.destinationState.motion != null) ||
                transition.destinationStateMachine != null;
            if (!leadsToMotion) return;

            foreach (var condition in transition.conditions)
            {
                if (triggers.Contains(condition.parameter)) playable.Add(condition.parameter);
            }
        }
#endif
    }
}
