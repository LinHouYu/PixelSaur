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

        [Header("奔跑按钮配置 (PUBG/COD手游上推奔跑)")]
        [Tooltip("上方圆形奔跑按钮")]
        [SerializeField] private RectTransform sprintButton;
        [SerializeField] private Image sprintButtonBg;
        [SerializeField] private Image sprintButtonIcon;
        [Tooltip("32*32 像素翼龙奔跑图标资产")]
        [SerializeField] private Sprite pterosaurRunSprite;
        [Tooltip("奔跑按钮在摇杆正上方的垂直距离（像素）")]
        [Range(100f, 250f)]
        [SerializeField] private float sprintButtonYOffset = 160f;
        [Tooltip("手指滑动到奔跑按钮的吸附判定半径")]
        [Range(20f, 80f)]
        [SerializeField] private float sprintSnapDistance = 48f;
        [SerializeField] private Color sprintNormalColor = new Color(0.10f, 0.14f, 0.20f, 0.75f);
        [SerializeField] private Color sprintActiveColor = new Color(0.18f, 0.75f, 0.95f, 0.92f);

        private Vector2 _defaultBackgroundPosition;
        private Canvas _canvas;
        private static Sprite _cachedBaseCircleSprite;
        private static Sprite _cachedKnobCircleSprite;

        public bool IsSprinting { get; private set; }

        private void Awake()
        {
            InitializeReferences();
            EnsureCircularAppearance();
        }

        private void Start()
        {
            if (Application.isPlaying && sprintButton != null)
            {
                sprintButton.gameObject.SetActive(false);
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
        private void EnsureSprintButtonHierarchy()
        {
#if UNITY_EDITOR
            if (pterosaurRunSprite == null)
            {
                pterosaurRunSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_pterosaur_run_32x32.png");
            }
#endif
            Transform sprintTr = transform.Find("SprintButton");
            GameObject sprintObj = sprintTr != null ? sprintTr.gameObject : new GameObject("SprintButton", typeof(RectTransform));
            sprintButton = sprintObj.GetComponent<RectTransform>();
            sprintButton.SetParent(transform, false);
            sprintButton.anchorMin = new Vector2(0.5f, 0.5f);
            sprintButton.anchorMax = new Vector2(0.5f, 0.5f);
            sprintButton.pivot = new Vector2(0.5f, 0.5f);
            sprintButton.anchoredPosition = new Vector2(0f, sprintButtonYOffset);
            sprintButton.sizeDelta = new Vector2(76f, 76f);

            sprintButtonBg = sprintObj.GetComponent<Image>();
            if (sprintButtonBg == null) sprintButtonBg = sprintObj.AddComponent<Image>();
            if (_cachedBaseCircleSprite == null) _cachedBaseCircleSprite = CreateAntiAliasedCircleSprite(256, true);
            sprintButtonBg.sprite = _cachedBaseCircleSprite;
            sprintButtonBg.color = sprintNormalColor;
            sprintButtonBg.raycastTarget = false;

            Transform iconTr = sprintObj.transform.Find("Icon");
            GameObject iconObj = iconTr != null ? iconTr.gameObject : new GameObject("Icon", typeof(RectTransform), typeof(Image));
            RectTransform iconRT = iconObj.GetComponent<RectTransform>();
            iconRT.SetParent(sprintButton, false);
            iconRT.anchorMin = new Vector2(0.5f, 0.5f);
            iconRT.anchorMax = new Vector2(0.5f, 0.5f);
            iconRT.pivot = new Vector2(0.5f, 0.5f);
            iconRT.anchoredPosition = Vector2.zero;
            iconRT.sizeDelta = new Vector2(36f, 36f);

            sprintButtonIcon = iconObj.GetComponent<Image>();
            sprintButtonIcon.sprite = pterosaurRunSprite;
            sprintButtonIcon.color = Color.white;
            sprintButtonIcon.preserveAspect = true;
            sprintButtonIcon.raycastTarget = false;

            if (Application.isPlaying)
            {
                sprintButton.gameObject.SetActive(false);
            }
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
                // 计算偏移向量并限制在摇杆范围内
                Vector2 clampedPos = Vector2.ClampMagnitude(position, handleRange);

                // 归一化输入值 (-1 ~ 1)
                Vector2 normalizedInput = clampedPos / handleRange;
                if (normalizedInput.magnitude < deadZone)
                {
                    normalizedInput = Vector2.zero;
                }

                // 奔跑按钮逻辑 (手指向上推进进入奔跑区域)
                if (sprintButton != null)
                {
                    // 仅当玩家有移动输入时显示奔跑按钮，静止时不显示
                    if (normalizedInput.sqrMagnitude > 0.01f)
                    {
                        if (!sprintButton.gameObject.activeSelf) sprintButton.gameObject.SetActive(true);

                        float distToSprint = Vector2.Distance(position, sprintButton.anchoredPosition);
                        if (position.y > handleRange * 0.75f && distToSprint <= sprintSnapDistance)
                        {
                            IsSprinting = true;
                            handle.anchoredPosition = sprintButton.anchoredPosition;
                            if (sprintButtonBg != null) sprintButtonBg.color = sprintActiveColor;
                            normalizedInput = Vector2.up; // 向上全速狂奔
                        }
                        else
                        {
                            IsSprinting = false;
                            handle.anchoredPosition = clampedPos;
                            if (sprintButtonBg != null) sprintButtonBg.color = sprintNormalColor;
                        }
                    }
                    else
                    {
                        IsSprinting = false;
                        handle.anchoredPosition = clampedPos;
                        if (sprintButton.gameObject.activeSelf) sprintButton.gameObject.SetActive(false);
                    }
                }
                else
                {
                    handle.anchoredPosition = clampedPos;
                }

                if (PlayerInputProxy.Instance != null)
                {
                    PlayerInputProxy.Instance.SetJoystickInput(normalizedInput);
                    PlayerInputProxy.Instance.SetSprint(IsSprinting);
                }
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            IsSprinting = false;
            if (sprintButton != null)
            {
                sprintButton.gameObject.SetActive(false);
                if (sprintButtonBg != null) sprintButtonBg.color = sprintNormalColor;
            }

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
    }
}
