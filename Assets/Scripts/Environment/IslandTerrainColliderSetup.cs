using UnityEngine;

namespace PixelSaur.Environment
{
    /// <summary>
    /// 岛屿地形碰撞体自动化配置工具：自动遍历 Island_Terrain 所有子网格，
    /// 确保 sand_main、grass_center 等静态地形节点均配备精确的 MeshCollider。
    /// </summary>
    [ExecuteAlways]
    public class IslandTerrainColliderSetup : MonoBehaviour
    {
        [Tooltip("是否在运行或启动时自动补齐缺失的 MeshCollider")]
        [SerializeField] private bool autoSetupOnAwake = true;

        private void Awake()
        {
            if (autoSetupOnAwake)
            {
                SetupColliders();
            }
        }

        [ContextMenu("立即配置所有子网格 MeshCollider")]
        public void SetupColliders()
        {
            MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>(true);
            int addedCount = 0;

            foreach (MeshFilter mf in meshFilters)
            {
                if (mf.sharedMesh == null) continue;

                MeshCollider col = mf.GetComponent<MeshCollider>();
                if (col == null)
                {
                    col = mf.gameObject.AddComponent<MeshCollider>();
                    addedCount++;
                }

                col.sharedMesh = mf.sharedMesh;
                col.convex = false; // 地形网格使用非凸凹碰撞以获得最精确的地表贴合
            }

            Debug.Log($"[IslandTerrainColliderSetup] 地形碰撞体配置完成，共检测到 {meshFilters.Length} 个网格节点，新增 {addedCount} 个 MeshCollider。");
        }
    }
}
