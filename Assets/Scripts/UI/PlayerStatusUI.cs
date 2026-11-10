using UnityEngine;
using UnityEngine.UI;

namespace PixelSaur.UI
{
    /// <summary>
    /// 角色生存状态 UI（生命、食物、口渴）：
    /// 位于屏幕底部居中，包含三个长条状态 Slider，各图标（红心、鸡腿、水滴）置于 Slider 顶部居中。
    /// 默认初始数值均为 100/100，支持动态增减与 Inspector 实时预览。
    /// </summary>
    [ExecuteAlways]
    public class PlayerStatusUI : MonoBehaviour
    {
        public static PlayerStatusUI Instance { get; private set; }

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

        // 缓存生成的程序化图标
        private static Sprite _heartSprite;
        private static Sprite _foodSprite;
        private static Sprite _waterSprite;
        private static Sprite _barBgSprite;
        private static Sprite _barFillSprite;

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
            EnsureUIHierarchy();
            UpdateUI();
        }

        private void Update()
        {
            UpdateUI();
        }

        private void OnValidate()
        {
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
        /// 自动检测并构建完备的底部居中状态 UI 层级
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

            InitSprites();

            // 根容器：底部居中
            RectTransform panelRT = GetComponent<RectTransform>();
            if (panelRT == null) panelRT = gameObject.AddComponent<RectTransform>();

            panelRT.SetParent(canvas.transform, false);
            panelRT.anchorMin = new Vector2(0.5f, 0f);
            panelRT.anchorMax = new Vector2(0.5f, 0f);
            panelRT.pivot = new Vector2(0.5f, 0f);
            panelRT.anchoredPosition = new Vector2(0f, 32f); // 底部安全边距 32px
            panelRT.sizeDelta = new Vector2(760f, 68f);

            // 1. 生命状态条 (绿色)
            if (healthSlider == null)
            {
                healthSlider = CreateStatItem("HealthBar", panelRT, new Vector2(-255f, 0f), 
                    _heartSprite, new Color(0.42f, 0.78f, 0.16f, 1f), new Color(0.12f, 0.22f, 0.08f, 0.92f),
                    out healthText, out healthIcon);
            }

            // 2. 食物状态条 (暖橙/棕色)
            if (foodSlider == null)
            {
                foodSlider = CreateStatItem("FoodBar", panelRT, new Vector2(0f, 0f), 
                    _foodSprite, new Color(0.88f, 0.46f, 0.15f, 1f), new Color(0.24f, 0.12f, 0.05f, 0.92f),
                    out foodText, out foodIcon);
            }

            // 3. 口渴状态条 (水天蓝色)
            if (thirstSlider == null)
            {
                thirstSlider = CreateStatItem("ThirstBar", panelRT, new Vector2(255f, 0f), 
                    _waterSprite, new Color(0.18f, 0.65f, 0.94f, 1f), new Color(0.06f, 0.16f, 0.28f, 0.92f),
                    out thirstText, out thirstIcon);
            }
        }

        private Slider CreateStatItem(string itemName, RectTransform parent, Vector2 pos, 
            Sprite iconSprite, Color fillColor, Color bgColor, out Text outText, out Image outIcon)
        {
            Transform itemTr = parent.Find(itemName);
            GameObject itemObj = itemTr != null ? itemTr.gameObject : new GameObject(itemName, typeof(RectTransform));
            RectTransform itemRT = itemObj.GetComponent<RectTransform>();
            itemRT.SetParent(parent, false);
            itemRT.anchorMin = new Vector2(0.5f, 0f);
            itemRT.anchorMax = new Vector2(0.5f, 0f);
            itemRT.pivot = new Vector2(0.5f, 0f);
            itemRT.anchoredPosition = pos;
            itemRT.sizeDelta = new Vector2(220f, 62f);

            // 1. 顶部居中的 Icon
            Transform iconTr = itemObj.transform.Find("Icon");
            GameObject iconObj = iconTr != null ? iconTr.gameObject : new GameObject("Icon", typeof(RectTransform), typeof(Image));
            RectTransform iconRT = iconObj.GetComponent<RectTransform>();
            iconRT.SetParent(itemRT, false);
            iconRT.anchorMin = new Vector2(0.5f, 1f);
            iconRT.anchorMax = new Vector2(0.5f, 1f);
            iconRT.pivot = new Vector2(0.5f, 1f);
            iconRT.anchoredPosition = new Vector2(0f, 0f); // 顶端居中
            iconRT.sizeDelta = new Vector2(34f, 34f);

            Image iconImg = iconObj.GetComponent<Image>();
            iconImg.sprite = iconSprite;
            iconImg.color = Color.white;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            outIcon = iconImg;

            // 2. 下方的 Slider 条
            Transform sliderTr = itemObj.transform.Find("Slider");
            GameObject sliderObj = sliderTr != null ? sliderTr.gameObject : new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            RectTransform sliderRT = sliderObj.GetComponent<RectTransform>();
            sliderRT.SetParent(itemRT, false);
            sliderRT.anchorMin = new Vector2(0.5f, 0f);
            sliderRT.anchorMax = new Vector2(0.5f, 0f);
            sliderRT.pivot = new Vector2(0.5f, 0f);
            sliderRT.anchoredPosition = new Vector2(0f, 0f);
            sliderRT.sizeDelta = new Vector2(220f, 24f);

            Slider slider = sliderObj.GetComponent<Slider>();
            slider.interactable = false;
            slider.transition = Selectable.Transition.None;

            // Slider Background
            Transform bgTr = sliderObj.transform.Find("Background");
            GameObject bgObj = bgTr != null ? bgTr.gameObject : new GameObject("Background", typeof(RectTransform), typeof(Image));
            RectTransform bgRT = bgObj.GetComponent<RectTransform>();
            bgRT.SetParent(sliderRT, false);
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.sizeDelta = Vector2.zero;
            Image bgImg = bgObj.GetComponent<Image>();
            bgImg.sprite = _barBgSprite;
            bgImg.type = Image.Type.Sliced;
            bgImg.color = bgColor;
            bgImg.raycastTarget = false;

            // Fill Area & Fill
            Transform fillAreaTr = sliderObj.transform.Find("Fill Area");
            GameObject fillAreaObj = fillAreaTr != null ? fillAreaTr.gameObject : new GameObject("Fill Area", typeof(RectTransform));
            RectTransform fillAreaRT = fillAreaObj.GetComponent<RectTransform>();
            fillAreaRT.SetParent(sliderRT, false);
            fillAreaRT.anchorMin = new Vector2(0f, 0f);
            fillAreaRT.anchorMax = new Vector2(1f, 1f);
            fillAreaRT.sizeDelta = new Vector2(-4f, -4f); // 2px 内缩边框

            Transform fillTr = fillAreaObj.transform.Find("Fill");
            GameObject fillObj = fillTr != null ? fillTr.gameObject : new GameObject("Fill", typeof(RectTransform), typeof(Image));
            RectTransform fillRT = fillObj.GetComponent<RectTransform>();
            fillRT.SetParent(fillAreaRT, false);
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.sizeDelta = Vector2.zero;
            Image fillImg = fillObj.GetComponent<Image>();
            fillImg.sprite = _barFillSprite;
            fillImg.type = Image.Type.Sliced;
            fillImg.color = fillColor;
            fillImg.raycastTarget = false;

            slider.targetGraphic = bgImg;
            slider.fillRect = fillRT;

            // 3. 数值文本（居左偏内，带黑描边）
            Transform textTr = sliderObj.transform.Find("ValueText");
            GameObject textObj = textTr != null ? textTr.gameObject : new GameObject("ValueText", typeof(RectTransform), typeof(Text), typeof(Outline));
            RectTransform textRT = textObj.GetComponent<RectTransform>();
            textRT.SetParent(sliderRT, false);
            textRT.anchorMin = new Vector2(0f, 0f);
            textRT.anchorMax = new Vector2(1f, 1f);
            textRT.offsetMin = new Vector2(10f, 0f);
            textRT.offsetMax = new Vector2(-10f, 0f);

            Text textComp = textObj.GetComponent<Text>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            textComp.font = font;
            textComp.fontSize = 17;
            textComp.fontStyle = FontStyle.Bold;
            textComp.alignment = TextAnchor.MiddleLeft;
            textComp.color = Color.white;
            textComp.text = "100";
            textComp.raycastTarget = false;

            Outline outline = textObj.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
                outline.effectDistance = new Vector2(1.5f, -1.5f);
            }

            outText = textComp;
            return slider;
        }

        private static void InitSprites()
        {
            if (_heartSprite == null) _heartSprite = GenerateHeartSprite();
            if (_foodSprite == null) _foodSprite = GenerateDrumstickSprite();
            if (_waterSprite == null) _waterSprite = GenerateWaterDropSprite();
            if (_barBgSprite == null) _barBgSprite = GenerateBarBoxSprite(64, 24, true);
            if (_barFillSprite == null) _barFillSprite = GenerateBarBoxSprite(64, 24, false);
        }

        /// <summary>
        /// 程序化生成饱满可爱的红心图标
        /// </summary>
        private static Sprite GenerateHeartSprite()
        {
            int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };

            Color clear = new Color(0, 0, 0, 0);
            Color outline = new Color(0.12f, 0.05f, 0.05f, 0.95f);
            Color fill = new Color(0.95f, 0.22f, 0.22f, 1f);
            Color highlight = new Color(1f, 0.85f, 0.85f, 0.85f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 归一化心形坐标，中心在 (32, 28)
                    float nx = (x - 31.5f) / 19.5f;
                    float ny = (y - 28.0f) / 19.5f;

                    // 心形公式: (x^2 + y^2 - 1)^3 - x^2 * y^3 <= 0
                    float x2 = nx * nx;
                    float y2 = ny * ny;
                    float term = x2 + y2 - 1f;
                    float d = term * term * term - x2 * ny * y2;

                    if (d <= 0.32f)
                    {
                        if (d > 0f)
                        {
                            tex.SetPixel(x, y, outline);
                        }
                        else
                        {
                            // 左上角高光小弧
                            float hlDist = Mathf.Sqrt((nx + 0.38f) * (nx + 0.38f) + (ny - 0.42f) * (ny - 0.42f));
                            if (hlDist < 0.25f)
                            {
                                tex.SetPixel(x, y, Color.Lerp(fill, highlight, (0.25f - hlDist) / 0.25f));
                            }
                            else
                            {
                                tex.SetPixel(x, y, fill);
                            }
                        }
                    }
                    else
                    {
                        tex.SetPixel(x, y, clear);
                    }
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        /// <summary>
        /// 程序化生成香脆饱满的烤鸡腿图标
        /// </summary>
        private static Sprite GenerateDrumstickSprite()
        {
            int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };

            Color clear = new Color(0, 0, 0, 0);
            Color outline = new Color(0.20f, 0.08f, 0.02f, 0.95f);
            Color meatColor = new Color(0.92f, 0.50f, 0.18f, 1f);
            Color meatShade = new Color(0.78f, 0.36f, 0.10f, 1f);
            Color boneColor = new Color(0.96f, 0.94f, 0.88f, 1f);
            Color boneOutline = new Color(0.25f, 0.20f, 0.18f, 0.95f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 1. 肉块部分：右上方大椭圆心 (39, 39)
                    float mdx = (x - 39f) / 16.5f;
                    float mdy = (y - 39f) / 14.5f;
                    float meatD = mdx * mdx + mdy * mdy;

                    // 肉身延伸到左下 (26, 26)
                    float bodyDist = DistanceToSegment(new Vector2(x, y), new Vector2(39, 39), new Vector2(24, 24));
                    float bodyRadius = Mathf.Lerp(6.5f, 15f, Mathf.Clamp01((x + y - 48f) / 30f));

                    // 2. 骨头柄部分：左下方骨头双头 (14, 18) 与 (18, 14)
                    float b1 = Vector2.Distance(new Vector2(x, y), new Vector2(14, 19));
                    float b2 = Vector2.Distance(new Vector2(x, y), new Vector2(19, 14));
                    float boneBar = DistanceToSegment(new Vector2(x, y), new Vector2(16, 16), new Vector2(25, 25));

                    bool isBone = (b1 <= 4.8f || b2 <= 4.8f || (boneBar <= 3.2f && x < 25 && y < 25));
                    bool isBoneEdge = (b1 <= 6.2f || b2 <= 6.2f || (boneBar <= 4.5f && x < 26 && y < 26));

                    bool isMeat = (meatD <= 1.0f) || (bodyDist <= bodyRadius);
                    bool isMeatEdge = (meatD <= 1.25f) || (bodyDist <= bodyRadius + 2.5f);

                    if (isMeat)
                    {
                        // 带有微微高光渐变
                        float hl = (x - y + 10f) / 40f;
                        tex.SetPixel(x, y, Color.Lerp(meatShade, meatColor, Mathf.Clamp01(hl + 0.5f)));
                    }
                    else if (isMeatEdge)
                    {
                        tex.SetPixel(x, y, outline);
                    }
                    else if (isBone)
                    {
                        tex.SetPixel(x, y, boneColor);
                    }
                    else if (isBoneEdge)
                    {
                        tex.SetPixel(x, y, boneOutline);
                    }
                    else
                    {
                        tex.SetPixel(x, y, clear);
                    }
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        /// <summary>
        /// 程序化生成清透水滴图标
        /// </summary>
        private static Sprite GenerateWaterDropSprite()
        {
            int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };

            Color clear = new Color(0, 0, 0, 0);
            Color outline = new Color(0.06f, 0.20f, 0.45f, 0.95f);
            Color waterColor = new Color(0.24f, 0.68f, 0.96f, 1f);
            Color waterLight = new Color(0.65f, 0.90f, 1f, 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - 31.5f);
                    float dy = y;

                    // 水滴形：下半部半圆 (R=15, 圆心在 32, 23)，上半部切线收束至 (32, 53)
                    float dropD = 100f;
                    if (dy <= 23f)
                    {
                        dropD = Mathf.Sqrt(dx * dx + (dy - 23f) * (dy - 23f)) / 15f;
                    }
                    else if (dy <= 53f)
                    {
                        float taperRatio = (53f - dy) / 30f;
                        float maxWidth = 15f * Mathf.Sqrt(Mathf.Clamp01(taperRatio));
                        dropD = Mathf.Abs(dx) / Mathf.Max(0.1f, maxWidth);
                    }

                    if (dropD <= 1.0f)
                    {
                        // 右侧白色小高光弧
                        float hlX = dx - 4.5f;
                        float hlY = dy - 24f;
                        float hlDist = Mathf.Sqrt(hlX * hlX + hlY * hlY);
                        if (hlDist < 4f)
                        {
                            tex.SetPixel(x, y, waterLight);
                        }
                        else
                        {
                            tex.SetPixel(x, y, Color.Lerp(waterColor, waterLight, (dy - 10f) / 50f * 0.4f));
                        }
                    }
                    else if (dropD <= 1.25f)
                    {
                        tex.SetPixel(x, y, outline);
                    }
                    else
                    {
                        tex.SetPixel(x, y, clear);
                    }
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        /// <summary>
        /// 程序化生成带微圆角与深色边框的九宫格进度条板
        /// </summary>
        private static Sprite GenerateBarBoxSprite(int w, int h, bool isBorder)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };

            Color borderColor = new Color(0.10f, 0.12f, 0.15f, 0.95f);
            Color insideColor = isBorder ? new Color(0.08f, 0.10f, 0.12f, 0.85f) : Color.white;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool edge = (x < 2 || x >= w - 2 || y < 2 || y >= h - 2);
                    // 切圆角
                    bool corner = (x < 3 && y < 3) || (x < 3 && y >= h - 3) || (x >= w - 3 && y < 3) || (x >= w - 3 && y >= h - 3);

                    if (corner && (x == 0 || x == w - 1 || y == 0 || y == h - 1))
                    {
                        tex.SetPixel(x, y, new Color(0, 0, 0, 0));
                    }
                    else if (edge)
                    {
                        tex.SetPixel(x, y, borderColor);
                    }
                    else
                    {
                        tex.SetPixel(x, y, insideColor);
                    }
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(6, 6, 6, 6));
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float l2 = ab.sqrMagnitude;
            if (l2 == 0f) return Vector2.Distance(p, a);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2);
            Vector2 proj = a + t * ab;
            return Vector2.Distance(p, proj);
        }
    }
}
