using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IVH.Core.UI
{
    /// <summary>
    /// Makes a text larger or smaller at runtime (for example the conversation transcript on a large or
    /// distant display) and remembers the size between sessions. Wire two buttons, or call
    /// <see cref="Increase"/> / <see cref="Decrease"/> from your own input.
    /// </summary>
    [DisallowMultipleComponent]
    public class UITextSizeControl : MonoBehaviour
    {
        [Tooltip("The text whose font size is changed.")]
        public Text target;

        [Tooltip("Optional button that makes the text larger.")]
        public Button increaseButton;

        [Tooltip("Optional button that makes the text smaller.")]
        public Button decreaseButton;

        [Range(6, 128)] public int minFontSize = 12;
        [Range(6, 128)] public int maxFontSize = 64;
        [Range(1, 16)] public int step = 2;

        [Tooltip("PlayerPrefs key. Empty = '<scene name>/<object name>'.")]
        public string storageKey = "";

        private string Key => "IVA.UI.FontSize." + (string.IsNullOrEmpty(storageKey) ? gameObject.scene.name + "/" + name : storageKey);

        private void Awake()
        {
            if (increaseButton != null) increaseButton.onClick.AddListener(Increase);
            if (decreaseButton != null) decreaseButton.onClick.AddListener(Decrease);
            if (target != null && PlayerPrefs.HasKey(Key))
                target.fontSize = Mathf.Clamp(PlayerPrefs.GetInt(Key), minFontSize, maxFontSize);
        }

        private void OnDestroy()
        {
            if (increaseButton != null) increaseButton.onClick.RemoveListener(Increase);
            if (decreaseButton != null) decreaseButton.onClick.RemoveListener(Decrease);
        }

        /// <summary>Makes the text one step larger.</summary>
        public void Increase() => SetFontSize(target != null ? target.fontSize + step : 0);

        /// <summary>Makes the text one step smaller.</summary>
        public void Decrease() => SetFontSize(target != null ? target.fontSize - step : 0);

        /// <summary>Sets and stores the font size (clamped to the allowed range).</summary>
        public void SetFontSize(int size)
        {
            if (target == null) return;
            target.fontSize = Mathf.Clamp(size, minFontSize, maxFontSize);
            PlayerPrefs.SetInt(Key, target.fontSize);
            PlayerPrefs.Save();
            // A button that keeps the selection would be pressed again by Space or Enter.
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }
    }
}
