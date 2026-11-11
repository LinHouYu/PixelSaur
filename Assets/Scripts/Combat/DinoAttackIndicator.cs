using UnityEngine;
using PixelSaur.Player;

namespace PixelSaur.Combat
{
    public enum IndicatorState
    {
        IdleHidden,
        NormalMove,
        AttackEnemy,  // 红色：可攻击敌人/存活猎物
        FeedCorpse,   // 黄色：可食用猎物尸体
        DrinkWater    // 蓝色：可饮水区域
    }

    /// <summary>
    /// 恐龙近战攻击范围指示圈：
    /// 1. 移动或进入交互状态时动态显示，静止时平滑淡出；
    /// 2. 随目标智能变色：攻击红色、喝水蓝色、吃猎物黄色；
    /// 3. 自适应地面贴合与防穿模（Raycast 贴地 + URP 深度负偏移）；
    /// 4. 专为恐龙模拟器近身撕咬调校，范围小巧紧凑。
    /// </summary>
    [ExecuteAlways]
    public class DinoAttackIndicator : MonoBehaviour
    {
        [Header("范围与几何尺寸")]
        [Tooltip("恐龙咬击有效半径（米）")]
        [Range(1.0f, 4.0f)]
        [SerializeField] private float attackRadius = 1.85f;

        [Tooltip("贴地短射线检测层级（地面、地形）")]
        [SerializeField] private LayerMask groundLayer = ~0;

        [Tooltip("相对地表的微小悬浮高度（米，防Z-fighting）")]
        [SerializeField] private float groundOffset = 0.04f;

        [Header("状态变色配置")]
        [SerializeField] private Color normalColor = new Color(1.0f, 0.45f, 0.45f, 0.55f);
        [SerializeField] private Color attackColor = new Color(1.0f, 0.15f, 0.15f, 0.90f);
        [SerializeField] private Color feedColor = new Color(1.0f, 0.85f, 0.15f, 0.90f);
        [SerializeField] private Color waterColor = new Color(0.18f, 0.72f, 1.0f, 0.90f);

        [Header("淡入淡出速度")]
        [SerializeField] private float fadeSpeed = 8.0f;

        [Header("运行时引用（自动生成与绑定）")]
        [SerializeField] private MeshRenderer ringRenderer;
        [SerializeField] private Material indicatorMaterial;

        private MaterialPropertyBlock _propBlock;
        private IndicatorState _currentState = IndicatorState.IdleHidden;
        private float _currentAlpha = 0f;
        private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");

        public float AttackRadius => attackRadius;
        public IndicatorState CurrentState => _currentState;

        private void Awake()
        {
            EnsureGeometry();
        }

        private void OnEnable()
        {
            EnsureGeometry();
        }

        private void Update()
        {
            UpdatePositionAndGroundAlignment();
            UpdateVisuals();
        }

        /// <summary>
        /// 设置当前指示圈判定状态（由外部判定控制器每帧更新）
        /// </summary>
        public void SetIndicatorState(IndicatorState state)
        {
            _currentState = state;
        }

        private void EnsureGeometry()
        {
            if (_propBlock == null) _propBlock = new MaterialPropertyBlock();

            Transform quadTr = transform.Find("IndicatorQuad");
            GameObject quadObj;
            if (quadTr == null)
            {
                quadObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quadObj.name = "IndicatorQuad";
                quadObj.transform.SetParent(transform, false);
                quadObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

                // 移除自带碰撞体
                Collider col = quadObj.GetComponent<Collider>();
                if (col != null)
                {
                    if (Application.isPlaying) Destroy(col);
                    else DestroyImmediate(col);
                }
            }
            else
            {
                quadObj = quadTr.gameObject;
            }

            quadObj.transform.localScale = new Vector3(attackRadius * 2f, attackRadius * 2f, 1f);

            ringRenderer = quadObj.GetComponent<MeshRenderer>();
            ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ringRenderer.receiveShadows = false;

            if (indicatorMaterial == null)
            {
                Shader shader = Shader.Find("PixelSaur/DinoRangeIndicator");
                if (shader != null)
                {
                    indicatorMaterial = new Material(shader) { name = "M_DinoRangeIndicator_Inst" };
                }
            }

            if (indicatorMaterial != null && ringRenderer.sharedMaterial != indicatorMaterial)
            {
                ringRenderer.sharedMaterial = indicatorMaterial;
            }
        }

        private void UpdatePositionAndGroundAlignment()
        {
            Transform parent = transform.parent;
            Vector3 center = parent != null ? parent.position : transform.position;

            // 脚底基准高度（胶囊体高度 2.0，中心 y=1.0，脚底为 center.y - 1.0）
            float feetY = parent != null ? parent.position.y - 1.0f : center.y;

            // 向地表打射线以适应凹凸起伏与坡度，必须过滤掉玩家自身所有碰撞体
            Vector3 rayStart = new Vector3(center.x, feetY + 1.0f, center.z);
            RaycastHit[] hits = Physics.RaycastAll(rayStart, Vector3.down, 4.0f, groundLayer, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            bool foundGround = false;
            foreach (var hit in hits)
            {
                if (parent != null && (hit.transform == parent || hit.transform.IsChildOf(parent) || hit.transform.root == parent.root))
                {
                    continue; // 彻底排除玩家自身碰撞体，绝不打在头顶
                }

                transform.position = hit.point + Vector3.up * groundOffset;
                transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
                foundGround = true;
                break;
            }

            if (!foundGround)
            {
                transform.position = new Vector3(center.x, feetY + groundOffset, center.z);
                transform.rotation = Quaternion.identity;
            }

            if (ringRenderer != null)
            {
                ringRenderer.transform.localScale = new Vector3(attackRadius * 2f, attackRadius * 2f, 1f);
            }
        }

        private void UpdateVisuals()
        {
            if (ringRenderer == null) return;

            // 目标颜色与透明度
            Color targetColor = normalColor;
            float targetAlpha = 0f;

            switch (_currentState)
            {
                case IndicatorState.IdleHidden:
                    targetAlpha = 0f;
                    break;
                case IndicatorState.NormalMove:
                    targetColor = normalColor;
                    targetAlpha = normalColor.a;
                    break;
                case IndicatorState.AttackEnemy:
                    targetColor = attackColor;
                    targetAlpha = attackColor.a;
                    break;
                case IndicatorState.FeedCorpse:
                    targetColor = feedColor;
                    targetAlpha = feedColor.a;
                    break;
                case IndicatorState.DrinkWater:
                    targetColor = waterColor;
                    targetAlpha = waterColor.a;
                    break;
            }

            _currentAlpha = Mathf.MoveTowards(_currentAlpha, targetAlpha, Time.deltaTime * fadeSpeed);

            if (_currentAlpha <= 0.001f)
            {
                ringRenderer.enabled = false;
                return;
            }

            ringRenderer.enabled = true;
            Color finalColor = targetColor;
            finalColor.a = _currentAlpha;

            if (_propBlock == null) _propBlock = new MaterialPropertyBlock();
            ringRenderer.GetPropertyBlock(_propBlock);
            _propBlock.SetColor(BaseColorProp, finalColor);
            ringRenderer.SetPropertyBlock(_propBlock);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRadius);
        }
#endif
    }
}
