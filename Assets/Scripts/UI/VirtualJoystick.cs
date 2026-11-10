using UnityEngine;
using UnityEngine.EventSystems;
using PixelSaur.Player;

namespace PixelSaur.UI
{
    /// <summary>
    /// 移动端虚拟摇杆组件：捕获触控拖拽，计算标准归一化方向向量并传递给 PlayerInputProxy。
    /// </summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("UI 引用")]
        [Tooltip("摇杆底座背景")]
        [SerializeField] private RectTransform background;

        [Tooltip("摇杆可拖动的操纵钮")]
        [SerializeField] private RectTransform handle;

        [Header("参数配置")]
        [Tooltip("摇杆操纵钮最大移动半径（像素）")]
        [Range(20f, 200f)]
        [SerializeField] private float handleRange = 80f;

        [Tooltip("死区阈值（低于此模长视为无输入）")]
        [Range(0f, 0.3f)]
        [SerializeField] private float deadZone = 0.05f;

        [Tooltip("是否为动态摇杆（按下时将底盘移至手指触摸点）")]
        [SerializeField] private bool dynamicJoystick = false;

        private Vector2 _defaultBackgroundPosition;
        private Canvas _canvas;

        private void Awake()
        {
            if (background == null)
            {
                background = GetComponent<RectTransform>();
            }
            if (handle == null && transform.childCount > 0)
            {
                handle = transform.GetChild(0) as RectTransform;
            }

            _canvas = GetComponentInParent<Canvas>();
            if (background != null)
            {
                _defaultBackgroundPosition = background.anchoredPosition;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (dynamicJoystick && background != null)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    background.parent as RectTransform,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 localPoint
                );
                background.anchoredPosition = localPoint;
            }

            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (background == null || handle == null) return;

            Camera cam = _canvas != null && _canvas.renderMode == RenderMode.ScreenSpaceCamera ? _canvas.worldCamera : null;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(background, eventData.position, cam, out Vector2 position))
            {
                // 计算偏移向量并限制在有效半径内
                Vector2 clampedPos = Vector2.ClampMagnitude(position, handleRange);
                handle.anchoredPosition = clampedPos;

                // 归一化输入值 (-1 ~ 1)
                Vector2 normalizedInput = clampedPos / handleRange;
                if (normalizedInput.magnitude < deadZone)
                {
                    normalizedInput = Vector2.zero;
                }

                if (PlayerInputProxy.Instance != null)
                {
                    PlayerInputProxy.Instance.SetJoystickInput(normalizedInput);
                }
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (handle != null)
            {
                handle.anchoredPosition = Vector2.zero;
            }

            if (dynamicJoystick && background != null)
            {
                background.anchoredPosition = _defaultBackgroundPosition;
            }

            if (PlayerInputProxy.Instance != null)
            {
                PlayerInputProxy.Instance.SetJoystickInput(Vector2.zero);
            }
        }
    }
}
