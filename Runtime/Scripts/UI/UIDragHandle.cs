using UnityEngine;
using UnityEngine.EventSystems;

namespace IVH.Core.UI
{
    /// <summary>
    /// Drag handle that moves a target <see cref="RectTransform"/> when the user drags on this
    /// GameObject. Use it on a header strip placed above a draggable HUD panel — for example, the
    /// title bar of the Gemini settings panel.
    /// </summary>
    /// <remarks>
    /// The handle reads pointer positions in the target's parent rect, so it works under any
    /// canvas render mode (ScreenSpaceOverlay, ScreenSpaceCamera, WorldSpace). When
    /// <see cref="clampToParent"/> is true, the dragged target is kept fully inside its parent's
    /// rect — a deliberate restriction so users can't lose the panel off-screen.
    /// </remarks>
    [RequireComponent(typeof(RectTransform))]
    public class UIDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        /// <summary>RectTransform to move. Falls back to this GameObject's RectTransform if null.</summary>
        public RectTransform target;

        /// <summary>If true, the dragged target's rect is clamped to its parent's rect so the panel
        /// can't be dragged off-screen.</summary>
        public bool clampToParent = true;

        private Vector2 _pointerStart;
        private Vector2 _targetStart;

        private void Awake()
        {
            if (target == null) target = (RectTransform)transform;
        }

        /// <inheritdoc/>
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (target == null) return;
            _targetStart = target.anchoredPosition;
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

            Vector2 newPos = _targetStart + (current - _pointerStart);
            if (clampToParent)
            {
                newPos = ClampToParent(target, parentRt, newPos);
            }
            target.anchoredPosition = newPos;
        }

        private static Vector2 ClampToParent(RectTransform target, RectTransform parent, Vector2 desiredAnchored)
        {
            // The target's local origin (when anchoredPosition == 0) sits at the anchor center
            // expressed in the parent's rect. We work out the min/max anchoredPosition such that
            // the target's rect stays inside the parent's rect.
            Rect parentRect = parent.rect;
            Vector2 size = target.rect.size;

            float anchorXCenter = (target.anchorMin.x + target.anchorMax.x) * 0.5f;
            float anchorYCenter = (target.anchorMin.y + target.anchorMax.y) * 0.5f;
            float anchorOriginX = parentRect.xMin + anchorXCenter * parentRect.width;
            float anchorOriginY = parentRect.yMin + anchorYCenter * parentRect.height;

            float pivotOffsetX = target.pivot.x * size.x;
            float pivotOffsetY = target.pivot.y * size.y;

            float minX = parentRect.xMin - anchorOriginX + pivotOffsetX;
            float maxX = parentRect.xMax - anchorOriginX - (size.x - pivotOffsetX);
            float minY = parentRect.yMin - anchorOriginY + pivotOffsetY;
            float maxY = parentRect.yMax - anchorOriginY - (size.y - pivotOffsetY);

            // When the target is larger than the parent on an axis, min > max; collapse to the
            // start position so the clamp doesn't flip the panel around.
            if (minX > maxX) { minX = maxX = desiredAnchored.x; }
            if (minY > maxY) { minY = maxY = desiredAnchored.y; }

            return new Vector2(Mathf.Clamp(desiredAnchored.x, minX, maxX), Mathf.Clamp(desiredAnchored.y, minY, maxY));
        }
    }
}
