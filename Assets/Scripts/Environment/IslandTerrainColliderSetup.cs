using System.Collections.Generic;
using UnityEngine;

namespace PixelSaur.Environment
{
    /// <summary>
    /// 岛屿地形与浅海海床碰撞体自动化配置工具：
    /// 1. 自动遍历 Island_Terrain 所有子网格（sand_main、grass_center），配置精确的 MeshCollider
    ///    并附加向下延伸的实心厚底 BoxCollider，彻底杜绝薄片网格导致的胶囊体穿模；
    /// 2. 自动构建环绕岛屿的水下缓坡沙滩与浅海海床（Ocean_Seabed），
    ///    让角色可以自由走入海水中互动并顺畅走回岛上，同时为 MinionsArt 水面 Shader 提供真实的海岸线深度渐变。
    /// </summary>
    [ExecuteAlways]
    public class IslandTerrainColliderSetup : MonoBehaviour
    {
        [Header("碰撞体加固配置")]
        [Tooltip("是否在运行或启动时自动配置碰撞体与水下海床")]
        [SerializeField] private bool autoSetupOnAwake = true;

        [Tooltip("岛屿网格底部向下延伸的实心碰撞体厚度（米），防止角色穿透薄网格")]
        [Range(0.5f, 5.0f)]
        [SerializeField] private float solidColliderThickness = 2.0f;

        [Header("浅海海床与涉水斜坡配置")]
        [Tooltip("是否自动生成环绕岛屿的水下缓坡与浅海海床（使角色可在水中行走互动）")]
        [SerializeField] private bool generateUnderwaterSeabed = true;

        [Tooltip("浅海海床深度（相对于海平面的下沉高度，米）")]
        [Range(-1.2f, -0.15f)]
        [SerializeField] private float seabedHeightY = -0.38f;

        [Tooltip("沙滩向海中延伸的缓坡宽度（米）")]
        [Range(1.5f, 10.0f)]
        [SerializeField] private float beachSlopeWidth = 4.2f;

        [Tooltip("海床总覆盖半宽（米）")]
        [Range(20f, 150f)]
        [SerializeField] private float seabedExtent = 100f;

        [Tooltip("水下海床沙地颜色")]
        [SerializeField] private Color seabedSandColor = new Color(0.86f, 0.78f, 0.55f, 1.0f);

        private Mesh _seabedMesh;
        private Material _seabedMaterial;

        private void Awake()
        {
            if (autoSetupOnAwake)
            {
                SetupColliders();
            }
        }

        [ContextMenu("立即配置岛屿碰撞体与浅海海床")]
        public void SetupColliders()
        {
            MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>(true);
            int configuredCount = 0;

            Bounds islandWorldBounds = new Bounds(transform.position, Vector3.zero);
            bool hasBounds = false;

            foreach (MeshFilter mf in meshFilters)
            {
                if (mf.sharedMesh == null) continue;
                if (mf.gameObject.name == "Ocean_Seabed") continue;

                // 1. 配置精确的静态非凸 MeshCollider
                MeshCollider meshCol = mf.GetComponent<MeshCollider>();
                if (meshCol == null)
                {
                    meshCol = mf.gameObject.AddComponent<MeshCollider>();
                }
                meshCol.sharedMesh = mf.sharedMesh;
                meshCol.convex = false;

                // 2. 配置向下延伸的实心厚底 BoxCollider，确保顶面与网格顶面严丝合缝，彻底杜绝穿模
                BoxCollider boxCol = mf.GetComponent<BoxCollider>();
                if (boxCol == null)
                {
                    boxCol = mf.gameObject.AddComponent<BoxCollider>();
                }

                Bounds localBounds = mf.sharedMesh.bounds;
                float topLocalY = localBounds.max.y;
                float lossyY = Mathf.Max(0.01f, Mathf.Abs(mf.transform.lossyScale.y));
                float localThickness = solidColliderThickness / lossyY;

                boxCol.size = new Vector3(localBounds.size.x, localThickness, localBounds.size.z);
                boxCol.center = new Vector3(localBounds.center.x, topLocalY - localThickness * 0.5f, localBounds.center.z);

                MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    if (!hasBounds)
                    {
                        islandWorldBounds = mr.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        islandWorldBounds.Encapsulate(mr.bounds);
                    }
                }

                configuredCount++;
            }

            if (generateUnderwaterSeabed)
            {
                SetupSeabed(hasBounds ? islandWorldBounds : new Bounds(transform.position, new Vector3(9.08f, 0.06f, 9.08f)));
            }

            Debug.Log($"[IslandTerrainColliderSetup] 岛屿碰撞加固完成：共配置 {configuredCount} 个实心防穿模网格节点及环岛涉水海床。");
        }

        private void SetupSeabed(Bounds islandBounds)
        {
            Transform seabedTr = transform.Find("Ocean_Seabed");
            GameObject seabedObj;
            if (seabedTr == null)
            {
                seabedObj = new GameObject("Ocean_Seabed");
                seabedObj.transform.SetParent(transform, false);
            }
            else
            {
                seabedObj = seabedTr.gameObject;
            }

            // 保持世界坐标变换一致
            seabedObj.transform.position = Vector3.zero;
            seabedObj.transform.rotation = Quaternion.identity;
            Vector3 parentScale = transform.lossyScale;
            seabedObj.transform.localScale = new Vector3(
                1f / Mathf.Max(0.001f, parentScale.x),
                1f / Mathf.Max(0.001f, parentScale.y),
                1f / Mathf.Max(0.001f, parentScale.z)
            );

            MeshFilter mf = seabedObj.GetComponent<MeshFilter>();
            if (mf == null) mf = seabedObj.AddComponent<MeshFilter>();

            MeshRenderer mr = seabedObj.GetComponent<MeshRenderer>();
            if (mr == null) mr = seabedObj.AddComponent<MeshRenderer>();

            MeshCollider mc = seabedObj.GetComponent<MeshCollider>();
            if (mc == null) mc = seabedObj.AddComponent<MeshCollider>();

            BoxCollider floorBox = seabedObj.GetComponent<BoxCollider>();
            if (floorBox == null) floorBox = seabedObj.AddComponent<BoxCollider>();

            // 构建从岛屿边缘 (innerHalf) 向外缓坡过渡到浅海海床 (outerHalf -> seabedExtent) 的网格
            float innerX = Mathf.Max(2.0f, islandBounds.extents.x - 0.08f);
            float innerZ = Mathf.Max(2.0f, islandBounds.extents.z - 0.08f);
            float slopeX = innerX + beachSlopeWidth;
            float slopeZ = innerZ + beachSlopeWidth;
            float outer = Mathf.Max(seabedExtent, slopeX + 10f);

            // 沙滩岸边衔接高度（刚好位于 sand_main 顶面 0.0625 略微靠下一点点，形成平滑下水坡道）
            float shoreY = 0.052f;
            float deepY = seabedHeightY;

            _seabedMesh = BuildSeabedSkirtMesh(innerX, innerZ, slopeX, slopeZ, outer, shoreY, deepY);
            mf.sharedMesh = _seabedMesh;

            mc.sharedMesh = _seabedMesh;
            mc.convex = false;

            // 底部兜底大平层 BoxCollider，保证在水域任何位置绝对不会掉出世界
            floorBox.size = new Vector3(outer * 2f, 2.0f, outer * 2f);
            floorBox.center = new Vector3(0f, deepY - 1.0f, 0f);

            // 赋予沙滩色材质，使浅水区能透出自然的海底沙地色彩
            if (_seabedMaterial == null)
            {
                Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
                if (litShader != null)
                {
                    _seabedMaterial = new Material(litShader) { name = "M_Seabed_Runtime" };
                    _seabedMaterial.SetColor("_BaseColor", seabedSandColor);
                    _seabedMaterial.SetFloat("_Smoothness", 0.15f);
                }
            }
            if (_seabedMaterial != null)
            {
                mr.sharedMaterial = _seabedMaterial;
            }
        }

        private static Mesh BuildSeabedSkirtMesh(float inX, float inZ, float midX, float midZ, float outR, float shoreY, float deepY)
        {
            Mesh mesh = new Mesh { name = "ProceduralIslandSeabed" };

            // 3 圈同心矩形环：
            // Ring 0 (内圈，岛屿中心及边缘 y = shoreY)
            // Ring 1 (缓坡底圈，x/z = midX/midZ, y = deepY)
            // Ring 2 (远海边界，x/z = outR, y = deepY)
            Vector3[] verts = new Vector3[12];
            // Ring 0: 岛屿岸线内缘 (0..3)
            verts[0] = new Vector3(-inX, shoreY, -inZ);
            verts[1] = new Vector3(-inX, shoreY,  inZ);
            verts[2] = new Vector3( inX, shoreY,  inZ);
            verts[3] = new Vector3( inX, shoreY, -inZ);

            // Ring 1: 缓坡底部 (4..7)
            verts[4] = new Vector3(-midX, deepY, -midZ);
            verts[5] = new Vector3(-midX, deepY,  midZ);
            verts[6] = new Vector3( midX, deepY,  midZ);
            verts[7] = new Vector3( midX, deepY, -midZ);

            // Ring 2: 远海海床外缘 (8..11)
            verts[8]  = new Vector3(-outR, deepY, -outR);
            verts[9]  = new Vector3(-outR, deepY,  outR);
            verts[10] = new Vector3( outR, deepY,  outR);
            verts[11] = new Vector3( outR, deepY, -outR);

            List<int> tris = new System.Collections.Generic.List<int>(60);

            // 中心封口 (0, 1, 2, 3)
            AddQuad(tris, 0, 1, 2, 3);

            // Ring 0 -> Ring 1 水下沙滩缓坡 (4 个梯形斜面)
            for (int i = 0; i < 4; i++)
            {
                int next = (i + 1) % 4;
                AddQuad(tris, i, 4 + i, 4 + next, next);
            }

            // Ring 1 -> Ring 2 远海平底海床 (4 个外扩区域)
            for (int i = 0; i < 4; i++)
            {
                int next = (i + 1) % 4;
                AddQuad(tris, 4 + i, 8 + i, 8 + next, 4 + next);
            }

            Vector2[] uvs = new Vector2[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                uvs[i] = new Vector2(verts[i].x * 0.1f, verts[i].z * 0.1f);
            }

            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddQuad(System.Collections.Generic.List<int> tris, int a, int b, int c, int d)
        {
            tris.Add(a); tris.Add(b); tris.Add(c);
            tris.Add(a); tris.Add(c); tris.Add(d);
        }
    }
}
