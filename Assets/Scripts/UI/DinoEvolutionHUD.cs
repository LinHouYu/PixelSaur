using UnityEngine;
using UnityEngine.UI;
using PixelSaur.Player;

namespace PixelSaur.UI
{
    /// <summary>
    /// 恐龙进化系统主界面入口按钮 + 2D 基础进化弹窗 UI（与 TopBarStatusHUD / SettingsMenuPanel 风格高度统一）：
    /// 1. 右上角主界面入口按钮：左侧 upgrade.png 图标 + 右侧“升级系统”文字，与 TopBarStatusHUD 相同的深灰半透明圆角底框；
    ///    经验攒满可进化时自动高亮呼吸闪烁并显示角标提示；下方紧跟轻量级实时阶段/经验条。
    /// 2. 点击入口打开 2D 进化弹窗：正中大号字体按阶段颜色（白/绿/蓝/紫）展示当前阶段，显示进阶路线与经验进度，
    ///    底部提供手动【进化】按钮（未满置灰不可点，满值高亮可点；点击立即关窗并在场景原地触发 3 秒进化演出）。
    /// </summary>
    [ExecuteAlways]
    public class DinoEvolutionHUD : MonoBehaviour
    {
        public static DinoEvolutionHUD Instance { get; private set; }

        [Header("主界面入口位置与尺寸（与 TopBarStatusHUD 同高 68px，风格一致）")]
        [SerializeField] private Vector2 entryPosition = new Vector2(-24f, -20f);
        [SerializeField] private Vector2 entrySize = new Vector2(248f, 68f);

        [Header("图标与圆角底板资产")]
        [SerializeField] private Sprite upgradeIconSprite;
        [SerializeField] private Sprite panelBgSprite;

        [Header("入口与常驻条 UI 引用")]
        [SerializeField] private Button entryButton;
        [SerializeField] private Image entryBgImage;
        [SerializeField] private Image entryIconImage;
        [SerializeField] private Text entryLabelText;
        [SerializeField] private GameObject readyBadgeObj;
        [SerializeField] private Text miniStageText;
        [SerializeField] private Slider miniExpSlider;
        [SerializeField] private Image miniExpFillImage;
        [SerializeField] private Text miniExpText;

        [Header("进化弹窗 UI 引用")]
        [SerializeField] private GameObject modalOverlayObj;
        [SerializeField] private Button modalBackdropButton;
        [SerializeField] private Button modalCloseButton;
        [SerializeField] private Text modalStageTitleText;
        [SerializeField] private Text modalRouteText;
        [SerializeField] private Slider modalExpSlider;
        [SerializeField] private Image modalExpFillImage;
        [SerializeField] private Text modalExpText;
        [SerializeField] private Button modalEvolveButton;
        [SerializeField] private Image modalEvolveButtonBg;
        [SerializeField] private Text modalEvolveButtonText;

        private static readonly Color DefaultPillColor = new Color(0.12f, 0.13f, 0.16f, 0.90f);
        private static readonly Color ReadyPulseColor = new Color(0.88f, 0.68f, 0.14f, 0.96f);
        private static readonly Color DisabledBtnColor = new Color(0.24f, 0.26f, 0.30f, 0.88f);

        private DinoEvolutionController _boundController;
        private RectTransform _entryRect;
        private static Sprite _whiteSprite;
        private static Sprite _roundedRectSprite;

        public bool IsModalOpen => modalOverlayObj != null && modalOverlayObj.activeSelf;

        private void Awake()
        {
            if (Application.isPlaying) Instance = this;
            LoadDefaultSprites();
            EnsureUIHierarchy();
        }

        private void OnEnable()
        {
            if (Application.isPlaying) Instance = this;
            LoadDefaultSprites();
            EnsureUIHierarchy();
            BindListeners();
            TryBindController();
            RefreshView();
        }

        private void OnDisable()
        {
            UnbindListeners();
            UnbindController();
        }

        private void Update()
        {
            if (!Application.isPlaying) return;

            if (_boundController == null)
            {
                TryBindController();
                if (_boundController != null) RefreshView();
            }

            // 经验满可进化时，主界面【升级系统】入口按钮轻微呼吸高亮闪烁提示
            if (_boundController != null && _boundController.CanEvolve)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5.2f);
                if (entryBgImage != null)
                {
                    if (_roundedRectSprite != null && entryBgImage.sprite != _roundedRectSprite)
                    {
                        entryBgImage.sprite = _roundedRectSprite;
                        entryBgImage.type = Image.Type.Sliced;
                    }
                    Color targetHighlight = Color.Lerp(ReadyPulseColor, _boundController.NextStage.stageColor, 0.25f);
                    entryBgImage.color = Color.Lerp(new Color(0.28f, 0.24f, 0.10f, 0.94f), targetHighlight, pulse);
                }
                if (_entryRect != null)
                {
                    float s = Mathf.Lerp(1.0f, 1.045f, pulse);
                    _entryRect.localScale = new Vector3(s, s, 1f);
                }
            }
            else
            {
                if (entryBgImage != null)
                {
                    if (panelBgSprite != null && entryBgImage.sprite != panelBgSprite)
                    {
                        entryBgImage.sprite = panelBgSprite;
                        entryBgImage.type = Image.Type.Sliced;
                    }
                    if (entryBgImage.color != DefaultPillColor)
                    {
                        entryBgImage.color = DefaultPillColor;
                    }
                }
                if (_entryRect != null && _entryRect.localScale != Vector3.one)
                {
                    _entryRect.localScale = Vector3.one;
                }
            }
        }

        private void TryBindController()
        {
            if (_boundController != null) return;
            _boundController = DinoEvolutionController.Instance != null
                ? DinoEvolutionController.Instance
                : FindAnyObjectByType<DinoEvolutionController>();

            if (_boundController != null)
            {
                _boundController.OnEvolutionStateChanged -= RefreshView;
                _boundController.OnEvolutionStateChanged += RefreshView;
            }
        }

        private void UnbindController()
        {
            if (_boundController != null)
            {
                _boundController.OnEvolutionStateChanged -= RefreshView;
                _boundController = null;
            }
        }

        private void LoadDefaultSprites()
        {
#if UNITY_EDITOR
            if (upgradeIconSprite == null)
            {
                upgradeIconSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/upgrade.png");
            }
            if (panelBgSprite == null)
            {
                panelBgSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/hud_pill_bg_64x64.png");
            }
#endif
            if (_whiteSprite == null)
            {
                Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                Color[] px = new Color[16];
                for (int i = 0; i < px.Length; i++) px[i] = Color.white;
                tex.SetPixels(px);
                tex.Apply();
                _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
                _whiteSprite.hideFlags = HideFlags.HideAndDontSave;
            }
            if (_roundedRectSprite == null)
            {
                const int size = 32;
                const float r = 8f;
                Texture2D rtex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                Color[] pixels = new Color[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float cx = x + 0.5f;
                        float cy = y + 0.5f;
                        float dx = Mathf.Max(0f, Mathf.Max(r - cx, cx - (size - r)));
                        float dy = Mathf.Max(0f, Mathf.Max(r - cy, cy - (size - r)));
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);
                        float alpha = Mathf.Clamp01(r - dist + 0.5f);
                        pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                    }
                }
                rtex.SetPixels(pixels);
                rtex.Apply();
                _roundedRectSprite = Sprite.Create(
                    rtex,
                    new Rect(0, 0, size, size),
                    new Vector2(0.5f, 0.5f),
                    100f,
                    0,
                    SpriteMeshType.FullRect,
                    new Vector4(10f, 10f, 10f, 10f));
                _roundedRectSprite.hideFlags = HideFlags.HideAndDontSave;
            }
        }

        public void EnsureUIHierarchy()
        {
            // 根节点改为全屏透明容器，方便右上入口按钮与居中弹窗统一管理且不互相裁剪
            RectTransform rootRT = GetComponent<RectTransform>();
            if (rootRT == null) rootRT = gameObject.AddComponent<RectTransform>();
            rootRT.anchorMin = Vector2.zero;
            rootRT.anchorMax = Vector2.one;
            rootRT.offsetMin = Vector2.zero;
            rootRT.offsetMax = Vector2.zero;
            rootRT.localScale = Vector3.one;

            // 若旧版根节点上挂有底板 Image，仅置完全透明不删组件，防止整屏变黑
            Image rootImg = GetComponent<Image>();
            if (rootImg != null)
            {
                rootImg.color = Color.clear;
                rootImg.raycastTarget = false;
            }

            // 清理旧版直接子级（如旧版 StageNameText / ExpSlider / ScaleBadgeText）
            string[] legacyNames = { "StageNameText", "ScaleBadgeText", "ExpSlider" };
            foreach (string ln in legacyNames)
            {
                Transform old = transform.Find(ln);
                if (old != null)
                {
                    if (Application.isPlaying) Destroy(old.gameObject);
                    else DestroyImmediate(old.gameObject);
                }
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            BuildTopRightEntryAndMiniBar(font);
            BuildEvolutionModalPopup(font);
        }

        private void BuildTopRightEntryAndMiniBar(Font font)
        {
            // 1. 主界面右上角【升级系统】入口按钮（与 TopBarStatusHUD 同款圆角深灰胶囊样式）
            Transform entryTr = transform.Find("UpgradeEntryButton");
            GameObject entryGo = entryTr != null ? entryTr.gameObject : new GameObject("UpgradeEntryButton", typeof(RectTransform), typeof(Image), typeof(Button));
            entryGo.transform.SetParent(transform, false);

            _entryRect = entryGo.GetComponent<RectTransform>();
            _entryRect.anchorMin = new Vector2(1f, 1f);
            _entryRect.anchorMax = new Vector2(1f, 1f);
            _entryRect.pivot = new Vector2(1f, 1f);
            _entryRect.anchoredPosition = entryPosition;
            _entryRect.sizeDelta = entrySize;
            _entryRect.localScale = Vector3.one;

            entryBgImage = entryGo.GetComponent<Image>();
            if (panelBgSprite != null)
            {
                entryBgImage.sprite = panelBgSprite;
                entryBgImage.type = Image.Type.Sliced;
            }
            entryBgImage.color = DefaultPillColor;
            entryBgImage.raycastTarget = true;

            entryButton = entryGo.GetComponent<Button>();
            entryButton.targetGraphic = entryBgImage;
            ColorBlock cb = entryButton.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            cb.pressedColor = new Color(0.82f, 0.88f, 0.96f, 1f);
            cb.selectedColor = Color.white;
            entryButton.colors = cb;

            // 左侧 upgrade.png 图标
            Transform iconTr = entryGo.transform.Find("UpgradeIcon");
            GameObject iconGo = iconTr != null ? iconTr.gameObject : new GameObject("UpgradeIcon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(entryGo.transform, false);
            RectTransform iconRT = iconGo.GetComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0f, 0.5f);
            iconRT.anchorMax = new Vector2(0f, 0.5f);
            iconRT.pivot = new Vector2(0f, 0.5f);
            iconRT.anchoredPosition = new Vector2(12f, 0f);
            iconRT.sizeDelta = new Vector2(48f, 48f);
            iconRT.localScale = Vector3.one;

            entryIconImage = iconGo.GetComponent<Image>();
            if (upgradeIconSprite != null) entryIconImage.sprite = upgradeIconSprite;
            entryIconImage.type = Image.Type.Simple;
            entryIconImage.preserveAspect = true;
            entryIconImage.color = Color.white;
            entryIconImage.raycastTarget = false;

            // 右侧 "升级系统" 文字
            entryLabelText = EnsureText("EntryLabel", entryGo.transform, font, 26, TextAnchor.MiddleLeft,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(72f, 0f), new Vector2(-14f, 0f), "升级系统", Color.white);

            // 可进化高亮角标
            Transform badgeTr = entryGo.transform.Find("ReadyBadge");
            GameObject badgeGo = badgeTr != null ? badgeTr.gameObject : new GameObject("ReadyBadge", typeof(RectTransform), typeof(Image));
            badgeGo.transform.SetParent(entryGo.transform, false);
            RectTransform badgeRT = badgeGo.GetComponent<RectTransform>();
            badgeRT.anchorMin = new Vector2(0f, 1f);
            badgeRT.anchorMax = new Vector2(0f, 1f);
            badgeRT.pivot = new Vector2(0.5f, 0.5f);
            badgeRT.anchoredPosition = new Vector2(8f, -4f);
            badgeRT.sizeDelta = new Vector2(82f, 26f);
            Image badgeImg = badgeGo.GetComponent<Image>();
            badgeImg.sprite = _roundedRectSprite != null ? _roundedRectSprite : _whiteSprite;
            badgeImg.type = _roundedRectSprite != null ? Image.Type.Sliced : Image.Type.Simple;
            badgeImg.color = new Color(0.96f, 0.64f, 0.08f, 1f);
            badgeImg.raycastTarget = false;
            EnsureText("BadgeText", badgeGo.transform, font, 15, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, "可进化!", Color.white);
            readyBadgeObj = badgeGo;
            readyBadgeObj.SetActive(false);

            // 2. 入口下方紧凑的阶段/经验实时状态条（同样可点击打开弹窗）
            Transform stripTr = transform.Find("MiniEvolutionStrip");
            GameObject stripGo = stripTr != null ? stripTr.gameObject : new GameObject("MiniEvolutionStrip", typeof(RectTransform), typeof(Image), typeof(Button));
            stripGo.transform.SetParent(transform, false);
            RectTransform stripRT = stripGo.GetComponent<RectTransform>();
            stripRT.anchorMin = new Vector2(1f, 1f);
            stripRT.anchorMax = new Vector2(1f, 1f);
            stripRT.pivot = new Vector2(1f, 1f);
            stripRT.anchoredPosition = new Vector2(entryPosition.x, entryPosition.y - entrySize.y - 8f);
            stripRT.sizeDelta = new Vector2(entrySize.x, 48f);
            stripRT.localScale = Vector3.one;

            Image stripBg = stripGo.GetComponent<Image>();
            if (panelBgSprite != null)
            {
                stripBg.sprite = panelBgSprite;
                stripBg.type = Image.Type.Sliced;
            }
            stripBg.color = DefaultPillColor;
            stripBg.raycastTarget = true;

            Button stripBtn = stripGo.GetComponent<Button>();
            stripBtn.targetGraphic = stripBg;
            stripBtn.onClick.RemoveListener(OpenModal);
            stripBtn.onClick.AddListener(OpenModal);

            miniStageText = EnsureText("MiniStageText", stripGo.transform, font, 16, TextAnchor.MiddleLeft,
                new Vector2(0f, 0.52f), new Vector2(1f, 1f), new Vector2(12f, 0f), new Vector2(-12f, -2f), "幼年期 · 1.0x", Color.white);

            CreateProgressSlider("MiniExpSlider", stripGo.transform, font,
                new Vector2(0f, 0f), new Vector2(1f, 0.50f), new Vector2(12f, 6f), new Vector2(-12f, -2f), 13,
                out miniExpSlider, out miniExpFillImage, out miniExpText);
        }

        private void BuildEvolutionModalPopup(Font font)
        {
            Transform modalTr = transform.Find("EvolutionModalOverlay");
            GameObject overlayGo = modalTr != null ? modalTr.gameObject : new GameObject("EvolutionModalOverlay", typeof(RectTransform), typeof(Image), typeof(Button));
            overlayGo.transform.SetParent(transform, false);
            RectTransform overlayRT = overlayGo.GetComponent<RectTransform>();
            overlayRT.anchorMin = Vector2.zero;
            overlayRT.anchorMax = Vector2.one;
            overlayRT.offsetMin = Vector2.zero;
            overlayRT.offsetMax = Vector2.zero;
            overlayRT.localScale = Vector3.one;

            Image maskImg = overlayGo.GetComponent<Image>();
            maskImg.sprite = null;
            maskImg.color = new Color(0.08f, 0.10f, 0.14f, 0.72f);
            maskImg.raycastTarget = true;

            modalBackdropButton = overlayGo.GetComponent<Button>();
            modalBackdropButton.targetGraphic = maskImg;
            modalBackdropButton.transition = Selectable.Transition.None;

            // 中央 2D 圆角深灰底框（与 SettingsMenuPanel 视觉风格统一）
            Transform cardTr = overlayGo.transform.Find("EvolutionCard");
            GameObject cardGo = cardTr != null ? cardTr.gameObject : new GameObject("EvolutionCard", typeof(RectTransform), typeof(Image));
            cardGo.transform.SetParent(overlayGo.transform, false);
            RectTransform cardRT = cardGo.GetComponent<RectTransform>();
            cardRT.anchorMin = new Vector2(0.5f, 0.5f);
            cardRT.anchorMax = new Vector2(0.5f, 0.5f);
            cardRT.pivot = new Vector2(0.5f, 0.5f);
            cardRT.anchoredPosition = Vector2.zero;
            cardRT.sizeDelta = new Vector2(780f, 540f);
            cardRT.localScale = Vector3.one;

            Image cardBg = cardGo.GetComponent<Image>();
            if (_roundedRectSprite != null)
            {
                cardBg.sprite = _roundedRectSprite;
                cardBg.type = Image.Type.Sliced;
            }
            else if (panelBgSprite != null)
            {
                cardBg.sprite = panelBgSprite;
                cardBg.type = Image.Type.Sliced;
            }
            cardBg.color = new Color(0.12f, 0.14f, 0.19f, 0.96f);
            cardBg.raycastTarget = true;

            Outline cardOutline = cardGo.GetComponent<Outline>();
            if (cardOutline == null) cardOutline = cardGo.AddComponent<Outline>();
            cardOutline.effectColor = new Color(0.38f, 0.44f, 0.54f, 0.85f);
            cardOutline.effectDistance = new Vector2(2f, -2f);

            // 顶部标题栏
            Transform headIconTr = cardGo.transform.Find("HeaderIcon");
            GameObject headIconGo = headIconTr != null ? headIconTr.gameObject : new GameObject("HeaderIcon", typeof(RectTransform), typeof(Image));
            headIconGo.transform.SetParent(cardGo.transform, false);
            RectTransform hIconRT = headIconGo.GetComponent<RectTransform>();
            hIconRT.anchorMin = new Vector2(0f, 1f);
            hIconRT.anchorMax = new Vector2(0f, 1f);
            hIconRT.pivot = new Vector2(0f, 1f);
            hIconRT.anchoredPosition = new Vector2(30f, -22f);
            hIconRT.sizeDelta = new Vector2(52f, 52f);
            Image hIconImg = headIconGo.GetComponent<Image>();
            if (upgradeIconSprite != null) hIconImg.sprite = upgradeIconSprite;
            hIconImg.preserveAspect = true;
            hIconImg.raycastTarget = false;

            EnsureText("HeaderTitle", cardGo.transform, font, 34, TextAnchor.MiddleLeft,
                new Vector2(0f, 1f), new Vector2(0.8f, 1f), new Vector2(96f, -78f), new Vector2(0f, -18f), "恐龙进化系统", new Color(0.94f, 0.96f, 1f, 1f));

            // 右上角关闭按钮 ✕
            Transform closeTr = cardGo.transform.Find("CloseButton");
            GameObject closeGo = closeTr != null ? closeTr.gameObject : new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            closeGo.transform.SetParent(cardGo.transform, false);
            RectTransform closeRT = closeGo.GetComponent<RectTransform>();
            closeRT.anchorMin = new Vector2(1f, 1f);
            closeRT.anchorMax = new Vector2(1f, 1f);
            closeRT.pivot = new Vector2(1f, 1f);
            closeRT.anchoredPosition = new Vector2(-24f, -22f);
            closeRT.sizeDelta = new Vector2(54f, 54f);
            Image closeBg = closeGo.GetComponent<Image>();
            if (_roundedRectSprite != null) { closeBg.sprite = _roundedRectSprite; closeBg.type = Image.Type.Sliced; }
            else if (panelBgSprite != null) { closeBg.sprite = panelBgSprite; closeBg.type = Image.Type.Sliced; }
            closeBg.color = new Color(0.76f, 0.24f, 0.24f, 0.96f);
            modalCloseButton = closeGo.GetComponent<Button>();
            modalCloseButton.targetGraphic = closeBg;
            EnsureText("CloseX", closeGo.transform, font, 28, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, "✕", Color.white);

            // 分割线
            Transform divTr = cardGo.transform.Find("Divider");
            GameObject divGo = divTr != null ? divTr.gameObject : new GameObject("Divider", typeof(RectTransform), typeof(Image));
            divGo.transform.SetParent(cardGo.transform, false);
            RectTransform divRT = divGo.GetComponent<RectTransform>();
            divRT.anchorMin = new Vector2(0.05f, 1f);
            divRT.anchorMax = new Vector2(0.95f, 1f);
            divRT.pivot = new Vector2(0.5f, 1f);
            divRT.anchoredPosition = new Vector2(0f, -92f);
            divRT.sizeDelta = new Vector2(0f, 2f);
            Image divImg = divGo.GetComponent<Image>();
            divImg.sprite = _whiteSprite;
            divImg.color = new Color(0.60f, 0.68f, 0.80f, 0.45f);
            divImg.raycastTarget = false;

            // 正中间大号字体：当前阶段名称（按白/绿/蓝/紫对应颜色渲染）
            modalStageTitleText = EnsureText("CurrentStageBigTitle", cardGo.transform, font, 70, TextAnchor.MiddleCenter,
                new Vector2(0.06f, 0.50f), new Vector2(0.94f, 0.80f), Vector2.zero, Vector2.zero, "幼年期", Color.white);

            // 进阶路线说明（如 幼年期 → 亚成年期 | 体型 1.0x → 1.5x）
            modalRouteText = EnsureText("StageRouteText", cardGo.transform, font, 26, TextAnchor.MiddleCenter,
                new Vector2(0.06f, 0.40f), new Vector2(0.94f, 0.51f), Vector2.zero, Vector2.zero, "幼年期  →  亚成年期", new Color(0.88f, 0.92f, 0.98f, 1f));

            // 经验进度条与数值文本
            CreateProgressSlider("ModalExpSlider", cardGo.transform, font,
                new Vector2(0.10f, 0.25f), new Vector2(0.90f, 0.36f), Vector2.zero, Vector2.zero, 24,
                out modalExpSlider, out modalExpFillImage, out modalExpText);

            // 底部手动进化按钮
            Transform evoBtnTr = cardGo.transform.Find("EvolveActionButton");
            GameObject evoBtnGo = evoBtnTr != null ? evoBtnTr.gameObject : new GameObject("EvolveActionButton", typeof(RectTransform), typeof(Image), typeof(Button));
            evoBtnGo.transform.SetParent(cardGo.transform, false);
            RectTransform evoBtnRT = evoBtnGo.GetComponent<RectTransform>();
            evoBtnRT.anchorMin = new Vector2(0.5f, 0f);
            evoBtnRT.anchorMax = new Vector2(0.5f, 0f);
            evoBtnRT.pivot = new Vector2(0.5f, 0f);
            evoBtnRT.anchoredPosition = new Vector2(0f, 34f);
            evoBtnRT.sizeDelta = new Vector2(360f, 78f);

            modalEvolveButtonBg = evoBtnGo.GetComponent<Image>();
            if (_roundedRectSprite != null)
            {
                modalEvolveButtonBg.sprite = _roundedRectSprite;
                modalEvolveButtonBg.type = Image.Type.Sliced;
            }
            else if (panelBgSprite != null)
            {
                modalEvolveButtonBg.sprite = panelBgSprite;
                modalEvolveButtonBg.type = Image.Type.Sliced;
            }
            modalEvolveButtonBg.color = DisabledBtnColor;
            modalEvolveButton = evoBtnGo.GetComponent<Button>();
            modalEvolveButton.targetGraphic = modalEvolveButtonBg;

            modalEvolveButtonText = EnsureText("ButtonText", evoBtnGo.transform, font, 32, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, "经验不足", Color.white);

            modalOverlayObj = overlayGo;
            if (!Application.isPlaying)
            {
                modalOverlayObj.SetActive(false);
            }
        }

        private static void CreateProgressSlider(string name, Transform parent, Font font,
            Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax, int fontSize,
            out Slider outSlider, out Image outFill, out Text outText)
        {
            Transform tr = parent.Find(name);
            GameObject sliderGo = tr != null ? tr.gameObject : new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Slider));
            sliderGo.transform.SetParent(parent, false);
            RectTransform srt = sliderGo.GetComponent<RectTransform>();
            srt.anchorMin = aMin;
            srt.anchorMax = aMax;
            srt.offsetMin = oMin;
            srt.offsetMax = oMax;
            srt.localScale = Vector3.one;

            Image sbg = sliderGo.GetComponent<Image>();
            sbg.sprite = _whiteSprite;
            sbg.color = new Color(0.06f, 0.08f, 0.11f, 0.94f);
            sbg.raycastTarget = false;

            Transform faTr = sliderGo.transform.Find("Fill Area");
            GameObject faGo = faTr != null ? faTr.gameObject : new GameObject("Fill Area", typeof(RectTransform));
            faGo.transform.SetParent(sliderGo.transform, false);
            RectTransform faRT = faGo.GetComponent<RectTransform>();
            faRT.anchorMin = Vector2.zero;
            faRT.anchorMax = Vector2.one;
            faRT.offsetMin = new Vector2(2f, 2f);
            faRT.offsetMax = new Vector2(-2f, -2f);

            Transform fTr = faGo.transform.Find("Fill");
            GameObject fGo = fTr != null ? fTr.gameObject : new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fGo.transform.SetParent(faGo.transform, false);
            RectTransform fRT = fGo.GetComponent<RectTransform>();
            fRT.anchorMin = Vector2.zero;
            fRT.anchorMax = Vector2.one;
            fRT.offsetMin = Vector2.zero;
            fRT.offsetMax = Vector2.zero;

            outFill = fGo.GetComponent<Image>();
            outFill.sprite = _whiteSprite;
            outFill.color = Color.white;
            outFill.raycastTarget = false;

            outSlider = sliderGo.GetComponent<Slider>();
            outSlider.interactable = false;
            outSlider.transition = Selectable.Transition.None;
            outSlider.fillRect = fRT;
            outSlider.minValue = 0f;
            outSlider.maxValue = 1f;
            outSlider.value = 0f;

            outText = EnsureText("ValueText", sliderGo.transform, font, fontSize, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, "0 / 50", Color.white);
        }

        private static Text EnsureText(string name, Transform parent, Font font, int fontSize, TextAnchor align,
            Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax, string defaultText, Color color)
        {
            Transform tr = parent.Find(name);
            GameObject go = tr != null ? tr.gameObject : new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = oMin;
            rt.offsetMax = oMax;
            rt.localScale = Vector3.one;

            Text txt = go.GetComponent<Text>();
            txt.font = font;
            txt.fontSize = fontSize;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = align;
            txt.color = color;
            txt.text = defaultText;
            txt.supportRichText = true;
            txt.raycastTarget = false;

            Outline outline = go.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = new Color(0f, 0f, 0f, 0.92f);
                outline.effectDistance = new Vector2(1.5f, -1.5f);
            }
            return txt;
        }

        private void BindListeners()
        {
            if (entryButton != null)
            {
                entryButton.onClick.RemoveListener(OpenModal);
                entryButton.onClick.AddListener(OpenModal);
            }
            if (modalCloseButton != null)
            {
                modalCloseButton.onClick.RemoveListener(CloseModal);
                modalCloseButton.onClick.AddListener(CloseModal);
            }
            if (modalBackdropButton != null)
            {
                modalBackdropButton.onClick.RemoveListener(CloseModal);
                modalBackdropButton.onClick.AddListener(CloseModal);
            }
            if (modalEvolveButton != null)
            {
                modalEvolveButton.onClick.RemoveListener(HandleManualEvolveClick);
                modalEvolveButton.onClick.AddListener(HandleManualEvolveClick);
            }
        }

        private void UnbindListeners()
        {
            if (entryButton != null) entryButton.onClick.RemoveListener(OpenModal);
            if (modalCloseButton != null) modalCloseButton.onClick.RemoveListener(CloseModal);
            if (modalBackdropButton != null) modalBackdropButton.onClick.RemoveListener(CloseModal);
            if (modalEvolveButton != null) modalEvolveButton.onClick.RemoveListener(HandleManualEvolveClick);
        }

        public void OpenModal()
        {
            if (modalOverlayObj == null) EnsureUIHierarchy();
            RefreshView();
            if (modalOverlayObj != null)
            {
                modalOverlayObj.SetActive(true);
                modalOverlayObj.transform.SetAsLastSibling();
            }
            if (Application.isPlaying) UISoundManager.PlayOpen();
        }

        public void CloseModal()
        {
            if (modalOverlayObj != null && modalOverlayObj.activeSelf)
            {
                modalOverlayObj.SetActive(false);
                if (Application.isPlaying) UISoundManager.PlayClose();
            }
        }

        private void HandleManualEvolveClick()
        {
            DinoEvolutionController ctrl = _boundController != null ? _boundController : DinoEvolutionController.Instance;
            if (ctrl == null || !ctrl.CanEvolve) return;

            // 点击后立即关闭进化弹窗，在当前游戏场景中触发 3 秒原地高燃进化演出
            if (modalOverlayObj != null) modalOverlayObj.SetActive(false);
            ctrl.TryStartManualEvolution();
        }

        public void RefreshView()
        {
            if (entryButton == null || modalStageTitleText == null) EnsureUIHierarchy();

            DinoEvolutionController ctrl = _boundController != null ? _boundController : DinoEvolutionController.Instance;
            if (ctrl == null)
            {
                ApplyFallbackVisual();
                return;
            }

            EvolutionStageConfig curStage = ctrl.CurrentStage;
            bool isMax = ctrl.IsMaxStage;
            bool canEvolve = ctrl.CanEvolve;
            bool isEvolving = ctrl.IsEvolving;

            if (readyBadgeObj != null)
            {
                readyBadgeObj.SetActive(canEvolve);
            }

            // 1. 更新右上常驻迷你阶段条
            if (miniStageText != null)
            {
                miniStageText.text = $"{curStage.stageName} · {curStage.targetScale:0.0}x";
                miniStageText.color = curStage.stageColor;
            }
            if (miniExpFillImage != null)
            {
                miniExpFillImage.color = canEvolve ? ctrl.NextStage.stageColor : curStage.stageColor;
            }

            // 2. 更新进化弹窗内容
            if (modalStageTitleText != null)
            {
                modalStageTitleText.text = curStage.stageName;
                modalStageTitleText.color = curStage.stageColor;
            }

            if (isMax)
            {
                if (miniExpSlider != null) miniExpSlider.value = 1f;
                if (miniExpText != null) miniExpText.text = "MAX";

                if (modalRouteText != null)
                {
                    modalRouteText.text = $"已达到完全体 MAX（体型 {curStage.targetScale:0.0}x）";
                    modalRouteText.color = curStage.stageColor;
                }
                if (modalExpSlider != null) modalExpSlider.value = 1f;
                if (modalExpFillImage != null) modalExpFillImage.color = curStage.stageColor;
                if (modalExpText != null) modalExpText.text = "已达到完全体 MAX";
            }
            else
            {
                EvolutionStageConfig nextStage = ctrl.NextStage;
                float req = Mathf.Max(1f, ctrl.CurrentStageRequiredExp);
                float cur = Mathf.Max(0f, ctrl.CurrentExp);
                float fill01 = Mathf.Clamp01(cur / req);

                if (miniExpSlider != null) miniExpSlider.value = fill01;
                if (miniExpText != null) miniExpText.text = $"{Mathf.RoundToInt(cur)} / {Mathf.RoundToInt(req)}";

                if (modalRouteText != null)
                {
                    string curHex = ColorUtility.ToHtmlStringRGB(curStage.stageColor);
                    string nextHex = ColorUtility.ToHtmlStringRGB(nextStage.stageColor);
                    modalRouteText.text = $"<color=#{curHex}>{curStage.stageName} ({curStage.targetScale:0.0}x)</color>   →   <color=#{nextHex}>{nextStage.stageName} ({nextStage.targetScale:0.0}x)</color>";
                    modalRouteText.color = Color.white;
                }

                if (modalExpSlider != null) modalExpSlider.value = fill01;
                if (modalExpFillImage != null) modalExpFillImage.color = canEvolve ? nextStage.stageColor : curStage.stageColor;
                if (modalExpText != null)
                {
                    modalExpText.text = canEvolve
                        ? $"当前经验: {Mathf.RoundToInt(cur)} / {Mathf.RoundToInt(req)}（可进化！）"
                        : $"当前经验: {Mathf.RoundToInt(cur)} / {Mathf.RoundToInt(req)}";
                }
            }

            // 3. 手动进化按钮状态（未满置灰不可点；经验满高亮可点）
            if (modalEvolveButton != null)
            {
                modalEvolveButton.interactable = canEvolve;
            }
            if (modalEvolveButtonBg != null)
            {
                if (canEvolve)
                {
                    Color targetCol = ctrl.NextStage.stageColor;
                    modalEvolveButtonBg.color = Color.Lerp(targetCol, Color.white, 0.15f);
                }
                else
                {
                    modalEvolveButtonBg.color = DisabledBtnColor;
                }
            }
            if (modalEvolveButtonText != null)
            {
                if (isMax) modalEvolveButtonText.text = "已达到完全体 MAX";
                else if (isEvolving) modalEvolveButtonText.text = "进化演出中...";
                else if (canEvolve) modalEvolveButtonText.text = $"进 化 → {ctrl.NextStage.stageName}";
                else modalEvolveButtonText.text = "经验不足";

                modalEvolveButtonText.color = canEvolve ? Color.white : new Color(0.68f, 0.70f, 0.74f, 1f);
            }
        }

        private void ApplyFallbackVisual()
        {
            if (readyBadgeObj != null) readyBadgeObj.SetActive(false);
            if (miniStageText != null) { miniStageText.text = "幼年期 · 1.0x"; miniStageText.color = Color.white; }
            if (miniExpSlider != null) miniExpSlider.value = 0f;
            if (miniExpText != null) miniExpText.text = "0 / 50";
            if (modalStageTitleText != null) { modalStageTitleText.text = "幼年期"; modalStageTitleText.color = Color.white; }
            if (modalRouteText != null) modalRouteText.text = "幼年期 (1.0x)  →  亚成年期 (1.5x)";
            if (modalExpSlider != null) modalExpSlider.value = 0f;
            if (modalExpText != null) modalExpText.text = "当前经验: 0 / 50";
            if (modalEvolveButton != null) modalEvolveButton.interactable = false;
            if (modalEvolveButtonBg != null) modalEvolveButtonBg.color = DisabledBtnColor;
            if (modalEvolveButtonText != null) modalEvolveButtonText.text = "经验不足";
        }
    }
}
