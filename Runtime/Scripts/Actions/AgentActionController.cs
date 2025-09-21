#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;

public class AnimationControllerSetup : EditorWindow
{
    // Fields to assign in the editor
    private AnimatorController animatorController;
    private List<AnimationClip> animationClips = new List<AnimationClip>();
    private string layerName = "FacialExpression";
    private string targetStateName = "StandingIdle";
    [MenuItem("Tools/Animation Controller Setup")]
    public static void ShowWindow()
    {
        GetWindow<AnimationControllerSetup>("Animation Controller Setup");
    }

    private void OnGUI()
    {
        GUILayout.Label("Animation Controller Setup", EditorStyles.boldLabel);

        // Assign the Animator Controller
        animatorController = (AnimatorController)EditorGUILayout.ObjectField("Animator Controller", animatorController, typeof(AnimatorController), false);

        // Layer name field
        layerName = EditorGUILayout.TextField("Layer Name", layerName);

        targetStateName = EditorGUILayout.TextField("Target State Name", targetStateName);

        // Animation clips list management
        GUILayout.Label("Animation Clips", EditorStyles.boldLabel);
        if (GUILayout.Button("Add Animation Clip"))
        {
            animationClips.Add(null);
        }

        for (int i = 0; i < animationClips.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();

            animationClips[i] = (AnimationClip)EditorGUILayout.ObjectField(animationClips[i], typeof(AnimationClip), false);

            if (GUILayout.Button("Remove", GUILayout.Width(60)))
            {
                animationClips.RemoveAt(i);
                i--;
            }

            EditorGUILayout.EndHorizontal();
        }

        GUILayout.Space(10);

        if (GUILayout.Button("Add Animations to Controller"))
        {
            AddAnimationsToController();
        }
    }

    private void AddAnimationsToController()
    {
        if (animatorController == null)
        {
            Debug.LogError("Animator Controller is not assigned.");
            return;
        }

        if (animationClips.Count == 0)
        {
            Debug.LogError("No animation clips provided.");
            return;
        }

        // Ensure the specified layer exists
        AnimatorControllerLayer layer = GetOrCreateLayer(layerName);

        // Check if Idle state exists, otherwise create it
        AnimatorState idleState = GetOrCreateIdleState(layer);

        foreach (var clip in animationClips)
        {
            if (clip == null)
            {
                Debug.LogWarning("One of the animation clips is null. Skipping.");
                continue;
            }

            string clipName = clip.name;
            string triggerName = GetTriggerName(clipName);

            // Rename the animation clip (assuming it is from an FBX with a random motion clip name)
            clip.name = triggerName;

            // Add the trigger parameter if it doesn't exist
            if (!HasParameter(animatorController, triggerName))
            {
                animatorController.AddParameter(triggerName, AnimatorControllerParameterType.Trigger);
            }

            // Create a state for the animation clip in the specified layer
            AnimatorState state = layer.stateMachine.AddState(triggerName);
            state.motion = clip;

            // Add transition from Idle to this state with the trigger
            AnimatorStateTransition idleToState = idleState.AddTransition(state);
            idleToState.AddCondition(AnimatorConditionMode.If, 0, triggerName);
            idleToState.hasExitTime = false;
            idleToState.duration = 0.15f;

            //Add transition back to Idle after the animation finishes
            AnimatorStateTransition stateToIdle = state.AddTransition(idleState);
            //stateToIdle.AddCondition(AnimatorConditionMode.If, 0, "ReturnToIdle");
            stateToIdle.hasExitTime = true;
            stateToIdle.exitTime = 1.0f; // Wait until the animation is done
            stateToIdle.duration = 0.15f;
        }

        Debug.Log("Animations added successfully to the Animator Controller.");
    }

    private string GetTriggerName(string originalName)
    {
        // Modify the name to create the trigger name
        return originalName.Replace("_", string.Empty).Replace("Motions", "");
    }

    private AnimatorControllerLayer GetOrCreateLayer(string layerName)
    {
        foreach (var layer in animatorController.layers)
        {
            if (layer.name == layerName)
            {
                return layer;
            }
        }

        // Create the layer if it doesn't exist
        AnimatorControllerLayer newLayer = new AnimatorControllerLayer
        {
            name = layerName,
            stateMachine = new AnimatorStateMachine(),
            defaultWeight = 1.0f
        };

        AssetDatabase.AddObjectToAsset(newLayer.stateMachine, AssetDatabase.GetAssetPath(animatorController));
        animatorController.AddLayer(newLayer);

        return newLayer;
    }

    private AnimatorState GetOrCreateIdleState(AnimatorControllerLayer layer)
    {
        // Check if "Idle" state exists
        foreach (var state in layer.stateMachine.states)
        {
            if (state.state.name == targetStateName)
            {
                return state.state;
            }
        }

        // Create the "Idle" state if it doesn't exist
        AnimatorState idleState = layer.stateMachine.AddState("Idle");
        idleState.motion = null; // No animation for idle state
        return idleState;
    }

    private bool HasParameter(AnimatorController controller, string parameterName)
    {
        foreach (var parameter in controller.parameters)
        {
            if (parameter.name == parameterName)
            {
                return true;
            }
        }
        return false;
    }
}
#endif