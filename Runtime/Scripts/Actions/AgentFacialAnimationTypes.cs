using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using System.Reflection;
using IVH.Core.ServiceConnector;

namespace IVH.Core.Actions
{

    public abstract class AgentFacialAnimation
    {
        // Properties of the animation
        public string Name { get; protected set; }
        public string Description { get; protected set; }
        public List<string> Categories { get; protected set; }
        public Dictionary<string, (string type, string propertyDescription, string[] enumValues)> Parameters { get; protected set; }

        protected AgentFacialAnimation(string name, string description, List<string> categories)
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

    public abstract class AgentCognitiveState
    {
        // Properties of the animation
        public string Name { get; protected set; }
        public string Description { get; protected set; }
        public List<string> Categories { get; protected set; }
        public Dictionary<string, (string type, string propertyDescription, string[] enumValues)> Parameters { get; protected set; }

        protected AgentCognitiveState(string name, string description, List<string> categories)
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
#if DE_UHH_HCI_IVH_CCINTERNAL
    // The following animations are only supported in CC4 characters with the Digital Soul 100+ library purchase
    #region positive

    public class AmusedSmiling : AgentFacialAnimation
    {
        public AmusedSmiling() : base(
                typeof(AmusedSmiling).Name,
            "An IVA expressing amusement with a cheerful smile, conveying lightheartedness and joy.",
            new List<string> { "CC4", "Positive" })
        { }
    }

    public class Bewildered : AgentFacialAnimation
    {
        public Bewildered() : base(
                typeof(Bewildered).Name,
            "An IVA expressing bewilderment by raising both eyebrows and tilting the head slightly to the side.",
            new List<string> { "CC4",  "Neutral" })
        { }
    }

    public class ConfidentAndSmile : AgentFacialAnimation
    {
        public ConfidentAndSmile() : base(
                typeof(ConfidentAndSmile).Name,
            "An IVA expressing confidence with a warm smile and an upright posture, exuding self-assurance.",
            new List<string> { "CC4",  "Positive" })
        { }
    }

    public class ConfusedUnhappy : AgentFacialAnimation
    {
        public ConfusedUnhappy() : base(
                typeof(ConfusedUnhappy).Name,
            "An IVA expressing confusion with a furrowed brow and a slight tilt of the head, showing uncertainty.",
            new List<string> { "CC4" })
        { }
    }

/*    public class Considering : AgentFacialAnimation
    {
        public Considering() : base(
                typeof(Considering).Name,
            "An IVA thoughtfully tapping their chin and gazing upward, conveying contemplation.",
            new List<string> { "CC4",  "Neutral" })
        { }
    }
*/
    public class Daydream : AgentFacialAnimation
    {
        public Daydream() : base(
                typeof(Daydream).Name,
            "An IVA staring off into the distance with a serene expression, as if lost in thought.",
            new List<string> { "CC4",  "Neutral" })
        { }
    }

    public class Disagree : AgentFacialAnimation
    {
        public Disagree() : base(
                typeof(Disagree).Name,
            "An IVA shaking their head slightly, showing disagreement or dissent.",
            new List<string> { "CC4",  "Negative" })
        { }
    }

    public class Disbelief : AgentFacialAnimation
    {
        public Disbelief() : base(
                typeof(Disbelief).Name,
            "An IVA raising an eyebrow and leaning slightly backward, conveying disbelief or surprise.",
            new List<string> { "CC4",  "Surprise" })
        { }
    }

    public class Disgust : AgentFacialAnimation
    {
        public Disgust() : base(
                typeof(Disgust).Name,
            "An IVA wrinkling their nose and curling their lip, expressing distaste.",
            new List<string> { "CC4",  "Negative" })
        { }
    }

    public class Dislike : AgentFacialAnimation
    {
        public Dislike() : base(
                typeof(Dislike).Name,
            "An IVA shaking their head and frowning slightly, showing dislike or disapproval.",
            new List<string> { "CC4",  "Negative" })
        { }
    }

/*    public class Displeased : AgentFacialAnimation
    {
        public Displeased() : base(
                typeof(Displeased).Name,
            "An IVA pressing their lips together and crossing their arms, conveying displeasure.",
            new List<string> { "CC4",  "Negative" })
        { }
    }
*/
    public class EnergeticSmile : AgentFacialAnimation
    {
        public EnergeticSmile() : base(
                typeof(EnergeticSmile).Name,
            "An IVA smiling broadly and showing lively body language, conveying high energy and positivity.",
            new List<string> { "CC4",  "Positive" })
        { }
    }

    public class Explore : AgentFacialAnimation
    {
        public Explore() : base(
                typeof(Explore).Name,
            "An IVA looking around curiously, appearing eager to explore or investigate.",
            new List<string> { "CC4",  "Neutral" })
        { }
    }

    public class Frightened : AgentFacialAnimation
    {
        public Frightened() : base(
                typeof(Frightened).Name,
            "An IVA widening their eyes and stepping back slightly, conveying fear or alarm.",
            new List<string> { "CC4",  "Negative" })
        { }
    }

    public class GladSmiling : AgentFacialAnimation
    {
        public GladSmiling() : base(
                typeof(GladSmiling).Name,
            "An IVA smiling warmly, conveying happiness and contentment.",
            new List<string> { "CC4",  "Positive" })
        { }
    }

    public class GrievedLowerHead : AgentFacialAnimation
    {
        public GrievedLowerHead() : base(
                typeof(GrievedLowerHead).Name,
            "An IVA lowering their head and showing a sorrowful expression, conveying grief or sadness.",
            new List<string> { "CC4",  "Negative" })
        { }
    }

    public class HeadTiltForward : AgentFacialAnimation
    {
        public HeadTiltForward() : base(
                typeof(HeadTiltForward).Name,
            "An IVA tilting their head forward slightly, as if leaning in to listen or pay attention.",
            new List<string> { "CC4",  "Neutral" })
        { }
    }

    public class HelplesslySighed : AgentFacialAnimation
    {
        public HelplesslySighed() : base(
                typeof(HelplesslySighed).Name,
            "An IVA sighing with a slight shrug, conveying helplessness or resignation.",
            new List<string> { "CC4",  "Negative" })
        { }
    }

    public class Incredulous : AgentFacialAnimation
    {
        public Incredulous() : base(
                typeof(Incredulous).Name,
            "An IVA widening their eyes and raising their brows, conveying incredulity or disbelief.",
            new List<string> { "CC4",  "Surprise" })
        { }
    }

    public class InterestedHeadTilt : AgentFacialAnimation
    {
        public InterestedHeadTilt() : base(
                typeof(InterestedHeadTilt).Name,
            "An IVA tilting their head slightly with a curious expression, showing interest or attentiveness.",
            new List<string> { "CC4",  "Neutral" })
        { }
    }

    // public class LookAround : AgentFacialAnimation
    // {
    //     public LookAround() : base(
    //             typeof(LookAround).Name,
    //         "An IVA glancing around their environment curiously or cautiously.",
    //         new List<string> { "CC4",  "Neutral" })
    //     { }
    // }

    // public class LookAroundPleased : AgentFacialAnimation
    // {
    //     public LookAroundPleased() : base(
    //             typeof(LookAroundPleased).Name,
    //         "An IVA looking around with a relaxed smile, showing satisfaction or pleasure.",
    //         new List<string> { "CC4",  "Positive" })
    //     { }
    // }

/*    public class LookAroundSatisfied : AgentFacialAnimation
    {
        public LookAroundSatisfied() : base(
                typeof(LookAroundSatisfied).Name,
            "An IVA looking around confidently with a contented expression, conveying satisfaction.",
            new List<string> { "CC4",  "Positive" })
        { }
    }
*/
    public class Satisfied : AgentFacialAnimation
    {
        public Satisfied() : base(
                typeof(Satisfied).Name,
            "An IVA smiling softly with a contented posture, expressing satisfaction.",
            new List<string> { "CC4",  "Positive" })
        { }
    }

    public class TouchingSmile : AgentFacialAnimation
    {
        public TouchingSmile() : base(
                typeof(Satisfied).Name,
            "An IVA smiling softly with a touched facial expression showing warmth, gratitude, and vulnerability.",
            new List<string> { "CC4", "Positive" })
        { }
    }

    // This animation seems inccorect, need reimport!

    public class ShowingCompassionExpression : AgentFacialAnimation
    {
        public ShowingCompassionExpression() : base(
                typeof(ShowingCompassionExpression).Name,
            "An IVA expressing compassion with a soft gaze and gentle posture, showing empathy.",
            new List<string> { "CC4",  "Positive" })
        { }
    }

    public class ShowingDisbeliefPositiveWay : AgentFacialAnimation
    {
        public ShowingDisbeliefPositiveWay() : base(
                typeof(ShowingDisbeliefPositiveWay).Name,
            "An IVA expressing positive disbelief by raising their brows and smiling slightly, as if pleasantly surprised.",
            new List<string> { "CC4",  "Positive" })
        { }
    }

    public class SlightlyShyLowerHead : AgentFacialAnimation
    {
        public SlightlyShyLowerHead() : base(
                typeof(SlightlyShyLowerHead).Name,
            "An IVA lowering their head slightly and smiling shyly, conveying bashfulness.",
            new List<string> { "CC4",  "Positive" })
        { }
    }


/*    public class TalkingConfidently : AgentFacialAnimation
    {
        public TalkingConfidently() : base(
                typeof(TalkingConfidently).Name,
            "An IVA speaking with a firm and confident tone, showing self-assurance.",
            new List<string> { "CC4",  "Positive" })
        { }
    }
*/


    #endregion

    #region negative

    public class AngryStare : AgentFacialAnimation
    {
        public AngryStare() : base(
                typeof(AngryStare).Name,
            "An IVA expressing anger with a stern stare and furrowed brows, conveying frustration or displeasure.",
            new List<string> { "CC4",  "Negative" })
        { }
    }

    public class Annoyed : AgentFacialAnimation
    {
        public Annoyed() : base(
                typeof(Annoyed).Name,
            "An IVA expressing annoyance with a slight frown and crossed arms, showing irritation.",
            new List<string> { "CC4",  "Negative" })
        { }
    }


    // public class LookAroundNervous : AgentFacialAnimation
    // {
    //     public LookAroundNervous() : base(
    //             typeof(LookAroundNervous).Name,
    //         "An IVA glancing around quickly with a worried expression, showing nervousness or anxiety.",
    //         new List<string> { "CC4",  "Negative" })
    //     { }
    // }


    public class ScaredDisturbed : AgentFacialAnimation
    {
        public ScaredDisturbed() : base(
                typeof(ScaredDisturbed).Name,
            "An IVA backing away slightly with widened eyes, conveying fear or disturbance.",
            new List<string> { "CC4",  "Negative" })
        { }
    }


    public class Resentful : AgentFacialAnimation
    {
        public Resentful() : base(
                typeof(Resentful).Name,
            "An IVA frowning slightly with a tense posture, showing resentment or bitterness.",
            new List<string> { "CC4",  "Negative" })
        { }
    }


    public class Irritated : AgentFacialAnimation
    {
        public Irritated() : base(
                typeof(Irritated).Name,
            "An IVA furrowing their brow and tapping their foot, expressing irritation or impatience.",
            new List<string> { "CC4",  "Negative" })
        { }
    }

    #endregion

    #region neutral
/*    public class TalkingSeriously : AgentFacialAnimation
    {
        public TalkingSeriously() : base(
                typeof(TalkingSeriously).Name,
            "An IVA speaking with a serious expression and focused tone, conveying importance or gravity.",
            new List<string> { "CC4",  "Neutral" })
        { }
    }
*/
    // public class Surprised : AgentFacialAnimation
    // {
    //     public Surprised() : base(
    //             typeof(Surprised).Name,
    //         "An IVA widening their eyes and opening their mouth slightly, conveying surprise or shock.",
    //         new List<string> { "CC4",  "Surprise" })
    //     { }
    // }

    public class SmirkAndRaiseEyeBrow : AgentFacialAnimation
    {
        public SmirkAndRaiseEyeBrow() : base(
                typeof(SmirkAndRaiseEyeBrow).Name,
            "An IVA smirking and raising one eyebrow, conveying mischief or playful sarcasm.",
            new List<string> { "CC4",  "Neutral" })
        { }
    }

    // public class NormalTalking : AgentFacialAnimation
    // {
    //     public NormalTalking() : base(
    //             typeof(NormalTalking).Name,
    //         "An IVA speaking with a calm and natural tone, engaging in neutral conversation.",
    //         new List<string> { "CC4",  "Neutral" })
    //     { }
    // }
/*
    public class LookAroundRelax : AgentFacialAnimation
    {
        public LookAroundRelax() : base(
                typeof(LookAroundRelax).Name,
            "An IVA looking around casually, conveying a relaxed or neutral state.",
            new List<string> { "CC4",  "Neutral" })
        { }
    }*/

    public class Contempt : AgentFacialAnimation
    {
        public Contempt() : base(
                typeof(Contempt).Name,
            "An IVA expressing contempt with a curled lip and a dismissive gaze, conveying disdain.",
            new List<string> { "CC4",  "Negative" })
        { }
    }

    #endregion

    #region cognitive states
    // cognitive states which will remain 

/*
    public class ListeningAndThinking : AgentFacialAnimation
    {
        public ListeningAndThinking() : base(
                typeof(ListeningAndThinking).Name,
            "An IVA nodding slightly with a focused expression, showing they are listening and processing information.",
            new List<string> { "CC4",  "Neutral" })
        { }
    }

    public class ListeningHappily : AgentFacialAnimation
    {
        public ListeningHappily() : base(
                typeof(ListeningHappily).Name,
            "An IVA smiling gently and nodding, showing they are listening with happiness or agreement.",
            new List<string> { "CC4",  "Positive" })
        { }
    }
*/

    public class NodSurely : AgentFacialAnimation
    {
        public NodSurely() : base(
                typeof(NodSurely).Name,
            "An IVA nodding firmly, conveying certainty or agreement.",
            new List<string> { "CC4",  "Positive" })
        { }
    }

    #endregion
#endif
    public static class AgentFacialAnimations
    {
        public static List<AgentFacialAnimation> GetAvailableActions()
        {
            // Use Reflection to get all AgentAction subclasses
            var actionTypes = Assembly.GetExecutingAssembly() // Get current assembly
                .GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(AgentFacialAnimation))); // Find subclasses of AgentAction

            // Instantiate each subclass dynamically
            var actions = new List<AgentFacialAnimation>();
            foreach (var type in actionTypes)
            {
                if (Activator.CreateInstance(type) is AgentFacialAnimation action)
                {
                    actions.Add(action);
                }
            }

            return actions;
        }
    }

    public static class AgentCognitiveStates
    {
        public static List<AgentCognitiveState> GetAvailableActions()
        {
            // Use Reflection to get all AgentAction subclasses
            var actionTypes = Assembly.GetExecutingAssembly() // Get current assembly
                .GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(AgentCognitiveState))); // Find subclasses of AgentAction

            // Instantiate each subclass dynamically
            var actions = new List<AgentCognitiveState>();
            foreach (var type in actionTypes)
            {
                if (Activator.CreateInstance(type) is AgentCognitiveState action)
                {
                    actions.Add(action);
                }
            }

            return actions;
        }
    }
}
