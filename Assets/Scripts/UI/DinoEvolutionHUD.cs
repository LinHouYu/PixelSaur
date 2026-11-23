using UnityEngine;
using UnityEngine.UI;
using PixelSaur.Player;

namespace PixelSaur.UI
{
    /// <summary>
    /// 恐龙进化主界面 HUD（直接显示在 Game 场景主界面右上角，不放入设置菜单）：
    /// 实时展示当前进化阶段名称（按白/绿/蓝/紫对应颜色渲染）、体型倍率与成长值进度条/数值（满级显示 MAX）。
    /// </summary>
    [ExecuteAlways]
    public class DinoEvolutionHUD : MonoBehaviour
    {
        public static DinoEvolutionHUD Instance { get; private set; }

        [Header("面板位置与尺寸（默认右上角，避免与其他 HUD 重叠）")]
        [SerializeField] private Vector2 panelPosition = new Vector2(-24f, -20f);
        [SerializeField] private Vector2 panelSize = new Vector2(336f, 84f);

        [Header("底板资产")]
        [SerializeField] private Sprite panelBgSprite;

        [Header("UI 控件引用")]
        [SerializeField] private Text stageNameText;
        [SerializeField] private Text scaleBadgeText;
        [SerializeField] private Slider expSlider;
        [SerializeField] private Image expFillImage;
        [SerializeField] private Text expValueText;

        private DinoEvolutionController _boundController;
        private static Sprite _whiteSprite;

        private void Awake()
        {
            if (Application.isPlaying) Instance = this;
            LoadDefaultSprites();
            EnsureUIHierarchy();
        }

        private void OnEnable()
        {
            if (Application.isPlaying) Instance = this;
            LoadDefaultSprites();
            EnsureUIHierarchy();
            TryBindController();
            RefreshView();
        }

        private void OnDisable()
        {
            UnbindController();
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            if (_boundController == null)
            {
                TryBindController();
                if (_boundController != null) RefreshView();
            }
        }

        private void TryBindController()
        {
            if (_boundController != null) return;
            _boundController = DinoEvolutionController.Instance != null
                ? DinoEvolutionController.Instance
                : FindAnyObjectByType<DinoEvolutionController>();

            if (_boundController != null)
            {
                _boundController.OnEvolutionStateChanged -= RefreshView;
                _boundController.OnEvolutionStateChanged += RefreshView;
            }
        }

        private void UnbindController()
        {
            if (_boundController != null)
            {
                _boundController.OnEvolutionStateChanged -= RefreshView;
                _boundController = null;
            }
        }

        private void LoadDefaultSprites()
        {
#if UNITY_EDITOR
            if (panelBgSprite == null)
            {
                panelBgSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/hud_pill_bg_64x64.png");
            }
#endif
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
        }

        public void EnsureUIHierarchy()
        {
            RectTransform rt = GetComponent<RectTransform>();
            if (rt == null) rt = gameObject.AddComponent<RectTransform>();

            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.localScale = Vector3.one;
            rt.anchoredPosition = panelPosition;
            rt.sizeDelta = panelSize;

            Image bg = GetComponent<Image>();
            if (bg == null) bg = gameObject.AddComponent<Image>();
            if (panelBgSprite != null)
            {
                bg.sprite = panelBgSprite;
                bg.type = Image.Type.Sliced;
            }
            bg.color = new Color(0.10f, 0.12f, 0.16f, 0.90f);
            bg.raycastTarget = false;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // 1. 阶段名称文本（左上）
            stageNameText = EnsureText("StageNameText", transform, font, 24, TextAnchor.MiddleLeft,
                new Vector2(0f, 0.50f), new Vector2(0.68f, 1f), new Vector2(16f, 0f), new Vector2(0f, -6f), "幼年期");

            // 2. 体型倍率标签（右上）
            scaleBadgeText = EnsureText("ScaleBadgeText", transform, font, 20, TextAnchor.MiddleRight,
                new Vector2(0.66f, 0.50f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(-16f, -6f), "1.0x");

            // 3. 成长值进度条 Slider（下半区）
            Transform sliderTr = transform.Find("ExpSlider");
            GameObject sliderGo = sliderTr != null ? sliderTr.gameObject : new GameObject("ExpSlider", typeof(RectTransform), typeof(Image), typeof(Slider));
            sliderGo.transform.SetParent(transform, false);
            RectTransform sliderRT = sliderGo.GetComponent<RectTransform>();
            sliderRT.anchorMin = new Vector2(0f, 0f);
            sliderRT.anchorMax = new Vector2(1f, 0.48f);
            sliderRT.offsetMin = new Vector2(14f, 10f);
            sliderRT.offsetMax = new Vector2(-14f, -4f);
            sliderRT.localScale = Vector3.one;

            Image sliderBg = sliderGo.GetComponent<Image>();
            sliderBg.sprite = _whiteSprite;
            sliderBg.color = new Color(0.06f, 0.08f, 0.11f, 0.95f);
            sliderBg.raycastTarget = false;

            Transform fillAreaTr = sliderGo.transform.Find("Fill Area");
            GameObject fillAreaGo = fillAreaTr != null ? fillAreaTr.gameObject : new GameObject("Fill Area", typeof(RectTransform));
            fillAreaGo.transform.SetParent(sliderGo.transform, false);
            RectTransform fillAreaRT = fillAreaGo.GetComponent<RectTransform>();
            fillAreaRT.anchorMin = Vector2.zero;
            fillAreaRT.anchorMax = Vector2.one;
            fillAreaRT.offsetMin = new Vector2(2f, 2f);
            fillAreaRT.offsetMax = new Vector2(-2f, -2f);

            Transform fillTr = fillAreaGo.transform.Find("Fill");
            GameObject fillGo = fillTr != null ? fillTr.gameObject : new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(fillAreaGo.transform, false);
            RectTransform fillRT = fillGo.GetComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;

            expFillImage = fillGo.GetComponent<Image>();
            expFillImage.sprite = _whiteSprite;
            expFillImage.color = Color.white;
            expFillImage.raycastTarget = false;

            expSlider = sliderGo.GetComponent<Slider>();
            expSlider.interactable = false;
            expSlider.transition = Selectable.Transition.None;
            expSlider.fillRect = fillRT;
            expSlider.minValue = 0f;
            expSlider.maxValue = 1f;
            expSlider.value = 0f;

            // 4. 进度条居中数值文本（如 25 / 50 或 MAX）
            expValueText = EnsureText("ExpValueText", sliderGo.transform, font, 17, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, "0 / 50");
        }

        private static Text EnsureText(string name, Transform parent, Font font, int fontSize, TextAnchor align,
            Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax, string defaultText)
        {
            Transform tr = parent.Find(name);
            GameObject go = tr != null ? tr.gameObject : new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = oMin;
            rt.offsetMax = oMax;
            rt.localScale = Vector3.one;

            Text txt = go.GetComponent<Text>();
            txt.font = font;
            txt.fontSize = fontSize;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = align;
            txt.color = Color.white;
            txt.text = defaultText;
            txt.raycastTarget = false;

            Outline outline = go.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = new Color(0f, 0f, 0f, 0.92f);
                outline.effectDistance = new Vector2(1.4f, -1.4f);
            }
            return txt;
        }

        public void RefreshView()
        {
            if (stageNameText == null || expSlider == null) EnsureUIHierarchy();

            DinoEvolutionController ctrl = _boundController != null ? _boundController : DinoEvolutionController.Instance;
            if (ctrl == null || ctrl.CurrentStage == null)
            {
                if (stageNameText != null) { stageNameText.text = "幼年期"; stageNameText.color = Color.white; }
                if (scaleBadgeText != null) { scaleBadgeText.text = "1.0x"; scaleBadgeText.color = Color.white; }
                if (expSlider != null) expSlider.value = 0f;
                if (expFillImage != null) expFillImage.color = new Color(0.85f, 0.88f, 0.92f, 1f);
                if (expValueText != null) expValueText.text = "0 / 50";
                return;
            }

            EvolutionStageConfig stage = ctrl.CurrentStage;
            if (stageNameText != null)
            {
                stageNameText.text = stage.stageName;
                stageNameText.color = stage.stageColor;
            }
            if (scaleBadgeText != null)
            {
                scaleBadgeText.text = $"{stage.targetScale:0.0}x";
                scaleBadgeText.color = stage.stageColor;
            }
            if (expFillImage != null)
            {
                expFillImage.color = stage.stageColor;
            }

            if (ctrl.IsMaxStage)
            {
                if (expSlider != null) expSlider.value = 1f;
                if (expValueText != null) expValueText.text = "MAX";
            }
            else
            {
                float req = Mathf.Max(1f, ctrl.CurrentStageRequiredExp);
                float cur = Mathf.Clamp(ctrl.CurrentExp, 0f, req);
                if (expSlider != null) expSlider.value = cur / req;
                if (expValueText != null) expValueText.text = $"{Mathf.RoundToInt(cur)} / {Mathf.RoundToInt(req)}";
            }
        }
    }
}
