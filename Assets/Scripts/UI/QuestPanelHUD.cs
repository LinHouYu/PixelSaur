using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PixelSaur.UI
{
    /// <summary>
    /// 任务系统主界面面板：
    /// 支持【每日任务 / 主线任务】分类页签切换、ScrollRect 滚动列表、
    /// 进度条与数字统计（如 0/5）、三态按钮切换（进行中 / 可领取 / 已领取）及领奖同步货币逻辑。
    /// </summary>
    public class QuestPanelHUD : MonoBehaviour
    {
        [Header("按钮样式资产")]
        [SerializeField] private Sprite buttonPillSprite;

        private RectTransform _panelRT;
        private Button _dailyTabBtn;
        private Image _dailyTabImg;
        private Text _dailyTabText;

        private Button _mainTabBtn;
        private Image _mainTabImg;
        private Text _mainTabText;

        private ScrollRect _scrollRect;
        private RectTransform _listContent;
        private GameObject _toastObj;
        private Text _toastText;

        private QuestCategory _activeCategory = QuestCategory.Daily;
        private bool _built;
        private float _toastHideTime;

        private static Sprite _whitePillSprite;

        private static readonly Color PanelBgColor = new Color(0.07f, 0.10f, 0.15f, 0.99f);
        private static readonly Color CardBgColor = new Color(0.12f, 0.16f, 0.24f, 0.98f);
        private static readonly Color TabActiveColor = new Color(0.16f, 0.62f, 0.36f, 1f);
        private static readonly Color TabNormalColor = new Color(0.18f, 0.24f, 0.33f, 1f);

        private static readonly Color BtnInProgressColor = new Color(0.24f, 0.31f, 0.42f, 1f);
        private static readonly Color BtnClaimableColor = new Color(0.16f, 0.74f, 0.36f, 1f);
        private static readonly Color BtnClaimedColor = new Color(0.16f, 0.19f, 0.24f, 1f);

        private void Awake()
        {
            EnsureUI();
        }

        private void OnEnable()
        {
            EnsureUI();
            QuestManager.OnDataChanged += HandleQuestDataChanged;
            RefreshUI();
        }

        private void OnDisable()
        {
            QuestManager.OnDataChanged -= HandleQuestDataChanged;
        }

        private void Update()
        {
            if (_toastObj != null && _toastObj.activeSelf && Time.unscaledTime >= _toastHideTime)
            {
                _toastObj.SetActive(false);
            }
        }

        private static Sprite GetWhitePillSprite()
        {
            if (_whitePillSprite != null) return _whitePillSprite;
            const int size = 64;
            const float r = 14f;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "QuestWhitePill_Procedural",
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
                    float alpha = Mathf.Clamp01(r + 0.5f - dist);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            _whitePillSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
            _whitePillSprite.hideFlags = HideFlags.HideAndDontSave;
            return _whitePillSprite;
        }

        private void HandleQuestDataChanged()
        {
            if (gameObject.activeInHierarchy)
            {
                RefreshUI();
            }
        }

        public void OpenDefault()
        {
            _activeCategory = QuestCategory.Daily;
            if (UIPanelManager.Instance != null)
            {
                UIPanelManager.Instance.OpenPanel("QuestPanel");
            }
            else
            {
                gameObject.SetActive(true);
                transform.SetAsLastSibling();
            }
            RefreshUI(resetScroll: true);
        }

        private void SwitchCategory(QuestCategory cat)
        {
            if (_activeCategory == cat) return;
            UISoundManager.PlayClick();
            _activeCategory = cat;
            RefreshUI(resetScroll: true);
        }

        public void EnsureUI()
        {
            if (_built) return;
            _built = true;

            // 清理编辑器预构建的旧子节点，防止运行时 Awake 重建产生重复控件
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }

            Sprite pillSprite = GetWhitePillSprite();
#if UNITY_EDITOR
            if (buttonPillSprite == null)
            {
                buttonPillSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/ui_bevel_pill_64x64.png");
            }
#endif

            _panelRT = GetComponent<RectTransform>();
            if (_panelRT == null) _panelRT = gameObject.AddComponent<RectTransform>();
            _panelRT.anchorMin = new Vector2(0.10f, 0.09f);
            _panelRT.anchorMax = new Vector2(0.90f, 0.91f);
            _panelRT.offsetMin = Vector2.zero;
            _panelRT.offsetMax = Vector2.zero;
            _panelRT.pivot = new Vector2(0.5f, 0.5f);

            Image panelBg = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            panelBg.sprite = pillSprite;
            panelBg.type = Image.Type.Sliced;
            panelBg.color = PanelBgColor;
            panelBg.raycastTarget = true;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 1. 顶部标题与关闭 X
            CreateText(transform, "Title", font, "任 务 中 心", 38, FontStyle.Bold, Color.white,
                TextAnchor.MiddleLeft, new Vector2(0f, 0.87f), new Vector2(0.6f, 1f), new Vector2(38f, 0f), new Vector2(0f, -6f));

            Button closeBtn = CreateStyledButton(transform, "CloseBtn", font, "✕", 32, new Color(0.46f, 0.18f, 0.20f, 1f),
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-26f, -20f), new Vector2(68f, 68f));
            closeBtn.onClick.AddListener(() =>
            {
                if (UIPanelManager.Instance != null) UIPanelManager.Instance.ClosePanel("QuestPanel");
                else { UISoundManager.PlayClose(); gameObject.SetActive(false); }
            });

            // 2. 分类页签（每日任务 / 主线任务）
            _dailyTabBtn = CreateStyledButton(transform, "DailyTabBtn", font, "每日任务", 26, TabActiveColor,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -104f), new Vector2(240f, 62f));
            _dailyTabImg = _dailyTabBtn.GetComponent<Image>();
            _dailyTabText = _dailyTabBtn.GetComponentInChildren<Text>();
            _dailyTabBtn.onClick.AddListener(() => SwitchCategory(QuestCategory.Daily));

            _mainTabBtn = CreateStyledButton(transform, "MainTabBtn", font, "主线任务", 26, TabNormalColor,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(296f, -104f), new Vector2(240f, 62f));
            _mainTabImg = _mainTabBtn.GetComponent<Image>();
            _mainTabText = _mainTabBtn.GetComponentInChildren<Text>();
            _mainTabBtn.onClick.AddListener(() => SwitchCategory(QuestCategory.Main));

            // 3. 滚动任务列表区
            GameObject scrollGo = new GameObject("QuestScrollView", typeof(RectTransform), typeof(ScrollRect));
            scrollGo.transform.SetParent(transform, false);
            RectTransform scrollRT = scrollGo.GetComponent<RectTransform>();
            scrollRT.anchorMin = new Vector2(0.03f, 0.05f);
            scrollRT.anchorMax = new Vector2(0.97f, 0.77f);
            scrollRT.offsetMin = Vector2.zero;
            scrollRT.offsetMax = Vector2.zero;

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
            _listContent = contentGo.GetComponent<RectTransform>();
            _listContent.anchorMin = new Vector2(0f, 1f);
            _listContent.anchorMax = new Vector2(1f, 1f);
            _listContent.pivot = new Vector2(0.5f, 1f);
            _listContent.anchoredPosition = Vector2.zero;
            _listContent.sizeDelta = new Vector2(0f, 200f);

            _scrollRect = scrollGo.GetComponent<ScrollRect>();
            _scrollRect.viewport = vpRT;
            _scrollRect.content = _listContent;
            _scrollRect.horizontal = false;
            _scrollRect.vertical = true;
            _scrollRect.inertia = true;
            _scrollRect.scrollSensitivity = 28f;
            _scrollRect.movementType = ScrollRect.MovementType.Clamped;

            // 4. 底部领奖 Toast 提示
            _toastObj = new GameObject("QuestToast", typeof(RectTransform), typeof(Image));
            _toastObj.transform.SetParent(transform, false);
            RectTransform toastRT = _toastObj.GetComponent<RectTransform>();
            toastRT.anchorMin = new Vector2(0.5f, 0f);
            toastRT.anchorMax = new Vector2(0.5f, 0f);
            toastRT.pivot = new Vector2(0.5f, 0f);
            toastRT.anchoredPosition = new Vector2(0f, 22f);
            toastRT.sizeDelta = new Vector2(480f, 56f);
            Image toastImg = _toastObj.GetComponent<Image>();
            toastImg.sprite = pillSprite;
            toastImg.type = Image.Type.Sliced;
            toastImg.color = new Color(0.14f, 0.68f, 0.36f, 0.98f);
            _toastText = CreateText(_toastObj.transform, "Text", font, "", 24, FontStyle.Bold, Color.white,
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _toastObj.SetActive(false);
        }

        public void RefreshUI(bool resetScroll = false)
        {
            if (!_built) EnsureUI();

            int dailyClaimable = QuestManager.GetClaimableCount(QuestCategory.Daily);
            int mainClaimable = QuestManager.GetClaimableCount(QuestCategory.Main);

            if (_dailyTabImg != null) _dailyTabImg.color = _activeCategory == QuestCategory.Daily ? TabActiveColor : TabNormalColor;
            if (_mainTabImg != null) _mainTabImg.color = _activeCategory == QuestCategory.Main ? TabActiveColor : TabNormalColor;

            if (_dailyTabText != null) _dailyTabText.text = dailyClaimable > 0 ? $"每日任务 ({dailyClaimable})" : "每日任务";
            if (_mainTabText != null) _mainTabText.text = mainClaimable > 0 ? $"主线任务 ({mainClaimable})" : "主线任务";

            for (int i = _listContent.childCount - 1; i >= 0; i--)
            {
                Destroy(_listContent.GetChild(i).gameObject);
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            List<QuestItemData> quests = QuestManager.GetQuestsByCategory(_activeCategory);

            float y = 0f;
            const float cardHeight = 142f;
            const float spacing = 16f;

            for (int i = 0; i < quests.Count; i++)
            {
                BuildQuestCard(quests[i], font, y, cardHeight);
                y += cardHeight + spacing;
            }

            _listContent.sizeDelta = new Vector2(0f, Mathf.Max(100f, y));
            Canvas.ForceUpdateCanvases();
            if (resetScroll && _scrollRect != null)
            {
                _scrollRect.velocity = Vector2.zero;
                _scrollRect.verticalNormalizedPosition = 1f;
            }
        }

        private void BuildQuestCard(QuestItemData quest, Font font, float posY, float height)
        {
            GameObject cardGo = new GameObject("Quest_" + quest.id, typeof(RectTransform), typeof(Image));
            cardGo.transform.SetParent(_listContent, false);
            RectTransform rt = cardGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -posY);
            rt.sizeDelta = new Vector2(0f, height);

            Image bg = cardGo.GetComponent<Image>();
            bg.sprite = GetWhitePillSprite();
            bg.type = Image.Type.Sliced;
            bg.color = CardBgColor;

            // 左侧标题 + 说明 + 奖励文本
            CreateText(cardGo.transform, "Title", font, quest.title, 27, FontStyle.Bold, Color.white,
                TextAnchor.MiddleLeft, new Vector2(0f, 0.54f), new Vector2(0.54f, 1f), new Vector2(24f, 0f), new Vector2(0f, -6f));

            CreateText(cardGo.transform, "Desc", font, quest.desc, 19, FontStyle.Bold, new Color(0.72f, 0.82f, 0.92f, 0.95f),
                TextAnchor.MiddleLeft, new Vector2(0f, 0.24f), new Vector2(0.54f, 0.56f), new Vector2(24f, 0f), Vector2.zero);

            string rewardLabel = quest.rewardType == QuestRewardType.Gold
                ? $"奖励：+{quest.rewardAmount} 金币"
                : $"奖励：+{quest.rewardAmount} 琥珀";
            Color rewardColor = quest.rewardType == QuestRewardType.Gold
                ? new Color(1.0f, 0.84f, 0.30f, 1f)
                : new Color(0.42f, 0.86f, 1.0f, 1f);

            CreateText(cardGo.transform, "Reward", font, rewardLabel, 20, FontStyle.Bold, rewardColor,
                TextAnchor.MiddleLeft, new Vector2(0f, 0f), new Vector2(0.54f, 0.28f), new Vector2(24f, 4f), Vector2.zero);

            // 中右进度条 + 数字统计 (如 0/5)
            BuildProgressBar(cardGo.transform, font, quest.currentProgress, quest.targetCount);

            // 最右侧三态按钮（进行中 / 可领取 / 已领取）
            QuestButtonState state = quest.GetState();
            string btnLabel = state == QuestButtonState.Claimed ? "已领取" :
                              state == QuestButtonState.Claimable ? "领 取" : "进行中";
            Color btnColor = state == QuestButtonState.Claimed ? BtnClaimedColor :
                             state == QuestButtonState.Claimable ? BtnClaimableColor : BtnInProgressColor;

            Button actionBtn = CreateStyledButton(cardGo.transform, "ActionBtn", font, btnLabel, 24, btnColor,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(172f, 66f));
            actionBtn.interactable = state == QuestButtonState.Claimable;
            if (state == QuestButtonState.Claimed)
            {
                Text txt = actionBtn.GetComponentInChildren<Text>();
                if (txt != null) txt.color = new Color(0.65f, 0.69f, 0.75f, 0.90f);
            }

            if (state == QuestButtonState.Claimable)
            {
                string capturedId = quest.id;
                actionBtn.onClick.AddListener(() =>
                {
                    if (QuestManager.TryClaimReward(capturedId, out QuestRewardType rType, out int rAmt))
                    {
                        UISoundManager.PlaySave();
                        string unit = rType == QuestRewardType.Gold ? "金币" : "琥珀";
                        ShowToast($"✓ 领取成功：+{rAmt} {unit}！");
                    }
                });
            }
        }

        private void BuildProgressBar(Transform parent, Font font, int cur, int target)
        {
            Sprite pillSprite = GetWhitePillSprite();
#if UNITY_EDITOR
            if (buttonPillSprite == null)
            {
                buttonPillSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/ui_bevel_pill_64x64.png");
            }
#endif

            GameObject boxGo = new GameObject("ProgressBox", typeof(RectTransform));
            boxGo.transform.SetParent(parent, false);
            RectTransform boxRT = boxGo.GetComponent<RectTransform>();
            boxRT.anchorMin = new Vector2(0.55f, 0.15f);
            boxRT.anchorMax = new Vector2(0.81f, 0.85f);
            boxRT.offsetMin = Vector2.zero;
            boxRT.offsetMax = Vector2.zero;

            // 数字统计文本 (如 0/5)
            CreateText(boxGo.transform, "CountText", font, $"{cur}/{target}", 23, FontStyle.Bold, Color.white,
                TextAnchor.MiddleCenter, new Vector2(0f, 0.52f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

            // 进度条底框
            GameObject barBg = new GameObject("BarBg", typeof(RectTransform), typeof(Image), typeof(Slider));
            barBg.transform.SetParent(boxGo.transform, false);
            RectTransform barRT = barBg.GetComponent<RectTransform>();
            barRT.anchorMin = new Vector2(0.06f, 0.22f);
            barRT.anchorMax = new Vector2(0.94f, 0.48f);
            barRT.offsetMin = Vector2.zero;
            barRT.offsetMax = Vector2.zero;
            Image bgImg = barBg.GetComponent<Image>();
            bgImg.sprite = pillSprite;
            bgImg.type = Image.Type.Sliced;
            bgImg.color = new Color(0.05f, 0.08f, 0.12f, 1f);

            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(barBg.transform, false);
            RectTransform faRT = fillArea.GetComponent<RectTransform>();
            faRT.anchorMin = Vector2.zero;
            faRT.anchorMax = Vector2.one;
            faRT.offsetMin = new Vector2(3f, 3f);
            faRT.offsetMax = new Vector2(-3f, -3f);

            GameObject fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(fillArea.transform, false);
            RectTransform fRT = fillGo.GetComponent<RectTransform>();
            fRT.anchorMin = Vector2.zero;
            fRT.anchorMax = Vector2.one;
            fRT.offsetMin = Vector2.zero;
            fRT.offsetMax = Vector2.zero;
            Image fillImg = fillGo.GetComponent<Image>();
            fillImg.sprite = pillSprite;
            fillImg.type = Image.Type.Sliced;
            fillImg.color = cur >= target ? new Color(0.20f, 0.84f, 0.42f, 1f) : new Color(0.24f, 0.66f, 0.98f, 1f);

            Slider slider = barBg.GetComponent<Slider>();
            slider.interactable = false;
            slider.transition = Selectable.Transition.None;
            slider.fillRect = fRT;
            slider.minValue = 0f;
            slider.maxValue = Mathf.Max(1, target);
            slider.value = Mathf.Clamp(cur, 0, target);
        }

        private void ShowToast(string msg)
        {
            if (_toastObj == null || _toastText == null) return;
            _toastText.text = msg;
            _toastObj.SetActive(true);
            _toastObj.transform.SetAsLastSibling();
            _toastHideTime = Time.unscaledTime + 1.8f;
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
            img.sprite = buttonPillSprite != null ? buttonPillSprite : GetWhitePillSprite();
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
            Outline outline = txt.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.90f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
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
    }
}
