using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PixelSaur.UI
{
    /// <summary>
    /// 角色生存状态 UI（生命、食物、口渴）：
    /// 位于屏幕底部居中，包含三个长条状态 Slider，各手搓 8x8 像素图标（红心、烤鸡腿、水滴）置于 Slider 顶部居中。
    /// 默认初始数值均为 100/100，支持动态增减与 Inspector 实时预览。
    /// </summary>
    [ExecuteAlways]
    public class PlayerStatusUI : MonoBehaviour
    {
        public static PlayerStatusUI Instance { get; private set; }

        [Header("8x8 像素风格贴图资产")]
        [SerializeField] private Sprite heartSprite;
        [SerializeField] private Sprite foodSprite;
        [SerializeField] private Sprite waterSprite;
        [SerializeField] private Sprite barBgSprite;
        [SerializeField] private Sprite barFillSprite;

        [Header("状态数值配置 (0 ~ 100)")]
        [Range(0f, 100f)]
        [SerializeField] private float currentHealth = 100f;
        [SerializeField] private float maxHealth = 100f;

        [Space(6)]
        [Range(0f, 100f)]
        [SerializeField] private float currentFood = 100f;
        [SerializeField] private float maxFood = 100f;

        [Space(6)]
        [Range(0f, 100f)]
        [SerializeField] private float currentThirst = 100f;
        [SerializeField] private float maxThirst = 100f;

        [Header("文本与排版配置")]
        [Tooltip("数值文本是否在条内居中显示（勾选为居中对齐，不勾选为靠左内缩对齐）")]
        [SerializeField] private bool centerValueText = true;
        [Tooltip("靠左对齐时的左侧内缩边距（像素）")]
        [SerializeField] private float textLeftPadding = 12f;

        [Header("UI 控件绑定")]
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Text healthText;
        [SerializeField] private Image healthIcon;

        [SerializeField] private Slider foodSlider;
        [SerializeField] private Text foodText;
        [SerializeField] private Image foodIcon;

        [SerializeField] private Slider thirstSlider;
        [SerializeField] private Text thirstText;
        [SerializeField] private Image thirstIcon;

        public float Health => currentHealth;
        public float Food => currentFood;
        public float Thirst => currentThirst;

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            Instance = this;
            LoadPixelSpritesIfMissing();
            EnsureUIHierarchy();
            UpdateUI();
        }

        private void Update()
        {
            UpdateUI();
        }

        private void OnValidate()
        {
            LoadPixelSpritesIfMissing();
            UpdateUI();
        }

        public void SetHealth(float value)
        {
            currentHealth = Mathf.Clamp(value, 0f, maxHealth);
            UpdateUI();
        }

        public void SetFood(float value)
        {
            currentFood = Mathf.Clamp(value, 0f, maxFood);
            UpdateUI();
        }

        public void SetThirst(float value)
        {
            currentThirst = Mathf.Clamp(value, 0f, maxThirst);
            UpdateUI();
        }

        public void ModifyHealth(float delta) => SetHealth(currentHealth + delta);
        public void ModifyFood(float delta) => SetFood(currentFood + delta);
        public void ModifyThirst(float delta) => SetThirst(currentThirst + delta);

        public void UpdateUI()
        {
            if (healthSlider != null)
            {
                healthSlider.maxValue = maxHealth;
                healthSlider.value = currentHealth;
            }
            if (healthText != null)
            {
                healthText.text = Mathf.RoundToInt(currentHealth).ToString();
            }

            if (foodSlider != null)
            {
                foodSlider.maxValue = maxFood;
                foodSlider.value = currentFood;
            }
            if (foodText != null)
            {
                foodText.text = Mathf.RoundToInt(currentFood).ToString();
            }

            if (thirstSlider != null)
            {
                thirstSlider.maxValue = maxThirst;
                thirstSlider.value = currentThirst;
            }
            if (thirstText != null)
            {
                thirstText.text = Mathf.RoundToInt(currentThirst).ToString();
            }
        }

        /// <summary>
        /// 自动加载 Assets/Textures/UI/ 下的 8x8 像素资产
        /// </summary>
        public void LoadPixelSpritesIfMissing(bool forceReload = false)
        {
#if UNITY_EDITOR
            bool changed = false;
            if (forceReload || heartSprite == null || heartSprite.rect.width != 8)
            {
                heartSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_heart_8x8.png");
                changed = true;
            }
            if (forceReload || foodSprite == null || foodSprite.rect.width != 8)
            {
                foodSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_food_8x8.png");
                changed = true;
            }
            if (forceReload || waterSprite == null || waterSprite.rect.width != 8)
            {
                waterSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_water_8x8.png");
                changed = true;
            }
            if (forceReload || barBgSprite == null || barBgSprite.rect.width != 8)
            {
                barBgSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/bar_bg_8x8.png");
                changed = true;
            }
            if (forceReload || barFillSprite == null || barFillSprite.rect.width != 8)
            {
                barFillSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/bar_fill_8x8.png");
                changed = true;
            }

            if (changed && !Application.isPlaying)
            {
                EditorUtility.SetDirty(this);
            }
#endif
        }

        /// <summary>
        /// 自动检测并构建完备的底部居中像素状态 UI 层级
        /// </summary>
        [ContextMenu("重建/刷新状态条UI层级")]
        public void EnsureUIHierarchy()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                canvas = Object.FindAnyObjectByType<Canvas>();
            }
            if (canvas == null) return;

            LoadPixelSpritesIfMissing(true);

            // 根容器：底部居中
            RectTransform panelRT = GetComponent<RectTransform>();
            if (panelRT == null) panelRT = gameObject.AddComponent<RectTransform>();

            panelRT.SetParent(canvas.transform, false);
            panelRT.anchorMin = new Vector2(0.5f, 0f);
            panelRT.anchorMax = new Vector2(0.5f, 0f);
            panelRT.pivot = new Vector2(0.5f, 0f);
            panelRT.anchoredPosition = new Vector2(0f, 24f); // 底部边距 24px
            panelRT.sizeDelta = new Vector2(760f, 34f);

            // 1. 生命状态条 (草绿色填充)
            healthSlider = SetupOrUpdateStatItem("HealthBar", panelRT, new Vector2(-255f, 0f), 
                heartSprite, new Color(0.40f, 0.78f, 0.16f, 1f),
                out healthText, out healthIcon);

            // 2. 食物状态条 (暖橙/烤肉色填充)
            foodSlider = SetupOrUpdateStatItem("FoodBar", panelRT, new Vector2(0f, 0f), 
                foodSprite, new Color(0.90f, 0.48f, 0.12f, 1f),
                out foodText, out foodIcon);

            // 3. 口渴状态条 (清透天蓝色填充)
            thirstSlider = SetupOrUpdateStatItem("ThirstBar", panelRT, new Vector2(255f, 0f), 
                waterSprite, new Color(0.18f, 0.65f, 0.95f, 1f),
                out thirstText, out thirstIcon);

            UpdateUI();
        }

        private Slider SetupOrUpdateStatItem(string itemName, RectTransform parent, Vector2 pos, 
            Sprite iconSp, Color fillColor, out Text outText, out Image outIcon)
        {
            Transform itemTr = parent.Find(itemName);
            GameObject itemObj = itemTr != null ? itemTr.gameObject : new GameObject(itemName, typeof(RectTransform));
            RectTransform itemRT = itemObj.GetComponent<RectTransform>();
            itemRT.SetParent(parent, false);
            itemRT.anchorMin = new Vector2(0.5f, 0f);
            itemRT.anchorMax = new Vector2(0.5f, 0f);
            itemRT.pivot = new Vector2(0.5f, 0f);
            itemRT.anchoredPosition = pos;
            itemRT.sizeDelta = new Vector2(240f, 32f);

            // 1. 移动到 Slider 左边边缘的 8x8 像素图标 (整倍数 30x30，紧贴 Slider 左侧)
            Transform iconTr = itemObj.transform.Find("Icon");
            GameObject iconObj = iconTr != null ? iconTr.gameObject : new GameObject("Icon", typeof(RectTransform), typeof(Image));
            RectTransform iconRT = iconObj.GetComponent<RectTransform>();
            iconRT.SetParent(itemRT, false);
            iconRT.anchorMin = new Vector2(0f, 0.5f);
            iconRT.anchorMax = new Vector2(0f, 0.5f);
            iconRT.pivot = new Vector2(0f, 0.5f);
            iconRT.anchoredPosition = new Vector2(0f, 0f); // 左侧靠齐
            iconRT.sizeDelta = new Vector2(30f, 30f);

            Image iconImg = iconObj.GetComponent<Image>();
            iconImg.sprite = iconSp;
            iconImg.color = Color.white;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            outIcon = iconImg;

            // 2. 右侧紧挨着的 Slider 像素进度条 (202 x 24)
            Transform sliderTr = itemObj.transform.Find("Slider");
            GameObject sliderObj = sliderTr != null ? sliderTr.gameObject : new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            RectTransform sliderRT = sliderObj.GetComponent<RectTransform>();
            sliderRT.SetParent(itemRT, false);
            sliderRT.anchorMin = new Vector2(0f, 0.5f);
            sliderRT.anchorMax = new Vector2(0f, 0.5f);
            sliderRT.pivot = new Vector2(0f, 0.5f);
            sliderRT.anchoredPosition = new Vector2(34f, 0f);
            sliderRT.sizeDelta = new Vector2(202f, 24f);

            Slider slider = sliderObj.GetComponent<Slider>();
            slider.interactable = false;
            slider.transition = Selectable.Transition.None;

            // Slider Background (深色槽九宫格，四周严密贴齐 Slider)
            Transform bgTr = sliderObj.transform.Find("Background");
            GameObject bgObj = bgTr != null ? bgTr.gameObject : new GameObject("Background", typeof(RectTransform), typeof(Image));
            RectTransform bgRT = bgObj.GetComponent<RectTransform>();
            bgRT.SetParent(sliderRT, false);
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.pivot = new Vector2(0.5f, 0.5f);
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;
            bgRT.anchoredPosition = Vector2.zero;
            bgRT.sizeDelta = Vector2.zero;

            Image bgImg = bgObj.GetComponent<Image>();
            bgImg.sprite = barBgSprite;
            bgImg.type = Image.Type.Sliced;
            bgImg.pixelsPerUnitMultiplier = 1f;
            bgImg.color = Color.white;
            bgImg.raycastTarget = false;

            // Fill Area & Fill (2px 内缩槽位，严丝合缝)
            Transform fillAreaTr = sliderObj.transform.Find("Fill Area");
            GameObject fillAreaObj = fillAreaTr != null ? fillAreaTr.gameObject : new GameObject("Fill Area", typeof(RectTransform));
            RectTransform fillAreaRT = fillAreaObj.GetComponent<RectTransform>();
            fillAreaRT.SetParent(sliderRT, false);
            fillAreaRT.anchorMin = Vector2.zero;
            fillAreaRT.anchorMax = Vector2.one;
            fillAreaRT.pivot = new Vector2(0.5f, 0.5f);
            fillAreaRT.offsetMin = new Vector2(2f, 2f);
            fillAreaRT.offsetMax = new Vector2(-2f, -2f);
            fillAreaRT.anchoredPosition = Vector2.zero;

            Transform fillTr = fillAreaObj.transform.Find("Fill");
            GameObject fillObj = fillTr != null ? fillTr.gameObject : new GameObject("Fill", typeof(RectTransform), typeof(Image));
            RectTransform fillRT = fillObj.GetComponent<RectTransform>();
            fillRT.SetParent(fillAreaRT, false);
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.pivot = new Vector2(0.5f, 0.5f);
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;
            fillRT.anchoredPosition = Vector2.zero;
            fillRT.sizeDelta = Vector2.zero;

            Image fillImg = fillObj.GetComponent<Image>();
            fillImg.sprite = barFillSprite;
            fillImg.type = Image.Type.Sliced;
            fillImg.pixelsPerUnitMultiplier = 1f;
            fillImg.color = fillColor;
            fillImg.raycastTarget = false;

            slider.targetGraphic = bgImg;
            slider.fillRect = fillRT;

            // 3. 数值文本（条内居中或靠左内缩，加粗带描边）
            Transform textTr = sliderObj.transform.Find("ValueText");
            GameObject textObj = textTr != null ? textTr.gameObject : new GameObject("ValueText", typeof(RectTransform), typeof(Text), typeof(Outline));
            RectTransform textRT = textObj.GetComponent<RectTransform>();
            textRT.SetParent(sliderRT, false);
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.pivot = new Vector2(0.5f, 0.5f);
            if (!centerValueText)
            {
                textRT.offsetMin = new Vector2(textLeftPadding, 0f);
                textRT.offsetMax = new Vector2(-textLeftPadding, 0f);
            }
            else
            {
                textRT.offsetMin = Vector2.zero;
                textRT.offsetMax = Vector2.zero;
            }
            textRT.anchoredPosition = Vector2.zero;

            Text textComp = textObj.GetComponent<Text>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            textComp.font = font;
            textComp.fontSize = 16;
            textComp.fontStyle = FontStyle.Bold;
            textComp.alignment = centerValueText ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
            textComp.color = Color.white;
            textComp.text = "100";
            textComp.raycastTarget = false;

            Outline outline = textObj.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = new Color(0f, 0f, 0f, 0.90f);
                outline.effectDistance = new Vector2(1.5f, -1.5f);
            }

            outText = textComp;
            return slider;
        }
    }
}
