using System.Collections.Generic;
using UnityEngine;

namespace PixelSaur.Water
{
    /// <summary>
    /// 水面交互涟漪管理器：严格按照技术规范生成并维护 256x256 分辨率的 _GlobalEffectRT。
    /// 跟踪角色涉水运动并生成向外扩散的物理波纹环，
    /// 同时提供全局着色器变量注入与全套 Inspector 微调参数。
    /// </summary>
    public class WaterRippleManager : MonoBehaviour
    {
        public static WaterRippleManager Instance { get; private set; }

        [Header("目标与追踪中心")]
        [Tooltip("交互角色 Transform（涉水检测对象）")]
        [SerializeField] private Transform targetPlayer;

        [Tooltip("水面水平高度（Y 坐标）")]
        [SerializeField] private float waterLevel = 0.0f;

        [Header("RT 贴图与正交范围 (规范控制在 256)")]
        [Tooltip("全局涟漪贴图分辨率，规范固定为 256")]
        [SerializeField] private int rtResolution = 256;

        [Tooltip("正交投影半宽/半高 (_OrthographicCamSize)，决定 256 贴图在世界空间的覆盖范围")]
        [Range(5f, 100f)]
        [SerializeField] private float orthographicCamSize = 25.0f;

        [Header("涟漪形态与颜色")]
        [Tooltip("涟漪高光颜色 (_RippleColor)")]
        [SerializeField] private Color rippleColor = Color.white;

        [Tooltip("水波扩散速率（米/秒）")]
        [Range(0.5f, 10.0f)]
        [SerializeField] private float rippleExpandSpeed = 3.0f;

        [Tooltip("涟漪波纹环单圈粗细")]
        [Range(0.005f, 0.06f)]
        [SerializeField] private float rippleRingThickness = 0.018f;

        [Tooltip("涟漪持续消退速度")]
        [Range(0.2f, 5.0f)]
        [SerializeField] private float rippleFadeSpeed = 1.2f;

        [Tooltip("涉水移动触发新涟漪的位移间隔（米）")]
        [Range(0.1f, 2.0f)]
        [SerializeField] private float spawnIntervalDistance = 0.5f;

        [Header("材质引用")]
        [SerializeField] private Shader painterShader;

        private class RippleInstance
        {
            public Vector2 centerUV;
            public float currentRadiusUV;
            public float maxRadiusUV;
            public float intensity;
            public bool isDead;
        }

        private readonly List<RippleInstance> _activeRipples = new List<RippleInstance>();
        private RenderTexture _rtA;
        private RenderTexture _rtB;
        private bool _pingPong;
        private Material _painterMaterial;
        private Vector3 _lastSpawnPos;
        private Vector3 _centerPosition;

        private static readonly int GlobalEffectRTProp = Shader.PropertyToID("_GlobalEffectRT");
        private static readonly int PositionProp = Shader.PropertyToID("_Position");
        private static readonly int OrthographicCamSizeProp = Shader.PropertyToID("_OrthographicCamSize");
        private static readonly int RippleColorProp = Shader.PropertyToID("_RippleColor");

        private static readonly int RippleCenterProp = Shader.PropertyToID("_RippleCenter");
        private static readonly int RippleRadiusProp = Shader.PropertyToID("_RippleRadius");
        private static readonly int RippleThicknessProp = Shader.PropertyToID("_RippleThickness");
        private static readonly int RippleIntensityProp = Shader.PropertyToID("_RippleIntensity");
        private static readonly int FadeRateProp = Shader.PropertyToID("_FadeRate");
        private static readonly int DeltaTimeProp = Shader.PropertyToID("_DeltaTime");

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            InitRenderTextures();

            if (painterShader == null)
            {
                painterShader = Shader.Find("Hidden/PixelSaur/WaterRipplePainter");
            }
            if (painterShader != null)
            {
                _painterMaterial = new Material(painterShader);
            }
        }

        private void Start()
        {
            FindPlayerIfNull();
            if (targetPlayer != null)
            {
                _lastSpawnPos = targetPlayer.position;
                _centerPosition = targetPlayer.position;
            }
            else
            {
                _centerPosition = transform.position;
            }
        }

        private void Update()
        {
            FindPlayerIfNull();
            CheckPlayerMovement();
            SimulateRipples();
        }

        private void OnDestroy()
        {
            ReleaseRenderTextures();
            if (_painterMaterial != null)
            {
                Destroy(_painterMaterial);
            }
        }

        private void FindPlayerIfNull()
        {
            if (targetPlayer == null)
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                {
                    targetPlayer = playerObj.transform;
                }
            }
        }

        private void InitRenderTextures()
        {
            ReleaseRenderTextures();

            RenderTextureDescriptor desc = new RenderTextureDescriptor(rtResolution, rtResolution, RenderTextureFormat.RHalf, 0)
            {
                autoGenerateMips = false
            };

            _rtA = new RenderTexture(desc)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _rtB = new RenderTexture(desc)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _rtA.Create();
            _rtB.Create();

            ClearRT(_rtA);
            ClearRT(_rtB);
        }

        private void ClearRT(RenderTexture rt)
        {
            RenderTexture active = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(false, true, Color.black);
            RenderTexture.active = active;
        }

        private void ReleaseRenderTextures()
        {
            if (_rtA != null) { _rtA.Release(); _rtA = null; }
            if (_rtB != null) { _rtB.Release(); _rtB = null; }
        }

        private void CheckPlayerMovement()
        {
            if (targetPlayer == null) return;

            Vector3 currentPos = targetPlayer.position;

            // 检测是否在水面高度附近（例如水面上下 1.2 米内）
            bool isInWater = Mathf.Abs(currentPos.y - waterLevel) < 1.5f;

            if (isInWater && Vector3.Distance(currentPos, _lastSpawnPos) >= spawnIntervalDistance)
            {
                CreateRippleAtWorldPos(currentPos);
                _lastSpawnPos = currentPos;
            }

            // 中心点可平滑平移或跟踪
            _centerPosition = targetPlayer.position;
        }

        /// <summary>
        /// 在指定世界坐标生成一圈水波涟漪
        /// </summary>
        public void CreateRippleAtWorldPos(Vector3 worldPos)
        {
            float coverage = orthographicCamSize * 2.0f;
            float uvX = (worldPos.x - _centerPosition.x) / coverage + 0.5f;
            float uvY = (worldPos.z - _centerPosition.z) / coverage + 0.5f;

            if (uvX >= 0f && uvX <= 1f && uvY >= 0f && uvY <= 1f)
            {
                _activeRipples.Add(new RippleInstance
                {
                    centerUV = new Vector2(uvX, uvY),
                    currentRadiusUV = 0.005f,
                    maxRadiusUV = 0.25f,
                    intensity = 1.0f,
                    isDead = false
                });
            }
        }

        private void SimulateRipples()
        {
            if (_painterMaterial == null || _rtA == null || _rtB == null) return;

            float coverage = orthographicCamSize * 2.0f;
            float radiusSpeedUV = (rippleExpandSpeed / coverage);

            // 1. 更新所有活跃涟漪
            for (int i = _activeRipples.Count - 1; i >= 0; i--)
            {
                RippleInstance rip = _activeRipples[i];
                rip.currentRadiusUV += radiusSpeedUV * Time.deltaTime;
                rip.intensity -= (rippleFadeSpeed * 0.5f) * Time.deltaTime;

                if (rip.intensity <= 0.01f || rip.currentRadiusUV >= rip.maxRadiusUV)
                {
                    _activeRipples.RemoveAt(i);
                }
            }

            // 2. 依次叠加绘制至 256x256 RT
            RenderTexture src = _pingPong ? _rtA : _rtB;
            RenderTexture dst = _pingPong ? _rtB : _rtA;

            _painterMaterial.SetFloat(FadeRateProp, rippleFadeSpeed);
            _painterMaterial.SetFloat(DeltaTimeProp, Time.deltaTime);
            _painterMaterial.SetFloat(RippleThicknessProp, rippleRingThickness);

            if (_activeRipples.Count > 0)
            {
                // 绘制最新活跃涟漪
                RippleInstance primary = _activeRipples[_activeRipples.Count - 1];
                _painterMaterial.SetVector(RippleCenterProp, new Vector4(primary.centerUV.x, primary.centerUV.y, 0, 0));
                _painterMaterial.SetFloat(RippleRadiusProp, primary.currentRadiusUV);
                _painterMaterial.SetFloat(RippleIntensityProp, primary.intensity);
            }
            else
            {
                _painterMaterial.SetVector(RippleCenterProp, new Vector4(-10, -10, 0, 0));
                _painterMaterial.SetFloat(RippleRadiusProp, 0);
                _painterMaterial.SetFloat(RippleIntensityProp, 0);
            }

            Graphics.Blit(src, dst, _painterMaterial);
            _pingPong = !_pingPong;

            // 3. 严格按照要求绑定全局 Shader 变量
            Shader.SetGlobalTexture(GlobalEffectRTProp, dst);
            Shader.SetGlobalVector(PositionProp, new Vector4(_centerPosition.x, _centerPosition.y, _centerPosition.z, 0));
            Shader.SetGlobalFloat(OrthographicCamSizeProp, orthographicCamSize);
            Shader.SetGlobalColor(RippleColorProp, rippleColor);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.6f, 1.0f, 0.4f);
            Vector3 center = targetPlayer != null ? new Vector3(targetPlayer.position.x, waterLevel, targetPlayer.position.z) : transform.position;
            Gizmos.DrawWireCube(center, new Vector3(orthographicCamSize * 2f, 0.2f, orthographicCamSize * 2f));
        }
#endif
    }
}
