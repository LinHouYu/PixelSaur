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

        [Header("原神/开放世界摄像机朝向配置 (可自由微调)")]
        [Tooltip("是否实时看向主摄像机（原神/开放世界模式）。无论玩家在哪个角度，血条正面始终面向摄像机，清晰无变形")]
        [SerializeField] private bool faceCamera = true;

        [Tooltip("是否开启全角度屏幕对齐（原神模式）。开启后不论仰视俯视，血条完全平面对齐屏幕，绝不倾斜或压扁")]
        [SerializeField] private bool screenAligned = true;

        [Tooltip("触发显示头顶血条的感知距离（米），玩家靠近在此距离内显示，离开自动隐藏")]
        [SerializeField] private float showHealthDistance = 3.5f;

        [Tooltip("头顶血条相对生物底部世界空间的悬浮高度（米）")]
        [SerializeField] private float headBarHeight = 1.55f;

        [Header("朝向性能调节与回退配置")]
        [Tooltip("朝向更新间隔（秒）。原神模式下直接对齐相机四元数仅为4个float赋值，设为0即每帧实时对齐；如需节流可设为0.15s")]
        [SerializeField] private float orientationUpdateInterval = 0f;

        [Tooltip("若关闭全角度屏幕对齐，是否锁定水平 Y 轴朝向相机（垂直立正）")]
        [SerializeField] private bool lockYAxis = false;

        [Tooltip("是否开启平滑插值旋转")]
        [SerializeField] private bool smoothRotate = false;

        [Tooltip("平滑旋转插值速度")]
        [SerializeField] private float rotateSpeed = 15.0f;

        [Header("头顶 UI (仅在靠近或攻击范围内显示)")]
        [SerializeField] private Transform headBarRoot;
        [SerializeField] private Slider hpSlider;
        [SerializeField] private Text statusText;

        private float _lastDamageTime = -999f;
        private float _lastRangePingTime = -999f;
        private float _lastOrientationUpdateTime = -999f;
        private Quaternion _targetRotation = Quaternion.identity;
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
            if (!enabled) enabled = true; // 防御机制：强制激活组件，确保 Update 与 LateUpdate 正常执行
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
            // 1. 距离感知：玩家靠近显示血条，离开自动隐藏
            UpdateDistanceAndVisibility();

            // 2. 贴身触碰检测：当角色贴近胶囊体且敌人存活时稳定触发扣血
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

        private void UpdateDistanceAndVisibility()
        {
            Transform playerTr = PlayerController.Instance != null 
                ? PlayerController.Instance.transform 
                : GameObject.FindGameObjectWithTag("Player")?.transform;

            bool isNear = false;
            if (playerTr != null && !_isDead)
            {
                float dist = Vector3.Distance(transform.position, playerTr.position);
                isNear = (dist <= showHealthDistance);
            }

            // 心跳通知判定（如果有外部指示圈触发也同步支持）
            bool inRange = isNear || (_isInAttackRange && Time.time - _lastRangePingTime <= 0.15f);

            if (_isInAttackRange != inRange)
            {
                _isInAttackRange = inRange;
                UpdateVisualColor();
                UpdateHeadBarVisibility();
            }
            else
            {
                UpdateHeadBarVisibility();
            }
        }

        private Camera _cachedMainCamera;

        private Camera GetMainCamera()
        {
            if (_cachedMainCamera == null || !_cachedMainCamera.gameObject.activeInHierarchy)
            {
                _cachedMainCamera = Camera.main;
            }
            return _cachedMainCamera;
        }

        private void UpdateHeadBarOrientation()
        {
            if (headBarRoot == null || !headBarRoot.gameObject.activeSelf) return;

            // 保持在生物正上方世界坐标 headBarHeight，防止胶囊体翻滚或受击时歪斜
            headBarRoot.position = transform.position + Vector3.up * headBarHeight;

            // 核心：原神/开放世界经典模式，血条正面时时看着摄像机，保持正面无论玩家哪个角度看都是始终保持正面
            if (faceCamera)
            {
                Camera cam = GetMainCamera();
                if (cam == null) return;

                if (screenAligned)
                {
                    // 屏幕平行公告板模式（原神模式）：直接与相机四元数对齐，无论任何视角，血条绝对正对屏幕
                    if (orientationUpdateInterval <= 0f)
                    {
                        headBarRoot.rotation = cam.transform.rotation;
                        return;
                    }
                    else if (Time.time - _lastOrientationUpdateTime >= orientationUpdateInterval)
                    {
                        _lastOrientationUpdateTime = Time.time;
                        _targetRotation = cam.transform.rotation;
                    }
                }
                else
                {
                    // 视线朝向模式
                    if (orientationUpdateInterval <= 0f || Time.time - _lastOrientationUpdateTime >= orientationUpdateInterval)
                    {
                        _lastOrientationUpdateTime = Time.time;
                        Vector3 dirToCam = cam.transform.position - headBarRoot.position;
                        if (lockYAxis) dirToCam.y = 0f;
                        if (dirToCam.sqrMagnitude > 0.001f)
                        {
                            _targetRotation = Quaternion.LookRotation(-dirToCam.normalized, Vector3.up);
                        }
                    }
                }
            }
            else
            {
                // 回退模式：朝向玩家角色模型
                Transform playerTr = PlayerController.Instance != null 
                    ? PlayerController.Instance.transform 
                    : GameObject.FindGameObjectWithTag("Player")?.transform;

                if (playerTr == null) return;

                if (orientationUpdateInterval <= 0f || Time.time - _lastOrientationUpdateTime >= orientationUpdateInterval)
                {
                    _lastOrientationUpdateTime = Time.time;
                    Vector3 toPlayer = playerTr.position - headBarRoot.position;
                    if (lockYAxis) toPlayer.y = 0f;
                    if (toPlayer.sqrMagnitude > 0.001f)
                    {
                        _targetRotation = Quaternion.LookRotation(-toPlayer.normalized, Vector3.up);
                    }
                }
            }

            if (smoothRotate)
            {
                headBarRoot.rotation = Quaternion.Slerp(headBarRoot.rotation, _targetRotation, Time.deltaTime * rotateSpeed);
            }
            else
            {
                headBarRoot.rotation = _targetRotation;
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
                // 核心规则：仅在玩家靠近范围内且未死亡时显示血条！
                bool shouldShow = _isInAttackRange && !_isDead;
                if (headBarRoot.gameObject.activeSelf != shouldShow)
                {
                    headBarRoot.gameObject.SetActive(shouldShow);
                    if (shouldShow)
                    {
                        _lastOrientationUpdateTime = -999f; // 刚显现瞬间立即计算一次面向与位置
                    }
                }
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
                hbObj.transform.localPosition = new Vector3(0f, headBarHeight, 0f);
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
