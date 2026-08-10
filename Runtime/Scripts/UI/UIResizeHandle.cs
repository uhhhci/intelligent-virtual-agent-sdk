using UnityEngine;
using UnityEngine.EventSystems;

namespace IVH.Core.UI
{
    /// <summary>
    /// Corner grip that resizes a target <see cref="RectTransform"/> via pointer drag. Use it on a
    /// small handle GameObject placed in a corner of a resizable HUD panel — for example, the
    /// bottom-right corner of the Gemini transcription panel.
    /// </summary>
    /// <remarks>
    /// The handle adjusts the target's <c>sizeDelta</c> by the delta between the current pointer
    /// position and the position recorded at <see cref="OnBeginDrag"/>. Because the math works on
    /// the rect's measured size, the behavior is independent of the target's anchor configuration
    /// (works for both fixed and stretched anchors).
    /// </remarks>
    [RequireComponent(typeof(RectTransform))]
    public class UIResizeHandle : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        /// <summary>RectTransform to resize. Falls back to this GameObject's parent RectTransform if null.</summary>
        public RectTransform target;

        /// <summary>Minimum (width, height) in pixels the user is allowed to drag the target down to.</summary>
        public Vector2 minSize = new Vector2(200f, 160f);

        /// <summary>Maximum (width, height) in pixels the user is allowed to drag the target up to.</summary>
        public Vector2 maxSize = new Vector2(4000f, 4000f);

        /// <summary>If true, dragging changes the target's width.</summary>
        public bool resizeWidth = true;

        /// <summary>If true, dragging changes the target's height.</summary>
        public bool resizeHeight = true;

        /// <summary>Sign of the size change per axis. Use <c>(1, -1)</c> for a bottom-right grip
        /// (drag right grows width, drag down — which decreases Y in UI space — grows height).</summary>
        public Vector2 growDirection = new Vector2(1f, -1f);

        private Vector2 _pointerStart;
        private Vector2 _sizeStart;

        private void Awake()
        {
            if (target == null) target = transform.parent as RectTransform;
        }

        /// <inheritdoc/>
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (target == null) return;
            _sizeStart = target.rect.size;
            var parentRt = target.parent as RectTransform;
            if (parentRt == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRt, eventData.position, eventData.pressEventCamera, out _pointerStart);
        }

        /// <inheritdoc/>
        public void OnDrag(PointerEventData eventData)
        {
            if (target == null) return;
            var parentRt = target.parent as RectTransform;
            if (parentRt == null) return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRt, eventData.position, eventData.pressEventCamera, out var current))
            {
                return;
            }

            Vector2 delta = current - _pointerStart;
            float newWidth  = resizeWidth  ? Mathf.Clamp(_sizeStart.x + delta.x * growDirection.x, minSize.x, maxSize.x) : _sizeStart.x;
            float newHeight = resizeHeight ? Mathf.Clamp(_sizeStart.y + delta.y * growDirection.y, minSize.y, maxSize.y) : _sizeStart.y;

            // We bump sizeDelta by the difference between the requested rect size and the current
            // rect size. This works for both fixed and stretched anchors: sizeDelta is always
            // additive on top of the anchor-defined rect, so the same delta produces the same
            // change in measured rect size regardless of anchor layout.
            Vector2 currentSize = target.rect.size;
            Vector2 sizeDelta = target.sizeDelta;
            target.sizeDelta = new Vector2(
                sizeDelta.x + (newWidth  - currentSize.x),
                sizeDelta.y + (newHeight - currentSize.y));
        }
    }
}
