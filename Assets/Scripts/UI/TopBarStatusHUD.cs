using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace PixelSaur.UI
{
    /// <summary>
    /// 游戏左上角状态信息条（玩家数量、像素设置齿轮、实时 FPS 监控）：
    /// 采用参考图的胶囊圆角面板风格，使用 32x32 纯手搓像素图标，支持动态帧率监控与人数更新。
    /// </summary>
    [ExecuteAlways]
    public class TopBarStatusHUD : MonoBehaviour
    {
        public static TopBarStatusHUD Instance { get; private set; }

        [Header("32x32 手搓像素风格图标资产")]
        [SerializeField] private Sprite playersIconSprite;
        [SerializeField] private Sprite gearIconSprite;
        [SerializeField] private Sprite fpsBadgeSprite;
        [SerializeField] private Sprite panelBgSprite;

        [Header("数据与更新配置 (可自由微调)")]
        [Tooltip("显示的玩家数量（默认50，可通过代码或Inspector微调）")]
        [SerializeField] private int playerCount = 50;

        [Tooltip("FPS 刷新时间间隔（秒），避免过频刷新晃眼，默认 0.25s")]
        [SerializeField] private float fpsUpdateInterval = 0.25f;

        [Tooltip("是否根据帧率高低为 FPS 数字着色（高于55绿色，高于30黄色，低于30红色）")]
        [SerializeField] private bool colorizeFPS = false;

        [Header("面板尺寸与位置")]
        [SerializeField] private Vector2 panelPosition = new Vector2(20f, -16f);
        [SerializeField] private Vector2 panelSize = new Vector2(230f, 44f);

        [Header("事件")]
        public UnityEvent onSettingsClicked = new UnityEvent();

        [Header("内部组件引用")]
        [SerializeField] private RectTransform panelRect;
        [SerializeField] private Image bgImage;
        [SerializeField] private Image playersIcon;
        [SerializeField] private Text playersCountText;
        [SerializeField] private Button gearButton;
        [SerializeField] private Image gearIcon;
        [SerializeField] private Image fpsBadgeIcon;
        [SerializeField] private Text fpsValueText;

        // FPS 统计变量
        private float _fpsAccumTime = 0f;
        private int _fpsAccumFrames = 0;
        private int _currentFPS = 60;

        public int PlayerCount => playerCount;
        public int CurrentFPS => _currentFPS;

        private void Awake()
        {
            if (Application.isPlaying)
            {
                if (Instance == null) Instance = this;
                else if (Instance != this) { Destroy(gameObject); return; }
            }

            LoadDefaultSprites();
            EnsureUIHierarchy();
        }

        private void OnEnable()
        {
            LoadDefaultSprites();
            EnsureUIHierarchy();
            UpdatePlayerCountDisplay();
            if (!Application.isPlaying && fpsValueText != null)
            {
                fpsValueText.text = "120"; // 编辑模式下显示参考值 120
            }
        }

        private void Start()
        {
            if (gearButton != null)
            {
                gearButton.onClick.RemoveListener(HandleGearClick);
                gearButton.onClick.AddListener(HandleGearClick);
            }
        }

        private void Update()
        {
            if (Application.isPlaying)
            {
                UpdateFPS();
            }
        }

        /// <summary>
        /// 外部公共接口：动态更新玩家数量
        /// </summary>
        public void SetPlayerCount(int count)
        {
            playerCount = Mathf.Max(0, count);
            UpdatePlayerCountDisplay();
        }

        private void HandleGearClick()
        {
            Debug.Log("<color=#66CCFF>[TopBarStatusHUD] 点击了像素齿轮设置按钮！</color>");
            onSettingsClicked?.Invoke();
        }

        private void UpdatePlayerCountDisplay()
        {
            if (playersCountText != null)
            {
                playersCountText.text = playerCount.ToString();
            }
        }

        private void UpdateFPS()
        {
            _fpsAccumTime += Time.unscaledDeltaTime;
            _fpsAccumFrames++;

            if (_fpsAccumTime >= fpsUpdateInterval)
            {
                _currentFPS = Mathf.RoundToInt(_fpsAccumFrames / _fpsAccumTime);
                _fpsAccumTime = 0f;
                _fpsAccumFrames = 0;

                if (fpsValueText != null)
                {
                    fpsValueText.text = _currentFPS.ToString();

                    if (colorizeFPS)
                    {
                        if (_currentFPS >= 55) fpsValueText.color = new Color(0.3f, 0.95f, 0.4f);
                        else if (_currentFPS >= 30) fpsValueText.color = new Color(1.0f, 0.85f, 0.2f);
                        else fpsValueText.color = new Color(1.0f, 0.35f, 0.35f);
                    }
                    else
                    {
                        fpsValueText.color = Color.white;
                    }
                }
            }
        }

        /// <summary>
        /// 加载 32x32 像素贴图资源
        /// </summary>
        private void LoadDefaultSprites()
        {
#if UNITY_EDITOR
            if (playersIconSprite == null)
                playersIconSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_players_32x32.png");
            if (gearIconSprite == null)
                gearIconSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_gear_32x32.png");
            if (fpsBadgeSprite == null)
                fpsBadgeSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_fps_badge_32x32.png");
            if (panelBgSprite == null)
                panelBgSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/hud_pill_bg_64x64.png");
#endif
        }

        /// <summary>
        /// 确保 UI 节点层级完好，复用现有控件（防重复创建、防出厂重置）
        /// </summary>
        public void EnsureUIHierarchy()
        {
            panelRect = GetComponent<RectTransform>();
            if (panelRect == null) panelRect = gameObject.AddComponent<RectTransform>();

            // 1. 设置左上角锚点对齐
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = panelPosition;
            panelRect.sizeDelta = panelSize;

            // 2. 胶囊形状深色背景底板
            bgImage = GetComponent<Image>();
            if (bgImage == null) bgImage = gameObject.AddComponent<Image>();
            if (panelBgSprite != null)
            {
                bgImage.sprite = panelBgSprite;
                bgImage.type = Image.Type.Sliced;
            }
            bgImage.color = new Color(0.12f, 0.13f, 0.16f, 0.90f);
            bgImage.raycastTarget = false;

            Font mainFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (mainFont == null) mainFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // 3. 左侧：玩家图标与数量
            Transform playersGroupTr = transform.Find("PlayersGroup");
            if (playersGroupTr == null)
            {
                var pGroupObj = new GameObject("PlayersGroup", typeof(RectTransform));
                pGroupObj.transform.SetParent(transform, false);
                playersGroupTr = pGroupObj.transform;
            }
            RectTransform playersGroupRT = playersGroupTr.GetComponent<RectTransform>();
            playersGroupRT.anchorMin = new Vector2(0f, 0.5f);
            playersGroupRT.anchorMax = new Vector2(0f, 0.5f);
            playersGroupRT.pivot = new Vector2(0f, 0.5f);
            playersGroupRT.anchoredPosition = new Vector2(10f, 0f);
            playersGroupRT.sizeDelta = new Vector2(68f, 36f);

            // 玩家图标
            Transform pIconTr = playersGroupTr.Find("Icon");
            if (pIconTr == null)
            {
                var pIconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                pIconObj.transform.SetParent(playersGroupTr, false);
                pIconTr = pIconObj.transform;
            }
            RectTransform pIconRT = pIconTr.GetComponent<RectTransform>();
            pIconRT.anchorMin = new Vector2(0f, 0.5f);
            pIconRT.anchorMax = new Vector2(0f, 0.5f);
            pIconRT.pivot = new Vector2(0f, 0.5f);
            pIconRT.anchoredPosition = new Vector2(0f, 0f);
            pIconRT.sizeDelta = new Vector2(28f, 28f);
            playersIcon = pIconTr.GetComponent<Image>();
            playersIcon.sprite = playersIconSprite;
            playersIcon.raycastTarget = false;

            // 玩家数量文本
            Transform pTxtTr = playersGroupTr.Find("CountText");
            if (pTxtTr == null)
            {
                var pTxtObj = new GameObject("CountText", typeof(RectTransform), typeof(Text), typeof(Outline));
                pTxtObj.transform.SetParent(playersGroupTr, false);
                pTxtTr = pTxtObj.transform;
            }
            RectTransform pTxtRT = pTxtTr.GetComponent<RectTransform>();
            pTxtRT.anchorMin = new Vector2(0f, 0.5f);
            pTxtRT.anchorMax = new Vector2(1f, 0.5f);
            pTxtRT.pivot = new Vector2(0f, 0.5f);
            pTxtRT.anchoredPosition = new Vector2(30f, 0f);
            pTxtRT.sizeDelta = new Vector2(38f, 34f);

            playersCountText = pTxtTr.GetComponent<Text>();
            playersCountText.font = mainFont;
            playersCountText.fontSize = 20;
            playersCountText.fontStyle = FontStyle.Bold;
            playersCountText.alignment = TextAnchor.MiddleLeft;
            playersCountText.color = Color.white;
            playersCountText.text = playerCount.ToString();
            playersCountText.raycastTarget = false;

            Outline pOutline = pTxtTr.GetComponent<Outline>();
            pOutline.effectColor = new Color(0f, 0f, 0f, 0.90f);
            pOutline.effectDistance = new Vector2(1.5f, -1.5f);

            // 4. 中间：像素齿轮（可点击）
            Transform gearTr = transform.Find("GearButton");
            if (gearTr == null)
            {
                var gearObj = new GameObject("GearButton", typeof(RectTransform), typeof(Image), typeof(Button));
                gearObj.transform.SetParent(transform, false);
                gearTr = gearObj.transform;
            }
            RectTransform gearRT = gearTr.GetComponent<RectTransform>();
            gearRT.anchorMin = new Vector2(0f, 0.5f);
            gearRT.anchorMax = new Vector2(0f, 0.5f);
            gearRT.pivot = new Vector2(0.5f, 0.5f);
            gearRT.anchoredPosition = new Vector2(94f, 0f);
            gearRT.sizeDelta = new Vector2(24f, 24f);

            gearIcon = gearTr.GetComponent<Image>();
            gearIcon.sprite = gearIconSprite;
            gearIcon.raycastTarget = true;

            gearButton = gearTr.GetComponent<Button>();
            gearButton.targetGraphic = gearIcon;

            // 5. 中间分割线 (参考图风格细竖线)
            Transform divTr = transform.Find("Divider");
            if (divTr == null)
            {
                var divObj = new GameObject("Divider", typeof(RectTransform), typeof(Image));
                divObj.transform.SetParent(transform, false);
                divTr = divObj.transform;
            }
            RectTransform divRT = divTr.GetComponent<RectTransform>();
            divRT.anchorMin = new Vector2(0f, 0.5f);
            divRT.anchorMax = new Vector2(0f, 0.5f);
            divRT.pivot = new Vector2(0.5f, 0.5f);
            divRT.anchoredPosition = new Vector2(113f, 0f);
            divRT.sizeDelta = new Vector2(1.5f, 20f);
            Image divImg = divTr.GetComponent<Image>();
            divImg.color = new Color(0.55f, 0.60f, 0.68f, 0.6f);
            divImg.raycastTarget = false;

            // 6. 右侧：FPS 绿色徽章与帧率数值
            Transform fpsGroupTr = transform.Find("FPSGroup");
            if (fpsGroupTr == null)
            {
                var fpsGroupObj = new GameObject("FPSGroup", typeof(RectTransform));
                fpsGroupObj.transform.SetParent(transform, false);
                fpsGroupTr = fpsGroupObj.transform;
            }
            RectTransform fpsGroupRT = fpsGroupTr.GetComponent<RectTransform>();
            fpsGroupRT.anchorMin = new Vector2(0f, 0.5f);
            fpsGroupRT.anchorMax = new Vector2(0f, 0.5f);
            fpsGroupRT.pivot = new Vector2(0f, 0.5f);
            fpsGroupRT.anchoredPosition = new Vector2(122f, 0f);
            fpsGroupRT.sizeDelta = new Vector2(98f, 36f);

            // FPS 徽章图标
            Transform fBadgeTr = fpsGroupTr.Find("BadgeIcon");
            if (fBadgeTr == null)
            {
                var fBadgeObj = new GameObject("BadgeIcon", typeof(RectTransform), typeof(Image));
                fBadgeObj.transform.SetParent(fpsGroupTr, false);
                fBadgeTr = fBadgeObj.transform;
            }
            RectTransform fBadgeRT = fBadgeTr.GetComponent<RectTransform>();
            fBadgeRT.anchorMin = new Vector2(0f, 0.5f);
            fBadgeRT.anchorMax = new Vector2(0f, 0.5f);
            fBadgeRT.pivot = new Vector2(0f, 0.5f);
            fBadgeRT.anchoredPosition = new Vector2(0f, 0f);
            fBadgeRT.sizeDelta = new Vector2(34f, 22f);
            fpsBadgeIcon = fBadgeTr.GetComponent<Image>();
            fpsBadgeIcon.sprite = fpsBadgeSprite;
            fpsBadgeIcon.raycastTarget = false;

            // FPS 数值文本
            Transform fValTr = fpsGroupTr.Find("FPSValueText");
            if (fValTr == null)
            {
                var fValObj = new GameObject("FPSValueText", typeof(RectTransform), typeof(Text), typeof(Outline));
                fValObj.transform.SetParent(fpsGroupTr, false);
                fValTr = fValObj.transform;
            }
            RectTransform fValRT = fValTr.GetComponent<RectTransform>();
            fValRT.anchorMin = new Vector2(0f, 0.5f);
            fValRT.anchorMax = new Vector2(1f, 0.5f);
            fValRT.pivot = new Vector2(0f, 0.5f);
            fValRT.anchoredPosition = new Vector2(38f, 0f);
            fValRT.sizeDelta = new Vector2(58f, 34f);

            fpsValueText = fValTr.GetComponent<Text>();
            fpsValueText.font = mainFont;
            fpsValueText.fontSize = 20;
            fpsValueText.fontStyle = FontStyle.Bold;
            fpsValueText.alignment = TextAnchor.MiddleLeft;
            fpsValueText.color = Color.white;
            fpsValueText.text = Application.isPlaying ? _currentFPS.ToString() : "120";
            fpsValueText.raycastTarget = false;

            Outline fOutline = fValTr.GetComponent<Outline>();
            fOutline.effectColor = new Color(0f, 0f, 0f, 0.90f);
            fOutline.effectDistance = new Vector2(1.5f, -1.5f);
        }
    }
}
