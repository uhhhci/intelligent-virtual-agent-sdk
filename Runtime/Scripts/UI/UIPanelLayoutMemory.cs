using UnityEngine;

namespace IVH.Core.UI
{
    /// <summary>
    /// Remembers a HUD panel's position and size between sessions and keeps the panel on screen.
    /// Put it on a panel that the user moves with a <see cref="UIDragHandle"/> or resizes with a
    /// <see cref="UIResizeHandle"/>: the layout arranged once (for example on a portrait display)
    /// comes back the next time the scene plays.
    /// </summary>
    /// <remarks>
    /// The layout is stored in <see cref="PlayerPrefs"/> half a second after the user stops changing it.
    /// When the screen is smaller than the panel (a small Game view, a different display), the panel
    /// is moved, and shrunk if needed, so its title bar and resize grip stay reachable.
    /// </remarks>
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public class UIPanelLayoutMemory : MonoBehaviour
    {
        [Tooltip("PlayerPrefs key. Empty = '<scene name>/<object name>'.")]
        public string storageKey = "";

        [Tooltip("Restore the saved position and size when the panel starts.")]
        public bool restoreOnStart = true;

        [Tooltip("Keep the panel inside its parent (the screen), shrinking it when the screen is smaller than the panel.")]
        public bool keepOnScreen = true;

        [Tooltip("Smallest size the panel is shrunk to when it does not fit on screen.")]
        public Vector2 minSize = new Vector2(200f, 120f);

        private const float SaveDelaySeconds = 0.5f;

        private RectTransform _rt;
        private Vector2 _lastPosition;
        private Vector2 _lastSize;
        private Vector2 _lastParentSize;
        private float _changedAt = -1f;
        private readonly Vector3[] _corners = new Vector3[4];

        private string Key => "IVA.UI." + (string.IsNullOrEmpty(storageKey) ? gameObject.scene.name + "/" + name : storageKey);

        private void Awake()
        {
            _rt = (RectTransform)transform;
        }

        private void Start()
        {
            if (restoreOnStart) Restore();
            _lastPosition = _rt.anchoredPosition;
            _lastSize = _rt.sizeDelta;
        }

        private void LateUpdate()
        {
            var parent = _rt.parent as RectTransform;
            if (keepOnScreen && parent != null && parent.rect.size != _lastParentSize)
            {
                _lastParentSize = parent.rect.size;
                ClampIntoParent();
            }

            if (_rt.anchoredPosition != _lastPosition || _rt.sizeDelta != _lastSize)
            {
                _lastPosition = _rt.anchoredPosition;
                _lastSize = _rt.sizeDelta;
                _changedAt = Time.unscaledTime;
            }
            else if (_changedAt >= 0f && Time.unscaledTime - _changedAt > SaveDelaySeconds)
            {
                _changedAt = -1f;
                if (keepOnScreen) ClampIntoParent();
                Save();
            }
        }

        private void OnDisable()
        {
            if (_changedAt >= 0f)
            {
                _changedAt = -1f;
                Save();
            }
        }

        /// <summary>Applies the saved layout, if there is one.</summary>
        public void Restore()
        {
            string key = Key;
            if (!PlayerPrefs.HasKey(key + ".x")) return;
            _rt.anchoredPosition = new Vector2(PlayerPrefs.GetFloat(key + ".x"), PlayerPrefs.GetFloat(key + ".y"));
            _rt.sizeDelta = new Vector2(PlayerPrefs.GetFloat(key + ".w", _rt.sizeDelta.x), PlayerPrefs.GetFloat(key + ".h", _rt.sizeDelta.y));
        }

        /// <summary>Stores the current layout.</summary>
        public void Save()
        {
            string key = Key;
            PlayerPrefs.SetFloat(key + ".x", _rt.anchoredPosition.x);
            PlayerPrefs.SetFloat(key + ".y", _rt.anchoredPosition.y);
            PlayerPrefs.SetFloat(key + ".w", _rt.sizeDelta.x);
            PlayerPrefs.SetFloat(key + ".h", _rt.sizeDelta.y);
            PlayerPrefs.Save();
        }

        /// <summary>Deletes the saved layout; the panel starts from its scene layout next time.</summary>
        [ContextMenu("Forget Saved Layout")]
        public void Forget()
        {
            string key = Key;
            PlayerPrefs.DeleteKey(key + ".x");
            PlayerPrefs.DeleteKey(key + ".y");
            PlayerPrefs.DeleteKey(key + ".w");
            PlayerPrefs.DeleteKey(key + ".h");
            PlayerPrefs.Save();
        }

        /// <summary>Shrinks the panel to fit its parent if needed, then moves it fully inside.</summary>
        public void ClampIntoParent()
        {
            if (_rt == null) _rt = (RectTransform)transform;
            var parent = _rt.parent as RectTransform;
            if (parent == null) return;

            Rect bounds = parent.rect;
            if (bounds.width <= 0f || bounds.height <= 0f) return;

            Vector2 size = _rt.rect.size;
            float width = Mathf.Max(Mathf.Min(size.x, bounds.width), Mathf.Min(minSize.x, bounds.width));
            float height = Mathf.Max(Mathf.Min(size.y, bounds.height), Mathf.Min(minSize.y, bounds.height));
            if (!Mathf.Approximately(width, size.x) || !Mathf.Approximately(height, size.y))
                _rt.sizeDelta += new Vector2(width - size.x, height - size.y);

            _rt.GetWorldCorners(_corners);
            Vector2 min = parent.InverseTransformPoint(_corners[0]);
            Vector2 max = parent.InverseTransformPoint(_corners[2]);
            Vector2 shift = Vector2.zero;
            if (min.x < bounds.xMin) shift.x = bounds.xMin - min.x;
            else if (max.x > bounds.xMax) shift.x = bounds.xMax - max.x;
            if (max.y > bounds.yMax) shift.y = bounds.yMax - max.y;
            else if (min.y < bounds.yMin) shift.y = bounds.yMin - min.y;
            if (shift != Vector2.zero) _rt.anchoredPosition += shift;
        }
    }
}
