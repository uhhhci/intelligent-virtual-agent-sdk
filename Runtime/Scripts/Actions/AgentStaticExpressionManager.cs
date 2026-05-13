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
        // Facial Expressions using Microsoft Rocketbox ARKit blendshapes (AK_XX prefix)
        // Blendshape names follow standard ARKit ordering: AK_01_BrowDownLeft ... AK_52_TongueOut
        public static Dictionary<string, float> FACE_HAPPY = new Dictionary<string, float>
        {
            // Brows
            {"blendShape1.AK_01_BrowDownLeft", 0f },
            {"blendShape1.AK_02_BrowDownRight", 0f },
            {"blendShape1.AK_03_BrowInnerUp", 0f },
            {"blendShape1.AK_04_BrowOuterUpLeft", 40f },
            {"blendShape1.AK_05_BrowOuterUpRight", 40f },
            // Cheeks
            {"blendShape1.AK_07_CheekSquintLeft", 50f },
            {"blendShape1.AK_08_CheekSquintRight", 50f },
            // Eyes
            {"blendShape1.AK_19_EyeSquintLeft", 40f },
            {"blendShape1.AK_20_EyeSquintRight", 40f },
            {"blendShape1.AK_21_EyeWideLeft", 0f },
            {"blendShape1.AK_22_EyeWideRight", 0f },
            // Jaw
            {"blendShape1.AK_25_JawOpen", 10f },
            // Mouth
            {"blendShape1.AK_28_MouthDimpleLeft", 20f },
            {"blendShape1.AK_29_MouthDimpleRight", 20f },
            {"blendShape1.AK_30_MouthFrownLeft", 0f },
            {"blendShape1.AK_31_MouthFrownRight", 0f },
            {"blendShape1.AK_34_MouthLowerDownLeft", 30f },
            {"blendShape1.AK_35_MouthLowerDownRight", 30f },
            {"blendShape1.AK_38_MouthPucker", 0f },
            {"blendShape1.AK_42_MouthShrugLower", 0f },
            {"blendShape1.AK_43_MouthShrugUpper", 0f },
            {"blendShape1.AK_44_MouthSmileLeft", 70f },
            {"blendShape1.AK_45_MouthSmileRight", 70f },
            {"blendShape1.AK_48_MouthUpperUpLeft", 20f },
            {"blendShape1.AK_49_MouthUpperUpRight", 20f },
            // Nose
            {"blendShape1.AK_50_NoseSneerLeft", 0f },
            {"blendShape1.AK_51_NoseSneerRight", 0f },
        };

        public static Dictionary<string, float> FACE_SAD = new Dictionary<string, float>
        {
            // Brows
            {"blendShape1.AK_01_BrowDownLeft", 20f },
            {"blendShape1.AK_02_BrowDownRight", 20f },
            {"blendShape1.AK_03_BrowInnerUp", 80f },
            {"blendShape1.AK_04_BrowOuterUpLeft", 0f },
            {"blendShape1.AK_05_BrowOuterUpRight", 0f },
            // Cheeks
            {"blendShape1.AK_07_CheekSquintLeft", 30f },
            {"blendShape1.AK_08_CheekSquintRight", 30f },
            // Eyes
            {"blendShape1.AK_19_EyeSquintLeft", 20f },
            {"blendShape1.AK_20_EyeSquintRight", 20f },
            {"blendShape1.AK_21_EyeWideLeft", 0f },
            {"blendShape1.AK_22_EyeWideRight", 0f },
            // Jaw
            {"blendShape1.AK_25_JawOpen", 0f },
            // Mouth
            {"blendShape1.AK_28_MouthDimpleLeft", 0f },
            {"blendShape1.AK_29_MouthDimpleRight", 0f },
            {"blendShape1.AK_30_MouthFrownLeft", 70f },
            {"blendShape1.AK_31_MouthFrownRight", 70f },
            {"blendShape1.AK_34_MouthLowerDownLeft", 0f },
            {"blendShape1.AK_35_MouthLowerDownRight", 0f },
            {"blendShape1.AK_38_MouthPucker", 20f },
            {"blendShape1.AK_42_MouthShrugLower", 30f },
            {"blendShape1.AK_43_MouthShrugUpper", 0f },
            {"blendShape1.AK_44_MouthSmileLeft", 0f },
            {"blendShape1.AK_45_MouthSmileRight", 0f },
            {"blendShape1.AK_48_MouthUpperUpLeft", 0f },
            {"blendShape1.AK_49_MouthUpperUpRight", 0f },
            // Nose
            {"blendShape1.AK_50_NoseSneerLeft", 0f },
            {"blendShape1.AK_51_NoseSneerRight", 0f },
        };

        public static Dictionary<string, float> FACE_ANGRY = new Dictionary<string, float>
        {
            // Brows
            {"blendShape1.AK_01_BrowDownLeft", 80f },
            {"blendShape1.AK_02_BrowDownRight", 80f },
            {"blendShape1.AK_03_BrowInnerUp", 0f },
            {"blendShape1.AK_04_BrowOuterUpLeft", 0f },
            {"blendShape1.AK_05_BrowOuterUpRight", 0f },
            // Cheeks
            {"blendShape1.AK_07_CheekSquintLeft", 50f },
            {"blendShape1.AK_08_CheekSquintRight", 50f },
            // Eyes
            {"blendShape1.AK_19_EyeSquintLeft", 60f },
            {"blendShape1.AK_20_EyeSquintRight", 60f },
            {"blendShape1.AK_21_EyeWideLeft", 0f },
            {"blendShape1.AK_22_EyeWideRight", 0f },
            // Jaw
            {"blendShape1.AK_25_JawOpen", 0f },
            // Mouth
            {"blendShape1.AK_28_MouthDimpleLeft", 0f },
            {"blendShape1.AK_29_MouthDimpleRight", 0f },
            {"blendShape1.AK_30_MouthFrownLeft", 60f },
            {"blendShape1.AK_31_MouthFrownRight", 60f },
            {"blendShape1.AK_34_MouthLowerDownLeft", 0f },
            {"blendShape1.AK_35_MouthLowerDownRight", 0f },
            {"blendShape1.AK_38_MouthPucker", 30f },
            {"blendShape1.AK_42_MouthShrugLower", 0f },
            {"blendShape1.AK_43_MouthShrugUpper", 0f },
            {"blendShape1.AK_44_MouthSmileLeft", 0f },
            {"blendShape1.AK_45_MouthSmileRight", 0f },
            {"blendShape1.AK_48_MouthUpperUpLeft", 50f },
            {"blendShape1.AK_49_MouthUpperUpRight", 50f },
            // Nose
            {"blendShape1.AK_50_NoseSneerLeft", 60f },
            {"blendShape1.AK_51_NoseSneerRight", 60f },
        };

        public static Dictionary<string, float> FACE_DISGUST = new Dictionary<string, float>
        {
            // Brows
            {"blendShape1.AK_01_BrowDownLeft", 50f },
            {"blendShape1.AK_02_BrowDownRight", 50f },
            {"blendShape1.AK_03_BrowInnerUp", 0f },
            {"blendShape1.AK_04_BrowOuterUpLeft", 30f },
            {"blendShape1.AK_05_BrowOuterUpRight", 30f },
            // Cheeks
            {"blendShape1.AK_07_CheekSquintLeft", 40f },
            {"blendShape1.AK_08_CheekSquintRight", 40f },
            // Eyes
            {"blendShape1.AK_19_EyeSquintLeft", 30f },
            {"blendShape1.AK_20_EyeSquintRight", 30f },
            {"blendShape1.AK_21_EyeWideLeft", 0f },
            {"blendShape1.AK_22_EyeWideRight", 0f },
            // Jaw
            {"blendShape1.AK_25_JawOpen", 0f },
            // Mouth
            {"blendShape1.AK_28_MouthDimpleLeft", 20f },
            {"blendShape1.AK_29_MouthDimpleRight", 20f },
            {"blendShape1.AK_30_MouthFrownLeft", 40f },
            {"blendShape1.AK_31_MouthFrownRight", 40f },
            {"blendShape1.AK_34_MouthLowerDownLeft", 0f },
            {"blendShape1.AK_35_MouthLowerDownRight", 0f },
            {"blendShape1.AK_38_MouthPucker", 0f },
            {"blendShape1.AK_42_MouthShrugLower", 0f },
            {"blendShape1.AK_43_MouthShrugUpper", 0f },
            {"blendShape1.AK_44_MouthSmileLeft", 0f },
            {"blendShape1.AK_45_MouthSmileRight", 0f },
            {"blendShape1.AK_48_MouthUpperUpLeft", 70f },
            {"blendShape1.AK_49_MouthUpperUpRight", 70f },
            // Nose
            {"blendShape1.AK_50_NoseSneerLeft", 100f },
            {"blendShape1.AK_51_NoseSneerRight", 100f },
        };

        public static Dictionary<string, float> FACE_FEAR = new Dictionary<string, float>
        {
            // Brows
            {"blendShape1.AK_01_BrowDownLeft", 0f },
            {"blendShape1.AK_02_BrowDownRight", 0f },
            {"blendShape1.AK_03_BrowInnerUp", 80f },
            {"blendShape1.AK_04_BrowOuterUpLeft", 0f },
            {"blendShape1.AK_05_BrowOuterUpRight", 0f },
            // Cheeks
            {"blendShape1.AK_07_CheekSquintLeft", 60f },
            {"blendShape1.AK_08_CheekSquintRight", 60f },
            // Eyes
            {"blendShape1.AK_19_EyeSquintLeft", 50f },
            {"blendShape1.AK_20_EyeSquintRight", 50f },
            {"blendShape1.AK_21_EyeWideLeft", 100f },
            {"blendShape1.AK_22_EyeWideRight", 100f },
            // Jaw
            {"blendShape1.AK_25_JawOpen", 20f },
            // Mouth
            {"blendShape1.AK_28_MouthDimpleLeft", 0f },
            {"blendShape1.AK_29_MouthDimpleRight", 0f },
            {"blendShape1.AK_30_MouthFrownLeft", 60f },
            {"blendShape1.AK_31_MouthFrownRight", 60f },
            {"blendShape1.AK_34_MouthLowerDownLeft", 30f },
            {"blendShape1.AK_35_MouthLowerDownRight", 30f },
            {"blendShape1.AK_38_MouthPucker", 30f },
            {"blendShape1.AK_42_MouthShrugLower", 0f },
            {"blendShape1.AK_43_MouthShrugUpper", 0f },
            {"blendShape1.AK_44_MouthSmileLeft", 0f },
            {"blendShape1.AK_45_MouthSmileRight", 0f },
            {"blendShape1.AK_48_MouthUpperUpLeft", 40f },
            {"blendShape1.AK_49_MouthUpperUpRight", 40f },
            // Nose
            {"blendShape1.AK_50_NoseSneerLeft", 30f },
            {"blendShape1.AK_51_NoseSneerRight", 30f },
        };

        public static Dictionary<string, float> FACE_SURPRISE = new Dictionary<string, float>
        {
            // Brows
            {"blendShape1.AK_01_BrowDownLeft", 0f },
            {"blendShape1.AK_02_BrowDownRight", 0f },
            {"blendShape1.AK_03_BrowInnerUp", 70f },
            {"blendShape1.AK_04_BrowOuterUpLeft", 80f },
            {"blendShape1.AK_05_BrowOuterUpRight", 80f },
            // Cheeks
            {"blendShape1.AK_07_CheekSquintLeft", 30f },
            {"blendShape1.AK_08_CheekSquintRight", 30f },
            // Eyes
            {"blendShape1.AK_19_EyeSquintLeft", 0f },
            {"blendShape1.AK_20_EyeSquintRight", 0f },
            {"blendShape1.AK_21_EyeWideLeft", 80f },
            {"blendShape1.AK_22_EyeWideRight", 80f },
            // Jaw
            {"blendShape1.AK_25_JawOpen", 70f },
            // Mouth
            {"blendShape1.AK_28_MouthDimpleLeft", 0f },
            {"blendShape1.AK_29_MouthDimpleRight", 0f },
            {"blendShape1.AK_30_MouthFrownLeft", 0f },
            {"blendShape1.AK_31_MouthFrownRight", 0f },
            {"blendShape1.AK_34_MouthLowerDownLeft", 0f },
            {"blendShape1.AK_35_MouthLowerDownRight", 0f },
            {"blendShape1.AK_38_MouthPucker", 0f },
            {"blendShape1.AK_42_MouthShrugLower", 0f },
            {"blendShape1.AK_43_MouthShrugUpper", 0f },
            {"blendShape1.AK_44_MouthSmileLeft", 30f },
            {"blendShape1.AK_45_MouthSmileRight", 30f },
            {"blendShape1.AK_48_MouthUpperUpLeft", 0f },
            {"blendShape1.AK_49_MouthUpperUpRight", 0f },
            // Nose
            {"blendShape1.AK_50_NoseSneerLeft", 0f },
            {"blendShape1.AK_51_NoseSneerRight", 0f },
        };

        public static Dictionary<string, float> FACE_NEUTRAL = new Dictionary<string, float>
        {
            // Brows
            {"blendShape1.AK_01_BrowDownLeft", 0f },
            {"blendShape1.AK_02_BrowDownRight", 0f },
            {"blendShape1.AK_03_BrowInnerUp", 0f },
            {"blendShape1.AK_04_BrowOuterUpLeft", 0f },
            {"blendShape1.AK_05_BrowOuterUpRight", 0f },
            // Cheeks
            {"blendShape1.AK_07_CheekSquintLeft", 0f },
            {"blendShape1.AK_08_CheekSquintRight", 0f },
            // Eyes
            {"blendShape1.AK_19_EyeSquintLeft", 0f },
            {"blendShape1.AK_20_EyeSquintRight", 0f },
            {"blendShape1.AK_21_EyeWideLeft", 0f },
            {"blendShape1.AK_22_EyeWideRight", 0f },
            // Jaw
            {"blendShape1.AK_25_JawOpen", 0f },
            // Mouth
            {"blendShape1.AK_28_MouthDimpleLeft", 0f },
            {"blendShape1.AK_29_MouthDimpleRight", 0f },
            {"blendShape1.AK_30_MouthFrownLeft", 0f },
            {"blendShape1.AK_31_MouthFrownRight", 0f },
            {"blendShape1.AK_34_MouthLowerDownLeft", 0f },
            {"blendShape1.AK_35_MouthLowerDownRight", 0f },
            {"blendShape1.AK_38_MouthPucker", 0f },
            {"blendShape1.AK_42_MouthShrugLower", 0f },
            {"blendShape1.AK_43_MouthShrugUpper", 0f },
            {"blendShape1.AK_44_MouthSmileLeft", 0f },
            {"blendShape1.AK_45_MouthSmileRight", 0f },
            {"blendShape1.AK_48_MouthUpperUpLeft", 0f },
            {"blendShape1.AK_49_MouthUpperUpRight", 0f },
            // Nose
            {"blendShape1.AK_50_NoseSneerLeft", 0f },
            {"blendShape1.AK_51_NoseSneerRight", 0f },
        };
        #endregion

        // GPT4-o generated additional facial expressions
        #region addition facial expression

        #endregion

    }
}