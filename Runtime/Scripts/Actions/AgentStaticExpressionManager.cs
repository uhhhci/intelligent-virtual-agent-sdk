using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace IVH.Core.Actions
{
    // Note: in the CC4 RealIlusion Import namespace, there are already predefined dictionary of character morph avaliable 
    public enum FacialExpressionType
    {
        HAPPY,
        SAD,
        DISGUST,
        SURPRISE,
        ANGRY,
        NEUTRAL
    }
    // For CC4 agent
#if UNITY_EDITOR
    [CustomEditor(typeof(AgentStaticExpressionManager))]
    public class AgentStaticExpressionManagerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            // Draw the default inspector
            DrawDefaultInspector();

            // Get reference to the target script
            AgentStaticExpressionManager animator = (AgentStaticExpressionManager)target;

            // Add buttons for each facial expression
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Facial Expression Controls", EditorStyles.boldLabel);

            foreach (FacialExpressionType expressionType in System.Enum.GetValues(typeof(FacialExpressionType)))
            {
                if (GUILayout.Button($"Set {expressionType} Expression"))
                {
                    animator.SetFacialExpression(expressionType);
                }
            }

            EditorGUILayout.Space();
        }
    }
#endif
    public class AgentStaticExpressionManager : MonoBehaviour
    {

        public SkinnedMeshRenderer skinnedMeshRenderer;
        public float transitionDuration = 0.5f;

        private void Awake()
        {
            // Automatically find SkinnedMeshRenderer if not assigned
            if (skinnedMeshRenderer == null)
            {
                skinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
                if (skinnedMeshRenderer == null)
                {
                    Debug.LogError("SkinnedMeshRenderer not found! Please assign it manually.");
                }
            }
        }

        // Method to set the facial expression based on the given expression type
        public void SetFacialExpression(FacialExpressionType expressionType)
        {
            ApplyFaceExpression(FACE_NEUTRAL);
            Dictionary<string, float> expressionDict = GetExpressionDictionary(expressionType);
            ApplyFaceExpression(expressionDict);
        }

        // Helper method to get the corresponding dictionary for the facial expression
        private Dictionary<string, float> GetExpressionDictionary(FacialExpressionType expressionType)
        {
            switch (expressionType)
            {
                case FacialExpressionType.HAPPY:
                    return FACE_HAPPY;
                case FacialExpressionType.SAD:
                    return FACE_SAD;
                case FacialExpressionType.DISGUST:
                    return FACE_DISGUST;
                case FacialExpressionType.ANGRY:
                    return FACE_ANGRY;
                case FacialExpressionType.SURPRISE:
                    return FACE_SURPRISE;
                case FacialExpressionType.NEUTRAL:
                    return FACE_NEUTRAL;
                default:
                    return FACE_NEUTRAL; // Return an empty dictionary for unknown expressions
            }
        }
        // Function to apply face expression based on the dictionary
        void ApplyFaceExpression(Dictionary<string, float> faceExpression)
        {
            foreach (var entry in faceExpression)
            {
                string blendshapeName = entry.Key;
                float value = entry.Value;

                // Get the blendshape index by the blendshape name
                int blendShapeIndex = skinnedMeshRenderer.sharedMesh.GetBlendShapeIndex(blendshapeName);

                if (blendShapeIndex != -1)
                {
                    // Apply the value to the blendshape
                    skinnedMeshRenderer.SetBlendShapeWeight(blendShapeIndex, value);
                }
                else
                {
                    Debug.LogWarning("Blendshape " + blendshapeName + " not found.");
                }
            }
        }

        #region basic facial expression
        // Facial Expressions pre-defined blendshape morph, taken from CC4 RealIlusion.Import
        public static Dictionary<string, float> FACE_HAPPY = new Dictionary<string, float>
        {
            {"Brow_Raise_Inner_L", 0f },
            {"Brow_Raise_Inner_R", 0f },
            {"Brow_Raise_Outer_L", 0f },
            {"Brow_Raise_Outer_R", 0f },
            {"Brow_Drop_L", 0f },
            {"Brow_Drop_R", 0f },
            {"Brow_Raise_L", 70f },
            {"Brow_Raise_R", 70f },

            {"Eye_Wide_L", 40f },
            {"Eye_Wide_R", 40f },
            {"Eye_Squint_L", 30f },
            {"Eye_Squint_R", 30f },

            {"Nose_Scrunch", 0f },
            {"Nose_Nostrils_Flare", 40f },
            {"Cheek_Raise_L", 30f },
            {"Cheek_Raise_R", 30f },

            {"Mouth_Frown", 0f },
            {"Mouth_Blow", 0f },
            {"Mouth_Pucker", 0f },
            {"Mouth_Widen", 0f },
            {"Mouth_Widen_Sides", 0f },
            {"Mouth_Smile", 70f },
            {"Mouth_Smile_L", 40f },
            {"Mouth_Smile_R", 40f },
            {"Mouth_Dimple_L", 0f },
            {"Mouth_Dimple_R", 0f },
            {"Mouth_Plosive", 0f },
            {"Mouth_Lips_Open", 10f },
            {"Mouth_Lips_Part", 0f },
            {"Mouth_Bottom_Lip_Down", 70f },
            {"Mouth_Top_Lip_Up", 20f },
            {"Mouth_Bottom_Lip_Under", 30f },
            {"Mouth_Snarl_Upper_L", 20f },
            {"Mouth_Snarl_Upper_R", 20f },
            {"Mouth_Snarl_Lower_L", 0f },
            {"Mouth_Snarl_Lower_R", 0f },
            {"Mouth_Up", 30f },
            {"Mouth_Down", 0f },
            {"Mouth_Open", 0f },

            {"Turn_Jaw", 9f },
        };

        public static Dictionary<string, float> FACE_SAD = new Dictionary<string, float>
        {
            {"Brow_Raise_Inner_L", 100f },
            {"Brow_Raise_Inner_R", 100f },
            {"Brow_Raise_Outer_L", 0f },
            {"Brow_Raise_Outer_R", 0f },
            {"Brow_Drop_L", 40f },
            {"Brow_Drop_R", 40f },
            {"Brow_Raise_L", 0f },
            {"Brow_Raise_R", 0f },

            {"Eye_Wide_L", 40f },
            {"Eye_Wide_R", 40f },
            {"Eye_Squint_L", 20f },
            {"Eye_Squint_R", 20f },

            {"Nose_Scrunch", 0f },
            {"Nose_Nostrils_Flare", 0f },
            {"Cheek_Raise_L", 60f },
            {"Cheek_Raise_R", 60f },

            {"Mouth_Frown", 30f },
            {"Mouth_Blow", 20f },
            {"Mouth_Pucker", 0f },
            {"Mouth_Widen", 30f },
            {"Mouth_Widen_Sides", 0f },
            {"Mouth_Smile", 0f },
            {"Mouth_Smile_L", 0f },
            {"Mouth_Smile_R", 0f },
            {"Mouth_Dimple_L", 0f },
            {"Mouth_Dimple_R", 0f },
            {"Mouth_Plosive", 0f },
            {"Mouth_Lips_Open", 0f },
            {"Mouth_Lips_Part", 30f },
            {"Mouth_Bottom_Lip_Down", 0f },
            {"Mouth_Top_Lip_Up", 30f },
            {"Mouth_Bottom_Lip_Under", 0f },
            {"Mouth_Snarl_Upper_L", 0f },
            {"Mouth_Snarl_Upper_R", 0f },
            {"Mouth_Snarl_Lower_L", 0f },
            {"Mouth_Snarl_Lower_R", 0f },
            {"Mouth_Up", 0f },
            {"Mouth_Down", 60f },
            {"Mouth_Open", 0f },

            {"Turn_Jaw", 9f },
        };

        public static Dictionary<string, float> FACE_ANGRY = new Dictionary<string, float>
        {
            {"Brow_Raise_Inner_L", 0f },
            {"Brow_Raise_Inner_R", 0f },
            {"Brow_Raise_Outer_L", 50f },
            {"Brow_Raise_Outer_R", 50f },
            {"Brow_Drop_L", 0f },
            {"Brow_Drop_R", 0f },
            {"Brow_Raise_L", 0f },
            {"Brow_Raise_R", 0f },

            {"Eye_Wide_L", 80f },
            {"Eye_Wide_R", 80f },
            {"Eye_Squint_L", 60f },
            {"Eye_Squint_R", 60f },

            {"Nose_Scrunch", 80f },
            {"Nose_Nostrils_Flare", 0f },
            {"Cheek_Raise_L", 100f },
            {"Cheek_Raise_R", 100f },

            {"Mouth_Frown", 80f },
            {"Mouth_Blow", 0f },
            {"Mouth_Pucker", 30f },
            {"Mouth_Widen", 0f },
            {"Mouth_Widen_Sides", 0f },
            {"Mouth_Smile", 0f },
            {"Mouth_Smile_L", 0f },
            {"Mouth_Smile_R", 0f },
            {"Mouth_Dimple_L", 0f },
            {"Mouth_Dimple_R", 0f },
            {"Mouth_Plosive", 50f },
            {"Mouth_Lips_Open", 0f },
            {"Mouth_Lips_Part", 0f },
            {"Mouth_Bottom_Lip_Down", 60f },
            {"Mouth_Top_Lip_Up", 100f },
            {"Mouth_Bottom_Lip_Under", 0f },
            {"Mouth_Snarl_Upper_L", 0f },
            {"Mouth_Snarl_Upper_R", 0f },
            {"Mouth_Snarl_Lower_L", 0f },
            {"Mouth_Snarl_Lower_R", 0f },
            {"Mouth_Up", 50f },
            {"Mouth_Down", 0f },
            {"Mouth_Open", 0f },

            {"Turn_Jaw", 20f },
        };

        public static Dictionary<string, float> FACE_DISGUST = new Dictionary<string, float>
        {
            {"Brow_Raise_Inner_L", 0f },
            {"Brow_Raise_Inner_R", 0f },
            {"Brow_Raise_Outer_L", 60f },
            {"Brow_Raise_Outer_R", 60f },
            {"Brow_Drop_L", 70f },
            {"Brow_Drop_R", 70f },
            {"Brow_Raise_L", 0f },
            {"Brow_Raise_R", 0f },

            {"Eye_Wide_L", 0f },
            {"Eye_Wide_R", 0f },
            {"Eye_Squint_L", 20f },
            {"Eye_Squint_R", 20f },

            {"Nose_Scrunch", 100f },
            {"Nose_Nostrils_Flare", 0f },
            {"Cheek_Raise_L", 60f },
            {"Cheek_Raise_R", 60f },

            {"Mouth_Frown", 30f },
            {"Mouth_Blow", 0f },
            {"Mouth_Pucker", 0f },
            {"Mouth_Widen", 0f },
            {"Mouth_Widen_Sides", 0f },
            {"Mouth_Smile", 0f },
            {"Mouth_Smile_L", 0f },
            {"Mouth_Smile_R", 0f },
            {"Mouth_Dimple_L", 30f },
            {"Mouth_Dimple_R", 30f },
            {"Mouth_Plosive", 0f },
            {"Mouth_Lips_Open", 0f },
            {"Mouth_Lips_Part", 0f },
            {"Mouth_Bottom_Lip_Down", 0f },
            {"Mouth_Top_Lip_Up", 100f },
            {"Mouth_Bottom_Lip_Under", 0f },
            {"Mouth_Snarl_Upper_L", 0f },
            {"Mouth_Snarl_Upper_R", 0f },
            {"Mouth_Snarl_Lower_L", 20f },
            {"Mouth_Snarl_Lower_R", 20f },
            {"Mouth_Up", 0f },
            {"Mouth_Down", 40f },
            {"Mouth_Open", 0f },

            {"Turn_Jaw", 9f },
        };

        public static Dictionary<string, float> FACE_FEAR = new Dictionary<string, float>
        {
            {"Brow_Raise_Inner_L", 80f },
            {"Brow_Raise_Inner_R", 80f },
            {"Brow_Raise_Outer_L", 0f },
            {"Brow_Raise_Outer_R", 0f },
            {"Brow_Drop_L", 0f },
            {"Brow_Drop_R", 0f },
            {"Brow_Raise_L", 0f },
            {"Brow_Raise_R", 0f },

            {"Eye_Wide_L", 100f },
            {"Eye_Wide_R", 100f },
            {"Eye_Squint_L", 100f },
            {"Eye_Squint_R", 100f },

            {"Nose_Scrunch", 60f },
            {"Nose_Nostrils_Flare", 0f },
            {"Cheek_Raise_L", 100f },
            {"Cheek_Raise_R", 100f },

            {"Mouth_Frown", 70f },
            {"Mouth_Blow", 0f },
            {"Mouth_Pucker", 30f },
            {"Mouth_Widen", 40f },
            {"Mouth_Widen_Sides", 20f },
            {"Mouth_Smile", 0f },
            {"Mouth_Smile_L", 0f },
            {"Mouth_Smile_R", 0f },
            {"Mouth_Dimple_L", 0f },
            {"Mouth_Dimple_R", 0f },
            {"Mouth_Plosive", 0f },
            {"Mouth_Lips_Open", 0f },
            {"Mouth_Lips_Part", 0f },
            {"Mouth_Bottom_Lip_Down", 30f },
            {"Mouth_Top_Lip_Up", 100f },
            {"Mouth_Bottom_Lip_Under", 0f },
            {"Mouth_Snarl_Upper_L", 0f },
            {"Mouth_Snarl_Upper_R", 0f },
            {"Mouth_Snarl_Lower_L", 30f },
            {"Mouth_Snarl_Lower_R", 30f },
            {"Mouth_Up", 0f },
            {"Mouth_Down", 0f },
            {"Mouth_Open", 0f },

            {"Turn_Jaw", 20f },
        };

        public static Dictionary<string, float> FACE_SURPRISE = new Dictionary<string, float>
        {
            {"Brow_Raise_Inner_L", 70f },
            {"Brow_Raise_Inner_R", 70f },
            {"Brow_Raise_Outer_L", 0f },
            {"Brow_Raise_Outer_R", 0f },
            {"Brow_Drop_L", 0f },
            {"Brow_Drop_R", 0f },
            {"Brow_Raise_L", 80f },
            {"Brow_Raise_R", 80f },

            {"Eye_Wide_L", 80f },
            {"Eye_Wide_R", 80f },
            {"Eye_Squint_L", 0f },
            {"Eye_Squint_R", 0f },

            {"Nose_Scrunch", 0f },
            {"Nose_Nostrils_Flare", 30f },
            {"Cheek_Raise_L", 70f },
            {"Cheek_Raise_R", 70f },

            {"Mouth_Frown", 0f },
            {"Mouth_Blow", 0f },
            {"Mouth_Pucker", 0f },
            {"Mouth_Widen", 0f },
            {"Mouth_Widen_Sides", 0f },
            {"Mouth_Smile", 60f },
            {"Mouth_Smile_L", 0f },
            {"Mouth_Smile_R", 0f },
            {"Mouth_Dimple_L", 0f },
            {"Mouth_Dimple_R", 0f },
            {"Mouth_Plosive", 0f },
            {"Mouth_Lips_Open", 0f },
            {"Mouth_Lips_Part", 0f },
            {"Mouth_Bottom_Lip_Down", 0f },
            {"Mouth_Top_Lip_Up", 0f },
            {"Mouth_Bottom_Lip_Under", 0f },
            {"Mouth_Snarl_Upper_L", 0f },
            {"Mouth_Snarl_Upper_R", 0f },
            {"Mouth_Snarl_Lower_L", 0f },
            {"Mouth_Snarl_Lower_R", 0f },
            {"Mouth_Up", 90f },
            {"Mouth_Down", 0f },
            {"Mouth_Open", 80f },

            {"Turn_Jaw", 20f },
        };

        public static Dictionary<string, float> FACE_NEUTRAL = new Dictionary<string, float>
        {
            {"Brow_Raise_Inner_L", 0f },
            {"Brow_Raise_Inner_R", 0f },
            {"Brow_Raise_Outer_L", 0f },
            {"Brow_Raise_Outer_R", 0f },
            {"Brow_Drop_L", 0f },
            {"Brow_Drop_R", 0f },
            {"Brow_Raise_L", 0f },
            {"Brow_Raise_R", 0f },

            {"Eye_Wide_L", 0f },
            {"Eye_Wide_R", 0f },
            {"Eye_Squint_L", 0f },
            {"Eye_Squint_R", 0f },

            {"Nose_Scrunch", 0f },
            {"Nose_Nostrils_Flare", 0f },
            {"Cheek_Raise_L", 0f },
            {"Cheek_Raise_R", 0f },

            {"Mouth_Frown", 0f },
            {"Mouth_Blow", 0f },
            {"Mouth_Pucker", 0f },
            {"Mouth_Widen", 0f },
            {"Mouth_Widen_Sides", 0f },
            {"Mouth_Smile", 0f },
            {"Mouth_Smile_L", 0f },
            {"Mouth_Smile_R", 0f },
            {"Mouth_Dimple_L", 0f },
            {"Mouth_Dimple_R", 0f },
            {"Mouth_Plosive", 0f },
            {"Mouth_Lips_Open", 0f },
            {"Mouth_Lips_Part", 0f },
            {"Mouth_Bottom_Lip_Down", 0f },
            {"Mouth_Top_Lip_Up", 0f },
            {"Mouth_Bottom_Lip_Under", 0f },
            {"Mouth_Snarl_Upper_L", 0f },
            {"Mouth_Snarl_Upper_R", 0f },
            {"Mouth_Snarl_Lower_L", 0f },
            {"Mouth_Snarl_Lower_R", 0f },
            {"Mouth_Up", 0f },
            {"Mouth_Down", 0f },
            {"Mouth_Open", 0f },

            {"Turn_Jaw", 0f },
        };
        #endregion

        // GPT4-o generated additional facial expressions
        #region addition facial expression

        #endregion

    }
}