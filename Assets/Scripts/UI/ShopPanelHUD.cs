using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PixelSaur.UI
{
    /// <summary>
    /// 极简原型恐龙商店面板：
    /// 去除冗余装饰与冗长文案，仅保留【恐龙 / 皮肤 / 充值测试】页签、马赛克图标卡片、价格与一次性购买确认闭环。
    /// </summary>
    public class ShopPanelHUD : MonoBehaviour
    {
        [Header("基础按钮与面板底图")]
        [SerializeField] private Sprite whitePillSprite;
        [SerializeField] private Sprite buttonPillSprite;

        [Header("兼容保留图标资产")]
        [SerializeField] private Sprite iconDinoSneak;
        [SerializeField] private Sprite iconDinoBite;
        [SerializeField] private Sprite iconDinoJump;
        [SerializeField] private Sprite iconDinoSkill1;
        [SerializeField] private Sprite iconDinoSkill2;
        [SerializeField] private Sprite iconPterosaurRun;
        [SerializeField] private Sprite iconFood;
        [SerializeField] private Sprite iconWater;
        [SerializeField] private Sprite iconHeart;

        private Text _balanceText;
        private readonly Dictionary<ShopTab, Button> _tabButtons = new Dictionary<ShopTab, Button>();
        private readonly Dictionary<ShopTab, Image> _tabImages = new Dictionary<ShopTab, Image>();

        private RectTransform _gridContent;

        // 极简确认购买弹窗
        private GameObject _confirmOverlay;
        private Text _confirmTitleText;
        private Text _confirmInfoText;
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

        private static readonly Color PanelBgColor = new Color(0.08f, 0.10f, 0.14f, 0.98f);
        private static readonly Color GridBgColor = new Color(0.11f, 0.14f, 0.20f, 0.96f);
        private static readonly Color CardBgColor = new Color(0.16f, 0.21f, 0.29f, 1f);

        private static readonly Color TabActiveColor = new Color(0.16f, 0.64f, 0.36f, 1f);
        private static readonly Color TabNormalColor = new Color(0.18f, 0.23f, 0.32f, 1f);

        private static readonly Color BtnBuyColor = new Color(0.16f, 0.68f, 0.36f, 1f);
        private static readonly Color BtnRechargeColor = new Color(0.18f, 0.50f, 0.82f, 1f);
        private static readonly Color BtnShortColor = new Color(0.56f, 0.30f, 0.24f, 1f);
        private static readonly Color BtnMutedColor = new Color(0.22f, 0.26f, 0.32f, 1f);

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

            RefreshAll();
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
            if (iconFood == null) iconFood = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_food_8x8.png");
            if (iconWater == null) iconWater = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_water_8x8.png");
#endif
        }

        private Sprite GetPanelSprite()
        {
            if (whitePillSprite != null) return whitePillSprite;
            if (_fallbackWhitePill != null) return _fallbackWhitePill;

            const int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "ShopFallbackRect",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color[] px = new Color[size * size];
            for (int i = 0; i < px.Length; i++) px[i] = Color.white;
            tex.SetPixels(px);
            tex.Apply();
            _fallbackWhitePill = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            _fallbackWhitePill.hideFlags = HideFlags.HideAndDontSave;
            return _fallbackWhitePill;
        }

        private Sprite ResolveProductSprite(ShopProductData product)
        {
            if (product == null) return iconDinoSneak;
            if (product.isRechargePack)
            {
                return product.currency == ShopCurrency.Amber ? iconWater : iconFood;
            }
            Sprite mosaic = InventoryManager.GetItemMosaicSprite(product.targetInventoryId);
            return mosaic != null ? mosaic : iconDinoSneak;
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

            RefreshAll();
        }

        public void SwitchTab(ShopTab tab)
        {
            if (_activeTab == tab) return;
            UISoundManager.PlayClick();
            _activeTab = tab;
            if (_confirmOverlay != null) _confirmOverlay.SetActive(false);
            RefreshAll();
        }

        private void HandleDataChanged()
        {
            if (gameObject.activeInHierarchy)
            {
                RefreshAll();
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

            // 1. 极简顶栏：标题 + 【恐龙 / 皮肤 / 充值测试】 + 余额 + 去仓库 + 关闭
            CreateText(transform, "Title", font, "恐龙商店", 34, FontStyle.Bold, Color.white,
                TextAnchor.MiddleLeft, new Vector2(0f, 0.89f), new Vector2(0.18f, 0.99f), new Vector2(28f, 0f), Vector2.zero);

            BuildTabButton(font, ShopTab.Dinos, "ShopTabDinosBtn", "恐 龙", 230f);
            BuildTabButton(font, ShopTab.Skins, "ShopTabSkinsBtn", "皮 肤", 390f);
            BuildTabButton(font, ShopTab.Recharge, "ShopTabRechargeBtn", "充值测试", 550f);

            _balanceText = CreateText(transform, "BalanceText", font, "化石：0   琥珀：0", 22, FontStyle.Bold,
                new Color(1f, 0.88f, 0.44f, 1f),
                TextAnchor.MiddleRight, new Vector2(0.45f, 0.89f), new Vector2(0.80f, 0.99f), Vector2.zero, new Vector2(-12f, 0f));

            Button openInvBtn = CreateSimpleButton(transform, "ShopOpenInventoryBtn", font, "去仓库 →", 20,
                new Color(0.18f, 0.44f, 0.48f, 1f),
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-104f, -18f), new Vector2(144f, 56f));
            openInvBtn.onClick.AddListener(() =>
            {
                UISoundManager.PlayClick();
                if (_confirmOverlay != null) _confirmOverlay.SetActive(false);
                InventoryPanelHUD.OpenGlobalWarehouse();
            });

            Button closeBtn = CreateSimpleButton(transform, "ShopCloseBtn", font, "✕", 28, new Color(0.46f, 0.18f, 0.20f, 1f),
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -18f), new Vector2(62f, 56f));
            closeBtn.onClick.AddListener(() =>
            {
                if (_confirmOverlay != null) _confirmOverlay.SetActive(false);
                if (UIPanelManager.Instance != null) UIPanelManager.Instance.ClosePanel("ShopPanel");
                else { UISoundManager.PlayClose(); gameObject.SetActive(false); }
            });

            // 2. 极简 3 列商品网格区（6 格一屏完整呈现，无冗余文案）
            GameObject gridSection = CreateCard(transform, "ShopGridSection", panelSprite, GridBgColor);
            RectTransform gsRT = gridSection.GetComponent<RectTransform>();
            gsRT.anchorMin = new Vector2(0.03f, 0.04f);
            gsRT.anchorMax = new Vector2(0.97f, 0.87f);
            gsRT.offsetMin = Vector2.zero;
            gsRT.offsetMax = Vector2.zero;

            GameObject contentGo = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup));
            contentGo.transform.SetParent(gridSection.transform, false);
            _gridContent = contentGo.GetComponent<RectTransform>();
            _gridContent.anchorMin = Vector2.zero;
            _gridContent.anchorMax = Vector2.one;
            _gridContent.offsetMin = new Vector2(20f, 20f);
            _gridContent.offsetMax = new Vector2(-20f, -20f);

            GridLayoutGroup grid = contentGo.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(494f, 342f);
            grid.spacing = new Vector2(20f, 20f);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.MiddleCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;

            // 3. 极简购买确认弹窗
            BuildConfirmModal(panelSprite, font);

            // 4. 轻量 Toast
            _toastObj = CreateCard(transform, "ShopToast", panelSprite, new Color(0.14f, 0.66f, 0.36f, 0.96f));
            RectTransform toastRT = _toastObj.GetComponent<RectTransform>();
            toastRT.anchorMin = new Vector2(0.5f, 0f);
            toastRT.anchorMax = new Vector2(0.5f, 0f);
            toastRT.pivot = new Vector2(0.5f, 0f);
            toastRT.anchoredPosition = new Vector2(0f, 16f);
            toastRT.sizeDelta = new Vector2(560f, 48f);
            _toastText = CreateText(_toastObj.transform, "Text", font, "", 20, FontStyle.Bold, Color.white,
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _toastObj.SetActive(false);
        }

        private void BuildTabButton(Font font, ShopTab tab, string name, string label, float posX)
        {
            Button btn = CreateSimpleButton(transform, name, font, label, 22, TabNormalColor,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(posX, -18f), new Vector2(146f, 56f));
            _tabButtons[tab] = btn;
            _tabImages[tab] = btn.GetComponent<Image>();
            ShopTab captured = tab;
            btn.onClick.AddListener(() => SwitchTab(captured));
        }

        private void BuildConfirmModal(Sprite panelSprite, Font font)
        {
            _confirmOverlay = CreateCard(transform, "ConfirmPurchaseOverlay", panelSprite, new Color(0.02f, 0.04f, 0.08f, 0.78f));
            RectTransform ovRT = _confirmOverlay.GetComponent<RectTransform>();
            ovRT.anchorMin = Vector2.zero;
            ovRT.anchorMax = Vector2.one;
            ovRT.offsetMin = Vector2.zero;
            ovRT.offsetMax = Vector2.zero;
            _confirmOverlay.GetComponent<Image>().raycastTarget = true;

            GameObject modal = CreateCard(_confirmOverlay.transform, "ConfirmModalCard", panelSprite, new Color(0.11f, 0.15f, 0.22f, 1f));
            RectTransform mRT = modal.GetComponent<RectTransform>();
            mRT.anchorMin = new Vector2(0.5f, 0.5f);
            mRT.anchorMax = new Vector2(0.5f, 0.5f);
            mRT.pivot = new Vector2(0.5f, 0.5f);
            mRT.sizeDelta = new Vector2(580f, 280f);

            _confirmTitleText = CreateText(modal.transform, "ModalTitle", font, "确认购买", 28, FontStyle.Bold, Color.white,
                TextAnchor.MiddleCenter, new Vector2(0.05f, 0.66f), new Vector2(0.95f, 0.92f), Vector2.zero, Vector2.zero);

            _confirmInfoText = CreateText(modal.transform, "ModalInfo", font, "", 21, FontStyle.Bold, new Color(0.86f, 0.92f, 0.98f, 1f),
                TextAnchor.MiddleCenter, new Vector2(0.06f, 0.34f), new Vector2(0.94f, 0.66f), Vector2.zero, Vector2.zero);

            Button cancelBtn = CreateSimpleButton(modal.transform, "ConfirmCancelBtn", font, "取 消", 22, TabNormalColor,
                new Vector2(0.28f, 0.16f), new Vector2(0.28f, 0.16f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(190f, 56f));
            cancelBtn.onClick.AddListener(() =>
            {
                UISoundManager.PlayClick();
                _pendingProductId = null;
                _confirmOverlay.SetActive(false);
            });

            _confirmBuyBtn = CreateSimpleButton(modal.transform, "ConfirmBuyBtn", font, "确认购买", 22, BtnBuyColor,
                new Vector2(0.72f, 0.16f), new Vector2(0.72f, 0.16f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(190f, 56f));
            _confirmBuyImg = _confirmBuyBtn.GetComponent<Image>();
            _confirmBuyText = _confirmBuyBtn.GetComponentInChildren<Text>();
            _confirmBuyBtn.onClick.AddListener(HandleConfirmBuyClicked);

            _confirmOverlay.SetActive(false);
        }

        private void OpenConfirmModal(ShopProductData product)
        {
            if (product == null || _confirmOverlay == null) return;
            _pendingProductId = product.productId;

            string unit = ShopManager.GetCurrencyName(product.currency);
            int curBalance = ShopManager.GetCurrencyBalance(product.currency);
            int after = curBalance - product.price;
            bool affordable = after >= 0;

            _confirmTitleText.text = $"确认购买【{product.title}】？";
            _confirmInfoText.text = affordable
                ? $"支付 {product.price} {unit}（余额：{curBalance:N0} → {after:N0}）\n购买后永久进入恐龙仓库"
                : $"需要 {product.price} {unit}，当前仅有 {curBalance:N0} {unit}（余额不足）";
            _confirmInfoText.color = affordable ? new Color(0.76f, 0.96f, 0.82f, 1f) : new Color(1f, 0.50f, 0.50f, 1f);

            _confirmBuyBtn.interactable = affordable;
            _confirmBuyImg.color = affordable ? BtnBuyColor : BtnMutedColor;
            _confirmBuyText.text = affordable ? "确认购买" : "余额不足";

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

        public void RefreshAll()
        {
            if (!_built) EnsureUI();

            if (_balanceText != null)
            {
                _balanceText.text = $"化石：{QuestManager.Gold:N0}    琥珀：{QuestManager.Amber:N0}";
            }

            UpdateTabVisual(ShopTab.Dinos);
            UpdateTabVisual(ShopTab.Skins);
            UpdateTabVisual(ShopTab.Recharge);

            for (int i = _gridContent.childCount - 1; i >= 0; i--)
            {
                Destroy(_gridContent.GetChild(i).gameObject);
            }

            List<ShopProductData> products = ShopManager.GetProductsByTab(_activeTab);
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Sprite panelSprite = GetPanelSprite();

            for (int i = 0; i < products.Count; i++)
            {
                BuildMinimalCard(products[i], font, panelSprite);
            }
        }

        private void UpdateTabVisual(ShopTab tab)
        {
            if (_tabImages.TryGetValue(tab, out Image img) && img != null)
            {
                img.color = _activeTab == tab ? TabActiveColor : TabNormalColor;
            }
        }

        private void BuildMinimalCard(ShopProductData product, Font font, Sprite panelSprite)
        {
            bool isOwned = ShopManager.IsOwned(product);
            bool affordable = ShopManager.CanAfford(product);

            GameObject card = CreateCard(_gridContent, "Product_" + product.productId, panelSprite, CardBgColor);

            // 中上方大马赛克图标
            GameObject iconGo = new GameObject("MosaicIcon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(card.transform, false);
            RectTransform iRT = iconGo.GetComponent<RectTransform>();
            iRT.anchorMin = new Vector2(0.30f, 0.44f);
            iRT.anchorMax = new Vector2(0.70f, 0.92f);
            iRT.offsetMin = Vector2.zero;
            iRT.offsetMax = Vector2.zero;
            Image iconImg = iconGo.GetComponent<Image>();
            iconImg.sprite = ResolveProductSprite(product);
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            // 名称
            Text titleTxt = CreateText(card.transform, "Title", font, product.title, 24, FontStyle.Bold, Color.white,
                TextAnchor.MiddleCenter, new Vector2(0.05f, 0.28f), new Vector2(0.95f, 0.44f), Vector2.zero, Vector2.zero);
            AddOutline(titleTxt, 1.2f);

            // 价格文本
            string priceStr = product.isRechargePack
                ? "点击即加钱（测试）"
                : (isOwned ? "已永久解锁" : $"{product.price} {ShopManager.GetCurrencyName(product.currency)}");
            Color priceCol = product.isRechargePack
                ? new Color(0.52f, 0.88f, 1f, 1f)
                : (isOwned ? new Color(0.46f, 0.92f, 0.58f, 1f) : new Color(1f, 0.86f, 0.40f, 1f));
            CreateText(card.transform, "Price", font, priceStr, 19, FontStyle.Bold, priceCol,
                TextAnchor.MiddleCenter, new Vector2(0.05f, 0.18f), new Vector2(0.95f, 0.28f), Vector2.zero, Vector2.zero);

            // 底部单个按钮（购买 / 已拥有 / 立即充值）
            string btnLabel = product.isRechargePack ? "立即充值" : (isOwned ? "已拥有" : "购 买");
            Color btnColor = product.isRechargePack ? BtnRechargeColor : (isOwned ? BtnMutedColor : (affordable ? BtnBuyColor : BtnShortColor));

            Button btn = CreateSimpleButton(card.transform, "BuyBtn", font, btnLabel, 22, btnColor,
                new Vector2(0.5f, 0.09f), new Vector2(0.5f, 0.09f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240f, 52f));
            btn.interactable = product.isRechargePack || !isOwned;

            ShopProductData captured = product;
            btn.onClick.AddListener(() =>
            {
                if (captured.isRechargePack)
                {
                    bool ok = ShopManager.TryPurchaseProduct(captured.productId, out string msg);
                    if (ok) UISoundManager.PlaySave();
                    ShowToast(msg, ok);
                }
                else
                {
                    UISoundManager.PlayClick();
                    OpenConfirmModal(captured);
                }
            });
        }

        private void ShowToast(string msg, bool success)
        {
            if (_toastObj == null || _toastText == null) return;
            Image img = _toastObj.GetComponent<Image>();
            if (img != null) img.color = success ? new Color(0.14f, 0.66f, 0.36f, 0.96f) : new Color(0.66f, 0.24f, 0.22f, 0.96f);
            _toastText.text = msg;
            _toastObj.SetActive(true);
            _toastObj.transform.SetAsLastSibling();
            _toastHideTime = Time.unscaledTime + 2.0f;
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

        private Button CreateSimpleButton(Transform parent, string name, Font font, string text, int fontSize, Color bgColor,
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
            AddOutline(txt, 1.2f);
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
