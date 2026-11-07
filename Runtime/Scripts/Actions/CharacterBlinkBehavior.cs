using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IVH.Core.Actions
{
    public class CharacterBlinkBehavior : MonoBehaviour
    {
        // Reference to the SkinnedMeshRenderer containing the blendshapes
        public SkinnedMeshRenderer faceRenderer;
        public string leftEyeBlendShapeName = "Eye_Blink_L"; // cc4 convention
        public string rightEyeBlendShapeName = "Eye_Blink_R";

        [Tooltip("Other meshes that carry their own copy of the blink blendshapes (e.g. Avatar SDK eyelashes). They blink together with the face renderer.")]
        public List<SkinnedMeshRenderer> additionalRenderers = new List<SkinnedMeshRenderer>();

        // Blink animation parameters
        public float minBlinkInterval = 0.3f; // Minimum time between blinks in seconds
        public float maxBlinkInterval = 3.0f; // Maximum time between blinks in seconds
        public float blinkDuration = 0.1f; // Time for the blink animation

        private float nextBlinkTime = 0f;
        private bool isBlinking = false;
        private BlendshapeBinding leftBlink;
        private BlendshapeBinding rightBlink;

        /// <summary>Current procedural blink weight (0-100). Lets other face drivers combine their own blinks with it.</summary>
        public float CurrentWeight { get; private set; }

        private void Start()
        {
            var renderers = new List<SkinnedMeshRenderer> { faceRenderer };
            if (additionalRenderers != null) renderers.AddRange(additionalRenderers);

            leftBlink = FacialBlendshapeRig.Bind(renderers, leftEyeBlendShapeName);
            rightBlink = FacialBlendshapeRig.Bind(renderers, rightEyeBlendShapeName);

            if (!leftBlink.IsValid || !rightBlink.IsValid)
            {
                Debug.LogError($"Eye blink blendshapes ('{leftEyeBlendShapeName}', '{rightEyeBlendShapeName}') not found on the SkinnedMeshRenderer.");
                enabled = false;
                return;
            }

            ScheduleNextBlink();
        }

        private void Update()
        {
            if (Time.time >= nextBlinkTime && !isBlinking)
            {
                StartCoroutine(Blink());
            }
        }

        private void ScheduleNextBlink()
        {
            nextBlinkTime = Time.time + Random.Range(minBlinkInterval, maxBlinkInterval);
        }

        private IEnumerator Blink()
        {
            isBlinking = true;

            // Close eyes
            yield return AnimateBlendshape(80.0f);

            // Pause at the closed position for a brief moment
            yield return new WaitForSeconds(blinkDuration);

            // Open eyes
            yield return AnimateBlendshape(0.0f);

            ScheduleNextBlink();
            isBlinking = false;
        }

        private IEnumerator AnimateBlendshape(float targetValue)
        {
            float startValue = leftBlink.Get();
            float elapsedTime = 0f;
            float animationTime = blinkDuration / 2f; // Half the total blink duration

            while (elapsedTime < animationTime)
            {
                elapsedTime += Time.deltaTime;
                float currentValue = Mathf.Lerp(startValue, targetValue, elapsedTime / animationTime);
                leftBlink.Set(currentValue);
                rightBlink.Set(currentValue);
                CurrentWeight = currentValue;
                yield return null;
            }

            leftBlink.Set(targetValue);
            rightBlink.Set(targetValue);
            CurrentWeight = targetValue;
        }
    }
}
