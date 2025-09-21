using UnityEngine;
using System.Collections.Generic;

namespace IVH.Core.Actions
{
    [RequireComponent(typeof(Animator))] // Ensure Animator is present for IK
    public class EyeGazeController : MonoBehaviour
    {
        public SkinnedMeshRenderer characterSkinnedMeshRenderer;

        [Header("Eye Blendshape Names")]
        private List<string> eyeLLookLOptions = new List<string> { "Eye_L_Look_L", "blendShape1.AK_13_EyeLookInLeft" };
        private List<string> eyeRLookLOptions = new List<string> { "Eye_R_Look_L", "blendShape1.AK_14_EyeLookInRight" };
        private List<string> eyeLLookROptions = new List<string> { "Eye_L_Look_R", "blendShape1.AK_15_EyeLookOutLeft" };
        private List<string> eyeRLookROptions = new List<string> { "Eye_R_Look_R", "blendShape1.AK_16_EyeLookOutRight" };
        private List<string> eyeLLookUpOptions = new List<string> { "Eye_L_Look_Up", "blendShape1.AK_17_EyeLookUpLeft" };
        private List<string> eyeRLookUpOptions = new List<string> { "Eye_R_Look_Up", "blendShape1.AK_18_EyeLookUpRight" };
        private List<string> eyeLLookDownOptions = new List<string> { "Eye_L_Look_Down", "blendShape1.AK_11_EyeLookDownLeft" };
        private List<string> eyeRLookDownOptions = new List<string> { "Eye_R_Look_Down", "blendShape1.AK_12_EyeLookDownRight" };

        [Header("Gaze Targets")]
        public Transform playerTarget;
        // public Transform itemTarget;

        [Header("Gaze Settings")]
        [Range(0, 100)] public float maxEyeLookAngle = 45f; // Max angle for eye blendshape movement
        [Range(0, 100)] public float eyeBlendshapeStrength = 100f; // Max blendshape weight (0-100)
        public float eyeGazeSpeed = 2f; // Speed of eye movement (lerp factor)

        [Header("Head Gaze Settings (IK)")]
        [Range(0, 1)] public float headLookWeight = 0.5f; // How much the head follows the target (0-1)
        public float headGazeSpeed = 4.0f; // Speed of head movement
        public float maxHeadLookAngle = 60f; // Max angle the head can turn

        // Offset for the IK target from the actual gaze point (e.g., to look slightly above or below)
        public Vector3 headLookOffset = Vector3.zero;

        private Vector3 currentGazeDirection = Vector3.forward; // Current direction eyes are looking
        private Vector3 targetGazeDirection = Vector3.forward; // The desired direction to look towards

        private Animator animator;
        private Vector3 currentHeadLookAtPosition; // For smooth head IK target
        private Vector3 initialHeadLookAtPosition; // Store initial head position for idle

        public enum GazeMode
        {
            Idle,
            LookAtPlayer,
            //LookAtItem
        }

        public GazeMode currentGazeMode = GazeMode.Idle;

        // Store blendshape indices for performance
        private int eyeLLookLIndex, eyeRLookLIndex, eyeLLookRIndex, eyeRLookRIndex;
        private int eyeLLookUpIndex, eyeRLookUpIndex, eyeLLookDownIndex, eyeRLookDownIndex;

        void Awake()
        {
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                Debug.LogError("EyeGazeController: Animator component not found! Head IK requires an Animator.", this);
                enabled = false;
                return;
            }

            if (animator.avatar == null || !animator.avatar.isHuman)
            {
                Debug.LogError("EyeGazeController: Animator does not have a Humanoid Avatar assigned or is not a Humanoid rig. Head IK will not work.", this);
                enabled = false;
                return;
            }
        }

        void Start()
        {
            if (characterSkinnedMeshRenderer == null)
            {
                characterSkinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
                if (characterSkinnedMeshRenderer == null)
                {
                    Debug.LogError("EyeGazeController: SkinnedMeshRenderer not found! Please assign it in the Inspector or ensure it's a child.", this);
                    // We'll still allow script to run for head IK if blendshapes are missing
                }
            }

            if (characterSkinnedMeshRenderer != null)
            {
                eyeLLookLIndex = GetFirstValidBlendshapeIndex(characterSkinnedMeshRenderer, eyeLLookLOptions);
                eyeRLookLIndex = GetFirstValidBlendshapeIndex(characterSkinnedMeshRenderer, eyeRLookLOptions);
                eyeLLookRIndex = GetFirstValidBlendshapeIndex(characterSkinnedMeshRenderer, eyeLLookROptions);
                eyeRLookRIndex = GetFirstValidBlendshapeIndex(characterSkinnedMeshRenderer, eyeRLookROptions);
                eyeLLookUpIndex = GetFirstValidBlendshapeIndex(characterSkinnedMeshRenderer, eyeLLookUpOptions);
                eyeRLookUpIndex = GetFirstValidBlendshapeIndex(characterSkinnedMeshRenderer, eyeRLookUpOptions);
                eyeLLookDownIndex = GetFirstValidBlendshapeIndex(characterSkinnedMeshRenderer, eyeLLookDownOptions);
                eyeRLookDownIndex = GetFirstValidBlendshapeIndex(characterSkinnedMeshRenderer, eyeRLookDownOptions);

                CheckBlendshapeIndices();
            }
            // Initialize head look at position
            if (animator.GetBoneTransform(HumanBodyBones.Head) != null)
            {
                initialHeadLookAtPosition = animator.GetBoneTransform(HumanBodyBones.Head).position + animator.GetBoneTransform(HumanBodyBones.Head).forward * 10f; // Some point far in front
                currentHeadLookAtPosition = initialHeadLookAtPosition;
            }
            else
            {
                Debug.LogWarning("EyeGazeController: Head bone not found in Animator's avatar. Head IK will not function.", this);
                headLookWeight = 0; // Disable head IK if no head bone
            }

            Debug.Log("EyeGazeController Initialized. Current Gaze Mode: " + currentGazeMode, this);
        }

        // Update is for calculating the desired gaze direction
        void Update()
        {
            // Calculate the target direction based on the current gaze mode
            switch (currentGazeMode)
            {
                case GazeMode.Idle:
                    // Subtle random head movement for idle
                    float noiseX = Mathf.PerlinNoise(Time.time * 0.2f, 0) * 2f - 1f; // -1 to 1
                    float noiseY = Mathf.PerlinNoise(0, Time.time * 0.2f) * 2f - 1f; // -1 to 1
                    Vector3 idleLookDirLocal = new Vector3(noiseX, noiseY, 1).normalized; // Z is forward
                    targetGazeDirection = transform.TransformDirection(idleLookDirLocal).normalized; // Convert to world space
                    break;
                case GazeMode.LookAtPlayer:
                    if (playerTarget != null)
                    {
                        targetGazeDirection = (playerTarget.position - GetEyeCenterPosition()).normalized;
                    }
                    else
                    {
                        Debug.LogWarning("EyeGazeController: Player Target is null for LookAtPlayer mode. Falling back to Idle.", this);
                        currentGazeMode = GazeMode.Idle; // Fallback
                    }
                    break;
                    // case GazeMode.LookAtItem:
                    //     if (itemTarget != null)
                    //     {
                    //         targetGazeDirection = (itemTarget.position - GetEyeCenterPosition()).normalized;
                    //     }
                    //     else
                    //     {
                    //         Debug.LogWarning("EyeGazeController: Item Target is null for LookAtItem mode. Falling back to Idle.", this);
                    //         currentGazeMode = GazeMode.Idle; // Fallback
                    //     }
                    //     break;
            }

            // Smoothly interpolate the current gaze direction towards the target
            currentGazeDirection = Vector3.Lerp(currentGazeDirection, targetGazeDirection, Time.deltaTime * eyeGazeSpeed).normalized;

            // Calculate target head look position based on currentGazeDirection
            Vector3 headBonePosition = animator.GetBoneTransform(HumanBodyBones.Head) != null ? animator.GetBoneTransform(HumanBodyBones.Head).position : GetEyeCenterPosition();
            Vector3 targetHeadLookAtPosition = headBonePosition + currentGazeDirection * 100f + headLookOffset; // Project gaze far out

            // Smoothly interpolate head look-at position
            currentHeadLookAtPosition = Vector3.Lerp(currentHeadLookAtPosition, targetHeadLookAtPosition, Time.deltaTime * headGazeSpeed);
        }

        // LateUpdate to apply blendshapes AFTER animation has run
        void LateUpdate()
        {
            if (characterSkinnedMeshRenderer != null)
            {
                ApplyGazeToBlendshapes(currentGazeDirection);
            }
        }

        // OnAnimatorIK is called by the Animator component
        void OnAnimatorIK(int layerIndex)
        {
            if (animator == null || !animator.avatar.isHuman) return;

            // Set the weight for the head IK
            // This makes the head turn towards the target
            animator.SetLookAtWeight(headLookWeight, 0f, 1f, 0f, 0f); // weight, body, head, eyes, clamp

            // Set the position the head should look at
            animator.SetLookAtPosition(currentHeadLookAtPosition);
        }

        // Helper method to approximate eye center position
        // For more accuracy, consider getting the actual eye bones from the rig
        // or a specifically placed GameObject for the eye center.
        Vector3 GetEyeCenterPosition()
        {
            // If head bone is available, use its position as a base, otherwise fall back to character root + offset
            Transform headBone = animator.GetBoneTransform(HumanBodyBones.Head);
            if (headBone != null)
            {
                // A small offset from the head bone to approximate eye level
                return headBone.position + headBone.up * 0.1f;
            }
            return transform.position + transform.up * 1.6f; // Fallback
        }

        void ApplyGazeToBlendshapes(Vector3 gazeDirection)
        {
            // Ensure characterSkinnedMeshRenderer is valid before proceeding
            if (characterSkinnedMeshRenderer == null || characterSkinnedMeshRenderer.sharedMesh == null)
            {
                // Debug.LogWarning("Skipping ApplyGazeToBlendshapes: SkinnedMeshRenderer is null or invalid.", this);
                return;
            }

            // Convert world gaze direction to local space relative to the character's head/forward
            // It's better to use the Head's forward here if head IK is active, but character's forward is also okay
            // as the head will roughly align with the gaze direction.
            Vector3 localGazeDirection = transform.InverseTransformDirection(gazeDirection).normalized;

            // Calculate horizontal and vertical angles
            float horizontalAngle = Mathf.Atan2(localGazeDirection.x, localGazeDirection.z) * Mathf.Rad2Deg;
            float verticalAngle = Mathf.Asin(localGazeDirection.y) * Mathf.Rad2Deg;

            // Clamp angles to prevent extreme eye positions
            horizontalAngle = Mathf.Clamp(horizontalAngle, -maxEyeLookAngle, maxEyeLookAngle);
            verticalAngle = Mathf.Clamp(verticalAngle, -maxEyeLookAngle, maxEyeLookAngle);

            // Reset all relevant eye blendshapes to 0 before applying new values
            ResetAllEyeBlendshapes();

            // Apply blendshape weights based on calculated angles
            // Horizontal (Left/Right)
            if (horizontalAngle < -0.1f) // Looking Left
            {
                float weight = Mathf.Abs(horizontalAngle) / maxEyeLookAngle * eyeBlendshapeStrength;
                SetBlendshapeWeightIfValid(eyeLLookLIndex, weight);
                SetBlendshapeWeightIfValid(eyeRLookLIndex, weight);
            }
            else if (horizontalAngle > 0.1f) // Looking Right
            {
                float weight = horizontalAngle / maxEyeLookAngle * eyeBlendshapeStrength;
                SetBlendshapeWeightIfValid(eyeLLookRIndex, weight);
                SetBlendshapeWeightIfValid(eyeRLookRIndex, weight);
            }

            // Vertical (Up/Down)
            if (verticalAngle > 0.1f) // Looking Up
            {
                float weight = verticalAngle / maxEyeLookAngle * eyeBlendshapeStrength;
                SetBlendshapeWeightIfValid(eyeLLookUpIndex, weight);
                SetBlendshapeWeightIfValid(eyeRLookUpIndex, weight);
            }
            else if (verticalAngle < -0.1f) // Looking Down
            {
                float weight = Mathf.Abs(verticalAngle) / maxEyeLookAngle * eyeBlendshapeStrength;
                SetBlendshapeWeightIfValid(eyeLLookDownIndex, weight);
                SetBlendshapeWeightIfValid(eyeRLookDownIndex, weight);
            }
        }

        void ResetAllEyeBlendshapes()
        {
            if (characterSkinnedMeshRenderer == null || characterSkinnedMeshRenderer.sharedMesh == null) return;

            SetBlendshapeWeightIfValid(eyeLLookLIndex, 0);
            SetBlendshapeWeightIfValid(eyeRLookLIndex, 0);
            SetBlendshapeWeightIfValid(eyeLLookRIndex, 0);
            SetBlendshapeWeightIfValid(eyeRLookRIndex, 0);
            SetBlendshapeWeightIfValid(eyeLLookUpIndex, 0);
            SetBlendshapeWeightIfValid(eyeRLookUpIndex, 0);
            SetBlendshapeWeightIfValid(eyeRLookDownIndex, 0);
            SetBlendshapeWeightIfValid(eyeRLookDownIndex, 0);
        }

        void SetBlendshapeWeightIfValid(int index, float weight)
        {
            if (characterSkinnedMeshRenderer != null && characterSkinnedMeshRenderer.sharedMesh != null &&
                index != -1 && index < characterSkinnedMeshRenderer.sharedMesh.blendShapeCount)
            {
                characterSkinnedMeshRenderer.SetBlendShapeWeight(index, weight);
            }
        }

        void CheckBlendshapeIndices()
        {
            // Only log errors if SkinnedMeshRenderer is present, otherwise it's expected
            if (characterSkinnedMeshRenderer == null) return;

            bool allFound = true;
            if (eyeLLookLIndex == -1) { Debug.LogError($"EyeGazeController: Blendshape '{eyeLLookLOptions}' not found on mesh.", this); allFound = false; }
            if (eyeRLookLIndex == -1) { Debug.LogError($"EyeGazeController: Blendshape '{eyeRLookLOptions}' not found on mesh.", this); allFound = false; }
            if (eyeLLookRIndex == -1) { Debug.LogError($"EyeGazeController: Blendshape '{eyeLLookROptions }' not found on mesh.", this); allFound = false; }
            if (eyeRLookRIndex == -1) { Debug.LogError($"EyeGazeController: Blendshape '{eyeRLookROptions }' not found on mesh.", this); allFound = false; }
            if (eyeLLookUpIndex == -1) { Debug.LogError($"EyeGazeController: Blendshape '{eyeLLookUpOptions }' not found on mesh.", this); allFound = false; }
            if (eyeRLookUpIndex == -1) { Debug.LogError($"EyeGazeController: Blendshape '{eyeRLookUpOptions }' not found on mesh.", this); allFound = false; }
            if (eyeLLookDownIndex == -1) { Debug.LogError($"EyeGazeController: Blendshape '{eyeLLookDownOptions }' not found on mesh.", this); allFound = false; }
            if (eyeRLookDownIndex == -1) { Debug.LogError($"EyeGazeController: Blendshape '{eyeRLookDownOptions }' not found on mesh.", this); allFound = false; }

            if (!allFound)
            {
                Debug.LogError("EyeGazeController: One or more required eye blendshapes were not found. Eye gaze behavior functionality may be impaired.", this);
            }
        }

        // Public methods to change gaze mode (can be called from other scripts or UI)
        public void SetGazeModeIdle()
        {
            currentGazeMode = GazeMode.Idle;
            Debug.Log("EyeGazeController: Switched to Idle Gaze Mode.", this);
        }

        public void SetGazeModeLookAtPlayer()
        {
            currentGazeMode = GazeMode.LookAtPlayer;
            Debug.Log("EyeGazeController: Switched to Look At Player Gaze Mode.", this);
        }

        private int GetFirstValidBlendshapeIndex(SkinnedMeshRenderer renderer, List<string> possibleNames)
        {
            foreach (var name in possibleNames)
            {
                int index = renderer.sharedMesh.GetBlendShapeIndex(name);
                if (index != -1)
                {
                    return index;
                }
            }
            return -1; // Not found
        }

        // public void SetGazeModeLookAtItem()
        // {
        //     currentGazeMode = GazeMode.LookAtItem;
        //     Debug.Log("EyeGazeController: Switched to Look At Item Gaze Mode.", this);
        // }
    }
}