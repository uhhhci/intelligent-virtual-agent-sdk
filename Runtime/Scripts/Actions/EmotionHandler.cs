using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace IVH.Core.IntelligentVirtualAgent
{
public class EmotionHandler : MonoBehaviour
{
    public SkinnedMeshRenderer skinnedMeshRenderer;
    public CharacterType characterType = CharacterType.CC4OrDIDIMO;
    public void HandleEmotion(string emotion, float intensity, string duration)
    {
        switch (emotion)
        {
            case "happy":
                Debug.Log("happiness emotion detected!");
                // Handle happiness emotion
                SetHappinessBlendshapes(intensity);
                if (duration == "before")
                {
                    StartCoroutine(ResetBlendshapesAfterTime(2.0f));
                }
                break;
            case "sad":
                Debug.Log("Sadness emotion detected!");
                // Handle sadness emotion
                SetSadnessBlendshapes(intensity);
                if (duration == "before")
                {
                    StartCoroutine(ResetBlendshapesAfterTime(2.0f));
                }
                break;
            case "angry":
                Debug.Log("Anger emotion detected!");
                // Handle anger emotion
                SetAngerBlendshapes(intensity);
                if (duration == "before")
                {
                    StartCoroutine(ResetBlendshapesAfterTime(2.0f));
                }
                break;
            case "scared":
                Debug.Log("Fear emotion detected!");
                // Handle fear emotion
                SetFearBlendshapes(intensity);
                if (duration == "before")
                {
                    StartCoroutine(ResetBlendshapesAfterTime(2.0f));
                }
                break;
            case "disgusted":
                Debug.Log("Disgust emotion detected!");
                // Handle disgust emotion
                SetDisgustBlendshapes(intensity);
                if (duration == "before")
                {
                    StartCoroutine(ResetBlendshapesAfterTime(2.0f));
                }
                break;
            case "surprised":
                Debug.Log("Surprise emotion detected!");
                SetSurpriseBlendshapes(intensity);
                if (duration == "before")
                {
                    StartCoroutine(ResetBlendshapesAfterTime(2.0f));
                }
                break;
            case "neutral":
                Debug.Log("Neutral emotion detected!");
                // Handle neutral emotion
                ResetBlendShapes();
                break;
            default:
                break;
        }
    }

    // all blendshape name follows ARKit convention
    private void SetHappinessBlendshapes(float intensity)
    {
        ResetBlendShapes();
        
        switch (characterType)
        {
            case CharacterType.CC4OrDIDIMO:
                skinnedMeshRenderer.SetBlendShapeWeight(37, 100 * intensity); // check squint left
                skinnedMeshRenderer.SetBlendShapeWeight(38, 100 * intensity); // check squint right
                skinnedMeshRenderer.SetBlendShapeWeight(41, 100 * intensity); // Mouth Smile Left
                skinnedMeshRenderer.SetBlendShapeWeight(42, 100 * intensity); // Mouth Smile Right
                break;
            case CharacterType.Rocketbox:
                skinnedMeshRenderer.SetBlendShapeWeight(21, 100 * intensity); // Check squint left
                skinnedMeshRenderer.SetBlendShapeWeight(22, 100 * intensity); // check squint right
                skinnedMeshRenderer.SetBlendShapeWeight(58, 100 * intensity); // Mouth Smile Left
                skinnedMeshRenderer.SetBlendShapeWeight(59, 100 * intensity); // Mouth Smile Right
                break;
        }
    }

    private void SetSadnessBlendshapes(float intensity)
    {
        ResetBlendShapes();
        
        switch (characterType)
        {
            case CharacterType.CC4OrDIDIMO:
                skinnedMeshRenderer.SetBlendShapeWeight(19, 100 * intensity); // brown down left
                skinnedMeshRenderer.SetBlendShapeWeight(20, 100 * intensity); // brown down right
                skinnedMeshRenderer.SetBlendShapeWeight(15, 100 * intensity); // brown inner up
                skinnedMeshRenderer.SetBlendShapeWeight(16, 100 * intensity); // brown inner up
                skinnedMeshRenderer.SetBlendShapeWeight(21, 20 * intensity);  // eye blink left
                skinnedMeshRenderer.SetBlendShapeWeight(22, 20 * intensity); // eye blink right
                skinnedMeshRenderer.SetBlendShapeWeight(33, 50 * intensity); // eye look down left
                skinnedMeshRenderer.SetBlendShapeWeight(34, 50 * intensity); // eye look down right
                skinnedMeshRenderer.SetBlendShapeWeight(43, 100 * intensity); // mouth frown left
                skinnedMeshRenderer.SetBlendShapeWeight(44, 100 * intensity); // mouth frown right
                break;
            case CharacterType.Rocketbox:
                skinnedMeshRenderer.SetBlendShapeWeight(15, 100 * intensity); // brown down left
                skinnedMeshRenderer.SetBlendShapeWeight(16, 100 * intensity); // brown down right
                skinnedMeshRenderer.SetBlendShapeWeight(17, 100 * intensity); // brown inner up
                skinnedMeshRenderer.SetBlendShapeWeight(18, 100 * intensity); // brown inner up
                skinnedMeshRenderer.SetBlendShapeWeight(23, 20 * intensity);  // eye blink left
                skinnedMeshRenderer.SetBlendShapeWeight(24, 20 * intensity); // eye blink right
                skinnedMeshRenderer.SetBlendShapeWeight(25, 50 * intensity); // eye look down left
                skinnedMeshRenderer.SetBlendShapeWeight(26, 50 * intensity); // eye look down right
                skinnedMeshRenderer.SetBlendShapeWeight(44, 100 * intensity); // mouth frown left
                skinnedMeshRenderer.SetBlendShapeWeight(45, 100 * intensity); // mouth frown right
                break;
        }
    }

    private void SetAngerBlendshapes(float intensity)
    {
        ResetBlendShapes();
        
        switch (characterType)
        {
            case CharacterType.CC4OrDIDIMO:
                skinnedMeshRenderer.SetBlendShapeWeight(19, 100 * intensity); // brow down left
                skinnedMeshRenderer.SetBlendShapeWeight(20, 100 * intensity); // brow down right
                skinnedMeshRenderer.SetBlendShapeWeight(17, 100 * intensity); // brow outer up left
                skinnedMeshRenderer.SetBlendShapeWeight(18, 100 * intensity); // brow outer up right
                skinnedMeshRenderer.SetBlendShapeWeight(23, 100 * intensity); // eye squint left
                skinnedMeshRenderer.SetBlendShapeWeight(24, 100 * intensity); // eye squint right
                skinnedMeshRenderer.SetBlendShapeWeight(25, 70 * intensity); // eye wide left
                skinnedMeshRenderer.SetBlendShapeWeight(26, 70 * intensity); // eye wide right
                skinnedMeshRenderer.SetBlendShapeWeight(43, 100 * intensity); // mouth frown left
                skinnedMeshRenderer.SetBlendShapeWeight(44, 100 * intensity); // mouth frown right
                skinnedMeshRenderer.SetBlendShapeWeight(57, 60 * intensity); // mouth shrug lower
                skinnedMeshRenderer.SetBlendShapeWeight(58, 60 * intensity); // mouth shrug upper
                break;
            case CharacterType.Rocketbox:
                skinnedMeshRenderer.SetBlendShapeWeight(15, 100 * intensity); // brow down left
                skinnedMeshRenderer.SetBlendShapeWeight(16, 100 * intensity); // brow down right
                skinnedMeshRenderer.SetBlendShapeWeight(18, 100 * intensity); // brow outer up left
                skinnedMeshRenderer.SetBlendShapeWeight(19, 100 * intensity); // brow outer up right
                skinnedMeshRenderer.SetBlendShapeWeight(33, 100 * intensity); // eye squint left
                skinnedMeshRenderer.SetBlendShapeWeight(34, 100 * intensity); // eye squint right
                skinnedMeshRenderer.SetBlendShapeWeight(35, 70 * intensity); // eye wide left
                skinnedMeshRenderer.SetBlendShapeWeight(36, 70 * intensity); // eye wide right
                skinnedMeshRenderer.SetBlendShapeWeight(44, 100 * intensity); // mouth frown left
                skinnedMeshRenderer.SetBlendShapeWeight(45, 100 * intensity); // mouth frown right
                skinnedMeshRenderer.SetBlendShapeWeight(54, 40 * intensity); // mouth roll lower
                skinnedMeshRenderer.SetBlendShapeWeight(55, 40 * intensity); // mouth roll upper
                skinnedMeshRenderer.SetBlendShapeWeight(56, 60 * intensity); // mouth shrug lower
                skinnedMeshRenderer.SetBlendShapeWeight(57, 60 * intensity); // mouth shrug upper
                break;
        }
    }

    private void SetDisgustBlendshapes(float intensity)
    {
        ResetBlendShapes();
        
        switch (characterType)
        {
            case CharacterType.CC4OrDIDIMO:
                skinnedMeshRenderer.SetBlendShapeWeight(19, 100 * intensity); // brow down left
                skinnedMeshRenderer.SetBlendShapeWeight(20, 100 * intensity); // brow down right
                skinnedMeshRenderer.SetBlendShapeWeight(17, 100 * intensity); // brow outer up left
                skinnedMeshRenderer.SetBlendShapeWeight(18, 100 * intensity); // brow outer up right
                skinnedMeshRenderer.SetBlendShapeWeight(37, 100 * intensity); // check squint left
                skinnedMeshRenderer.SetBlendShapeWeight(38, 100 * intensity); // check squint right
                skinnedMeshRenderer.SetBlendShapeWeight(43, 100 * intensity); // mouth frown left
                skinnedMeshRenderer.SetBlendShapeWeight(44, 100 * intensity); // mouth frown right
                skinnedMeshRenderer.SetBlendShapeWeight(57, 60 * intensity); // mouth shrug lower
                skinnedMeshRenderer.SetBlendShapeWeight(58, 60 * intensity); // mouth shrug upper
                skinnedMeshRenderer.SetBlendShapeWeight(59, 30 * intensity); // mouth upper up left
                skinnedMeshRenderer.SetBlendShapeWeight(60, 30 * intensity); // mouth upper up right
                skinnedMeshRenderer.SetBlendShapeWeight(35, 100 * intensity); // nose sneer left
                skinnedMeshRenderer.SetBlendShapeWeight(36, 100 * intensity); // nose sneer right
                break;
            case CharacterType.Rocketbox:
                skinnedMeshRenderer.SetBlendShapeWeight(15, 100 * intensity); // brow down left
                skinnedMeshRenderer.SetBlendShapeWeight(18, 100 * intensity); // brow down right
                skinnedMeshRenderer.SetBlendShapeWeight(17, 100 * intensity); // brow outer up left
                skinnedMeshRenderer.SetBlendShapeWeight(19, 100 * intensity); // brow outer up right
                skinnedMeshRenderer.SetBlendShapeWeight(21, 100 * intensity); // check squint left
                skinnedMeshRenderer.SetBlendShapeWeight(22, 100 * intensity); // check squint right
                skinnedMeshRenderer.SetBlendShapeWeight(44, 100 * intensity); // mouth frown left
                skinnedMeshRenderer.SetBlendShapeWeight(45, 100 * intensity); // mouth frown right
                skinnedMeshRenderer.SetBlendShapeWeight(56, 60 * intensity); // mouth shrug lower
                skinnedMeshRenderer.SetBlendShapeWeight(57, 60 * intensity); // mouth shrug upper
                skinnedMeshRenderer.SetBlendShapeWeight(62, 30 * intensity); // mouth upper up left
                skinnedMeshRenderer.SetBlendShapeWeight(63, 30 * intensity); // mouth upper up right
                skinnedMeshRenderer.SetBlendShapeWeight(64, 100 * intensity); // nose sneer left
                skinnedMeshRenderer.SetBlendShapeWeight(65, 100 * intensity); // nose sneer right
                break;
        }
    }

    private void SetSurpriseBlendshapes(float intensity)
    {
        ResetBlendShapes();
        
        switch (characterType)
        {
            case CharacterType.CC4OrDIDIMO:
                skinnedMeshRenderer.SetBlendShapeWeight(15, 100 * intensity); // brow inner up
                skinnedMeshRenderer.SetBlendShapeWeight(16, 100 * intensity); // brow inner up
                skinnedMeshRenderer.SetBlendShapeWeight(17, 100 * intensity); // brow outer up left
                skinnedMeshRenderer.SetBlendShapeWeight(18, 100 * intensity); // brow outer up right
                skinnedMeshRenderer.SetBlendShapeWeight(25, 50 * intensity); // eye wide left
                skinnedMeshRenderer.SetBlendShapeWeight(26, 50 * intensity); // eye wide right
                skinnedMeshRenderer.SetBlendShapeWeight(66, 50 * intensity); // jaw open
                break;
            case CharacterType.Rocketbox:
                skinnedMeshRenderer.SetBlendShapeWeight(17, 100 * intensity); // brow inner up
                skinnedMeshRenderer.SetBlendShapeWeight(18, 100 * intensity); // brow outer up left
                skinnedMeshRenderer.SetBlendShapeWeight(19, 100 * intensity); // brow outer up right
                skinnedMeshRenderer.SetBlendShapeWeight(35, 50 * intensity); // eye wide left
                skinnedMeshRenderer.SetBlendShapeWeight(36, 50 * intensity); // eye wide right
                skinnedMeshRenderer.SetBlendShapeWeight(39, 50 * intensity); // jaw open
                break;
        }
    }
    private void SetFearBlendshapes(float intensity)
    {
        ResetBlendShapes();
        
        switch (characterType)
        {
            case CharacterType.CC4OrDIDIMO:
                skinnedMeshRenderer.SetBlendShapeWeight(15, 100 * intensity); // brow inner up
                skinnedMeshRenderer.SetBlendShapeWeight(16, 100 * intensity); // brow inner up
                skinnedMeshRenderer.SetBlendShapeWeight(37, 100 * intensity); // check squint
                skinnedMeshRenderer.SetBlendShapeWeight(38, 100 * intensity); // check squint
                skinnedMeshRenderer.SetBlendShapeWeight(25, 100 * intensity); // eye wide left
                skinnedMeshRenderer.SetBlendShapeWeight(26, 100 * intensity); // eye wide right
                skinnedMeshRenderer.SetBlendShapeWeight(66, 50 * intensity); // jaw open
                skinnedMeshRenderer.SetBlendShapeWeight(45, 30 * intensity); // mouth stretch left
                skinnedMeshRenderer.SetBlendShapeWeight(46, 30 * intensity); // mounth strecth right
                break;
            case CharacterType.Rocketbox:
                skinnedMeshRenderer.SetBlendShapeWeight(17, 100 * intensity); // brow inner up
                skinnedMeshRenderer.SetBlendShapeWeight(21, 100 * intensity); // check squint
                skinnedMeshRenderer.SetBlendShapeWeight(22, 100 * intensity); // check squint
                skinnedMeshRenderer.SetBlendShapeWeight(35, 100 * intensity); // eye wide left
                skinnedMeshRenderer.SetBlendShapeWeight(36, 100 * intensity); // eye wide right
                skinnedMeshRenderer.SetBlendShapeWeight(39, 50 * intensity); // jaw open
                skinnedMeshRenderer.SetBlendShapeWeight(60, 30 * intensity); // mouth stretch left
                skinnedMeshRenderer.SetBlendShapeWeight(61, 30 * intensity); // mounth strecth right
                break;
        }
    }

    private void ResetBlendShapes()
    {
        for (int i = 0; i < skinnedMeshRenderer.sharedMesh.blendShapeCount; i++)
        {
            skinnedMeshRenderer.SetBlendShapeWeight(i, 0);
        }
    }
    
    private System.Collections.IEnumerator ResetBlendshapesAfterTime(float time)
    {
        yield return new WaitForSeconds(time);
        ResetBlendShapes();
    }

}
}