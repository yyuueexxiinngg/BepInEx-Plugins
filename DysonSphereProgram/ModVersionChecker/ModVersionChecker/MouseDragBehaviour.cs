using UnityEngine;
using UnityEngine.EventSystems;

namespace ModVersionChecker
{
    // http://gyanendushekhar.com/2019/11/11/move-canvas-ui-mouse-drag-unity-3d-drag-drop-ui/
    public class MouseDragBehaviour : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        private Vector2 _lastMousePosition;

        public void OnBeginDrag(PointerEventData eventData)
        {
            _lastMousePosition = eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            var currentMousePosition = eventData.position;
            var diff = currentMousePosition - _lastMousePosition;
            var rect = GetComponent<RectTransform>();

            var position = rect.position;
            var newPosition = position + new Vector3(diff.x, diff.y, transform.position.z);
            var oldPos = position;
            position = newPosition;
            rect.position = position;
            if (!IsRectTransformInsideScreen(rect))
            {
                rect.position = oldPos;
            }

            _lastMousePosition = currentMousePosition;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
        }

        private bool IsRectTransformInsideScreen(RectTransform rectTransform)
        {
            var isInside = false;
            var corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            var visibleCorners = 0;
            var rect = new Rect(0, 0, Screen.width, Screen.height);
            foreach (var corner in corners)
            {
                if (rect.Contains(corner))
                {
                    visibleCorners++;
                }
            }

            if (visibleCorners == 4)
            {
                isInside = true;
            }

            return isInside;
        }
    }
}