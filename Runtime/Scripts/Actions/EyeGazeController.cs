using UnityEngine;
using System.Collections.Generic;

namespace IVH.Core.Actions
{
    [RequireComponent(typeof(Animator))] // Ensure Animator is present for IK
    public class EyeGazeController : MonoBehaviour
    {
        public SkinnedMeshRenderer characterSkinnedMeshRenderer;

        [Tooltip("Other meshes carrying their own copy of the eye-look blendshapes (e.g. Avatar SDK eyelashes). Driven together with the main renderer.")]
        public List<SkinnedMeshRenderer> additionalRenderers = new List<SkinnedMeshRenderer>();

        // Candidate names per logical shape, tried in order: CC4, Rocketbox, ARKit (Avatar SDK & co.).
        // "L"/"R" in the logical names = the character's own left/right; ARKit names are anatomical too,
        // so the left eye looking to the character's left is eyeLookOutLeft.
        [Header("Eye Blendshape Names")]
        private List<string> eyeLLookLOptions = new List<string> { "Eye_L_Look_L", "blendShape1.AK_13_EyeLookInLeft", "eyeLookOutLeft" };
        private List<string> eyeRLookLOptions = new List<string> { "Eye_R_Look_L", "blendShape1.AK_14_EyeLookInRight", "eyeLookInRight" };
        private List<string> eyeLLookROptions = new List<string> { "Eye_L_Look_R", "blendShape1.AK_15_EyeLookOutLeft", "eyeLookInLeft" };
        private List<string> eyeRLookROptions = new List<string> { "Eye_R_Look_R", "blendShape1.AK_16_EyeLookOutRight", "eyeLookOutRight" };
        private List<string> eyeLLookUpOptions = new List<string> { "Eye_L_Look_Up", "blendShape1.AK_17_EyeLookUpLeft", "eyeLookUpLeft" };
        private List<string> eyeRLookUpOptions = new List<string> { "Eye_R_Look_Up", "blendShape1.AK_18_EyeLookUpRight", "eyeLookUpRight" };
        private List<string> eyeLLookDownOptions = new List<string> { "Eye_L_Look_Down", "blendShape1.AK_11_EyeLookDownLeft", "eyeLookDownLeft" };
        private List<string> eyeRLookDownOptions = new List<string> { "Eye_R_Look_Down", "blendShape1.AK_12_EyeLookDownRight", "eyeLookDownRight" };

        [Header("Gaze Targets")]
        public Transform playerTarget;
        // public Transform itemTarget;

        // v2.8.0 — group-conversation gaze. Set by SpeakerTracker (or any caller) to a peer agent's
        // head transform; activated by switching currentGazeMode to GazeMode.LookAtAgent. Falls back
        // to Idle if null at update time.
        [Tooltip("Target transform for LookAtAgent gaze mode. Typically set at runtime by SpeakerTracker to point at the current group speaker.")]
        public Transform agentTarget;

        [Header("Gaze Settings")]
        [Range(0, 100)] public float maxEyeLookAngle = 45f; // Max angle for eye blendshape movement
        [Range(0, 100)] public float eyeBlendshapeStrength = 100f; // Max blendshape weight (0-100)
        public float eyeGazeSpeed = 2f; // Speed of eye movement (lerp factor)

        [Tooltip("How far the gaze wanders in Idle mode. 1 = up to 45 degrees in any direction; lower values keep the agent looking roughly ahead.")]
        [Range(0f, 1f)] public float idleGazeAmplitude = 1f;

        [Header("Head Gaze Settings (IK)")]
        [Range(0, 1)] public float headLookWeight = 0.5f; // How much the head follows the target (0-1)
        public float headGazeSpeed = 4.0f; // Speed of head movement
        public float maxHeadLookAngle = 60f; // Max angle the head can turn

        // Offset for the IK target from the actual gaze point (e.g., to look slightly above or below)
        public Vector3 headLookOffset = Vector3.zero;

        [Header("Eye Bones (rigs with separate eyeball meshes, e.g. Avatar SDK)")]
        [Tooltip("Rotate the humanoid LeftEye/RightEye bones toward the gaze direction after animation. Needed when the eyeballs are separate meshes skinned to eye bones; eye-look blendshapes then only move the eyelids.")]
        public bool driveEyeBones = false;
        [Range(0, 45)] public float maxEyeBoneYaw = 30f;
        [Range(0, 45)] public float maxEyeBonePitch = 20f;

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
            // v2.8.0 — used by group-conversation flows. Eyes/head follow agentTarget.
            LookAtAgent
        }

        public GazeMode currentGazeMode = GazeMode.Idle;

        // Eye-look blendshapes, resolved on the main renderer and every additional renderer.
        private BlendshapeBinding eyeLLookL, eyeRLookL, eyeLLookR, eyeRLookR;
        private BlendshapeBinding eyeLLookUp, eyeRLookUp, eyeLLookDown, eyeRLookDown;
        private bool hasEyeBlendshapes;

        // Eye bones (driveEyeBones)
        private Transform headBone, leftEyeBone, rightEyeBone;
        private Quaternion leftEyeRestInHead, rightEyeRestInHead;
        private Vector3 eyeForwardInHead, eyeUpInHead;
        private bool eyeBonesReady;

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
                var renderers = new List<SkinnedMeshRenderer> { characterSkinnedMeshRenderer };
                if (additionalRenderers != null) renderers.AddRange(additionalRenderers);

                eyeLLookL = FacialBlendshapeRig.Bind(renderers, eyeLLookLOptions.ToArray());
                eyeRLookL = FacialBlendshapeRig.Bind(renderers, eyeRLookLOptions.ToArray());
                eyeLLookR = FacialBlendshapeRig.Bind(renderers, eyeLLookROptions.ToArray());
                eyeRLookR = FacialBlendshapeRig.Bind(renderers, eyeRLookROptions.ToArray());
                eyeLLookUp = FacialBlendshapeRig.Bind(renderers, eyeLLookUpOptions.ToArray());
                eyeRLookUp = FacialBlendshapeRig.Bind(renderers, eyeRLookUpOptions.ToArray());
                eyeLLookDown = FacialBlendshapeRig.Bind(renderers, eyeLLookDownOptions.ToArray());
                eyeRLookDown = FacialBlendshapeRig.Bind(renderers, eyeRLookDownOptions.ToArray());
                hasEyeBlendshapes = true;

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

            if (driveEyeBones) InitEyeBones();

            Debug.Log("EyeGazeController Initialized. Current Gaze Mode: " + currentGazeMode, this);
        }

        /// <summary>
        /// Captures the eyes' rest orientation relative to the head. Runs in Start, i.e. before the
        /// Animator's first evaluation, while the character still stands in its bind pose facing
        /// <c>transform.forward</c> with the eyes looking straight ahead.
        /// </summary>
        private void InitEyeBones()
        {
            headBone = animator.GetBoneTransform(HumanBodyBones.Head);
            leftEyeBone = animator.GetBoneTransform(HumanBodyBones.LeftEye);
            rightEyeBone = animator.GetBoneTransform(HumanBodyBones.RightEye);
            if (headBone == null || leftEyeBone == null || rightEyeBone == null)
            {
                Debug.LogWarning("EyeGazeController: driveEyeBones is on but the avatar has no Head/LeftEye/RightEye mapping. Eye bones will not move.", this);
                return;
            }

            Quaternion invHead = Quaternion.Inverse(headBone.rotation);
            leftEyeRestInHead = invHead * leftEyeBone.rotation;
            rightEyeRestInHead = invHead * rightEyeBone.rotation;
            eyeForwardInHead = invHead * transform.forward;
            eyeUpInHead = invHead * transform.up;
            eyeBonesReady = true;
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
                    Vector3 idleLookDirLocal = new Vector3(noiseX * idleGazeAmplitude, noiseY * idleGazeAmplitude, 1).normalized; // Z is forward
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
                case GazeMode.LookAtAgent:
                    if (agentTarget != null)
                    {
                        targetGazeDirection = (agentTarget.position - GetEyeCenterPosition()).normalized;
                    }
                    else
                    {
                        // Silent fallback — agentTarget is expected to flicker between assignments
                        // as the group's active speaker changes; warning each frame would spam.
                        currentGazeMode = GazeMode.Idle;
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
            if (eyeBonesReady)
            {
                ApplyGazeToEyeBones(currentGazeDirection);
            }
            else if (characterSkinnedMeshRenderer != null)
            {
                ApplyGazeToBlendshapes(currentGazeDirection);
            }
        }

        void OnAnimatorIK(int layerIndex)
        {
            if (animator == null || !animator.avatar.isHuman) return;

            animator.SetLookAtWeight(headLookWeight, 0.3f, 1.0f, 0f, 0.5f);

            animator.SetLookAtPosition(currentHeadLookAtPosition);
        }
        // Helper method to approximate eye center position
        // For more accuracy, consider getting the actual eye bones from the rig
        // or a specifically placed GameObject for the eye center.
        Vector3 GetEyeCenterPosition()
        {
            if (eyeBonesReady)
            {
                return (leftEyeBone.position + rightEyeBone.position) * 0.5f;
            }
            // If head bone is available, use its position as a base, otherwise fall back to character root + offset
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (head != null)
            {
                // A small offset from the head bone to approximate eye level
                return head.position + head.up * 0.1f;
            }
            return transform.position + transform.up * 1.6f; // Fallback
        }

        /// <summary>
        /// Turns both eye bones toward <paramref name="gazeDirection"/> relative to the animated head,
        /// clamped to <see cref="maxEyeBoneYaw"/>/<see cref="maxEyeBonePitch"/>, and lets the eyelid
        /// blendshapes follow. Runs after the Animator has written the frame's pose.
        /// </summary>
        void ApplyGazeToEyeBones(Vector3 gazeDirection)
        {
            Quaternion headRotation = headBone.rotation;
            Quaternion headFrame = headRotation * Quaternion.LookRotation(eyeForwardInHead, eyeUpInHead);

            Vector3 local = Quaternion.Inverse(headFrame) * gazeDirection;
            float yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            float pitch = Mathf.Asin(Mathf.Clamp(local.y, -1f, 1f)) * Mathf.Rad2Deg;
            yaw = Mathf.Clamp(yaw, -maxEyeBoneYaw, maxEyeBoneYaw);
            pitch = Mathf.Clamp(pitch, -maxEyeBonePitch, maxEyeBonePitch);

            // Positive pitch looks up, i.e. a negative rotation about the frame's X axis.
            Quaternion offset = Quaternion.Euler(-pitch, yaw, 0f);
            Quaternion worldOffset = headFrame * offset * Quaternion.Inverse(headFrame);

            leftEyeBone.rotation = worldOffset * (headRotation * leftEyeRestInHead);
            rightEyeBone.rotation = worldOffset * (headRotation * rightEyeRestInHead);

            if (hasEyeBlendshapes)
            {
                ResetAllEyeBlendshapes();
                ApplyEyeLookWeights(yaw, pitch, maxEyeBoneYaw, maxEyeBonePitch);
            }
        }

        void ApplyGazeToBlendshapes(Vector3 gazeDirection)
        {
            // Ensure characterSkinnedMeshRenderer is valid before proceeding
            if (characterSkinnedMeshRenderer == null || characterSkinnedMeshRenderer.sharedMesh == null || !hasEyeBlendshapes)
            {
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

            ApplyEyeLookWeights(horizontalAngle, verticalAngle, maxEyeLookAngle, maxEyeLookAngle);
        }

        /// <summary>Negative horizontal = character's left, positive vertical = up.</summary>
        void ApplyEyeLookWeights(float horizontalAngle, float verticalAngle, float maxHorizontal, float maxVertical)
        {
            if (maxHorizontal <= 0f || maxVertical <= 0f) return;

            // Horizontal (Left/Right)
            if (horizontalAngle < -0.1f) // Looking Left
            {
                float weight = Mathf.Abs(horizontalAngle) / maxHorizontal * eyeBlendshapeStrength;
                eyeLLookL.Set(weight);
                eyeRLookL.Set(weight);
            }
            else if (horizontalAngle > 0.1f) // Looking Right
            {
                float weight = horizontalAngle / maxHorizontal * eyeBlendshapeStrength;
                eyeLLookR.Set(weight);
                eyeRLookR.Set(weight);
            }

            // Vertical (Up/Down)
            if (verticalAngle > 0.1f) // Looking Up
            {
                float weight = verticalAngle / maxVertical * eyeBlendshapeStrength;
                eyeLLookUp.Set(weight);
                eyeRLookUp.Set(weight);
            }
            else if (verticalAngle < -0.1f) // Looking Down
            {
                float weight = Mathf.Abs(verticalAngle) / maxVertical * eyeBlendshapeStrength;
                eyeLLookDown.Set(weight);
                eyeRLookDown.Set(weight);
            }
        }

        void ResetAllEyeBlendshapes()
        {
            if (!hasEyeBlendshapes) return;

            eyeLLookL.Set(0);
            eyeRLookL.Set(0);
            eyeLLookR.Set(0);
            eyeRLookR.Set(0);
            eyeLLookUp.Set(0);
            eyeRLookUp.Set(0);
            eyeLLookDown.Set(0);
            eyeRLookDown.Set(0);
        }

        void CheckBlendshapeIndices()
        {
            // Only log errors if SkinnedMeshRenderer is present, otherwise it's expected
            if (characterSkinnedMeshRenderer == null) return;

            bool allFound = true;
            if (!eyeLLookL.IsValid) { Debug.LogError($"EyeGazeController: Blendshape '{string.Join("/", eyeLLookLOptions)}' not found on mesh.", this); allFound = false; }
            if (!eyeRLookL.IsValid) { Debug.LogError($"EyeGazeController: Blendshape '{string.Join("/", eyeRLookLOptions)}' not found on mesh.", this); allFound = false; }
            if (!eyeLLookR.IsValid) { Debug.LogError($"EyeGazeController: Blendshape '{string.Join("/", eyeLLookROptions)}' not found on mesh.", this); allFound = false; }
            if (!eyeRLookR.IsValid) { Debug.LogError($"EyeGazeController: Blendshape '{string.Join("/", eyeRLookROptions)}' not found on mesh.", this); allFound = false; }
            if (!eyeLLookUp.IsValid) { Debug.LogError($"EyeGazeController: Blendshape '{string.Join("/", eyeLLookUpOptions)}' not found on mesh.", this); allFound = false; }
            if (!eyeRLookUp.IsValid) { Debug.LogError($"EyeGazeController: Blendshape '{string.Join("/", eyeRLookUpOptions)}' not found on mesh.", this); allFound = false; }
            if (!eyeLLookDown.IsValid) { Debug.LogError($"EyeGazeController: Blendshape '{string.Join("/", eyeLLookDownOptions)}' not found on mesh.", this); allFound = false; }
            if (!eyeRLookDown.IsValid) { Debug.LogError($"EyeGazeController: Blendshape '{string.Join("/", eyeRLookDownOptions)}' not found on mesh.", this); allFound = false; }

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

        /// <summary>
        /// v2.8.0 — switch to <see cref="GazeMode.LookAtAgent"/> targeting the given transform.
        /// Used by group-conversation flows so listening agents track the active speaker.
        /// </summary>
        public void SetGazeModeLookAtAgent(Transform target)
        {
            agentTarget = target;
            currentGazeMode = GazeMode.LookAtAgent;
        }

        // public void SetGazeModeLookAtItem()
        // {
        //     currentGazeMode = GazeMode.LookAtItem;
        //     Debug.Log("EyeGazeController: Switched to Look At Item Gaze Mode.", this);
        // }
    }
}
