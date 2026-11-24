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

        [Header("扇形疾跑与自动奔跑锁定配置 (CODM / 和平精英手感)")]
        [Tooltip("上方圆形疾跑视觉反馈按钮")]
        [SerializeField] private RectTransform sprintButton;
        [SerializeField] private Image sprintButtonBg;
        [SerializeField] private Image sprintButtonIcon;
        [Tooltip("32*32 像素翼龙奔跑图标资产")]
        [SerializeField] private Sprite pterosaurRunSprite;
        [Tooltip("疾跑图标在摇杆正上方的垂直距离（像素）")]
        [Range(100f, 360f)]
        [SerializeField] private float sprintButtonYOffset = 160f;
        [Tooltip("与正上方(Vector2.up)的最大夹角（度）：左右各偏35°共70°扇形区")]
        [Range(5f, 90f)]
        [SerializeField] private float sprintAngleLimit = 35f;
        [Tooltip("进入疾跑所需的推力比例阈值（0.85 表示 85%）")]
        [Range(0.5f, 1.0f)]
        [SerializeField] private float sprintMagnitude = 0.85f;
        [Tooltip("上滑超出外圈触发自动奔跑锁定的推力比例阈值（默认 1.25）")]
        [Range(1.05f, 2.5f)]
        [SerializeField] private float lockMagnitude = 1.25f;
        [Tooltip("平时/非疾跑时的默认暗色半透明颜色")]
        [SerializeField] private Color sprintNormalColor = new Color(0.10f, 0.14f, 0.20f, 0.72f);
        [Tooltip("进入疾跑或自动奔跑锁定时的黄色高亮颜色")]
        [SerializeField] private Color sprintActiveColor = Color.yellow;

        [Header("锁定奔跑按钮配置（父节点为 SprintButton，严格同 X 轴仅调 Y 偏移）")]
        [Tooltip("SprintButton 上方的奔跑锁定图标容器（直接挂在 SprintButton 下）")]
        [SerializeField] private RectTransform sprintLockButton;
        [SerializeField] private Image sprintLockBg;
        [SerializeField] private Image sprintLockIcon;
        [Tooltip("32*32 白色锁图标资产")]
        [SerializeField] private Sprite sprintLockSprite;
        [Tooltip("锁定奔跑按钮相对 SprintButton 的垂直偏移（仅 Y 轴，X 恒为 0）")]
        [SerializeField] private float sprintLockYOffset = 116f;
        [Tooltip("锁定奔跑按钮默认尺寸（略小于 110x110 的 SprintButton）")]
        [SerializeField] private Vector2 sprintLockSize = new Vector2(92f, 92f);
        [Tooltip("手指滑到 SprintLockButton 触发锁定的半径宽松系数")]
        [Range(0.8f, 1.8f)]
        [SerializeField] private float lockHitRadiusMultiplier = 1.2f;
        [Tooltip("未锁定时锁定按钮默认底色")]
        [SerializeField] private Color lockNormalColor = new Color(0.10f, 0.14f, 0.20f, 0.72f);
        [Tooltip("锁定奔跑状态时锁定按钮高亮变色反馈")]
        [SerializeField] private Color lockActiveColor = new Color(1.0f, 0.84f, 0.12f, 0.96f);

        private Vector2 _defaultBackgroundPosition;
        private bool _hasRecordedDefaultPos;
        private bool _hasCustomSprintLayout;
        private Vector2 _customSprintCanvasPos = new Vector2(335f, 563f);
        private Vector2 _customSprintSize = new Vector2(110f, 110f);
        private int _activePointerId = int.MinValue;
        private Canvas _canvas;
        private bool _isAutoRunPendingLock;
        private static Sprite _cachedBaseCircleSprite;
        private static Sprite _cachedKnobCircleSprite;
        private static Sprite _cachedFallbackLockSprite;

        public bool IsSprinting { get; private set; }
        public bool IsAutoRunLocked { get; private set; }
        public Color SprintActiveColor => sprintActiveColor;
        public RectTransform SprintButtonRect => sprintButton;
        public RectTransform SprintLockButtonRect => sprintLockButton;
        public Image SprintLockBgImage => sprintLockBg;

        public bool DynamicJoystick
        {
            get => dynamicJoystick;
            set
            {
                dynamicJoystick = value;
                UpdateDynamicTouchZone();
            }
        }

        private RectTransform _dynamicTouchZone;

        private void Awake()
        {
            InitializeReferences();
            EnsureCircularAppearance();
        }

        private void Start()
        {
            if (Application.isPlaying)
            {
                if (PlayerPrefs.HasKey("SETTING_CTRL_DYNAMIC_JOYSTICK"))
                {
                    dynamicJoystick = PlayerPrefs.GetInt("SETTING_CTRL_DYNAMIC_JOYSTICK", 0) == 1;
                }
                UpdateDynamicTouchZone();
            }

            if (Application.isPlaying && sprintButton != null)
            {
                sprintButton.gameObject.SetActive(true);
                UpdateSprintButtonVisual(false);
            }
        }

        private void Update()
        {
            if (!Application.isPlaying) return;

            if (IsAutoRunLocked)
            {
                IsSprinting = true;
                if (PlayerInputProxy.Instance != null)
                {
                    PlayerInputProxy.Instance.SetJoystickInput(Vector2.up);
                    PlayerInputProxy.Instance.SetSprint(true);
                }
                UpdateSprintButtonVisual(true);
            }
        }

        private void OnEnable()
        {
            InitializeReferences();
            EnsureCircularAppearance();
        }

        private void OnValidate()
        {
            InitializeReferences();
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
            if (background != null && Application.isPlaying && !_hasRecordedDefaultPos)
            {
                _defaultBackgroundPosition = background.anchoredPosition;
                _hasRecordedDefaultPos = true;
            }
        }

        /// <summary>
        /// 确保摇杆底座和中心圆钮始终渲染为平滑抗锯齿的正圆形
        /// </summary>
        private void EnsureSprintButtonHierarchy()
        {
#if UNITY_EDITOR
            if (pterosaurRunSprite == null)
            {
                pterosaurRunSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_pterosaur_run_32x32.png");
            }
            if (sprintLockSprite == null)
            {
                sprintLockSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_sprint_lock_32x32.png");
            }
#endif
            Transform sprintTr = transform.Find("SprintButton");
            GameObject sprintObj = sprintTr != null ? sprintTr.gameObject : new GameObject("SprintButton", typeof(RectTransform));
            sprintButton = sprintObj.GetComponent<RectTransform>();
            sprintButton.SetParent(transform, false);
            sprintButton.anchorMin = new Vector2(0.5f, 0.5f);
            sprintButton.anchorMax = new Vector2(0.5f, 0.5f);
            sprintButton.pivot = new Vector2(0.5f, 0.5f);
            sprintButton.localScale = Vector3.one;

            float baseDim = background != null && background.sizeDelta.x > 10f ? background.sizeDelta.x : 220f;
            float ratio = baseDim / 220f;
            if (_hasCustomSprintLayout)
            {
                Vector2 joyPos = background != null ? background.anchoredPosition : new Vector2(335f, 330f);
                sprintButton.anchoredPosition = _customSprintCanvasPos - joyPos;
                sprintButton.sizeDelta = _customSprintSize;
            }
            else
            {
                sprintButtonYOffset = Mathf.Clamp(160f * ratio, 100f, 360f);
                sprintButton.anchoredPosition = new Vector2(0f, sprintButtonYOffset);
                sprintButton.sizeDelta = new Vector2(76f * ratio, 76f * ratio);
            }

            sprintButtonBg = sprintObj.GetComponent<Image>();
            if (sprintButtonBg == null) sprintButtonBg = sprintObj.AddComponent<Image>();
            if (_cachedBaseCircleSprite == null) _cachedBaseCircleSprite = CreateAntiAliasedCircleSprite(256, true);
            sprintButtonBg.sprite = _cachedBaseCircleSprite;
            sprintButtonBg.color = (IsSprinting || IsAutoRunLocked) ? sprintActiveColor : sprintNormalColor;
            sprintButtonBg.raycastTarget = false;

            float sprintIconDim = Mathf.Clamp(sprintButton.sizeDelta.x * 0.48f, 24f, 110f);
            Transform iconTr = sprintObj.transform.Find("Icon");
            GameObject iconObj = iconTr != null ? iconTr.gameObject : new GameObject("Icon", typeof(RectTransform), typeof(Image));
            RectTransform iconRT = iconObj.GetComponent<RectTransform>();
            iconRT.SetParent(sprintButton, false);
            iconRT.anchorMin = new Vector2(0.5f, 0.5f);
            iconRT.anchorMax = new Vector2(0.5f, 0.5f);
            iconRT.pivot = new Vector2(0.5f, 0.5f);
            iconRT.anchoredPosition = Vector2.zero;
            iconRT.sizeDelta = new Vector2(sprintIconDim, sprintIconDim);

            sprintButtonIcon = iconObj.GetComponent<Image>();
            sprintButtonIcon.sprite = pterosaurRunSprite;
            sprintButtonIcon.color = (IsSprinting || IsAutoRunLocked) ? new Color(0.12f, 0.12f, 0.12f, 1f) : new Color(1f, 1f, 1f, 0.78f);
            sprintButtonIcon.preserveAspect = true;
            sprintButtonIcon.raycastTarget = false;

            sprintButton.gameObject.SetActive(true);

            // 在 SprintButton 正上方生成奔跑锁定按钮（严格作为 SprintButton 的子物体，X 固定为 0，仅可调 Y）
            EnsureSprintLockHierarchy();
        }

        private void EnsureSprintLockHierarchy()
        {
            if (sprintButton == null) return;

            Transform lockTr = sprintButton.Find("SprintLockButton");
            GameObject lockObj = lockTr != null ? lockTr.gameObject : new GameObject("SprintLockButton", typeof(RectTransform));
            sprintLockButton = lockObj.GetComponent<RectTransform>();
            sprintLockButton.SetParent(sprintButton, false);
            sprintLockButton.anchorMin = new Vector2(0.5f, 0.5f);
            sprintLockButton.anchorMax = new Vector2(0.5f, 0.5f);
            sprintLockButton.pivot = new Vector2(0.5f, 0.5f);
            sprintLockButton.localScale = Vector3.one;
            sprintLockButton.anchoredPosition = new Vector2(0f, sprintLockYOffset);
            float lockDim = Mathf.Clamp(Mathf.Max(sprintLockSize.x, sprintLockSize.y), 52f, 180f);
            sprintLockButton.sizeDelta = new Vector2(lockDim, lockDim);

            sprintLockBg = lockObj.GetComponent<Image>();
            if (sprintLockBg == null) sprintLockBg = lockObj.AddComponent<Image>();
            if (_cachedBaseCircleSprite == null) _cachedBaseCircleSprite = CreateAntiAliasedCircleSprite(256, true);
            sprintLockBg.sprite = _cachedBaseCircleSprite;
            sprintLockBg.raycastTarget = false;

            Transform lockIconTr = lockObj.transform.Find("LockIcon");
            GameObject lockIconObj = lockIconTr != null ? lockIconTr.gameObject : new GameObject("LockIcon", typeof(RectTransform), typeof(Image));
            RectTransform lockIconRT = lockIconObj.GetComponent<RectTransform>();
            lockIconRT.SetParent(sprintLockButton, false);
            lockIconRT.anchorMin = new Vector2(0.5f, 0.5f);
            lockIconRT.anchorMax = new Vector2(0.5f, 0.5f);
            lockIconRT.pivot = new Vector2(0.5f, 0.5f);
            lockIconRT.anchoredPosition = Vector2.zero;
            // 32x32 白色像素锁图标按容器尺寸居中清晰展示（Point 滤波保持像素锐利）
            float iconSize = Mathf.Clamp(lockDim * 0.50f, 24f, 96f);
            lockIconRT.sizeDelta = new Vector2(iconSize, iconSize);
            lockIconRT.localScale = Vector3.one;

            sprintLockIcon = lockIconObj.GetComponent<Image>();
            sprintLockIcon.sprite = sprintLockSprite != null ? sprintLockSprite : GetFallbackWhiteLockSprite();
            sprintLockIcon.type = Image.Type.Simple;
            sprintLockIcon.preserveAspect = true;
            sprintLockIcon.raycastTarget = false;

            sprintLockButton.gameObject.SetActive(true);
            UpdateSprintLockVisual(_isAutoRunPendingLock || IsAutoRunLocked);
        }

        public static Sprite GetFallbackWhiteLockSprite()
        {
            if (_cachedFallbackLockSprite != null) return _cachedFallbackLockSprite;
            const int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "ProceduralWhiteLock32",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color[] px = new Color[size * size];
            for (int i = 0; i < px.Length; i++) px[i] = Color.clear;

            for (int y = 4; y <= 17; y++)
            {
                for (int x = 6; x <= 25; x++)
                {
                    bool corner = (x == 6 || x == 25) && (y == 4 || y == 17);
                    if (!corner) px[y * size + x] = Color.white;
                }
            }
            for (int y = 6; y <= 15; y++)
            {
                for (int x = 12; x <= 19; x++)
                {
                    float dx = (x + 0.5f) - 16f;
                    float dy = (y + 0.5f) - 12.5f;
                    bool inHead = (dx * dx + dy * dy) <= 2.3f * 2.3f;
                    bool inStem = (x == 15 || x == 16) && (y >= 7 && y <= 11);
                    if (inHead || inStem) px[y * size + x] = Color.clear;
                }
            }
            for (int y = 17; y <= 29; y++)
            {
                for (int x = 8; x <= 23; x++)
                {
                    float cx = x + 0.5f;
                    float cy = y + 0.5f;
                    if (cy >= 21f)
                    {
                        float r = Mathf.Sqrt((cx - 16f) * (cx - 16f) + (cy - 21f) * (cy - 21f));
                        if (r >= 4.0f && r <= 7.2f) px[y * size + x] = Color.white;
                    }
                    else if ((x >= 9 && x <= 11) || (x >= 20 && x <= 22))
                    {
                        px[y * size + x] = Color.white;
                    }
                }
            }

            tex.SetPixels(px);
            tex.Apply();
            _cachedFallbackLockSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            _cachedFallbackLockSprite.hideFlags = HideFlags.HideAndDontSave;
            return _cachedFallbackLockSprite;
        }

        public void SetSprintActiveColor(Color color)
        {
            sprintActiveColor = color;
            UpdateSprintButtonVisual(IsSprinting || IsAutoRunLocked);
        }

        private void UpdateSprintButtonVisual(bool highlighted)
        {
            if (sprintButton != null && !sprintButton.gameObject.activeSelf)
            {
                sprintButton.gameObject.SetActive(true);
            }
            if (sprintButtonBg != null)
            {
                if (_cachedBaseCircleSprite == null) _cachedBaseCircleSprite = CreateAntiAliasedCircleSprite(256, true);
                if (_cachedKnobCircleSprite == null) _cachedKnobCircleSprite = CreateAntiAliasedCircleSprite(128, false);
                sprintButtonBg.sprite = highlighted ? _cachedKnobCircleSprite : _cachedBaseCircleSprite;
                sprintButtonBg.color = highlighted ? sprintActiveColor : sprintNormalColor;
            }
            if (sprintButtonIcon != null)
            {
                sprintButtonIcon.color = highlighted ? new Color(0.12f, 0.12f, 0.12f, 1f) : new Color(1f, 1f, 1f, 0.78f);
            }
            UpdateSprintLockVisual(_isAutoRunPendingLock || IsAutoRunLocked);
        }

        private void UpdateSprintLockVisual(bool locked)
        {
            if (sprintLockButton != null && !sprintLockButton.gameObject.activeSelf)
            {
                sprintLockButton.gameObject.SetActive(true);
            }
            if (sprintLockBg != null)
            {
                if (_cachedBaseCircleSprite == null) _cachedBaseCircleSprite = CreateAntiAliasedCircleSprite(256, true);
                if (_cachedKnobCircleSprite == null) _cachedKnobCircleSprite = CreateAntiAliasedCircleSprite(128, false);
                sprintLockBg.sprite = locked ? _cachedKnobCircleSprite : _cachedBaseCircleSprite;
                sprintLockBg.color = locked ? lockActiveColor : lockNormalColor;
            }
            if (sprintLockIcon != null)
            {
                sprintLockIcon.color = locked ? new Color(0.10f, 0.10f, 0.10f, 1f) : new Color(1f, 1f, 1f, 0.94f);
                sprintLockIcon.rectTransform.localScale = locked ? Vector3.one * 1.12f : Vector3.one;
            }
        }

        public void ApplySprintButtonLayout(Vector2 canvasAnchoredPos, Vector2 size)
        {
            InitializeReferences();
            _hasCustomSprintLayout = true;
            _customSprintCanvasPos = canvasAnchoredPos;
            float dim = Mathf.Clamp(Mathf.Max(size.x, size.y), 64f, 220f);
            _customSprintSize = new Vector2(dim, dim);
            EnsureSprintButtonHierarchy();
        }

        public void ApplySprintLockLayout(float localOffsetY, Vector2 size)
        {
            InitializeReferences();
            sprintLockYOffset = localOffsetY;
            float dim = Mathf.Clamp(Mathf.Max(size.x, size.y), 52f, 180f);
            sprintLockSize = new Vector2(dim, dim);
            EnsureSprintLockHierarchy();
        }

        public void CancelAutoRunLock()
        {
            IsAutoRunLocked = false;
            _isAutoRunPendingLock = false;
            IsSprinting = false;
            UpdateSprintButtonVisual(false);
        }

        public void ApplyCustomLayout(Vector2 anchoredPos, Vector2 size)
        {
            InitializeReferences();
            if (background == null) return;

            float dim = Mathf.Max(100f, Mathf.Max(size.x, size.y));
            background.anchorMin = new Vector2(0f, 0f);
            background.anchorMax = new Vector2(0f, 0f);
            background.pivot = new Vector2(0.5f, 0.5f);
            background.localScale = Vector3.one;
            background.anchoredPosition = anchoredPos;
            background.sizeDelta = new Vector2(dim, dim);

            _defaultBackgroundPosition = anchoredPos;
            _hasRecordedDefaultPos = true;

            float ratio = dim / 220f;
            handleRange = Mathf.Clamp(75f * ratio, 36f, 220f);
            if (handle != null)
            {
                handle.localScale = Vector3.one;
                handle.sizeDelta = new Vector2(88f * ratio, 88f * ratio);
            }

            EnsureSprintButtonHierarchy();
        }

        public void EnsureCircularAppearance()
        {
            EnsureSprintButtonHierarchy();

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
            if (_activePointerId != int.MinValue && _activePointerId != eventData.pointerId) return;
            _activePointerId = eventData.pointerId;

            // 再次触碰摇杆时立即解除自动奔跑锁定
            if (IsAutoRunLocked)
            {
                CancelAutoRunLock();
            }

            if (dynamicJoystick && background != null)
            {
                MoveBackgroundToScreenPosition(eventData.position, eventData.pressEventCamera);
            }

            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (background == null || handle == null) return;
            if (_activePointerId != int.MinValue && eventData.pointerId != _activePointerId) return;

            if (IsAutoRunLocked)
            {
                CancelAutoRunLock();
            }

            Camera cam = _canvas != null && _canvas.renderMode == RenderMode.ScreenSpaceCamera ? _canvas.worldCamera : null;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(background, eventData.position, cam, out Vector2 position))
            {
                ProcessDragLocalPosition(position);
            }
        }

        /// <summary>
        /// 计算摇杆拖动位置是否真正滑到了 SprintLockButton 锁定区（支持布局编辑器自由移动后的动态实时位置）
        /// </summary>
        private bool IsPointerOnSprintLockButton(Vector2 joystickLocalPos)
        {
            if (background == null || sprintLockButton == null || !sprintLockButton.gameObject.activeInHierarchy)
            {
                return false;
            }

            Vector3 lockWorldPos = sprintLockButton.position;
            Vector3 lockInBg = background.InverseTransformPoint(lockWorldPos);
            Vector2 lockLocalPos = new Vector2(lockInBg.x, lockInBg.y);

            float lockRadius = Mathf.Max(26f, sprintLockButton.sizeDelta.x * 0.5f) * Mathf.Clamp(lockHitRadiusMultiplier, 0.8f, 1.8f);
            float distToLock = Vector2.Distance(joystickLocalPos, lockLocalPos);
            if (distToLock <= lockRadius)
            {
                return true;
            }

            // 若手指沿同 X 轴继续上滑越过锁图标顶部，同样保持在锁定区
            bool passedTopInColumn = joystickLocalPos.y >= lockLocalPos.y - lockRadius * 0.25f
                && Mathf.Abs(joystickLocalPos.x - lockLocalPos.x) <= lockRadius * 1.15f;
            return passedTopInColumn;
        }

        /// <summary>
        /// 基于摇杆本地偏移向量执行扇形疾跑，且必须手指滑到 SprintLockButton 区域才触发自动奔跑锁定
        /// </summary>
        public void ProcessDragLocalPosition(Vector2 position)
        {
            float safeRange = Mathf.Max(1f, handleRange);
            float rawRatio = position.magnitude / safeRange;

            // 计算限幅后的摇杆旋钮位置与归一化输入向量
            Vector2 clampedPos = Vector2.ClampMagnitude(position, safeRange);
            Vector2 normalizedInput = clampedPos / safeRange;
            if (normalizedInput.magnitude < deadZone)
            {
                normalizedInput = Vector2.zero;
            }

            // 1. 扇形疾跑判定：与正上方(Vector2.up)夹角 <= sprintAngleLimit (35°)，且推力 >= sprintMagnitude (0.85)
            float angleFromUp = position.sqrMagnitude > 0.001f ? Vector2.Angle(Vector2.up, position) : 180f;
            bool inSprintCone = normalizedInput.sqrMagnitude > 0.001f && angleFromUp <= sprintAngleLimit;

            // 2. 奔跑锁定判定：必须手指从摇杆滑到 SprintLockButton 所在区域才锁定；滑到 SprintButton 仅疾跑不锁定
            bool onLockButton = IsPointerOnSprintLockButton(position);

            if (onLockButton)
            {
                IsSprinting = true;
                _isAutoRunPendingLock = true;
                if (handle != null)
                {
                    handle.anchoredPosition = new Vector2(0f, safeRange);
                }
                normalizedInput = Vector2.up;
            }
            else if (inSprintCone && rawRatio >= sprintMagnitude)
            {
                // 滑到 SprintButton / 扇形疾跑区：仅疾跑，不触发自动锁定
                IsSprinting = true;
                _isAutoRunPendingLock = false;
                if (handle != null) handle.anchoredPosition = clampedPos;
            }
            else
            {
                IsSprinting = false;
                _isAutoRunPendingLock = false;
                if (handle != null) handle.anchoredPosition = clampedPos;
            }

            // 3. 疾跑按钮与锁按钮分别更新视觉反馈（锁按钮仅在进入锁定区/已锁定时变色高亮）
            UpdateSprintButtonVisual(IsSprinting || _isAutoRunPendingLock || IsAutoRunLocked);

            if (PlayerInputProxy.Instance != null)
            {
                PlayerInputProxy.Instance.SetJoystickInput(normalizedInput);
                PlayerInputProxy.Instance.SetSprint(IsSprinting);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_activePointerId != int.MinValue && eventData.pointerId != _activePointerId) return;
            _activePointerId = int.MinValue;

            if (_isAutoRunPendingLock)
            {
                // 松手触发自动奔跑锁定：保持持续疾跑向前移动，摇杆旋钮停在顶部外缘不遮挡疾跑/锁按钮
                _isAutoRunPendingLock = false;
                IsAutoRunLocked = true;
                IsSprinting = true;

                if (dynamicJoystick && background != null)
                {
                    background.anchoredPosition = _defaultBackgroundPosition;
                }
                if (handle != null)
                {
                    handle.anchoredPosition = new Vector2(0f, handleRange);
                }

                UpdateSprintButtonVisual(true);

                if (PlayerInputProxy.Instance != null)
                {
                    PlayerInputProxy.Instance.SetJoystickInput(Vector2.up);
                    PlayerInputProxy.Instance.SetSprint(true);
                }
                return;
            }

            IsAutoRunLocked = false;
            IsSprinting = false;
            UpdateSprintButtonVisual(false);

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
                PlayerInputProxy.Instance.SetSprint(false);
            }
        }

        /// <summary>
        /// 当开启动态摇杆时，在屏幕左半区建立全透明接收层（元气骑士机制：玩家按压左屏任何位置均吸附移动）
        /// </summary>
        public void UpdateDynamicTouchZone()
        {
            if (!Application.isPlaying) return;

            Transform zoneTr = transform.parent != null ? transform.parent.Find("DynamicJoystickLeftZone") : null;
            if (!dynamicJoystick)
            {
                if (zoneTr != null) zoneTr.gameObject.SetActive(false);
                return;
            }

            GameObject zoneObj;
            if (zoneTr == null)
            {
                zoneObj = new GameObject("DynamicJoystickLeftZone", typeof(RectTransform), typeof(Image), typeof(DynamicJoystickZoneProxy));
                zoneObj.transform.SetParent(transform.parent, false);
            }
            else
            {
                zoneObj = zoneTr.gameObject;
            }

            // 确保位于 TouchLookArea 之上、摇杆及其他 HUD 按钮之下，防止右屏视角区抢占左半区触控
            Transform lookTr = transform.parent.Find("TouchLookArea");
            if (lookTr != null && transform.GetSiblingIndex() < lookTr.GetSiblingIndex())
            {
                transform.SetSiblingIndex(lookTr.GetSiblingIndex() + 1);
            }
            zoneObj.transform.SetSiblingIndex(transform.GetSiblingIndex());

            RectTransform rt = zoneObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0.5f, 1f); // 覆盖左半侧屏幕
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Image img = zoneObj.GetComponent<Image>();
            img.color = Color.clear;
            img.raycastTarget = true;

            var proxy = zoneObj.GetComponent<DynamicJoystickZoneProxy>();
            if (proxy == null) proxy = zoneObj.AddComponent<DynamicJoystickZoneProxy>();
            proxy.joystick = this;

            zoneObj.SetActive(true);
            _dynamicTouchZone = rt;
        }

        public void HandleDynamicPointerDown(PointerEventData eventData)
        {
            if (_activePointerId != int.MinValue && _activePointerId != eventData.pointerId) return;
            _activePointerId = eventData.pointerId;

            if (IsAutoRunLocked)
            {
                CancelAutoRunLock();
            }

            if (background != null)
            {
                MoveBackgroundToScreenPosition(eventData.position, eventData.pressEventCamera);
            }
            OnDrag(eventData);
        }

        private void MoveBackgroundToScreenPosition(Vector2 screenPosition, Camera pressCamera)
        {
            if (background == null) return;
            Camera cam = _canvas != null && _canvas.renderMode == RenderMode.ScreenSpaceCamera ? _canvas.worldCamera : pressCamera;
            if (_canvas != null && _canvas.renderMode == RenderMode.ScreenSpaceOverlay) cam = null;

            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(background, screenPosition, cam, out Vector3 worldPos))
            {
                background.position = worldPos;
            }
        }
    }

    /// <summary>
    /// 动态摇杆左屏触控转发代理
    /// </summary>
    public class DynamicJoystickZoneProxy : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public VirtualJoystick joystick;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (joystick != null) joystick.HandleDynamicPointerDown(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (joystick != null) joystick.OnDrag(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (joystick != null) joystick.OnPointerUp(eventData);
        }
    }
}
