using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PixelSaur.UI
{
    /// <summary>
    /// 开始场景恐龙背包与仓库界面面板：
    /// 1. 分类筛选页签（全部 / 恐龙 / 皮肤 / 道具）；
    /// 2. 基于 ScrollRect + GridLayoutGroup 动态生成背包格子；
    /// 3. 点击任意格子高亮选中并在右侧面板刷新完整详情与属性；
    /// 4. 支持穿戴/出战、卸下与道具使用消耗状态切换；
    /// 5. 改动自动经 InventoryManager 写入本地 JSON 存档并在启动时同步大厅霸王龙预览。
    /// </summary>
    public class InventoryPanelHUD : MonoBehaviour
    {
        private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");

        [Header("基础按钮与面板底图")]
        [SerializeField] private Sprite whitePillSprite;
        [SerializeField] private Sprite buttonPillSprite;

        [Header("物品图标资产")]
        [SerializeField] private Sprite iconDinoSneak;
        [SerializeField] private Sprite iconDinoBite;
        [SerializeField] private Sprite iconDinoJump;
        [SerializeField] private Sprite iconDinoSkill1;
        [SerializeField] private Sprite iconDinoSkill2;
        [SerializeField] private Sprite iconPterosaurRun;
        [SerializeField] private Sprite iconFood;
        [SerializeField] private Sprite iconWater;
        [SerializeField] private Sprite iconHeart;

        private RectTransform _panelRT;
        private Text _loadoutSummaryText;

        private readonly Dictionary<InventoryCategory, Button> _tabButtons = new Dictionary<InventoryCategory, Button>();
        private readonly Dictionary<InventoryCategory, Image> _tabImages = new Dictionary<InventoryCategory, Image>();

        private ScrollRect _scrollRect;
        private RectTransform _gridContent;

        // 右侧详情说明控件
        private Image _detailIconBg;
        private Image _detailIconImg;
        private Image _detailRarityBadgeImg;
        private Text _detailRarityText;
        private Text _detailCategoryText;
        private Text _detailTitleText;
        private Text _detailSubtitleText;
        private Text _detailStatusText;
        private Text _detailStatsText;
        private Text _detailDescText;
        private Text _detailSaveHintText;
        private Button _actionBtn;
        private Image _actionBtnImg;
        private Text _actionBtnText;

        private GameObject _toastObj;
        private Text _toastText;
        private float _toastHideTime;

        private InventoryCategory _activeCategory = InventoryCategory.All;
        private string _selectedItemId = InventoryManager.DefaultDinoId;
        private readonly Dictionary<string, GameObject> _slotSelectionFrames = new Dictionary<string, GameObject>();

        private bool _built;
        private static Sprite _fallbackWhitePill;

        private static readonly Color PanelBgColor = new Color(0.07f, 0.10f, 0.15f, 0.99f);
        private static readonly Color SubPanelBgColor = new Color(0.10f, 0.14f, 0.21f, 0.98f);
        private static readonly Color InsetCardBgColor = new Color(0.07f, 0.10f, 0.16f, 0.98f);
        private static readonly Color SlotNormalBgColor = new Color(0.15f, 0.21f, 0.31f, 1f);
        private static readonly Color SlotSelectedBgColor = new Color(0.22f, 0.34f, 0.48f, 1f);
        private static readonly Color SlotDepletedBgColor = new Color(0.12f, 0.15f, 0.20f, 0.92f);

        private static readonly Color TabActiveColor = new Color(0.16f, 0.64f, 0.36f, 1f);
        private static readonly Color TabNormalColor = new Color(0.18f, 0.24f, 0.33f, 1f);

        private static readonly Color BtnEquipColor = new Color(0.16f, 0.68f, 0.36f, 1f);
        private static readonly Color BtnUnequipColor = new Color(0.68f, 0.44f, 0.16f, 1f);
        private static readonly Color BtnUseColor = new Color(0.18f, 0.48f, 0.78f, 1f);
        private static readonly Color BtnMutedColor = new Color(0.20f, 0.24f, 0.30f, 1f);

        private void Awake()
        {
            LoadFallbackAssets();
            InventoryManager.EnsureInitialized();
            EnsureUI();
        }

        private void OnEnable()
        {
            LoadFallbackAssets();
            InventoryManager.EnsureInitialized();
            EnsureUI();
            InventoryManager.OnInventoryChanged += HandleInventoryChanged;
            RefreshAll(resetScroll: false);
            SyncLobbyDinoPreview();
        }

        private void OnDisable()
        {
            InventoryManager.OnInventoryChanged -= HandleInventoryChanged;
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
                name = "InventoryPanelFallbackPill",
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

        private Sprite ResolveItemSprite(string iconKey)
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

        public static void SyncLobbyDinoPreview()
        {
            GameObject preview = GameObject.Find("LobbyTRexPreview");
            if (preview == null) return;

            InventoryManager.EnsureInitialized();
            float scale = InventoryManager.GetEffectivePreviewScale();
            preview.transform.localScale = Vector3.one * scale;

            Color tint = InventoryManager.GetEffectivePreviewTint();
            bool isDefaultTint = Mathf.Approximately(tint.r, 1f) && Mathf.Approximately(tint.g, 1f) && Mathf.Approximately(tint.b, 1f);

            Renderer[] renderers = preview.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;
                if (isDefaultTint)
                {
                    r.SetPropertyBlock(null);
                }
                else
                {
                    var mpb = new MaterialPropertyBlock();
                    r.GetPropertyBlock(mpb);
                    mpb.SetColor(BaseColorProp, tint);
                    r.SetPropertyBlock(mpb);
                }
            }
        }

        public void OpenDefault()
        {
            _activeCategory = InventoryCategory.All;
            InventoryManager.EnsureInitialized();
            if (string.IsNullOrEmpty(_selectedItemId) || InventoryManager.GetItemById(_selectedItemId) == null)
            {
                _selectedItemId = InventoryManager.EquippedDinoId;
            }

            if (UIPanelManager.Instance != null)
            {
                UIPanelManager.Instance.OpenPanel("InventoryPanel");
            }
            else
            {
                gameObject.SetActive(true);
                transform.SetAsLastSibling();
            }

            RefreshAll(resetScroll: true);
        }

        private void HandleInventoryChanged()
        {
            SyncLobbyDinoPreview();
            if (gameObject.activeInHierarchy)
            {
                RefreshAll(resetScroll: false);
            }
        }

        public void SwitchCategory(InventoryCategory category)
        {
            if (_activeCategory == category) return;
            UISoundManager.PlayClick();
            _activeCategory = category;

            List<InventoryItemData> filtered = InventoryManager.GetItemsByCategory(_activeCategory);
            if (filtered.Count > 0)
            {
                bool stillVisible = false;
                for (int i = 0; i < filtered.Count; i++)
                {
                    if (filtered[i].id == _selectedItemId)
                    {
                        stillVisible = true;
                        break;
                    }
                }
                if (!stillVisible)
                {
                    _selectedItemId = filtered[0].id;
                }
            }

            RefreshAll(resetScroll: true);
        }

        public void SelectItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return;
            if (_selectedItemId != itemId)
            {
                UISoundManager.PlayClick();
                _selectedItemId = itemId;
                UpdateSlotSelectionVisuals();
                RefreshDetailPane();
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
            _slotSelectionFrames.Clear();

            Sprite panelSprite = GetPanelSprite();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            _panelRT = GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
            _panelRT.anchorMin = new Vector2(0.06f, 0.06f);
            _panelRT.anchorMax = new Vector2(0.94f, 0.94f);
            _panelRT.offsetMin = Vector2.zero;
            _panelRT.offsetMax = Vector2.zero;
            _panelRT.pivot = new Vector2(0.5f, 0.5f);

            Image bg = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            bg.sprite = panelSprite;
            bg.type = Image.Type.Sliced;
            bg.color = PanelBgColor;
            bg.raycastTarget = true;

            // 1. 顶部标题、当前出战提示与关闭按钮 X
            CreateText(transform, "Title", font, "恐龙背包 & 仓库", 36, FontStyle.Bold, Color.white,
                TextAnchor.MiddleLeft, new Vector2(0f, 0.87f), new Vector2(0.45f, 1f), new Vector2(36f, 0f), new Vector2(0f, -6f));

            _loadoutSummaryText = CreateText(transform, "LoadoutSummary", font, "当前出战：成年霸王龙 · 原生丛林绿涂装", 20, FontStyle.Bold,
                new Color(0.68f, 0.88f, 1.0f, 1f),
                TextAnchor.MiddleRight, new Vector2(0.38f, 0.88f), new Vector2(0.91f, 0.99f), Vector2.zero, new Vector2(-12f, -4f));

            Button closeBtn = CreateStyledButton(transform, "InventoryCloseBtn", font, "✕", 32, new Color(0.46f, 0.18f, 0.20f, 1f),
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -18f), new Vector2(68f, 68f));
            closeBtn.onClick.AddListener(() =>
            {
                if (UIPanelManager.Instance != null) UIPanelManager.Instance.ClosePanel("InventoryPanel");
                else { UISoundManager.PlayClose(); gameObject.SetActive(false); }
            });

            // 2. 分类筛选页签（全部 / 恐龙 / 皮肤 / 道具）+ 右侧重置按钮
            BuildTabButton(font, InventoryCategory.All, "TabAllBtn", "全部", 36f);
            BuildTabButton(font, InventoryCategory.Dino, "TabDinoBtn", "恐龙", 212f);
            BuildTabButton(font, InventoryCategory.Skin, "TabSkinBtn", "皮肤", 388f);
            BuildTabButton(font, InventoryCategory.Item, "TabItemBtn", "道具", 564f);

            Button resetBtn = CreateStyledButton(transform, "ResetInventoryBtn", font, "重置库存", 21,
                new Color(0.24f, 0.28f, 0.36f, 1f),
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-36f, -104f), new Vector2(168f, 58f));
            resetBtn.onClick.AddListener(() =>
            {
                UISoundManager.PlayClick();
                InventoryManager.ResetInventoryData();
                ShowToast("✓ 已恢复背包初始默认数据并写入本地存档！");
            });

            // 3. 左侧动态网格背包区（ScrollRect + GridLayoutGroup）
            GameObject gridSection = CreateCard(transform, "GridSection", panelSprite, SubPanelBgColor);
            RectTransform gridSecRT = gridSection.GetComponent<RectTransform>();
            gridSecRT.anchorMin = new Vector2(0.03f, 0.05f);
            gridSecRT.anchorMax = new Vector2(0.60f, 0.77f);
            gridSecRT.offsetMin = Vector2.zero;
            gridSecRT.offsetMax = Vector2.zero;

            GameObject scrollGo = new GameObject("InventoryScrollView", typeof(RectTransform), typeof(ScrollRect));
            scrollGo.transform.SetParent(gridSection.transform, false);
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
            grid.padding = new RectOffset(8, 8, 8, 8);
            grid.cellSize = new Vector2(214f, 206f);
            grid.spacing = new Vector2(12f, 12f);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;

            _scrollRect = scrollGo.GetComponent<ScrollRect>();
            _scrollRect.viewport = vpRT;
            _scrollRect.content = _gridContent;
            _scrollRect.horizontal = false;
            _scrollRect.vertical = true;
            _scrollRect.inertia = true;
            _scrollRect.scrollSensitivity = 28f;
            _scrollRect.movementType = ScrollRect.MovementType.Clamped;

            // 4. 右侧详情说明区
            BuildDetailSection(panelSprite, font);

            // 5. 底部 Toast 反馈横幅
            _toastObj = CreateCard(transform, "InventoryToast", panelSprite, new Color(0.14f, 0.66f, 0.36f, 0.98f));
            RectTransform toastRT = _toastObj.GetComponent<RectTransform>();
            toastRT.anchorMin = new Vector2(0.5f, 0f);
            toastRT.anchorMax = new Vector2(0.5f, 0f);
            toastRT.pivot = new Vector2(0.5f, 0f);
            toastRT.anchoredPosition = new Vector2(0f, 18f);
            toastRT.sizeDelta = new Vector2(620f, 54f);
            _toastText = CreateText(_toastObj.transform, "Text", font, "", 22, FontStyle.Bold, Color.white,
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(16f, 0f), new Vector2(-16f, 0f));
            _toastObj.SetActive(false);
        }

        private void BuildTabButton(Font font, InventoryCategory category, string name, string label, float posX)
        {
            Button btn = CreateStyledButton(transform, name, font, label, 24, TabNormalColor,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(posX, -104f), new Vector2(160f, 58f));
            Image img = btn.GetComponent<Image>();
            _tabButtons[category] = btn;
            _tabImages[category] = img;

            InventoryCategory captured = category;
            btn.onClick.AddListener(() => SwitchCategory(captured));
        }

        private void BuildDetailSection(Sprite panelSprite, Font font)
        {
            GameObject detailSection = CreateCard(transform, "DetailSection", panelSprite, SubPanelBgColor);
            RectTransform dRT = detailSection.GetComponent<RectTransform>();
            dRT.anchorMin = new Vector2(0.62f, 0.05f);
            dRT.anchorMax = new Vector2(0.97f, 0.77f);
            dRT.offsetMin = Vector2.zero;
            dRT.offsetMax = Vector2.zero;

            // 4.1 头部图标展示框 + 名称 + 稀有度标签
            _detailIconBg = CreateCard(detailSection.transform, "DetailIconBox", panelSprite, InsetCardBgColor).GetComponent<Image>();
            RectTransform iconBoxRT = _detailIconBg.rectTransform;
            iconBoxRT.anchorMin = new Vector2(0.04f, 0.75f);
            iconBoxRT.anchorMax = new Vector2(0.27f, 0.96f);
            iconBoxRT.offsetMin = Vector2.zero;
            iconBoxRT.offsetMax = Vector2.zero;

            GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(_detailIconBg.transform, false);
            RectTransform iconRT = iconGo.GetComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0.14f, 0.14f);
            iconRT.anchorMax = new Vector2(0.86f, 0.86f);
            iconRT.offsetMin = Vector2.zero;
            iconRT.offsetMax = Vector2.zero;
            _detailIconImg = iconGo.GetComponent<Image>();
            _detailIconImg.preserveAspect = true;
            _detailIconImg.raycastTarget = false;

            GameObject rarityGo = CreateCard(detailSection.transform, "RarityBadge", panelSprite, new Color(0.95f, 0.68f, 0.20f, 1f));
            RectTransform rarRT = rarityGo.GetComponent<RectTransform>();
            rarRT.anchorMin = new Vector2(0.30f, 0.90f);
            rarRT.anchorMax = new Vector2(0.47f, 0.96f);
            rarRT.offsetMin = Vector2.zero;
            rarRT.offsetMax = Vector2.zero;
            _detailRarityBadgeImg = rarityGo.GetComponent<Image>();
            _detailRarityText = CreateText(rarityGo.transform, "Text", font, "传说", 18, FontStyle.Bold, Color.white,
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddOutline(_detailRarityText, 1f);

            GameObject catGo = CreateCard(detailSection.transform, "CategoryBadge", panelSprite, new Color(0.18f, 0.26f, 0.38f, 1f));
            RectTransform catRT = catGo.GetComponent<RectTransform>();
            catRT.anchorMin = new Vector2(0.49f, 0.90f);
            catRT.anchorMax = new Vector2(0.68f, 0.96f);
            catRT.offsetMin = Vector2.zero;
            catRT.offsetMax = Vector2.zero;
            _detailCategoryText = CreateText(catGo.transform, "Text", font, "恐龙", 18, FontStyle.Bold,
                new Color(0.82f, 0.92f, 1f, 1f),
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            _detailTitleText = CreateText(detailSection.transform, "DetailTitle", font, "物品名称", 29, FontStyle.Bold, Color.white,
                TextAnchor.MiddleLeft, new Vector2(0.30f, 0.80f), new Vector2(0.96f, 0.90f), Vector2.zero, Vector2.zero);
            AddOutline(_detailTitleText, 1.2f);

            _detailSubtitleText = CreateText(detailSection.transform, "DetailSubtitle", font, "副标题", 19, FontStyle.Bold,
                new Color(0.66f, 0.86f, 1f, 1f),
                TextAnchor.MiddleLeft, new Vector2(0.30f, 0.74f), new Vector2(0.96f, 0.81f), Vector2.zero, Vector2.zero);

            // 4.2 属性与状态卡
            GameObject statsCard = CreateCard(detailSection.transform, "StatsCard", panelSprite, InsetCardBgColor);
            RectTransform statsRT = statsCard.GetComponent<RectTransform>();
            statsRT.anchorMin = new Vector2(0.04f, 0.55f);
            statsRT.anchorMax = new Vector2(0.96f, 0.72f);
            statsRT.offsetMin = Vector2.zero;
            statsRT.offsetMax = Vector2.zero;

            _detailStatusText = CreateText(statsCard.transform, "StatusText", font, "当前状态：已出战", 20, FontStyle.Bold,
                new Color(0.42f, 0.96f, 0.56f, 1f),
                TextAnchor.MiddleLeft, new Vector2(0f, 0.52f), new Vector2(1f, 1f), new Vector2(18f, 0f), new Vector2(-18f, -4f));

            _detailStatsText = CreateText(statsCard.transform, "StatsText", font, "属性参数", 19, FontStyle.Bold,
                new Color(0.96f, 0.86f, 0.44f, 1f),
                TextAnchor.MiddleLeft, new Vector2(0f, 0f), new Vector2(1f, 0.52f), new Vector2(18f, 4f), new Vector2(-18f, 0f));

            // 4.3 详细描述区
            GameObject descCard = CreateCard(detailSection.transform, "DescCard", panelSprite, InsetCardBgColor);
            RectTransform descRT = descCard.GetComponent<RectTransform>();
            descRT.anchorMin = new Vector2(0.04f, 0.19f);
            descRT.anchorMax = new Vector2(0.96f, 0.53f);
            descRT.offsetMin = Vector2.zero;
            descRT.offsetMax = Vector2.zero;

            CreateText(descCard.transform, "DescHeader", font, "详情说明", 20, FontStyle.Bold,
                new Color(0.66f, 0.84f, 1f, 1f),
                TextAnchor.MiddleLeft, new Vector2(0f, 0.76f), new Vector2(1f, 1f), new Vector2(18f, 0f), new Vector2(-18f, -4f));

            _detailDescText = CreateText(descCard.transform, "DescBody", font, "请选择左侧背包格子查看详情。", 20, FontStyle.Normal,
                new Color(0.86f, 0.91f, 0.96f, 1f),
                TextAnchor.UpperLeft, new Vector2(0f, 0.16f), new Vector2(1f, 0.76f), new Vector2(18f, 4f), new Vector2(-18f, -4f));
            _detailDescText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _detailDescText.verticalOverflow = VerticalWrapMode.Truncate;
            _detailDescText.lineSpacing = 1.24f;

            _detailSaveHintText = CreateText(descCard.transform, "SaveHint", font, "本地数据：已同步保存，重启游戏不丢失", 15, FontStyle.Bold,
                new Color(0.54f, 0.66f, 0.78f, 0.95f),
                TextAnchor.MiddleLeft, new Vector2(0f, 0f), new Vector2(1f, 0.16f), new Vector2(18f, 2f), new Vector2(-18f, 0f));

            // 4.4 底部穿戴/卸下/使用交互按钮
            _actionBtn = CreateStyledButton(detailSection.transform, "DetailActionBtn", font, "穿 戴", 28, BtnEquipColor,
                new Vector2(0.04f, 0.03f), new Vector2(0.96f, 0.16f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            RectTransform actRT = _actionBtn.GetComponent<RectTransform>();
            actRT.anchorMin = new Vector2(0.04f, 0.03f);
            actRT.anchorMax = new Vector2(0.96f, 0.16f);
            actRT.offsetMin = Vector2.zero;
            actRT.offsetMax = Vector2.zero;
            _actionBtnImg = _actionBtn.GetComponent<Image>();
            _actionBtnText = _actionBtn.GetComponentInChildren<Text>();
            _actionBtn.onClick.AddListener(HandleActionButtonClicked);
        }

        private void HandleActionButtonClicked()
        {
            if (string.IsNullOrEmpty(_selectedItemId)) return;

            bool ok = InventoryManager.EquipOrUseItem(_selectedItemId, out string msg);
            if (ok) UISoundManager.PlaySave();
            else UISoundManager.PlayClick();

            ShowToast(msg);
        }

        public void RefreshAll(bool resetScroll)
        {
            if (!_built) EnsureUI();

            // 1. 刷新顶部页签颜色与数量统计
            UpdateTabVisual(InventoryCategory.All, "全部");
            UpdateTabVisual(InventoryCategory.Dino, "恐龙");
            UpdateTabVisual(InventoryCategory.Skin, "皮肤");
            UpdateTabVisual(InventoryCategory.Item, "道具");

            if (_loadoutSummaryText != null)
            {
                InventoryItemData eqDino = InventoryManager.GetEquippedDino();
                InventoryItemData eqSkin = InventoryManager.GetEquippedSkin();
                string dName = eqDino != null ? eqDino.name : "成年霸王龙";
                string sName = eqSkin != null ? eqSkin.name : "默认涂装";
                _loadoutSummaryText.text = $"当前出战：{dName}  ·  皮肤：{sName}";
            }

            // 2. 动态生成左侧网格格子
            for (int i = _gridContent.childCount - 1; i >= 0; i--)
            {
                Destroy(_gridContent.GetChild(i).gameObject);
            }
            _slotSelectionFrames.Clear();

            List<InventoryItemData> items = InventoryManager.GetItemsByCategory(_activeCategory);
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Sprite panelSprite = GetPanelSprite();

            if (items.Count > 0)
            {
                bool hasSelected = false;
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i].id == _selectedItemId)
                    {
                        hasSelected = true;
                        break;
                    }
                }
                if (!hasSelected) _selectedItemId = items[0].id;
            }

            for (int i = 0; i < items.Count; i++)
            {
                BuildGridSlot(items[i], font, panelSprite);
            }

            int rows = Mathf.CeilToInt(items.Count / 4f);
            float contentHeight = 16f + rows * 206f + Mathf.Max(0, rows - 1) * 12f;
            _gridContent.sizeDelta = new Vector2(0f, Mathf.Max(220f, contentHeight));

            Canvas.ForceUpdateCanvases();
            if (resetScroll && _scrollRect != null)
            {
                _scrollRect.velocity = Vector2.zero;
                _scrollRect.verticalNormalizedPosition = 1f;
            }

            // 3. 刷新选中状态与右侧详情面板
            UpdateSlotSelectionVisuals();
            RefreshDetailPane();
        }

        private void UpdateTabVisual(InventoryCategory cat, string label)
        {
            if (_tabImages.TryGetValue(cat, out Image img) && img != null)
            {
                img.color = _activeCategory == cat ? TabActiveColor : TabNormalColor;
            }
            if (_tabButtons.TryGetValue(cat, out Button btn) && btn != null)
            {
                Text txt = btn.GetComponentInChildren<Text>();
                if (txt != null)
                {
                    int count = InventoryManager.GetItemsByCategory(cat).Count;
                    txt.text = $"{label} ({count})";
                }
            }
        }

        private void BuildGridSlot(InventoryItemData item, Font font, Sprite panelSprite)
        {
            bool isEquipped = InventoryManager.IsItemEquipped(item);
            bool isDepleted = item.category == InventoryCategory.Item && item.quantity <= 0;
            bool isSelected = item.id == _selectedItemId;

            Color bgTint = isDepleted ? SlotDepletedBgColor : (isSelected ? SlotSelectedBgColor : SlotNormalBgColor);

            GameObject slotGo = new GameObject("Slot_" + item.id, typeof(RectTransform), typeof(Image), typeof(Button));
            slotGo.transform.SetParent(_gridContent, false);

            Image slotBg = slotGo.GetComponent<Image>();
            slotBg.sprite = buttonPillSprite != null ? buttonPillSprite : panelSprite;
            slotBg.type = Image.Type.Sliced;
            slotBg.color = bgTint;

            Button btn = slotGo.GetComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            cb.pressedColor = new Color(0.70f, 0.85f, 1.0f, 1f);
            cb.selectedColor = Color.white;
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.1f;
            btn.colors = cb;

            string capturedId = item.id;
            btn.onClick.AddListener(() => SelectItem(capturedId));

            // 选中高亮边框层
            GameObject selFrame = CreateCard(slotGo.transform, "SelectedHighlight", panelSprite, new Color(0.32f, 0.92f, 0.56f, 0.26f));
            RectTransform sfRT = selFrame.GetComponent<RectTransform>();
            sfRT.anchorMin = Vector2.zero;
            sfRT.anchorMax = Vector2.one;
            sfRT.offsetMin = new Vector2(4f, 4f);
            sfRT.offsetMax = new Vector2(-4f, -4f);
            selFrame.GetComponent<Image>().raycastTarget = false;
            selFrame.SetActive(isSelected);
            _slotSelectionFrames[item.id] = selFrame;

            // 左上角分类/稀有度徽章
            GameObject catTag = CreateCard(slotGo.transform, "CategoryTag", panelSprite, item.GetRarityColor());
            RectTransform ctRT = catTag.GetComponent<RectTransform>();
            ctRT.anchorMin = new Vector2(0.06f, 0.81f);
            ctRT.anchorMax = new Vector2(0.40f, 0.94f);
            ctRT.offsetMin = Vector2.zero;
            ctRT.offsetMax = Vector2.zero;
            catTag.GetComponent<Image>().raycastTarget = false;
            Text ctTxt = CreateText(catTag.transform, "Text", font, item.GetRarityLabel(), 15, FontStyle.Bold, Color.white,
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddOutline(ctTxt, 1f);

            // 右上角状态角标（出战中 / 已穿戴 / x数量）
            if (isEquipped)
            {
                string eqLabel = item.category == InventoryCategory.Dino ? "出战中" : "已穿戴";
                GameObject eqTag = CreateCard(slotGo.transform, "EquippedTag", panelSprite, new Color(0.14f, 0.72f, 0.34f, 1f));
                RectTransform eqRT = eqTag.GetComponent<RectTransform>();
                eqRT.anchorMin = new Vector2(0.50f, 0.81f);
                eqRT.anchorMax = new Vector2(0.94f, 0.94f);
                eqRT.offsetMin = Vector2.zero;
                eqRT.offsetMax = Vector2.zero;
                eqTag.GetComponent<Image>().raycastTarget = false;
                Text eqTxt = CreateText(eqTag.transform, "Text", font, eqLabel, 15, FontStyle.Bold, Color.white,
                    TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                AddOutline(eqTxt, 1f);
            }
            else if (item.category == InventoryCategory.Item)
            {
                Color qtyBg = item.quantity > 0 ? new Color(0.16f, 0.40f, 0.68f, 1f) : new Color(0.32f, 0.20f, 0.22f, 1f);
                GameObject qtyTag = CreateCard(slotGo.transform, "QuantityTag", panelSprite, qtyBg);
                RectTransform qRT = qtyTag.GetComponent<RectTransform>();
                qRT.anchorMin = new Vector2(0.58f, 0.81f);
                qRT.anchorMax = new Vector2(0.94f, 0.94f);
                qRT.offsetMin = Vector2.zero;
                qRT.offsetMax = Vector2.zero;
                qtyTag.GetComponent<Image>().raycastTarget = false;
                Text qTxt = CreateText(qtyTag.transform, "Text", font, $"x{item.quantity}", 16, FontStyle.Bold, Color.white,
                    TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                AddOutline(qTxt, 1f);
            }

            // 中央图标底座 + 像素图标
            Color pedestalTint = new Color(item.accentColor.r * 0.35f, item.accentColor.g * 0.35f, item.accentColor.b * 0.35f, 0.90f);
            GameObject pedestal = CreateCard(slotGo.transform, "IconPedestal", panelSprite, pedestalTint);
            RectTransform pedRT = pedestal.GetComponent<RectTransform>();
            pedRT.anchorMin = new Vector2(0.24f, 0.30f);
            pedRT.anchorMax = new Vector2(0.76f, 0.76f);
            pedRT.offsetMin = Vector2.zero;
            pedRT.offsetMax = Vector2.zero;
            pedestal.GetComponent<Image>().raycastTarget = false;

            GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(pedestal.transform, false);
            RectTransform iconRT = iconGo.GetComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0.14f, 0.14f);
            iconRT.anchorMax = new Vector2(0.86f, 0.86f);
            iconRT.offsetMin = Vector2.zero;
            iconRT.offsetMax = Vector2.zero;
            Image iconImg = iconGo.GetComponent<Image>();
            iconImg.sprite = ResolveItemSprite(item.iconKey);
            iconImg.preserveAspect = true;
            iconImg.color = isDepleted ? new Color(0.55f, 0.55f, 0.55f, 0.7f) : Color.white;
            iconImg.raycastTarget = false;

            // 底部物品名称
            Text nameTxt = CreateText(slotGo.transform, "ItemName", font, item.name, 18, FontStyle.Bold,
                isDepleted ? new Color(0.64f, 0.68f, 0.74f, 0.9f) : Color.white,
                TextAnchor.MiddleCenter, new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.27f), Vector2.zero, Vector2.zero);
            AddOutline(nameTxt, 1.2f);
        }

        private void UpdateSlotSelectionVisuals()
        {
            foreach (var kv in _slotSelectionFrames)
            {
                bool selected = kv.Key == _selectedItemId;
                if (kv.Value != null)
                {
                    kv.Value.SetActive(selected);
                    Transform slotTransform = kv.Value.transform.parent;
                    if (slotTransform != null)
                    {
                        Image bg = slotTransform.GetComponent<Image>();
                        InventoryItemData item = InventoryManager.GetItemById(kv.Key);
                        if (bg != null && item != null)
                        {
                            bool depleted = item.category == InventoryCategory.Item && item.quantity <= 0;
                            bg.color = depleted ? SlotDepletedBgColor : (selected ? SlotSelectedBgColor : SlotNormalBgColor);
                        }
                    }
                }
            }
        }

        private void RefreshDetailPane()
        {
            InventoryItemData item = InventoryManager.GetItemById(_selectedItemId);
            if (item == null)
            {
                List<InventoryItemData> items = InventoryManager.GetItemsByCategory(_activeCategory);
                if (items.Count > 0)
                {
                    item = items[0];
                    _selectedItemId = item.id;
                }
            }
            if (item == null) return;

            bool isEquipped = InventoryManager.IsItemEquipped(item);

            if (_detailIconImg != null)
            {
                _detailIconImg.sprite = ResolveItemSprite(item.iconKey);
                _detailIconImg.color = (item.category == InventoryCategory.Item && item.quantity <= 0)
                    ? new Color(0.55f, 0.55f, 0.55f, 0.7f)
                    : Color.white;
            }
            if (_detailIconBg != null)
            {
                _detailIconBg.color = new Color(item.accentColor.r * 0.30f, item.accentColor.g * 0.30f, item.accentColor.b * 0.30f, 0.98f);
            }
            if (_detailRarityBadgeImg != null) _detailRarityBadgeImg.color = item.GetRarityColor();
            if (_detailRarityText != null) _detailRarityText.text = item.GetRarityLabel();
            if (_detailCategoryText != null) _detailCategoryText.text = item.GetCategoryLabel();
            if (_detailTitleText != null) _detailTitleText.text = item.name;
            if (_detailSubtitleText != null)
            {
                _detailSubtitleText.text = item.subtitle;
                _detailSubtitleText.color = Color.Lerp(item.accentColor, Color.white, 0.42f);
            }
            if (_detailStatsText != null) _detailStatsText.text = item.statsSummary;
            if (_detailDescText != null) _detailDescText.text = item.description;
            if (_detailSaveHintText != null)
            {
                _detailSaveHintText.text = $"本地存档：已启用 JSON + PlayerPrefs 持久化";
            }

            if (item.category == InventoryCategory.Dino)
            {
                if (_detailStatusText != null)
                {
                    _detailStatusText.text = isEquipped ? "状态：当前正在大厅与岛屿出战中" : "状态：已解锁（可随时切换出战）";
                    _detailStatusText.color = isEquipped ? new Color(0.42f, 0.96f, 0.56f, 1f) : new Color(0.76f, 0.86f, 0.96f, 1f);
                }
                SetActionButtonState(
                    isEquipped ? "已 出 战" : "穿 戴 出 战",
                    isEquipped ? BtnMutedColor : BtnEquipColor,
                    interactable: !isEquipped);
            }
            else if (item.category == InventoryCategory.Skin)
            {
                if (isEquipped)
                {
                    bool isDefault = item.id == InventoryManager.DefaultSkinId;
                    if (_detailStatusText != null)
                    {
                        _detailStatusText.text = isDefault ? "状态：当前已穿戴原生默认皮肤" : "状态：当前已穿戴此定制皮肤（可点击卸下）";
                        _detailStatusText.color = new Color(0.42f, 0.96f, 0.56f, 1f);
                    }
                    SetActionButtonState(
                        isDefault ? "已穿戴（默认）" : "卸 下 皮 肤",
                        isDefault ? BtnMutedColor : BtnUnequipColor,
                        interactable: !isDefault);
                }
                else
                {
                    if (_detailStatusText != null)
                    {
                        _detailStatusText.text = "状态：未穿戴（点击穿戴立即生效）";
                        _detailStatusText.color = new Color(0.76f, 0.86f, 0.96f, 1f);
                    }
                    SetActionButtonState("穿 戴 皮 肤", BtnEquipColor, interactable: true);
                }
            }
            else
            {
                bool hasStock = item.quantity > 0;
                if (_detailStatusText != null)
                {
                    _detailStatusText.text = hasStock ? $"当前库存：{item.quantity} 件（点击使用立即生效）" : "当前库存：0 件（已耗尽）";
                    _detailStatusText.color = hasStock ? new Color(0.44f, 0.84f, 1f, 1f) : new Color(0.72f, 0.56f, 0.56f, 1f);
                }
                SetActionButtonState(
                    hasStock ? "立 即 使 用" : "已 耗 尽",
                    hasStock ? BtnUseColor : BtnMutedColor,
                    interactable: hasStock);
            }
        }

        private void SetActionButtonState(string label, Color bgColor, bool interactable)
        {
            if (_actionBtn == null) return;
            _actionBtn.interactable = interactable;
            if (_actionBtnImg != null) _actionBtnImg.color = bgColor;
            if (_actionBtnText != null)
            {
                _actionBtnText.text = label;
                _actionBtnText.color = interactable ? Color.white : new Color(0.68f, 0.72f, 0.78f, 0.92f);
            }
        }

        private void ShowToast(string msg)
        {
            if (_toastObj == null || _toastText == null) return;
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
