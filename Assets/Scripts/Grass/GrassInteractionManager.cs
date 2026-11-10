using UnityEngine;

namespace PixelSaur.Grass
{
    /// <summary>
    /// 草地交互管理器：维护 64x64 分辨率的动态交互 RenderTexture（双缓冲），
    /// 实时追踪玩家足迹并生成带回弹衰减的交互轨迹，全局传递给草地 Shader。
    /// </summary>
    public class GrassInteractionManager : MonoBehaviour
    {
        public static GrassInteractionManager Instance { get; private set; }

        [Header("交互跟踪目标")]
        [Tooltip("交互主体（角色）Transform，为空时自动搜寻 Tag 为 Player 的对象")]
        [SerializeField] private Transform targetPlayer;

        [Header("RT 贴图与分辨率 (按规范控制在 64)")]
        [Tooltip("交互贴图分辨率，规范固定为 64")]
        [SerializeField] private int rtResolution = 64;

        [Tooltip("交互覆盖范围区域边长（米）")]
        [Range(10f, 100f)]
        [SerializeField] private float interactionAreaSize = 40f;

        [Header("足迹与交互微调参数")]
        [Tooltip("角色踩踏半径（米）")]
        [Range(0.2f, 3.0f)]
        [SerializeField] private float footprintRadius = 1.0f;

        [Tooltip("踩踏边缘硬度")]
        [Range(0.5f, 5.0f)]
        [SerializeField] private float footprintHardness = 2.0f;

        [Tooltip("草地踩踏后回弹恢复速度（数值越大恢复越快）")]
        [Range(0.2f, 10.0f)]
        [SerializeField] private float recoverySpeed = 2.5f;

        [Header("材质引用")]
        [SerializeField] private Shader painterShader;

        private RenderTexture _rtA;
        private RenderTexture _rtB;
        private bool _pingPong;
        private Material _painterMaterial;
        private Vector3 _currentAreaCenter;

        private static readonly int GrassInteractionRTProp = Shader.PropertyToID("_GrassInteractionRT");
        private static readonly int GrassInteractionCenterProp = Shader.PropertyToID("_GrassInteractionCenter");
        private static readonly int GrassInteractionAreaSizeProp = Shader.PropertyToID("_GrassInteractionAreaSize");
        private static readonly int PlayerWorldPosProp = Shader.PropertyToID("_PlayerWorldPos");

        private static readonly int StampCenterProp = Shader.PropertyToID("_StampCenter");
        private static readonly int StampRadiusProp = Shader.PropertyToID("_StampRadius");
        private static readonly int StampHardnessProp = Shader.PropertyToID("_StampHardness");
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
                painterShader = Shader.Find("Hidden/PixelSaur/GrassInteractionPainter");
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
                _currentAreaCenter = targetPlayer.position;
            }
        }

        private void Update()
        {
            FindPlayerIfNull();
            UpdateInteraction();
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
                autoGenerateMips = false,
                enableRandomWrite = false
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

            // 预填充纯黑
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

        private void UpdateInteraction()
        {
            if (targetPlayer == null || _painterMaterial == null || _rtA == null || _rtB == null) return;

            // 交互中心跟随玩家移动（带微弱吸附或实时更新）
            _currentAreaCenter = targetPlayer.position;

            Vector3 playerPos = targetPlayer.position;
            float uvX = (playerPos.x - _currentAreaCenter.x) / interactionAreaSize + 0.5f;
            float uvY = (playerPos.z - _currentAreaCenter.z) / interactionAreaSize + 0.5f;
            float radiusUV = footprintRadius / interactionAreaSize;

            RenderTexture src = _pingPong ? _rtA : _rtB;
            RenderTexture dst = _pingPong ? _rtB : _rtA;

            _painterMaterial.SetVector(StampCenterProp, new Vector4(uvX, uvY, 0, 0));
            _painterMaterial.SetFloat(StampRadiusProp, radiusUV);
            _painterMaterial.SetFloat(StampHardnessProp, footprintHardness);
            _painterMaterial.SetFloat(FadeRateProp, recoverySpeed);
            _painterMaterial.SetFloat(DeltaTimeProp, Time.deltaTime);

            Graphics.Blit(src, dst, _painterMaterial);
            _pingPong = !_pingPong;

            // 全局设置着色器变量
            Shader.SetGlobalTexture(GrassInteractionRTProp, dst);
            Shader.SetGlobalVector(GrassInteractionCenterProp, new Vector4(_currentAreaCenter.x, _currentAreaCenter.y, _currentAreaCenter.z, 0));
            Shader.SetGlobalFloat(GrassInteractionAreaSizeProp, interactionAreaSize);
            Shader.SetGlobalVector(PlayerWorldPosProp, new Vector4(playerPos.x, playerPos.y, playerPos.z, 0));
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 0.2f, 0.35f);
            Vector3 center = targetPlayer != null ? targetPlayer.position : transform.position;
            Gizmos.DrawWireCube(center, new Vector3(interactionAreaSize, 5f, interactionAreaSize));
        }
#endif
    }
}
