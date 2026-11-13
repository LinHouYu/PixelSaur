using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using PixelSaur.Grass;

namespace PixelSaur.UI
{
    public enum SettingsCategory
    {
        Graphics,
        Controls
    }

    /// <summary>
    /// 全屏次级设置页面（画面设置 / 游戏控制）
    /// 支持 iOS 拨动开关（亮白顶层防变暗）、下拉菜单、滑块+可点击键盘输入数字框、
    /// 保存/重置二次确认弹窗及未保存退出拦截提示。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SubSettingsPageHUD : MonoBehaviour
    {
        public static SubSettingsPageHUD Instance { get; private set; }

        public const string KeyDisableGrass = "SETTING_GRAPHICS_GRASS_WIND";
        public const string KeyGrassRes = "SETTING_GRAPHICS_GRASS_RES";
        public const string KeyRenderScale = "SETTING_GRAPHICS_RENDER_SCALE";
        public const string KeyVSync = "SETTING_GRAPHICS_VSYNC";
        public const string KeyFpsLimit = "SETTING_GRAPHICS_FPS_LIMIT";
        public const string KeyWaterRipple = "SETTING_GRAPHICS_WATER_RIPPLE";

        public const string KeyDynamicJoystick = "SETTING_CTRL_DYNAMIC_JOYSTICK";
        public const string KeyHaptics = "SETTING_CTRL_HAPTICS";
        public const string KeyAttackLock = "SETTING_CTRL_ATTACK_LOCK";

        private enum ItemWidgetType
        {
            Toggle,
            GrassResCombo,
            RenderScaleSlider,
            FpsLimitSlider
        }

        private class SettingItemDef
        {
            public string key;
            public string title;
            public string desc;
            public int defaultValue;
            public ItemWidgetType widgetType;
        }

        private class ToggleItemView
        {
            public string key;
            public bool currentVal;
            public Image trackImage;
            public RectTransform thumbRT;
            public Button button;
            public Coroutine anim;
        }

        [Header("样式与资源")]
        [SerializeField] private Sprite roundedPillSprite;
        [SerializeField] private Color fullScreenCoverColor = new Color(0.05f, 0.07f, 0.10f, 1.00f);
        [SerializeField] private Color cardBgColor = new Color(0.09f, 0.13f, 0.18f, 0.94f);
        [SerializeField] private Color closeBtnColor = new Color(0.32f, 0.15f, 0.17f, 0.95f);
        [SerializeField] private Color saveBtnNormalColor = new Color(0.16f, 0.24f, 0.34f, 0.96f);
        [SerializeField] private Color saveBtnDirtyColor = new Color(0.14f, 0.62f, 0.34f, 0.98f);
        [SerializeField] private Color resetBtnColor = new Color(0.42f, 0.22f, 0.20f, 0.96f);

        [Header("动画时长")]
        [SerializeField] private float slideDuration = 0.28f;

        private RectTransform _rootRect;
        private RectTransform _slideRoot;
        private Image _bgCover;
        private Text _titleText;
        private Button _closeButton;
        private Button _saveButton;
        private Image _saveButtonImage;
        private Text _saveButtonText;
        private Button _resetButton;
        private RectTransform _itemsContainer;
        private GameObject _toastObj;
        private Text _toastText;
        private CanvasGroup _toastGroup;

        // 未保存退出提示弹窗
        private GameObject _modalDialogObj;
        private Button _modalSaveAndExitBtn;
        private Button _modalDiscardBtn;
        private Button _modalCancelBtn;

        // 重置二次确认弹窗
        private GameObject _resetDialogObj;
        private Button _resetConfirmBtn;
        private Button _resetCancelBtn;

        private SettingsCategory _currentCategory = SettingsCategory.Graphics;
        private bool _isOpen;
        public bool IsOpen => _isOpen;

        private Coroutine _animCoroutine;
        private Coroutine _toastCoroutine;
        private bool _isUpdatingControls;

        private readonly Dictionary<string, int> _committedSettings = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _draftSettings = new Dictionary<string, int>();
        private readonly List<ToggleItemView> _activeToggleViews = new List<ToggleItemView>();

        private static Sprite _whitePillSprite;
        private static Sprite _whiteCircleSprite;
        private static float? _editorCachedRenderScale;

        private static readonly int[] GrassPresets = { 400, 800, 1400, 2800, 5000 };
        private static readonly string[] GrassPresetLabels = { "极低 (400)", "低 (800)", "默认 (1400)", "高 (2800)", "极高 (5000)", "自定义" };

        private static readonly SettingItemDef[] GraphicsDefs =
        {
            new SettingItemDef
            {
                key = KeyDisableGrass,
                title = "关闭草地",
                desc = "开启后将关闭场景草地渲染以提升性能（默认关闭此项即显示草地）",
                defaultValue = 0,
                widgetType = ItemWidgetType.Toggle
            },
            new SettingItemDef
            {
                key = KeyGrassRes,
                title = "草地分辨率 (密度)",
                desc = "下拉选择预设档位或拖动滑块，点击右侧数字可唤出键盘精准输入",
                defaultValue = 1400,
                widgetType = ItemWidgetType.GrassResCombo
            },
            new SettingItemDef
            {
                key = KeyRenderScale,
                title = "渲染分辨率 (%)",
                desc = "调整 3D 画面渲染缩放比例（默认 100%，点击数字可唤出键盘输入）",
                defaultValue = 100,
                widgetType = ItemWidgetType.RenderScaleSlider
            },
            new SettingItemDef
            {
                key = KeyVSync,
                title = "垂直同步 (VSync)",
                desc = "默认开启，使帧输出与屏幕刷新率同步以消除画面撕裂",
                defaultValue = 1,
                widgetType = ItemWidgetType.Toggle
            },
            new SettingItemDef
            {
                key = KeyFpsLimit,
                title = "帧率上限 (FPS)",
                desc = "滑动设置 30~120 FPS，拉至最右变为无限制，点击数字可精准输入",
                defaultValue = 120,
                widgetType = ItemWidgetType.FpsLimitSlider
            },
            new SettingItemDef
            {
                key = KeyWaterRipple,
                title = "水面物理涟漪 (Water Ripples)",
                desc = "允许角色划水时产生扩散水波渲染",
                defaultValue = 1,
                widgetType = ItemWidgetType.Toggle
            }
        };

        private static readonly SettingItemDef[] ControlsDefs =
        {
            new SettingItemDef
            {
                key = KeyDynamicJoystick,
                title = "动态虚拟摇杆 (元气骑士模式)",
                desc = "开启后在屏幕左侧任意位置按下，摇杆自动移动至手指处进行移动",
                defaultValue = 0,
                widgetType = ItemWidgetType.Toggle
            },
            new SettingItemDef
            {
                key = KeyHaptics,
                title = "操作触觉震动反馈",
                desc = "攻击、受击及交互时触发设备微震动",
                defaultValue = 1,
                widgetType = ItemWidgetType.Toggle
            },
            new SettingItemDef
            {
                key = KeyAttackLock,
                title = "按键连续锁定",
                desc = "防止误触退出锁定长按连续攻击状态",
                defaultValue = 1,
                widgetType = ItemWidgetType.Toggle
            }
        };

        private void Awake()
        {
            if (Instance == null) Instance = this;
            LoadDefaultSprites();
            EnsureUIHierarchy();
        }

        private void OnApplicationQuit()
        {
#if UNITY_EDITOR
            if (_editorCachedRenderScale.HasValue)
            {
                var urp = GetActiveURPAsset();
                if (urp != null) urp.renderScale = _editorCachedRenderScale.Value;
                _editorCachedRenderScale = null;
            }
#endif
        }

        private void LoadDefaultSprites()
        {
#if UNITY_EDITOR
            if (roundedPillSprite == null)
            {
                roundedPillSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/hud_pill_bg_64x64.png");
            }
#endif
            EnsureWhiteProceduralSprites();
        }

        private static void EnsureWhiteProceduralSprites()
        {
            if (_whitePillSprite == null) _whitePillSprite = CreateWhitePillSprite(64, 18);
            if (_whiteCircleSprite == null) _whiteCircleSprite = CreateWhiteCircleSprite(64);
        }

        private static Sprite CreateWhitePillSprite(int size, int borderRadius)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "WhitePillSprite_Procedural",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color[] pixels = new Color[size * size];
            float r = borderRadius;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = 0f;
                    if (x < r) dx = r - x;
                    else if (x > size - 1 - r) dx = x - (size - 1 - r);

                    float dy = 0f;
                    if (y < r) dy = r - y;
                    else if (y > size - 1 - r) dy = y - (size - 1 - r);

                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(r + 0.5f - dist);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            Sprite sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
            sp.hideFlags = HideFlags.HideAndDontSave;
            return sp;
        }

        private static Sprite CreateWhiteCircleSprite(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "WhiteCircleSprite_Procedural",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color[] pixels = new Color[size * size];
            float c = (size - 1) * 0.5f;
            float maxR = size * 0.46f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
                    float alpha = Mathf.Clamp01((maxR - dist) * 0.8f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            Sprite sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            sp.hideFlags = HideFlags.HideAndDontSave;
            return sp;
        }

        public static void OpenPage(SettingsCategory category)
        {
            if (Instance == null)
            {
                var found = Resources.FindObjectsOfTypeAll<SubSettingsPageHUD>();
                if (found.Length > 0) Instance = found[0];
            }
            if (Instance == null)
            {
                Canvas canvas = FindAnyObjectByType<Canvas>();
                if (canvas != null)
                {
                    Transform t = canvas.transform.Find("SubSettingsPage");
                    GameObject go = t != null ? t.gameObject : new GameObject("SubSettingsPage");
                    go.transform.SetParent(canvas.transform, false);
                    Instance = go.GetComponent<SubSettingsPageHUD>() ?? go.AddComponent<SubSettingsPageHUD>();
                }
            }

            if (Instance != null) Instance.Open(category);
        }

        /// <summary>
        /// 游戏启动时从 PlayerPrefs 读取并应用所有已保存或默认配置
        /// </summary>
        public static void ApplyAllSavedSettingsAtStartup()
        {
            int disableGrass = PlayerPrefs.GetInt(KeyDisableGrass, 0);
            int grassRes = PlayerPrefs.GetInt(KeyGrassRes, 1400);
            int renderScale = PlayerPrefs.GetInt(KeyRenderScale, 100);
            int vsync = PlayerPrefs.GetInt(KeyVSync, 1);
            int fpsLimit = PlayerPrefs.GetInt(KeyFpsLimit, 120);
            int waterRipple = PlayerPrefs.GetInt(KeyWaterRipple, 1);
            int dynJoy = PlayerPrefs.GetInt(KeyDynamicJoystick, 0);

            ApplyGraphicsValues(disableGrass, grassRes, renderScale, vsync, fpsLimit, waterRipple);
            ApplyControlsValues(dynJoy);
        }

        private static UniversalRenderPipelineAsset GetActiveURPAsset()
        {
            return (QualitySettings.renderPipeline ?? GraphicsSettings.currentRenderPipeline) as UniversalRenderPipelineAsset;
        }

        private static void ApplyGraphicsValues(int disableGrass, int grassRes, int renderScalePercent, int vsync, int fpsLimit, int waterRipple)
        {
            QualitySettings.vSyncCount = vsync == 1 ? 1 : 0;
            Application.targetFrameRate = (fpsLimit <= 0 || fpsLimit > 120) ? -1 : Mathf.Clamp(fpsLimit, 30, 120);

            if (Application.isPlaying)
            {
                var urp = GetActiveURPAsset();
                if (urp != null)
                {
#if UNITY_EDITOR
                    if (!_editorCachedRenderScale.HasValue) _editorCachedRenderScale = urp.renderScale;
#endif
                    urp.renderScale = Mathf.Clamp(renderScalePercent, 25, 200) / 100f;
                }

                var grass = FindAnyObjectByType<ProceduralGrassRenderer>(FindObjectsInactive.Include);
                if (grass != null)
                {
                    grass.enabled = disableGrass == 0;
                    if (grass.enabled) grass.GrassCount = Mathf.Clamp(grassRes, 50, 10000);
                }

                var ripple = FindAnyObjectByType<Water.WaterRippleManager>(FindObjectsInactive.Include);
                if (ripple != null) ripple.enabled = waterRipple == 1;
            }
        }

        private static void ApplyControlsValues(int dynamicJoystick)
        {
            if (Application.isPlaying)
            {
                var joystick = FindAnyObjectByType<VirtualJoystick>(FindObjectsInactive.Include);
                if (joystick != null)
                {
                    joystick.DynamicJoystick = dynamicJoystick == 1;
                }
            }
        }

        public void Open(SettingsCategory category)
        {
            _currentCategory = category;
            LoadDefaultSprites();
            EnsureUIHierarchy();
            transform.SetAsLastSibling();

            LoadSettingsData();
            RebuildItemsList();
            UpdateSaveButtonState();

            if (_modalDialogObj != null) _modalDialogObj.SetActive(false);
            if (_resetDialogObj != null) _resetDialogObj.SetActive(false);
            if (_toastObj != null) _toastObj.SetActive(false);

            if (_animCoroutine != null) StopCoroutine(_animCoroutine);
            gameObject.SetActive(true);
            _isOpen = true;

            if (Application.isPlaying)
            {
                _animCoroutine = StartCoroutine(AnimateSlide(true));
            }
            else if (_slideRoot != null)
            {
                _slideRoot.anchoredPosition = Vector2.zero;
            }
        }

        private void OnDisable()
        {
            CleanupStrayDropdownArtifacts();
        }

        private void CleanupStrayDropdownArtifacts()
        {
            foreach (var dd in GetComponentsInChildren<Dropdown>(true))
            {
                dd.Hide();
                Transform listTr = dd.transform.Find("Dropdown List");
                if (listTr != null && !Application.isPlaying) DestroyImmediate(listTr.gameObject);
            }
            if (transform.parent != null)
            {
                for (int i = transform.parent.childCount - 1; i >= 0; i--)
                {
                    Transform child = transform.parent.GetChild(i);
                    if (child.name == "Blocker")
                    {
                        if (Application.isPlaying) Destroy(child.gameObject);
                        else DestroyImmediate(child.gameObject);
                    }
                }
            }
        }

        public void CloseDirectly()
        {
            CleanupStrayDropdownArtifacts();
            if (_modalDialogObj != null) _modalDialogObj.SetActive(false);
            if (_resetDialogObj != null) _resetDialogObj.SetActive(false);
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
                float ease = 1f - Mathf.Pow(1f - t, 3);
                if (_slideRoot != null) _slideRoot.anchoredPosition = new Vector2(Mathf.Lerp(startX, targetX, ease), 0);
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

        private SettingItemDef[] GetCurrentDefs()
        {
            return _currentCategory == SettingsCategory.Graphics ? GraphicsDefs : ControlsDefs;
        }

        private void LoadSettingsData()
        {
            _committedSettings.Clear();
            _draftSettings.Clear();

            foreach (var def in GetCurrentDefs())
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

        private void SetDraftValue(string key, int value)
        {
            _draftSettings[key] = value;
            UpdateSaveButtonState();
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
                _saveButtonText.color = dirty ? Color.white : new Color(0.86f, 0.90f, 0.96f, 1f);
            }
        }

        private void HandleSaveClick()
        {
            foreach (var kvp in _draftSettings)
            {
                _committedSettings[kvp.Key] = kvp.Value;
                PlayerPrefs.SetInt(kvp.Key, kvp.Value);
            }
            PlayerPrefs.Save();

            ApplyActiveSettings();
            UpdateSaveButtonState();
            ShowToast("✓ 设置已成功保存生效！");
        }

        private void HandleResetClick()
        {
            if (_resetDialogObj != null)
            {
                _resetDialogObj.SetActive(true);
                _resetDialogObj.transform.SetAsLastSibling();
            }
        }

        private void OnResetConfirm()
        {
            foreach (var def in GetCurrentDefs())
            {
                _draftSettings[def.key] = def.defaultValue;
                _committedSettings[def.key] = def.defaultValue;
                PlayerPrefs.SetInt(def.key, def.defaultValue);
            }
            PlayerPrefs.Save();

            ApplyActiveSettings();
            RebuildItemsList();
            UpdateSaveButtonState();

            if (_resetDialogObj != null) _resetDialogObj.SetActive(false);
            ShowToast("✓ 已恢复默认设置！");
        }

        private void OnResetCancel()
        {
            if (_resetDialogObj != null) _resetDialogObj.SetActive(false);
        }

        private void ApplyActiveSettings()
        {
            if (_currentCategory == SettingsCategory.Graphics)
            {
                ApplyGraphicsValues(
                    _committedSettings.TryGetValue(KeyDisableGrass, out int dg) ? dg : 0,
                    _committedSettings.TryGetValue(KeyGrassRes, out int gr) ? gr : 1400,
                    _committedSettings.TryGetValue(KeyRenderScale, out int rs) ? rs : 100,
                    _committedSettings.TryGetValue(KeyVSync, out int vs) ? vs : 1,
                    _committedSettings.TryGetValue(KeyFpsLimit, out int fps) ? fps : 120,
                    _committedSettings.TryGetValue(KeyWaterRipple, out int wr) ? wr : 1
                );
            }
            else
            {
                ApplyControlsValues(_committedSettings.TryGetValue(KeyDynamicJoystick, out int dj) ? dj : 0);
            }
        }

        private void HandleCloseClick()
        {
            if (IsDirty())
            {
                if (_modalDialogObj != null)
                {
                    _modalDialogObj.SetActive(true);
                    _modalDialogObj.transform.SetAsLastSibling();
                }
            }
            else
            {
                CloseDirectly();
            }
        }

        private void OnModalSaveAndExit()
        {
            HandleSaveClick();
            if (_modalDialogObj != null) _modalDialogObj.SetActive(false);
            CloseDirectly();
        }

        private void OnModalDiscardAndExit()
        {
            foreach (var kvp in _committedSettings) _draftSettings[kvp.Key] = kvp.Value;
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
            if (Application.isPlaying)
            {
                _toastCoroutine = StartCoroutine(ToastRoutine(message));
            }
            else
            {
                _toastText.text = message;
                _toastObj.SetActive(true);
            }
        }

        private IEnumerator ToastRoutine(string message)
        {
            _toastObj.SetActive(true);
            _toastObj.transform.SetAsLastSibling();
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

        private void OnToggleClicked(ToggleItemView view)
        {
            bool newVal = !view.currentVal;
            view.currentVal = newVal;
            SetDraftValue(view.key, newVal ? 1 : 0);

            if (view.anim != null) StopCoroutine(view.anim);
            if (Application.isPlaying)
            {
                view.anim = StartCoroutine(AnimateSwitch(view, newVal));
            }
            else
            {
                view.thumbRT.anchoredPosition = new Vector2(newVal ? 15f : -15f, 0f);
                view.trackImage.color = newVal ? new Color(0.20f, 0.78f, 0.35f, 1f) : new Color(0.32f, 0.37f, 0.45f, 1f);
            }
        }

        private IEnumerator AnimateSwitch(ToggleItemView view, bool on)
        {
            float startX = view.thumbRT.anchoredPosition.x;
            float targetX = on ? 15f : -15f;
            Color startColor = view.trackImage.color;
            Color targetColor = on ? new Color(0.20f, 0.78f, 0.35f, 1f) : new Color(0.32f, 0.37f, 0.45f, 1f);

            float elapsed = 0f;
            while (elapsed < 0.16f)
            {
                elapsed += Time.unscaledDeltaTime;
                float ease = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 0.16f));
                view.thumbRT.anchoredPosition = new Vector2(Mathf.Lerp(startX, targetX, ease), 0f);
                view.trackImage.color = Color.Lerp(startColor, targetColor, ease);
                yield return null;
            }

            view.thumbRT.anchoredPosition = new Vector2(targetX, 0f);
            view.trackImage.color = targetColor;
            view.anim = null;
        }

        private void RebuildItemsList()
        {
            _activeToggleViews.Clear();
            EnsureWhiteProceduralSprites();

            for (int i = _itemsContainer.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(_itemsContainer.GetChild(i).gameObject);
            }

            Font mainFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _titleText.text = _currentCategory == SettingsCategory.Graphics ? "画 面 设 置" : "游 戏 控 制";

            SettingItemDef[] defs = GetCurrentDefs();
            float currentY = 0f;
            const float spacing = 12f;

            for (int idx = 0; idx < defs.Length; idx++)
            {
                var def = defs[idx];
                int curVal = _draftSettings[def.key];
                float cardHeight = def.widgetType == ItemWidgetType.GrassResCombo ? 112f :
                                   def.widgetType == ItemWidgetType.Toggle ? 74f : 84f;

                GameObject card = new GameObject("Item_" + def.key, typeof(RectTransform), typeof(Image));
                card.transform.SetParent(_itemsContainer, false);
                RectTransform cardRT = card.GetComponent<RectTransform>();
                cardRT.anchorMin = new Vector2(0f, 1f);
                cardRT.anchorMax = new Vector2(1f, 1f);
                cardRT.pivot = new Vector2(0.5f, 1f);
                cardRT.sizeDelta = new Vector2(0f, cardHeight);
                cardRT.anchoredPosition = new Vector2(0f, -currentY);

                Image cardImg = card.GetComponent<Image>();
                cardImg.sprite = roundedPillSprite;
                cardImg.type = Image.Type.Sliced;
                cardImg.color = cardBgColor;

                float leftWidthRatio = def.widgetType == ItemWidgetType.Toggle ? 0.78f : 0.44f;
                BuildCardTexts(card.transform, mainFont, def.title, def.desc, leftWidthRatio);

                switch (def.widgetType)
                {
                    case ItemWidgetType.Toggle:
                        BuildToggleWidget(card.transform, def.key, curVal == 1);
                        break;
                    case ItemWidgetType.GrassResCombo:
                        BuildGrassResComboWidget(card.transform, mainFont, def.key, curVal);
                        break;
                    case ItemWidgetType.RenderScaleSlider:
                        BuildRenderScaleWidget(card.transform, mainFont, def.key, curVal);
                        break;
                    case ItemWidgetType.FpsLimitSlider:
                        BuildFpsLimitWidget(card.transform, mainFont, def.key, curVal);
                        break;
                }

                currentY += cardHeight + spacing;
            }

            _itemsContainer.sizeDelta = new Vector2(0f, Mathf.Max(currentY, 100f));
        }

        private void BuildCardTexts(Transform cardTr, Font font, string title, string desc, float maxAnchorX)
        {
            GameObject textObj = new GameObject("TextInfo", typeof(RectTransform));
            textObj.transform.SetParent(cardTr, false);
            RectTransform textRT = textObj.GetComponent<RectTransform>();
            textRT.anchorMin = new Vector2(0f, 0f);
            textRT.anchorMax = new Vector2(maxAnchorX, 1f);
            textRT.offsetMin = new Vector2(22f, 8f);
            textRT.offsetMax = new Vector2(-8f, -8f);

            GameObject titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleGo.transform.SetParent(textObj.transform, false);
            RectTransform titRT = titleGo.GetComponent<RectTransform>();
            titRT.anchorMin = new Vector2(0f, 0.44f);
            titRT.anchorMax = new Vector2(1f, 1f);
            titRT.offsetMin = Vector2.zero;
            titRT.offsetMax = Vector2.zero;
            Text titText = titleGo.GetComponent<Text>();
            titText.font = font;
            titText.fontSize = 20;
            titText.fontStyle = FontStyle.Bold;
            titText.text = title;
            titText.color = Color.white;
            titText.alignment = TextAnchor.MiddleLeft;

            GameObject descGo = new GameObject("Desc", typeof(RectTransform), typeof(Text));
            descGo.transform.SetParent(textObj.transform, false);
            RectTransform descRT = descGo.GetComponent<RectTransform>();
            descRT.anchorMin = new Vector2(0f, 0f);
            descRT.anchorMax = new Vector2(1f, 0.44f);
            descRT.offsetMin = Vector2.zero;
            descRT.offsetMax = Vector2.zero;
            Text descText = descGo.GetComponent<Text>();
            descText.font = font;
            descText.fontSize = 13;
            descText.text = desc;
            descText.color = new Color(0.68f, 0.77f, 0.88f, 0.90f);
            descText.alignment = TextAnchor.MiddleLeft;
        }

        private void BuildToggleWidget(Transform cardTr, string key, bool currentVal)
        {
            GameObject switchGo = new GameObject("iOSToggleSwitch", typeof(RectTransform), typeof(Image), typeof(Button));
            switchGo.transform.SetParent(cardTr, false);
            switchGo.transform.SetAsLastSibling();

            RectTransform swRT = switchGo.GetComponent<RectTransform>();
            swRT.anchorMin = new Vector2(1f, 0.5f);
            swRT.anchorMax = new Vector2(1f, 0.5f);
            swRT.pivot = new Vector2(1f, 0.5f);
            swRT.anchoredPosition = new Vector2(-22f, 0f);
            swRT.sizeDelta = new Vector2(64f, 34f);

            // 使用纯白程序化圆角贴图，保证颜色明快不发暗
            Image trackImg = switchGo.GetComponent<Image>();
            trackImg.sprite = _whitePillSprite;
            trackImg.type = Image.Type.Sliced;
            trackImg.color = currentVal ? new Color(0.20f, 0.78f, 0.35f, 1f) : new Color(0.32f, 0.37f, 0.45f, 1f);

            GameObject thumbGo = new GameObject("Thumb", typeof(RectTransform), typeof(Image));
            thumbGo.transform.SetParent(switchGo.transform, false);
            thumbGo.transform.SetAsLastSibling();

            RectTransform thRT = thumbGo.GetComponent<RectTransform>();
            thRT.anchorMin = new Vector2(0.5f, 0.5f);
            thRT.anchorMax = new Vector2(0.5f, 0.5f);
            thRT.pivot = new Vector2(0.5f, 0.5f);
            thRT.sizeDelta = new Vector2(26f, 26f);
            thRT.anchoredPosition = new Vector2(currentVal ? 15f : -15f, 0f);

            Image thumbImg = thumbGo.GetComponent<Image>();
            thumbImg.sprite = _whiteCircleSprite;
            thumbImg.type = Image.Type.Simple;
            thumbImg.color = Color.white;
            thumbImg.raycastTarget = false;

            Button swBtn = switchGo.GetComponent<Button>();
            swBtn.transition = Selectable.Transition.None;

            ToggleItemView view = new ToggleItemView
            {
                key = key,
                currentVal = currentVal,
                trackImage = trackImg,
                thumbRT = thRT,
                button = swBtn
            };
            swBtn.onClick.AddListener(() => OnToggleClicked(view));
            _activeToggleViews.Add(view);
        }

        private void BuildGrassResComboWidget(Transform cardTr, Font font, string key, int currentVal)
        {
            GameObject area = CreateRightControlArea(cardTr, 0.45f, 0.98f);

            // 上半区：下拉菜单 + 可点击数字框
            Dropdown dropdown = BuildStyledDropdown(area.transform, font, GrassPresetLabels, new Vector2(0f, 0.54f), new Vector2(0.62f, 0.94f));
            InputField input = BuildNumericInputField(area.transform, font, new Vector2(0.66f, 0.54f), new Vector2(1f, 0.94f), "", "数量");

            // 下半区：Slider
            Slider slider = BuildStyledSlider(area.transform, new Vector2(0f, 0.08f), new Vector2(1f, 0.46f), 100, 6000, true);

            void SyncUI(int val)
            {
                _isUpdatingControls = true;
                slider.SetValueWithoutNotify(Mathf.Clamp(val, 100, 6000));
                input.SetTextWithoutNotify(val.ToString());

                int presetIdx = Array.IndexOf(GrassPresets, val);
                dropdown.SetValueWithoutNotify(presetIdx >= 0 ? presetIdx : GrassPresets.Length);
                dropdown.RefreshShownValue();
                _isUpdatingControls = false;
            }

            SyncUI(currentVal);

            dropdown.onValueChanged.AddListener(idx =>
            {
                if (_isUpdatingControls) return;
                if (idx >= 0 && idx < GrassPresets.Length)
                {
                    int val = GrassPresets[idx];
                    SetDraftValue(key, val);
                    SyncUI(val);
                }
            });

            slider.onValueChanged.AddListener(v =>
            {
                if (_isUpdatingControls) return;
                int val = Mathf.RoundToInt(v);
                SetDraftValue(key, val);
                SyncUI(val);
            });

            input.onEndEdit.AddListener(raw =>
            {
                if (_isUpdatingControls) return;
                int val = int.TryParse(raw, out int parsed) ? Mathf.Clamp(parsed, 50, 10000) : _draftSettings[key];
                SetDraftValue(key, val);
                SyncUI(val);
            });
        }

        private void BuildRenderScaleWidget(Transform cardTr, Font font, string key, int currentVal)
        {
            GameObject area = CreateRightControlArea(cardTr, 0.45f, 0.98f);
            Slider slider = BuildStyledSlider(area.transform, new Vector2(0f, 0.20f), new Vector2(0.66f, 0.80f), 50, 150, true);
            InputField input = BuildNumericInputField(area.transform, font, new Vector2(0.70f, 0.22f), new Vector2(1f, 0.78f), "%", "100");

            void SyncUI(int val)
            {
                _isUpdatingControls = true;
                slider.SetValueWithoutNotify(Mathf.Clamp(val, 50, 150));
                input.SetTextWithoutNotify(val.ToString());
                _isUpdatingControls = false;
            }

            SyncUI(currentVal);

            slider.onValueChanged.AddListener(v =>
            {
                if (_isUpdatingControls) return;
                int val = Mathf.RoundToInt(v);
                SetDraftValue(key, val);
                SyncUI(val);
            });

            input.onEndEdit.AddListener(raw =>
            {
                if (_isUpdatingControls) return;
                int val = int.TryParse(raw, out int parsed) ? Mathf.Clamp(parsed, 25, 200) : _draftSettings[key];
                SetDraftValue(key, val);
                SyncUI(val);
            });
        }

        private void BuildFpsLimitWidget(Transform cardTr, Font font, string key, int currentVal)
        {
            GameObject area = CreateRightControlArea(cardTr, 0.45f, 0.98f);
            // 30~120 为对应帧率，超过 120 (121~130) 自动吸附到 130 代表无限制 (内部存 0)
            Slider slider = BuildStyledSlider(area.transform, new Vector2(0f, 0.20f), new Vector2(0.66f, 0.80f), 30, 130, true);
            InputField input = BuildNumericInputField(area.transform, font, new Vector2(0.70f, 0.22f), new Vector2(1f, 0.78f), "", "无限制");
            Text placeholder = input.placeholder as Text;

            void SyncUI(int val)
            {
                _isUpdatingControls = true;
                bool isUnlimited = val <= 0 || val > 120;
                slider.SetValueWithoutNotify(isUnlimited ? 130 : Mathf.Clamp(val, 30, 120));
                input.SetTextWithoutNotify(isUnlimited ? string.Empty : val.ToString());
                if (placeholder != null)
                {
                    placeholder.text = isUnlimited ? "无限制" : "输入FPS";
                    placeholder.color = isUnlimited ? new Color(0.24f, 0.85f, 0.48f, 1f) : new Color(0.55f, 0.65f, 0.75f, 0.6f);
                }
                _isUpdatingControls = false;
            }

            SyncUI(currentVal);

            slider.onValueChanged.AddListener(v =>
            {
                if (_isUpdatingControls) return;
                int raw = Mathf.RoundToInt(v);
                int stored = raw > 120 ? 0 : Mathf.Clamp(raw, 30, 120);
                SetDraftValue(key, stored);
                SyncUI(stored);
            });

            input.onEndEdit.AddListener(raw =>
            {
                if (_isUpdatingControls) return;
                int stored;
                if (string.IsNullOrWhiteSpace(raw))
                {
                    stored = 0;
                }
                else if (int.TryParse(raw, out int parsed))
                {
                    stored = (parsed <= 0 || parsed > 120) ? 0 : Mathf.Clamp(parsed, 30, 120);
                }
                else
                {
                    stored = _draftSettings[key];
                }
                SetDraftValue(key, stored);
                SyncUI(stored);
            });
        }

        private GameObject CreateRightControlArea(Transform cardTr, float minX, float maxX)
        {
            GameObject area = new GameObject("ControlArea", typeof(RectTransform));
            area.transform.SetParent(cardTr, false);
            area.transform.SetAsLastSibling();
            RectTransform rt = area.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(minX, 0f);
            rt.anchorMax = new Vector2(maxX, 1f);
            rt.offsetMin = new Vector2(8f, 8f);
            rt.offsetMax = new Vector2(-18f, -8f);
            return area;
        }

        private Slider BuildStyledSlider(Transform parent, Vector2 anchorMin, Vector2 anchorMax, float minVal, float maxVal, bool wholeNumbers)
        {
            GameObject sliderGo = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            sliderGo.transform.SetParent(parent, false);
            RectTransform srt = sliderGo.GetComponent<RectTransform>();
            srt.anchorMin = anchorMin;
            srt.anchorMax = anchorMax;
            srt.offsetMin = Vector2.zero;
            srt.offsetMax = Vector2.zero;

            // Background Track
            GameObject bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(sliderGo.transform, false);
            RectTransform bgRT = bgGo.GetComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0f, 0.5f);
            bgRT.anchorMax = new Vector2(1f, 0.5f);
            bgRT.sizeDelta = new Vector2(0f, 10f);
            Image bgImg = bgGo.GetComponent<Image>();
            bgImg.sprite = _whitePillSprite;
            bgImg.type = Image.Type.Sliced;
            bgImg.pixelsPerUnitMultiplier = 3f;
            bgImg.color = new Color(0.24f, 0.30f, 0.38f, 1f);

            // Fill Area & Fill
            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderGo.transform, false);
            RectTransform faRT = fillArea.GetComponent<RectTransform>();
            faRT.anchorMin = new Vector2(0f, 0.5f);
            faRT.anchorMax = new Vector2(1f, 0.5f);
            faRT.sizeDelta = new Vector2(-12f, 10f);

            GameObject fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(fillArea.transform, false);
            RectTransform fillRT = fillGo.GetComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;
            Image fillImg = fillGo.GetComponent<Image>();
            fillImg.sprite = _whitePillSprite;
            fillImg.type = Image.Type.Sliced;
            fillImg.pixelsPerUnitMultiplier = 3f;
            fillImg.color = new Color(0.20f, 0.78f, 0.35f, 1f);

            // Handle Slide Area & Handle (垂直居中定高，确保滑块为正圆)
            GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(sliderGo.transform, false);
            RectTransform haRT = handleArea.GetComponent<RectTransform>();
            haRT.anchorMin = new Vector2(0f, 0.5f);
            haRT.anchorMax = new Vector2(1f, 0.5f);
            haRT.sizeDelta = new Vector2(-24f, 24f);
            haRT.anchoredPosition = Vector2.zero;

            GameObject handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleGo.transform.SetParent(handleArea.transform, false);
            RectTransform hRT = handleGo.GetComponent<RectTransform>();
            hRT.sizeDelta = new Vector2(24f, 24f);
            Image handleImg = handleGo.GetComponent<Image>();
            handleImg.sprite = _whiteCircleSprite;
            handleImg.preserveAspect = true;
            handleImg.color = Color.white;

            Slider slider = sliderGo.GetComponent<Slider>();
            slider.targetGraphic = handleImg;
            slider.fillRect = fillRT;
            slider.handleRect = hRT;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = minVal;
            slider.maxValue = maxVal;
            slider.wholeNumbers = wholeNumbers;
            slider.transition = Selectable.Transition.None;
            return slider;
        }

        private InputField BuildNumericInputField(Transform parent, Font font, Vector2 anchorMin, Vector2 anchorMax, string suffix, string placeholderText)
        {
            GameObject boxGo = new GameObject("NumericInput", typeof(RectTransform), typeof(Image), typeof(InputField));
            boxGo.transform.SetParent(parent, false);
            RectTransform rt = boxGo.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Image boxImg = boxGo.GetComponent<Image>();
            boxImg.sprite = _whitePillSprite;
            boxImg.type = Image.Type.Sliced;
            boxImg.color = new Color(0.16f, 0.22f, 0.30f, 1f);

            GameObject phGo = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
            phGo.transform.SetParent(boxGo.transform, false);
            RectTransform phRT = phGo.GetComponent<RectTransform>();
            phRT.anchorMin = Vector2.zero;
            phRT.anchorMax = Vector2.one;
            phRT.offsetMin = new Vector2(6f, 2f);
            phRT.offsetMax = new Vector2(string.IsNullOrEmpty(suffix) ? -6f : -22f, -2f);
            Text phTxt = phGo.GetComponent<Text>();
            phTxt.font = font;
            phTxt.fontSize = 15;
            phTxt.fontStyle = FontStyle.Bold;
            phTxt.alignment = TextAnchor.MiddleCenter;
            phTxt.text = placeholderText;
            phTxt.color = new Color(0.55f, 0.65f, 0.75f, 0.65f);

            GameObject txtGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            txtGo.transform.SetParent(boxGo.transform, false);
            RectTransform tRT = txtGo.GetComponent<RectTransform>();
            tRT.anchorMin = Vector2.zero;
            tRT.anchorMax = Vector2.one;
            tRT.offsetMin = new Vector2(6f, 2f);
            tRT.offsetMax = new Vector2(string.IsNullOrEmpty(suffix) ? -6f : -22f, -2f);
            Text txt = txtGo.GetComponent<Text>();
            txt.font = font;
            txt.fontSize = 16;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.supportRichText = false;

            if (!string.IsNullOrEmpty(suffix))
            {
                GameObject sufGo = new GameObject("Suffix", typeof(RectTransform), typeof(Text));
                sufGo.transform.SetParent(boxGo.transform, false);
                RectTransform sRT = sufGo.GetComponent<RectTransform>();
                sRT.anchorMin = new Vector2(1f, 0f);
                sRT.anchorMax = new Vector2(1f, 1f);
                sRT.pivot = new Vector2(1f, 0.5f);
                sRT.sizeDelta = new Vector2(20f, 0f);
                sRT.anchoredPosition = new Vector2(-4f, 0f);
                Text sufTxt = sufGo.GetComponent<Text>();
                sufTxt.font = font;
                sufTxt.fontSize = 14;
                sufTxt.alignment = TextAnchor.MiddleCenter;
                sufTxt.color = new Color(0.70f, 0.80f, 0.90f, 0.9f);
                sufTxt.text = suffix;
                sufTxt.raycastTarget = false;
            }

            InputField field = boxGo.GetComponent<InputField>();
            field.targetGraphic = boxImg;
            field.textComponent = txt;
            field.placeholder = phTxt;
            field.contentType = InputField.ContentType.IntegerNumber;
            field.keyboardType = TouchScreenKeyboardType.NumberPad;
            field.characterLimit = 5;
            return field;
        }

        private Dropdown BuildStyledDropdown(Transform parent, Font font, string[] optionLabels, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject ddGo = new GameObject("PresetDropdown", typeof(RectTransform), typeof(Image), typeof(Dropdown));
            ddGo.transform.SetParent(parent, false);
            RectTransform ddRT = ddGo.GetComponent<RectTransform>();
            ddRT.anchorMin = anchorMin;
            ddRT.anchorMax = anchorMax;
            ddRT.offsetMin = Vector2.zero;
            ddRT.offsetMax = Vector2.zero;

            Image ddImg = ddGo.GetComponent<Image>();
            ddImg.sprite = _whitePillSprite;
            ddImg.type = Image.Type.Sliced;
            ddImg.color = new Color(0.16f, 0.22f, 0.30f, 1f);

            GameObject capGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            capGo.transform.SetParent(ddGo.transform, false);
            RectTransform capRT = capGo.GetComponent<RectTransform>();
            capRT.anchorMin = Vector2.zero;
            capRT.anchorMax = Vector2.one;
            capRT.offsetMin = new Vector2(12f, 2f);
            capRT.offsetMax = new Vector2(-26f, -2f);
            Text capTxt = capGo.GetComponent<Text>();
            capTxt.font = font;
            capTxt.fontSize = 15;
            capTxt.fontStyle = FontStyle.Bold;
            capTxt.alignment = TextAnchor.MiddleLeft;
            capTxt.color = Color.white;

            GameObject arrowGo = new GameObject("Arrow", typeof(RectTransform), typeof(Text));
            arrowGo.transform.SetParent(ddGo.transform, false);
            RectTransform arrRT = arrowGo.GetComponent<RectTransform>();
            arrRT.anchorMin = new Vector2(1f, 0f);
            arrRT.anchorMax = new Vector2(1f, 1f);
            arrRT.pivot = new Vector2(1f, 0.5f);
            arrRT.sizeDelta = new Vector2(24f, 0f);
            arrRT.anchoredPosition = new Vector2(-4f, 0f);
            Text arrTxt = arrowGo.GetComponent<Text>();
            arrTxt.font = font;
            arrTxt.fontSize = 12;
            arrTxt.alignment = TextAnchor.MiddleCenter;
            arrTxt.color = new Color(0.75f, 0.85f, 0.95f, 0.9f);
            arrTxt.text = "▼";
            arrTxt.raycastTarget = false;

            // Template
            GameObject tplGo = new GameObject("Template", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            tplGo.transform.SetParent(ddGo.transform, false);
            RectTransform tplRT = tplGo.GetComponent<RectTransform>();
            tplRT.anchorMin = new Vector2(0f, 0f);
            tplRT.anchorMax = new Vector2(1f, 0f);
            tplRT.pivot = new Vector2(0.5f, 1f);
            tplRT.anchoredPosition = new Vector2(0f, -2f);
            tplRT.sizeDelta = new Vector2(0f, 204f);
            Image tplImg = tplGo.GetComponent<Image>();
            tplImg.sprite = _whitePillSprite;
            tplImg.type = Image.Type.Sliced;
            tplImg.color = new Color(0.11f, 0.16f, 0.22f, 0.98f);

            GameObject vpGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
            vpGo.transform.SetParent(tplGo.transform, false);
            RectTransform vpRT = vpGo.GetComponent<RectTransform>();
            vpRT.anchorMin = Vector2.zero;
            vpRT.anchorMax = Vector2.one;
            vpRT.offsetMin = new Vector2(2f, 4f);
            vpRT.offsetMax = new Vector2(-2f, -4f);
            vpGo.GetComponent<Image>().color = Color.clear;

            GameObject contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(vpGo.transform, false);
            RectTransform cRT = contentGo.GetComponent<RectTransform>();
            cRT.anchorMin = new Vector2(0f, 1f);
            cRT.anchorMax = new Vector2(1f, 1f);
            cRT.pivot = new Vector2(0.5f, 1f);
            cRT.sizeDelta = new Vector2(0f, 34f);

            GameObject itemGo = new GameObject("Item", typeof(RectTransform), typeof(Toggle));
            itemGo.transform.SetParent(contentGo.transform, false);
            RectTransform iRT = itemGo.GetComponent<RectTransform>();
            iRT.anchorMin = new Vector2(0f, 0.5f);
            iRT.anchorMax = new Vector2(1f, 0.5f);
            iRT.sizeDelta = new Vector2(0f, 32f);

            GameObject iBgGo = new GameObject("Item Background", typeof(RectTransform), typeof(Image));
            iBgGo.transform.SetParent(itemGo.transform, false);
            RectTransform ibRT = iBgGo.GetComponent<RectTransform>();
            ibRT.anchorMin = Vector2.zero;
            ibRT.anchorMax = Vector2.one;
            ibRT.offsetMin = Vector2.zero;
            ibRT.offsetMax = Vector2.zero;
            Image ibImg = iBgGo.GetComponent<Image>();
            ibImg.color = new Color(0.16f, 0.22f, 0.30f, 0.85f);

            GameObject iChkGo = new GameObject("Item Checkmark", typeof(RectTransform), typeof(Image));
            iChkGo.transform.SetParent(itemGo.transform, false);
            RectTransform icRT = iChkGo.GetComponent<RectTransform>();
            icRT.anchorMin = new Vector2(0f, 0.5f);
            icRT.anchorMax = new Vector2(0f, 0.5f);
            icRT.sizeDelta = new Vector2(6f, 20f);
            icRT.anchoredPosition = new Vector2(8f, 0f);
            Image icImg = iChkGo.GetComponent<Image>();
            icImg.sprite = _whitePillSprite;
            icImg.type = Image.Type.Sliced;
            icImg.color = new Color(0.20f, 0.78f, 0.35f, 1f);

            GameObject iLblGo = new GameObject("Item Label", typeof(RectTransform), typeof(Text));
            iLblGo.transform.SetParent(itemGo.transform, false);
            RectTransform ilRT = iLblGo.GetComponent<RectTransform>();
            ilRT.anchorMin = Vector2.zero;
            ilRT.anchorMax = Vector2.one;
            ilRT.offsetMin = new Vector2(18f, 0f);
            ilRT.offsetMax = new Vector2(-8f, 0f);
            Text ilTxt = iLblGo.GetComponent<Text>();
            ilTxt.font = font;
            ilTxt.fontSize = 15;
            ilTxt.alignment = TextAnchor.MiddleLeft;
            ilTxt.color = Color.white;

            Toggle itemToggle = itemGo.GetComponent<Toggle>();
            itemToggle.targetGraphic = ibImg;
            itemToggle.graphic = icImg;
            itemToggle.isOn = true;

            ScrollRect sr = tplGo.GetComponent<ScrollRect>();
            sr.content = cRT;
            sr.viewport = vpRT;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;

            tplGo.SetActive(false);

            Dropdown dd = ddGo.GetComponent<Dropdown>();
            dd.targetGraphic = ddImg;
            dd.template = tplRT;
            dd.captionText = capTxt;
            dd.itemText = ilTxt;
            dd.options.Clear();
            foreach (string lbl in optionLabels)
            {
                dd.options.Add(new Dropdown.OptionData(lbl));
            }
            return dd;
        }

        public void EnsureUIHierarchy()
        {
            EnsureWhiteProceduralSprites();

            _rootRect = GetComponent<RectTransform>();
            if (_rootRect == null) _rootRect = gameObject.AddComponent<RectTransform>();
            _rootRect.anchorMin = Vector2.zero;
            _rootRect.anchorMax = Vector2.one;
            _rootRect.offsetMin = Vector2.zero;
            _rootRect.offsetMax = Vector2.zero;
            _rootRect.pivot = new Vector2(0.5f, 0.5f);

            // 1. 全屏底层遮罩
            Transform coverTr = transform.Find("FullScreenCover");
            GameObject coverObj = coverTr != null ? coverTr.gameObject : new GameObject("FullScreenCover", typeof(RectTransform), typeof(Image));
            coverObj.transform.SetParent(transform, false);
            coverObj.transform.SetSiblingIndex(0);
            StretchFull(coverObj.GetComponent<RectTransform>());
            _bgCover = coverObj.GetComponent<Image>();
            _bgCover.sprite = null;
            _bgCover.color = fullScreenCoverColor;
            _bgCover.raycastTarget = true;

            // 2. 滑动容器
            Transform slideTr = transform.Find("SlideRoot");
            GameObject slideObj = slideTr != null ? slideTr.gameObject : new GameObject("SlideRoot", typeof(RectTransform), typeof(Image));
            slideObj.transform.SetParent(transform, false);
            slideObj.transform.SetSiblingIndex(1);

            Image slideBg = slideObj.GetComponent<Image>() ?? slideObj.AddComponent<Image>();
            slideBg.sprite = null;
            slideBg.color = fullScreenCoverColor;
            slideBg.raycastTarget = true;

            _slideRoot = slideObj.GetComponent<RectTransform>();
            StretchFull(_slideRoot);
            _slideRoot.pivot = new Vector2(0.5f, 0.5f);

            Font mainFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 2.1 Header
            Transform headerTr = _slideRoot.Find("Header");
            GameObject headerObj = headerTr != null ? headerTr.gameObject : new GameObject("Header", typeof(RectTransform));
            headerObj.transform.SetParent(_slideRoot, false);
            RectTransform headerRT = headerObj.GetComponent<RectTransform>();
            headerRT.anchorMin = new Vector2(0f, 1f);
            headerRT.anchorMax = new Vector2(1f, 1f);
            headerRT.pivot = new Vector2(0.5f, 1f);
            headerRT.anchoredPosition = Vector2.zero;
            headerRT.sizeDelta = new Vector2(0f, 82f);

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

            Transform lineTr = headerObj.transform.Find("SepLine");
            GameObject lineObj = lineTr != null ? lineTr.gameObject : new GameObject("SepLine", typeof(RectTransform), typeof(Image));
            lineObj.transform.SetParent(headerObj.transform, false);
            RectTransform lineRT = lineObj.GetComponent<RectTransform>();
            lineRT.anchorMin = new Vector2(0f, 0f);
            lineRT.anchorMax = new Vector2(1f, 0f);
            lineRT.sizeDelta = new Vector2(0f, 2f);
            lineRT.anchoredPosition = Vector2.zero;
            lineObj.GetComponent<Image>().color = new Color(0.24f, 0.34f, 0.46f, 0.48f);

            // 右上角 X 关闭按钮
            _closeButton = EnsureCornerButton(headerObj.transform, "CloseButton", "✕", mainFont, closeBtnColor,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-28f, 0f), new Vector2(50f, 50f), 24);
            _closeButton.onClick.RemoveListener(HandleCloseClick);
            _closeButton.onClick.AddListener(HandleCloseClick);

            // 2.2 ScrollView + ItemsContainer (移除任何未嵌套的旧 ItemsContainer)
            Transform legacyCont = _slideRoot.Find("ItemsContainer");
            if (legacyCont != null) DestroyImmediate(legacyCont.gameObject);

            Transform scrollTr = _slideRoot.Find("ItemsScrollView");
            GameObject scrollObj = scrollTr != null ? scrollTr.gameObject : new GameObject("ItemsScrollView", typeof(RectTransform), typeof(ScrollRect));
            scrollObj.transform.SetParent(_slideRoot, false);
            RectTransform scrollRT = scrollObj.GetComponent<RectTransform>();
            scrollRT.anchorMin = new Vector2(0.04f, 0.14f);
            scrollRT.anchorMax = new Vector2(0.96f, 0.87f);
            scrollRT.offsetMin = Vector2.zero;
            scrollRT.offsetMax = Vector2.zero;

            Transform vpTr = scrollObj.transform.Find("Viewport");
            GameObject vpObj = vpTr != null ? vpTr.gameObject : new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
            vpObj.transform.SetParent(scrollObj.transform, false);
            RectTransform vpRT = vpObj.GetComponent<RectTransform>();
            StretchFull(vpRT);
            Image vpImg = vpObj.GetComponent<Image>();
            vpImg.color = Color.clear;
            vpImg.raycastTarget = true;

            Transform contTr = vpObj.transform.Find("ItemsContainer");
            GameObject contObj = contTr != null ? contTr.gameObject : new GameObject("ItemsContainer", typeof(RectTransform));
            contObj.transform.SetParent(vpObj.transform, false);
            _itemsContainer = contObj.GetComponent<RectTransform>();
            _itemsContainer.anchorMin = new Vector2(0f, 1f);
            _itemsContainer.anchorMax = new Vector2(1f, 1f);
            _itemsContainer.pivot = new Vector2(0.5f, 1f);
            _itemsContainer.anchoredPosition = Vector2.zero;
            _itemsContainer.sizeDelta = new Vector2(0f, 400f);

            ScrollRect sr = scrollObj.GetComponent<ScrollRect>();
            sr.viewport = vpRT;
            sr.content = _itemsContainer;
            sr.horizontal = false;
            sr.vertical = true;
            sr.scrollSensitivity = 25f;
            sr.movementType = ScrollRect.MovementType.Clamped;

            // 2.3 右下角：SaveButton 在左，ResetButton 在其右边
            _saveButton = EnsureCornerButton(_slideRoot, "SaveButton", "保 存", mainFont, saveBtnNormalColor,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-174f, 24f), new Vector2(140f, 50f), 20);
            _saveButtonImage = _saveButton.GetComponent<Image>();
            _saveButtonText = _saveButton.transform.Find("Text").GetComponent<Text>();
            _saveButton.onClick.RemoveListener(HandleSaveClick);
            _saveButton.onClick.AddListener(HandleSaveClick);

            _resetButton = EnsureCornerButton(_slideRoot, "ResetButton", "重 置", mainFont, resetBtnColor,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 24f), new Vector2(130f, 50f), 20);
            _resetButton.onClick.RemoveListener(HandleResetClick);
            _resetButton.onClick.AddListener(HandleResetClick);

            // 2.4 Toast
            EnsureToastHierarchy(mainFont);

            // 3. 未保存弹窗 + 重置确认弹窗
            EnsureUnsavedDialogHierarchy(mainFont);
            EnsureResetDialogHierarchy(mainFont);
        }

        private Button EnsureCornerButton(Transform parent, string name, string label, Font font, Color bgColor,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size, int fontSize)
        {
            Transform tr = parent.Find(name);
            GameObject obj = tr != null ? tr.gameObject : new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);

            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
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
            StretchFull(txtObj.GetComponent<RectTransform>());
            Text txt = txtObj.GetComponent<Text>();
            txt.font = font;
            txt.fontSize = fontSize;
            txt.fontStyle = FontStyle.Bold;
            txt.text = label;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
            return btn;
        }

        private void EnsureToastHierarchy(Font mainFont)
        {
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
            toastImg.sprite = _whitePillSprite;
            toastImg.type = Image.Type.Sliced;
            toastImg.color = new Color(0.10f, 0.48f, 0.28f, 0.96f);

            _toastGroup = _toastObj.GetComponent<CanvasGroup>();
            Transform tTextTr = _toastObj.transform.Find("Text");
            GameObject tTextObj = tTextTr != null ? tTextTr.gameObject : new GameObject("Text", typeof(RectTransform), typeof(Text));
            tTextObj.transform.SetParent(_toastObj.transform, false);
            StretchFull(tTextObj.GetComponent<RectTransform>());
            _toastText = tTextObj.GetComponent<Text>();
            _toastText.font = mainFont;
            _toastText.fontSize = 18;
            _toastText.fontStyle = FontStyle.Bold;
            _toastText.alignment = TextAnchor.MiddleCenter;
            _toastText.color = Color.white;
            _toastObj.SetActive(false);
        }

        private void EnsureUnsavedDialogHierarchy(Font font)
        {
            GameObject cardObj = CreateModalBase("UnsavedModalDialog", "提 示", "检测到设置尚未保存，是否保存当前修改？", font, out _modalDialogObj);

            const float btnW = 120f;
            const float btnH = 42f;
            _modalSaveAndExitBtn = EnsureModalButton(cardObj.transform, "BtnSaveExit", "保存退出", font, new Color(0.14f, 0.58f, 0.32f, 1f), new Vector2(-130f, 26f), new Vector2(btnW, btnH));
            _modalSaveAndExitBtn.onClick.RemoveListener(OnModalSaveAndExit);
            _modalSaveAndExitBtn.onClick.AddListener(OnModalSaveAndExit);

            _modalDiscardBtn = EnsureModalButton(cardObj.transform, "BtnDiscard", "放弃修改", font, new Color(0.68f, 0.22f, 0.22f, 1f), new Vector2(0f, 26f), new Vector2(btnW, btnH));
            _modalDiscardBtn.onClick.RemoveListener(OnModalDiscardAndExit);
            _modalDiscardBtn.onClick.AddListener(OnModalDiscardAndExit);

            _modalCancelBtn = EnsureModalButton(cardObj.transform, "BtnCancel", "取 消", font, new Color(0.25f, 0.32f, 0.42f, 1f), new Vector2(130f, 26f), new Vector2(btnW, btnH));
            _modalCancelBtn.onClick.RemoveListener(OnModalCancel);
            _modalCancelBtn.onClick.AddListener(OnModalCancel);

            _modalDialogObj.SetActive(false);
        }

        private void EnsureResetDialogHierarchy(Font font)
        {
            GameObject cardObj = CreateModalBase("ResetConfirmDialog", "重置确认", "确定要将当前页面的所有设置恢复为默认出厂值吗？", font, out _resetDialogObj);

            const float btnW = 148f;
            const float btnH = 44f;
            _resetConfirmBtn = EnsureModalButton(cardObj.transform, "BtnConfirmReset", "确认重置", font, new Color(0.72f, 0.24f, 0.22f, 1f), new Vector2(-88f, 26f), new Vector2(btnW, btnH));
            _resetConfirmBtn.onClick.RemoveListener(OnResetConfirm);
            _resetConfirmBtn.onClick.AddListener(OnResetConfirm);

            _resetCancelBtn = EnsureModalButton(cardObj.transform, "BtnCancelReset", "取 消", font, new Color(0.25f, 0.32f, 0.42f, 1f), new Vector2(88f, 26f), new Vector2(btnW, btnH));
            _resetCancelBtn.onClick.RemoveListener(OnResetCancel);
            _resetCancelBtn.onClick.AddListener(OnResetCancel);

            _resetDialogObj.SetActive(false);
        }

        private GameObject CreateModalBase(string modalName, string title, string message, Font font, out GameObject modalObj)
        {
            Transform modalTr = transform.Find(modalName);
            modalObj = modalTr != null ? modalTr.gameObject : new GameObject(modalName, typeof(RectTransform), typeof(Image));
            modalObj.transform.SetParent(transform, false);
            StretchFull(modalObj.GetComponent<RectTransform>());
            Image mBg = modalObj.GetComponent<Image>();
            mBg.sprite = null;
            mBg.color = new Color(0f, 0f, 0f, 0.68f);
            mBg.raycastTarget = true;

            Transform cardTr = modalObj.transform.Find("ModalCard");
            GameObject cardObj = cardTr != null ? cardTr.gameObject : new GameObject("ModalCard", typeof(RectTransform), typeof(Image));
            cardObj.transform.SetParent(modalObj.transform, false);
            RectTransform cRT = cardObj.GetComponent<RectTransform>();
            cRT.anchorMin = new Vector2(0.5f, 0.5f);
            cRT.anchorMax = new Vector2(0.5f, 0.5f);
            cRT.pivot = new Vector2(0.5f, 0.5f);
            cRT.anchoredPosition = Vector2.zero;
            cRT.sizeDelta = new Vector2(450f, 224f);

            Image cImg = cardObj.GetComponent<Image>();
            cImg.sprite = roundedPillSprite;
            cImg.type = Image.Type.Sliced;
            cImg.color = new Color(0.12f, 0.17f, 0.24f, 0.98f);

            Transform mTitTr = cardObj.transform.Find("Title");
            GameObject mTitObj = mTitTr != null ? mTitTr.gameObject : new GameObject("Title", typeof(RectTransform), typeof(Text));
            mTitObj.transform.SetParent(cardObj.transform, false);
            RectTransform mtRT = mTitObj.GetComponent<RectTransform>();
            mtRT.anchorMin = new Vector2(0f, 0.68f);
            mtRT.anchorMax = new Vector2(1f, 0.95f);
            mtRT.offsetMin = Vector2.zero;
            mtRT.offsetMax = Vector2.zero;
            Text mTitText = mTitObj.GetComponent<Text>();
            mTitText.font = font;
            mTitText.fontSize = 22;
            mTitText.fontStyle = FontStyle.Bold;
            mTitText.alignment = TextAnchor.MiddleCenter;
            mTitText.text = title;
            mTitText.color = Color.white;

            Transform mMsgTr = cardObj.transform.Find("Message");
            GameObject mMsgObj = mMsgTr != null ? mMsgTr.gameObject : new GameObject("Message", typeof(RectTransform), typeof(Text));
            mMsgObj.transform.SetParent(cardObj.transform, false);
            RectTransform mmRT = mMsgObj.GetComponent<RectTransform>();
            mmRT.anchorMin = new Vector2(0.06f, 0.36f);
            mmRT.anchorMax = new Vector2(0.94f, 0.68f);
            mmRT.offsetMin = Vector2.zero;
            mmRT.offsetMax = Vector2.zero;
            Text mMsgText = mMsgObj.GetComponent<Text>();
            mMsgText.font = font;
            mMsgText.fontSize = 17;
            mMsgText.alignment = TextAnchor.MiddleCenter;
            mMsgText.text = message;
            mMsgText.color = new Color(0.86f, 0.91f, 0.97f, 0.96f);

            return cardObj;
        }

        private Button EnsureModalButton(Transform parent, string name, string text, Font font, Color bgColor, Vector2 pos, Vector2 size)
        {
            return EnsureCornerButton(parent, name, text, font, bgColor,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), pos, size, 16);
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
