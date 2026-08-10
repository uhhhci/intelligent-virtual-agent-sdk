using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEditor;
using IVH.Core.ServiceConnector;
using IVH.Core.IntelligentVirtualAgent;
using IVH.Core.Utils.Logging;

namespace IVH.Core.Actions
{
    public class AgentFacialExpressionAnimator : MonoBehaviour
    {
        public List<AgentFacialAnimation> avaliableAnimations = AgentFacialAnimations.GetAvailableActions();
        public List<DidimoAgentFacialAnimation> avaliableDidimoActions = DidimoAgentFacialAnimations.GetAvailableActions();

        [Tooltip("Minimum seconds between different facial expression triggers. 0 = no gate (v2.6.0 behavior).")]
        [Range(0f, 5f)] public float minHoldSeconds = 0.8f;

        private Animator animator;
        private string _lastActionName = "";
        private float _lastTriggerTime = -999f;
        // Initialize with the available actions


        private void Start()
        {
            // Get the Animator component attached to the avatar
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                Debug.LogError("Animator component is missing on the humanoid avatar.");
            }

        }

        // Populate the available actions
        public void PopulateActions()
        {

            // Add actions here. This is where you initialize your actions.
            avaliableAnimations = AgentFacialAnimations.GetAvailableActions();
            avaliableDidimoActions = DidimoAgentFacialAnimations.GetAvailableActions();
        }

        // Trigger an action by its name
        public void TriggerActionViaActionName(string actionName)
        {
            if (!string.IsNullOrEmpty(_lastActionName)
                && actionName != _lastActionName
                && Time.time - _lastTriggerTime < minHoldSeconds)
            {
                IVALogger.Debug("FacialExpressionAnimator",
                    $"Drop '{actionName}' — still holding '{_lastActionName}' ({minHoldSeconds - (Time.time - _lastTriggerTime):0.0}s left)");
                return;
            }

            foreach (AgentFacialAnimation action in avaliableAnimations)
            {

                if (action.GetType().Name == actionName)
                {
                    Debug.Log("triggering action with name:" + action.GetType().Name);
                    //action.TriggerSimpleAnimation(animator);
                    animator.SetTrigger(action.GetType().Name);
                    _lastActionName = actionName;
                    _lastTriggerTime = Time.time;

                    return;
                }
            }

            Debug.LogWarning($"Action with name '{actionName}' not found.");
        }

        public void TriggerDidimoActionViaActionName(string actionName)
        {
            foreach (DidimoAgentFacialAnimation action in avaliableDidimoActions)
            {
                if (action.GetType().Name == actionName)
                {
                    Debug.Log("triggering action with name:" + action.GetType().Name);
                    action.TriggerSimpleAnimation(animator);
                    return;
                }
            }
            Debug.LogWarning($"Action with name '{actionName}' not found.");
        }


        // Function to convert enabled actions into List<GPTToolItem>
        public List<GPTToolItem> GetEnabledActionsAsGPTToolItems()
        {
            List<GPTToolItem> tools = new List<GPTToolItem>();
            foreach (AgentFacialAnimation action in avaliableAnimations)
            {
                tools.Add(action.ToGPTToolItem());
            }
            return tools;
        }

        public List<GPTToolItem> GetDetailFacialExpressionFiltered(FacialExpressionFilter actionFilter, Gender gender = Gender.Nonbinary)
        {
            List<GPTToolItem> tools = new List<GPTToolItem>();
            if (actionFilter == FacialExpressionFilter.NONE)
            {
                // If the filter is NONE, return all action names
                foreach (AgentFacialAnimation action in avaliableAnimations)
                {
                    tools.Add(action.ToGPTToolItem());
                }
            }
            else
            {
                // Otherwise, filter by the tag corresponding to the enum
                string filterTag = actionFilter.ToString(); // Converts "Positive" or "Negative" to string

                foreach (AgentFacialAnimation action in avaliableAnimations)
                {
                    // Check if the action's tags contain the filterTag
                    if (action.Categories != null && (action.Categories.Contains(filterTag) || action.Categories.Contains("Neutral")))
                    {
                        tools.Add(action.ToGPTToolItem());
                    }
                }
            }
            return tools;
        }
        public List<GPTToolItem> GetEnabledDidimoActionsAsGPTToolItems()
        {
            List<GPTToolItem> tools = new List<GPTToolItem>();
            foreach (DidimoAgentFacialAnimation action in avaliableDidimoActions)
            {
                tools.Add(action.ToGPTToolItem());
            }
            return tools;
        }

        public List<String> GetSimpleActionName()
        {
            List<String> toolNames = new List<String>();
            foreach (AgentFacialAnimation action in avaliableAnimations)
            {
                toolNames.Add(action.Name);
            }
            return toolNames;
        }

        public List<String> GetSimpleFacialExpressionNameFiltered(FacialExpressionFilter actionFilter, Gender gender= Gender.Nonbinary)
        {
            List<String> toolNames = new List<String>();

            if (actionFilter == FacialExpressionFilter.NONE)
            {
                // If the filter is NONE, return all action names
                foreach (AgentFacialAnimation action in avaliableAnimations)
                {
                    toolNames.Add(action.Name);
                }
            }
            else
            {
                // Otherwise, filter by the tag corresponding to the enum
                string filterTag = actionFilter.ToString(); // Converts "Positive" or "Negative" to string

                foreach (AgentFacialAnimation action in avaliableAnimations)
                {
                    // Check if the action's tags contain the filterTag
                    if (action.Categories != null && (action.Categories.Contains(filterTag) || action.Categories.Contains("Neutral")))
                    {
                        toolNames.Add(action.Name);
                    }
                }
            }
            return toolNames;
        }
        public List<String> GetSimpleDidimoActionName()
        {
            List<String> toolNames = new List<String>();
            foreach (DidimoAgentFacialAnimation action in avaliableDidimoActions)
            {
                toolNames.Add(action.Name);
            }
            return toolNames;
        }
    }
}