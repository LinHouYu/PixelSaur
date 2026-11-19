using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PixelSaur.UI
{
    /// <summary>
    /// 极简原型恐龙仓库面板：
    /// 1. 顶部仅保留【恐龙 / 皮肤】页签切换、重置与关闭；
    /// 2. 中间采用 4×4 = 16 个大尺寸方格展示已拥有马赛克恐龙/皮肤与空槽位；
    /// 3. 底部配备【进游戏出战恐龙小方格】与【当前皮肤小方格】，支持从上方仓库拖拽马赛克恐龙移入下方方格（或直接点击）完成选角色与换肤。
    /// </summary>
    public class InventoryPanelHUD : MonoBehaviour
    {
        private const int GridColumns = 4;
        private const int GridRowsPerPage = 4;
        private const int SlotsPerPage = GridColumns * GridRowsPerPage; // 4 * 4 = 16

        private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");
        private static readonly int BaseMapProp = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexProp = Shader.PropertyToID("_MainTex");

        [Header("基础按钮与面板底图")]
        [SerializeField] private Sprite whitePillSprite;
        [SerializeField] private Sprite buttonPillSprite;

        [Header("兼容保留图标引用")]
        [SerializeField] private Sprite iconDinoSneak;
        [SerializeField] private Sprite iconDinoBite;
        [SerializeField] private Sprite iconDinoJump;
        [SerializeField] private Sprite iconDinoSkill1;
        [SerializeField] private Sprite iconDinoSkill2;
        [SerializeField] private Sprite iconPterosaurRun;
        [SerializeField] private Sprite iconFood;
        [SerializeField] private Sprite iconWater;
        [SerializeField] private Sprite iconHeart;

        private readonly Dictionary<InventoryCategory, Button> _tabButtons = new Dictionary<InventoryCategory, Button>();
        private readonly Dictionary<InventoryCategory, Image> _tabImages = new Dictionary<InventoryCategory, Image>();

        private RectTransform _gridContent;

        // 底部出战小方格与皮肤小方格（拖拽目标）
        private RectTransform _selectedDinoSlotRT;
        private Image _selectedDinoSlotBg;
        private Image _selectedDinoIcon;
        private Text _selectedDinoNameText;

        private RectTransform _selectedSkinSlotRT;
        private Image _selectedSkinSlotBg;
        private Image _selectedSkinIcon;
        private Text _selectedSkinNameText;

        // 拖拽时的悬浮马赛克幽灵图标
        private RectTransform _dragGhostRT;
        private Image _dragGhostImg;
        private Canvas _rootCanvas;

        private GameObject _toastObj;
        private Text _toastText;
        private float _toastHideTime;

        private InventoryCategory _activeCategory = InventoryCategory.Dino;
        private bool _built;
        private static Sprite _fallbackWhitePill;

        private static readonly Color PanelBgColor = new Color(0.08f, 0.10f, 0.14f, 0.98f);
        private static readonly Color GridBgColor = new Color(0.11f, 0.14f, 0.20f, 0.96f);
        private static readonly Color SlotOwnedBgColor = new Color(0.17f, 0.22f, 0.30f, 1f);
        private static readonly Color SlotEquippedBgColor = new Color(0.16f, 0.42f, 0.28f, 1f);
        private static readonly Color SlotEmptyBgColor = new Color(0.09f, 0.11f, 0.16f, 0.85f);

        private static readonly Color DockSlotNormalColor = new Color(0.16f, 0.24f, 0.34f, 1f);
        private static readonly Color DockSlotHighlightColor = new Color(0.20f, 0.68f, 0.40f, 1f);

        private static readonly Color TabActiveColor = new Color(0.16f, 0.64f, 0.36f, 1f);
        private static readonly Color TabNormalColor = new Color(0.18f, 0.23f, 0.32f, 1f);

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
            RefreshAll();
            SyncLobbyDinoPreview();
        }

        private void OnDisable()
        {
            InventoryManager.OnInventoryChanged -= HandleInventoryChanged;
            EndDragVisual();
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
#endif
        }

        private Sprite GetPanelSprite()
        {
            if (whitePillSprite != null) return whitePillSprite;
            if (_fallbackWhitePill != null) return _fallbackWhitePill;

            const int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "WarehouseFallbackRect",
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

        private Sprite ResolveMosaicSprite(InventoryItemData item)
        {
            if (item == null) return iconDinoSneak;
            Sprite mosaic = InventoryManager.GetItemMosaicSprite(item.id);
            return mosaic != null ? mosaic : iconDinoSneak;
        }

        public static void SyncLobbyDinoPreview()
        {
            GameObject preview = GameObject.Find("LobbyTRexPreview");
            if (preview == null) return;

            InventoryManager.EnsureInitialized();
            float scale = InventoryManager.GetEffectivePreviewScale();
            preview.transform.localScale = Vector3.one * scale;

            Texture2D skinTex = InventoryManager.GetEffectiveSkinTexture();

            Renderer[] renderers = preview.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;
                if (skinTex == null)
                {
                    r.SetPropertyBlock(null);
                }
                else
                {
                    var mpb = new MaterialPropertyBlock();
                    r.GetPropertyBlock(mpb);
                    mpb.SetTexture(BaseMapProp, skinTex);
                    mpb.SetTexture(MainTexProp, skinTex);
                    mpb.SetColor(BaseColorProp, Color.white);
                    r.SetPropertyBlock(mpb);
                }
            }
        }

        public static bool OpenGlobalWarehouse()
        {
            if (UIPanelManager.Instance != null)
            {
                UIPanelManager.Instance.OpenPanel("InventoryPanel");
                return true;
            }

            InventoryPanelHUD[] all = Resources.FindObjectsOfTypeAll<InventoryPanelHUD>();
            InventoryPanelHUD target = null;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].gameObject.scene.IsValid() && all[i].gameObject.scene.isLoaded)
                {
                    target = all[i];
                    break;
                }
            }

            if (target == null)
            {
                Canvas activeCanvas = FindFirstObjectByType<Canvas>();
                if (activeCanvas == null) return false;
                GameObject go = new GameObject("InventoryPanel", typeof(RectTransform), typeof(InventoryPanelHUD));
                go.transform.SetParent(activeCanvas.transform, false);
                target = go.GetComponent<InventoryPanelHUD>();
            }

            target.OpenDefault();
            return true;
        }

        public void OpenDefault()
        {
            _activeCategory = InventoryCategory.Dino;
            InventoryManager.EnsureInitialized();

            if (UIPanelManager.Instance != null)
            {
                UIPanelManager.Instance.OpenPanel("InventoryPanel");
            }
            else
            {
                gameObject.SetActive(true);
                transform.SetAsLastSibling();
            }

            RefreshAll();
        }

        private void HandleInventoryChanged()
        {
            SyncLobbyDinoPreview();
            if (gameObject.activeInHierarchy)
            {
                RefreshAll();
            }
        }

        public void SwitchCategory(InventoryCategory category)
        {
            if (_activeCategory == category) return;
            UISoundManager.PlayClick();
            _activeCategory = category;
            RefreshAll();
        }

        public void EnsureUI()
        {
            if (_built) return;
            _built = true;

            LoadFallbackAssets();
            _rootCanvas = GetComponentInParent<Canvas>();

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }
            _tabButtons.Clear();
            _tabImages.Clear();

            Sprite panelSprite = GetPanelSprite();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            RectTransform panelRT = GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.04f, 0.03f);
            panelRT.anchorMax = new Vector2(0.96f, 0.97f);
            panelRT.offsetMin = Vector2.zero;
            panelRT.offsetMax = Vector2.zero;
            panelRT.pivot = new Vector2(0.5f, 0.5f);

            Image bg = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            bg.sprite = panelSprite;
            bg.type = Image.Type.Sliced;
            bg.color = PanelBgColor;
            bg.raycastTarget = true;

            // 1. 极简顶部栏：标题 + 【恐龙 / 皮肤】页签 + 重置 + 关闭
            CreateText(transform, "Title", font, "恐龙仓库", 34, FontStyle.Bold, Color.white,
                TextAnchor.MiddleLeft, new Vector2(0f, 0.91f), new Vector2(0.20f, 0.99f), new Vector2(28f, 0f), Vector2.zero);

            BuildTabButton(font, InventoryCategory.Dino, "TabDinoBtn", "恐 龙", 240f);
            BuildTabButton(font, InventoryCategory.Skin, "TabSkinBtn", "皮 肤", 420f);

            Button resetBtn = CreateSimpleButton(transform, "ResetInventoryBtn", font, "重置", 20, TabNormalColor,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-104f, -16f), new Vector2(110f, 54f));
            resetBtn.onClick.AddListener(() =>
            {
                UISoundManager.PlayClick();
                InventoryManager.ResetInventoryData();
                ShowToast("✓ 仓库已重置");
            });

            Button closeBtn = CreateSimpleButton(transform, "InventoryCloseBtn", font, "✕", 28, new Color(0.46f, 0.18f, 0.20f, 1f),
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-22f, -16f), new Vector2(62f, 54f));
            closeBtn.onClick.AddListener(() =>
            {
                if (UIPanelManager.Instance != null) UIPanelManager.Instance.ClosePanel("InventoryPanel");
                else { UISoundManager.PlayClose(); gameObject.SetActive(false); }
            });

            // 2. 中间 4×4 = 16 个大尺寸方格区域
            GameObject gridBox = CreateCard(transform, "WarehouseGridSection", panelSprite, GridBgColor);
            RectTransform gridBoxRT = gridBox.GetComponent<RectTransform>();
            gridBoxRT.anchorMin = new Vector2(0.02f, 0.22f);
            gridBoxRT.anchorMax = new Vector2(0.98f, 0.90f);
            gridBoxRT.offsetMin = Vector2.zero;
            gridBoxRT.offsetMax = Vector2.zero;

            GameObject contentGo = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup));
            contentGo.transform.SetParent(gridBox.transform, false);
            _gridContent = contentGo.GetComponent<RectTransform>();
            _gridContent.anchorMin = Vector2.zero;
            _gridContent.anchorMax = Vector2.one;
            _gridContent.offsetMin = new Vector2(14f, 12f);
            _gridContent.offsetMax = new Vector2(-14f, -12f);

            GridLayoutGroup grid = contentGo.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(400f, 154f);
            grid.spacing = new Vector2(12f, 12f);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.MiddleCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = GridColumns;

            // 3. 底部选角区：进游戏出战恐龙小方格 + 当前皮肤小方格 + 简要提示
            BuildBottomSelectionDock(panelSprite, font);

            // 4. 拖拽悬浮幽灵图标（最上层，不拦截射线）
            GameObject ghostGo = new GameObject("DragGhostIcon", typeof(RectTransform), typeof(Image));
            ghostGo.transform.SetParent(transform, false);
            _dragGhostRT = ghostGo.GetComponent<RectTransform>();
            _dragGhostRT.sizeDelta = new Vector2(132f, 132f);
            _dragGhostImg = ghostGo.GetComponent<Image>();
            _dragGhostImg.preserveAspect = true;
            _dragGhostImg.raycastTarget = false;
            ghostGo.SetActive(false);

            // 5. 底部轻量 Toast
            _toastObj = CreateCard(transform, "InventoryToast", panelSprite, new Color(0.14f, 0.66f, 0.36f, 0.96f));
            RectTransform toastRT = _toastObj.GetComponent<RectTransform>();
            toastRT.anchorMin = new Vector2(0.5f, 1f);
            toastRT.anchorMax = new Vector2(0.5f, 1f);
            toastRT.pivot = new Vector2(0.5f, 1f);
            toastRT.anchoredPosition = new Vector2(0f, -14f);
            toastRT.sizeDelta = new Vector2(460f, 48f);
            _toastText = CreateText(_toastObj.transform, "Text", font, "", 20, FontStyle.Bold, Color.white,
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _toastObj.SetActive(false);
        }

        private void BuildTabButton(Font font, InventoryCategory category, string name, string label, float posX)
        {
            Button btn = CreateSimpleButton(transform, name, font, label, 22, TabNormalColor,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(posX, -16f), new Vector2(164f, 54f));
            _tabButtons[category] = btn;
            _tabImages[category] = btn.GetComponent<Image>();
            InventoryCategory captured = category;
            btn.onClick.AddListener(() => SwitchCategory(captured));
        }

        private void BuildBottomSelectionDock(Sprite panelSprite, Font font)
        {
            GameObject dock = CreateCard(transform, "BottomSelectionDock", panelSprite, GridBgColor);
            RectTransform dockRT = dock.GetComponent<RectTransform>();
            dockRT.anchorMin = new Vector2(0.02f, 0.02f);
            dockRT.anchorMax = new Vector2(0.98f, 0.20f);
            dockRT.offsetMin = Vector2.zero;
            dockRT.offsetMax = Vector2.zero;

            // 3.1 进游戏出战恐龙小方格（核心选角色方格）
            GameObject dinoSlot = CreateCard(dock.transform, "SelectedDinoDropSlot", buttonPillSprite != null ? buttonPillSprite : panelSprite, DockSlotNormalColor);
            _selectedDinoSlotRT = dinoSlot.GetComponent<RectTransform>();
            _selectedDinoSlotRT.anchorMin = new Vector2(0f, 0.5f);
            _selectedDinoSlotRT.anchorMax = new Vector2(0f, 0.5f);
            _selectedDinoSlotRT.pivot = new Vector2(0f, 0.5f);
            _selectedDinoSlotRT.anchoredPosition = new Vector2(28f, 0f);
            _selectedDinoSlotRT.sizeDelta = new Vector2(156f, 156f);
            _selectedDinoSlotBg = dinoSlot.GetComponent<Image>();

            CreateText(dinoSlot.transform, "TopTag", font, "进游戏恐龙", 16, FontStyle.Bold, new Color(0.54f, 0.96f, 0.68f, 1f),
                TextAnchor.MiddleCenter, new Vector2(0f, 0.78f), new Vector2(1f, 0.98f), Vector2.zero, Vector2.zero);

            GameObject dIconGo = new GameObject("MosaicIcon", typeof(RectTransform), typeof(Image));
            dIconGo.transform.SetParent(dinoSlot.transform, false);
            RectTransform diRT = dIconGo.GetComponent<RectTransform>();
            diRT.anchorMin = new Vector2(0.18f, 0.22f);
            diRT.anchorMax = new Vector2(0.82f, 0.78f);
            diRT.offsetMin = Vector2.zero;
            diRT.offsetMax = Vector2.zero;
            _selectedDinoIcon = dIconGo.GetComponent<Image>();
            _selectedDinoIcon.preserveAspect = true;
            _selectedDinoIcon.raycastTarget = false;

            _selectedDinoNameText = CreateText(dinoSlot.transform, "Name", font, "成年霸王龙", 16, FontStyle.Bold, Color.white,
                TextAnchor.MiddleCenter, new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.22f), Vector2.zero, Vector2.zero);

            // 3.2 当前皮肤小方格（可拖入皮肤，点击可卸下恢复默认）
            GameObject skinSlot = CreateCard(dock.transform, "SelectedSkinDropSlot", buttonPillSprite != null ? buttonPillSprite : panelSprite, DockSlotNormalColor);
            _selectedSkinSlotRT = skinSlot.GetComponent<RectTransform>();
            _selectedSkinSlotRT.anchorMin = new Vector2(0f, 0.5f);
            _selectedSkinSlotRT.anchorMax = new Vector2(0f, 0.5f);
            _selectedSkinSlotRT.pivot = new Vector2(0f, 0.5f);
            _selectedSkinSlotRT.anchoredPosition = new Vector2(210f, 0f);
            _selectedSkinSlotRT.sizeDelta = new Vector2(156f, 156f);
            _selectedSkinSlotBg = skinSlot.GetComponent<Image>();
            Button skinSlotBtn = skinSlot.AddComponent<Button>();
            skinSlotBtn.onClick.AddListener(() =>
            {
                if (InventoryManager.EquippedSkinId != InventoryManager.DefaultSkinId)
                {
                    if (InventoryManager.EquipOrUseItem(InventoryManager.EquippedSkinId, out string msg))
                    {
                        UISoundManager.PlayClick();
                        ShowToast(msg);
                    }
                }
            });

            CreateText(skinSlot.transform, "TopTag", font, "当前皮肤", 16, FontStyle.Bold, new Color(0.56f, 0.86f, 1f, 1f),
                TextAnchor.MiddleCenter, new Vector2(0f, 0.78f), new Vector2(1f, 0.98f), Vector2.zero, Vector2.zero);

            GameObject sIconGo = new GameObject("MosaicIcon", typeof(RectTransform), typeof(Image));
            sIconGo.transform.SetParent(skinSlot.transform, false);
            RectTransform siRT = sIconGo.GetComponent<RectTransform>();
            siRT.anchorMin = new Vector2(0.18f, 0.22f);
            siRT.anchorMax = new Vector2(0.82f, 0.78f);
            siRT.offsetMin = Vector2.zero;
            siRT.offsetMax = Vector2.zero;
            _selectedSkinIcon = sIconGo.GetComponent<Image>();
            _selectedSkinIcon.preserveAspect = true;
            _selectedSkinIcon.raycastTarget = false;

            _selectedSkinNameText = CreateText(skinSlot.transform, "Name", font, "原生丛林绿", 16, FontStyle.Bold, Color.white,
                TextAnchor.MiddleCenter, new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.22f), Vector2.zero, Vector2.zero);

            // 3.3 右侧简明原型交互说明
            CreateText(dock.transform, "DockHint", font,
                "【选角色方式】从上方 4×4 仓库拖拽马赛克恐龙移入左下角方格（或直接点击格子），即选定进游戏出战角色与皮肤。",
                22, FontStyle.Bold, new Color(0.82f, 0.89f, 0.96f, 1f),
                TextAnchor.MiddleLeft, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(400f, 12f), new Vector2(-28f, -12f));
        }

        public void RefreshAll()
        {
            if (!_built) EnsureUI();

            UpdateTabVisual(InventoryCategory.Dino, "恐龙");
            UpdateTabVisual(InventoryCategory.Skin, "皮肤");

            // 刷新底部出战恐龙小方格与皮肤小方格
            InventoryItemData eqDino = InventoryManager.GetEquippedDino();
            if (eqDino != null)
            {
                if (_selectedDinoIcon != null) _selectedDinoIcon.sprite = ResolveMosaicSprite(eqDino);
                if (_selectedDinoNameText != null) _selectedDinoNameText.text = eqDino.name;
            }

            InventoryItemData eqSkin = InventoryManager.GetEquippedSkin();
            if (eqSkin != null)
            {
                if (_selectedSkinIcon != null) _selectedSkinIcon.sprite = ResolveMosaicSprite(eqSkin);
                if (_selectedSkinNameText != null) _selectedSkinNameText.text = eqSkin.name;
            }

            // 动态生成 4×4 = 16 个大格子（已拥有显示马赛克恐龙，其余为空方格）
            for (int i = _gridContent.childCount - 1; i >= 0; i--)
            {
                Destroy(_gridContent.GetChild(i).gameObject);
            }

            List<InventoryItemData> owned = InventoryManager.GetItemsByCategory(_activeCategory);
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Sprite panelSprite = GetPanelSprite();

            for (int i = 0; i < SlotsPerPage; i++)
            {
                if (i < owned.Count)
                {
                    BuildOwnedGridCell(owned[i], font, panelSprite);
                }
                else
                {
                    BuildEmptyGridCell(i, font, panelSprite);
                }
            }
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

        private void BuildOwnedGridCell(InventoryItemData item, Font font, Sprite panelSprite)
        {
            bool isEquipped = InventoryManager.IsItemEquipped(item);

            GameObject cellGo = new GameObject("Slot_" + item.id, typeof(RectTransform), typeof(Image), typeof(WarehouseDragSlot));
            cellGo.transform.SetParent(_gridContent, false);

            Image bg = cellGo.GetComponent<Image>();
            bg.sprite = buttonPillSprite != null ? buttonPillSprite : panelSprite;
            bg.type = Image.Type.Sliced;
            bg.color = isEquipped ? SlotEquippedBgColor : SlotOwnedBgColor;
            bg.raycastTarget = true;

            Sprite mosaic = ResolveMosaicSprite(item);

            // 左侧大尺寸马赛克恐龙图标
            GameObject iconGo = new GameObject("MosaicIcon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(cellGo.transform, false);
            RectTransform iRT = iconGo.GetComponent<RectTransform>();
            iRT.anchorMin = new Vector2(0.05f, 0.10f);
            iRT.anchorMax = new Vector2(0.38f, 0.90f);
            iRT.offsetMin = Vector2.zero;
            iRT.offsetMax = Vector2.zero;
            Image iconImg = iconGo.GetComponent<Image>();
            iconImg.sprite = mosaic;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            // 右侧简明名称与当前状态
            Text nameTxt = CreateText(cellGo.transform, "Name", font, item.name, 22, FontStyle.Bold, Color.white,
                TextAnchor.MiddleLeft, new Vector2(0.41f, 0.48f), new Vector2(0.96f, 0.88f), Vector2.zero, Vector2.zero);
            AddOutline(nameTxt, 1.2f);

            string statusStr = isEquipped
                ? (item.category == InventoryCategory.Dino ? "✓ 已选为出战" : "✓ 当前穿戴")
                : "拖拽到下方方格 / 点击选角";
            Color statusCol = isEquipped ? new Color(0.56f, 1f, 0.72f, 1f) : new Color(0.70f, 0.78f, 0.88f, 0.92f);
            CreateText(cellGo.transform, "Status", font, statusStr, 17, FontStyle.Bold, statusCol,
                TextAnchor.MiddleLeft, new Vector2(0.41f, 0.14f), new Vector2(0.96f, 0.48f), Vector2.zero, Vector2.zero);

            WarehouseDragSlot dragSlot = cellGo.GetComponent<WarehouseDragSlot>();
            dragSlot.Init(this, item, mosaic);
        }

        private void BuildEmptyGridCell(int index, Font font, Sprite panelSprite)
        {
            GameObject emptyGo = CreateCard(_gridContent, $"EmptySlot_{index + 1}", panelSprite, SlotEmptyBgColor);
            emptyGo.GetComponent<Image>().raycastTarget = false;
            CreateText(emptyGo.transform, "EmptyLabel", font, "空槽位", 18, FontStyle.Normal,
                new Color(0.42f, 0.48f, 0.56f, 0.55f),
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        internal void BeginDragItem(InventoryItemData item, Sprite sprite, PointerEventData eventData)
        {
            if (item == null || _dragGhostRT == null) return;
            _dragGhostImg.sprite = sprite;
            _dragGhostRT.gameObject.SetActive(true);
            _dragGhostRT.SetAsLastSibling();
            UpdateDragPosition(eventData);

            // 高亮下方对应落点方格
            if (item.category == InventoryCategory.Dino && _selectedDinoSlotBg != null)
            {
                _selectedDinoSlotBg.color = DockSlotHighlightColor;
            }
            else if (item.category == InventoryCategory.Skin && _selectedSkinSlotBg != null)
            {
                _selectedSkinSlotBg.color = DockSlotHighlightColor;
            }
        }

        internal void UpdateDragPosition(PointerEventData eventData)
        {
            if (_dragGhostRT == null || !_dragGhostRT.gameObject.activeSelf) return;
            if (_rootCanvas == null) _rootCanvas = GetComponentInParent<Canvas>();

            Camera cam = _rootCanvas != null && _rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? _rootCanvas.worldCamera : null;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    GetComponent<RectTransform>(), eventData.position, cam, out Vector2 localPt))
            {
                _dragGhostRT.anchoredPosition = localPt;
            }
        }

        internal void EndDragItem(InventoryItemData item, PointerEventData eventData)
        {
            EndDragVisual();
            if (item == null || eventData == null) return;

            Camera cam = _rootCanvas != null && _rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? _rootCanvas.worldCamera : null;
            RectTransform targetSlot = item.category == InventoryCategory.Dino ? _selectedDinoSlotRT : _selectedSkinSlotRT;

            if (targetSlot != null && RectTransformUtility.RectangleContainsScreenPoint(targetSlot, eventData.position, cam))
            {
                EquipItemWithFeedback(item);
            }
        }

        internal void HandleSlotClicked(InventoryItemData item)
        {
            if (item == null) return;
            EquipItemWithFeedback(item);
        }

        private void EquipItemWithFeedback(InventoryItemData item)
        {
            bool ok = InventoryManager.EquipOrUseItem(item.id, out string msg);
            if (ok) UISoundManager.PlaySave();
            else UISoundManager.PlayClick();
            ShowToast(msg);
        }

        private void EndDragVisual()
        {
            if (_dragGhostRT != null) _dragGhostRT.gameObject.SetActive(false);
            if (_selectedDinoSlotBg != null) _selectedDinoSlotBg.color = DockSlotNormalColor;
            if (_selectedSkinSlotBg != null) _selectedSkinSlotBg.color = DockSlotNormalColor;
        }

        private void ShowToast(string msg)
        {
            if (_toastObj == null || _toastText == null) return;
            _toastText.text = msg;
            _toastObj.SetActive(true);
            _toastObj.transform.SetAsLastSibling();
            _toastHideTime = Time.unscaledTime + 1.8f;
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

    /// <summary>
    /// 仓库大格子拖拽/点击交互组件：
    /// 支持拖拽马赛克恐龙移入底部出战方格，亦支持单击快速选角。
    /// </summary>
    public class WarehouseDragSlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        private InventoryPanelHUD _owner;
        private InventoryItemData _item;
        private Sprite _sprite;
        private bool _dragging;

        public void Init(InventoryPanelHUD owner, InventoryItemData item, Sprite sprite)
        {
            _owner = owner;
            _item = item;
            _sprite = sprite;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _dragging = true;
            _owner?.BeginDragItem(_item, _sprite, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            _owner?.UpdateDragPosition(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _dragging = false;
            _owner?.EndDragItem(_item, eventData);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_dragging) return;
            _owner?.HandleSlotClicked(_item);
        }
    }
}
