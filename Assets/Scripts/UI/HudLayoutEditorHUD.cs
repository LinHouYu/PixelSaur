using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PixelSaur.UI
{
    [Flags]
    public enum HudResizeEdge
    {
        None = 0,
        Left = 1,
        Right = 2,
        Top = 4,
        Bottom = 8,
        TopLeft = Top | Left,
        TopRight = Top | Right,
        BottomLeft = Bottom | Left,
        BottomRight = Bottom | Right
    }

    /// <summary>
    /// 跨场景游戏界面自定义布局编辑器（StartMenu 与 Game 双端可用并同步）：
    /// 1. 除 TopBarStatusHUD 外，所有游戏 HUD 均可自由拖动位置，并像 Windows 窗口一样按住四边/四角缩放；
    /// 2. 严格保持所有元素 localScale = (1, 1, 1) 不变，仅修改 sizeDelta 与 anchoredPosition；
    /// 3. 顶部工具栏提供【重置】【应用】【保存】【退出】；未保存退出弹出确认保存提示框。
    /// </summary>
    public class HudLayoutEditorHUD : MonoBehaviour
    {
        public static HudLayoutEditorHUD Instance { get; private set; }

        [Header("预览图标资产（用于 StartMenu 同步预览）")]
        [SerializeField] private Sprite skill1Sprite;
        [SerializeField] private Sprite skill2Sprite;
        [SerializeField] private Sprite skill3Sprite;
        [SerializeField] private Sprite skill4Sprite;
        [SerializeField] private Sprite skill5Sprite;
        [SerializeField] private Sprite dialogueSprite;
        [SerializeField] private Sprite eyeSprite;
        [SerializeField] private Sprite heartSprite;
        [SerializeField] private Sprite foodSprite;
        [SerializeField] private Sprite waterSprite;
        [SerializeField] private Sprite pillBgSprite;
        [SerializeField] private Sprite gearSprite;
        [SerializeField] private Sprite playersSprite;

        private RectTransform _canvasRect;
        private RectTransform _rootRect;
        private RectTransform _previewLayer;
        private RectTransform _handlesLayer;
        private RectTransform _toolbarRect;
        private Text _infoText;
        private Button _resetBtn;
        private Button _applyBtn;
        private Button _saveBtn;
        private Image _saveBtnImg;
        private Text _saveBtnText;
        private Button _exitBtn;

        private GameObject _toastObj;
        private Text _toastText;
        private Coroutine _toastCoroutine;

        private GameObject _unsavedModalObj;
        private Button _modalSaveExitBtn;
        private Button _modalDiscardExitBtn;
        private Button _modalCancelBtn;

        private bool _isOpen;
        private bool _isGameScene;
        private bool _isDirty;
        private string _selectedId = HudLayoutManager.IdStatusBarsPanel;

        private Dictionary<string, HudElementLayoutData> _savedSnapshot = new Dictionary<string, HudElementLayoutData>();
        private Dictionary<string, HudElementLayoutData> _draftLayout = new Dictionary<string, HudElementLayoutData>();
        private readonly Dictionary<string, HudEditableFrame> _frames = new Dictionary<string, HudEditableFrame>();
        private readonly Dictionary<string, RectTransform> _previewWidgets = new Dictionary<string, RectTransform>();
        private readonly List<GameObject> _hiddenWhileEditing = new List<GameObject>();

        private static Sprite _whiteSprite;
        private static Sprite _gridBgSprite;
        private static Sprite _circleBaseSprite;
        private static Sprite _circleKnobSprite;

        public bool IsOpen => _isOpen;
        public bool IsDirty => _isDirty;

        public static void OpenGlobalEditor()
        {
            HudLayoutEditorHUD editor = Instance;
            if (editor == null)
            {
                editor = FindAnyObjectByType<HudLayoutEditorHUD>(FindObjectsInactive.Include);
            }

            if (editor == null)
            {
                Canvas targetCanvas = null;
                GameObject hudCanvas = GameObject.Find("HUD_Canvas");
                if (hudCanvas != null) targetCanvas = hudCanvas.GetComponent<Canvas>();
                if (targetCanvas == null)
                {
                    GameObject startCanvas = GameObject.Find("StartMenu_Canvas");
                    if (startCanvas != null) targetCanvas = startCanvas.GetComponent<Canvas>();
                }
                if (targetCanvas == null) targetCanvas = FindAnyObjectByType<Canvas>();
                if (targetCanvas == null) return;

                GameObject go = new GameObject("HudLayoutEditorPanel", typeof(RectTransform), typeof(HudLayoutEditorHUD));
                go.transform.SetParent(targetCanvas.transform, false);
                editor = go.GetComponent<HudLayoutEditorHUD>();
            }

            editor.OpenEditor();
        }

        private void Awake()
        {
            Instance = this;
            LoadEditorFallbackSprites();
            EnsureUIHierarchy();
        }

        private void OnEnable()
        {
            Instance = this;
        }

        private void LoadEditorFallbackSprites()
        {
#if UNITY_EDITOR
            if (skill1Sprite == null) skill1Sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/skill1.png");
            if (skill2Sprite == null) skill2Sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/skill2.png");
            if (skill3Sprite == null) skill3Sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/skill3.png");
            if (skill4Sprite == null) skill4Sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/skill4.png");
            if (skill5Sprite == null) skill5Sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/skill5.png");
            if (dialogueSprite == null) dialogueSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_dialogue_32x32.png");
            if (eyeSprite == null) eyeSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/eyes.png");
            if (heartSprite == null) heartSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_heart_8x8.png");
            if (foodSprite == null) foodSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_food_8x8.png");
            if (waterSprite == null) waterSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_water_8x8.png");
            if (pillBgSprite == null) pillBgSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/hud_pill_bg_64x64.png");
            if (gearSprite == null) gearSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_gear_32x32.png");
            if (playersSprite == null) playersSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_players_32x32.png");
#endif
            EnsureProceduralSprites();
        }

        private static void EnsureProceduralSprites()
        {
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
            if (_circleBaseSprite == null) _circleBaseSprite = CreateCircleSprite(128, true);
            if (_circleKnobSprite == null) _circleKnobSprite = CreateCircleSprite(64, false);
            if (_gridBgSprite == null) _gridBgSprite = CreateDarkGridSprite(64);
        }

        private static Sprite CreateDarkGridSprite(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "HudEditorDarkGridTile",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color bg = new Color(0.11f, 0.13f, 0.17f, 1.0f);
            Color line = new Color(0.16f, 0.19f, 0.24f, 1.0f);
            Color[] px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    px[y * size + x] = (x == 0 || y == 0) ? line : bg;
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            Sprite sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            sp.hideFlags = HideFlags.HideAndDontSave;
            return sp;
        }

        private static Sprite CreateCircleSprite(int size, bool ring)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color[] px = new Color[size * size];
            float c = (size - 1) * 0.5f;
            float r = size * 0.48f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
                    float a = Mathf.Clamp01((r - d) * 0.5f);
                    if (a <= 0f) px[y * size + x] = Color.clear;
                    else if (ring)
                    {
                        float rf = Mathf.Clamp01((d - (r - size * 0.05f)) * 0.5f);
                        float br = Mathf.Lerp(0.20f, 0.92f, rf);
                        px[y * size + x] = new Color(br, br, br, Mathf.Lerp(0.45f, 0.90f, rf) * a);
                    }
                    else
                    {
                        px[y * size + x] = new Color(0.95f, 0.95f, 0.95f, a);
                    }
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            Sprite sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            sp.hideFlags = HideFlags.HideAndDontSave;
            return sp;
        }

        public void OpenEditor()
        {
            LoadEditorFallbackSprites();
            EnsureUIHierarchy();

            _isGameScene = GameObject.Find("HUD_Canvas") != null;
            _savedSnapshot = CloneLayout(HudLayoutManager.LoadSavedLayout());
            _draftLayout = CloneLayout(_savedSnapshot);
            _isDirty = false;

            HideInterferingMenus();

            // 无论在 StartMenu 还是 Game 中，均在纯暗灰不透明背景画布上专注展示并编辑全部 9 个 HUD 控件，
            // 完全遮蔽后方游戏画面，同时在 Game 场景中保存/应用时同步至真实 HUD。
            BuildStartMenuPreviewWidgets();
            _previewLayer.gameObject.SetActive(true);
            ApplyDraftToStartMenuPreview();

            if (_isGameScene)
            {
                HudLayoutManager.ApplyLayoutToGameHud(_draftLayout);
            }

            BuildEditableFrames();
            SelectElement(_selectedId);
            UpdateSaveButtonVisual();

            if (_unsavedModalObj != null) _unsavedModalObj.SetActive(false);
            if (_toastObj != null) _toastObj.SetActive(false);

            _isOpen = true;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            UISoundManager.PlayOpen();
        }

        public void RequestExit()
        {
            if (_isDirty)
            {
                if (_unsavedModalObj != null)
                {
                    _unsavedModalObj.SetActive(true);
                    _unsavedModalObj.transform.SetAsLastSibling();
                }
                return;
            }

            CloseEditor(saveBeforeExit: false, revertToSaved: false);
        }

        public void CloseEditor(bool saveBeforeExit, bool revertToSaved)
        {
            if (saveBeforeExit)
            {
                SaveCurrentLayout(showToast: false);
            }
            else if (revertToSaved)
            {
                _draftLayout = CloneLayout(_savedSnapshot);
                if (_isGameScene)
                {
                    HudLayoutManager.ApplyLayoutToGameHud(_savedSnapshot);
                }
            }

            if (_isGameScene)
            {
                GameObject hudCanvas = GameObject.Find("HUD_Canvas");
                if (hudCanvas != null)
                {
                    var progressHud = hudCanvas.GetComponent<InteractionProgressHUD>();
                    if (progressHud != null) progressHud.SetEditorPreviewMode(false);
                }
            }

            if (_unsavedModalObj != null) _unsavedModalObj.SetActive(false);
            _isOpen = false;
            gameObject.SetActive(false);
            RestoreHiddenMenus();
            UISoundManager.PlayClose();
        }

        private void HideInterferingMenus()
        {
            _hiddenWhileEditing.Clear();

            if (SubSettingsPageHUD.Instance != null && SubSettingsPageHUD.Instance.gameObject.activeSelf)
            {
                _hiddenWhileEditing.Add(SubSettingsPageHUD.Instance.gameObject);
                SubSettingsPageHUD.Instance.gameObject.SetActive(false);
            }

            if (SettingsMenuPanelHUD.Instance != null && SettingsMenuPanelHUD.Instance.gameObject.activeSelf)
            {
                _hiddenWhileEditing.Add(SettingsMenuPanelHUD.Instance.gameObject);
                SettingsMenuPanelHUD.Instance.gameObject.SetActive(false);
            }

            if (!_isGameScene)
            {
                GameObject startCanvas = GameObject.Find("StartMenu_Canvas");
                if (startCanvas != null)
                {
                    Transform safeArea = startCanvas.transform.Find("SafeAreaRoot") ?? startCanvas.transform.Find("SafeArea");
                    if (safeArea != null && safeArea.gameObject.activeSelf)
                    {
                        _hiddenWhileEditing.Add(safeArea.gameObject);
                        safeArea.gameObject.SetActive(false);
                    }
                    else
                    {
                        string[] hideNames = { "TopBar", "BottomActionDock", "RightQuickDock", "TitleLogo" };
                        foreach (string n in hideNames)
                        {
                            Transform t = startCanvas.transform.Find(n);
                            if (t != null && t.gameObject.activeSelf)
                            {
                                _hiddenWhileEditing.Add(t.gameObject);
                                t.gameObject.SetActive(false);
                            }
                        }
                    }
                }
            }
        }

        private void RestoreHiddenMenus()
        {
            for (int i = 0; i < _hiddenWhileEditing.Count; i++)
            {
                if (_hiddenWhileEditing[i] != null)
                {
                    _hiddenWhileEditing[i].SetActive(true);
                }
            }
            _hiddenWhileEditing.Clear();
        }

        public void EnsureUIHierarchy()
        {
            _rootRect = GetComponent<RectTransform>();
            if (_rootRect == null) _rootRect = gameObject.AddComponent<RectTransform>();
            _rootRect.anchorMin = Vector2.zero;
            _rootRect.anchorMax = Vector2.one;
            _rootRect.offsetMin = Vector2.zero;
            _rootRect.offsetMax = Vector2.zero;
            _rootRect.localScale = Vector3.one;

            Canvas c = GetComponentInParent<Canvas>();
            if (c != null) _canvasRect = c.GetComponent<RectTransform>();

            // 纯不透明深灰对齐网格画布（完全屏蔽后方游戏画面，让玩家专注设置界面布局）
            Image dimBg = GetComponent<Image>();
            if (dimBg == null) dimBg = gameObject.AddComponent<Image>();
            dimBg.sprite = _gridBgSprite;
            dimBg.type = Image.Type.Tiled;
            dimBg.color = Color.white;
            dimBg.raycastTarget = true;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // 1. StartMenu 预览层
            Transform prevTr = transform.Find("PreviewLayer");
            if (prevTr == null)
            {
                GameObject go = new GameObject("PreviewLayer", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                prevTr = go.transform;
            }
            _previewLayer = prevTr.GetComponent<RectTransform>();
            StretchFull(_previewLayer);

            // 2. Windows 拖拽与边角缩放操作框层
            Transform handleTr = transform.Find("HandlesLayer");
            if (handleTr == null)
            {
                GameObject go = new GameObject("HandlesLayer", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                handleTr = go.transform;
            }
            _handlesLayer = handleTr.GetComponent<RectTransform>();
            StretchFull(_handlesLayer);

            // 3. 顶部工具栏（置于右上区，避开左上 TopBarStatusHUD 与正中 InteractionProgressBar）
            BuildTopToolbar(font);

            // 4. Toast 提示
            BuildToast(font);

            // 5. 未保存退出确认弹窗
            BuildUnsavedModal(font);
        }

        private void BuildTopToolbar(Font font)
        {
            Transform old = transform.Find("TopToolbar");
            if (old != null) DestroyImmediate(old.gameObject);

            GameObject bar = new GameObject("TopToolbar", typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(transform, false);
            _toolbarRect = bar.GetComponent<RectTransform>();
            _toolbarRect.anchorMin = new Vector2(1f, 1f);
            _toolbarRect.anchorMax = new Vector2(1f, 1f);
            _toolbarRect.pivot = new Vector2(1f, 1f);
            _toolbarRect.anchoredPosition = new Vector2(-20f, -18f);
            _toolbarRect.sizeDelta = new Vector2(620f, 74f);

            Image barBg = bar.GetComponent<Image>();
            barBg.color = new Color(0.08f, 0.11f, 0.16f, 0.94f);
            barBg.raycastTarget = true;

            Outline barOut = bar.AddComponent<Outline>();
            barOut.effectColor = new Color(0.32f, 0.68f, 0.92f, 0.85f);
            barOut.effectDistance = new Vector2(2f, -2f);

            _resetBtn = CreateToolbarButton(bar.transform, "ResetButton", "重 置", font, new Vector2(12f, 0f), 136f, new Color(0.54f, 0.24f, 0.20f, 1f), out _, out _);
            _applyBtn = CreateToolbarButton(bar.transform, "ApplyButton", "应 用", font, new Vector2(162f, 0f), 136f, new Color(0.18f, 0.44f, 0.68f, 1f), out _, out _);
            _saveBtn = CreateToolbarButton(bar.transform, "SaveButton", "保 存", font, new Vector2(312f, 0f), 144f, new Color(0.16f, 0.56f, 0.34f, 1f), out _saveBtnImg, out _saveBtnText);
            _exitBtn = CreateToolbarButton(bar.transform, "ExitButton", "退 出", font, new Vector2(470f, 0f), 136f, new Color(0.28f, 0.32f, 0.40f, 1f), out _, out _);

            _resetBtn.onClick.AddListener(HandleResetClicked);
            _applyBtn.onClick.AddListener(HandleApplyClicked);
            _saveBtn.onClick.AddListener(() => SaveCurrentLayout(showToast: true));
            _exitBtn.onClick.AddListener(RequestExit);

            // 选中状态与操作提示条（置于工具栏正下方）
            Transform oldInfo = transform.Find("EditorInfoBanner");
            if (oldInfo != null) DestroyImmediate(oldInfo.gameObject);

            GameObject infoObj = new GameObject("EditorInfoBanner", typeof(RectTransform), typeof(Image));
            infoObj.transform.SetParent(transform, false);
            RectTransform infoRT = infoObj.GetComponent<RectTransform>();
            infoRT.anchorMin = new Vector2(1f, 1f);
            infoRT.anchorMax = new Vector2(1f, 1f);
            infoRT.pivot = new Vector2(1f, 1f);
            infoRT.anchoredPosition = new Vector2(-20f, -100f);
            infoRT.sizeDelta = new Vector2(620f, 44f);
            Image infoBg = infoObj.GetComponent<Image>();
            infoBg.color = new Color(0.06f, 0.09f, 0.13f, 0.86f);
            infoBg.raycastTarget = false;

            GameObject txtGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            txtGo.transform.SetParent(infoObj.transform, false);
            RectTransform txtRT = txtGo.GetComponent<RectTransform>();
            StretchFull(txtRT);
            txtRT.offsetMin = new Vector2(12f, 0f);
            txtRT.offsetMax = new Vector2(-12f, 0f);

            _infoText = txtGo.GetComponent<Text>();
            _infoText.font = font;
            _infoText.fontSize = 20;
            _infoText.fontStyle = FontStyle.Bold;
            _infoText.alignment = TextAnchor.MiddleCenter;
            _infoText.color = new Color(0.84f, 0.94f, 1f, 1f);
            _infoText.raycastTarget = false;
            _infoText.text = "拖动中心移动，按住四边/四角缩放（Scale固定）";
        }

        private Button CreateToolbarButton(Transform parent, string name, string label, Font font, Vector2 pos, float width, Color color, out Image outImg, out Text outText)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(width, 52f);

            outImg = go.GetComponent<Image>();
            outImg.color = color;
            outImg.raycastTarget = true;

            Button btn = go.GetComponent<Button>();
            btn.targetGraphic = outImg;

            GameObject txtGo = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(Outline));
            txtGo.transform.SetParent(go.transform, false);
            StretchFull(txtGo.GetComponent<RectTransform>());
            outText = txtGo.GetComponent<Text>();
            outText.font = font;
            outText.fontSize = 25;
            outText.fontStyle = FontStyle.Bold;
            outText.alignment = TextAnchor.MiddleCenter;
            outText.color = Color.white;
            outText.text = label;
            outText.raycastTarget = false;

            Outline o = txtGo.GetComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.85f);
            o.effectDistance = new Vector2(1.2f, -1.2f);
            return btn;
        }

        private void BuildToast(Font font)
        {
            Transform old = transform.Find("EditorToast");
            if (old != null) DestroyImmediate(old.gameObject);

            _toastObj = new GameObject("EditorToast", typeof(RectTransform), typeof(Image));
            _toastObj.transform.SetParent(transform, false);
            RectTransform rt = _toastObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -110f);
            rt.sizeDelta = new Vector2(520f, 54f);

            Image bg = _toastObj.GetComponent<Image>();
            bg.color = new Color(0.08f, 0.24f, 0.16f, 0.94f);
            bg.raycastTarget = false;

            GameObject txtGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            txtGo.transform.SetParent(_toastObj.transform, false);
            StretchFull(txtGo.GetComponent<RectTransform>());
            _toastText = txtGo.GetComponent<Text>();
            _toastText.font = font;
            _toastText.fontSize = 24;
            _toastText.fontStyle = FontStyle.Bold;
            _toastText.alignment = TextAnchor.MiddleCenter;
            _toastText.color = Color.white;
            _toastText.raycastTarget = false;

            _toastObj.SetActive(false);
        }

        private void BuildUnsavedModal(Font font)
        {
            Transform old = transform.Find("UnsavedConfirmOverlay");
            if (old != null) DestroyImmediate(old.gameObject);

            _unsavedModalObj = new GameObject("UnsavedConfirmOverlay", typeof(RectTransform), typeof(Image));
            _unsavedModalObj.transform.SetParent(transform, false);
            StretchFull(_unsavedModalObj.GetComponent<RectTransform>());
            Image maskImg = _unsavedModalObj.GetComponent<Image>();
            maskImg.color = new Color(0f, 0f, 0f, 0.68f);
            maskImg.raycastTarget = true;

            GameObject box = new GameObject("ConfirmBox", typeof(RectTransform), typeof(Image));
            box.transform.SetParent(_unsavedModalObj.transform, false);
            RectTransform boxRT = box.GetComponent<RectTransform>();
            boxRT.anchorMin = new Vector2(0.5f, 0.5f);
            boxRT.anchorMax = new Vector2(0.5f, 0.5f);
            boxRT.pivot = new Vector2(0.5f, 0.5f);
            boxRT.sizeDelta = new Vector2(680f, 300f);

            Image boxBg = box.GetComponent<Image>();
            boxBg.color = new Color(0.11f, 0.14f, 0.20f, 0.98f);

            Outline boxOut = box.AddComponent<Outline>();
            boxOut.effectColor = new Color(0.45f, 0.78f, 1f, 0.90f);
            boxOut.effectDistance = new Vector2(2f, -2f);

            CreateModalText(box.transform, "Title", "尚未保存布局修改", font, 34, new Vector2(0f, 84f), new Vector2(620f, 52f), new Color(1f, 0.88f, 0.45f, 1f));
            CreateModalText(box.transform, "Message", "检测到您还没有点击【保存】，是否确认保存当前界面布局后退出？", font, 24, new Vector2(0f, 18f), new Vector2(620f, 70f), Color.white);

            _modalSaveExitBtn = CreateModalButton(box.transform, "SaveAndExitBtn", "保存并退出", font, new Vector2(-205f, -86f), 192f, new Color(0.16f, 0.58f, 0.34f, 1f));
            _modalDiscardExitBtn = CreateModalButton(box.transform, "DiscardExitBtn", "不保存退出", font, new Vector2(0f, -86f), 192f, new Color(0.58f, 0.22f, 0.22f, 1f));
            _modalCancelBtn = CreateModalButton(box.transform, "CancelBtn", "继续编辑", font, new Vector2(205f, -86f), 192f, new Color(0.28f, 0.34f, 0.44f, 1f));

            _modalSaveExitBtn.onClick.AddListener(() => CloseEditor(saveBeforeExit: true, revertToSaved: false));
            _modalDiscardExitBtn.onClick.AddListener(() => CloseEditor(saveBeforeExit: false, revertToSaved: true));
            _modalCancelBtn.onClick.AddListener(() => _unsavedModalObj.SetActive(false));

            _unsavedModalObj.SetActive(false);
        }

        private static void CreateModalText(Transform parent, string name, string content, Font font, int size, Vector2 pos, Vector2 dim, Color col)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = dim;

            Text t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = col;
            t.text = content;
            t.raycastTarget = false;
        }

        private static Button CreateModalButton(Transform parent, string name, string label, Font font, Vector2 pos, float width, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(width, 62f);

            Image img = go.GetComponent<Image>();
            img.color = color;
            Button btn = go.GetComponent<Button>();
            btn.targetGraphic = img;

            CreateModalText(go.transform, "Text", label, font, 24, Vector2.zero, new Vector2(width, 62f), Color.white);
            return btn;
        }

        private void BuildStartMenuPreviewWidgets()
        {
            for (int i = _previewLayer.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(_previewLayer.GetChild(i).gameObject);
            }
            _previewWidgets.Clear();

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // 1. 锁定占位：左上角 TopBarStatusHUD（严格按原始 360x68 展示参考位置，不可拖动/缩放）
            GameObject lockedTop = new GameObject("LockedTopBarPreview", typeof(RectTransform), typeof(Image));
            lockedTop.transform.SetParent(_previewLayer, false);
            RectTransform topRT = lockedTop.GetComponent<RectTransform>();
            topRT.anchorMin = new Vector2(0f, 1f);
            topRT.anchorMax = new Vector2(0f, 1f);
            topRT.pivot = new Vector2(0f, 1f);
            topRT.anchoredPosition = new Vector2(24f, -20f);
            topRT.sizeDelta = new Vector2(360f, 68f);
            Image topBg = lockedTop.GetComponent<Image>();
            if (pillBgSprite != null) { topBg.sprite = pillBgSprite; topBg.type = Image.Type.Sliced; }
            topBg.color = new Color(0.16f, 0.18f, 0.22f, 0.92f);
            topBg.raycastTarget = false;

            GameObject lockTxtGo = new GameObject("LockLabel", typeof(RectTransform), typeof(Text));
            lockTxtGo.transform.SetParent(lockedTop.transform, false);
            StretchFull(lockTxtGo.GetComponent<RectTransform>());
            Text lockTxt = lockTxtGo.GetComponent<Text>();
            lockTxt.font = font;
            lockTxt.fontSize = 18;
            lockTxt.fontStyle = FontStyle.Bold;
            lockTxt.alignment = TextAnchor.MiddleCenter;
            lockTxt.color = new Color(0.75f, 0.80f, 0.88f, 0.95f);
            lockTxt.text = "TopBarStatusHUD（锁定 · 不可调）";
            lockTxt.raycastTarget = false;

            // 2. 构建 9 个可编辑游戏 HUD 预览控件
            foreach (var meta in HudLayoutManager.EditableElements)
            {
                RectTransform rt = CreatePreviewWidgetForElement(meta, font);
                _previewWidgets[meta.id] = rt;
            }
        }

        private RectTransform CreatePreviewWidgetForElement(HudElementMeta meta, Font font)
        {
            GameObject go = new GameObject("Preview_" + meta.id, typeof(RectTransform));
            go.transform.SetParent(_previewLayer, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = meta.anchorMin;
            rt.anchorMax = meta.anchorMax;
            rt.pivot = meta.pivot;
            rt.localScale = Vector3.one;

            if (meta.id == HudLayoutManager.IdVirtualJoystick)
            {
                Image bg = go.AddComponent<Image>();
                bg.sprite = _circleBaseSprite;
                bg.color = new Color(0.10f, 0.14f, 0.20f, 0.78f);
                bg.raycastTarget = false;

                GameObject knob = new GameObject("Handle", typeof(RectTransform), typeof(Image));
                knob.transform.SetParent(go.transform, false);
                RectTransform krt = knob.GetComponent<RectTransform>();
                krt.anchorMin = new Vector2(0.30f, 0.30f);
                krt.anchorMax = new Vector2(0.70f, 0.70f);
                krt.offsetMin = Vector2.zero;
                krt.offsetMax = Vector2.zero;
                Image kimg = knob.GetComponent<Image>();
                kimg.sprite = _circleKnobSprite;
                kimg.color = Color.white;
                kimg.raycastTarget = false;
            }
            else if (meta.id == HudLayoutManager.IdStatusBarsPanel)
            {
                CreatePreviewStatusBar(go.transform, 0, new Color(0.40f, 0.78f, 0.16f, 1f), heartSprite, font);
                CreatePreviewStatusBar(go.transform, 1, new Color(0.90f, 0.48f, 0.12f, 1f), foodSprite, font);
                CreatePreviewStatusBar(go.transform, 2, new Color(0.18f, 0.65f, 0.95f, 1f), waterSprite, font);
            }
            else if (meta.id == HudLayoutManager.IdInteractionProgressBar)
            {
                Image bg = go.AddComponent<Image>();
                bg.color = new Color(0.08f, 0.10f, 0.14f, 0.90f);
                bg.raycastTarget = false;

                GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
                fill.transform.SetParent(go.transform, false);
                RectTransform frt = fill.GetComponent<RectTransform>();
                frt.anchorMin = new Vector2(0f, 0f);
                frt.anchorMax = new Vector2(0.65f, 1f);
                frt.offsetMin = new Vector2(3f, 3f);
                frt.offsetMax = new Vector2(-3f, -3f);
                Image fimg = fill.GetComponent<Image>();
                fimg.color = new Color(0.18f, 0.65f, 0.95f, 1f);
                fimg.raycastTarget = false;

                GameObject txtGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
                txtGo.transform.SetParent(go.transform, false);
                StretchFull(txtGo.GetComponent<RectTransform>());
                Text t = txtGo.GetComponent<Text>();
                t.font = font;
                t.fontSize = 24;
                t.fontStyle = FontStyle.Bold;
                t.alignment = TextAnchor.MiddleCenter;
                t.color = Color.white;
                t.text = "喝水中 0.2s";
                t.raycastTarget = false;
            }
            else
            {
                Image img = go.AddComponent<Image>();
                img.sprite = ResolveButtonSprite(meta.id);
                img.preserveAspect = true;
                img.color = Color.white;
                img.raycastTarget = false;
            }

            return rt;
        }

        private static void CreatePreviewStatusBar(Transform parent, int index, Color fillColor, Sprite icon, Font font)
        {
            float segW = 1f / 3f;
            float xMin = index * segW + 0.01f;
            float xMax = (index + 1) * segW - 0.01f;

            GameObject item = new GameObject("Bar_" + index, typeof(RectTransform));
            item.transform.SetParent(parent, false);
            RectTransform rt = item.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(xMin, 0.08f);
            rt.anchorMax = new Vector2(xMax, 0.92f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            GameObject ic = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            ic.transform.SetParent(item.transform, false);
            RectTransform irt = ic.GetComponent<RectTransform>();
            irt.anchorMin = new Vector2(0f, 0.05f);
            irt.anchorMax = new Vector2(0.14f, 0.95f);
            irt.offsetMin = Vector2.zero;
            irt.offsetMax = Vector2.zero;
            Image iimg = ic.GetComponent<Image>();
            iimg.sprite = icon;
            iimg.preserveAspect = true;
            iimg.color = icon != null ? Color.white : fillColor;
            iimg.raycastTarget = false;

            GameObject barBg = new GameObject("BarBg", typeof(RectTransform), typeof(Image));
            barBg.transform.SetParent(item.transform, false);
            RectTransform brt = barBg.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.16f, 0.12f);
            brt.anchorMax = new Vector2(1f, 0.88f);
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;
            Image bimg = barBg.GetComponent<Image>();
            bimg.color = new Color(0.10f, 0.12f, 0.15f, 0.92f);
            bimg.raycastTarget = false;

            GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(barBg.transform, false);
            RectTransform frt = fill.GetComponent<RectTransform>();
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = Vector2.one;
            frt.offsetMin = new Vector2(2f, 2f);
            frt.offsetMax = new Vector2(-2f, -2f);
            Image fimg = fill.GetComponent<Image>();
            fimg.color = fillColor;
            fimg.raycastTarget = false;

            GameObject txtGo = new GameObject("Val", typeof(RectTransform), typeof(Text));
            txtGo.transform.SetParent(barBg.transform, false);
            StretchFull(txtGo.GetComponent<RectTransform>());
            Text t = txtGo.GetComponent<Text>();
            t.font = font;
            t.fontSize = 22;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.text = "100";
            t.raycastTarget = false;
        }

        private Sprite ResolveButtonSprite(string id)
        {
            switch (id)
            {
                case HudLayoutManager.IdDinoJumpButton: return skill5Sprite;
                case HudLayoutManager.IdDinoAttackButton: return skill4Sprite;
                case HudLayoutManager.IdDinoSneakButton: return skill2Sprite;
                case HudLayoutManager.IdDinoSkill1Button: return skill3Sprite;
                case HudLayoutManager.IdDinoSkill2Button: return skill1Sprite;
                case HudLayoutManager.IdDialogueButton: return dialogueSprite;
                case HudLayoutManager.IdFreeLookEyeButton: return eyeSprite;
                default: return _whiteSprite;
            }
        }

        private void BuildEditableFrames()
        {
            for (int i = _handlesLayer.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(_handlesLayer.GetChild(i).gameObject);
            }
            _frames.Clear();

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            foreach (var meta in HudLayoutManager.EditableElements)
            {
                GameObject frameGo = new GameObject("Frame_" + meta.id, typeof(RectTransform), typeof(HudEditableFrame));
                frameGo.transform.SetParent(_handlesLayer, false);
                HudEditableFrame frame = frameGo.GetComponent<HudEditableFrame>();
                frame.Initialize(this, meta, _draftLayout[meta.id], font, _whiteSprite);
                _frames[meta.id] = frame;
            }
        }

        public void SelectElement(string id)
        {
            _selectedId = id;
            foreach (var kv in _frames)
            {
                kv.Value.SetSelected(kv.Key == id);
            }
            UpdateInfoBanner();
        }

        public void OnFrameTransformChanged(string id, Vector2 newPos, Vector2 newSize)
        {
            var meta = HudLayoutManager.GetMeta(id);
            if (meta == null || !_draftLayout.TryGetValue(id, out var data)) return;

            data.posX = newPos.x;
            data.posY = newPos.y;
            data.width = Mathf.Clamp(newSize.x, meta.minSize.x, meta.maxSize.x);
            data.height = Mathf.Clamp(newSize.y, meta.minSize.y, meta.maxSize.y);

            if (_previewWidgets.TryGetValue(id, out var rt) && rt != null)
            {
                rt.anchorMin = meta.anchorMin;
                rt.anchorMax = meta.anchorMax;
                rt.pivot = meta.pivot;
                rt.localScale = Vector3.one;
                rt.anchoredPosition = new Vector2(data.posX, data.posY);
                rt.sizeDelta = new Vector2(data.width, data.height);
            }

            if (_isGameScene)
            {
                GameObject hudCanvas = GameObject.Find("HUD_Canvas");
                if (hudCanvas != null)
                {
                    HudLayoutManager.ApplySingleElementToGameHud(hudCanvas.transform, meta, data);
                }
            }

            _isDirty = !HudLayoutManager.LayoutsMatch(_draftLayout, _savedSnapshot);
            UpdateSaveButtonVisual();
            UpdateInfoBanner();
        }

        private void ApplyDraftToStartMenuPreview()
        {
            foreach (var meta in HudLayoutManager.EditableElements)
            {
                if (_previewWidgets.TryGetValue(meta.id, out var rt) && rt != null && _draftLayout.TryGetValue(meta.id, out var data))
                {
                    rt.anchorMin = meta.anchorMin;
                    rt.anchorMax = meta.anchorMax;
                    rt.pivot = meta.pivot;
                    rt.localScale = Vector3.one;
                    rt.anchoredPosition = new Vector2(data.posX, data.posY);
                    rt.sizeDelta = new Vector2(data.width, data.height);
                }
            }
        }

        private void SyncAllFramesFromDraft()
        {
            foreach (var meta in HudLayoutManager.EditableElements)
            {
                if (_frames.TryGetValue(meta.id, out var frame) && _draftLayout.TryGetValue(meta.id, out var data))
                {
                    frame.ApplyLayoutData(data);
                }
            }
        }

        private void HandleResetClicked()
        {
            _draftLayout = HudLayoutManager.GetDefaultLayout();
            SyncAllFramesFromDraft();
            ApplyDraftToStartMenuPreview();

            if (_isGameScene)
            {
                HudLayoutManager.ApplyLayoutToGameHud(_draftLayout);
            }

            _isDirty = !HudLayoutManager.LayoutsMatch(_draftLayout, _savedSnapshot);
            UpdateSaveButtonVisual();
            UpdateInfoBanner();
            ShowToast("已恢复默认 1.5x 无重叠布局（请点击保存生效）");
            UISoundManager.PlayClick();
        }

        private void HandleApplyClicked()
        {
            ApplyDraftToStartMenuPreview();
            if (_isGameScene)
            {
                HudLayoutManager.ApplyLayoutToGameHud(_draftLayout);
            }

            ShowToast("已应用当前界面布局预览（未写入存档）");
            UISoundManager.PlayClick();
        }

        public void SaveCurrentLayout(bool showToast)
        {
            HudLayoutManager.SaveLayout(_draftLayout);
            _savedSnapshot = CloneLayout(_draftLayout);
            _isDirty = false;
            UpdateSaveButtonVisual();
            if (showToast)
            {
                ShowToast("布局已保存，大厅与游戏场景已同步！");
            }
            UISoundManager.PlayClick();
        }

        private void UpdateSaveButtonVisual()
        {
            if (_saveBtnImg != null)
            {
                _saveBtnImg.color = _isDirty
                    ? new Color(0.14f, 0.72f, 0.36f, 1f)
                    : new Color(0.18f, 0.42f, 0.30f, 1f);
            }
            if (_saveBtnText != null)
            {
                _saveBtnText.text = _isDirty ? "保 存 *" : "保 存";
            }
        }

        private void UpdateInfoBanner()
        {
            if (_infoText == null) return;
            var meta = HudLayoutManager.GetMeta(_selectedId);
            if (meta != null && _draftLayout.TryGetValue(_selectedId, out var d))
            {
                _infoText.text = $"选中: {meta.displayName}  位置({Mathf.RoundToInt(d.posX)},{Mathf.RoundToInt(d.posY)})  尺寸 {Mathf.RoundToInt(d.width)}×{Mathf.RoundToInt(d.height)}  Scale=1";
            }
        }

        private void ShowToast(string msg)
        {
            if (_toastObj == null || _toastText == null) return;
            _toastText.text = msg;
            _toastObj.SetActive(true);
            _toastObj.transform.SetAsLastSibling();
            if (_toastCoroutine != null) StopCoroutine(_toastCoroutine);
            _toastCoroutine = StartCoroutine(HideToastRoutine());
        }

        private IEnumerator HideToastRoutine()
        {
            yield return new WaitForSecondsRealtime(2.0f);
            if (_toastObj != null) _toastObj.SetActive(false);
            _toastCoroutine = null;
        }

        public Vector2 ClampPositionInCanvas(HudElementMeta meta, Vector2 pos, Vector2 size)
        {
            Vector2 canvasSize = _canvasRect != null && _canvasRect.rect.width > 100f
                ? _canvasRect.rect.size
                : new Vector2(1920f, 1080f);

            float anchorX = meta.anchorMin.x * canvasSize.x;
            float anchorY = meta.anchorMin.y * canvasSize.y;

            float minX = size.x * meta.pivot.x - anchorX;
            float maxX = canvasSize.x - size.x * (1f - meta.pivot.x) - anchorX;
            float minY = size.y * meta.pivot.y - anchorY;
            float maxY = canvasSize.y - size.y * (1f - meta.pivot.y) - anchorY;

            return new Vector2(Mathf.Clamp(pos.x, minX, maxX), Mathf.Clamp(pos.y, minY, maxY));
        }

        private static Dictionary<string, HudElementLayoutData> CloneLayout(Dictionary<string, HudElementLayoutData> src)
        {
            var copy = new Dictionary<string, HudElementLayoutData>();
            foreach (var kv in src)
            {
                copy[kv.Key] = kv.Value.Clone();
            }
            return copy;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
        }
    }

    /// <summary>
    /// 单个 HUD 元素的 Windows 风格操作框：支持中间拖动位置 + 四边/四角拉伸调整大小（Scale 固定为 1）
    /// </summary>
    public class HudEditableFrame : MonoBehaviour
    {
        private HudLayoutEditorHUD _editor;
        private HudElementMeta _meta;
        private RectTransform _rt;
        private RectTransform _parentRT;
        private Image _fillOverlay;
        private readonly List<Image> _borderImages = new List<Image>();
        private readonly List<GameObject> _cornerDots = new List<GameObject>();

        private Vector2 _dragStartLocal;
        private Vector2 _startPos;
        private Vector2 _startSize;

        public void Initialize(HudLayoutEditorHUD editor, HudElementMeta meta, HudElementLayoutData initialData, Font font, Sprite whiteSprite)
        {
            _editor = editor;
            _meta = meta;
            _rt = GetComponent<RectTransform>();
            _parentRT = transform.parent as RectTransform;

            _rt.anchorMin = meta.anchorMin;
            _rt.anchorMax = meta.anchorMax;
            _rt.pivot = meta.pivot;
            _rt.localScale = Vector3.one;
            ApplyLayoutData(initialData);

            // 中央可拖动主体区
            _fillOverlay = gameObject.AddComponent<Image>();
            _fillOverlay.sprite = whiteSprite;
            _fillOverlay.color = new Color(0.20f, 0.72f, 1.0f, 0.14f);
            _fillOverlay.raycastTarget = true;

            var moveHandle = gameObject.AddComponent<HudEdgeDragHandle>();
            moveHandle.Bind(this, HudResizeEdge.None);

            // 四条可见边框线
            _borderImages.Add(CreateBorderLine("BorderTop", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -3f), Vector2.zero, whiteSprite));
            _borderImages.Add(CreateBorderLine("BorderBottom", new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 3f), whiteSprite));
            _borderImages.Add(CreateBorderLine("BorderLeft", new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(3f, 0f), whiteSprite));
            _borderImages.Add(CreateBorderLine("BorderRight", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-3f, 0f), Vector2.zero, whiteSprite));

            // 名称小标签
            GameObject tagGo = new GameObject("TagLabel", typeof(RectTransform), typeof(Image));
            tagGo.transform.SetParent(transform, false);
            RectTransform tagRT = tagGo.GetComponent<RectTransform>();
            tagRT.anchorMin = new Vector2(0.5f, 1f);
            tagRT.anchorMax = new Vector2(0.5f, 1f);
            tagRT.pivot = new Vector2(0.5f, 0f);
            tagRT.anchoredPosition = new Vector2(0f, 4f);
            tagRT.sizeDelta = new Vector2(138f, 26f);
            Image tagBg = tagGo.GetComponent<Image>();
            tagBg.color = new Color(0.06f, 0.10f, 0.16f, 0.88f);
            tagBg.raycastTarget = false;

            GameObject tagTxtGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            tagTxtGo.transform.SetParent(tagGo.transform, false);
            RectTransform trt = tagTxtGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            Text tagTxt = tagTxtGo.GetComponent<Text>();
            tagTxt.font = font;
            tagTxt.fontSize = 16;
            tagTxt.fontStyle = FontStyle.Bold;
            tagTxt.alignment = TextAnchor.MiddleCenter;
            tagTxt.color = Color.white;
            tagTxt.text = meta.displayName;
            tagTxt.raycastTarget = false;

            // Windows 窗口风格：4 条隐形宽边热区 + 4 个可见角点热区（触控与鼠标均易拖拽缩放）
            CreateEdgeHitZone("Edge_Left", HudResizeEdge.Left, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(-12f, 16f), new Vector2(12f, -16f));
            CreateEdgeHitZone("Edge_Right", HudResizeEdge.Right, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-12f, 16f), new Vector2(12f, -16f));
            CreateEdgeHitZone("Edge_Top", HudResizeEdge.Top, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -12f), new Vector2(-16f, 12f));
            CreateEdgeHitZone("Edge_Bottom", HudResizeEdge.Bottom, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(16f, -12f), new Vector2(-16f, 12f));

            CreateCornerHandle("Corner_TL", HudResizeEdge.TopLeft, new Vector2(0f, 1f), whiteSprite);
            CreateCornerHandle("Corner_TR", HudResizeEdge.TopRight, new Vector2(1f, 1f), whiteSprite);
            CreateCornerHandle("Corner_BL", HudResizeEdge.BottomLeft, new Vector2(0f, 0f), whiteSprite);
            CreateCornerHandle("Corner_BR", HudResizeEdge.BottomRight, new Vector2(1f, 0f), whiteSprite);
        }

        private Image CreateBorderLine(string name, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax, Sprite whiteSprite)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = oMin;
            rt.offsetMax = oMax;
            Image img = go.GetComponent<Image>();
            img.sprite = whiteSprite;
            img.color = new Color(0.35f, 0.88f, 1f, 0.85f);
            img.raycastTarget = false;
            return img;
        }

        private void CreateEdgeHitZone(string name, HudResizeEdge edge, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(HudEdgeDragHandle));
            go.transform.SetParent(transform, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = oMin;
            rt.offsetMax = oMax;
            Image img = go.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.001f); // 透明但可命中射线
            img.raycastTarget = true;
            go.GetComponent<HudEdgeDragHandle>().Bind(this, edge);
        }

        private void CreateCornerHandle(string name, HudResizeEdge edge, Vector2 anchor, Sprite whiteSprite)
        {
            GameObject hitGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(HudEdgeDragHandle));
            hitGo.transform.SetParent(transform, false);
            RectTransform rt = hitGo.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(32f, 32f);
            Image hitImg = hitGo.GetComponent<Image>();
            hitImg.color = new Color(0f, 0f, 0f, 0.001f);
            hitImg.raycastTarget = true;
            hitGo.GetComponent<HudEdgeDragHandle>().Bind(this, edge);

            // 可见白色方块把手（类似 Windows 窗口角点）
            GameObject dot = new GameObject("Dot", typeof(RectTransform), typeof(Image), typeof(Outline));
            dot.transform.SetParent(hitGo.transform, false);
            RectTransform drt = dot.GetComponent<RectTransform>();
            drt.anchorMin = new Vector2(0.5f, 0.5f);
            drt.anchorMax = new Vector2(0.5f, 0.5f);
            drt.pivot = new Vector2(0.5f, 0.5f);
            drt.sizeDelta = new Vector2(14f, 14f);
            Image dimg = dot.GetComponent<Image>();
            dimg.sprite = whiteSprite;
            dimg.color = Color.white;
            dimg.raycastTarget = false;
            Outline outline = dot.GetComponent<Outline>();
            outline.effectColor = new Color(0.05f, 0.35f, 0.65f, 1f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            _cornerDots.Add(dot);
        }

        public void ApplyLayoutData(HudElementLayoutData data)
        {
            if (_rt == null || data == null) return;
            _rt.localScale = Vector3.one;
            _rt.anchoredPosition = new Vector2(data.posX, data.posY);
            _rt.sizeDelta = new Vector2(data.width, data.height);
        }

        public void SetSelected(bool selected)
        {
            if (_fillOverlay != null)
            {
                _fillOverlay.color = selected
                    ? new Color(0.22f, 0.78f, 1.0f, 0.24f)
                    : new Color(0.22f, 0.78f, 1.0f, 0.08f);
            }
            Color borderCol = selected
                ? new Color(0.32f, 0.94f, 1.0f, 1.0f)
                : new Color(0.65f, 0.80f, 0.92f, 0.55f);
            for (int i = 0; i < _borderImages.Count; i++)
            {
                if (_borderImages[i] != null) _borderImages[i].color = borderCol;
            }
            for (int i = 0; i < _cornerDots.Count; i++)
            {
                if (_cornerDots[i] != null) _cornerDots[i].SetActive(selected);
            }
            if (selected) transform.SetAsLastSibling();
        }

        public void BeginInteraction(PointerEventData eventData)
        {
            _editor?.SelectElement(_meta.id);
            _startPos = _rt.anchoredPosition;
            _startSize = _rt.sizeDelta;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_parentRT, eventData.position, eventData.pressEventCamera, out _dragStartLocal);
        }

        public void ContinueInteraction(HudResizeEdge edge, PointerEventData eventData)
        {
            if (_parentRT == null || _editor == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_parentRT, eventData.position, eventData.pressEventCamera, out Vector2 curLocal))
            {
                return;
            }

            Vector2 delta = curLocal - _dragStartLocal;
            Vector2 newPos = _startPos;
            Vector2 newSize = _startSize;

            if (edge == HudResizeEdge.None)
            {
                // 拖动中心移动位置
                newPos = _editor.ClampPositionInCanvas(_meta, _startPos + delta, _startSize);
            }
            else
            {
                // 类似 Windows 窗口边缘/角点拖动缩放（严格只改 sizeDelta 与 anchoredPosition，不改 localScale）
                float dx = 0f;
                float dy = 0f;
                if ((edge & HudResizeEdge.Right) != 0) dx = delta.x;
                else if ((edge & HudResizeEdge.Left) != 0) dx = -delta.x;

                if ((edge & HudResizeEdge.Top) != 0) dy = delta.y;
                else if ((edge & HudResizeEdge.Bottom) != 0) dy = -delta.y;

                if (_meta.lockSquareAspect)
                {
                    float chosenDelta;
                    bool hasX = (edge & (HudResizeEdge.Left | HudResizeEdge.Right)) != 0;
                    bool hasY = (edge & (HudResizeEdge.Top | HudResizeEdge.Bottom)) != 0;
                    if (hasX && hasY) chosenDelta = Mathf.Abs(dx) >= Mathf.Abs(dy) ? dx : dy;
                    else chosenDelta = hasX ? dx : dy;

                    float targetDim = Mathf.Clamp(_startSize.x + chosenDelta, _meta.minSize.x, _meta.maxSize.x);
                    float actualDiff = targetDim - _startSize.x;
                    newSize = new Vector2(targetDim, targetDim);

                    // 按对侧边缘锚定补偿位移（与 Windows 窗口拉伸手感一致）
                    float shiftX = 0f;
                    float shiftY = 0f;
                    if ((edge & HudResizeEdge.Right) != 0) shiftX = actualDiff * _meta.pivot.x;
                    else if ((edge & HudResizeEdge.Left) != 0) shiftX = -actualDiff * (1f - _meta.pivot.x);

                    if ((edge & HudResizeEdge.Top) != 0) shiftY = actualDiff * _meta.pivot.y;
                    else if ((edge & HudResizeEdge.Bottom) != 0) shiftY = -actualDiff * (1f - _meta.pivot.y);

                    newPos = _editor.ClampPositionInCanvas(_meta, _startPos + new Vector2(shiftX, shiftY), newSize);
                }
                else
                {
                    float targetW = Mathf.Clamp(_startSize.x + dx, _meta.minSize.x, _meta.maxSize.x);
                    float targetH = Mathf.Clamp(_startSize.y + dy, _meta.minSize.y, _meta.maxSize.y);
                    float diffW = targetW - _startSize.x;
                    float diffH = targetH - _startSize.y;
                    newSize = new Vector2(targetW, targetH);

                    float shiftX = 0f;
                    float shiftY = 0f;
                    if ((edge & HudResizeEdge.Right) != 0) shiftX = diffW * _meta.pivot.x;
                    else if ((edge & HudResizeEdge.Left) != 0) shiftX = -diffW * (1f - _meta.pivot.x);

                    if ((edge & HudResizeEdge.Top) != 0) shiftY = diffH * _meta.pivot.y;
                    else if ((edge & HudResizeEdge.Bottom) != 0) shiftY = -diffH * (1f - _meta.pivot.y);

                    newPos = _editor.ClampPositionInCanvas(_meta, _startPos + new Vector2(shiftX, shiftY), newSize);
                }
            }

            _rt.localScale = Vector3.one;
            _rt.anchoredPosition = newPos;
            _rt.sizeDelta = newSize;
            _editor.OnFrameTransformChanged(_meta.id, newPos, newSize);
        }
    }

    public class HudEdgeDragHandle : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler
    {
        private HudEditableFrame _frame;
        private HudResizeEdge _edge;

        public void Bind(HudEditableFrame frame, HudResizeEdge edge)
        {
            _frame = frame;
            _edge = edge;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _frame?.BeginInteraction(eventData);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _frame?.BeginInteraction(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            _frame?.ContinueInteraction(_edge, eventData);
        }
    }
}
