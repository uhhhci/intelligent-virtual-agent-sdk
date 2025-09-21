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
         
        private Animator animator;
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
                    animator.SetTrigger(action.GetType().Name);

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

    public List<GPTToolItem> GetDetailActionsFiltered(BodyActionFilter actionFilter, Gender gender = Gender.Nonbinary)
    {
        // Use LINQ for a more concise and readable implementation
        IEnumerable<AgentAction> filteredActions = avaliableActions.AsEnumerable();

        // First, filter by the BodyActionFilter
        if (actionFilter != BodyActionFilter.NONE)
        {
            string filterTag = actionFilter.ToString();
            filteredActions = filteredActions.Where(action =>
                action.Categories != null &&
                (action.Categories.Contains(filterTag) || action.Categories.Contains("Neutral")));
        }

        // Second, filter by Gender
        if (gender == Gender.Female)
        {
            filteredActions = filteredActions.Where(action =>
                action.Categories != null &&
                action.Categories.Contains("Female"));
        }
        else if (gender == Gender.Male)
        {
            filteredActions = filteredActions.Where(action =>
                action.Categories != null &&
                action.Categories.Contains("Male"));
        }
        // If gender is Nonbinary, no additional gender filter is needed, as all animations (Male, Female, and those without a gender tag) are valid.

        // Finally, select and transform the filtered actions into GPTToolItems
        return filteredActions.Select(action => action.ToGPTToolItem()).ToList();
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

        public List<string> GetSimpleActionNameFiltered(BodyActionFilter actionFilter, Gender gender = Gender.Nonbinary)
        {
            // Use LINQ for a more concise and readable implementation
            IEnumerable<AgentAction> filteredActions = avaliableActions.AsEnumerable();

            // First, filter by the BodyActionFilter
            if (actionFilter != BodyActionFilter.NONE)
            {
                string filterTag = actionFilter.ToString();
                filteredActions = filteredActions.Where(action =>
                    action.Categories != null &&
                    (action.Categories.Contains(filterTag) || action.Categories.Contains("Neutral")));
            }

            // Second, filter by Gender
            if (gender == Gender.Female)
            {
                filteredActions = filteredActions.Where(action =>
                    action.Categories != null &&
                    action.Categories.Contains("Female"));
            }
            else if (gender == Gender.Male)
            {
                filteredActions = filteredActions.Where(action =>
                    action.Categories != null &&
                    action.Categories.Contains("Male"));
            }
            // If gender is Nonbinary, no additional gender filter is needed, as all animations (Male, Female, and those without a gender tag) are valid.

            // Finally, select the names from the filtered list
            return filteredActions.Select(action => action.Name).ToList();
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

