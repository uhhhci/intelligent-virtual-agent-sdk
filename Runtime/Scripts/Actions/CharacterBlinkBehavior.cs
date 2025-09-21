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
        // Blendshape indices for ARKit standard eye blinks
        private int eyeBlinkLeftIndex = -1;
        private int eyeBlinkRightIndex = -1;

        // Blink animation parameters
        public float minBlinkInterval = 0.3f; // Minimum time between blinks in seconds
        public float maxBlinkInterval = 3.0f; // Maximum time between blinks in seconds
        public float blinkDuration = 0.1f; // Time for the blink animation

        private float nextBlinkTime = 0f;
        private bool isBlinking = false;

        private void Start()
        {
            // Validate blendshape indices
            eyeBlinkLeftIndex = faceRenderer.sharedMesh.GetBlendShapeIndex(leftEyeBlendShapeName);
            eyeBlinkRightIndex = faceRenderer.sharedMesh.GetBlendShapeIndex(rightEyeBlendShapeName);

            if (eyeBlinkLeftIndex == -1 || eyeBlinkRightIndex == -1)
            {
                Debug.LogError("Eye blink blendshapes ('eyeBlinkLeft', 'eyeBlinkRight') not found on the SkinnedMeshRenderer.");
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

        private System.Collections.IEnumerator Blink()
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

        private System.Collections.IEnumerator AnimateBlendshape(float targetValue)
        {
            float startValue = faceRenderer.GetBlendShapeWeight(eyeBlinkLeftIndex);
            float elapsedTime = 0f;
            float animationTime = blinkDuration / 2f; // Half the total blink duration

            while (elapsedTime < animationTime)
            {
                elapsedTime += Time.deltaTime;
                float currentValue = Mathf.Lerp(startValue, targetValue, elapsedTime / animationTime);
                faceRenderer.SetBlendShapeWeight(eyeBlinkLeftIndex, currentValue);
                faceRenderer.SetBlendShapeWeight(eyeBlinkRightIndex, currentValue);
                yield return null;
            }

            faceRenderer.SetBlendShapeWeight(eyeBlinkLeftIndex, targetValue);
            faceRenderer.SetBlendShapeWeight(eyeBlinkRightIndex, targetValue);
        }
    }
}