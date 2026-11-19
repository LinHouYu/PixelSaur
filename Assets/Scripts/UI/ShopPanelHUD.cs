using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PixelSaur.UI
{
    /// <summary>
    /// 岛屿商店界面面板：
    /// 1. 支持【道具商店 / 恐龙商店 / 琥珀充值】三类页签切换；
    /// 2. 动态生成双列商品卡片列表，实时读取背包已拥有状态与道具库存；
    /// 3. 点击购买弹出确认弹窗，展示货币扣除前/后余额并执行真实扣款与背包仓库写入闭环；
    /// 4. 琥珀充值页提供点击即加钱的测试按钮，充值与消费实时同步顶部货币栏与本地存档。
    /// </summary>
    public class ShopPanelHUD : MonoBehaviour
    {
        [Header("基础按钮与面板底图")]
        [SerializeField] private Sprite whitePillSprite;
        [SerializeField] private Sprite buttonPillSprite;

        [Header("商品与货币图标资产")]
        [SerializeField] private Sprite iconDinoSneak;
        [SerializeField] private Sprite iconDinoBite;
        [SerializeField] private Sprite iconDinoJump;
        [SerializeField] private Sprite iconDinoSkill1;
        [SerializeField] private Sprite iconDinoSkill2;
        [SerializeField] private Sprite iconPterosaurRun;
        [SerializeField] private Sprite iconFood;
        [SerializeField] private Sprite iconWater;
        [SerializeField] private Sprite iconHeart;

        private Text _fossilBalanceText;
        private Text _amberBalanceText;

        private readonly Dictionary<ShopTab, Button> _tabButtons = new Dictionary<ShopTab, Button>();
        private readonly Dictionary<ShopTab, Image> _tabImages = new Dictionary<ShopTab, Image>();

        private ScrollRect _scrollRect;
        private RectTransform _gridContent;

        // 确认购买弹窗引用
        private GameObject _confirmOverlay;
        private Image _confirmIconBg;
        private Image _confirmIconImg;
        private Image _confirmRarityImg;
        private Text _confirmRarityText;
        private Text _confirmProductTitle;
        private Text _confirmProductSub;
        private Text _confirmPriceText;
        private Text _confirmBalanceText;
        private Text _confirmDeliveryText;
        private Button _confirmBuyBtn;
        private Image _confirmBuyImg;
        private Text _confirmBuyText;

        private GameObject _toastObj;
        private Text _toastText;
        private float _toastHideTime;

        private ShopTab _activeTab = ShopTab.Dinos;
        private string _pendingProductId;
        private bool _built;
        private static Sprite _fallbackWhitePill;

        private static readonly Color PanelBgColor = new Color(0.07f, 0.10f, 0.15f, 0.99f);
        private static readonly Color SubPanelBgColor = new Color(0.10f, 0.14f, 0.21f, 0.98f);
        private static readonly Color CardBgColor = new Color(0.13f, 0.18f, 0.27f, 0.99f);
        private static readonly Color InsetBgColor = new Color(0.07f, 0.10f, 0.16f, 0.98f);

        private static readonly Color TabActiveColor = new Color(0.16f, 0.64f, 0.36f, 1f);
        private static readonly Color TabNormalColor = new Color(0.18f, 0.24f, 0.33f, 1f);

        private static readonly Color BtnBuyColor = new Color(0.16f, 0.68f, 0.36f, 1f);
        private static readonly Color BtnRechargeColor = new Color(0.18f, 0.50f, 0.82f, 1f);
        private static readonly Color BtnShortColor = new Color(0.56f, 0.30f, 0.24f, 1f);
        private static readonly Color BtnMutedColor = new Color(0.20f, 0.24f, 0.30f, 1f);

        private void Awake()
        {
            LoadFallbackAssets();
            ShopManager.EnsureInitialized();
            InventoryManager.EnsureInitialized();
            QuestManager.EnsureInitialized();
            EnsureUI();
        }

        private void OnEnable()
        {
            LoadFallbackAssets();
            ShopManager.EnsureInitialized();
            InventoryManager.EnsureInitialized();
            QuestManager.EnsureInitialized();
            EnsureUI();

            QuestManager.OnDataChanged += HandleDataChanged;
            InventoryManager.OnInventoryChanged += HandleDataChanged;
            ShopManager.OnShopUpdated += HandleDataChanged;

            RefreshAll(resetScroll: false);
        }

        private void OnDisable()
        {
            QuestManager.OnDataChanged -= HandleDataChanged;
            InventoryManager.OnInventoryChanged -= HandleDataChanged;
            ShopManager.OnShopUpdated -= HandleDataChanged;
        }

        private void Update()
        {
            if (_toastObj != null && _toastObj.activeSelf && Time.unscaledTime >= _toastHideTime)
            {
                _toastObj.SetActive(false);
            }
        }

        private void LoadFallbackAssets()
        {
#if UNITY_EDITOR
            if (whitePillSprite == null) whitePillSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/ui_white_pill_64x64.png");
            if (buttonPillSprite == null) buttonPillSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/ui_bevel_pill_64x64.png");
            if (iconDinoSneak == null) iconDinoSneak = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_dino_sneak_32x32.png");
            if (iconDinoBite == null) iconDinoBite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_dino_bite_32x32.png");
            if (iconDinoJump == null) iconDinoJump = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_dino_jump_32x32.png");
            if (iconDinoSkill1 == null) iconDinoSkill1 = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_dino_skill1.png");
            if (iconDinoSkill2 == null) iconDinoSkill2 = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_dino_skill2.png");
            if (iconPterosaurRun == null) iconPterosaurRun = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_pterosaur_run_32x32.png");
            if (iconFood == null) iconFood = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_food_8x8.png");
            if (iconWater == null) iconWater = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_water_8x8.png");
            if (iconHeart == null) iconHeart = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_heart_8x8.png");
#endif
        }

        private Sprite GetPanelSprite()
        {
            if (whitePillSprite != null) return whitePillSprite;
            if (_fallbackWhitePill != null) return _fallbackWhitePill;

            const int size = 64;
            const float r = 14f;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "ShopPanelFallbackPill",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x < r ? (r - x) : (x > size - 1 - r ? x - (size - 1 - r) : 0f);
                    float dy = y < r ? (r - y) : (y > size - 1 - r ? y - (size - 1 - r) : 0f);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(r + 0.5f - dist));
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            _fallbackWhitePill = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
            _fallbackWhitePill.hideFlags = HideFlags.HideAndDontSave;
            return _fallbackWhitePill;
        }

        private Sprite ResolveIconSprite(string iconKey)
        {
            switch (iconKey)
            {
                case "dino_bite": return iconDinoBite ?? iconDinoSneak;
                case "dino_jump": return iconDinoJump ?? iconDinoSneak;
                case "dino_skill1": return iconDinoSkill1 ?? iconDinoBite;
                case "dino_skill2": return iconDinoSkill2 ?? iconDinoSneak;
                case "pterosaur": return iconPterosaurRun ?? iconDinoJump;
                case "food": return iconFood ?? iconHeart;
                case "water": return iconWater ?? iconHeart;
                case "heart": return iconHeart ?? iconFood;
                default: return iconDinoSneak;
            }
        }

        public void OpenDefault()
        {
            OpenTab(ShopTab.Dinos);
        }

        public void OpenRechargeTab()
        {
            OpenTab(ShopTab.Recharge);
        }

        public void OpenTab(ShopTab tab)
        {
            _activeTab = tab;
            if (_confirmOverlay != null) _confirmOverlay.SetActive(false);

            if (UIPanelManager.Instance != null)
            {
                UIPanelManager.Instance.OpenPanel("ShopPanel");
            }
            else
            {
                gameObject.SetActive(true);
                transform.SetAsLastSibling();
            }

            RefreshAll(resetScroll: true);
        }

        public void SwitchTab(ShopTab tab)
        {
            if (_activeTab == tab) return;
            UISoundManager.PlayClick();
            _activeTab = tab;
            if (_confirmOverlay != null) _confirmOverlay.SetActive(false);
            RefreshAll(resetScroll: true);
        }

        private void HandleDataChanged()
        {
            if (gameObject.activeInHierarchy)
            {
                RefreshAll(resetScroll: false);
            }
        }

        public void EnsureUI()
        {
            if (_built) return;
            _built = true;

            LoadFallbackAssets();

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }
            _tabButtons.Clear();
            _tabImages.Clear();

            Sprite panelSprite = GetPanelSprite();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            RectTransform panelRT = GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.06f, 0.06f);
            panelRT.anchorMax = new Vector2(0.94f, 0.94f);
            panelRT.offsetMin = Vector2.zero;
            panelRT.offsetMax = Vector2.zero;
            panelRT.pivot = new Vector2(0.5f, 0.5f);

            Image bg = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            bg.sprite = panelSprite;
            bg.type = Image.Type.Sliced;
            bg.color = PanelBgColor;
            bg.raycastTarget = true;

            // 1. 顶部标题、双货币实时余额显示、关闭按钮 X
            CreateText(transform, "Title", font, "岛屿恐龙商店", 36, FontStyle.Bold, Color.white,
                TextAnchor.MiddleLeft, new Vector2(0f, 0.87f), new Vector2(0.36f, 1f), new Vector2(36f, 0f), new Vector2(0f, -6f));

            _fossilBalanceText = BuildHeaderCurrencyPill(panelSprite, font, "ShopFossilChip", iconFood, "恐龙化石", -404f);
            _amberBalanceText = BuildHeaderCurrencyPill(panelSprite, font, "ShopAmberChip", iconWater, "琥珀", -124f);

            Button closeBtn = CreateStyledButton(transform, "ShopCloseBtn", font, "✕", 32, new Color(0.46f, 0.18f, 0.20f, 1f),
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -18f), new Vector2(68f, 68f));
            closeBtn.onClick.AddListener(() =>
            {
                if (_confirmOverlay != null) _confirmOverlay.SetActive(false);
                if (UIPanelManager.Instance != null) UIPanelManager.Instance.ClosePanel("ShopPanel");
                else { UISoundManager.PlayClose(); gameObject.SetActive(false); }
            });

            // 2. 商品页签切换（主卖恐龙 / 皮肤 + 充值测试） + 快捷前往恐龙仓库按钮
            BuildTabButton(font, ShopTab.Dinos, "ShopTabDinosBtn", "恐 龙", 36f);
            BuildTabButton(font, ShopTab.Skins, "ShopTabSkinsBtn", "皮 肤", 248f);
            BuildTabButton(font, ShopTab.Recharge, "ShopTabRechargeBtn", "充值测试", 460f);

            Button openInvBtn = CreateStyledButton(transform, "ShopOpenInventoryBtn", font, "前往恐龙仓库 →", 22,
                new Color(0.18f, 0.44f, 0.48f, 1f),
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-36f, -104f), new Vector2(228f, 58f));
            openInvBtn.onClick.AddListener(() =>
            {
                UISoundManager.PlayClick();
                if (_confirmOverlay != null) _confirmOverlay.SetActive(false);
                if (UIPanelManager.Instance != null) UIPanelManager.Instance.OpenPanel("InventoryPanel");
            });

            // 3. 商品卡片滚动列表区
            GameObject listSection = CreateCard(transform, "ProductListSection", panelSprite, SubPanelBgColor);
            RectTransform listRT = listSection.GetComponent<RectTransform>();
            listRT.anchorMin = new Vector2(0.03f, 0.05f);
            listRT.anchorMax = new Vector2(0.97f, 0.77f);
            listRT.offsetMin = Vector2.zero;
            listRT.offsetMax = Vector2.zero;

            GameObject scrollGo = new GameObject("ShopScrollView", typeof(RectTransform), typeof(ScrollRect));
            scrollGo.transform.SetParent(listSection.transform, false);
            RectTransform scrollRT = scrollGo.GetComponent<RectTransform>();
            scrollRT.anchorMin = Vector2.zero;
            scrollRT.anchorMax = Vector2.one;
            scrollRT.offsetMin = new Vector2(14f, 14f);
            scrollRT.offsetMax = new Vector2(-14f, -14f);

            GameObject vpGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
            vpGo.transform.SetParent(scrollGo.transform, false);
            RectTransform vpRT = vpGo.GetComponent<RectTransform>();
            vpRT.anchorMin = Vector2.zero;
            vpRT.anchorMax = Vector2.one;
            vpRT.offsetMin = Vector2.zero;
            vpRT.offsetMax = Vector2.zero;
            Image vpImg = vpGo.GetComponent<Image>();
            vpImg.color = Color.clear;
            vpImg.raycastTarget = true;

            GameObject contentGo = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup));
            contentGo.transform.SetParent(vpGo.transform, false);
            _gridContent = contentGo.GetComponent<RectTransform>();
            _gridContent.anchorMin = new Vector2(0f, 1f);
            _gridContent.anchorMax = new Vector2(1f, 1f);
            _gridContent.pivot = new Vector2(0.5f, 1f);
            _gridContent.anchoredPosition = Vector2.zero;
            _gridContent.sizeDelta = new Vector2(0f, 300f);

            GridLayoutGroup grid = contentGo.GetComponent<GridLayoutGroup>();
            grid.padding = new RectOffset(10, 10, 10, 10);
            grid.cellSize = new Vector2(756f, 184f);
            grid.spacing = new Vector2(18f, 16f);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;

            _scrollRect = scrollGo.GetComponent<ScrollRect>();
            _scrollRect.viewport = vpRT;
            _scrollRect.content = _gridContent;
            _scrollRect.horizontal = false;
            _scrollRect.vertical = true;
            _scrollRect.inertia = true;
            _scrollRect.scrollSensitivity = 28f;
            _scrollRect.movementType = ScrollRect.MovementType.Clamped;

            // 4. 购买确认弹窗（默认隐藏）
            BuildConfirmModal(panelSprite, font);

            // 5. 底部 Toast 反馈提示
            _toastObj = CreateCard(transform, "ShopToast", panelSprite, new Color(0.14f, 0.66f, 0.36f, 0.98f));
            RectTransform toastRT = _toastObj.GetComponent<RectTransform>();
            toastRT.anchorMin = new Vector2(0.5f, 0f);
            toastRT.anchorMax = new Vector2(0.5f, 0f);
            toastRT.pivot = new Vector2(0.5f, 0f);
            toastRT.anchoredPosition = new Vector2(0f, 18f);
            toastRT.sizeDelta = new Vector2(720f, 54f);
            _toastText = CreateText(_toastObj.transform, "Text", font, "", 22, FontStyle.Bold, Color.white,
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(16f, 0f), new Vector2(-16f, 0f));
            _toastObj.SetActive(false);
        }

        private Text BuildHeaderCurrencyPill(Sprite panelSprite, Font font, string name, Sprite iconSprite, string label, float rightPosX)
        {
            GameObject go = CreateCard(transform, name, panelSprite, SubPanelBgColor);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(rightPosX, -22f);
            rt.sizeDelta = new Vector2(260f, 58f);

            GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            RectTransform iRT = iconGo.GetComponent<RectTransform>();
            iRT.anchorMin = new Vector2(0f, 0.5f);
            iRT.anchorMax = new Vector2(0f, 0.5f);
            iRT.pivot = new Vector2(0f, 0.5f);
            iRT.anchoredPosition = new Vector2(14f, 0f);
            iRT.sizeDelta = new Vector2(36f, 36f);
            Image iImg = iconGo.GetComponent<Image>();
            iImg.sprite = iconSprite;
            iImg.preserveAspect = true;
            iImg.raycastTarget = false;

            return CreateText(go.transform, "ValueText", font, $"{label}：0", 22, FontStyle.Bold, Color.white,
                TextAnchor.MiddleLeft, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(60f, 0f), new Vector2(-12f, 0f));
        }

        private void BuildTabButton(Font font, ShopTab tab, string name, string label, float posX)
        {
            Button btn = CreateStyledButton(transform, name, font, label, 24, TabNormalColor,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(posX, -104f), new Vector2(196f, 58f));
            _tabButtons[tab] = btn;
            _tabImages[tab] = btn.GetComponent<Image>();
            ShopTab captured = tab;
            btn.onClick.AddListener(() => SwitchTab(captured));
        }

        private void BuildConfirmModal(Sprite panelSprite, Font font)
        {
            _confirmOverlay = new GameObject("ConfirmPurchaseOverlay", typeof(RectTransform), typeof(Image));
            _confirmOverlay.transform.SetParent(transform, false);
            RectTransform ovRT = _confirmOverlay.GetComponent<RectTransform>();
            ovRT.anchorMin = Vector2.zero;
            ovRT.anchorMax = Vector2.one;
            ovRT.offsetMin = Vector2.zero;
            ovRT.offsetMax = Vector2.zero;
            Image ovImg = _confirmOverlay.GetComponent<Image>();
            ovImg.sprite = panelSprite;
            ovImg.type = Image.Type.Sliced;
            ovImg.color = new Color(0.02f, 0.04f, 0.08f, 0.80f);
            ovImg.raycastTarget = true;

            GameObject modalCard = CreateCard(_confirmOverlay.transform, "ConfirmModalCard", panelSprite, new Color(0.09f, 0.13f, 0.20f, 1f));
            RectTransform mRT = modalCard.GetComponent<RectTransform>();
            mRT.anchorMin = new Vector2(0.5f, 0.5f);
            mRT.anchorMax = new Vector2(0.5f, 0.5f);
            mRT.pivot = new Vector2(0.5f, 0.5f);
            mRT.anchoredPosition = Vector2.zero;
            mRT.sizeDelta = new Vector2(760f, 460f);

            CreateText(modalCard.transform, "ModalTitle", font, "确认购买商品", 32, FontStyle.Bold, Color.white,
                TextAnchor.MiddleCenter, new Vector2(0f, 0.84f), new Vector2(1f, 0.98f), Vector2.zero, Vector2.zero);

            // 头部商品信息
            _confirmIconBg = CreateCard(modalCard.transform, "ProductIconBg", panelSprite, InsetBgColor).GetComponent<Image>();
            RectTransform ciRT = _confirmIconBg.rectTransform;
            ciRT.anchorMin = new Vector2(0.05f, 0.58f);
            ciRT.anchorMax = new Vector2(0.21f, 0.82f);
            ciRT.offsetMin = Vector2.zero;
            ciRT.offsetMax = Vector2.zero;

            GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(_confirmIconBg.transform, false);
            RectTransform iRT = iconGo.GetComponent<RectTransform>();
            iRT.anchorMin = new Vector2(0.14f, 0.14f);
            iRT.anchorMax = new Vector2(0.86f, 0.86f);
            iRT.offsetMin = Vector2.zero;
            iRT.offsetMax = Vector2.zero;
            _confirmIconImg = iconGo.GetComponent<Image>();
            _confirmIconImg.preserveAspect = true;
            _confirmIconImg.raycastTarget = false;

            GameObject rarityGo = CreateCard(modalCard.transform, "RarityBadge", panelSprite, new Color(0.95f, 0.68f, 0.20f, 1f));
            RectTransform rRT = rarityGo.GetComponent<RectTransform>();
            rRT.anchorMin = new Vector2(0.24f, 0.75f);
            rRT.anchorMax = new Vector2(0.38f, 0.82f);
            rRT.offsetMin = Vector2.zero;
            rRT.offsetMax = Vector2.zero;
            _confirmRarityImg = rarityGo.GetComponent<Image>();
            _confirmRarityText = CreateText(rarityGo.transform, "Text", font, "稀有", 17, FontStyle.Bold, Color.white,
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddOutline(_confirmRarityText, 1f);

            _confirmProductTitle = CreateText(modalCard.transform, "ProductTitle", font, "商品名称", 28, FontStyle.Bold, Color.white,
                TextAnchor.MiddleLeft, new Vector2(0.24f, 0.64f), new Vector2(0.95f, 0.75f), Vector2.zero, Vector2.zero);

            _confirmProductSub = CreateText(modalCard.transform, "ProductSub", font, "副标题", 19, FontStyle.Bold,
                new Color(0.70f, 0.86f, 1f, 1f),
                TextAnchor.MiddleLeft, new Vector2(0.24f, 0.56f), new Vector2(0.95f, 0.64f), Vector2.zero, Vector2.zero);

            // 扣除详情与闭环说明卡片
            GameObject breakdown = CreateCard(modalCard.transform, "BreakdownBox", panelSprite, InsetBgColor);
            RectTransform bRT = breakdown.GetComponent<RectTransform>();
            bRT.anchorMin = new Vector2(0.05f, 0.23f);
            bRT.anchorMax = new Vector2(0.95f, 0.53f);
            bRT.offsetMin = Vector2.zero;
            bRT.offsetMax = Vector2.zero;

            _confirmPriceText = CreateText(breakdown.transform, "PriceLine", font, "支付价格：", 22, FontStyle.Bold,
                new Color(1f, 0.84f, 0.34f, 1f),
                TextAnchor.MiddleLeft, new Vector2(0f, 0.64f), new Vector2(1f, 0.96f), new Vector2(20f, 0f), new Vector2(-20f, 0f));

            _confirmBalanceText = CreateText(breakdown.transform, "BalanceLine", font, "当前余额：", 21, FontStyle.Bold,
                new Color(0.56f, 0.92f, 0.68f, 1f),
                TextAnchor.MiddleLeft, new Vector2(0f, 0.32f), new Vector2(1f, 0.64f), new Vector2(20f, 0f), new Vector2(-20f, 0f));

            _confirmDeliveryText = CreateText(breakdown.transform, "DeliveryLine", font, "发放去向：直接写入恐龙背包本地存档", 19, FontStyle.Normal,
                new Color(0.80f, 0.88f, 0.96f, 1f),
                TextAnchor.MiddleLeft, new Vector2(0f, 0.02f), new Vector2(1f, 0.34f), new Vector2(20f, 0f), new Vector2(-20f, 0f));

            // 取消 / 确认购买按钮
            Button cancelBtn = CreateStyledButton(modalCard.transform, "ConfirmCancelBtn", font, "取 消", 25,
                new Color(0.22f, 0.27f, 0.35f, 1f),
                new Vector2(0.28f, 0.11f), new Vector2(0.28f, 0.11f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240f, 64f));
            cancelBtn.onClick.AddListener(() =>
            {
                UISoundManager.PlayClick();
                _pendingProductId = null;
                _confirmOverlay.SetActive(false);
            });

            _confirmBuyBtn = CreateStyledButton(modalCard.transform, "ConfirmBuyBtn", font, "确认购买", 25, BtnBuyColor,
                new Vector2(0.72f, 0.11f), new Vector2(0.72f, 0.11f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240f, 64f));
            _confirmBuyImg = _confirmBuyBtn.GetComponent<Image>();
            _confirmBuyText = _confirmBuyBtn.GetComponentInChildren<Text>();
            _confirmBuyBtn.onClick.AddListener(HandleConfirmBuyClicked);

            _confirmOverlay.SetActive(false);
        }

        private void OpenConfirmModal(ShopProductData product)
        {
            if (product == null || _confirmOverlay == null) return;
            _pendingProductId = product.productId;

            if (_confirmIconImg != null) _confirmIconImg.sprite = ResolveIconSprite(product.iconKey);
            if (_confirmIconBg != null)
            {
                _confirmIconBg.color = new Color(product.accentColor.r * 0.30f, product.accentColor.g * 0.30f, product.accentColor.b * 0.30f, 1f);
            }
            if (_confirmRarityImg != null) _confirmRarityImg.color = product.rarityColor;
            if (_confirmRarityText != null) _confirmRarityText.text = product.rarityLabel;
            if (_confirmProductTitle != null) _confirmProductTitle.text = product.title;
            if (_confirmProductSub != null) _confirmProductSub.text = product.subtitle;

            string unit = ShopManager.GetCurrencyName(product.currency);
            int curBalance = ShopManager.GetCurrencyBalance(product.currency);
            int afterBalance = curBalance - product.price;
            bool affordable = afterBalance >= 0;

            if (_confirmPriceText != null) _confirmPriceText.text = $"支付金额：{product.price} {unit}";
            if (_confirmBalanceText != null)
            {
                _confirmBalanceText.text = affordable
                    ? $"余额变化：{curBalance:N0}  →  {afterBalance:N0} {unit}"
                    : $"当前余额：{curBalance:N0} {unit}（还差 {-afterBalance} {unit}，余额不足！）";
                _confirmBalanceText.color = affordable ? new Color(0.56f, 0.92f, 0.68f, 1f) : new Color(1f, 0.46f, 0.46f, 1f);
            }
            if (_confirmDeliveryText != null)
            {
                _confirmDeliveryText.text = product.tab == ShopTab.Dinos
                    ? "发放去向：一次性解锁永久入库，可在仓库中随时出战切换"
                    : "发放去向：一次性解锁永久入库，可在仓库中随时穿戴生效";
            }

            if (_confirmBuyBtn != null) _confirmBuyBtn.interactable = affordable;
            if (_confirmBuyImg != null) _confirmBuyImg.color = affordable ? BtnBuyColor : BtnMutedColor;
            if (_confirmBuyText != null) _confirmBuyText.text = affordable ? "确认购买" : "余额不足";

            _confirmOverlay.SetActive(true);
            _confirmOverlay.transform.SetAsLastSibling();
        }

        private void HandleConfirmBuyClicked()
        {
            if (string.IsNullOrEmpty(_pendingProductId)) return;

            bool ok = ShopManager.TryPurchaseProduct(_pendingProductId, out string msg);
            if (ok) UISoundManager.PlaySave();
            else UISoundManager.PlayClick();

            _pendingProductId = null;
            if (_confirmOverlay != null) _confirmOverlay.SetActive(false);
            ShowToast(msg, ok);
        }

        public void RefreshAll(bool resetScroll)
        {
            if (!_built) EnsureUI();

            // 1. 更新顶部双货币余额
            if (_fossilBalanceText != null) _fossilBalanceText.text = $"恐龙化石：{QuestManager.Gold:N0}";
            if (_amberBalanceText != null) _amberBalanceText.text = $"琥珀：{QuestManager.Amber:N0}";

            // 2. 更新页签状态
            UpdateTabVisual(ShopTab.Dinos, "恐 龙");
            UpdateTabVisual(ShopTab.Skins, "皮 肤");
            UpdateTabVisual(ShopTab.Recharge, "充值测试");

            // 3. 重新生成商品卡片列表
            for (int i = _gridContent.childCount - 1; i >= 0; i--)
            {
                Destroy(_gridContent.GetChild(i).gameObject);
            }

            List<ShopProductData> products = ShopManager.GetProductsByTab(_activeTab);
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Sprite panelSprite = GetPanelSprite();

            for (int i = 0; i < products.Count; i++)
            {
                BuildProductCard(products[i], font, panelSprite);
            }

            int rows = Mathf.CeilToInt(products.Count / 2f);
            float totalH = 20f + rows * 184f + Mathf.Max(0, rows - 1) * 16f;
            _gridContent.sizeDelta = new Vector2(0f, Mathf.Max(220f, totalH));

            Canvas.ForceUpdateCanvases();
            if (resetScroll && _scrollRect != null)
            {
                _scrollRect.velocity = Vector2.zero;
                _scrollRect.verticalNormalizedPosition = 1f;
            }
        }

        private void UpdateTabVisual(ShopTab tab, string label)
        {
            if (_tabImages.TryGetValue(tab, out Image img) && img != null)
            {
                img.color = _activeTab == tab ? TabActiveColor : TabNormalColor;
            }
            if (_tabButtons.TryGetValue(tab, out Button btn) && btn != null)
            {
                Text txt = btn.GetComponentInChildren<Text>();
                if (txt != null) txt.text = label;
            }
        }

        private void BuildProductCard(ShopProductData product, Font font, Sprite panelSprite)
        {
            bool isOwned = ShopManager.IsOwned(product);
            bool affordable = ShopManager.CanAfford(product);

            GameObject cardGo = CreateCard(_gridContent, "Product_" + product.productId, panelSprite, CardBgColor);

            // 1. 左侧图标底座 + 稀有度标签
            Color pedColor = new Color(product.accentColor.r * 0.30f, product.accentColor.g * 0.30f, product.accentColor.b * 0.30f, 1f);
            GameObject iconBox = CreateCard(cardGo.transform, "IconBox", panelSprite, pedColor);
            RectTransform ibRT = iconBox.GetComponent<RectTransform>();
            ibRT.anchorMin = new Vector2(0.03f, 0.14f);
            ibRT.anchorMax = new Vector2(0.21f, 0.86f);
            ibRT.offsetMin = Vector2.zero;
            ibRT.offsetMax = Vector2.zero;

            GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(iconBox.transform, false);
            RectTransform iRT = iconGo.GetComponent<RectTransform>();
            iRT.anchorMin = new Vector2(0.14f, 0.14f);
            iRT.anchorMax = new Vector2(0.86f, 0.86f);
            iRT.offsetMin = Vector2.zero;
            iRT.offsetMax = Vector2.zero;
            Image iconImg = iconGo.GetComponent<Image>();
            iconImg.sprite = ResolveIconSprite(product.iconKey);
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            GameObject rarityTag = CreateCard(iconBox.transform, "RarityTag", panelSprite, product.rarityColor);
            RectTransform rtRT = rarityTag.GetComponent<RectTransform>();
            rtRT.anchorMin = new Vector2(0.06f, 0.76f);
            rtRT.anchorMax = new Vector2(0.94f, 0.96f);
            rtRT.offsetMin = Vector2.zero;
            rtRT.offsetMax = Vector2.zero;
            Text rTxt = CreateText(rarityTag.transform, "Text", font, product.rarityLabel, 14, FontStyle.Bold, Color.white,
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddOutline(rTxt, 1f);

            // 2. 中间标题 + 描述 + 当前背包同步状态
            Text titleTxt = CreateText(cardGo.transform, "Title", font, product.title, 24, FontStyle.Bold, Color.white,
                TextAnchor.MiddleLeft, new Vector2(0.23f, 0.72f), new Vector2(0.71f, 0.93f), Vector2.zero, Vector2.zero);
            AddOutline(titleTxt, 1f);

            Text descTxt = CreateText(cardGo.transform, "Desc", font, product.description, 16, FontStyle.Normal,
                new Color(0.78f, 0.86f, 0.94f, 0.98f),
                TextAnchor.UpperLeft, new Vector2(0.23f, 0.25f), new Vector2(0.71f, 0.72f), Vector2.zero, new Vector2(-4f, -2f));
            descTxt.horizontalOverflow = HorizontalWrapMode.Wrap;
            descTxt.verticalOverflow = VerticalWrapMode.Truncate;
            descTxt.lineSpacing = 1.18f;

            string warehouseNote;
            Color noteColor;
            if (product.isRechargePack)
            {
                warehouseNote = product.subtitle;
                noteColor = new Color(0.46f, 0.88f, 1f, 1f);
            }
            else
            {
                warehouseNote = isOwned ? "仓库状态：已拥有（已在恐龙仓库中）" : "仓库状态：未购买（一次性解锁写入仓库）";
                noteColor = isOwned ? new Color(0.42f, 0.94f, 0.56f, 1f) : new Color(0.96f, 0.84f, 0.44f, 1f);
            }

            CreateText(cardGo.transform, "WarehouseNote", font, warehouseNote, 16, FontStyle.Bold, noteColor,
                TextAnchor.MiddleLeft, new Vector2(0.23f, 0.05f), new Vector2(0.71f, 0.25f), Vector2.zero, Vector2.zero);

            // 3. 右侧价格行 + 购买/测试充值按钮
            GameObject pricePill = CreateCard(cardGo.transform, "PricePill", panelSprite, InsetBgColor);
            RectTransform ppRT = pricePill.GetComponent<RectTransform>();
            ppRT.anchorMin = new Vector2(0.73f, 0.54f);
            ppRT.anchorMax = new Vector2(0.97f, 0.86f);
            ppRT.offsetMin = Vector2.zero;
            ppRT.offsetMax = Vector2.zero;

            GameObject cIconGo = new GameObject("CurrencyIcon", typeof(RectTransform), typeof(Image));
            cIconGo.transform.SetParent(pricePill.transform, false);
            RectTransform ciRT = cIconGo.GetComponent<RectTransform>();
            ciRT.anchorMin = new Vector2(0.08f, 0.18f);
            ciRT.anchorMax = new Vector2(0.28f, 0.82f);
            ciRT.offsetMin = Vector2.zero;
            ciRT.offsetMax = Vector2.zero;
            Image cImg = cIconGo.GetComponent<Image>();
            cImg.sprite = product.currency == ShopCurrency.Fossil ? iconFood : iconWater;
            cImg.preserveAspect = true;
            cImg.raycastTarget = false;

            string priceLabel = product.isRechargePack
                ? "测试加钱"
                : $"{product.price} {ShopManager.GetCurrencyName(product.currency)}";
            Color priceColor = product.isRechargePack
                ? new Color(0.46f, 0.90f, 1f, 1f)
                : (affordable ? new Color(1f, 0.88f, 0.40f, 1f) : new Color(1f, 0.48f, 0.48f, 1f));
            CreateText(pricePill.transform, "PriceText", font, priceLabel, 18, FontStyle.Bold, priceColor,
                TextAnchor.MiddleLeft, new Vector2(0.30f, 0f), new Vector2(0.96f, 1f), Vector2.zero, Vector2.zero);

            string btnText;
            Color btnColor;
            bool btnInteractable;
            if (product.isRechargePack)
            {
                btnText = "立即充值";
                btnColor = BtnRechargeColor;
                btnInteractable = true;
            }
            else if (isOwned)
            {
                btnText = "已拥有";
                btnColor = BtnMutedColor;
                btnInteractable = false;
            }
            else
            {
                btnText = "购 买";
                btnColor = affordable ? BtnBuyColor : BtnShortColor;
                btnInteractable = true;
            }

            Button actionBtn = CreateStyledButton(cardGo.transform, "BuyBtn", font, btnText, 22, btnColor,
                new Vector2(0.85f, 0.28f), new Vector2(0.85f, 0.28f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            RectTransform abRT = actionBtn.GetComponent<RectTransform>();
            abRT.anchorMin = new Vector2(0.73f, 0.12f);
            abRT.anchorMax = new Vector2(0.97f, 0.48f);
            abRT.offsetMin = Vector2.zero;
            abRT.offsetMax = Vector2.zero;
            actionBtn.interactable = btnInteractable;
            if (!btnInteractable)
            {
                Text bt = actionBtn.GetComponentInChildren<Text>();
                if (bt != null) bt.color = new Color(0.68f, 0.72f, 0.78f, 0.92f);
            }

            ShopProductData capturedProduct = product;
            actionBtn.onClick.AddListener(() =>
            {
                if (capturedProduct.isRechargePack)
                {
                    bool ok = ShopManager.TryPurchaseProduct(capturedProduct.productId, out string msg);
                    if (ok) UISoundManager.PlaySave();
                    else UISoundManager.PlayClick();
                    ShowToast(msg, ok);
                }
                else
                {
                    UISoundManager.PlayClick();
                    OpenConfirmModal(capturedProduct);
                }
            });
        }

        private void ShowToast(string msg, bool success)
        {
            if (_toastObj == null || _toastText == null) return;
            Image img = _toastObj.GetComponent<Image>();
            if (img != null)
            {
                img.color = success
                    ? new Color(0.14f, 0.66f, 0.36f, 0.98f)
                    : new Color(0.66f, 0.24f, 0.22f, 0.98f);
            }
            _toastText.text = msg;
            _toastObj.SetActive(true);
            _toastObj.transform.SetAsLastSibling();
            _toastHideTime = Time.unscaledTime + 2.2f;
        }

        private static GameObject CreateCard(Transform parent, string name, Sprite sprite, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.color = color;
            return go;
        }

        private Button CreateStyledButton(Transform parent, string name, Font font, string text, int fontSize, Color bgColor,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            Image img = go.GetComponent<Image>();
            img.sprite = buttonPillSprite != null ? buttonPillSprite : GetPanelSprite();
            img.type = Image.Type.Sliced;
            img.color = bgColor;

            Button btn = go.GetComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            cb.pressedColor = new Color(0.70f, 0.85f, 1.0f, 1f);
            cb.selectedColor = Color.white;
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.1f;
            btn.colors = cb;

            Text txt = CreateText(go.transform, "Text", font, text, fontSize, FontStyle.Bold, Color.white,
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddOutline(txt, 1.5f);
            return btn;
        }

        private static Text CreateText(Transform parent, string name, Font font, string content, int fontSize, FontStyle style, Color color,
            TextAnchor align, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;

            Text txt = go.GetComponent<Text>();
            txt.font = font;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.text = content;
            txt.color = color;
            txt.alignment = align;
            txt.raycastTarget = false;
            return txt;
        }

        private static void AddOutline(Text txt, float dist)
        {
            Outline outline = txt.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.90f);
            outline.effectDistance = new Vector2(dist, -dist);
        }
    }
}
