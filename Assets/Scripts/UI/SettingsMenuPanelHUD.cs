using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace PixelSaur.UI
{
    /// <summary>
    /// 齿轮设置菜单面板组件：
    /// 1. 中间圆角 Panel 占比至少 60% 屏幕；
    /// 2. Panel 内部包含 3 个按钮（从上到下）：继续游戏、画面设置、游戏控制；
    /// 3. 所有圆角底板均使用 hud_pill_bg_64x64 九宫格切片精灵（GUID e4d5c6b7a8f90a1b2c3d4e5f6a7b8293）；
    /// 4. 动画效果：
    ///    - 打开：灰蒙蒙背景从下往上填充（Fill Vertical），中间 Panel 从小变大（Scale 0.2 -> 1.0）；
    ///    - 关闭：灰蒙蒙面板往下拉回到游戏（Slide Down Offscreen）。
    /// </summary>
    [ExecuteAlways]
    public class SettingsMenuPanelHUD : MonoBehaviour
    {
        public static SettingsMenuPanelHUD Instance { get; private set; }

        [Header("精灵资产 (hud_pill_bg_64x64)")]
        [Tooltip("九宫格圆角切片底板精灵 (GUID e4d5c6b7a8f90a1b2c3d4e5f6a7b8293)")]
        [SerializeField] private Sprite roundedPillSprite;

        [Header("尺寸与比例 (占比至少 60% 屏幕)")]
        [Range(0.6f, 0.9f)]
        [SerializeField] private float panelWidthRatio = 0.64f;
        [Range(0.6f, 0.9f)]
        [SerializeField] private float panelHeightRatio = 0.68f;

        [Header("颜色配置")]
        [SerializeField] private Color foggyOverlayColor = new Color(0.22f, 0.25f, 0.30f, 0.72f);
        [SerializeField] private Color panelBgColor = new Color(0.12f, 0.14f, 0.18f, 0.95f);
        [SerializeField] private Color resumeBtnColor = new Color(0.18f, 0.45f, 0.65f, 0.98f);
        [SerializeField] private Color buttonBgColor = new Color(0.20f, 0.24f, 0.32f, 0.98f);
        [SerializeField] private Color buttonHoverColor = new Color(0.28f, 0.36f, 0.48f, 1f);
        [SerializeField] private Color buttonPressedColor = new Color(0.14f, 0.55f, 0.82f, 1f);

        [Header("动画时长")]
        [SerializeField] private float openDuration = 0.35f;
        [SerializeField] private float closeDuration = 0.28f;

        [Header("按钮交互事件")]
        public UnityEvent onResumeClicked;
        public UnityEvent onGraphicsSettingsClicked;
        public UnityEvent onControlsClicked;

        [Header("节点与组件引用")]
        [SerializeField] private RectTransform rootRect;
        [SerializeField] private Image foggyBgImage;
        [SerializeField] private RectTransform centerPanelRect;
        [SerializeField] private Image centerPanelBg;
        [SerializeField] private CanvasGroup centerPanelCanvasGroup;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button graphicsButton;
        [SerializeField] private Button controlsButton;

        private Coroutine _animCoroutine;
        private bool _isOpen = false;

        public bool IsOpen => _isOpen;

        private void Awake()
        {
            if (Application.isPlaying)
            {
                if (Instance == null) Instance = this;
                else if (Instance != this) { Destroy(gameObject); return; }
            }

            LoadDefaultSprites();
            EnsureUIHierarchy();
        }

        private void OnEnable()
        {
            LoadDefaultSprites();
            EnsureUIHierarchy();
            BindButtonListeners();
        }

        private void OnDisable()
        {
            UnbindButtonListeners();
        }

        private void LoadDefaultSprites()
        {
#if UNITY_EDITOR
            if (roundedPillSprite == null)
            {
                roundedPillSprite = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd")
                                    ?? UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            }
#endif
        }

        public void EnsureUIHierarchy()
        {
            rootRect = GetComponent<RectTransform>();
            if (rootRect == null) rootRect = gameObject.AddComponent<RectTransform>();

            // 根节点：全屏拉伸
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            rootRect.pivot = new Vector2(0.5f, 0.5f);

            // 1. 灰蒙蒙背景 (从下往上填充 Image)
            Transform bgTr = transform.Find("FoggyBackground");
            GameObject bgObj = bgTr != null ? bgTr.gameObject : new GameObject("FoggyBackground", typeof(RectTransform), typeof(Image), typeof(Button));
            bgObj.transform.SetParent(transform, false);
            bgObj.transform.SetSiblingIndex(0);

            RectTransform bgRT = bgObj.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;

            foggyBgImage = bgObj.GetComponent<Image>();
            foggyBgImage.sprite = null;
            foggyBgImage.type = Image.Type.Filled;
            foggyBgImage.fillMethod = Image.FillMethod.Vertical;
            foggyBgImage.fillOrigin = (int)Image.OriginVertical.Bottom;
            foggyBgImage.fillAmount = _isOpen ? 1f : 0f;
            foggyBgImage.color = foggyOverlayColor;
            foggyBgImage.raycastTarget = true;

            // 点击背景也可顺畅关闭
            Button bgBtn = bgObj.GetComponent<Button>();
            bgBtn.targetGraphic = foggyBgImage;
            bgBtn.onClick.RemoveListener(CloseMenu);
            bgBtn.onClick.AddListener(CloseMenu);

            // 2. 中间圆角 Panel (占比至少 60% 屏幕)
            Transform panelTr = transform.Find("CenterPanel");
            GameObject panelObj = panelTr != null ? panelTr.gameObject : new GameObject("CenterPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            panelObj.transform.SetParent(transform, false);
            panelObj.transform.SetSiblingIndex(1);

            centerPanelRect = panelObj.GetComponent<RectTransform>();
            float marginX = (1f - panelWidthRatio) * 0.5f;
            float marginY = (1f - panelHeightRatio) * 0.5f;
            centerPanelRect.anchorMin = new Vector2(marginX, marginY);
            centerPanelRect.anchorMax = new Vector2(1f - marginX, 1f - marginY);
            centerPanelRect.offsetMin = Vector2.zero;
            centerPanelRect.offsetMax = Vector2.zero;
            centerPanelRect.pivot = new Vector2(0.5f, 0.5f);

            centerPanelBg = panelObj.GetComponent<Image>();
            centerPanelBg.sprite = roundedPillSprite;
            centerPanelBg.type = Image.Type.Sliced;
            centerPanelBg.color = panelBgColor;
            centerPanelBg.raycastTarget = true;

            centerPanelCanvasGroup = panelObj.GetComponent<CanvasGroup>();

            Font mainFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (mainFont == null) mainFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // 3. Panel 顶部标题 "游戏设置"
            Transform titleTr = panelTr.Find("TitleText");
            GameObject titleObj = titleTr != null ? titleTr.gameObject : new GameObject("TitleText", typeof(RectTransform), typeof(Text), typeof(Outline));
            titleObj.transform.SetParent(panelTr, false);

            RectTransform titleRT = titleObj.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0f, 1f);
            titleRT.anchorMax = new Vector2(1f, 1f);
            titleRT.pivot = new Vector2(0.5f, 1f);
            titleRT.anchoredPosition = new Vector2(0f, -32f);
            titleRT.sizeDelta = new Vector2(0f, 44f);

            Text titleText = titleObj.GetComponent<Text>();
            titleText.font = mainFont;
            titleText.fontSize = 28;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(0.92f, 0.95f, 1.0f, 1f);
            titleText.text = "游戏设置";
            titleText.raycastTarget = false;

            Outline titleOutline = titleObj.GetComponent<Outline>();
            titleOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            titleOutline.effectDistance = new Vector2(1.5f, -1.5f);

            // 分割线
            Transform divTr = panelTr.Find("Divider");
            GameObject divObj = divTr != null ? divTr.gameObject : new GameObject("Divider", typeof(RectTransform), typeof(Image));
            divObj.transform.SetParent(panelTr, false);
            RectTransform divRT = divObj.GetComponent<RectTransform>();
            divRT.anchorMin = new Vector2(0.1f, 1f);
            divRT.anchorMax = new Vector2(0.9f, 1f);
            divRT.pivot = new Vector2(0.5f, 1f);
            divRT.anchoredPosition = new Vector2(0f, -86f);
            divRT.sizeDelta = new Vector2(0f, 2f);
            Image divImg = divObj.GetComponent<Image>();
            divImg.color = new Color(0.40f, 0.46f, 0.58f, 0.45f);
            divImg.raycastTarget = false;

            // 4. 三个按钮的垂直布局容器
            Transform groupTr = panelTr.Find("ButtonGroup");
            GameObject groupObj = groupTr != null ? groupTr.gameObject : new GameObject("ButtonGroup", typeof(RectTransform), typeof(VerticalLayoutGroup));
            groupObj.transform.SetParent(panelTr, false);

            RectTransform groupRT = groupObj.GetComponent<RectTransform>();
            groupRT.anchorMin = new Vector2(0.5f, 0.5f);
            groupRT.anchorMax = new Vector2(0.5f, 0.5f);
            groupRT.pivot = new Vector2(0.5f, 0.5f);
            groupRT.anchoredPosition = new Vector2(0f, -36f);
            groupRT.sizeDelta = new Vector2(560f, 320f);

            VerticalLayoutGroup vlg = groupObj.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 24f;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = false;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;

            // 按钮 1: 继续游戏
            resumeButton = EnsureMenuButton(groupObj.transform, "ResumeButton", "继续游戏", mainFont, resumeBtnColor);
            // 按钮 2: 画面设置
            graphicsButton = EnsureMenuButton(groupObj.transform, "GraphicsButton", "画面设置", mainFont, buttonBgColor);
            // 按钮 3: 游戏控制
            controlsButton = EnsureMenuButton(groupObj.transform, "ControlsButton", "游戏控制", mainFont, buttonBgColor);

            if (!_isOpen)
            {
                if (centerPanelRect != null) centerPanelRect.localScale = Vector3.one;
                if (foggyBgImage != null) foggyBgImage.fillAmount = 0f;
            }
        }

        private Button EnsureMenuButton(Transform parent, string name, string label, Font font, Color baseColor)
        {
            Transform btnTr = parent.Find(name);
            GameObject btnObj = btnTr != null ? btnTr.gameObject : new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent, false);

            RectTransform btnRT = btnObj.GetComponent<RectTransform>();
            btnRT.sizeDelta = new Vector2(540f, 80f);

            Image img = btnObj.GetComponent<Image>();
            img.sprite = roundedPillSprite;
            img.type = Image.Type.Sliced;
            img.color = baseColor;
            img.raycastTarget = true;

            Button btn = btnObj.GetComponent<Button>();
            btn.targetGraphic = img;

            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = buttonHoverColor;
            cb.pressedColor = buttonPressedColor;
            cb.selectedColor = Color.white;
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.1f;
            btn.colors = cb;

            Transform txtTr = btnObj.transform.Find("Text");
            GameObject txtObj = txtTr != null ? txtTr.gameObject : new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(Outline));
            txtObj.transform.SetParent(btnObj.transform, false);

            RectTransform txtRT = txtObj.GetComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero;
            txtRT.anchorMax = Vector2.one;
            txtRT.offsetMin = Vector2.zero;
            txtRT.offsetMax = Vector2.zero;

            Text txt = txtObj.GetComponent<Text>();
            txt.font = font;
            txt.fontSize = 26;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.text = label;
            txt.raycastTarget = false;

            Outline outline = txtObj.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.90f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            return btn;
        }

        private void BindButtonListeners()
        {
            if (resumeButton != null)
            {
                resumeButton.onClick.RemoveListener(HandleResumeClick);
                resumeButton.onClick.AddListener(HandleResumeClick);
            }
            if (graphicsButton != null)
            {
                graphicsButton.onClick.RemoveListener(HandleGraphicsClick);
                graphicsButton.onClick.AddListener(HandleGraphicsClick);
            }
            if (controlsButton != null)
            {
                controlsButton.onClick.RemoveListener(HandleControlsClick);
                controlsButton.onClick.AddListener(HandleControlsClick);
            }
        }

        private void UnbindButtonListeners()
        {
            if (resumeButton != null) resumeButton.onClick.RemoveListener(HandleResumeClick);
            if (graphicsButton != null) graphicsButton.onClick.RemoveListener(HandleGraphicsClick);
            if (controlsButton != null) controlsButton.onClick.RemoveListener(HandleControlsClick);
        }

        private void HandleResumeClick()
        {
            Debug.Log("<color=#66CCFF>[SettingsMenuPanelHUD] 点击【继续游戏】，面板往下拉回到游戏。</color>");
            onResumeClicked?.Invoke();
            CloseMenu();
        }

        private void HandleGraphicsClick()
        {
            Debug.Log("<color=#66CCFF>[SettingsMenuPanelHUD] 点击【画面设置】。</color>");
            onGraphicsSettingsClicked?.Invoke();
        }

        private void HandleControlsClick()
        {
            Debug.Log("<color=#66CCFF>[SettingsMenuPanelHUD] 点击【游戏控制】。</color>");
            onControlsClicked?.Invoke();
        }

        /// <summary>
        /// 切换菜单显示/隐藏
        /// </summary>
        public void ToggleMenu()
        {
            if (_isOpen)
            {
                CloseMenu();
            }
            else
            {
                OpenMenu();
            }
        }

        /// <summary>
        /// 打开菜单：背景从下往上填充，Panel 从小变大弹性放大
        /// </summary>
        public void OpenMenu()
        {
            if (Instance == null) Instance = this;
            LoadDefaultSprites();
            EnsureUIHierarchy();
            BindButtonListeners();

            if (_animCoroutine != null) StopCoroutine(_animCoroutine);
            gameObject.SetActive(true);
            if (Application.isPlaying)
            {
                _animCoroutine = StartCoroutine(AnimateOpen());
            }
            else
            {
                _isOpen = true;
                if (rootRect != null) rootRect.anchoredPosition = Vector2.zero;
                if (foggyBgImage != null) foggyBgImage.fillAmount = 1f;
                if (centerPanelRect != null) centerPanelRect.localScale = Vector3.one;
                if (centerPanelCanvasGroup != null) centerPanelCanvasGroup.alpha = 1f;
                TopBarStatusHUD.Instance?.SetGearActive(true);
            }
        }

        /// <summary>
        /// 关闭菜单：面板往下拉回到游戏
        /// </summary>
        public void CloseMenu()
        {
            if (_animCoroutine != null) StopCoroutine(_animCoroutine);
            if (Application.isPlaying)
            {
                _animCoroutine = StartCoroutine(AnimateClose());
            }
            else
            {
                _isOpen = false;
                if (rootRect != null) rootRect.anchoredPosition = Vector2.zero;
                if (foggyBgImage != null) foggyBgImage.fillAmount = 0f;
                gameObject.SetActive(false);
                TopBarStatusHUD.Instance?.SetGearActive(false);
            }
        }

        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3) + c1 * Mathf.Pow(x - 1f, 2);
        }

        private static float EaseInCubic(float x)
        {
            return x * x * x;
        }

        private IEnumerator AnimateOpen()
        {
            _isOpen = true;
            gameObject.SetActive(true);
            if (rootRect != null) rootRect.anchoredPosition = Vector2.zero;

            if (centerPanelCanvasGroup != null) centerPanelCanvasGroup.alpha = 0f;
            if (centerPanelRect != null) centerPanelRect.localScale = new Vector3(0.2f, 0.2f, 1f);
            if (foggyBgImage != null)
            {
                foggyBgImage.type = Image.Type.Filled;
                foggyBgImage.fillMethod = Image.FillMethod.Vertical;
                foggyBgImage.fillOrigin = (int)Image.OriginVertical.Bottom;
                foggyBgImage.fillAmount = 0f;
            }

            TopBarStatusHUD.Instance?.SetGearActive(true);

            float elapsed = 0f;
            while (elapsed < openDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / openDuration);

                // 1. 灰蒙蒙背景从下往上填充
                if (foggyBgImage != null)
                {
                    foggyBgImage.fillAmount = Mathf.SmoothStep(0f, 1f, t);
                }

                // 2. Panel 从小变大（带自然微弹性）
                if (centerPanelRect != null)
                {
                    float s = Mathf.LerpUnclamped(0.2f, 1.0f, EaseOutBack(t));
                    centerPanelRect.localScale = new Vector3(s, s, 1f);
                }

                if (centerPanelCanvasGroup != null)
                {
                    centerPanelCanvasGroup.alpha = Mathf.Clamp01(t * 3f);
                }

                yield return null;
            }

            if (foggyBgImage != null) foggyBgImage.fillAmount = 1f;
            if (centerPanelRect != null) centerPanelRect.localScale = Vector3.one;
            if (centerPanelCanvasGroup != null) centerPanelCanvasGroup.alpha = 1f;
            _animCoroutine = null;
        }

        private IEnumerator AnimateClose()
        {
            _isOpen = false;
            TopBarStatusHUD.Instance?.SetGearActive(false);

            float canvasHeight = 1080f;
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                RectTransform cRT = canvas.GetComponent<RectTransform>();
                if (cRT != null && cRT.rect.height > 0)
                {
                    canvasHeight = cRT.rect.height;
                }
            }
            float targetY = -canvasHeight - 150f;

            float elapsed = 0f;
            while (elapsed < closeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / closeDuration);
                float easeT = EaseInCubic(t);

                // 灰蒙蒙面板往下拉回到游戏
                if (rootRect != null)
                {
                    rootRect.anchoredPosition = new Vector2(0f, Mathf.Lerp(0f, targetY, easeT));
                }

                yield return null;
            }

            if (rootRect != null) rootRect.anchoredPosition = Vector2.zero;
            if (foggyBgImage != null) foggyBgImage.fillAmount = 0f;
            gameObject.SetActive(false);
            _animCoroutine = null;
        }
    }
}
