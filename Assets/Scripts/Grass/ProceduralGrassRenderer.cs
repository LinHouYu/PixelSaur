using System.Collections.Generic;
using UnityEngine;

namespace PixelSaur.Grass
{
    /// <summary>
    /// 草地渲染器：参考 InfiniteGrass 架构，在 Grass_Plain 网格区域内高效分布草叶实例，
    /// 支持 GPU Instancing 批量绘制、风力与 64x64 交互贴图弯折。
    /// 所有形态、密度与渲染参数均在 Inspector 中完整开放微调。
    /// </summary>
    [ExecuteAlways]
    public class ProceduralGrassRenderer : MonoBehaviour
    {
        [Header("生成区域参考")]
        [Tooltip("目标草地表面 MeshFilter（通常为 Grass_Plain 下的 grass_center）")]
        [SerializeField] private MeshFilter targetMeshFilter;

        [Header("草地材质与网格")]
        [Tooltip("草地 URP 材质球（使用 PixelSaur/GrassURP 着色器）")]
        [SerializeField] private Material grassMaterial;

        [Tooltip("自定义单株草叶网格（留空时自动程序化生成标准草叶）")]
        [SerializeField] private Mesh customBladeMesh;

        [Header("密度与规模配置 (手机性能优化)")]
        [Tooltip("草叶总生成数量（已按性能优化减少4倍，手机端推荐 1500~2000）")]
        [Range(100, 15000)]
        [SerializeField] private int grassCount = 2000;

        [Tooltip("单株草基准高度（米）")]
        [Range(0.2f, 2.5f)]
        [SerializeField] private float bladeHeight = 0.72f;

        [Tooltip("单株草基准宽度（米）")]
        [Range(0.02f, 0.5f)]
        [SerializeField] private float bladeWidth = 0.11f;

        [Tooltip("高度随机差异幅度")]
        [Range(0f, 0.8f)]
        [SerializeField] private float heightVariation = 0.35f;

        [Header("剔除与视距")]
        [Tooltip("草地可视最大渲染距离（超出平滑剔除）")]
        [Range(10f, 150f)]
        [SerializeField] private float maxDrawDistance = 38f;

        private Mesh _generatedBladeMesh;
        private readonly List<Matrix4x4[]> _batches = new List<Matrix4x4[]>();
        private readonly int _batchSize = 1023; // Unity GPU Instancing 单批次最大实例上限
        private bool _isInitialized;

        private void OnEnable()
        {
            InitializeGrass();
        }

        private void Start()
        {
            if (!_isInitialized)
            {
                InitializeGrass();
            }
        }

        private void Update()
        {
            RenderGrassBatches();
        }

        private void OnValidate()
        {
            // 在 Inspector 修改参数时即时重新生成预览
            _isInitialized = false;
        }

        [ContextMenu("强制重新生成草地分布")]
        public void RebuildGrass()
        {
            _isInitialized = false;
            InitializeGrass();
        }

        private void InitializeGrass()
        {
            if (targetMeshFilter == null)
            {
                // 自动尝试寻找名为 Grass_Plain 或 grass_center 的网格
                GameObject plainObj = GameObject.Find("Grass_Plain") ?? GameObject.Find("grass_center");
                if (plainObj != null)
                {
                    targetMeshFilter = plainObj.GetComponentInChildren<MeshFilter>();
                }
            }

            // 1. 准备草叶几何体
            if (customBladeMesh != null)
            {
                _generatedBladeMesh = customBladeMesh;
            }
            else
            {
                if (_generatedBladeMesh == null)
                {
                    _generatedBladeMesh = CreateProceduralBladeMesh();
                }
            }

            // 2. 在目标网格表面散布草叶变换矩阵
            _batches.Clear();
            List<Matrix4x4> matrixList = SampleGrassMatrices();

            int total = matrixList.Count;
            for (int i = 0; i < total; i += _batchSize)
            {
                int count = Mathf.Min(_batchSize, total - i);
                Matrix4x4[] batch = new Matrix4x4[count];
                matrixList.CopyTo(i, batch, 0, count);
                _batches.Add(batch);
            }

            _isInitialized = true;
        }

        private List<Matrix4x4> SampleGrassMatrices()
        {
            List<Matrix4x4> matrices = new List<Matrix4x4>(grassCount);
            Random.InitState(42); // 固定种子保证草地分布稳定

            if (targetMeshFilter != null && targetMeshFilter.sharedMesh != null)
            {
                Mesh mesh = targetMeshFilter.sharedMesh;
                Transform meshTr = targetMeshFilter.transform;

                if (mesh.isReadable)
                {
                    Vector3[] vertices = mesh.vertices;
                    int[] triangles = mesh.triangles;

                    // 预先筛选出所有朝上的顶面三角形，避免在侧面和底面浪费采样次数
                    List<int> upwardTris = new List<int>();
                    int triCount = triangles.Length / 3;
                    for (int t = 0; t < triCount; t++)
                    {
                        int baseIdx = t * 3;
                        Vector3 p0 = meshTr.TransformPoint(vertices[triangles[baseIdx]]);
                        Vector3 p1 = meshTr.TransformPoint(vertices[triangles[baseIdx + 1]]);
                        Vector3 p2 = meshTr.TransformPoint(vertices[triangles[baseIdx + 2]]);
                        Vector3 faceNormal = Vector3.Cross(p1 - p0, p2 - p0).normalized;

                        if (Vector3.Dot(faceNormal, Vector3.up) > 0.45f)
                        {
                            upwardTris.Add(baseIdx);
                        }
                    }

                    if (upwardTris.Count > 0)
                    {
                        for (int i = 0; i < grassCount; i++)
                        {
                            int triIndex = upwardTris[Random.Range(0, upwardTris.Count)];
                            Vector3 v0 = vertices[triangles[triIndex]];
                            Vector3 v1 = vertices[triangles[triIndex + 1]];
                            Vector3 v2 = vertices[triangles[triIndex + 2]];

                            // 重心坐标均匀随机采样
                            float r1 = Random.value;
                            float r2 = Random.value;
                            if (r1 + r2 > 1f) { r1 = 1f - r1; r2 = 1f - r2; }
                            float r3 = 1f - r1 - r2;

                            Vector3 localPos = v0 * r1 + v1 * r2 + v2 * r3;
                            Vector3 worldPos = meshTr.TransformPoint(localPos);

                            Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                            float scaleY = bladeHeight * (1f + Random.Range(-heightVariation, heightVariation));
                            float scaleXZ = bladeWidth * (1f + Random.Range(-0.2f, 0.2f));
                            Vector3 scale = new Vector3(scaleXZ, scaleY, scaleXZ);

                            matrices.Add(Matrix4x4.TRS(worldPos, rotation, scale));
                        }
                        return matrices;
                    }
                }

                // 若网格未开启 Read/Write，则直接基于 MeshRenderer 的世界包围盒顶面精准散布
                MeshRenderer mr = targetMeshFilter.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    Bounds b = mr.bounds;
                    for (int i = 0; i < grassCount; i++)
                    {
                        float x = Random.Range(b.min.x + 0.1f, b.max.x - 0.1f);
                        float z = Random.Range(b.min.z + 0.1f, b.max.z - 0.1f);
                        Vector3 worldPos = new Vector3(x, b.max.y, z);
                        Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                        float scaleY = bladeHeight * (1f + Random.Range(-heightVariation, heightVariation));
                        float scaleXZ = bladeWidth * (1f + Random.Range(-0.2f, 0.2f));
                        matrices.Add(Matrix4x4.TRS(worldPos, rotation, new Vector3(scaleXZ, scaleY, scaleXZ)));
                    }
                    return matrices;
                }
            }

            // 备用：基于当前挂载物体 Bounds 散布
            Vector3 center = transform.position;
            float radius = 15f;
            for (int i = 0; i < grassCount; i++)
            {
                Vector2 circle = Random.insideUnitCircle * radius;
                Vector3 worldPos = center + new Vector3(circle.x, 0, circle.y);
                Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                float scaleY = bladeHeight * (1f + Random.Range(-heightVariation, heightVariation));
                float scaleXZ = bladeWidth;
                matrices.Add(Matrix4x4.TRS(worldPos, rotation, new Vector3(scaleXZ, scaleY, scaleXZ)));
            }

            return matrices;
        }

        private Mesh CreateProceduralBladeMesh()
        {
            Mesh mesh = new Mesh { name = "ProceduralGrassBlade" };

            // 5 点 3 三角形经典单株草网格 (底部宽，中间收拢，尖端收为单点)
            Vector3[] verts = new Vector3[]
            {
                new Vector3(-0.5f, 0f, 0f),      // 0 底部左
                new Vector3(0.5f, 0f, 0f),       // 1 底部右
                new Vector3(-0.35f, 0.5f, 0.05f),// 2 中部左
                new Vector3(0.35f, 0.5f, 0.05f), // 3 中部右
                new Vector3(0f, 1.0f, 0.15f)     // 4 草尖
            };

            Vector2[] uvs = new Vector2[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(0.5f, 1.0f)
            };

            int[] tris = new int[]
            {
                0, 1, 2,
                1, 3, 2,
                2, 3, 4
            };

            Vector3[] normals = new Vector3[]
            {
                Vector3.back, Vector3.back, Vector3.back, Vector3.back, Vector3.back
            };

            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.normals = normals;
            mesh.RecalculateBounds();

            return mesh;
        }

        private void RenderGrassBatches()
        {
            if (!_isInitialized || grassMaterial == null || _generatedBladeMesh == null) return;

            if (!grassMaterial.enableInstancing)
            {
                grassMaterial.enableInstancing = true;
            }

            Camera cam = Camera.main;
            Vector3 camPos = cam != null ? cam.transform.position : Vector3.zero;

            RenderParams rp = new RenderParams(grassMaterial)
            {
                receiveShadows = true,
                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                worldBounds = new Bounds(transform.position, Vector3.one * 200f)
            };

            foreach (Matrix4x4[] batch in _batches)
            {
                if (cam != null)
                {
                    // 简易批次距离剔除
                    Vector3 batchCenter = batch[0].GetColumn(3);
                    if (Vector3.Distance(camPos, batchCenter) > maxDrawDistance)
                    {
                        continue;
                    }
                }

                Graphics.RenderMeshInstanced(rp, _generatedBladeMesh, 0, batch);
            }
        }

        private void OnDestroy()
        {
            if (_generatedBladeMesh != null && _generatedBladeMesh != customBladeMesh)
            {
                DestroyImmediate(_generatedBladeMesh);
            }
        }
    }
}
