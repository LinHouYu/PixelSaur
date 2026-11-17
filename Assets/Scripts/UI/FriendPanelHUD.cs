using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PixelSaur.UI
{
    /// <summary>
    /// 开始场景好友与查找朋友面板：
    /// 1. 玩家 ID 搜索框与模糊/精确搜索（含本地模拟玩家目录与任意新 ID 发现）；
    /// 2. 好友列表滚动栏（ScrollRect）及在线/离线状态色块占位；
    /// 3. 好友申请列表与【同意 / 拒绝】按钮交互；
    /// 4. 本地模拟增删好友并写入 FriendManager 本地 JSON + PlayerPrefs 存档（不含联机服务器长连接）。
    /// </summary>
    public class FriendPanelHUD : MonoBehaviour
    {
        [Header("基础按钮与面板底图")]
        [SerializeField] private Sprite whitePillSprite;
        [SerializeField] private Sprite buttonPillSprite;

        [Header("图标资产")]
        [SerializeField] private Sprite iconPlayers;
        [SerializeField] private Sprite iconDinoSneak;

        private Text _selfIdText;
        private InputField _searchInput;
        private Text _friendsHeaderText;
        private Text _searchHeaderText;
        private Text _requestsHeaderText;

        private ScrollRect _friendsScroll;
        private RectTransform _friendsContent;

        private ScrollRect _searchScroll;
        private RectTransform _searchContent;

        private ScrollRect _requestsScroll;
        private RectTransform _requestsContent;

        private GameObject _toastObj;
        private Text _toastText;
        private float _toastHideTime;

        private string _currentQuery = string.Empty;
        private bool _built;
        private static Sprite _fallbackWhitePill;

        private static readonly Color PanelBgColor = new Color(0.07f, 0.10f, 0.15f, 0.99f);
        private static readonly Color SubPanelBgColor = new Color(0.10f, 0.14f, 0.21f, 0.98f);
        private static readonly Color CardBgColor = new Color(0.13f, 0.18f, 0.27f, 0.99f);
        private static readonly Color InsetBgColor = new Color(0.07f, 0.10f, 0.16f, 0.98f);

        private static readonly Color OnlineColor = new Color(0.18f, 0.86f, 0.44f, 1f);
        private static readonly Color OfflineColor = new Color(0.46f, 0.50f, 0.56f, 1f);

        private static readonly Color BtnBlueColor = new Color(0.18f, 0.48f, 0.78f, 1f);
        private static readonly Color BtnGreenColor = new Color(0.16f, 0.68f, 0.36f, 1f);
        private static readonly Color BtnRedColor = new Color(0.58f, 0.20f, 0.22f, 1f);
        private static readonly Color BtnSlateColor = new Color(0.22f, 0.27f, 0.36f, 1f);

        private void Awake()
        {
            LoadFallbackAssets();
            FriendManager.EnsureInitialized();
            EnsureUI();
        }

        private void OnEnable()
        {
            LoadFallbackAssets();
            FriendManager.EnsureInitialized();
            EnsureUI();
            FriendManager.OnFriendsChanged += HandleFriendsChanged;
            RefreshAll(resetScrolls: false);
        }

        private void OnDisable()
        {
            FriendManager.OnFriendsChanged -= HandleFriendsChanged;
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
            if (iconPlayers == null) iconPlayers = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_players_32x32.png");
            if (iconDinoSneak == null) iconDinoSneak = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_dino_sneak_32x32.png");
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
                name = "FriendPanelFallbackPill",
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

        public void OpenDefault()
        {
            _currentQuery = string.Empty;
            if (_searchInput != null) _searchInput.text = string.Empty;

            if (UIPanelManager.Instance != null)
            {
                UIPanelManager.Instance.OpenPanel("FriendPanel");
            }
            else
            {
                gameObject.SetActive(true);
                transform.SetAsLastSibling();
            }

            RefreshAll(resetScrolls: true);
        }

        private void HandleFriendsChanged()
        {
            if (gameObject.activeInHierarchy)
            {
                RefreshAll(resetScrolls: false);
            }
        }

        public void ExecuteSearch(string query)
        {
            _currentQuery = query != null ? query.Trim() : string.Empty;
            if (_searchInput != null && _searchInput.text != _currentQuery)
            {
                _searchInput.text = _currentQuery;
            }
            RefreshSearchResults(resetScroll: true);
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

            // 1. 顶部标题、自身 ID 复制芯片、重置列表、关闭按钮
            CreateText(transform, "Title", font, "查找朋友 & 好友列表", 36, FontStyle.Bold, Color.white,
                TextAnchor.MiddleLeft, new Vector2(0f, 0.87f), new Vector2(0.40f, 1f), new Vector2(36f, 0f), new Vector2(0f, -6f));

            GameObject selfIdChip = CreateCard(transform, "SelfIdChip", panelSprite, SubPanelBgColor);
            RectTransform idRT = selfIdChip.GetComponent<RectTransform>();
            idRT.anchorMin = new Vector2(0.42f, 0.89f);
            idRT.anchorMax = new Vector2(0.70f, 0.97f);
            idRT.offsetMin = Vector2.zero;
            idRT.offsetMax = Vector2.zero;
            _selfIdText = CreateText(selfIdChip.transform, "Text", font,
                $"我的 ID：{FriendManager.SelfPlayerId}（{FriendManager.SelfNickname}）", 20, FontStyle.Bold,
                new Color(0.66f, 0.88f, 1f, 1f),
                TextAnchor.MiddleLeft, Vector2.zero, new Vector2(0.74f, 1f), new Vector2(18f, 0f), Vector2.zero);

            Button copyIdBtn = CreateStyledButton(selfIdChip.transform, "CopySelfIdBtn", font, "复制ID", 18, BtnSlateColor,
                new Vector2(0.86f, 0.5f), new Vector2(0.86f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(104f, 44f));
            copyIdBtn.onClick.AddListener(() =>
            {
                UISoundManager.PlayClick();
                GUIUtility.systemCopyBuffer = FriendManager.SelfPlayerId;
                ShowToast($"✓ 已复制个人 ID：{FriendManager.SelfPlayerId}", true);
            });

            Button resetBtn = CreateStyledButton(transform, "ResetFriendsBtn", font, "重置好友", 21, BtnSlateColor,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-116f, -22f), new Vector2(150f, 58f));
            resetBtn.onClick.AddListener(() =>
            {
                UISoundManager.PlayClick();
                _currentQuery = string.Empty;
                if (_searchInput != null) _searchInput.text = string.Empty;
                FriendManager.ResetFriendsData();
                ShowToast("✓ 已恢复本地初始好友与申请列表！", true);
            });

            Button closeBtn = CreateStyledButton(transform, "FriendCloseBtn", font, "✕", 32, new Color(0.46f, 0.18f, 0.20f, 1f),
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -18f), new Vector2(68f, 68f));
            closeBtn.onClick.AddListener(() =>
            {
                if (UIPanelManager.Instance != null) UIPanelManager.Instance.ClosePanel("FriendPanel");
                else { UISoundManager.PlayClose(); gameObject.SetActive(false); }
            });

            // 2. 玩家 ID 搜索栏区
            BuildSearchBar(panelSprite, font);

            // 3. 左侧：好友列表滚动栏（含在线/离线状态色块占位 & 删除好友按钮）
            BuildFriendsListSection(panelSprite, font);

            // 4. 右上：ID 搜索结果 / 推荐玩家（添加好友）
            BuildSearchResultSection(panelSprite, font);

            // 5. 右下：好友申请列表（同意 / 拒绝按钮交互）
            BuildRequestSection(panelSprite, font);

            // 6. 底部 Toast 反馈提示
            _toastObj = CreateCard(transform, "FriendToast", panelSprite, new Color(0.14f, 0.66f, 0.36f, 0.98f));
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

        private void BuildSearchBar(Sprite panelSprite, Font font)
        {
            GameObject searchBar = CreateCard(transform, "SearchBarRow", panelSprite, SubPanelBgColor);
            RectTransform sbRT = searchBar.GetComponent<RectTransform>();
            sbRT.anchorMin = new Vector2(0.03f, 0.77f);
            sbRT.anchorMax = new Vector2(0.97f, 0.87f);
            sbRT.offsetMin = Vector2.zero;
            sbRT.offsetMax = Vector2.zero;

            // 搜索输入框
            GameObject inputGo = CreateCard(searchBar.transform, "PlayerIdSearchInput", panelSprite, InsetBgColor);
            RectTransform inRT = inputGo.GetComponent<RectTransform>();
            inRT.anchorMin = new Vector2(0.015f, 0.14f);
            inRT.anchorMax = new Vector2(0.75f, 0.86f);
            inRT.offsetMin = Vector2.zero;
            inRT.offsetMax = Vector2.zero;

            Text placeholderTxt = CreateText(inputGo.transform, "Placeholder", font,
                "输入玩家 ID 或昵称查找朋友（如 10005、10006 或任意测试 ID）...", 21, FontStyle.Normal,
                new Color(0.56f, 0.66f, 0.76f, 0.85f),
                TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, new Vector2(20f, 4f), new Vector2(-20f, -4f));

            Text inputTxt = CreateText(inputGo.transform, "Text", font, "", 22, FontStyle.Bold, Color.white,
                TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, new Vector2(20f, 4f), new Vector2(-20f, -4f));
            inputTxt.supportRichText = false;

            _searchInput = inputGo.AddComponent<InputField>();
            _searchInput.targetGraphic = inputGo.GetComponent<Image>();
            _searchInput.textComponent = inputTxt;
            _searchInput.placeholder = placeholderTxt;
            _searchInput.characterLimit = 24;
            _searchInput.onSubmit.AddListener(val =>
            {
                UISoundManager.PlayClick();
                ExecuteSearch(val);
            });

            Button searchBtn = CreateStyledButton(searchBar.transform, "SearchPlayerBtn", font, "搜索玩家", 23, BtnBlueColor,
                new Vector2(0.82f, 0.5f), new Vector2(0.82f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            RectTransform sBtnRT = searchBtn.GetComponent<RectTransform>();
            sBtnRT.anchorMin = new Vector2(0.765f, 0.14f);
            sBtnRT.anchorMax = new Vector2(0.885f, 0.86f);
            sBtnRT.offsetMin = Vector2.zero;
            sBtnRT.offsetMax = Vector2.zero;
            searchBtn.onClick.AddListener(() =>
            {
                UISoundManager.PlayClick();
                ExecuteSearch(_searchInput != null ? _searchInput.text : string.Empty);
            });

            Button clearBtn = CreateStyledButton(searchBar.transform, "SearchClearBtn", font, "清 空", 22, BtnSlateColor,
                new Vector2(0.94f, 0.5f), new Vector2(0.94f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            RectTransform cBtnRT = clearBtn.GetComponent<RectTransform>();
            cBtnRT.anchorMin = new Vector2(0.895f, 0.14f);
            cBtnRT.anchorMax = new Vector2(0.985f, 0.86f);
            cBtnRT.offsetMin = Vector2.zero;
            cBtnRT.offsetMax = Vector2.zero;
            clearBtn.onClick.AddListener(() =>
            {
                UISoundManager.PlayClick();
                ExecuteSearch(string.Empty);
            });
        }

        private void BuildFriendsListSection(Sprite panelSprite, Font font)
        {
            GameObject section = CreateCard(transform, "FriendsSection", panelSprite, SubPanelBgColor);
            RectTransform secRT = section.GetComponent<RectTransform>();
            secRT.anchorMin = new Vector2(0.03f, 0.05f);
            secRT.anchorMax = new Vector2(0.54f, 0.75f);
            secRT.offsetMin = Vector2.zero;
            secRT.offsetMax = Vector2.zero;

            _friendsHeaderText = CreateText(section.transform, "Header", font, "我的好友（在线 0 / 共 0）", 24, FontStyle.Bold, Color.white,
                TextAnchor.MiddleLeft, new Vector2(0f, 0.90f), new Vector2(0.64f, 1f), new Vector2(20f, 0f), new Vector2(0f, -4f));

            // 在线/离线状态色块占位图例
            BuildLegendBadge(section.transform, panelSprite, font, "OnlineLegend", OnlineColor, "在线", 0.65f, 0.80f);
            BuildLegendBadge(section.transform, panelSprite, font, "OfflineLegend", OfflineColor, "离线", 0.82f, 0.97f);

            (_friendsScroll, _friendsContent) = BuildVerticalScrollView(section.transform, "FriendsScrollView",
                new Vector2(0f, 0f), new Vector2(1f, 0.90f), new Vector2(14f, 14f), new Vector2(-14f, -6f));
        }

        private void BuildSearchResultSection(Sprite panelSprite, Font font)
        {
            GameObject section = CreateCard(transform, "SearchResultSection", panelSprite, SubPanelBgColor);
            RectTransform secRT = section.GetComponent<RectTransform>();
            secRT.anchorMin = new Vector2(0.56f, 0.41f);
            secRT.anchorMax = new Vector2(0.97f, 0.75f);
            secRT.offsetMin = Vector2.zero;
            secRT.offsetMax = Vector2.zero;

            _searchHeaderText = CreateText(section.transform, "Header", font, "查找结果 / 推荐玩家", 22, FontStyle.Bold,
                new Color(0.66f, 0.86f, 1f, 1f),
                TextAnchor.MiddleLeft, new Vector2(0f, 0.82f), new Vector2(1f, 1f), new Vector2(18f, 0f), new Vector2(-18f, -4f));

            (_searchScroll, _searchContent) = BuildVerticalScrollView(section.transform, "SearchResultScrollView",
                new Vector2(0f, 0f), new Vector2(1f, 0.82f), new Vector2(12f, 12f), new Vector2(-12f, -4f));
        }

        private void BuildRequestSection(Sprite panelSprite, Font font)
        {
            GameObject section = CreateCard(transform, "RequestSection", panelSprite, SubPanelBgColor);
            RectTransform secRT = section.GetComponent<RectTransform>();
            secRT.anchorMin = new Vector2(0.56f, 0.05f);
            secRT.anchorMax = new Vector2(0.97f, 0.39f);
            secRT.offsetMin = Vector2.zero;
            secRT.offsetMax = Vector2.zero;

            _requestsHeaderText = CreateText(section.transform, "Header", font, "好友申请（待处理 0 条）", 22, FontStyle.Bold,
                new Color(1f, 0.86f, 0.44f, 1f),
                TextAnchor.MiddleLeft, new Vector2(0f, 0.82f), new Vector2(1f, 1f), new Vector2(18f, 0f), new Vector2(-18f, -4f));

            (_requestsScroll, _requestsContent) = BuildVerticalScrollView(section.transform, "RequestScrollView",
                new Vector2(0f, 0f), new Vector2(1f, 0.82f), new Vector2(12f, 12f), new Vector2(-12f, -4f));
        }

        private static void BuildLegendBadge(Transform parent, Sprite pillSprite, Font font, string name, Color blockColor, string label,
            float anchorMinX, float anchorMaxX)
        {
            GameObject holder = new GameObject(name, typeof(RectTransform));
            holder.transform.SetParent(parent, false);
            RectTransform hRT = holder.GetComponent<RectTransform>();
            hRT.anchorMin = new Vector2(anchorMinX, 0.91f);
            hRT.anchorMax = new Vector2(anchorMaxX, 0.99f);
            hRT.offsetMin = Vector2.zero;
            hRT.offsetMax = Vector2.zero;

            GameObject dot = CreateCard(holder.transform, "ColorBlock", pillSprite, blockColor);
            RectTransform dRT = dot.GetComponent<RectTransform>();
            dRT.anchorMin = new Vector2(0.05f, 0.25f);
            dRT.anchorMax = new Vector2(0.28f, 0.75f);
            dRT.offsetMin = Vector2.zero;
            dRT.offsetMax = Vector2.zero;

            CreateText(holder.transform, "Label", font, label, 18, FontStyle.Bold,
                new Color(0.84f, 0.90f, 0.96f, 1f),
                TextAnchor.MiddleLeft, new Vector2(0.34f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        }

        private static (ScrollRect, RectTransform) BuildVerticalScrollView(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject scrollGo = new GameObject(name, typeof(RectTransform), typeof(ScrollRect));
            scrollGo.transform.SetParent(parent, false);
            RectTransform scrollRT = scrollGo.GetComponent<RectTransform>();
            scrollRT.anchorMin = anchorMin;
            scrollRT.anchorMax = anchorMax;
            scrollRT.offsetMin = offsetMin;
            scrollRT.offsetMax = offsetMax;

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

            GameObject contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(vpGo.transform, false);
            RectTransform contentRT = contentGo.GetComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0f, 1f);
            contentRT.anchorMax = new Vector2(1f, 1f);
            contentRT.pivot = new Vector2(0.5f, 1f);
            contentRT.anchoredPosition = Vector2.zero;
            contentRT.sizeDelta = new Vector2(0f, 200f);

            ScrollRect sr = scrollGo.GetComponent<ScrollRect>();
            sr.viewport = vpRT;
            sr.content = contentRT;
            sr.horizontal = false;
            sr.vertical = true;
            sr.inertia = true;
            sr.scrollSensitivity = 28f;
            sr.movementType = ScrollRect.MovementType.Clamped;
            return (sr, contentRT);
        }

        public void RefreshAll(bool resetScrolls)
        {
            if (!_built) EnsureUI();
            RefreshFriendsList(resetScrolls);
            RefreshSearchResults(resetScrolls);
            RefreshRequestsList(resetScrolls);
        }

        private void RefreshFriendsList(bool resetScroll)
        {
            for (int i = _friendsContent.childCount - 1; i >= 0; i--)
            {
                Destroy(_friendsContent.GetChild(i).gameObject);
            }

            List<FriendEntryData> friends = FriendManager.GetFriends();
            int onlineCount = FriendManager.GetOnlineFriendCount();
            if (_friendsHeaderText != null)
            {
                _friendsHeaderText.text = $"我的好友（在线 {onlineCount} / 共 {friends.Count}）";
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Sprite panelSprite = GetPanelSprite();

            float y = 0f;
            const float cardHeight = 112f;
            const float spacing = 12f;

            if (friends.Count == 0)
            {
                GameObject empty = CreateCard(_friendsContent, "EmptyHint", panelSprite, InsetBgColor);
                RectTransform eRT = empty.GetComponent<RectTransform>();
                eRT.anchorMin = new Vector2(0f, 1f);
                eRT.anchorMax = new Vector2(1f, 1f);
                eRT.pivot = new Vector2(0.5f, 1f);
                eRT.anchoredPosition = Vector2.zero;
                eRT.sizeDelta = new Vector2(0f, 120f);
                CreateText(empty.transform, "Text", font, "暂无好友，可在上方搜索玩家 ID 或在右侧同意好友申请", 20, FontStyle.Bold,
                    new Color(0.66f, 0.76f, 0.86f, 1f),
                    TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                y = 132f;
            }
            else
            {
                for (int i = 0; i < friends.Count; i++)
                {
                    BuildFriendCard(friends[i], font, panelSprite, y, cardHeight);
                    y += cardHeight + spacing;
                }
            }

            _friendsContent.sizeDelta = new Vector2(0f, Mathf.Max(140f, y));
            Canvas.ForceUpdateCanvases();
            if (resetScroll && _friendsScroll != null)
            {
                _friendsScroll.velocity = Vector2.zero;
                _friendsScroll.verticalNormalizedPosition = 1f;
            }
        }

        private void BuildFriendCard(FriendEntryData f, Font font, Sprite panelSprite, float posY, float height)
        {
            GameObject card = CreateCard(_friendsContent, "FriendCard_" + f.playerId, panelSprite, CardBgColor);
            RectTransform rt = card.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -posY);
            rt.sizeDelta = new Vector2(0f, height);

            Color statusColor = f.isOnline ? OnlineColor : OfflineColor;

            // 在线/离线状态色块占位（左侧色条）
            GameObject colorStrip = CreateCard(card.transform, "StatusColorBlock", panelSprite, statusColor);
            RectTransform csRT = colorStrip.GetComponent<RectTransform>();
            csRT.anchorMin = new Vector2(0.01f, 0.14f);
            csRT.anchorMax = new Vector2(0.025f, 0.86f);
            csRT.offsetMin = Vector2.zero;
            csRT.offsetMax = Vector2.zero;

            // 头像底座
            Color acc = f.GetAccentColor();
            GameObject avatarBox = CreateCard(card.transform, "AvatarBox", panelSprite,
                new Color(acc.r * 0.35f, acc.g * 0.35f, acc.b * 0.35f, 1f));
            RectTransform avRT = avatarBox.GetComponent<RectTransform>();
            avRT.anchorMin = new Vector2(0.04f, 0.16f);
            avRT.anchorMax = new Vector2(0.145f, 0.84f);
            avRT.offsetMin = Vector2.zero;
            avRT.offsetMax = Vector2.zero;

            GameObject avIcon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            avIcon.transform.SetParent(avatarBox.transform, false);
            RectTransform aiRT = avIcon.GetComponent<RectTransform>();
            aiRT.anchorMin = new Vector2(0.14f, 0.14f);
            aiRT.anchorMax = new Vector2(0.86f, 0.86f);
            aiRT.offsetMin = Vector2.zero;
            aiRT.offsetMax = Vector2.zero;
            Image avImg = avIcon.GetComponent<Image>();
            avImg.sprite = iconDinoSneak != null ? iconDinoSneak : iconPlayers;
            avImg.preserveAspect = true;
            avImg.color = f.isOnline ? Color.white : new Color(0.65f, 0.68f, 0.72f, 0.85f);
            avImg.raycastTarget = false;

            // 昵称与 ID
            Text nameTxt = CreateText(card.transform, "Name", font, $"{f.nickname}  (ID:{f.playerId})", 22, FontStyle.Bold, Color.white,
                TextAnchor.MiddleLeft, new Vector2(0.16f, 0.52f), new Vector2(0.62f, 0.92f), Vector2.zero, Vector2.zero);
            AddOutline(nameTxt, 1f);

            // 等级、恐龙标签及状态描述
            CreateText(card.transform, "Info", font, $"Lv.{f.level} · {f.dinoTag}  |  {f.statusDesc}", 17, FontStyle.Normal,
                new Color(0.74f, 0.84f, 0.94f, 0.96f),
                TextAnchor.MiddleLeft, new Vector2(0.16f, 0.10f), new Vector2(0.62f, 0.50f), Vector2.zero, Vector2.zero);

            // 在线 / 离线状态色块徽章
            GameObject statusBadge = CreateCard(card.transform, "OnlineStatusBadge", panelSprite, statusColor);
            RectTransform sbRT = statusBadge.GetComponent<RectTransform>();
            sbRT.anchorMin = new Vector2(0.635f, 0.30f);
            sbRT.anchorMax = new Vector2(0.765f, 0.70f);
            sbRT.offsetMin = Vector2.zero;
            sbRT.offsetMax = Vector2.zero;
            Text sbTxt = CreateText(statusBadge.transform, "Text", font, f.isOnline ? "● 在线" : "● 离线", 18, FontStyle.Bold, Color.white,
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddOutline(sbTxt, 1f);

            // 删除好友按钮
            Button removeBtn = CreateStyledButton(card.transform, "RemoveBtn", font, "删 除", 20, BtnRedColor,
                new Vector2(0.88f, 0.5f), new Vector2(0.88f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            RectTransform rbRT = removeBtn.GetComponent<RectTransform>();
            rbRT.anchorMin = new Vector2(0.785f, 0.22f);
            rbRT.anchorMax = new Vector2(0.975f, 0.78f);
            rbRT.offsetMin = Vector2.zero;
            rbRT.offsetMax = Vector2.zero;

            string capturedId = f.playerId;
            removeBtn.onClick.AddListener(() =>
            {
                bool ok = FriendManager.RemoveFriend(capturedId, out string msg);
                if (ok) UISoundManager.PlayClick();
                ShowToast(msg, ok);
            });
        }

        private void RefreshSearchResults(bool resetScroll)
        {
            for (int i = _searchContent.childCount - 1; i >= 0; i--)
            {
                Destroy(_searchContent.GetChild(i).gameObject);
            }

            List<FriendEntryData> matches = FriendManager.SearchPlayers(_currentQuery, out string hint);
            if (_searchHeaderText != null) _searchHeaderText.text = hint;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Sprite panelSprite = GetPanelSprite();

            float y = 0f;
            const float cardHeight = 90f;
            const float spacing = 10f;

            if (matches.Count == 0)
            {
                GameObject empty = CreateCard(_searchContent, "EmptySearch", panelSprite, InsetBgColor);
                RectTransform eRT = empty.GetComponent<RectTransform>();
                eRT.anchorMin = new Vector2(0f, 1f);
                eRT.anchorMax = new Vector2(1f, 1f);
                eRT.pivot = new Vector2(0.5f, 1f);
                eRT.anchoredPosition = Vector2.zero;
                eRT.sizeDelta = new Vector2(0f, 92f);
                CreateText(empty.transform, "Text", font, hint, 19, FontStyle.Bold, new Color(0.76f, 0.84f, 0.94f, 1f),
                    TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(12f, 0f), new Vector2(-12f, 0f));
                y = 102f;
            }
            else
            {
                for (int i = 0; i < matches.Count; i++)
                {
                    BuildSearchResultCard(matches[i], font, panelSprite, y, cardHeight);
                    y += cardHeight + spacing;
                }
            }

            _searchContent.sizeDelta = new Vector2(0f, Mathf.Max(110f, y));
            Canvas.ForceUpdateCanvases();
            if (resetScroll && _searchScroll != null)
            {
                _searchScroll.velocity = Vector2.zero;
                _searchScroll.verticalNormalizedPosition = 1f;
            }
        }

        private void BuildSearchResultCard(FriendEntryData candidate, Font font, Sprite panelSprite, float posY, float height)
        {
            bool alreadyFriend = FriendManager.IsFriend(candidate.playerId);
            bool hasPendingReq = FriendManager.HasPendingRequest(candidate.playerId);

            GameObject card = CreateCard(_searchContent, "SearchCard_" + candidate.playerId, panelSprite, CardBgColor);
            RectTransform rt = card.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -posY);
            rt.sizeDelta = new Vector2(0f, height);

            // 状态色块条
            GameObject strip = CreateCard(card.transform, "StatusColorBlock", panelSprite, candidate.isOnline ? OnlineColor : OfflineColor);
            RectTransform sRT = strip.GetComponent<RectTransform>();
            sRT.anchorMin = new Vector2(0.012f, 0.14f);
            sRT.anchorMax = new Vector2(0.03f, 0.86f);
            sRT.offsetMin = Vector2.zero;
            sRT.offsetMax = Vector2.zero;

            Text titleTxt = CreateText(card.transform, "Name", font,
                $"{candidate.nickname}  (ID:{candidate.playerId})", 20, FontStyle.Bold, Color.white,
                TextAnchor.MiddleLeft, new Vector2(0.05f, 0.50f), new Vector2(0.71f, 0.92f), Vector2.zero, Vector2.zero);
            AddOutline(titleTxt, 1f);

            string onlineTag = candidate.isOnline ? "在线" : "离线";
            CreateText(card.transform, "Sub", font,
                $"{onlineTag} · Lv.{candidate.level} · {candidate.dinoTag}", 16, FontStyle.Normal,
                new Color(0.74f, 0.84f, 0.94f, 0.95f),
                TextAnchor.MiddleLeft, new Vector2(0.05f, 0.08f), new Vector2(0.71f, 0.48f), Vector2.zero, Vector2.zero);

            string btnText = alreadyFriend ? "已是好友" : (hasPendingReq ? "通过申请" : "加为好友");
            Color btnColor = alreadyFriend ? BtnSlateColor : BtnGreenColor;

            Button addBtn = CreateStyledButton(card.transform, "AddFriendBtn", font, btnText, 19, btnColor,
                new Vector2(0.85f, 0.5f), new Vector2(0.85f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            RectTransform abRT = addBtn.GetComponent<RectTransform>();
            abRT.anchorMin = new Vector2(0.73f, 0.18f);
            abRT.anchorMax = new Vector2(0.975f, 0.82f);
            abRT.offsetMin = Vector2.zero;
            abRT.offsetMax = Vector2.zero;
            addBtn.interactable = !alreadyFriend;
            if (alreadyFriend)
            {
                Text bt = addBtn.GetComponentInChildren<Text>();
                if (bt != null) bt.color = new Color(0.68f, 0.72f, 0.78f, 0.92f);
            }

            FriendEntryData capturedCandidate = candidate;
            addBtn.onClick.AddListener(() =>
            {
                bool ok = FriendManager.AddFriend(capturedCandidate, out string msg);
                if (ok) UISoundManager.PlaySave();
                else UISoundManager.PlayClick();
                ShowToast(msg, ok);
            });
        }

        private void RefreshRequestsList(bool resetScroll)
        {
            for (int i = _requestsContent.childCount - 1; i >= 0; i--)
            {
                Destroy(_requestsContent.GetChild(i).gameObject);
            }

            List<FriendRequestData> requests = FriendManager.GetRequests();
            if (_requestsHeaderText != null)
            {
                _requestsHeaderText.text = $"好友申请（待处理 {requests.Count} 条）";
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Sprite panelSprite = GetPanelSprite();

            float y = 0f;
            const float cardHeight = 104f;
            const float spacing = 10f;

            if (requests.Count == 0)
            {
                GameObject empty = CreateCard(_requestsContent, "EmptyRequests", panelSprite, InsetBgColor);
                RectTransform eRT = empty.GetComponent<RectTransform>();
                eRT.anchorMin = new Vector2(0f, 1f);
                eRT.anchorMax = new Vector2(1f, 1f);
                eRT.pivot = new Vector2(0.5f, 1f);
                eRT.anchoredPosition = Vector2.zero;
                eRT.sizeDelta = new Vector2(0f, 100f);
                CreateText(empty.transform, "Text", font, "暂无待处理的好友申请", 19, FontStyle.Bold,
                    new Color(0.66f, 0.76f, 0.86f, 1f),
                    TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                y = 110f;
            }
            else
            {
                for (int i = 0; i < requests.Count; i++)
                {
                    BuildRequestCard(requests[i], font, panelSprite, y, cardHeight);
                    y += cardHeight + spacing;
                }
            }

            _requestsContent.sizeDelta = new Vector2(0f, Mathf.Max(110f, y));
            Canvas.ForceUpdateCanvases();
            if (resetScroll && _requestsScroll != null)
            {
                _requestsScroll.velocity = Vector2.zero;
                _requestsScroll.verticalNormalizedPosition = 1f;
            }
        }

        private void BuildRequestCard(FriendRequestData req, Font font, Sprite panelSprite, float posY, float height)
        {
            GameObject card = CreateCard(_requestsContent, "RequestCard_" + req.playerId, panelSprite, CardBgColor);
            RectTransform rt = card.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -posY);
            rt.sizeDelta = new Vector2(0f, height);

            // 状态色块条
            GameObject strip = CreateCard(card.transform, "StatusColorBlock", panelSprite, req.isOnline ? OnlineColor : OfflineColor);
            RectTransform sRT = strip.GetComponent<RectTransform>();
            sRT.anchorMin = new Vector2(0.012f, 0.14f);
            sRT.anchorMax = new Vector2(0.03f, 0.86f);
            sRT.offsetMin = Vector2.zero;
            sRT.offsetMax = Vector2.zero;

            Text titleTxt = CreateText(card.transform, "Name", font,
                $"{req.nickname}  (ID:{req.playerId}) · Lv.{req.level}", 19, FontStyle.Bold, Color.white,
                TextAnchor.MiddleLeft, new Vector2(0.05f, 0.52f), new Vector2(0.60f, 0.92f), Vector2.zero, Vector2.zero);
            AddOutline(titleTxt, 1f);

            Text noteTxt = CreateText(card.transform, "Note", font, req.requestNote, 15, FontStyle.Normal,
                new Color(0.76f, 0.85f, 0.94f, 0.96f),
                TextAnchor.UpperLeft, new Vector2(0.05f, 0.08f), new Vector2(0.60f, 0.50f), Vector2.zero, Vector2.zero);
            noteTxt.horizontalOverflow = HorizontalWrapMode.Wrap;
            noteTxt.verticalOverflow = VerticalWrapMode.Truncate;

            // 同意 / 拒绝按钮
            Button acceptBtn = CreateStyledButton(card.transform, "AcceptBtn", font, "同 意", 19, BtnGreenColor,
                new Vector2(0.70f, 0.5f), new Vector2(0.70f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            RectTransform acRT = acceptBtn.GetComponent<RectTransform>();
            acRT.anchorMin = new Vector2(0.62f, 0.20f);
            acRT.anchorMax = new Vector2(0.79f, 0.80f);
            acRT.offsetMin = Vector2.zero;
            acRT.offsetMax = Vector2.zero;

            Button rejectBtn = CreateStyledButton(card.transform, "RejectBtn", font, "拒 绝", 19, BtnRedColor,
                new Vector2(0.89f, 0.5f), new Vector2(0.89f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            RectTransform rjRT = rejectBtn.GetComponent<RectTransform>();
            rjRT.anchorMin = new Vector2(0.805f, 0.20f);
            rjRT.anchorMax = new Vector2(0.975f, 0.80f);
            rjRT.offsetMin = Vector2.zero;
            rjRT.offsetMax = Vector2.zero;

            string capturedId = req.playerId;
            acceptBtn.onClick.AddListener(() =>
            {
                bool ok = FriendManager.AcceptRequest(capturedId, out string msg);
                if (ok) UISoundManager.PlaySave();
                ShowToast(msg, ok);
            });
            rejectBtn.onClick.AddListener(() =>
            {
                bool ok = FriendManager.RejectRequest(capturedId, out string msg);
                if (ok) UISoundManager.PlayClick();
                ShowToast(msg, false);
            });
        }

        private void ShowToast(string msg, bool positive)
        {
            if (_toastObj == null || _toastText == null) return;
            Image img = _toastObj.GetComponent<Image>();
            if (img != null)
            {
                img.color = positive
                    ? new Color(0.14f, 0.66f, 0.36f, 0.98f)
                    : new Color(0.46f, 0.30f, 0.24f, 0.98f);
            }
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
