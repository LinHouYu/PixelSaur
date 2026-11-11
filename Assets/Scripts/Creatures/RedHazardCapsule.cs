using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using PixelSaur.UI;
using PixelSaur.Player;

namespace PixelSaur.Creatures
{
    /// <summary>
    /// 红色危险生物胶囊（敌对目标）：
    /// 1. 拥有 100 生命值与头顶悬浮血条，仅在玩家攻击范围内才显示，平时完全隐藏；
    /// 2. 存活时进入玩家攻击范围，外圈呈现红色描边Shader，头顶显现动态血条与数值；
    /// 3. 当玩家碰到它时扣除 20 点生命值（最小触发间隔 0.4s）；
    /// 4. 可被玩家咬击与冲击技能击杀，血量归零后倒地隐去并在 5 秒后满血原地重生；
    /// 5. 所有参数均在 Inspector 暴露，方便微调。
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Collider))]
    public class RedHazardCapsule : MonoBehaviour
    {
        [Header("基础生命与状态")]
        [Tooltip("初始最大生命值")]
        [SerializeField] private float maxHealth = 100.0f;
        [SerializeField] private float currentHealth = 100.0f;

        [Header("伤害配置")]
        [Tooltip("每次碰撞扣减玩家生命值")]
        [SerializeField] private float damageAmount = 20.0f;

        [Tooltip("伤害触发冷却间隔（秒）")]
        [SerializeField] private float damageInterval = 0.4f;

        [Header("受击反馈")]
        [Tooltip("碰击时给玩家轻微后退击退力")]
        [SerializeField] private float knockbackForce = 2.5f;

        [Tooltip("贴身触碰检测距离（米）")]
        [SerializeField] private float contactDistance = 1.25f;

        [Tooltip("完全死亡后重生冷却时间（秒）")]
        [SerializeField] private float respawnDelay = 5.0f;

        [Header("视觉与描边高亮材质")]
        [SerializeField] private Color baseBodyColor = new Color(0.95f, 0.15f, 0.15f, 1f);
        [SerializeField] private Color inRangeOutlineColor = new Color(1.0f, 0.15f, 0.15f, 1f); // 攻击范围内红圈描边
        [SerializeField] private float outlineWidth = 0.06f;

        [Header("头顶 UI (仅在攻击范围内显示)")]
        [SerializeField] private Transform headBarRoot;
        [SerializeField] private Slider hpSlider;
        [SerializeField] private Text statusText;

        private float _lastDamageTime = -999f;
        private float _lastRangePingTime = -999f;
        private bool _isInAttackRange = false;
        private bool _isDead = false;
        private MeshRenderer _renderer;
        private Collider _collider;
        private MaterialPropertyBlock _propBlock;
        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;

        private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");
        private static readonly int OutlineColorProp = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineWidthProp = Shader.PropertyToID("_OutlineWidth");

        public bool IsDead => _isDead;
        public float Health => currentHealth;

        private void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            _collider = GetComponent<Collider>();
            _spawnPosition = transform.position;
            _spawnRotation = transform.rotation;
            EnsureVisuals();
            EnsureUIHierarchy();
            ResetCreature();
        }

        private void OnEnable()
        {
            EnsureVisuals();
            EnsureUIHierarchy();
            UpdateVisualColor();
            UpdateHeadBarVisibility();
        }

        private void Update()
        {
            // 1. 心跳检测：若超过 0.15 秒未收到攻击范围通知，自动取消外圈描边并隐藏血条
            if (_isInAttackRange && Time.time - _lastRangePingTime > 0.15f)
            {
                _isInAttackRange = false;
                UpdateVisualColor();
                UpdateHeadBarVisibility();
            }

            // 2. 距离检测：当角色贴近胶囊体且敌人存活时稳定触发扣血
            if (!_isDead && PlayerController.Instance != null)
            {
                float d = Vector3.Distance(transform.position, PlayerController.Instance.transform.position);
                if (d <= contactDistance)
                {
                    TryDamagePlayer(PlayerController.Instance.gameObject);
                }
            }
        }

        private void LateUpdate()
        {
            UpdateHeadBarOrientation();
        }

        private void UpdateHeadBarOrientation()
        {
            if (headBarRoot == null || !headBarRoot.gameObject.activeSelf) return;

            // 保持在生物正上方世界坐标 1.55m，防止胶囊体翻滚或受击时歪斜
            headBarRoot.position = transform.position + Vector3.up * 1.55f;

            // 动态面向视线：优先主摄像机视点（随玩家移动与摄像机跟随平滑变向），备选玩家角色坐标
            Vector3 viewPos = Vector3.zero;
            if (Camera.main != null)
            {
                viewPos = Camera.main.transform.position;
            }
            else if (PlayerController.Instance != null)
            {
                viewPos = PlayerController.Instance.transform.position;
            }

            if (viewPos != Vector3.zero)
            {
                Vector3 toViewer = viewPos - headBarRoot.position;
                if (toViewer.sqrMagnitude > 0.001f)
                {
                    headBarRoot.rotation = Quaternion.LookRotation(-toViewer.normalized, Vector3.up);
                }
            }
        }

        /// <summary>
        /// 外部指示圈每帧通知：玩家是否在此敌人的有效攻击范围内
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
                // 核心规则：仅在玩家攻击范围内且未死亡时显示血条！
                headBarRoot.gameObject.SetActive(_isInAttackRange && !_isDead);
            }
        }

        private void UpdateVisualColor()
        {
            if (_renderer == null) return;

            Color baseCol = baseBodyColor;
            Color outlineCol = Color.clear;
            float currentWidth = 0f;

            if (_isInAttackRange && !_isDead)
            {
                outlineCol = inRangeOutlineColor;
                currentWidth = outlineWidth;
            }

            if (_propBlock == null) _propBlock = new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(_propBlock);
            _propBlock.SetColor(BaseColorProp, baseCol);
            _propBlock.SetColor(OutlineColorProp, outlineCol);
            _propBlock.SetFloat(OutlineWidthProp, currentWidth);
            _renderer.SetPropertyBlock(_propBlock);
        }

        /// <summary>
        /// 受到攻击扣血
        /// </summary>
        public void TakeDamage(float damage)
        {
            if (_isDead) return;

            currentHealth = Mathf.Max(0f, currentHealth - damage);
            if (hpSlider != null) hpSlider.value = currentHealth;
            if (statusText != null) statusText.text = $"敌人 HP: {Mathf.CeilToInt(currentHealth)}/{maxHealth}";

            if (currentHealth <= 0.01f)
            {
                Die();
            }
        }

        private void Die()
        {
            _isDead = true;
            currentHealth = 0f;

            if (headBarRoot != null) headBarRoot.gameObject.SetActive(false);

            StartCoroutine(DisappearAndRespawnRoutine());
        }

        private IEnumerator DisappearAndRespawnRoutine()
        {
            yield return new WaitForSeconds(0.3f);

            // 隐藏
            if (_renderer != null) _renderer.enabled = false;
            if (_collider != null) _collider.enabled = false;

            // 等待重生
            yield return new WaitForSeconds(respawnDelay);

            // 原地重生
            ResetCreature();
        }

        private void ResetCreature()
        {
            _isDead = false;
            currentHealth = maxHealth;
            _isInAttackRange = false;

            transform.position = _spawnPosition;
            transform.rotation = _spawnRotation;

            if (_renderer != null) _renderer.enabled = true;
            if (_collider != null) _collider.enabled = true;

            if (hpSlider != null)
            {
                hpSlider.maxValue = maxHealth;
                hpSlider.value = currentHealth;
            }
            if (statusText != null)
            {
                statusText.text = $"敌人 HP: {maxHealth}/{maxHealth}";
            }

            UpdateVisualColor();
            UpdateHeadBarVisibility();
        }

        private void EnsureVisuals()
        {
            if (_renderer != null)
            {
                Shader shader = Shader.Find("PixelSaur/CreatureHighlight") ?? Shader.Find("Universal Render Pipeline/Lit");
                if (shader != null && (_renderer.sharedMaterial == null || _renderer.sharedMaterial.shader != shader))
                {
                    Material mat = new Material(shader) { name = "M_RedHazard_Mat" };
                    mat.SetColor("_BaseColor", baseBodyColor);
                    mat.SetColor("_OutlineColor", Color.clear);
                    mat.SetFloat("_OutlineWidth", 0f);
                    _renderer.sharedMaterial = mat;
                }
            }
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
                Transform panelExist = hbTr.Find("PanelBg");
                if (hpSlider != null && statusText != null && panelExist != null)
                {
                    Canvas cExist = hbTr.GetComponent<Canvas>();
                    if (cExist != null)
                    {
                        cExist.overrideSorting = true;
                        cExist.sortingOrder = 100;
                        if (Camera.main != null) cExist.worldCamera = Camera.main;
                    }
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
                hbObj = new GameObject("HeadBar", typeof(RectTransform), typeof(Canvas));
                hbObj.transform.SetParent(transform, false);
                hbObj.transform.localPosition = new Vector3(0f, 1.55f, 0f);
            }

            headBarRoot = hbObj.transform;

            Canvas canvas = hbObj.GetComponent<Canvas>();
            if (canvas == null) canvas = hbObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 100; // 高优先级排序，彻底防止水面或半透明物体遮挡导致变淡
            if (Camera.main != null) canvas.worldCamera = Camera.main;

            RectTransform canvasRT = hbObj.GetComponent<RectTransform>();
            canvasRT.sizeDelta = new Vector2(260f, 70f);
            canvasRT.localScale = Vector3.one * 0.0035f;

            // 0. 深色半透明背景底板 (增强强光/水面/天空下的对比度)
            GameObject panelObj = new GameObject("PanelBg", typeof(RectTransform), typeof(Image));
            panelObj.transform.SetParent(headBarRoot, false);
            RectTransform panelRT = panelObj.GetComponent<RectTransform>();
            panelRT.anchorMin = Vector2.zero;
            panelRT.anchorMax = Vector2.one;
            panelRT.sizeDelta = Vector2.zero;
            Image panelImg = panelObj.GetComponent<Image>();
            panelImg.color = new Color(0.08f, 0.09f, 0.12f, 0.82f);

            // 1. Slider
            GameObject sliderObj = new GameObject("HPSlider", typeof(RectTransform), typeof(Slider));
            sliderObj.transform.SetParent(headBarRoot, false);
            RectTransform sliderRT = sliderObj.GetComponent<RectTransform>();
            sliderRT.sizeDelta = new Vector2(230f, 22f);
            sliderRT.anchoredPosition = new Vector2(0f, 13f);

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
            fillImg.color = new Color(0.95f, 0.20f, 0.20f, 1f); // 鲜红血条

            slider.fillRect = fillRT;
            slider.targetGraphic = bgImg;
            slider.maxValue = maxHealth;
            slider.value = currentHealth;
            hpSlider = slider;

            // 2. StatusText (高清晰度文字配置)
            GameObject textObj = new GameObject("StatusText", typeof(RectTransform), typeof(Text), typeof(Outline));
            textObj.transform.SetParent(headBarRoot, false);
            RectTransform textRT = textObj.GetComponent<RectTransform>();
            textRT.sizeDelta = new Vector2(250f, 28f);
            textRT.anchoredPosition = new Vector2(0f, -14f);

            statusText = textObj.GetComponent<Text>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            statusText.font = font;
            statusText.fontSize = 22;
            statusText.fontStyle = FontStyle.Bold;
            statusText.alignment = TextAnchor.MiddleCenter;
            statusText.color = Color.white;
            statusText.text = $"敌人 HP: {Mathf.CeilToInt(currentHealth)}/{maxHealth}";

            Outline outline = textObj.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            // 初始隐藏，进入攻击范围才显示！
            headBarRoot.gameObject.SetActive(false);
        }

        public void NotifyPlayerContact(PlayerController player)
        {
            if (player != null && !_isDead)
            {
                TryDamagePlayer(player.gameObject);
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (!_isDead) TryDamagePlayer(other.gameObject);
        }

        private void OnCollisionStay(Collision collision)
        {
            if (!_isDead) TryDamagePlayer(collision.gameObject);
        }

        private void TryDamagePlayer(GameObject targetObj)
        {
            if (_isDead || Time.time - _lastDamageTime < damageInterval) return;

            PlayerController player = targetObj.GetComponentInParent<PlayerController>();
            if (player == null) return;

            _lastDamageTime = Time.time;

            // 1. 扣减玩家生命
            if (PlayerStatusUI.Instance != null)
            {
                PlayerStatusUI.Instance.ModifyHealth(-damageAmount);
            }

            // 2. 给予微小击退反冲力
            Vector3 pushDir = (player.transform.position - transform.position);
            pushDir.y = 0f;
            if (pushDir.sqrMagnitude > 0.001f)
            {
                player.PerformDash(pushDir.normalized, knockbackForce, 0.12f);
            }

            Debug.Log($"<color=#FF3333>[RedHazardCapsule] 触碰玩家！造成 {damageAmount} 点伤害。</color>");
        }
    }
}
