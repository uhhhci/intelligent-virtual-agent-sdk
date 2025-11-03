using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using System.Reflection;
using IVH.Core.ServiceConnector;

namespace IVH.Core.Actions{
    // Define the base class for animations
    public abstract class AgentAction
    {
        // Properties of the animation
        public string Name { get; protected set; }
        public string Description { get; protected set; }
        public List<string> Categories { get; protected set; }
        public Dictionary<string, (string type, string propertyDescription, string[] enumValues)> Parameters { get; protected set; }

        protected AgentAction(string name, string description, List<string> categories)
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

#if DE_UHH_HCI_IVH_ASSETS_MIXAMO
    // currently disabled due to restriction of redistribution, please contact the developer team if you want to use these actions in your academic researc
    #region mixamo animations 
    public class GreetWithQuickBow : AgentAction
    {
        public GreetWithQuickBow() : base(
            typeof(GreetWithQuickBow).Name,
            "An agent quickly bows to a person. This gesture is used in appropriate social and conversational contexts. " +
            "For example, people from East Asian cultures like Japan, China, and Korea often bow to greet others, say goodbye, or apologize.",
            new List<string> { "Mixamo", "Neutral", "Greet", "Respect", "Cultural Gesture", "Formal", "GenderNeutral" })
        { }
    }

    public class HelloWithRaiseHand : AgentAction
    {
        public HelloWithRaiseHand() : base(
            typeof(HelloWithRaiseHand).Name,
            "An agent greets others by raising and waving their right hand while lifting their head, only use when first meeting someone.",
            new List<string> { "Mixamo", "Neutral",  "Hand Gesture","GenderNeutral" })
        { }
    }

    public class HappyLittleDance : AgentAction
    {
        public HappyLittleDance() : base(
            typeof(HappyLittleDance).Name,
            "An agent expresses happiness by performing a small dance, swinging their body and hands.",
            new List<string> { "Mixamo", "Positive","GenderNeutral" })
        { }
    }

    public class ExcitedLittleDance : AgentAction
    {
        public ExcitedLittleDance() : base(
            typeof(ExcitedLittleDance).Name,
            "An agent shows excitement by jumping around and moving their hands.",
            new List<string> { "Mixamo", "Positive", "Energetic","GenderNeutral" })
        { }
    }

    public class ExpressRejection : AgentAction
    {
        public ExpressRejection() : base(
            typeof(ExpressRejection).Name,
            "An agent expresses feeling rejected by lowering their head and upper body.",
            new List<string> { "Mixamo", "Neutral", "Sad","GenderNeutral"})
        { }
    }

    public class ExpressThankfulness : AgentAction
    {
        public ExpressThankfulness() : base(
            typeof(ExpressThankfulness).Name,
            "An agent expresses gratitude by placing one hand on their chest and bowing slightly in a shy manner.",
            new List<string> { "Mixamo",  "Positive","GenderNeutral" })
        { }
    }

    public class Clapping : AgentAction
    {
        public Clapping() : base(
            typeof(Clapping).Name,
            "An agent claps their hands three times to show appreciation or celebration.",
            new List<string> { "Mixamo", "Positive","GenderNeutral" })
        { }
    }

    public class AskingAQuestion : AgentAction
    {
        public AskingAQuestion() : base(
            typeof(AskingAQuestion).Name,
            "An agent asks a question by using a hand gesture.",
            new List<string> { "Mixamo",  "Neutral","GenderNeutral" })
        { }
    }

    public class TalkingSomethingFunny : AgentAction
    {
        public TalkingSomethingFunny() : base(
            typeof(TalkingSomethingFunny).Name,
            "An agent introduces something humorous to lighten the conversation.",
            new List<string> { "Mixamo",  "Positive","GenderNeutral" })
        { }
    }

    public class TellingASecret : AgentAction
    {
        public TellingASecret() : base(
            typeof(TellingASecret).Name,
            "An agent gestures as if they are telling a secret in a confidential manner.",
            new List<string> { "Mixamo",  "Neutral","GenderNeutral" })
        { }
    }

    public class Yelling : AgentAction
    {
        public Yelling() : base(
             typeof(Yelling).Name,
            "An agent yelling, showing that they are angry in an argument.",
            new List<string> { "Mixamo","Negative","GenderNeutral" })
        { }
    }

    public class AnnoyedHeadShake : AgentAction
    {
        public AnnoyedHeadShake() : base(
             typeof(AnnoyedHeadShake).Name,
            "An agent shakes their head to express that they feel annoyed.",
            new List<string> { "Mixamo", "Negative","GenderNeutral" })
        { }
    }
    public class ShakingHeadNo : AgentAction
    {
        public ShakingHeadNo() : base(
             typeof(ShakingHeadNo).Name,
            "An agent shakes their head to express disagreement or disbelief.",
            new List<string> { "Mixamo",  "Negative","GenderNeutral" })
        { }
    }

    public class HeadNodYes : AgentAction
    {
        public HeadNodYes() : base(
             typeof(HeadNodYes).Name,
            "An agent nods their head as a simple yes when answering questions or agreeing.",
            new List<string> { "Mixamo", "Positive","GenderNeutral" })
        { }
    }

    public class SarcasticHeadNod : AgentAction
    {
        public SarcasticHeadNod() : base(
             typeof(SarcasticHeadNod).Name,
            "An agent nods their head when they agree with something sarcastically.",
            new List<string> { "Mixamo",  "Neutral","GenderNeutral" })
        { }
    }

    public class HardHeadNod : AgentAction
    {
        public HardHeadNod() : base(
             typeof(HardHeadNod).Name,
            "An agent does a hard head nod, expressing strong agreement with something.",
            new List<string> { "Mixamo", "Neutral","GenderNeutral" })
        { }
    }

    public class LengthyHeadNod : AgentAction
    {
        public LengthyHeadNod() : base(
             typeof(LengthyHeadNod).Name,
            "An agent nods their head in a lengthier way compared to a simple yes nod.",
            new List<string> { "Mixamo", "Neutral","GenderNeutral" })
        { }
    }

    public class ThoughtfulHeadNod : AgentAction
    {
        public ThoughtfulHeadNod() : base(
             typeof(ThoughtfulHeadNod).Name,
            "An agent nods their head thoughtfully, indicating deep thinking.",
            new List<string> { "Mixamo", "Neutral","GenderNeutral" })
        { }
    }


    
    public class LookingBehind : AgentAction
    {
        public LookingBehind() : base(
            typeof(LookingBehind).Name,
            "An agent turn its head and look behind ",
            new List<string> { "Mixamo", "Neutral","GenderNeutral" })
        { }
    }


    public class AgreeingWithHands : AgentAction
    {
        public AgreeingWithHands() : base(
            typeof(AgreeingWithHands).Name,
            "An agent move two hands to show aggrement, although seems like in a situation when the agent runs out of argument and has to agree on something.",
            new List<string> { "Mixamo", "Positive","GenderNeutral" })
        { }
    }

    public class Angry : AgentAction
    {
        public Angry() : base(
            typeof(Angry).Name,
            "An agent expresses anger by crosing their arms",
            new List<string> { "Mixamo", "Negative","GenderNeutral" })
        { }
    }

    public class AngryPointing : AgentAction
    {
        public AngryPointing() : base(
            typeof(AngryPointing).Name,
            "An agent points aggressively while angry. Use in arguments or confrontational situations.",
            new List<string> { "Mixamo", "Negative","GenderNeutral" })
        { }
    }


    public class Bashful : AgentAction
    {
        public Bashful() : base(
            typeof(Bashful).Name,
            "An agent displays shyness or embarrassment by looking slightly down.",
            new List<string> { "Mixamo", "Neutral","GenderNeutral"})
        { }
    }

    public class CrazyGesture : AgentAction
    {
        public CrazyGesture() : base(
            typeof(CrazyGesture).Name,
            "An agent wave hands around its' head, a gesture expressing something like: are you crazy?",
            new List<string> { "Mixamo", "Negative","GenderNeutral"})
        { }
    }


    public class Defeat : AgentAction
    {
        public Defeat() : base(
            typeof(Defeat).Name,
            "An agent reacts to losing or failing in a sad way by covering their face with hands",
            new List<string> { "Mixamo", "Negative","GenderNeutral"})
        { }
    }

    public class Defeated : AgentAction
    {
        public Defeated() : base(
            typeof(Defeated).Name,
            "An agent shows defeat in a frustrated ways by steping their feet and head looking down",
            new List<string> { "Mixamo", "Negative","GenderNeutral"})
        { }
    }

    public class Disappointed : AgentAction
    {
        public Disappointed() : base(
            typeof(Disappointed).Name,
            "An agent expresses disappointment by waving their hand and looking down.",
            new List<string> { "Mixamo", "Negative","GenderNeutral" })
        { }
    }


    public class SurpriseReacting : AgentAction
    {
        public SurpriseReacting() : base(
            typeof(SurpriseReacting).Name,
            "An agent expresses surprise by sudden reaction with a little jump.",
            new List<string> { "Mixamo", "Neutral","GenderNeutral" })
        { }
    }


    public class FistPump : AgentAction
    {
        public FistPump() : base(
            typeof(FistPump).Name,
            "An agent raises a fist in excitement or celebration. Use to express success or enthusiasm.",
            new List<string> { "Mixamo", "Positive","GenderNeutral"})
        { }
    }

    public class HandOneRaisingHigh : AgentAction
    {
        public HandOneRaisingHigh() : base(
            typeof(HandOneRaisingHigh).Name,
            "An agent raises one hand high above its hand. Use to signal attention, volunteering, or asking a question.",
            new List<string> { "Mixamo", "Neutral","GenderNeutral"})
        { }
    }


    public class Laughing : AgentAction
    {
        public Laughing() : base(
            typeof(Laughing).Name,
            "An agent laughs joyfully. Use in humorous or lighthearted situations.",
            new List<string> { "Mixamo", "Positive","GenderNeutral" })
        { }
    }

    public class Arguing : AgentAction
    {
        public Arguing() : base(
             typeof(Arguing).Name,
            "Agent showing arguing gesture in a normal conversation",
            new List<string> { "Mixamo", "Neutral","GenderNeutral" })
        { }
    }


    public class StandingArguing : AgentAction
    {
        public StandingArguing() : base(
             typeof(StandingArguing).Name,
            "Agent arguing extensively in an intense conversation",
            new List<string> { "Mixamo", "Neutral","GenderNeutral" })
        { }
    }

    
    public class Whatever : AgentAction
    {
        public Whatever() : base(
             typeof(Whatever).Name,
            "Agent step back and put hands up, showing an whatever attitude",
            new List<string> { "Mixamo", "NegativeF","GenderNeutral" })
        { }
    }


    public class BeingCocky : AgentAction
    {
        public BeingCocky() : base(
            typeof(BeingCocky).Name,
            "An agent acts with arrogance or overconfidence. Use to portray an egoistic attitude.",
            new List<string> { "Mixamo", "Negative","GenderNeutral" })
        { }
    }

    public class AngryGesture : AgentAction
    {
        public AngryGesture() : base(
            typeof(AngryGesture).Name,
            "An agent makes an exaggerated angry gesture. Use to reinforce anger or frustration.",
            new List<string> { "Mixamo", "Negative","GenderNeutral" })
        { }
    }

    public class Dismissing : AgentAction
    {
        public Dismissing() : base(
            typeof(Dismissing).Name,
            "An agent dismisses something with a hand wave. Use when rejecting, ignoring, or brushing off something.",
            new List<string> { "Mixamo", "Negative","GenderNeutral" })
        { }
    }


    public class YellingOut : AgentAction
    {
        public YellingOut() : base(
             typeof(YellingOut).Name,
            "Yelling out gesture, with body aggressively moving forward",
            new List<string> { "Mixamo", "Negative","GenderNeutral" })
        { }
    }

    #endregion
#endif
    // avaliable by default
    #region Rocketbox Animations 
    public class fcheer01 : AgentAction
    {
        public fcheer01() : base(
            typeof(fcheer01).Name,
            "An agent performs a cheer gesture, raising both hands in celebration.",
            new List<string> { "Rocketbox", "Female", "Positive" })
        { }
    }

    public class flclaphands01 : AgentAction
    {
        public flclaphands01() : base(
            typeof(flclaphands01).Name,
            "An agent claps their hands together, showing appreciation or celebration.",
            new List<string> { "Rocketbox", "Female", "Positive" })
        { }
    }

    public class fdancingneutral : AgentAction
    {
        public fdancingneutral() : base(
            typeof(fdancingneutral).Name,
            "An agent performs a neutral dance, moving rhythmically without strong emotion.",
            new List<string> { "Rocketbox", "Female", "Neutral" })
        { }
    }

    public class fgesticlaugh : AgentAction
    {
        public fgesticlaugh() : base(
            typeof(fgesticlaugh).Name,
            "An agent laughs while gesturing, expressing joy and amusement.",
            new List<string> { "Rocketbox", "Female", "Positive" })
        { }
    }

    public class fgesticlistenaccept01 : AgentAction
    {
        public fgesticlistenaccept01() : base(
            typeof(fgesticlistenaccept01).Name,
            "An agent listens and accepts with a nod, showing attentiveness and agreement.",
            new List<string> { "Rocketbox", "Female", "Neutral" })
        { }
    }

    public class fgesticlistenangry01 : AgentAction
    {
        public fgesticlistenangry01() : base(
            typeof(fgesticlistenangry01).Name,
            "An agent listens with an irritated expression, indicating frustration or disagreement.",
            new List<string> { "Rocketbox", "Female", "Negative" })
        { }
    }

    public class fgesticlistendeny01 : AgentAction
    {
        public fgesticlistendeny01() : base(
            typeof(fgesticlistendeny01).Name,
            "An agent listens while shaking their head, indicating denial or disagreement.",
            new List<string> { "Rocketbox", "Female", "Negative" })
        { }
    }

    // public class fgesticlistenexcited01 : AgentAction
    // {
    //     public fgesticlistenexcited01() : base(
    //         typeof(fgesticlistenexcited01).Name,
    //         "An agent listens with excitement, showing enthusiasm and engagement.",
    //         new List<string> { "Rocketbox", "Female", "Positive" })
    //     { }
    // }

    public class fgesticlistennervous01 : AgentAction
    {
        public fgesticlistennervous01() : base(
            typeof(fgesticlistennervous01).Name,
            "An agent listens with a nervous expression, indicating anxiety or uncertainty.",
            new List<string> { "Rocketbox", "Female", "Negative" })
        { }
    }

    // public class fgesticlistenrelaxed01 : AgentAction
    // {
    //     public fgesticlistenrelaxed01() : base(
    //         typeof(fgesticlistenrelaxed01).Name,
    //         "An agent listens in a relaxed manner, showing calmness and ease.",
    //         new List<string> { "Rocketbox", "Female", "Neutral" })
    //     { }
    // }

    public class fgesticlistensad01 : AgentAction
    {
        public fgesticlistensad01() : base(
            typeof(fgesticlistensad01).Name,
            "An agent listens with a sad expression, indicating disappointment or sorrow.",
            new List<string> { "Rocketbox", "Female", "Negative" })
        { }
    }

    public class fgesticshrug01 : AgentAction
    {
        public fgesticshrug01() : base(
            typeof(fgesticshrug01).Name,
            "An agent shrugs their shoulders, indicating uncertainty or indifference.",
            new List<string> { "Rocketbox", "Female", "Neutral" })
        { }
    }

    public class fgestictalkangry01 : AgentAction
    {
        public fgestictalkangry01() : base(
            typeof(fgestictalkangry01).Name,
            "An agent talks while showing an angry expression, indicating frustration or confrontation.",
            new List<string> { "Rocketbox", "Female", "Negative" })
        { }
    }

    public class fgestictalkexcited01 : AgentAction
    {
        public fgestictalkexcited01() : base(
            typeof(fgestictalkexcited01).Name,
            "An agent talks with excitement, showing enthusiasm and energy.",
            new List<string> { "Rocketbox", "Female", "Positive" })
        { }
    }

    public class fgesticthoughtful01 : AgentAction
    {
        public fgesticthoughtful01() : base(
            typeof(fgesticthoughtful01).Name,
            "An agent gestures thoughtfully, indicating deep consideration or reflection.",
            new List<string> { "Rocketbox", "Female", "Neutral" })
        { }
    }

    public class fidleangry01 : AgentAction
    {
        public fidleangry01() : base(
            typeof(fidleangry01).Name,
            "An agent stands idle with an angry expression, indicating frustration or irritation.",
            new List<string> { "Rocketbox", "Female", "Negative" })
        { }
    }

    public class fidlecough01 : AgentAction
    {
        public fidlecough01() : base(
            typeof(fidlecough01).Name,
            "An agent coughs while standing idle, indicating a need to clear their throat or a sign of illness.",
            new List<string> { "Rocketbox", "Female", "Neutral" })
        { }
    }

    public class fidlelookaround01 : AgentAction
    {
        public fidlelookaround01() : base(
            typeof(fidlelookaround01).Name,
            "An agent looks around while standing idle, indicating curiosity or awareness of surroundings.",
            new List<string> { "Rocketbox", "Female", "Neutral" })
        { }
    }

    public class fidlenervous01 : AgentAction
    {
        public fidlenervous01() : base(
            typeof(fidlenervous01).Name,
            "An agent stands idle with a nervous expression, indicating anxiety or uncertainty.",
            new List<string> { "Rocketbox", "Female", "Negative" })
        { }
    }

    public class fidlerollhead01 : AgentAction
    {
        public fidlerollhead01() : base(
            typeof(fidlerollhead01).Name,
            "An agent rolls their head while standing idle, indicating relaxation or boredom.",
            new List<string> { "Rocketbox", "Female", "Neutral" })
        { }
    }

    public class fidlescrathhead01 : AgentAction
    {
        public fidlescrathhead01() : base(
            typeof(fidlescrathhead01).Name,
            "An agent scratches their head while standing idle, indicating confusion or contemplation.",
            new List<string> { "Rocketbox", "Female", "Neutral" })
        { }
    }

    public class fidleyawn01 : AgentAction
    {
        public fidleyawn01() : base(
            typeof(fidleyawn01).Name,
            "An agent yawns while standing idle, indicating tiredness or boredom.",
            new List<string> { "Rocketbox", "Female", "Neutral" })
        { }
    }

    public class fidle01 : AgentAction
    {
        public fidle01() : base(
            typeof(fidle01).Name,
            "An agent stands idle in a neutral position, indicating calmness or waiting.",
            new List<string> { "Rocketbox", "Female", "Neutral" })
        { }
    }

    public class mcheer01 : AgentAction
    {
        public mcheer01() : base(
            typeof(mcheer01).Name,
            "An agent performs a cheer gesture, raising both hands in celebration.",
            new List<string> { "Rocketbox", "Male", "Positive" })
        { }
    }

    public class mlclaphands01 : AgentAction
    {
        public mlclaphands01() : base(
            typeof(mlclaphands01).Name,
            "An agent claps their hands together, showing appreciation or celebration.",
            new List<string> { "Rocketbox", "Male", "Positive" })
        { }
    }

    public class mdancingneutral : AgentAction
    {
        public mdancingneutral() : base(
            typeof(mdancingneutral).Name,
            "An agent performs a neutral dance, moving rhythmically without strong emotion.",
            new List<string> { "Rocketbox", "Male", "Neutral" })
        { }
    }

    public class mgesticlistenaccept01 : AgentAction
    {
        public mgesticlistenaccept01() : base(
            typeof(mgesticlistenaccept01).Name,
            "An agent listens and accepts with a nod, showing attentiveness and agreement.",
            new List<string> { "Rocketbox", "Male", "Neutral" })
        { }
    }

    public class mgesticlistenangry01 : AgentAction
    {
        public mgesticlistenangry01() : base(
            typeof(mgesticlistenangry01).Name,
            "An agent listens with an irritated expression, indicating frustration or disagreement.",
            new List<string> { "Rocketbox", "Male", "Negative" })
        { }
    }

    public class mgesticlistendeny01 : AgentAction
    {
        public mgesticlistendeny01() : base(
            typeof(mgesticlistendeny01).Name,
            "An agent listens while shaking their head, indicating denial or disagreement.",
            new List<string> { "Rocketbox", "Male", "Negative" })
        { }
    }

    // public class mgesticlistenexcited01 : AgentAction
    // {
    //     public mgesticlistenexcited01() : base(
    //         typeof(mgesticlistenexcited01).Name,
    //         "An agent listens with excitement, showing enthusiasm and engagement.",
    //         new List<string> { "Rocketbox", "Male", "Positive" })
    //     { }
    // }

    public class mgesticlistennervous01 : AgentAction
    {
        public mgesticlistennervous01() : base(
            typeof(mgesticlistennervous01).Name,
            "An agent listens with a nervous expression, indicating anxiety or uncertainty.",
            new List<string> { "Rocketbox", "Male", "Negative" })
        { }
    }

    // public class mgesticlistenrelaxed01 : AgentAction
    // {
    //     public mgesticlistenrelaxed01() : base(
    //         typeof(mgesticlistenrelaxed01).Name,
    //         "An agent listens in a relaxed manner, showing calmness and ease.",
    //         new List<string> { "Rocketbox", "Male", "Neutral" })
    //     { }
    // }

    public class mgesticlistensad01 : AgentAction
    {
        public mgesticlistensad01() : base(
            typeof(mgesticlistensad01).Name,
            "An agent listens with a sad expression, indicating disappointment or sorrow.",
            new List<string> { "Rocketbox", "Male", "Negative" })
        { }
    }

    public class mgesticshrug01 : AgentAction
    {
        public mgesticshrug01() : base(
            typeof(mgesticshrug01).Name,
            "An agent shrugs their shoulders, indicating uncertainty or indifference.",
            new List<string> { "Rocketbox", "Male", "Neutral" })
        { }
    }

    public class mgestictalkangry01 : AgentAction
    {
        public mgestictalkangry01() : base(
            typeof(mgestictalkangry01).Name,
            "An agent talks while showing an angry expression, indicating frustration or confrontation.",
            new List<string> { "Rocketbox", "Male", "Negative" })
        { }
    }

    public class mgestictalkexcited01 : AgentAction
    {
        public mgestictalkexcited01() : base(
            typeof(mgestictalkexcited01).Name,
            "An agent talks with excitement, showing enthusiasm and energy.",
            new List<string> { "Rocketbox", "Male", "Positive" })
        { }
    }

    public class midleangry01 : AgentAction
    {
        public midleangry01() : base(
            typeof(midleangry01).Name,
            "An agent stands idle with an angry expression, indicating frustration or irritation.",
            new List<string> { "Rocketbox", "Male", "Negative" })
        { }
    }

    public class midlecough01 : AgentAction
    {
        public midlecough01() : base(
            typeof(midlecough01).Name,
            "An agent coughs while standing idle, indicating a need to clear their throat or a sign of illness.",
            new List<string> { "Rocketbox", "Male", "Neutral" })
        { }
    }

    public class midlelookaround01 : AgentAction
    {
        public midlelookaround01() : base(
            typeof(midlelookaround01).Name,
            "An agent looks around while standing idle, indicating curiosity or awareness of surroundings.",
            new List<string> { "Rocketbox", "Male", "Neutral" })
        { }
    }

    public class midleshakearms01 : AgentAction
    {
        public midleshakearms01() : base(
            typeof(midleshakearms01).Name,
            "An agent stands idle shaking his arms.",
            new List<string> { "Rocketbox", "Male", "Positive" })
        { }
    }

    public class midlewaiting01 : AgentAction
    {
        public midlewaiting01() : base(
            typeof(midlewaiting01).Name,
            "An agent stands idle wating for something.",
            new List<string> { "Rocketbox", "Male", "Positive" })
        { }
    }

    public class midlenervous01 : AgentAction
    {
        public midlenervous01() : base(
            typeof(midlenervous01).Name,
            "An agent stands idle with a nervous expression, indicating anxiety or uncertainty.",
            new List<string> { "Rocketbox", "Male", "Negative" })
        { }
    }

    public class midlerollhead01 : AgentAction
    {
        public midlerollhead01() : base(
            typeof(midlerollhead01).Name,
            "An agent rolls their head while standing idle, indicating relaxation or boredom.",
            new List<string> { "Rocketbox", "Male", "Neutral" })
        { }
    }

    public class midlescrathhead01 : AgentAction
    {
        public midlescrathhead01() : base(
            typeof(midlescrathhead01).Name,
            "An agent scratches their head while standing idle, indicating confusion or contemplation.",
            new List<string> { "Rocketbox", "Male", "Neutral" })
        { }
    }

    public class midleyawn01 : AgentAction
    {
        public midleyawn01() : base(
            typeof(midleyawn01).Name,
            "An agent yawns while standing idle, indicating tiredness or boredom.",
            new List<string> { "Rocketbox", "Male", "Neutral" })
        { }
    }

    public class midleneutral01 : AgentAction
    {
        public midleneutral01() : base(
            typeof(midleneutral01).Name,
            "An agent stands idle in a neutral position, indicating calmness or waiting.",
            new List<string> { "Rocketbox", "Male", "Neutral" })
        { }
    }
    #endregion

    public static class AgentActions
    {
        public static List<AgentAction> GetAvailableActions()
        {
            // Use Reflection to get all AgentAction subclasses
            var actionTypes = Assembly.GetExecutingAssembly() // Get current assembly
                .GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(AgentAction))); // Find subclasses of AgentAction

            // Instantiate each subclass dynamically
            var actions = new List<AgentAction>();
            foreach (var type in actionTypes)
            {
                if (Activator.CreateInstance(type) is AgentAction action)
                {
                    actions.Add(action);
                }
            }

            return actions;
        }
    }
}