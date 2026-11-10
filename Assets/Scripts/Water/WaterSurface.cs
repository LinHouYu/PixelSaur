using UnityEngine;

namespace PixelSaur.Water
{
    /// <summary>
    /// 水面控制器：挂载在水体平面 GameObject 上，
    /// 提供完整的 MinionsArt 风格海水参数（深浅水色、岸边泡沫、波光频率）微调界面，
    /// 并实时同步当前水面高度给 WaterRippleManager。
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshRenderer))]
    public class WaterSurface : MonoBehaviour
    {
        [Header("MinionsArt 水体颜色微调")]
        [Tooltip("浅水区（岛屿岸边）清澈蓝绿色")]
        [SerializeField] private Color shallowColor = new Color(0.22f, 0.78f, 0.88f, 0.78f);

        [Tooltip("深水区（远海）深海蓝色")]
        [SerializeField] private Color deepColor = new Color(0.06f, 0.34f, 0.68f, 0.94f);

        [Tooltip("海岸线与涉水泡沫颜色")]
        [SerializeField] private Color foamColor = new Color(1.0f, 1.0f, 1.0f, 0.95f);

        [Tooltip("256x256 玩家交互涟漪颜色 (_RippleColor)")]
        [SerializeField] private Color rippleColor = Color.white;

        [Header("海岸深度与泡沫参数")]
        [Tooltip("浅水向深水过渡的深度阈值（米）")]
        [Range(0.05f, 3.0f)]
        [SerializeField] private float depthMaxDistance = 0.42f;

        [Tooltip("岛屿岸边及涉水角色周围的白色泡沫带宽（米）")]
        [Range(0.02f, 1.2f)]
        [SerializeField] private float foamDistance = 0.22f;

        [Tooltip("泡沫边缘卡通裁剪阈值（越小泡沫越密）")]
        [Range(0.1f, 0.95f)]
        [SerializeField] private float foamCutoff = 0.52f;

        [Header("波浪与噪点微调")]
        [Range(0.1f, 5.0f)]
        [SerializeField] private float waveSpeed = 1.1f;

        [Range(0.2f, 5.0f)]
        [SerializeField] private float waveFrequency = 1.6f;

        [Tooltip("顶点上下微起伏高度（严格控制在 0.01 以内，确保绝不没过岛屿）")]
        [Range(0.0f, 0.02f)]
        [SerializeField] private float waveHeight = 0.002f;

        [Header("表面高光")]
        [Range(0.0f, 1.0f)]
        [SerializeField] private float smoothness = 0.85f;

        private MeshRenderer _meshRenderer;
        private MaterialPropertyBlock _propBlock;

        private static readonly int WaterColorProp = Shader.PropertyToID("_WaterColor");
        private static readonly int DeepWaterColorProp = Shader.PropertyToID("_DeepWaterColor");
        private static readonly int FoamColorProp = Shader.PropertyToID("_FoamColor");
        private static readonly int RippleColorProp = Shader.PropertyToID("_RippleColor");
        private static readonly int DepthMaxDistanceProp = Shader.PropertyToID("_DepthMaxDistance");
        private static readonly int FoamDistanceProp = Shader.PropertyToID("_FoamDistance");
        private static readonly int FoamCutoffProp = Shader.PropertyToID("_FoamCutoff");
        private static readonly int WaveSpeedProp = Shader.PropertyToID("_WaveSpeed");
        private static readonly int WaveFrequencyProp = Shader.PropertyToID("_WaveFrequency");
        private static readonly int WaveHeightProp = Shader.PropertyToID("_WaveHeight");
        private static readonly int SmoothnessProp = Shader.PropertyToID("_Smoothness");

        private void OnEnable()
        {
            ApplyProperties();
        }

        private void Update()
        {
            if (WaterRippleManager.Instance != null)
            {
                WaterRippleManager.Instance.WaterLevel = transform.position.y;
            }
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                ApplyProperties();
            }
#endif
        }

        private void OnValidate()
        {
            ApplyProperties();
        }

        public void ApplyProperties()
        {
            if (_meshRenderer == null) _meshRenderer = GetComponent<MeshRenderer>();
            if (_meshRenderer == null) return;

            if (_propBlock == null) _propBlock = new MaterialPropertyBlock();

            _meshRenderer.GetPropertyBlock(_propBlock);
            _propBlock.SetColor(WaterColorProp, shallowColor);
            _propBlock.SetColor(DeepWaterColorProp, deepColor);
            _propBlock.SetColor(FoamColorProp, foamColor);
            _propBlock.SetColor(RippleColorProp, rippleColor);
            _propBlock.SetFloat(DepthMaxDistanceProp, depthMaxDistance);
            _propBlock.SetFloat(FoamDistanceProp, foamDistance);
            _propBlock.SetFloat(FoamCutoffProp, foamCutoff);
            _propBlock.SetFloat(WaveSpeedProp, waveSpeed);
            _propBlock.SetFloat(WaveFrequencyProp, waveFrequency);
            _propBlock.SetFloat(WaveHeightProp, waveHeight);
            _propBlock.SetFloat(SmoothnessProp, smoothness);
            _meshRenderer.SetPropertyBlock(_propBlock);
        }
    }
}
