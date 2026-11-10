using UnityEngine;

namespace PixelSaur.Water
{
    /// <summary>
    /// 水面控制器：挂载在水体平面 Gameobject 上，
    /// 提供直观的水体材质参数微调界面，并自动同步当前水面高度给 WaterRippleManager。
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshRenderer))]
    public class WaterSurface : MonoBehaviour
    {
        [Header("水体颜色与视觉微调")]
        [Tooltip("浅水区颜色")]
        [SerializeField] private Color shallowColor = new Color(0.2f, 0.65f, 0.85f, 0.75f);

        [Tooltip("深水区颜色")]
        [SerializeField] private Color deepColor = new Color(0.05f, 0.25f, 0.55f, 0.95f);

        [Tooltip("涟漪高光颜色")]
        [SerializeField] private Color rippleColor = Color.white;

        [Header("波浪微调")]
        [Range(0.1f, 5.0f)]
        [SerializeField] private float waveSpeed = 1.2f;

        [Range(0.1f, 5.0f)]
        [SerializeField] private float waveFrequency = 1.0f;

        [Range(0.0f, 0.5f)]
        [SerializeField] private float waveHeight = 0.05f;

        [Header("表面光泽")]
        [Range(0.0f, 1.0f)]
        [SerializeField] private float smoothness = 0.85f;

        private MeshRenderer _meshRenderer;
        private MaterialPropertyBlock _propBlock;

        private static readonly int WaterColorProp = Shader.PropertyToID("_WaterColor");
        private static readonly int DeepWaterColorProp = Shader.PropertyToID("_DeepWaterColor");
        private static readonly int RippleColorProp = Shader.PropertyToID("_RippleColor");
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
            _propBlock.SetColor(RippleColorProp, rippleColor);
            _propBlock.SetFloat(WaveSpeedProp, waveSpeed);
            _propBlock.SetFloat(WaveFrequencyProp, waveFrequency);
            _propBlock.SetFloat(WaveHeightProp, waveHeight);
            _propBlock.SetFloat(SmoothnessProp, smoothness);
            _meshRenderer.SetPropertyBlock(_propBlock);
        }
    }
}
