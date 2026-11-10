using UnityEngine;

namespace PixelSaur.Grass
{
    /// <summary>
    /// 草地交互管理器：维护 64x64 分辨率的动态交互 RenderTexture（双缓冲），
    /// 实时追踪玩家足迹、记录世界空间压草方向与回弹衰减轨迹，并全局传递给草地 Shader。
    /// </summary>
    [ExecuteAlways]
    public class GrassInteractionManager : MonoBehaviour
    {
        public static GrassInteractionManager Instance { get; private set; }

        [Header("交互跟踪目标")]
        [Tooltip("交互主体（角色）Transform，为空时自动搜寻 Tag 为 Player 的对象")]
        [SerializeField] private Transform targetPlayer;

        [Header("RT 贴图与分辨率 (按规范控制在 64)")]
        [Tooltip("交互贴图分辨率，规范固定为 64")]
        [SerializeField] private int rtResolution = 64;

        [Tooltip("交互覆盖范围区域边长（米），适配草地平原尺寸以获得清晰足迹")]
        [Range(5f, 60f)]
        [SerializeField] private float interactionAreaSize = 14f;

        [Header("足迹与交互微调参数")]
        [Tooltip("角色接触草丛拂碰半径（米），贴合角色身形以产生走过碰到的自然触感")]
        [Range(0.3f, 3.5f)]
        [SerializeField] private float footprintRadius = 0.95f;

        [Tooltip("接触边缘过渡硬度")]
        [Range(0.3f, 4.0f)]
        [SerializeField] private float footprintHardness = 1.0f;

        [Tooltip("草叶受碰后回弹恢复速度（数值越大回弹恢复越迅速轻快）")]
        [Range(0.1f, 5.0f)]
        [SerializeField] private float recoverySpeed = 1.35f;

        [Header("材质引用")]
        [SerializeField] private Shader painterShader;

        private RenderTexture _rtA;
        private RenderTexture _rtB;
        private bool _pingPong;
        private Material _painterMaterial;
        private Vector3 _currentAreaCenter;
        private bool _hasCenterInit;

        private static readonly int GrassInteractionRTProp = Shader.PropertyToID("_GrassInteractionRT");
        private static readonly int GrassInteractionCenterProp = Shader.PropertyToID("_GrassInteractionCenter");
        private static readonly int GrassInteractionAreaSizeProp = Shader.PropertyToID("_GrassInteractionAreaSize");
        private static readonly int PlayerWorldPosProp = Shader.PropertyToID("_PlayerWorldPos");
        private static readonly int PlayerFootprintRadiusProp = Shader.PropertyToID("_PlayerFootprintRadius");

        private static readonly int StampCenterProp = Shader.PropertyToID("_StampCenter");
        private static readonly int UVOffsetProp = Shader.PropertyToID("_UVOffset");
        private static readonly int StampRadiusProp = Shader.PropertyToID("_StampRadius");
        private static readonly int StampHardnessProp = Shader.PropertyToID("_StampHardness");
        private static readonly int FadeRateProp = Shader.PropertyToID("_FadeRate");
        private static readonly int DeltaTimeProp = Shader.PropertyToID("_DeltaTime");

        private void OnEnable()
        {
            Instance = this;
            EnsureResources();
            FindPlayerIfNull();
            if (targetPlayer != null)
            {
                _currentAreaCenter = targetPlayer.position;
                _hasCenterInit = true;
            }
        }

        private void Update()
        {
            EnsureResources();
            FindPlayerIfNull();
            UpdateInteraction();
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
                painterShader = Shader.Find("Hidden/PixelSaur/GrassInteractionPainter");
            }
            if (_painterMaterial == null && painterShader != null)
            {
                _painterMaterial = new Material(painterShader) { hideFlags = HideFlags.HideAndDontSave };
            }
        }

        private void InitRenderTextures()
        {
            ReleaseRenderTextures();

            int res = Mathf.Max(16, rtResolution);
            RenderTextureDescriptor desc = new RenderTextureDescriptor(res, res, RenderTextureFormat.ARGBHalf, 0)
            {
                autoGenerateMips = false,
                enableRandomWrite = false
            };

            _rtA = new RenderTexture(desc)
            {
                name = "GrassInteractionRT_A_64",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            _rtB = new RenderTexture(desc)
            {
                name = "GrassInteractionRT_B_64",
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
            GL.Clear(false, true, new Color(0f, 0.5f, 0.5f, 1f));
            RenderTexture.active = active;
        }

        private void ReleaseRenderTextures()
        {
            if (_rtA != null) { _rtA.Release(); DestroyImmediate(_rtA); _rtA = null; }
            if (_rtB != null) { _rtB.Release(); DestroyImmediate(_rtB); _rtB = null; }
        }

        private void UpdateInteraction()
        {
            if (targetPlayer == null || _painterMaterial == null || _rtA == null || _rtB == null) return;

            Vector3 playerPos = targetPlayer.position;
            if (!_hasCenterInit)
            {
                _currentAreaCenter = playerPos;
                _hasCenterInit = true;
            }

            // 计算上一帧中心到当前帧中心的 UV 位移，使得历史压草痕迹固定在世界空间
            float areaSize = Mathf.Max(1f, interactionAreaSize);
            Vector3 prevCenter = _currentAreaCenter;
            _currentAreaCenter = playerPos;

            float uvShiftX = (_currentAreaCenter.x - prevCenter.x) / areaSize;
            float uvShiftY = (_currentAreaCenter.z - prevCenter.z) / areaSize;

            float uvX = (playerPos.x - _currentAreaCenter.x) / areaSize + 0.5f;
            float uvY = (playerPos.z - _currentAreaCenter.z) / areaSize + 0.5f;
            float radiusUV = footprintRadius / areaSize;

            RenderTexture src = _pingPong ? _rtA : _rtB;
            RenderTexture dst = _pingPong ? _rtB : _rtA;

            float dt = Application.isPlaying ? Time.deltaTime : 0.016f;

            _painterMaterial.SetVector(StampCenterProp, new Vector4(uvX, uvY, 0, 0));
            _painterMaterial.SetVector(UVOffsetProp, new Vector4(uvShiftX, uvShiftY, 0, 0));
            _painterMaterial.SetFloat(StampRadiusProp, radiusUV);
            _painterMaterial.SetFloat(StampHardnessProp, footprintHardness);
            _painterMaterial.SetFloat(FadeRateProp, recoverySpeed);
            _painterMaterial.SetFloat(DeltaTimeProp, dt);

            Graphics.Blit(src, dst, _painterMaterial);
            _pingPong = !_pingPong;

            // 全局注入着色器变量供 GrassURP.shader 采样
            Shader.SetGlobalTexture(GrassInteractionRTProp, dst);
            Shader.SetGlobalVector(GrassInteractionCenterProp, new Vector4(_currentAreaCenter.x, _currentAreaCenter.y, _currentAreaCenter.z, 0));
            Shader.SetGlobalFloat(GrassInteractionAreaSizeProp, areaSize);
            Shader.SetGlobalVector(PlayerWorldPosProp, new Vector4(playerPos.x, playerPos.y, playerPos.z, 1.0f));
            Shader.SetGlobalFloat(PlayerFootprintRadiusProp, footprintRadius);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 0.2f, 0.35f);
            Vector3 center = targetPlayer != null ? targetPlayer.position : transform.position;
            Gizmos.DrawWireCube(center, new Vector3(interactionAreaSize, 2f, interactionAreaSize));
        }
#endif
    }
}
