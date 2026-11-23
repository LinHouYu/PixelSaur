using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PixelSaur.Cam;
using PixelSaur.Player;

namespace PixelSaur.UI
{
    /// <summary>
    /// 自由视角“小眼睛”按钮（参考《和平精英》）：
    /// 1. 置于 TopBarStatusHUD 右侧（左上角锚点对齐，防止不同分辨率错位或遮挡）；
    /// 2. 使用 32x32 像素恐龙眼睛图标 eyes.png（Point 过滤保持像素清晰）；
    /// 3. 按住并拖动仅旋转摄像机视角（Yaw/Pitch）观察四周，绝不改变恐龙移动方向，也不打断疾跑或自动奔跑锁定；
    /// 4. 松开后摄像机平滑插值回正到恐龙正后方。
    /// </summary>
    [ExecuteAlways]
    public class FreeLookEyeButton : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public static FreeLookEyeButton Instance { get; private set; }

        [Header("小眼睛图标资产 (eyes.png)")]
        [SerializeField] private Sprite eyeIconSprite;

        [Header("位置与尺寸（默认位于 TopBarStatusHUD 右侧）")]
        [SerializeField] private Vector2 buttonPosition = new Vector2(442f, -54f);
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

        [Header("视觉反馈配置")]
        [SerializeField] private Color normalBgColor = new Color(0.12f, 0.14f, 0.18f, 0.86f);
        [SerializeField] private Color activeBgColor = new Color(0.95f, 0.82f, 0.18f, 0.94f);
        [SerializeField] private float pressScale = 0.92f;

        [Header("内部节点引用")]
        [SerializeField] private Image bgImage;
        [SerializeField] private Image iconImage;

        private RectTransform _rectTransform;
        private int _activePointerId = int.MinValue;
        private Vector2 _lastPointerPos;
        private bool _isDragging;
        private static Sprite _cachedCircleBgSprite;

        public bool IsHoldingEye => _isDragging;
        public float EyeCameraSensitivity => eyeCameraSensitivity;
        public float CameraResetSpeed => cameraResetSpeed;

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

            // 与 TopBarStatusHUD 一致使用左上角锚点，避免不同屏幕纵横比错位或遮挡
            _rectTransform.anchorMin = new Vector2(0f, 1f);
            _rectTransform.anchorMax = new Vector2(0f, 1f);
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

            Transform iconTr = transform.Find("EyeIcon");
            GameObject iconObj = iconTr != null ? iconTr.gameObject : new GameObject("EyeIcon", typeof(RectTransform), typeof(Image));
            iconObj.transform.SetParent(transform, false);

            RectTransform iconRT = iconObj.GetComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0.12f, 0.12f);
            iconRT.anchorMax = new Vector2(0.88f, 0.88f);
            iconRT.offsetMin = Vector2.zero;
            iconRT.offsetMax = Vector2.zero;
            iconRT.localScale = Vector3.one;

            iconImage = iconObj.GetComponent<Image>();
            if (eyeIconSprite != null) iconImage.sprite = eyeIconSprite;
            iconImage.type = Image.Type.Simple;
            iconImage.preserveAspect = true;
            iconImage.color = Color.white;
            iconImage.raycastTarget = false;
        }

        private void ApplyVisualState(bool active)
        {
            transform.localScale = active ? Vector3.one * pressScale : Vector3.one;
            if (bgImage != null)
            {
                bgImage.color = active ? activeBgColor : normalBgColor;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_activePointerId != int.MinValue && _activePointerId != eventData.pointerId) return;
            _activePointerId = eventData.pointerId;
            _lastPointerPos = eventData.position;
            _isDragging = true;

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

            Vector2 currentPos = eventData.position;
            Vector2 delta = currentPos - _lastPointerPos;
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
            if (eventData.pointerId != _activePointerId) return;
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
