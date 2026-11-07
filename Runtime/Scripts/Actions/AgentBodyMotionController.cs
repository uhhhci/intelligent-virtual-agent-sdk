using System.Net;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using System;
using IVH.Core.ServiceConnector;

namespace IVH.Core.Actions{

    public class AgentBodyMotionController : MonoBehaviour
    {
        public List<AgentAction> avaliableActions = AgentActions.GetAvailableActions();

        [Tooltip("Animator layer that carries the head motion of facial clips (created by IVA SDK ▸ Animation ▸ Compose Body + Facial Controller). It is faded out while a body action plays, so nods and head shakes are not overridden by the facial clip's head motion. Ignored when the controller has no such layer.")]
        public string facialHeadLayerName = "FacialHeadMotion";

        [Tooltip("Seconds to fade the facial head-motion layer out and back in around body actions.")]
        [Range(0.05f, 2f)] public float headLayerFadeSeconds = 0.35f;

        private Animator animator;
        private int _headLayer = -1;
        private int _idleStateHash;
        private bool _idleCaptured;
        // Initialize with the available actions


        private void Start()
        {
            // Get the Animator component attached to the avatar
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                Debug.LogError("Animator component is missing on the humanoid avatar.");
            }
            else if (animator.runtimeAnimatorController != null && !string.IsNullOrEmpty(facialHeadLayerName))
            {
                _headLayer = animator.GetLayerIndex(facialHeadLayerName);
            }

        }

        private void Update()
        {
            if (_headLayer < 0 || animator == null) return;

            // The body layer starts in its idle state; anything else (an action, walking, a transition) is "busy".
            AnimatorStateInfo body = animator.GetCurrentAnimatorStateInfo(0);
            if (!_idleCaptured) { _idleStateHash = body.fullPathHash; _idleCaptured = true; }
            bool bodyBusy = animator.IsInTransition(0) || body.fullPathHash != _idleStateHash;

            float weight = Mathf.MoveTowards(animator.GetLayerWeight(_headLayer), bodyBusy ? 0f : 1f, Time.deltaTime / headLayerFadeSeconds);
            animator.SetLayerWeight(_headLayer, weight);
        }

        // Populate the available actions
        public void PopulateActions()
        {

            // Add actions here. This is where you initialize your actions.
            avaliableActions = AgentActions.GetAvailableActions();
        }

        // Trigger an action by its name
        public void TriggerActionViaActionName(string actionName)
        {
            foreach (AgentAction action in avaliableActions)
            {

                if (action.GetType().Name == actionName)
                {
                    Debug.Log("triggering action with name:" + action.GetType().Name);
                    //action.TriggerSimpleAnimation(animator);
                    animator.SetTrigger(AnimationTriggerAliases.Resolve(action.GetType().Name, animator));

                    return;
                }
            }

            Debug.LogWarning($"Action with name '{actionName}' not found.");
        }

        // Function to convert enabled actions into List<GPTToolItem>
        public List<GPTToolItem> GetEnabledActionsAsGPTToolItems()
        {
            List<GPTToolItem> tools = new List<GPTToolItem>();
            foreach (AgentAction action in avaliableActions)
            {
                tools.Add(action.ToGPTToolItem());
            }
            return tools;
        }

        public List<GPTToolItem> GetDetailActionsFiltered(BodyActionFilter actionFilter, Gender gender = Gender.Nonbinary, BodyAnimationControllerType bodyAnimationType = BodyAnimationControllerType.Rocketbox)
        {
            return FilterActions(actionFilter, gender, bodyAnimationType).Select(action => action.ToGPTToolItem()).ToList();
        }

        public List<String> GetSimpleActionName()
        {
            List<String> toolNames = new List<String>();
            foreach (AgentAction action in avaliableActions)
            {
                toolNames.Add(action.Name);
            }
            return toolNames;
        }

        public List<string> GetSimpleActionNameFiltered(BodyActionFilter actionFilter, Gender gender = Gender.Nonbinary, BodyAnimationControllerType bodyAnimationType = BodyAnimationControllerType.Rocketbox )
        {
            return FilterActions(actionFilter, gender, bodyAnimationType).Select(action => action.Name).ToList();
        }

        /// <summary>
        /// Actions matching the sentiment, gender and animation-source filters. When
        /// <paramref name="playableTriggers"/> is given, only actions whose trigger the animator can
        /// actually play are kept, and <paramref name="hiddenActions"/> are always removed.
        /// </summary>
        public List<AgentAction> GetActionsFiltered(BodyActionFilter actionFilter, Gender gender, BodyAnimationControllerType bodyAnimationType,
            ICollection<string> playableTriggers = null, ICollection<string> hiddenActions = null)
        {
            IEnumerable<AgentAction> actions = FilterActions(actionFilter, gender, bodyAnimationType);
            if (playableTriggers != null) actions = actions.Where(action => AnimationTriggerAliases.IsAvailable(action.GetType().Name, playableTriggers));
            if (hiddenActions != null) actions = actions.Where(action => !hiddenActions.Contains(action.GetType().Name));
            return actions.ToList();
        }

        private IEnumerable<AgentAction> FilterActions(BodyActionFilter actionFilter, Gender gender, BodyAnimationControllerType bodyAnimationType)
        {
            IEnumerable<AgentAction> filteredActions = avaliableActions.AsEnumerable();

            // First, filter by the BodyActionFilter
            if (actionFilter != BodyActionFilter.NONE)
            {
                string filterTag = actionFilter.ToString();
                filteredActions = filteredActions.Where(action =>
                    action.Categories != null &&
                    (action.Categories.Contains(filterTag) || action.Categories.Contains("Neutral")));
            }

            // Second, filter by Gender. Nonbinary keeps every animation.
            if (gender == Gender.Female)
            {
                filteredActions = filteredActions.Where(action =>
                    action.Categories != null &&
                    (action.Categories.Contains("Female") || action.Categories.Contains("GenderNeutral")));
            }
            else if (gender == Gender.Male)
            {
                filteredActions = filteredActions.Where(action =>
                    action.Categories != null &&
                    (action.Categories.Contains("Male") || action.Categories.Contains("GenderNeutral")));
            }

            // Third, filter by animation type (e.g. rocketbox vs. mixamo)
            if (bodyAnimationType == BodyAnimationControllerType.Rocketbox)
            {
                filteredActions = filteredActions.Where(action =>
                    action.Categories != null &&
                    action.Categories.Contains("Rocketbox"));
            }
            else if (bodyAnimationType == BodyAnimationControllerType.Mixamo)
            {
                filteredActions = filteredActions.Where(action =>
                    action.Categories != null &&
                    action.Categories.Contains("Mixamo"));
            }
            return filteredActions;
        }

        #region reactive actions (low level)
        // low level reactive actions
        public void triggerChangeWeight()
        {
            animator.SetTrigger("changeWeight");
        }

        #endregion
    }


}

