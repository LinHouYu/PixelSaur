using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using PixelSaur.UI;

namespace PixelSaur.Creatures
{
    /// <summary>
    /// 绿色猎物测试生物（无 AI，静止）：
    /// 1. 存活时 100 血量，仅在玩家攻击范围内显示头顶血条，平时完全隐藏；
    /// 2. 存活时进入玩家攻击范围，外圈呈现红色描边Shader，头顶显现动态血条与数值；
    /// 3. 死亡后倒地，在玩家攻击范围内外圈呈现黄色描边Shader，头顶显现剩余食用次数；
    /// 4. 处于攻击范围时长按 0.4s 进食：共可吃 3 次，每口 1.5 秒，每口恢复 20 饱食度并回复 15 生命；
    /// 5. 饱食度满（100）时不可进食；吃完 3 口后彻底食用完毕并在 5 秒后原地重新生成；
    /// 6. 所有参数全部支持 Inspector 灵活微调。
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Collider))]
    public class GreenPreyCapsule : MonoBehaviour
    {
        [Header("基础生命与状态")]
        [Tooltip("初始最大生命值")]
        [SerializeField] private float maxHealth = 100.0f;
        [SerializeField] private float currentHealth = 100.0f;

        [Header("进食与食用机制")]
        [Tooltip("最大可被食用次数")]
        [SerializeField] private int totalFeedCount = 3;
        [Tooltip("当前剩余可被食用次数")]
        [SerializeField] private int remainingFeedCount = 3;

        [Tooltip("每吃一口消耗的时间（秒）")]
        [SerializeField] private float feedBiteDuration = 1.5f;

        [Tooltip("每吃一口恢复的饱食度数值")]
        [SerializeField] private float feedFoodRecovery = 20.0f;

        [Tooltip("每吃一口恢复的生命值数值")]
        [SerializeField] private float feedHealthRecovery = 15.0f;

        [Tooltip("完全吃完后重生冷却时间（秒）")]
        [SerializeField] private float respawnDelay = 5.0f;

        [Header("视觉与描边高亮材质")]
        [SerializeField] private Color aliveNormalColor = new Color(0.25f, 0.85f, 0.35f, 1f); // 活体常态绿
        [SerializeField] private Color aliveOutlineColor = new Color(1.0f, 0.15f, 0.15f, 1f); // 攻击范围内显示外圈红色Shader
        [SerializeField] private Color corpseDeadColor = new Color(0.18f, 0.35f, 0.18f, 1f); // 尸体暗绿底色
        [SerializeField] private Color corpseOutlineColor = new Color(1.0f, 0.85f, 0.15f, 1f); // 死后在进食范围内显示外圈黄色Shader
        [SerializeField] private float outlineWidth = 0.06f; // 外圈描边宽度

        [Header("头顶 UI (仅在攻击范围内显示)")]
        [SerializeField] private Transform headBarRoot;
        [SerializeField] private Slider hpSlider;
        [SerializeField] private Text statusText;

        private MeshRenderer _renderer;
        private Collider _collider;
        private MaterialPropertyBlock _propBlock;
        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;
        private bool _isDead = false;
        private bool _isInAttackRange = false;
        private float _lastRangePingTime = -999f;
        private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");
        private static readonly int OutlineColorProp = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineWidthProp = Shader.PropertyToID("_OutlineWidth");

        public bool IsDead => _isDead;
        public float Health => currentHealth;
        public int RemainingFeedCount => remainingFeedCount;
        public float FeedBiteDuration => feedBiteDuration;

        private void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            _collider = GetComponent<Collider>();
            _spawnPosition = transform.position;
            _spawnRotation = transform.rotation;
            EnsureHighlightMaterial();
            EnsureUIHierarchy();
            ResetPrey();
        }

        private void EnsureHighlightMaterial()
        {
            if (_renderer != null)
            {
                Shader shader = Shader.Find("PixelSaur/CreatureHighlight");
                if (shader != null && (_renderer.sharedMaterial == null || _renderer.sharedMaterial.shader != shader))
                {
                    Material mat = new Material(shader) { name = "M_GreenPrey_Highlight" };
                    _renderer.sharedMaterial = mat;
                }
            }
        }

        private void OnEnable()
        {
            EnsureHighlightMaterial();
            EnsureUIHierarchy();
            UpdateVisualColor();
            UpdateHeadBarVisibility();
        }

        private void Update()
        {
            if (headBarRoot != null && headBarRoot.gameObject.activeSelf && Camera.main != null)
            {
                headBarRoot.rotation = Camera.main.transform.rotation; // 始终面向摄像机
            }

            // 心跳检测：若超过 0.15 秒未收到攻击范围通知，自动取消外圈描边并隐藏血条
            if (_isInAttackRange && Time.time - _lastRangePingTime > 0.15f)
            {
                _isInAttackRange = false;
                UpdateVisualColor();
                UpdateHeadBarVisibility();
            }
        }

        /// <summary>
        /// 外部指示圈每帧通知：玩家是否在此猎物的有效攻击范围内
        /// </summary>
        public void SetInAttackRange(bool inRange)
        {
            _lastRangePingTime = Time.time;
            if (_isInAttackRange != inRange)
            {
                _isInAttackRange = inRange;
                UpdateVisualColor();
                UpdateHeadBarVisibility();
            }
        }

        private void UpdateHeadBarVisibility()
        {
            if (headBarRoot != null)
            {
                // 核心规则：仅在玩家攻击范围内才显示头顶UI（血条或可食用次数）！
                headBarRoot.gameObject.SetActive(_isInAttackRange);
            }
        }

        /// <summary>
        /// 受到攻击咬击扣血
        /// </summary>
        public void TakeDamage(float damage)
        {
            if (_isDead) return;

            currentHealth = Mathf.Max(0f, currentHealth - damage);
            if (hpSlider != null) hpSlider.value = currentHealth;
            if (statusText != null) statusText.text = $"猎物 HP: {Mathf.CeilToInt(currentHealth)}/{maxHealth}";

            if (currentHealth <= 0.01f)
            {
                Die();
            }
        }

        private void Die()
        {
            _isDead = true;
            currentHealth = 0f;
            remainingFeedCount = totalFeedCount;

            // 倒地死亡表现
            transform.rotation = Quaternion.Euler(90f, transform.eulerAngles.y, 0f);

            if (hpSlider != null) hpSlider.gameObject.SetActive(false);
            if (statusText != null)
            {
                statusText.text = $"可食用: {remainingFeedCount}/{totalFeedCount}口";
                statusText.color = new Color(0.4f, 1f, 0.4f);
            }

            UpdateVisualColor();
            UpdateHeadBarVisibility();
        }

        /// <summary>
        /// 执行一次进食咬食（消耗1口，回饱食度与血量）
        /// </summary>
        public bool BiteFeed()
        {
            if (!_isDead || remainingFeedCount <= 0) return false;

            // 饱食度满（100）时不可进食
            if (PlayerStatusUI.Instance != null && PlayerStatusUI.Instance.Food >= 100f)
            {
                Debug.Log("[GreenPreyCapsule] 角色饱食度已满 (100)，无法继续进食！");
                return false;
            }

            remainingFeedCount--;

            if (PlayerStatusUI.Instance != null)
            {
                PlayerStatusUI.Instance.ModifyFood(feedFoodRecovery);
                PlayerStatusUI.Instance.ModifyHealth(feedHealthRecovery);
            }

            Debug.Log($"<color=#33FF33>[GreenPreyCapsule] 成功咬食一口！剩余 {remainingFeedCount} 口，恢复饱食度 {feedFoodRecovery}，生命 {feedHealthRecovery}。</color>");

            if (statusText != null)
            {
                statusText.text = $"可食用: {remainingFeedCount}/{totalFeedCount}口";
            }

            // 吃完 3 口后猎物彻底被吃完
            if (remainingFeedCount <= 0)
            {
                StartCoroutine(DisappearAndRespawnRoutine());
            }

            return true;
        }

        private IEnumerator DisappearAndRespawnRoutine()
        {
            if (statusText != null) statusText.text = "已被完全食用！";
            yield return new WaitForSeconds(0.4f);

            // 隐藏
            if (_renderer != null) _renderer.enabled = false;
            if (_collider != null) _collider.enabled = false;
            if (headBarRoot != null) headBarRoot.gameObject.SetActive(false);

            // 等待重生
            yield return new WaitForSeconds(respawnDelay);

            // 原地重生
            ResetPrey();
        }

        private void ResetPrey()
        {
            _isDead = false;
            currentHealth = maxHealth;
            remainingFeedCount = totalFeedCount;
            _isInAttackRange = false;

            transform.position = _spawnPosition;
            transform.rotation = _spawnRotation;

            if (_renderer != null) _renderer.enabled = true;
            if (_collider != null) _collider.enabled = true;

            if (hpSlider != null)
            {
                hpSlider.gameObject.SetActive(true);
                hpSlider.maxValue = maxHealth;
                hpSlider.value = currentHealth;
            }
            if (statusText != null)
            {
                statusText.text = $"猎物 HP: {maxHealth}/{maxHealth}";
                statusText.color = Color.white;
            }

            UpdateVisualColor();
            UpdateHeadBarVisibility();
        }

        private void UpdateVisualColor()
        {
            if (_renderer == null) return;

            Color baseCol = _isDead ? corpseDeadColor : aliveNormalColor;
            Color outlineCol = Color.clear;
            float currentWidth = 0f;

            if (_isInAttackRange)
            {
                // 存活时外圈红色Shader，死后外圈黄色Shader
                outlineCol = _isDead ? corpseOutlineColor : aliveOutlineColor;
                currentWidth = outlineWidth;
            }

            if (_propBlock == null) _propBlock = new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(_propBlock);
            _propBlock.SetColor(BaseColorProp, baseCol);
            _propBlock.SetColor(OutlineColorProp, outlineCol);
            _propBlock.SetFloat(OutlineWidthProp, currentWidth);
            _renderer.SetPropertyBlock(_propBlock);
        }

        public void EnsureUIHierarchy()
        {
            Transform hbTr = transform.Find("HeadBar");
            GameObject hbObj;
            if (hbTr != null)
            {
                headBarRoot = hbTr;
                hpSlider = hbTr.GetComponentInChildren<Slider>();
                statusText = hbTr.GetComponentInChildren<Text>();
                if (hpSlider != null && statusText != null)
                {
                    headBarRoot.gameObject.SetActive(false);
                    return;
                }

                // 清理旧子节点
                for (int i = hbTr.childCount - 1; i >= 0; i--)
                {
                    DestroyImmediate(hbTr.GetChild(i).gameObject);
                }
                hbObj = hbTr.gameObject;
            }
            else
            {
                hbObj = new GameObject("HeadBar");
                hbObj.transform.SetParent(transform, false);
                hbObj.transform.localPosition = new Vector3(0f, 1.45f, 0f);
            }

            headBarRoot = hbObj.transform;

            Canvas canvas = hbObj.GetComponent<Canvas>() ?? hbObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform canvasRT = hbObj.GetComponent<RectTransform>();
            canvasRT.sizeDelta = new Vector2(140f, 32f);
            canvasRT.localScale = Vector3.one * 0.0075f;

            // 1. Slider
            GameObject sliderObj = new GameObject("HPSlider", typeof(RectTransform), typeof(Slider));
            sliderObj.transform.SetParent(headBarRoot, false);
            RectTransform sliderRT = sliderObj.GetComponent<RectTransform>();
            sliderRT.sizeDelta = new Vector2(120f, 12f);
            sliderRT.anchoredPosition = new Vector2(0f, 7f);

            Slider slider = sliderObj.GetComponent<Slider>();
            slider.interactable = false;
            slider.transition = Selectable.Transition.None;

            // Background
            GameObject bgObj = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgObj.transform.SetParent(sliderRT, false);
            RectTransform bgRT = bgObj.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.sizeDelta = Vector2.zero;
            Image bgImg = bgObj.GetComponent<Image>();
            bgImg.color = new Color(0.12f, 0.12f, 0.14f, 0.88f);

            // Fill Area
            GameObject fillAreaObj = new GameObject("Fill Area", typeof(RectTransform));
            fillAreaObj.transform.SetParent(sliderRT, false);
            RectTransform fillAreaRT = fillAreaObj.GetComponent<RectTransform>();
            fillAreaRT.anchorMin = Vector2.zero;
            fillAreaRT.anchorMax = Vector2.one;
            fillAreaRT.offsetMin = new Vector2(1f, 1f);
            fillAreaRT.offsetMax = new Vector2(-1f, -1f);

            // Fill
            GameObject fillObj = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillObj.transform.SetParent(fillAreaRT, false);
            RectTransform fillRT = fillObj.GetComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.sizeDelta = Vector2.zero;
            Image fillImg = fillObj.GetComponent<Image>();
            fillImg.color = new Color(0.25f, 0.85f, 0.35f, 1f); // 翠绿血条

            slider.fillRect = fillRT;
            slider.targetGraphic = bgImg;
            slider.maxValue = maxHealth;
            slider.value = currentHealth;
            hpSlider = slider;

            // 2. StatusText
            GameObject textObj = new GameObject("StatusText", typeof(RectTransform), typeof(Text), typeof(Outline));
            textObj.transform.SetParent(headBarRoot, false);
            RectTransform textRT = textObj.GetComponent<RectTransform>();
            textRT.sizeDelta = new Vector2(140f, 16f);
            textRT.anchoredPosition = new Vector2(0f, -8f);

            statusText = textObj.GetComponent<Text>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            statusText.font = font;
            statusText.fontSize = 12;
            statusText.fontStyle = FontStyle.Bold;
            statusText.alignment = TextAnchor.MiddleCenter;
            statusText.color = Color.white;
            statusText.text = $"猎物 HP: {Mathf.CeilToInt(currentHealth)}/{maxHealth}";

            Outline outline = textObj.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
            outline.effectDistance = new Vector2(1f, -1f);

            // 初始隐藏，进入攻击范围才显示！
            headBarRoot.gameObject.SetActive(false);
        }
    }
}
