using UnityEngine;
using UnityEngine.EventSystems;
using PixelSaur.Player;

namespace PixelSaur.UI
{
    /// <summary>
    /// 移动端右屏触控滑动视角面板：覆盖在屏幕右半侧，将手指滑动增量转化为视角旋转 Delta。
    /// </summary>
    public class TouchLookArea : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Tooltip("旋转方向反转开关（X 轴）")]
        [SerializeField] private bool invertX = false;

        [Tooltip("旋转方向反转开关（Y 轴）")]
        [SerializeField] private bool invertY = false;

        private Vector2 _lastTouchPosition;
        private int _activePointerId = -1;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_activePointerId == -1)
            {
                _activePointerId = eventData.pointerId;
                _lastTouchPosition = eventData.position;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId == _activePointerId)
            {
                Vector2 currentPos = eventData.position;
                Vector2 delta = currentPos - _lastTouchPosition;
                _lastTouchPosition = currentPos;

                if (invertX) delta.x = -delta.x;
                if (invertY) delta.y = -delta.y;

                if (PlayerInputProxy.Instance != null)
                {
                    PlayerInputProxy.Instance.SetTouchLookDelta(delta);
                }
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == _activePointerId)
            {
                _activePointerId = -1;
            }
        }
    }
}
