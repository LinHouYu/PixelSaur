using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PixelSaur.Cam;
using PixelSaur.Player;

namespace PixelSaur.UI
{
    /// <summary>
    /// 自由视角“小眼睛”按钮（参考《和平精英》）：
    /// 1. 外层半透明圆形底框固定在 TopBarStatusHUD 右侧（左上角锚点对齐，防止不同分辨率错位或遮挡）；
    /// 2. 恐龙眼睛图标（eyes.png）作为居中子物体，按住拖动时像虚拟摇杆中心球一样跟随手指偏移并受 ClampMagnitude 限制；
    /// 3. 图标到达最大偏移边界后，继续拖动依然根据未截断的滑动增量持续旋转摄像机视角，且绝不改变恐龙移动方向或打断疾跑/自动奔跑；
    /// 4. 松手后眼睛图标通过平滑插值迅速回弹居中，摄像机视角平滑回正到恐龙正后方。
    /// </summary>
    [ExecuteAlways]
    public class FreeLookEyeButton : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IEndDragHandler
    {
        public static FreeLookEyeButton Instance { get; private set; }

        [Header("小眼睛图标资产 (eyes.png)")]
        [SerializeField] private Sprite eyeIconSprite;

        [Header("位置与尺寸（默认位于右上角小地图左下方）")]
        [SerializeField] private Vector2 buttonPosition = new Vector2(-204f, -294f);
        [SerializeField] private Vector2 buttonSize = new Vector2(80f, 80f);

        [Header("自由视角参数配置")]
        [Tooltip("小眼睛拖动旋转摄像机灵敏度")]
        [Range(0.05f, 2.0f)]
        [SerializeField] private float eyeCameraSensitivity = 0.35f;

        [Tooltip("松开小眼睛后摄像机平滑回正到恐龙正后方的速度")]
        [Range(1f, 30f)]
        [SerializeField] private float cameraResetSpeed = 8f;

        [SerializeField] private bool invertX = false;
        [SerializeField] private bool invertY = false;

        [Header("图标拖拽摇杆反馈配置")]
        [Tooltip("最大偏移半径比例（基于眼睛图标宽度，默认 0.5 即图标宽度的一半）")]
        [Range(0.1f, 1.5f)]
        [SerializeField] private float maxDragRadiusRatio = 0.5f;

        [Tooltip("手动指定最大偏移半径（像素）；设为 0 时自动使用 眼睛图标宽度 * maxDragRadiusRatio")]
        [Min(0f)]
        [SerializeField] private float maxDragRadius = 0f;

        [Tooltip("松手后眼睛图标平滑回弹至中心点 (0, 0) 的插值速度")]
        [Range(4f, 40f)]
        [SerializeField] private float iconReturnSpeed = 18f;

        [Header("视觉反馈配置")]
        [SerializeField] private Color normalBgColor = new Color(0.12f, 0.14f, 0.18f, 0.86f);
        [SerializeField] private Color activeBgColor = new Color(0.95f, 0.82f, 0.18f, 0.94f);
        [SerializeField] private float pressScale = 0.95f;

        [Header("内部节点引用")]
        [SerializeField] private Image bgImage;
        [SerializeField] private Image iconImage;

        private RectTransform _rectTransform;
        private RectTransform _iconRect;
        private Canvas _canvas;
        private int _activePointerId = int.MinValue;
        private Vector2 _lastPointerPos;
        private Vector2 _pointerDownLocalPos;
        private bool _isDragging;
        private static Sprite _cachedCircleBgSprite;

        public bool IsHoldingEye => _isDragging;
        public float EyeCameraSensitivity => eyeCameraSensitivity;
        public float CameraResetSpeed => cameraResetSpeed;
        public Vector2 CurrentIconOffset => _iconRect != null ? _iconRect.anchoredPosition : Vector2.zero;
        public float EffectiveMaxDragRadius
        {
            get
            {
                if (maxDragRadius > 0.01f) return maxDragRadius;
                float iconW = _iconRect != null && _iconRect.rect.width > 1f
                    ? _iconRect.rect.width
                    : buttonSize.x * 0.76f;
                return iconW * maxDragRadiusRatio;
            }
        }

        private void Awake()
        {
            if (Application.isPlaying) Instance = this;
            LoadDefaultSprite();
            EnsureUIHierarchy();
        }

        private void OnEnable()
        {
            if (Application.isPlaying) Instance = this;
            LoadDefaultSprite();
            EnsureUIHierarchy();
        }

        private void OnValidate()
        {
            LoadDefaultSprite();
            EnsureUIHierarchy();
        }

        private void OnDisable()
        {
            if (_isDragging)
            {
                _isDragging = false;
                _activePointerId = int.MinValue;
                ThirdPersonOrbitCamera.Instance?.EndFreeLook(cameraResetSpeed);
                ApplyVisualState(false);
            }
            if (_iconRect != null)
            {
                _iconRect.anchoredPosition = Vector2.zero;
            }
        }

        private void Update()
        {
            if (!Application.isPlaying) return;

            // 松手后通过平滑插值迅速回弹至中心点 (0, 0)
            if (!_isDragging && _iconRect != null && _iconRect.anchoredPosition.sqrMagnitude > 0.001f)
            {
                float t = 1f - Mathf.Exp(-iconReturnSpeed * Time.unscaledDeltaTime);
                Vector2 next = Vector2.Lerp(_iconRect.anchoredPosition, Vector2.zero, t);
                if (next.sqrMagnitude <= 0.04f)
                {
                    next = Vector2.zero;
                }
                _iconRect.anchoredPosition = next;
            }
        }

        private void LoadDefaultSprite()
        {
#if UNITY_EDITOR
            if (eyeIconSprite == null)
            {
                eyeIconSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/eyes.png");
            }
#endif
        }

        public void SetIconSprite(Sprite sprite)
        {
            eyeIconSprite = sprite;
            EnsureUIHierarchy();
        }

        public void ApplyCustomLayout(Vector2 anchoredPos, Vector2 size)
        {
            buttonPosition = anchoredPos;
            buttonSize = new Vector2(Mathf.Max(48f, size.x), Mathf.Max(48f, size.y));
            EnsureUIHierarchy();
        }

        public void EnsureUIHierarchy()
        {
            _rectTransform = GetComponent<RectTransform>();
            if (_rectTransform == null) _rectTransform = gameObject.AddComponent<RectTransform>();
            _canvas = GetComponentInParent<Canvas>();

            // 外层圆形半透明底框固定不动，使用右上角锚点紧贴小地图左下正下方
            _rectTransform.anchorMin = new Vector2(1f, 1f);
            _rectTransform.anchorMax = new Vector2(1f, 1f);
            _rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _rectTransform.localScale = Vector3.one;
            _rectTransform.anchoredPosition = buttonPosition;
            _rectTransform.sizeDelta = buttonSize;

            if (_cachedCircleBgSprite == null)
            {
                _cachedCircleBgSprite = CreateCircleBgSprite(128);
            }

            bgImage = GetComponent<Image>();
            if (bgImage == null) bgImage = gameObject.AddComponent<Image>();
            bgImage.sprite = _cachedCircleBgSprite;
            bgImage.type = Image.Type.Simple;
            bgImage.preserveAspect = true;
            bgImage.color = _isDragging ? activeBgColor : normalBgColor;
            bgImage.raycastTarget = true;

            // 恐龙眼睛图标作为居中子物体（anchoredPosition = Vector2.zero），支持摇杆中心球式拖拽偏移
            Transform iconTr = transform.Find("EyeIcon");
            GameObject iconObj = iconTr != null ? iconTr.gameObject : new GameObject("EyeIcon", typeof(RectTransform), typeof(Image));
            iconObj.transform.SetParent(transform, false);

            _iconRect = iconObj.GetComponent<RectTransform>();
            _iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            _iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            _iconRect.pivot = new Vector2(0.5f, 0.5f);
            _iconRect.sizeDelta = buttonSize * 0.76f;
            if (!_isDragging)
            {
                _iconRect.anchoredPosition = Vector2.zero;
            }
            _iconRect.localScale = Vector3.one;

            iconImage = iconObj.GetComponent<Image>();
            if (eyeIconSprite != null) iconImage.sprite = eyeIconSprite;
            iconImage.type = Image.Type.Simple;
            iconImage.preserveAspect = true;
            iconImage.color = Color.white;
            iconImage.raycastTarget = false;
        }

        private void ApplyVisualState(bool active)
        {
            // 外层底框保持固定不动，仅高亮颜色并对子物体图标做轻微按压缩放反馈
            if (bgImage != null)
            {
                bgImage.color = active ? activeBgColor : normalBgColor;
            }
            if (_iconRect != null)
            {
                _iconRect.localScale = active ? Vector3.one * pressScale : Vector3.one;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_activePointerId != int.MinValue && _activePointerId != eventData.pointerId) return;
            _activePointerId = eventData.pointerId;
            _lastPointerPos = eventData.position;
            _isDragging = true;

            Camera uiCam = _canvas != null && _canvas.renderMode == RenderMode.ScreenSpaceCamera ? _canvas.worldCamera : null;
            if (_rectTransform != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(_rectTransform, eventData.position, uiCam, out Vector2 localDown))
            {
                _pointerDownLocalPos = localDown;
            }
            else
            {
                _pointerDownLocalPos = Vector2.zero;
            }

            ApplyVisualState(true);

            var cam = ThirdPersonOrbitCamera.Instance;
            if (cam != null)
            {
                cam.CameraResetSpeed = cameraResetSpeed;
                cam.BeginFreeLook();
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isDragging || eventData.pointerId != _activePointerId) return;

            // 1. 眼睛图标摇杆式视觉偏移（严格使用 ClampMagnitude 限制最大半径为图标宽度一半，不影响下方视角旋转）
            Camera uiCam = _canvas != null && _canvas.renderMode == RenderMode.ScreenSpaceCamera ? _canvas.worldCamera : null;
            if (_rectTransform != null && _iconRect != null &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_rectTransform, eventData.position, uiCam, out Vector2 localCurrent))
            {
                Vector2 rawOffset = localCurrent - _pointerDownLocalPos;
                _iconRect.anchoredPosition = Vector2.ClampMagnitude(rawOffset, EffectiveMaxDragRadius);
            }

            // 2. 摄像机视角持续旋转：直接使用未受限的滑动增量（图标到边界卡住后继续滑动依然持续旋转）
            Vector2 currentPos = eventData.position;
            Vector2 delta = eventData.delta.sqrMagnitude > 0.0001f ? eventData.delta : (currentPos - _lastPointerPos);
            _lastPointerPos = currentPos;

            if (invertX) delta.x = -delta.x;
            if (invertY) delta.y = -delta.y;

            float sensMult = PlayerInputProxy.Instance != null
                ? Mathf.Clamp(PlayerInputProxy.Instance.LookSensitivityMultiplier, 0.2f, 2.5f)
                : 1.0f;

            Vector2 scaledDelta = delta * eyeCameraSensitivity * sensMult;
            ThirdPersonOrbitCamera.Instance?.AddFreeLookDelta(scaledDelta);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            FinishDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            FinishDrag(eventData);
        }

        private void FinishDrag(PointerEventData eventData)
        {
            if (eventData != null && _activePointerId != int.MinValue && eventData.pointerId != _activePointerId) return;
            if (!_isDragging && _activePointerId == int.MinValue) return;

            _activePointerId = int.MinValue;
            _isDragging = false;

            ApplyVisualState(false);
            ThirdPersonOrbitCamera.Instance?.EndFreeLook(cameraResetSpeed);
        }

        private static Sprite CreateCircleBgSprite(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "FreeLookEyeCircleBg",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color[] px = new Color[size * size];
            float c = (size - 1) * 0.5f;
            float r = size * 0.48f;
            float aa = 1.8f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
                    float outer = Mathf.Clamp01((r - d) / aa);
                    if (outer <= 0f)
                    {
                        px[y * size + x] = Color.clear;
                    }
                    else
                    {
                        float ringInner = r - size * 0.06f;
                        float ring = Mathf.Clamp01((d - ringInner) / aa);
                        float br = Mathf.Lerp(0.75f, 1.0f, ring);
                        px[y * size + x] = new Color(br, br, br, outer);
                    }
                }
            }

            tex.SetPixels(px);
            tex.Apply();
            Sprite sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            sp.hideFlags = HideFlags.HideAndDontSave;
            return sp;
        }
    }
}
