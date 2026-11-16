using UnityEngine;
using UnityEngine.UI;

namespace PixelSaur.UI
{
    /// <summary>
    /// 真实可互动的岛屿公告与反馈面板：
    /// 1. 展示最新游戏更新公告与反馈指南；
    /// 2. 提供可点击跳转/复制的 GitHub 开源仓库地址（https://github.com/LinHouYu/PixelSaur）；
    /// 3. 提供可点击跳转的 QQ 反馈群链接（https://qm.qq.com/q/COlQsIb0hU）、群号复制（325318218）及 QQ 群卡片二维码展示。
    /// </summary>
    public class NoticePanelHUD : MonoBehaviour
    {
        public const string GithubRepoUrl = "https://github.com/LinHouYu/PixelSaur";
        public const string QQGroupUrl = "https://qm.qq.com/q/COlQsIb0hU";
        public const string QQGroupNumber = "325318218";
        public const string QQGroupName = "哲学♂与智慧の肯定✓";

        [Header("资产引用")]
        [SerializeField] private Sprite whitePillSprite;
        [SerializeField] private Sprite buttonPillSprite;
        [SerializeField] private Sprite qqCardSprite;

        private GameObject _toastObj;
        private Text _toastText;
        private float _toastHideTime;
        private bool _built;

        private void Awake()
        {
            LoadSprites();
            EnsureUI();
        }

        private void OnEnable()
        {
            LoadSprites();
            EnsureUI();
        }

        private void Update()
        {
            if (_toastObj != null && _toastObj.activeSelf && Time.unscaledTime >= _toastHideTime)
            {
                _toastObj.SetActive(false);
            }
        }

        private void LoadSprites()
        {
#if UNITY_EDITOR
            if (whitePillSprite == null)
            {
                whitePillSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/ui_white_pill_64x64.png");
            }
            if (buttonPillSprite == null)
            {
                buttonPillSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/ui_bevel_pill_64x64.png");
            }
            if (qqCardSprite == null)
            {
                qqCardSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/qq_group_card.jpg");
            }
#endif
        }

        public void OpenPanel()
        {
            if (UIPanelManager.Instance != null)
            {
                UIPanelManager.Instance.OpenPanel("NoticePanel");
            }
            else
            {
                gameObject.SetActive(true);
                transform.SetAsLastSibling();
            }
        }

        public void EnsureUI()
        {
            if (_built) return;
            _built = true;

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }

            RectTransform rt = GetComponent<RectTransform>();
            if (rt == null) rt = gameObject.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.08f, 0.08f);
            rt.anchorMax = new Vector2(0.92f, 0.92f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);

            Image bg = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            bg.sprite = whitePillSprite;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.07f, 0.10f, 0.15f, 0.99f);
            bg.raycastTarget = true;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 1. 顶部标题与关闭 X
            CreateText(transform, "Title", font, "岛屿公告 & 问题反馈", 36, FontStyle.Bold, Color.white,
                TextAnchor.MiddleLeft, new Vector2(0f, 0.87f), new Vector2(0.75f, 1f), new Vector2(38f, 0f), new Vector2(0f, -6f));

            Button closeBtn = CreateStyledButton(transform, "NoticeCloseBtn", font, "✕", 32, new Color(0.46f, 0.18f, 0.20f, 1f),
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -20f), new Vector2(68f, 68f));
            closeBtn.onClick.AddListener(() =>
            {
                if (UIPanelManager.Instance != null) UIPanelManager.Instance.ClosePanel("NoticePanel");
                else { UISoundManager.PlayClose(); gameObject.SetActive(false); }
            });

            // 2. 左侧区域：公告内容 + GitHub & QQ 互动按钮
            GameObject leftPanel = new GameObject("LeftInfoColumn", typeof(RectTransform), typeof(Image));
            leftPanel.transform.SetParent(transform, false);
            RectTransform leftRT = leftPanel.GetComponent<RectTransform>();
            leftRT.anchorMin = new Vector2(0.03f, 0.06f);
            leftRT.anchorMax = new Vector2(0.61f, 0.84f);
            leftRT.offsetMin = Vector2.zero;
            leftRT.offsetMax = Vector2.zero;
            Image leftBg = leftPanel.GetComponent<Image>();
            leftBg.sprite = whitePillSprite;
            leftBg.type = Image.Type.Sliced;
            leftBg.color = new Color(0.11f, 0.15f, 0.22f, 0.96f);

            string bodyContent =
                "欢迎来到 PixelSaur（像素侏罗纪 3D 生存岛屿）！\n\n" +
                "【最新特性与功能说明】\n" +
                "• 3D 霸王龙模型 & 11 套动作：支持啃咬、跳跃、潜行、狂暴冲刺、吼叫、长按饮水/进食。\n" +
                "• 大厅实时预览设置：在开始界面点开右上角齿轮调整画质/草地/分辨率/音量，背景岛屿同步预览，进入游戏无缝继承！\n" +
                "• 每日/主线任务中心：完成岛屿行为可累计进度并领取金币与琥珀。\n\n" +
                $"【问题反馈与源码仓库】\n" +
                $"• GitHub 仓库：{GithubRepoUrl}\n" +
                $"• QQ 交流反馈群：【{QQGroupName}】（群号：{QQGroupNumber}）\n" +
                "• 遇到画面、适配或操作 BUG，欢迎点击下方按钮直接前往反馈！";

            CreateText(leftPanel.transform, "NoticeBody", font, bodyContent, 21, FontStyle.Bold, new Color(0.84f, 0.90f, 0.96f, 1f),
                TextAnchor.UpperLeft, new Vector2(0f, 0.30f), new Vector2(1f, 1f), new Vector2(26f, 10f), new Vector2(-26f, -22f));

            // GitHub 按钮行
            Button openGithubBtn = CreateStyledButton(leftPanel.transform, "OpenGithubButton", font, "打开 GitHub 仓库", 23,
                new Color(0.20f, 0.28f, 0.38f, 1f),
                new Vector2(0.03f, 0.16f), new Vector2(0.58f, 0.27f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchInAnchors(openGithubBtn.GetComponent<RectTransform>());
            openGithubBtn.onClick.AddListener(() =>
            {
                UISoundManager.PlayClick();
                Application.OpenURL(GithubRepoUrl);
                ShowToast("✓ 正在打开 GitHub 仓库页面...");
            });

            Button copyGithubBtn = CreateStyledButton(leftPanel.transform, "CopyGithubButton", font, "复制仓库链接", 22,
                new Color(0.18f, 0.42f, 0.62f, 1f),
                new Vector2(0.61f, 0.16f), new Vector2(0.97f, 0.27f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchInAnchors(copyGithubBtn.GetComponent<RectTransform>());
            copyGithubBtn.onClick.AddListener(() =>
            {
                UISoundManager.PlaySave();
                GUIUtility.systemCopyBuffer = GithubRepoUrl;
                ShowToast("✓ 已复制 GitHub 仓库链接！");
            });

            // QQ 群按钮行
            Button joinQQBtn = CreateStyledButton(leftPanel.transform, "JoinQQButton", font, "点击加入 QQ 反馈群", 23,
                new Color(0.16f, 0.64f, 0.36f, 1f),
                new Vector2(0.03f, 0.03f), new Vector2(0.58f, 0.14f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchInAnchors(joinQQBtn.GetComponent<RectTransform>());
            joinQQBtn.onClick.AddListener(() =>
            {
                UISoundManager.PlaySave();
                Application.OpenURL(QQGroupUrl);
                ShowToast("✓ 正在拉起 QQ 加入群聊反馈群...");
            });

            Button copyQQBtn = CreateStyledButton(leftPanel.transform, "CopyQQButton", font, $"复制群号 {QQGroupNumber}", 21,
                new Color(0.22f, 0.50f, 0.78f, 1f),
                new Vector2(0.61f, 0.03f), new Vector2(0.97f, 0.14f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchInAnchors(copyQQBtn.GetComponent<RectTransform>());
            copyQQBtn.onClick.AddListener(() =>
            {
                UISoundManager.PlaySave();
                GUIUtility.systemCopyBuffer = QQGroupNumber;
                ShowToast($"✓ 已复制 QQ 群号 {QQGroupNumber}！");
            });

            // 3. 右侧区域：QQ 群二维码卡片展示区（可点击跳转）
            GameObject rightPanel = new GameObject("RightQQCardColumn", typeof(RectTransform), typeof(Image), typeof(Button));
            rightPanel.transform.SetParent(transform, false);
            RectTransform rightRT = rightPanel.GetComponent<RectTransform>();
            rightRT.anchorMin = new Vector2(0.63f, 0.06f);
            rightRT.anchorMax = new Vector2(0.97f, 0.84f);
            rightRT.offsetMin = Vector2.zero;
            rightRT.offsetMax = Vector2.zero;
            Image rightBg = rightPanel.GetComponent<Image>();
            rightBg.sprite = whitePillSprite;
            rightBg.type = Image.Type.Sliced;
            rightBg.color = new Color(0.11f, 0.15f, 0.22f, 0.96f);

            Button qrBtn = rightPanel.GetComponent<Button>();
            qrBtn.onClick.AddListener(() =>
            {
                UISoundManager.PlaySave();
                Application.OpenURL(QQGroupUrl);
                ShowToast("✓ 正在拉起 QQ 加入群聊反馈群...");
            });

            GameObject qrGo = new GameObject("QQCardImage", typeof(RectTransform), typeof(Image));
            qrGo.transform.SetParent(rightPanel.transform, false);
            RectTransform qrRT = qrGo.GetComponent<RectTransform>();
            qrRT.anchorMin = new Vector2(0.06f, 0.12f);
            qrRT.anchorMax = new Vector2(0.94f, 0.95f);
            qrRT.offsetMin = Vector2.zero;
            qrRT.offsetMax = Vector2.zero;
            Image qrImg = qrGo.GetComponent<Image>();
            qrImg.sprite = qqCardSprite;
            qrImg.preserveAspect = true;
            qrImg.raycastTarget = false;

            CreateText(rightPanel.transform, "QRHint", font, "点击名片或扫码加入反馈 BUG", 20, FontStyle.Bold,
                new Color(0.68f, 0.86f, 0.98f, 1f), TextAnchor.MiddleCenter,
                new Vector2(0f, 0.01f), new Vector2(1f, 0.12f), Vector2.zero, Vector2.zero);

            // 4. 底部操作反馈 Toast
            _toastObj = new GameObject("NoticeToast", typeof(RectTransform), typeof(Image));
            _toastObj.transform.SetParent(transform, false);
            RectTransform toastRT = _toastObj.GetComponent<RectTransform>();
            toastRT.anchorMin = new Vector2(0.5f, 0f);
            toastRT.anchorMax = new Vector2(0.5f, 0f);
            toastRT.pivot = new Vector2(0.5f, 0f);
            toastRT.anchoredPosition = new Vector2(0f, 18f);
            toastRT.sizeDelta = new Vector2(520f, 54f);
            Image toastImg = _toastObj.GetComponent<Image>();
            toastImg.sprite = whitePillSprite;
            toastImg.type = Image.Type.Sliced;
            toastImg.color = new Color(0.16f, 0.68f, 0.36f, 0.98f);
            _toastText = CreateText(_toastObj.transform, "Text", font, "", 22, FontStyle.Bold, Color.white,
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _toastObj.SetActive(false);
        }

        private void ShowToast(string msg)
        {
            if (_toastObj == null || _toastText == null) return;
            _toastText.text = msg;
            _toastObj.SetActive(true);
            _toastObj.transform.SetAsLastSibling();
            _toastHideTime = Time.unscaledTime + 2.0f;
        }

        private static void StretchInAnchors(RectTransform rt)
        {
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
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
            img.sprite = buttonPillSprite != null ? buttonPillSprite : whitePillSprite;
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
