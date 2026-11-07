using System.Collections.Generic;
using UnityEngine;

namespace IVH.Core.Actions
{
    /// <summary>
    /// Alternative animator-trigger spellings for action classes. Some shipped animator controllers name a
    /// trigger differently from the SDK class (e.g. CC4 controllers use <c>HelplessSighed</c> for the
    /// <c>HelplesslySighed</c> class), which used to make those animations unreachable. The class name is
    /// always tried first, so controllers that already match are unaffected.
    /// </summary>
    public static class AnimationTriggerAliases
    {
        private static readonly Dictionary<string, string[]> Aliases = new Dictionary<string, string[]>
        {
            // Body motions
            ["HelloWithRaiseHand"] = new[] { "GreetWithRaiseHand" },
            ["TellingASecret"] = new[] { "TellingASecrete" },
            ["Acknowledging"] = new[] { "Acknowleding" },
            ["HeadNodSarcastic"] = new[] { "HeadNodSacarstic" },
            // CC4 facial expressions
            ["HelplesslySighed"] = new[] { "HelplessSighed" },
            ["TouchingSmile"] = new[] { "TouchingExpression" },
            ["ShowingDisbeliefPositiveWay"] = new[] { "ShowingDisbelfPositiveWay" },
            ["Irritated"] = new[] { "Iratated" },
            ["ListeningHappily"] = new[] { "ListeningHapiily" },
            ["ConfusedUnhappy"] = new[] { "Confused" },
        };

        /// <summary>The class name followed by its known alternative trigger spellings.</summary>
        public static IEnumerable<string> Candidates(string actionName)
        {
            yield return actionName;
            if (Aliases.TryGetValue(actionName, out var alternatives))
                foreach (string alternative in alternatives) yield return alternative;
        }

        /// <summary>True when any spelling of <paramref name="actionName"/> is in <paramref name="triggers"/>.</summary>
        public static bool IsAvailable(string actionName, ICollection<string> triggers)
        {
            foreach (string candidate in Candidates(actionName))
                if (triggers.Contains(candidate)) return true;
            return false;
        }

        /// <summary>
        /// The trigger to set on <paramref name="animator"/> for <paramref name="actionName"/>: the first spelling
        /// the controller defines, or the class name when none matches.
        /// </summary>
        public static string Resolve(string actionName, Animator animator)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return actionName;
            var triggers = AnimatorCapabilityInspector.GetTriggerParameters(animator);
            foreach (string candidate in Candidates(actionName))
                if (triggers.Contains(candidate)) return candidate;
            return actionName;
        }
    }
}
