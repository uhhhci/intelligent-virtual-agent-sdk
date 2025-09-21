using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using System.Reflection;
using IVH.Core.ServiceConnector;

namespace IVH.Core.Actions
{

    public abstract class DidimoAgentFacialAnimation
    {
        // Properties of the animation
        public string Name { get; protected set; }
        public string Description { get; protected set; }
        public List<string> Categories { get; protected set; }
        public Dictionary<string, (string type, string propertyDescription, string[] enumValues)> Parameters { get; protected set; }

        protected DidimoAgentFacialAnimation(string name, string description, List<string> categories)
        {
            Name = name;
            Description = description;
            Categories = categories;
            Parameters = new Dictionary<string, (string type, string propertyDescription, string[] enumValues)>();
        }
        // Abstract method to trigger animation
        //public abstract void TriggerAnimation(Animator animator, bool isTriggered = false);

        public void TriggerSimpleAnimation(Animator animator)
        {
            Debug.Log(this.GetType().Name);
            animator.SetTrigger(this.GetType().FullName.ToString());
        }
        public void StoppAnimationTrigger(Animator animator)
        {
            animator.ResetTrigger(this.GetType().Name);
        }
        // Helper function to construct a GPTToolItem
        public GPTToolItem ToGPTToolItem()
        {
            string functionName = Name.Replace(" ", ""); // Ensure function name is code-friendly
            Dictionary<string, (string type, string propertyDescription, string[] enumValues)> parameters = Parameters;
            List<string> requiredFields = new List<string>(Parameters.Keys); // Assume all parameters are required for simplicity

            return GPTFunctionMessagePayload.CreateFunctionTool(
                functionName,
                Description,
                parameters,
                requiredFields
            );
        }
    }

    public abstract class DidimoAgentCognitiveState
    {
        // Properties of the animation
        public string Name { get; protected set; }
        public string Description { get; protected set; }
        public List<string> Categories { get; protected set; }
        public Dictionary<string, (string type, string propertyDescription, string[] enumValues)> Parameters { get; protected set; }

        protected DidimoAgentCognitiveState(string name, string description, List<string> categories)
        {
            Name = name;
            Description = description;
            Categories = categories;
            Parameters = new Dictionary<string, (string type, string propertyDescription, string[] enumValues)>();
        }
        // Abstract method to trigger animation
        //public abstract void TriggerAnimation(Animator animator, bool isTriggered = false);

        public void TriggerSimpleAnimation(Animator animator)
        {
            Debug.Log(this.GetType().Name);
            animator.SetTrigger(this.GetType().FullName.ToString());
        }
        public void StoppAnimationTrigger(Animator animator)
        {
            animator.ResetTrigger(this.GetType().Name);
        }
        // Helper function to construct a GPTToolItem
        public GPTToolItem ToGPTToolItem()
        {
            string functionName = Name.Replace(" ", ""); // Ensure function name is code-friendly
            Dictionary<string, (string type, string propertyDescription, string[] enumValues)> parameters = Parameters;
            List<string> requiredFields = new List<string>(Parameters.Keys); // Assume all parameters are required for simplicity

            return GPTFunctionMessagePayload.CreateFunctionTool(
                functionName,
                Description,
                parameters,
                requiredFields
            );
        }
    }
    // The following animations are only supported in CC4 characters with the Digital Soul 100+ library purchase
    #region basic emotions

    public class happy : DidimoAgentFacialAnimation
    {
        public happy() : base(
                typeof(happy).Name,
            "IVA is happy",
            new List<string> { "Emotion Expressions", "Basic Emotions" })
        { }
    }

    public class sad : DidimoAgentFacialAnimation
    {
        public sad() : base(
                typeof(sad).Name,
            "IVA is sad",
            new List<string> { "Emotion Expressions", "Basic Emotions" })
        { }
    }

    public class angry : DidimoAgentFacialAnimation
    {
        public angry() : base(
                typeof(angry).Name,
            "IVA is angry",
            new List<string> { "Emotion Expressions", "Basic Emotions" })
        { }
    }

    public class scared : DidimoAgentFacialAnimation
    {
        public scared() : base(
                typeof(scared).Name,
            "IVA is scared",
            new List<string> { "Emotion Expressions", "Basic Emotions" })
        { }
    }

    public class disgusted : DidimoAgentFacialAnimation
    {
        public disgusted() : base(
                typeof(disgusted).Name,
            "IVA is disgusted",
            new List<string> { "Emotion Expressions", "Basic Emotions" })
        { }
    }

    public class surprised : DidimoAgentFacialAnimation
    {
        public surprised() : base(
                typeof(surprised).Name,
            "IVA is surprised",
            new List<string> { "Emotion Expressions", "Basic Emotions" })
        { }
    }

    public class neutral : DidimoAgentFacialAnimation
    {
        public neutral() : base(
                typeof(neutral).Name,
            "IVA is neutral",
            new List<string> { "Emotion Expressions", "Basic Emotions" })
        { }
    }

    #endregion

    public static class DidimoAgentFacialAnimations
    {
        public static List<DidimoAgentFacialAnimation> GetAvailableActions()
        {
            // Use Reflection to get all DidimoAgentAction subclasses
            var actionTypes = Assembly.GetExecutingAssembly() // Get current assembly
                .GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(DidimoAgentFacialAnimation))); // Find subclasses of DidimoAgentAction

            // Instantiate each subclass dynamically
            var actions = new List<DidimoAgentFacialAnimation>();
            foreach (var type in actionTypes)
            {
                if (Activator.CreateInstance(type) is DidimoAgentFacialAnimation action)
                {
                    actions.Add(action);
                }
            }

            return actions;
        }
    }

    public static class DidimoAgentCognitiveStates
    {
        public static List<DidimoAgentCognitiveState> GetAvailableActions()
        {
            // Use Reflection to get all DidimoAgentAction subclasses
            var actionTypes = Assembly.GetExecutingAssembly() // Get current assembly
                .GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(DidimoAgentCognitiveState))); // Find subclasses of DidimoAgentAction

            // Instantiate each subclass dynamically
            var actions = new List<DidimoAgentCognitiveState>();
            foreach (var type in actionTypes)
            {
                if (Activator.CreateInstance(type) is DidimoAgentCognitiveState action)
                {
                    actions.Add(action);
                }
            }

            return actions;
        }
    }
}