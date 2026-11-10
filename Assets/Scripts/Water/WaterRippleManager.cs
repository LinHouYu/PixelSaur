using System.Collections.Generic;
using UnityEngine;

namespace PixelSaur.Water
{
    /// <summary>
    /// 水面交互涟漪管理器：严格按照技术规范生成并维护 256x256 分辨率的 _GlobalEffectRT。
    /// 支持角色在海水中走动与驻留时实时激起多圈同心扩散波纹，
    /// 并提供全局着色器变量注入与全套 Inspector 微调参数。
    /// </summary>
    [ExecuteAlways]
    public class WaterRippleManager : MonoBehaviour
    {
        public static WaterRippleManager Instance { get; private set; }

        [Header("目标与水面高度参考")]
        [Tooltip("交互角色 Transform（涉水检测对象）")]
        [SerializeField] private Transform targetPlayer;

        [Tooltip("水面水平高度（Y 坐标，与 WaterSurface_Plane 保持一致）")]
        [SerializeField] private float waterLevel = 0.038f;

        [Header("RT 贴图与正交范围 (规范控制在 256)")]
        [Tooltip("全局涟漪贴图分辨率，规范固定为 256")]
        [SerializeField] private int rtResolution = 256;

        [Tooltip("正交投影半宽/半高 (_OrthographicCamSize)，决定 256 贴图在世界空间的覆盖范围")]
        [Range(5f, 60f)]
        [SerializeField] private float orthographicCamSize = 14.0f;

        [Header("涟漪形态与颜色微调")]
        [Tooltip("涟漪高光颜色 (_RippleColor)")]
        [SerializeField] private Color rippleColor = Color.white;

        [Tooltip("水波向外扩散速率（米/秒）")]
        [Range(0.5f, 8.0f)]
        [SerializeField] private float rippleExpandSpeed = 1.1f;

        [Tooltip("单个涟漪最大扩散半径（米）")]
        [Range(0.8f, 6.0f)]
        [SerializeField] private float maxRippleWorldRadius = 2.8f;

        [Tooltip("涟漪波纹环单圈粗细（UV 空间占比）")]
        [Range(0.002f, 0.04f)]
        [SerializeField] private float rippleRingThickness = 0.008f;

        [Tooltip("涟漪内圈残影消退速度（保持圆环中空清澈）")]
        [Range(0.5f, 12.0f)]
        [SerializeField] private float rippleFadeSpeed = 3.2f;

        [Header("涉水触发灵敏度")]
        [Tooltip("角色在水中移动触发新涟漪的步幅间隔（米）")]
        [Range(0.1f, 2.0f)]
        [SerializeField] private float spawnIntervalDistance = 0.55f;

        [Tooltip("角色在水中静止站立时产生呼吸波纹的间隔（秒）")]
        [Range(0.4f, 4.0f)]
        [SerializeField] private float idleRippleInterval = 1.8f;

        [Header("材质引用")]
        [SerializeField] private Shader painterShader;

        private class RippleInstance
        {
            public Vector3 worldCenter;
            public float currentWorldRadius;
            public float maxWorldRadius;
            public float intensity;
        }

        private readonly List<RippleInstance> _activeRipples = new List<RippleInstance>();
        private readonly Vector4[] _rippleShaderArray = new Vector4[8];
        private RenderTexture _rtA;
        private RenderTexture _rtB;
        private bool _pingPong;
        private Material _painterMaterial;
        private Vector3 _lastSpawnPos;
        private float _lastSpawnTime;
        private Vector3 _centerPosition;
        private bool _hasCenterInit;

        private static readonly int GlobalEffectRTProp = Shader.PropertyToID("_GlobalEffectRT");
        private static readonly int PositionProp = Shader.PropertyToID("_Position");
        private static readonly int OrthographicCamSizeProp = Shader.PropertyToID("_OrthographicCamSize");
        private static readonly int RippleColorProp = Shader.PropertyToID("_RippleColor");

        private static readonly int UVOffsetProp = Shader.PropertyToID("_UVOffset");
        private static readonly int RippleThicknessProp = Shader.PropertyToID("_RippleThickness");
        private static readonly int FadeRateProp = Shader.PropertyToID("_FadeRate");
        private static readonly int DeltaTimeProp = Shader.PropertyToID("_DeltaTime");
        private static readonly int ActiveRippleCountProp = Shader.PropertyToID("_ActiveRippleCount");
        private static readonly int RippleRingsProp = Shader.PropertyToID("_RippleRings");

        public float WaterLevel
        {
            get => waterLevel;
            set => waterLevel = value;
        }

        private void OnEnable()
        {
            Instance = this;
            EnsureResources();
            FindPlayerIfNull();
            if (targetPlayer != null)
            {
                _lastSpawnPos = targetPlayer.position;
                _centerPosition = targetPlayer.position;
                _hasCenterInit = true;
            }
        }

        private void Update()
        {
            EnsureResources();
            FindPlayerIfNull();
            CheckPlayerWaterInteraction();
            SimulateRipples();
        }

        private void OnDisable()
        {
            ReleaseRenderTextures();
            if (_painterMaterial != null)
            {
                DestroyImmediate(_painterMaterial);
                _painterMaterial = null;
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

        private void EnsureResources()
        {
            if (_rtA == null || _rtB == null || !_rtA.IsCreated() || !_rtB.IsCreated() || _rtA.width != rtResolution)
            {
                InitRenderTextures();
            }

            if (painterShader == null)
            {
                painterShader = Shader.Find("Hidden/PixelSaur/WaterRipplePainter");
            }
            if (_painterMaterial == null && painterShader != null)
            {
                _painterMaterial = new Material(painterShader) { hideFlags = HideFlags.HideAndDontSave };
            }
        }

        private void InitRenderTextures()
        {
            ReleaseRenderTextures();

            int res = Mathf.Max(32, rtResolution);
            RenderTextureDescriptor desc = new RenderTextureDescriptor(res, res, RenderTextureFormat.RHalf, 0)
            {
                autoGenerateMips = false
            };

            _rtA = new RenderTexture(desc)
            {
                name = "GlobalEffectRT_A_256",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            _rtB = new RenderTexture(desc)
            {
                name = "GlobalEffectRT_B_256",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
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
            if (_rtA != null) { _rtA.Release(); DestroyImmediate(_rtA); _rtA = null; }
            if (_rtB != null) { _rtB.Release(); DestroyImmediate(_rtB); _rtB = null; }
        }

        private void CheckPlayerWaterInteraction()
        {
            if (targetPlayer == null) return;

            Vector3 currentPos = targetPlayer.position;

            // 计算角色脚底高度
            float footY = currentPos.y - (targetPlayer.lossyScale.y * 1.0f);
            Renderer playerRenderer = targetPlayer.GetComponent<Renderer>();
            if (playerRenderer != null)
            {
                footY = playerRenderer.bounds.min.y;
            }

            // 当角色脚底接触或低于水面（允许轻微岸边水花余量 +0.018m）时判定为涉水互动状态
            bool isWadingInWater = footY <= (waterLevel + 0.018f);

            if (isWadingInWater)
            {
                float moveDist = Vector2.Distance(new Vector2(currentPos.x, currentPos.z), new Vector2(_lastSpawnPos.x, _lastSpawnPos.z));
                float now = Application.isPlaying ? Time.time : (float)
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.timeSinceStartup
#else
                    0f
#endif
                    ;

                if (moveDist >= spawnIntervalDistance)
                {
                    CreateRippleAtWorldPos(currentPos, 1.0f);
                    _lastSpawnPos = currentPos;
                    _lastSpawnTime = now;
                }
                else if (now - _lastSpawnTime >= idleRippleInterval)
                {
                    // 角色在水中驻留时也会产生轻柔的环状水波互动
                    CreateRippleAtWorldPos(currentPos, 0.9f);
                    _lastSpawnTime = now;
                }
            }
        }

        /// <summary>
        /// 在指定世界坐标处激起一圈水波纹
        /// </summary>
        public void CreateRippleAtWorldPos(Vector3 worldPos, float initialIntensity = 1.0f)
        {
            if (_activeRipples.Count >= 8)
            {
                _activeRipples.RemoveAt(0);
            }

            _activeRipples.Add(new RippleInstance
            {
                worldCenter = worldPos,
                currentWorldRadius = 0.42f,
                maxWorldRadius = maxRippleWorldRadius,
                intensity = initialIntensity
            });
        }

        private void SimulateRipples()
        {
            if (_painterMaterial == null || _rtA == null || _rtB == null) return;

            Vector3 newCenter = targetPlayer != null ? targetPlayer.position : transform.position;
            if (!_hasCenterInit)
            {
                _centerPosition = newCenter;
                _hasCenterInit = true;
            }

            float coverage = Mathf.Max(2f, orthographicCamSize * 2.0f);
            Vector3 prevCenter = _centerPosition;
            _centerPosition = newCenter;

            float uvShiftX = (_centerPosition.x - prevCenter.x) / coverage;
            float uvShiftY = (_centerPosition.z - prevCenter.z) / coverage;

            float dt = Application.isPlaying ? Time.deltaTime : 0.016f;

            // 1. 更新所有活跃水波纹的半径与强度
            for (int i = _activeRipples.Count - 1; i >= 0; i--)
            {
                RippleInstance rip = _activeRipples[i];
                rip.currentWorldRadius += rippleExpandSpeed * dt;
                float lifeProgress = Mathf.Clamp01(rip.currentWorldRadius / Mathf.Max(0.1f, rip.maxWorldRadius));
                // 保持前段强度充足 (>0.35)，以适配 WaterShader 中 step(0.9, ripples * 3) 的高光裁剪
                rip.intensity = Mathf.Lerp(1.0f, 0.25f, lifeProgress * lifeProgress);

                if (rip.currentWorldRadius >= rip.maxWorldRadius)
                {
                    _activeRipples.RemoveAt(i);
                }
            }

            // 2. 组装着色器数组
            int count = Mathf.Min(8, _activeRipples.Count);
            for (int i = 0; i < 8; i++)
            {
                if (i < count)
                {
                    RippleInstance r = _activeRipples[i];
                    float uvX = (r.worldCenter.x - _centerPosition.x) / coverage + 0.5f;
                    float uvY = (r.worldCenter.z - _centerPosition.z) / coverage + 0.5f;
                    float radiusUV = r.currentWorldRadius / coverage;
                    _rippleShaderArray[i] = new Vector4(uvX, uvY, radiusUV, r.intensity);
                }
                else
                {
                    _rippleShaderArray[i] = Vector4.zero;
                }
            }

            // 3. 渲染更新 256x256 _GlobalEffectRT
            RenderTexture src = _pingPong ? _rtA : _rtB;
            RenderTexture dst = _pingPong ? _rtB : _rtA;

            _painterMaterial.SetVector(UVOffsetProp, new Vector4(uvShiftX, uvShiftY, 0, 0));
            _painterMaterial.SetFloat(FadeRateProp, rippleFadeSpeed);
            _painterMaterial.SetFloat(DeltaTimeProp, dt);
            _painterMaterial.SetFloat(RippleThicknessProp, rippleRingThickness);
            _painterMaterial.SetInt(ActiveRippleCountProp, count);
            _painterMaterial.SetVectorArray(RippleRingsProp, _rippleShaderArray);

            Graphics.Blit(src, dst, _painterMaterial);
            _pingPong = !_pingPong;

            // 4. 严格按照技术规范注入全局 Shader 变量
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
