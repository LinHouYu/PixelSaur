using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using PixelSaur.UI;

namespace PixelSaur.Creatures
{
    /// <summary>
    /// 绿色猎物测试生物（无 AI，静止）：
    /// 1. 存活时 100 血量，在玩家攻击范围内显示红色高亮 Shader/描边；
    /// 2. 死亡后倒地，在玩家攻击范围内显示绿色；
    /// 3. 支持长按进食：共可吃 3 次，每口 1.5 秒，每口恢复 20 饱食度并回复 15 生命；
    /// 4. 饱食度满（100）时不可进食；
    /// 5. 吃完 3 口后猎物彻底被食用完毕，并在设定时间后原地重新生成；
    /// 6. 所有参数全部支持 Inspector 灵活微调。
    /// </summary>
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
        [SerializeField] private Color aliveNormalColor = new Color(0.25f, 0.85f, 0.35f, 1f);
        [SerializeField] private Color inRangeThreatColor = new Color(0.95f, 0.20f, 0.20f, 1f); // 攻击范围内显示红色
        [SerializeField] private Color corpseDeadColor = new Color(0.18f, 0.45f, 0.18f, 1f);
        [SerializeField] private Color corpseInFeedRangeColor = new Color(0.20f, 0.95f, 0.35f, 1f); // 尸体在进食范围内显示亮绿

        [Header("头顶 UI (自动构建)")]
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
        private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");

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
            EnsureUIHierarchy();
            ResetPrey();
        }

        private void OnEnable()
        {
            EnsureUIHierarchy();
            UpdateVisualColor();
        }

        private void Update()
        {
            if (headBarRoot != null && Camera.main != null)
            {
                headBarRoot.rotation = Camera.main.transform.rotation; // 始终面向摄像机
            }
        }

        /// <summary>
        /// 外部指示圈每帧通知：玩家是否在此猎物的有效攻击范围内
        /// </summary>
        public void SetInAttackRange(bool inRange)
        {
            if (_isInAttackRange != inRange)
            {
                _isInAttackRange = inRange;
                UpdateVisualColor();
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
            else
            {
                UpdateVisualColor();
            }
        }

        private void Die()
        {
            _isDead = true;
            currentHealth = 0f;
            remainingFeedCount = totalFeedCount;

            // 倒地死亡表现：倒在地面上
            transform.rotation = Quaternion.Euler(90f, transform.eulerAngles.y, 0f);

            if (hpSlider != null) hpSlider.gameObject.SetActive(false);
            if (statusText != null)
            {
                statusText.text = $"可食用: {remainingFeedCount}/{totalFeedCount}口";
                statusText.color = new Color(0.4f, 1f, 0.4f);
            }

            UpdateVisualColor();
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

            if (headBarRoot != null) headBarRoot.gameObject.SetActive(true);
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
        }

        private void UpdateVisualColor()
        {
            if (_renderer == null) return;

            Color targetColor;
            if (!_isDead)
            {
                // 存活：在攻击范围内显示红色高亮，否则常态绿
                targetColor = _isInAttackRange ? inRangeThreatColor : aliveNormalColor;
            }
            else
            {
                // 死亡：在攻击范围内显示鲜绿高亮，否则暗绿
                targetColor = _isInAttackRange ? corpseInFeedRangeColor : corpseDeadColor;
            }

            if (_propBlock == null) _propBlock = new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(_propBlock);
            _propBlock.SetColor(BaseColorProp, targetColor);
            _renderer.SetPropertyBlock(_propBlock);
        }

        private void EnsureUIHierarchy()
        {
            if (headBarRoot != null) return;

            Transform hbTr = transform.Find("HeadBar");
            if (hbTr == null)
            {
                GameObject hbObj = new GameObject("HeadBar");
                hbObj.transform.SetParent(transform, false);
                hbObj.transform.localPosition = new Vector3(0f, 1.4f, 0f);
                headBarRoot = hbObj.transform;

                Canvas c = hbObj.AddComponent<Canvas>();
                c.renderMode = RenderMode.WorldSpace;
                RectTransform crt = hbObj.GetComponent<RectTransform>();
                crt.sizeDelta = new Vector2(160f, 40f);
                crt.localScale = Vector3.one * 0.008f;

                // 文字
                GameObject textObj = new GameObject("StatusText", typeof(RectTransform), typeof(Text), typeof(Outline));
                textObj.transform.SetParent(headBarRoot, false);
                RectTransform trt = textObj.GetComponent<RectTransform>();
                trt.sizeDelta = new Vector2(160f, 24f);
                trt.anchoredPosition = new Vector2(0f, 10f);

                statusText = textObj.GetComponent<Text>();
                statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
                statusText.fontSize = 14;
                statusText.alignment = TextAnchor.MiddleCenter;
                statusText.color = Color.white;
                statusText.text = "猎物 HP: 100/100";

                Outline outline = textObj.GetComponent<Outline>();
                outline.effectColor = new Color(0, 0, 0, 0.9f);
                outline.effectDistance = new Vector2(1, -1);
            }
            else
            {
                headBarRoot = hbTr;
                statusText = headBarRoot.GetComponentInChildren<Text>();
            }
        }
    }
}
