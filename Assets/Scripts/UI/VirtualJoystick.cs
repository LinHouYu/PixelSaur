using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using PixelSaur.Player;

namespace PixelSaur.UI
{
    /// <summary>
    /// 移动端圆形虚拟摇杆组件：
    /// 1. 自动保证底座（外圆环底盘）与操纵杆（内圆旋钮）为高清抗锯齿正圆形；
    /// 2. 捕获触控拖拽，计算标准归一化方向向量并传递给 PlayerInputProxy。
    /// </summary>
    [ExecuteAlways]
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("UI 引用")]
        [Tooltip("摇杆圆形底座背景")]
        [SerializeField] private RectTransform background;

        [Tooltip("摇杆圆形操纵钮")]
        [SerializeField] private RectTransform handle;

        [Header("参数配置")]
        [Tooltip("摇杆操纵钮最大移动半径（像素）")]
        [Range(20f, 200f)]
        [SerializeField] private float handleRange = 75f;

        [Tooltip("死区阈值（低于此模长视为无输入）")]
        [Range(0f, 0.3f)]
        [SerializeField] private float deadZone = 0.05f;

        [Tooltip("是否为动态摇杆（按下时将底盘移至手指触摸点）")]
        [SerializeField] private bool dynamicJoystick = false;

        private Vector2 _defaultBackgroundPosition;
        private Canvas _canvas;
        private static Sprite _cachedBaseCircleSprite;
        private static Sprite _cachedKnobCircleSprite;

        private void Awake()
        {
            InitializeReferences();
            EnsureCircularAppearance();
        }

        private void OnEnable()
        {
            InitializeReferences();
            EnsureCircularAppearance();
        }

        private void OnValidate()
        {
            InitializeReferences();
            EnsureCircularAppearance();
        }

        private void InitializeReferences()
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
            if (background != null && Application.isPlaying)
            {
                _defaultBackgroundPosition = background.anchoredPosition;
            }
        }

        /// <summary>
        /// 确保摇杆底座和中心圆钮始终渲染为平滑抗锯齿的正圆形
        /// </summary>
        public void EnsureCircularAppearance()
        {
            if (background != null)
            {
                Image bgImg = background.GetComponent<Image>();
                if (bgImg != null && (bgImg.sprite == null || bgImg.sprite.name == "UISprite"))
                {
                    if (_cachedBaseCircleSprite == null)
                    {
                        _cachedBaseCircleSprite = CreateAntiAliasedCircleSprite(256, true);
                    }
                    bgImg.sprite = _cachedBaseCircleSprite;
                    bgImg.type = Image.Type.Simple;
                    bgImg.preserveAspect = true;
                }
            }

            if (handle != null)
            {
                Image handleImg = handle.GetComponent<Image>();
                if (handleImg != null && (handleImg.sprite == null || handleImg.sprite.name == "UISprite"))
                {
                    if (_cachedKnobCircleSprite == null)
                    {
                        _cachedKnobCircleSprite = CreateAntiAliasedCircleSprite(128, false);
                    }
                    handleImg.sprite = _cachedKnobCircleSprite;
                    handleImg.type = Image.Type.Simple;
                    handleImg.preserveAspect = true;
                }
            }
        }

        private static Sprite CreateAntiAliasedCircleSprite(int size, bool withOuterRing)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = withOuterRing ? "ProceduralJoystickBaseCircle" : "ProceduralJoystickKnobCircle",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color[] pixels = new Color[size * size];
            float center = (size - 1) * 0.5f;
            float maxRadius = size * 0.48f;
            float aaWidth = 2.0f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float outerAlpha = Mathf.Clamp01((maxRadius - dist) / aaWidth);

                    if (outerAlpha <= 0f)
                    {
                        pixels[y * size + x] = Color.clear;
                    }
                    else if (withOuterRing)
                    {
                        // 外圈带高亮描边环，内部半透明磨砂圆盘
                        float ringInner = maxRadius - (size * 0.045f);
                        float ringFactor = Mathf.Clamp01((dist - ringInner) / aaWidth);
                        float brightness = Mathf.Lerp(0.25f, 0.95f, ringFactor);
                        float alpha = Mathf.Lerp(0.55f, 0.92f, ringFactor) * outerAlpha;
                        pixels[y * size + x] = new Color(brightness, brightness, brightness, alpha);
                    }
                    else
                    {
                        // 中心操纵圆钮：柔和立体圆形
                        float radialT = Mathf.Clamp01(dist / maxRadius);
                        float shade = Mathf.Lerp(1.0f, 0.88f, radialT * radialT);
                        pixels[y * size + x] = new Color(shade, shade, shade, outerAlpha);
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            Sprite sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            sp.name = tex.name;
            sp.hideFlags = HideFlags.HideAndDontSave;
            return sp;
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
                // 计算偏移向量并严格限制在圆形半径内
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
