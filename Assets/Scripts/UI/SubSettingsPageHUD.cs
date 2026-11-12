using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PixelSaur.UI
{
    public enum SettingsCategory
    {
        Graphics,
        Controls
    }

    /// <summary>
    /// 全屏次级设置页面（画面设置 / 游戏控制）
    /// 1. 从左到右平滑掩盖全屏，彻底隔绝背后游戏干扰；
    /// 2. 右上角 X 关闭按钮，右下角 SAVE 保存按钮，风格统一；
    /// 3. 内置 iOS 风格高保真平滑拨动开关（iPhone Toggle Switch）；
    /// 4. 严密的保存检测机制：有改动未保存直接按 X 时，弹出“检测到未保存是否保存”弹窗。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SubSettingsPageHUD : MonoBehaviour
    {
        public static SubSettingsPageHUD Instance { get; private set; }

        [Header("样式与资源")]
        [SerializeField] private Sprite roundedPillSprite;
        [SerializeField] private Color fullScreenCoverColor = new Color(0.05f, 0.07f, 0.10f, 1.00f);
        [SerializeField] private Color cardBgColor = new Color(0.09f, 0.13f, 0.18f, 0.92f);
        [SerializeField] private Color closeBtnColor = new Color(0.25f, 0.16f, 0.18f, 0.92f);
        [SerializeField] private Color saveBtnNormalColor = new Color(0.14f, 0.20f, 0.28f, 0.92f);
        [SerializeField] private Color saveBtnDirtyColor = new Color(0.12f, 0.55f, 0.32f, 0.98f);

        [Header("动画时长")]
        [SerializeField] private float slideDuration = 0.28f;

        // UI 核心节点
        private RectTransform _rootRect;
        private RectTransform _slideRoot;
        private Image _bgCover;
        private Text _titleText;
        private Button _closeButton;
        private Button _saveButton;
        private Image _saveButtonImage;
        private Text _saveButtonText;
        private Transform _itemsContainer;
        private GameObject _toastObj;
        private Text _toastText;
        private CanvasGroup _toastGroup;

        // 弹窗节点
        private GameObject _modalDialogObj;
        private Text _modalTitleText;
        private Text _modalMessageText;
        private Button _modalSaveAndExitBtn;
        private Button _modalDiscardBtn;
        private Button _modalCancelBtn;

        // 运行状态
        private SettingsCategory _currentCategory = SettingsCategory.Graphics;
        private bool _isOpen = false;
        public bool IsOpen => _isOpen;
        private Coroutine _animCoroutine;
        private Coroutine _toastCoroutine;

        // 键值管理与脏标记
        private readonly Dictionary<string, int> _committedSettings = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _draftSettings = new Dictionary<string, int>();
        private readonly List<ToggleItemView> _activeToggleViews = new List<ToggleItemView>();

        private class SettingItemDef
        {
            public string key;
            public string title;
            public string desc;
            public int defaultValue;
        }

        private static readonly SettingItemDef[] GraphicsDefs = new[]
        {
            new SettingItemDef { key = "SETTING_GRAPHICS_HIGH_FPS", title = "高帧率模式 (60/120 FPS)", desc = "开启后提供更流畅的移动与动画体验", defaultValue = 1 },
            new SettingItemDef { key = "SETTING_GRAPHICS_WATER_RIPPLE", title = "水面物理涟漪 (Water Ripples)", desc = "允许角色划水时产生扩散水波渲染", defaultValue = 1 },
            new SettingItemDef { key = "SETTING_GRAPHICS_GRASS_WIND", title = "草丛物理摇曳 (Grass Physics)", desc = "草地在被碰撞与风吹时呈现平滑弯曲", defaultValue = 1 }
        };

        private static readonly SettingItemDef[] ControlsDefs = new[]
        {
            new SettingItemDef { key = "SETTING_CTRL_DYNAMIC_JOYSTICK", title = "虚拟摇杆跟随模式", desc = "摇杆在手指触碰屏幕左半区任意位置动态浮现", defaultValue = 0 },
            new SettingItemDef { key = "SETTING_CTRL_HAPTICS", title = "操作触觉震动反馈", desc = "攻击、受击及交互时触发设备微震动", defaultValue = 1 },
            new SettingItemDef { key = "SETTING_CTRL_ATTACK_LOCK", title = "按键连续锁定", desc = "防止误触退出锁定长按连续攻击状态", defaultValue = 1 }
        };

        private class ToggleItemView
        {
            public string key;
            public bool currentVal;
            public Image trackImage;
            public RectTransform thumbRT;
            public Button button;
            public Coroutine anim;
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            LoadDefaultSprites();
            EnsureUIHierarchy();
        }

        private void LoadDefaultSprites()
        {
#if UNITY_EDITOR
            if (roundedPillSprite == null)
            {
                roundedPillSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/hud_pill_bg_64x64.png");
            }
#endif
        }

        public static void OpenPage(SettingsCategory category)
        {
            if (Instance == null)
            {
                Canvas canvas = FindAnyObjectByType<Canvas>();
                if (canvas != null)
                {
                    Transform t = canvas.transform.Find("SubSettingsPage");
                    GameObject go = t != null ? t.gameObject : new GameObject("SubSettingsPage");
                    Instance = go.GetComponent<SubSettingsPageHUD>() ?? go.AddComponent<SubSettingsPageHUD>();
                }
            }

            if (Instance != null)
            {
                Instance.Open(category);
            }
        }

        public void Open(SettingsCategory category)
        {
            _currentCategory = category;
            LoadDefaultSprites();
            EnsureUIHierarchy();
            transform.SetAsLastSibling(); // 置于最顶层，彻底遮盖住一切背后画面

            // 读取已保存的数据
            LoadSettingsData();

            // 构建当前类别的设置列表
            RebuildItemsList();

            // 刷新 Save 按钮外观
            UpdateSaveButtonState();

            // 关闭可能遗留的弹窗与 toast
            if (_modalDialogObj != null) _modalDialogObj.SetActive(false);
            if (_toastObj != null) _toastObj.SetActive(false);

            if (_animCoroutine != null) StopCoroutine(_animCoroutine);
            gameObject.SetActive(true);
            _isOpen = true;

            if (Application.isPlaying)
            {
                _animCoroutine = StartCoroutine(AnimateSlide(true));
            }
            else
            {
                if (_slideRoot != null) _slideRoot.anchoredPosition = Vector2.zero;
            }
        }

        public void CloseDirectly()
        {
            if (_modalDialogObj != null) _modalDialogObj.SetActive(false);
            if (_animCoroutine != null) StopCoroutine(_animCoroutine);

            if (Application.isPlaying && gameObject.activeInHierarchy)
            {
                _animCoroutine = StartCoroutine(AnimateSlide(false));
            }
            else
            {
                _isOpen = false;
                if (_slideRoot != null) _slideRoot.anchoredPosition = new Vector2(-GetCanvasWidth(), 0);
                gameObject.SetActive(false);
            }
        }

        private float GetCanvasWidth()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                RectTransform crt = canvas.GetComponent<RectTransform>();
                if (crt != null && crt.rect.width > 0) return crt.rect.width;
            }
            return Screen.width > 0 ? Screen.width : 1920f;
        }

        private IEnumerator AnimateSlide(bool open)
        {
            float width = GetCanvasWidth();
            float startX = open ? -width : 0f;
            float targetX = open ? 0f : -width;

            float elapsed = 0f;
            while (elapsed < slideDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / slideDuration);
                // EaseOutCubic
                float ease = 1f - Mathf.Pow(1f - t, 3);
                float curX = Mathf.Lerp(startX, targetX, ease);

                if (_slideRoot != null)
                {
                    _slideRoot.anchoredPosition = new Vector2(curX, 0);
                }
                yield return null;
            }

            if (_slideRoot != null) _slideRoot.anchoredPosition = new Vector2(targetX, 0);

            if (!open)
            {
                _isOpen = false;
                gameObject.SetActive(false);
            }
            _animCoroutine = null;
        }

        private void LoadSettingsData()
        {
            _committedSettings.Clear();
            _draftSettings.Clear();

            SettingItemDef[] defs = _currentCategory == SettingsCategory.Graphics ? GraphicsDefs : ControlsDefs;
            foreach (var def in defs)
            {
                int val = PlayerPrefs.GetInt(def.key, def.defaultValue);
                _committedSettings[def.key] = val;
                _draftSettings[def.key] = val;
            }
        }

        private bool IsDirty()
        {
            foreach (var kvp in _draftSettings)
            {
                if (!_committedSettings.TryGetValue(kvp.Key, out int comVal) || comVal != kvp.Value)
                {
                    return true;
                }
            }
            return false;
        }

        private void OnToggleClicked(ToggleItemView view)
        {
            bool newVal = !view.currentVal;
            view.currentVal = newVal;
            _draftSettings[view.key] = newVal ? 1 : 0;

            // 触发 iOS 拨动动画
            if (view.anim != null) StopCoroutine(view.anim);
            view.anim = StartCoroutine(AnimateSwitch(view, newVal));

            UpdateSaveButtonState();
        }

        private IEnumerator AnimateSwitch(ToggleItemView view, bool on)
        {
            float startX = view.thumbRT.anchoredPosition.x;
            float targetX = on ? 15f : -15f;
            Color startColor = view.trackImage.color;
            Color targetColor = on ? new Color(0.20f, 0.78f, 0.35f, 1f) : new Color(0.24f, 0.29f, 0.36f, 1f);

            float duration = 0.16f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = Mathf.SmoothStep(0f, 1f, t);

                view.thumbRT.anchoredPosition = new Vector2(Mathf.Lerp(startX, targetX, ease), 0);
                view.trackImage.color = Color.Lerp(startColor, targetColor, ease);
                yield return null;
            }

            view.thumbRT.anchoredPosition = new Vector2(targetX, 0);
            view.trackImage.color = targetColor;
            view.anim = null;
        }

        private void UpdateSaveButtonState()
        {
            bool dirty = IsDirty();
            if (_saveButtonImage != null)
            {
                _saveButtonImage.color = dirty ? saveBtnDirtyColor : saveBtnNormalColor;
            }
            if (_saveButtonText != null)
            {
                _saveButtonText.text = dirty ? "保 存 *" : "保 存";
                _saveButtonText.color = dirty ? Color.white : new Color(0.80f, 0.86f, 0.94f, 1f);
            }
        }

        private void HandleSaveClick()
        {
            // 将草稿提交并写入 PlayerPrefs
            foreach (var kvp in _draftSettings)
            {
                _committedSettings[kvp.Key] = kvp.Value;
                PlayerPrefs.SetInt(kvp.Key, kvp.Value);
            }
            PlayerPrefs.Save();

            // 真实触发游戏内状态应用
            ApplyActiveSettings();

            UpdateSaveButtonState();
            ShowToast("✓ 设置已成功保存生效！");
        }

        private void ApplyActiveSettings()
        {
            if (_committedSettings.TryGetValue("SETTING_GRAPHICS_HIGH_FPS", out int fpsVal))
            {
                Application.targetFrameRate = fpsVal == 1 ? 120 : 60;
                Debug.Log($"<color=#34C759>[SubSettingsPageHUD] 高帧率设置已应用: targetFrameRate = {Application.targetFrameRate}</color>");
            }

            if (_committedSettings.TryGetValue("SETTING_GRAPHICS_WATER_RIPPLE", out int rippleVal))
            {
                var ripple = FindAnyObjectByType<Water.WaterRippleManager>();
                if (ripple != null) ripple.enabled = rippleVal == 1;
            }

            if (_committedSettings.TryGetValue("SETTING_CTRL_DYNAMIC_JOYSTICK", out int joyVal))
            {
                Debug.Log($"<color=#34C759>[SubSettingsPageHUD] 虚拟摇杆跟随模式已应用: {joyVal == 1}</color>");
            }
        }

        private void HandleCloseClick()
        {
            if (IsDirty())
            {
                // 玩家拨动了开关但未保存：弹出确认提示弹窗
                ShowUnsavedDialog();
            }
            else
            {
                CloseDirectly();
            }
        }

        private void ShowUnsavedDialog()
        {
            if (_modalDialogObj == null) return;
            _modalDialogObj.SetActive(true);
            _modalDialogObj.transform.SetAsLastSibling();
        }

        private void OnModalSaveAndExit()
        {
            HandleSaveClick();
            if (_modalDialogObj != null) _modalDialogObj.SetActive(false);
            CloseDirectly();
        }

        private void OnModalDiscardAndExit()
        {
            // 还原所有草稿
            foreach (var kvp in _committedSettings)
            {
                _draftSettings[kvp.Key] = kvp.Value;
            }
            if (_modalDialogObj != null) _modalDialogObj.SetActive(false);
            CloseDirectly();
        }

        private void OnModalCancel()
        {
            if (_modalDialogObj != null) _modalDialogObj.SetActive(false);
        }

        private void ShowToast(string message)
        {
            if (_toastObj == null) return;
            if (_toastCoroutine != null) StopCoroutine(_toastCoroutine);
            _toastCoroutine = StartCoroutine(ToastRoutine(message));
        }

        private IEnumerator ToastRoutine(string message)
        {
            _toastObj.SetActive(true);
            _toastText.text = message;
            _toastGroup.alpha = 0f;

            float elapsed = 0f;
            while (elapsed < 0.2f)
            {
                elapsed += Time.unscaledDeltaTime;
                _toastGroup.alpha = Mathf.Clamp01(elapsed / 0.2f);
                yield return null;
            }

            yield return new WaitForSecondsRealtime(1.2f);

            elapsed = 0f;
            while (elapsed < 0.3f)
            {
                elapsed += Time.unscaledDeltaTime;
                _toastGroup.alpha = 1f - Mathf.Clamp01(elapsed / 0.3f);
                yield return null;
            }

            _toastObj.SetActive(false);
            _toastCoroutine = null;
        }

        private void RebuildItemsList()
        {
            _activeToggleViews.Clear();

            // 清理旧子物件
            for (int i = _itemsContainer.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(_itemsContainer.GetChild(i).gameObject);
            }

            Font mainFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _titleText.text = _currentCategory == SettingsCategory.Graphics ? "画 面 设 置" : "游 戏 控 制";

            SettingItemDef[] defs = _currentCategory == SettingsCategory.Graphics ? GraphicsDefs : ControlsDefs;
            for (int idx = 0; idx < defs.Length; idx++)
            {
                var def = defs[idx];
                bool currentVal = _draftSettings[def.key] == 1;

                // 卡片外框
                GameObject card = new GameObject("Item_" + def.key, typeof(RectTransform), typeof(Image));
                card.transform.SetParent(_itemsContainer, false);
                RectTransform cardRT = card.GetComponent<RectTransform>();
                cardRT.anchorMin = new Vector2(0f, 1f);
                cardRT.anchorMax = new Vector2(1f, 1f);
                cardRT.pivot = new Vector2(0.5f, 1f);
                cardRT.sizeDelta = new Vector2(0f, 76f);
                cardRT.anchoredPosition = new Vector2(0f, -idx * 88f);

                Image cardImg = card.GetComponent<Image>();
                cardImg.sprite = roundedPillSprite;
                cardImg.type = Image.Type.Sliced;
                cardImg.color = cardBgColor;

                // 左侧标题与说明
                GameObject textObj = new GameObject("TextInfo", typeof(RectTransform));
                textObj.transform.SetParent(card.transform, false);
                RectTransform textRT = textObj.GetComponent<RectTransform>();
                textRT.anchorMin = new Vector2(0f, 0f);
                textRT.anchorMax = new Vector2(0.75f, 1f);
                textRT.offsetMin = new Vector2(22f, 8f);
                textRT.offsetMax = new Vector2(-10f, -8f);

                GameObject titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
                titleGo.transform.SetParent(textObj.transform, false);
                RectTransform titRT = titleGo.GetComponent<RectTransform>();
                titRT.anchorMin = new Vector2(0f, 0.45f);
                titRT.anchorMax = new Vector2(1f, 1f);
                titRT.offsetMin = Vector2.zero;
                titRT.offsetMax = Vector2.zero;
                Text titText = titleGo.GetComponent<Text>();
                titText.font = mainFont;
                titText.fontSize = 20;
                titText.fontStyle = FontStyle.Bold;
                titText.text = def.title;
                titText.color = Color.white;
                titText.alignment = TextAnchor.MiddleLeft;

                GameObject descGo = new GameObject("Desc", typeof(RectTransform), typeof(Text));
                descGo.transform.SetParent(textObj.transform, false);
                RectTransform descRT = descGo.GetComponent<RectTransform>();
                descRT.anchorMin = new Vector2(0f, 0f);
                descRT.anchorMax = new Vector2(1f, 0.45f);
                descRT.offsetMin = Vector2.zero;
                descRT.offsetMax = Vector2.zero;
                Text descText = descGo.GetComponent<Text>();
                descText.font = mainFont;
                descText.fontSize = 14;
                descText.text = def.desc;
                descText.color = new Color(0.65f, 0.74f, 0.85f, 0.85f);
                descText.alignment = TextAnchor.MiddleLeft;

                // 右侧 iOS 拨动开关
                GameObject switchGo = new GameObject("iOSToggleSwitch", typeof(RectTransform), typeof(Image), typeof(Button));
                switchGo.transform.SetParent(card.transform, false);
                RectTransform swRT = switchGo.GetComponent<RectTransform>();
                swRT.anchorMin = new Vector2(1f, 0.5f);
                swRT.anchorMax = new Vector2(1f, 0.5f);
                swRT.pivot = new Vector2(1f, 0.5f);
                swRT.anchoredPosition = new Vector2(-22f, 0f);
                swRT.sizeDelta = new Vector2(62f, 32f);

                Image trackImg = switchGo.GetComponent<Image>();
                trackImg.sprite = roundedPillSprite;
                trackImg.type = Image.Type.Sliced;
                trackImg.color = currentVal ? new Color(0.20f, 0.78f, 0.35f, 1f) : new Color(0.24f, 0.29f, 0.36f, 1f);

                // 圆形滑块 Thumb
                GameObject thumbGo = new GameObject("Thumb", typeof(RectTransform), typeof(Image));
                thumbGo.transform.SetParent(switchGo.transform, false);
                RectTransform thRT = thumbGo.GetComponent<RectTransform>();
                thRT.anchorMin = new Vector2(0.5f, 0.5f);
                thRT.anchorMax = new Vector2(0.5f, 0.5f);
                thRT.pivot = new Vector2(0.5f, 0.5f);
                thRT.sizeDelta = new Vector2(26f, 26f);
                thRT.anchoredPosition = new Vector2(currentVal ? 15f : -15f, 0f);

                Image thumbImg = thumbGo.GetComponent<Image>();
                thumbImg.sprite = roundedPillSprite;
                thumbImg.type = Image.Type.Sliced;
                thumbImg.color = Color.white;
                thumbImg.raycastTarget = false;

                Button swBtn = switchGo.GetComponent<Button>();
                swBtn.transition = Selectable.Transition.None;

                ToggleItemView view = new ToggleItemView
                {
                    key = def.key,
                    currentVal = currentVal,
                    trackImage = trackImg,
                    thumbRT = thRT,
                    button = swBtn
                };

                swBtn.onClick.AddListener(() => OnToggleClicked(view));
                _activeToggleViews.Add(view);
            }
        }

        public void EnsureUIHierarchy()
        {
            _rootRect = GetComponent<RectTransform>();
            if (_rootRect == null) _rootRect = gameObject.AddComponent<RectTransform>();

            _rootRect.anchorMin = Vector2.zero;
            _rootRect.anchorMax = Vector2.one;
            _rootRect.offsetMin = Vector2.zero;
            _rootRect.offsetMax = Vector2.zero;
            _rootRect.pivot = new Vector2(0.5f, 0.5f);

            // 1. 全屏纯色底层遮罩（不加载外部图片拉伸，彻底挡住游戏画面）
            Transform coverTr = transform.Find("FullScreenCover");
            GameObject coverObj = coverTr != null ? coverTr.gameObject : new GameObject("FullScreenCover", typeof(RectTransform), typeof(Image));
            coverObj.transform.SetParent(transform, false);
            coverObj.transform.SetSiblingIndex(0);

            RectTransform coverRT = coverObj.GetComponent<RectTransform>();
            coverRT.anchorMin = Vector2.zero;
            coverRT.anchorMax = Vector2.one;
            coverRT.offsetMin = Vector2.zero;
            coverRT.offsetMax = Vector2.zero;

            _bgCover = coverObj.GetComponent<Image>();
            _bgCover.sprite = null;
            _bgCover.color = fullScreenCoverColor;
            _bgCover.raycastTarget = true;

            // 2. 从左向右滑动的容器
            Transform slideTr = transform.Find("SlideRoot");
            GameObject slideObj = slideTr != null ? slideTr.gameObject : new GameObject("SlideRoot", typeof(RectTransform), typeof(Image));
            slideObj.transform.SetParent(transform, false);
            slideObj.transform.SetSiblingIndex(1);

            Image slideBg = slideObj.GetComponent<Image>();
            if (slideBg == null) slideBg = slideObj.AddComponent<Image>();
            slideBg.sprite = null;
            slideBg.color = fullScreenCoverColor;
            slideBg.raycastTarget = true;

            _slideRoot = slideObj.GetComponent<RectTransform>();
            _slideRoot.anchorMin = Vector2.zero;
            _slideRoot.anchorMax = Vector2.one;
            _slideRoot.offsetMin = Vector2.zero;
            _slideRoot.offsetMax = Vector2.zero;
            _slideRoot.pivot = new Vector2(0.5f, 0.5f);

            Font mainFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 2.1 顶部 Header
            Transform headerTr = _slideRoot.Find("Header");
            GameObject headerObj = headerTr != null ? headerTr.gameObject : new GameObject("Header", typeof(RectTransform));
            headerObj.transform.SetParent(_slideRoot, false);

            RectTransform headerRT = headerObj.GetComponent<RectTransform>();
            headerRT.anchorMin = new Vector2(0f, 1f);
            headerRT.anchorMax = new Vector2(1f, 1f);
            headerRT.pivot = new Vector2(0.5f, 1f);
            headerRT.anchoredPosition = Vector2.zero;
            headerRT.sizeDelta = new Vector2(0f, 85f);

            // 标题文字
            Transform titleTr = headerObj.transform.Find("TitleText");
            GameObject titleObj = titleTr != null ? titleTr.gameObject : new GameObject("TitleText", typeof(RectTransform), typeof(Text));
            titleObj.transform.SetParent(headerObj.transform, false);

            RectTransform titRT = titleObj.GetComponent<RectTransform>();
            titRT.anchorMin = new Vector2(0f, 0f);
            titRT.anchorMax = new Vector2(0.6f, 1f);
            titRT.offsetMin = new Vector2(36f, 0f);
            titRT.offsetMax = Vector2.zero;

            _titleText = titleObj.GetComponent<Text>();
            _titleText.font = mainFont;
            _titleText.fontSize = 28;
            _titleText.fontStyle = FontStyle.Bold;
            _titleText.text = "画 面 设 置";
            _titleText.color = Color.white;
            _titleText.alignment = TextAnchor.MiddleLeft;

            // 装饰细线
            Transform lineTr = headerObj.transform.Find("SepLine");
            GameObject lineObj = lineTr != null ? lineTr.gameObject : new GameObject("SepLine", typeof(RectTransform), typeof(Image));
            lineObj.transform.SetParent(headerObj.transform, false);
            RectTransform lineRT = lineObj.GetComponent<RectTransform>();
            lineRT.anchorMin = new Vector2(0f, 0f);
            lineRT.anchorMax = new Vector2(1f, 0f);
            lineRT.sizeDelta = new Vector2(0f, 2f);
            lineRT.anchoredPosition = Vector2.zero;
            Image lineImg = lineObj.GetComponent<Image>();
            lineImg.color = new Color(0.2f, 0.28f, 0.38f, 0.45f);

            // 右上角 X 关闭按钮
            Transform closeTr = headerObj.transform.Find("CloseButton");
            GameObject closeObj = closeTr != null ? closeTr.gameObject : new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            closeObj.transform.SetParent(headerObj.transform, false);

            RectTransform closeRT = closeObj.GetComponent<RectTransform>();
            closeRT.anchorMin = new Vector2(1f, 0.5f);
            closeRT.anchorMax = new Vector2(1f, 0.5f);
            closeRT.pivot = new Vector2(1f, 0.5f);
            closeRT.anchoredPosition = new Vector2(-28f, 0f);
            closeRT.sizeDelta = new Vector2(50f, 50f);

            Image closeImg = closeObj.GetComponent<Image>();
            closeImg.sprite = roundedPillSprite;
            closeImg.type = Image.Type.Sliced;
            closeImg.color = closeBtnColor;

            _closeButton = closeObj.GetComponent<Button>();
            _closeButton.targetGraphic = closeImg;
            _closeButton.onClick.RemoveListener(HandleCloseClick);
            _closeButton.onClick.AddListener(HandleCloseClick);

            Transform xTextTr = closeObj.transform.Find("Text");
            GameObject xTextObj = xTextTr != null ? xTextTr.gameObject : new GameObject("Text", typeof(RectTransform), typeof(Text));
            xTextObj.transform.SetParent(closeObj.transform, false);
            RectTransform xTRT = xTextObj.GetComponent<RectTransform>();
            xTRT.anchorMin = Vector2.zero;
            xTRT.anchorMax = Vector2.one;
            xTRT.offsetMin = Vector2.zero;
            xTRT.offsetMax = Vector2.zero;
            Text xTxt = xTextObj.GetComponent<Text>();
            xTxt.font = mainFont;
            xTxt.fontSize = 24;
            xTxt.fontStyle = FontStyle.Bold;
            xTxt.text = "✕";
            xTxt.color = new Color(1f, 0.85f, 0.85f, 1f);
            xTxt.alignment = TextAnchor.MiddleCenter;

            // 2.2 内容列表 ItemsContainer
            Transform contTr = _slideRoot.Find("ItemsContainer");
            GameObject contObj = contTr != null ? contTr.gameObject : new GameObject("ItemsContainer", typeof(RectTransform));
            contObj.transform.SetParent(_slideRoot, false);

            RectTransform contRT = contObj.GetComponent<RectTransform>();
            contRT.anchorMin = new Vector2(0.04f, 0.15f);
            contRT.anchorMax = new Vector2(0.96f, 0.86f);
            contRT.offsetMin = Vector2.zero;
            contRT.offsetMax = Vector2.zero;
            _itemsContainer = contObj.transform;

            // 2.3 右下角 SAVE 按钮
            Transform saveTr = _slideRoot.Find("SaveButton");
            GameObject saveObj = saveTr != null ? saveTr.gameObject : new GameObject("SaveButton", typeof(RectTransform), typeof(Image), typeof(Button));
            saveObj.transform.SetParent(_slideRoot, false);

            RectTransform saveRT = saveObj.GetComponent<RectTransform>();
            saveRT.anchorMin = new Vector2(1f, 0f);
            saveRT.anchorMax = new Vector2(1f, 0f);
            saveRT.pivot = new Vector2(1f, 0f);
            saveRT.anchoredPosition = new Vector2(-30f, 26f);
            saveRT.sizeDelta = new Vector2(160f, 54f);

            _saveButtonImage = saveObj.GetComponent<Image>();
            _saveButtonImage.sprite = roundedPillSprite;
            _saveButtonImage.type = Image.Type.Sliced;
            _saveButtonImage.color = saveBtnNormalColor;

            _saveButton = saveObj.GetComponent<Button>();
            _saveButton.targetGraphic = _saveButtonImage;
            _saveButton.onClick.RemoveListener(HandleSaveClick);
            _saveButton.onClick.AddListener(HandleSaveClick);

            Transform sTxtTr = saveObj.transform.Find("Text");
            GameObject sTxtObj = sTxtTr != null ? sTxtTr.gameObject : new GameObject("Text", typeof(RectTransform), typeof(Text));
            sTxtObj.transform.SetParent(saveObj.transform, false);
            RectTransform sTRT = sTxtObj.GetComponent<RectTransform>();
            sTRT.anchorMin = Vector2.zero;
            sTRT.anchorMax = Vector2.one;
            sTRT.offsetMin = Vector2.zero;
            sTRT.offsetMax = Vector2.zero;

            _saveButtonText = sTxtObj.GetComponent<Text>();
            _saveButtonText.font = mainFont;
            _saveButtonText.fontSize = 20;
            _saveButtonText.fontStyle = FontStyle.Bold;
            _saveButtonText.text = "保 存";
            _saveButtonText.color = Color.white;
            _saveButtonText.alignment = TextAnchor.MiddleCenter;

            // 2.4 飘字 Toast
            Transform toastTr = _slideRoot.Find("ToastPrompt");
            _toastObj = toastTr != null ? toastTr.gameObject : new GameObject("ToastPrompt", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            _toastObj.transform.SetParent(_slideRoot, false);
            RectTransform toastRT = _toastObj.GetComponent<RectTransform>();
            toastRT.anchorMin = new Vector2(0.5f, 0.18f);
            toastRT.anchorMax = new Vector2(0.5f, 0.18f);
            toastRT.pivot = new Vector2(0.5f, 0.5f);
            toastRT.sizeDelta = new Vector2(320f, 44f);
            toastRT.anchoredPosition = Vector2.zero;

            Image toastImg = _toastObj.GetComponent<Image>();
            toastImg.sprite = roundedPillSprite;
            toastImg.type = Image.Type.Sliced;
            toastImg.color = new Color(0.08f, 0.35f, 0.20f, 0.95f);

            _toastGroup = _toastObj.GetComponent<CanvasGroup>();

            Transform tTextTr = _toastObj.transform.Find("Text");
            GameObject tTextObj = tTextTr != null ? tTextTr.gameObject : new GameObject("Text", typeof(RectTransform), typeof(Text));
            tTextObj.transform.SetParent(_toastObj.transform, false);
            RectTransform tTRT = tTextObj.GetComponent<RectTransform>();
            tTRT.anchorMin = Vector2.zero;
            tTRT.anchorMax = Vector2.one;
            tTRT.offsetMin = Vector2.zero;
            tTRT.offsetMax = Vector2.zero;

            _toastText = tTextObj.GetComponent<Text>();
            _toastText.font = mainFont;
            _toastText.fontSize = 18;
            _toastText.fontStyle = FontStyle.Bold;
            _toastText.alignment = TextAnchor.MiddleCenter;
            _toastText.color = Color.white;
            _toastObj.SetActive(false);

            // 3. 未保存提示模态弹窗 (Modal Dialog)
            EnsureModalDialogHierarchy(mainFont);
        }

        private void EnsureModalDialogHierarchy(Font font)
        {
            Transform modalTr = transform.Find("UnsavedModalDialog");
            _modalDialogObj = modalTr != null ? modalTr.gameObject : new GameObject("UnsavedModalDialog", typeof(RectTransform), typeof(Image));
            _modalDialogObj.transform.SetParent(transform, false);

            RectTransform mRT = _modalDialogObj.GetComponent<RectTransform>();
            mRT.anchorMin = Vector2.zero;
            mRT.anchorMax = Vector2.one;
            mRT.offsetMin = Vector2.zero;
            mRT.offsetMax = Vector2.zero;

            Image mBg = _modalDialogObj.GetComponent<Image>();
            mBg.sprite = null;
            mBg.color = new Color(0f, 0f, 0f, 0.65f); // 半透明黑色遮罩
            mBg.raycastTarget = true;

            // 居中卡片
            Transform cardTr = _modalDialogObj.transform.Find("ModalCard");
            GameObject cardObj = cardTr != null ? cardTr.gameObject : new GameObject("ModalCard", typeof(RectTransform), typeof(Image));
            cardObj.transform.SetParent(_modalDialogObj.transform, false);

            RectTransform cRT = cardObj.GetComponent<RectTransform>();
            cRT.anchorMin = new Vector2(0.5f, 0.5f);
            cRT.anchorMax = new Vector2(0.5f, 0.5f);
            cRT.pivot = new Vector2(0.5f, 0.5f);
            cRT.anchoredPosition = Vector2.zero;
            cRT.sizeDelta = new Vector2(440f, 220f);

            Image cImg = cardObj.GetComponent<Image>();
            cImg.sprite = roundedPillSprite;
            cImg.type = Image.Type.Sliced;
            cImg.color = new Color(0.12f, 0.17f, 0.24f, 0.98f);

            // 弹窗标题
            Transform mTitTr = cardObj.transform.Find("Title");
            GameObject mTitObj = mTitTr != null ? mTitTr.gameObject : new GameObject("Title", typeof(RectTransform), typeof(Text));
            mTitObj.transform.SetParent(cardObj.transform, false);
            RectTransform mtRT = mTitObj.GetComponent<RectTransform>();
            mtRT.anchorMin = new Vector2(0f, 0.68f);
            mtRT.anchorMax = new Vector2(1f, 0.95f);
            mtRT.offsetMin = Vector2.zero;
            mtRT.offsetMax = Vector2.zero;
            _modalTitleText = mTitObj.GetComponent<Text>();
            _modalTitleText.font = font;
            _modalTitleText.fontSize = 22;
            _modalTitleText.fontStyle = FontStyle.Bold;
            _modalTitleText.alignment = TextAnchor.MiddleCenter;
            _modalTitleText.text = "提 示";
            _modalTitleText.color = Color.white;

            // 弹窗正文
            Transform mMsgTr = cardObj.transform.Find("Message");
            GameObject mMsgObj = mMsgTr != null ? mMsgTr.gameObject : new GameObject("Message", typeof(RectTransform), typeof(Text));
            mMsgObj.transform.SetParent(cardObj.transform, false);
            RectTransform mmRT = mMsgObj.GetComponent<RectTransform>();
            mmRT.anchorMin = new Vector2(0.06f, 0.38f);
            mmRT.anchorMax = new Vector2(0.94f, 0.68f);
            mmRT.offsetMin = Vector2.zero;
            mmRT.offsetMax = Vector2.zero;
            _modalMessageText = mMsgObj.GetComponent<Text>();
            _modalMessageText.font = font;
            _modalMessageText.fontSize = 17;
            _modalMessageText.alignment = TextAnchor.MiddleCenter;
            _modalMessageText.text = "检测到设置尚未保存，是否保存当前修改？";
            _modalMessageText.color = new Color(0.85f, 0.90f, 0.96f, 0.95f);

            // 底部 3 按钮横排
            float btnW = 120f;
            float btnH = 42f;

            // 按钮 1: 保存并退出
            _modalSaveAndExitBtn = EnsureModalButton(cardObj.transform, "BtnSaveExit", "保存退出", font, new Color(0.14f, 0.58f, 0.32f, 1f), new Vector2(-130f, 26f), new Vector2(btnW, btnH));
            _modalSaveAndExitBtn.onClick.RemoveListener(OnModalSaveAndExit);
            _modalSaveAndExitBtn.onClick.AddListener(OnModalSaveAndExit);

            // 按钮 2: 放弃修改
            _modalDiscardBtn = EnsureModalButton(cardObj.transform, "BtnDiscard", "放弃修改", font, new Color(0.68f, 0.22f, 0.22f, 1f), new Vector2(0f, 26f), new Vector2(btnW, btnH));
            _modalDiscardBtn.onClick.RemoveListener(OnModalDiscardAndExit);
            _modalDiscardBtn.onClick.AddListener(OnModalDiscardAndExit);

            // 按钮 3: 取消
            _modalCancelBtn = EnsureModalButton(cardObj.transform, "BtnCancel", "取 消", font, new Color(0.25f, 0.32f, 0.42f, 1f), new Vector2(130f, 26f), new Vector2(btnW, btnH));
            _modalCancelBtn.onClick.RemoveListener(OnModalCancel);
            _modalCancelBtn.onClick.AddListener(OnModalCancel);

            _modalDialogObj.SetActive(false);
        }

        private Button EnsureModalButton(Transform parent, string name, string text, Font font, Color bgColor, Vector2 pos, Vector2 size)
        {
            Transform tr = parent.Find(name);
            GameObject obj = tr != null ? tr.gameObject : new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);

            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            Image img = obj.GetComponent<Image>();
            img.sprite = roundedPillSprite;
            img.type = Image.Type.Sliced;
            img.color = bgColor;

            Button btn = obj.GetComponent<Button>();
            btn.targetGraphic = img;

            Transform txtTr = obj.transform.Find("Text");
            GameObject txtObj = txtTr != null ? txtTr.gameObject : new GameObject("Text", typeof(RectTransform), typeof(Text));
            txtObj.transform.SetParent(obj.transform, false);

            RectTransform tRT = txtObj.GetComponent<RectTransform>();
            tRT.anchorMin = Vector2.zero;
            tRT.anchorMax = Vector2.one;
            tRT.offsetMin = Vector2.zero;
            tRT.offsetMax = Vector2.zero;

            Text txt = txtObj.GetComponent<Text>();
            txt.font = font;
            txt.fontSize = 16;
            txt.fontStyle = FontStyle.Bold;
            txt.text = text;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;

            return btn;
        }
    }
}
