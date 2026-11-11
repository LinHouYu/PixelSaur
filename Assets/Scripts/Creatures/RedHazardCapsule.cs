using UnityEngine;
using PixelSaur.UI;
using PixelSaur.Player;

namespace PixelSaur.Creatures
{
    /// <summary>
    /// 红色危险胶囊测试生物（无 AI，静止）：
    /// 当玩家碰到它时扣除 20 点生命值，每 0.4 秒最多触发一次。
    /// 所有参数均在 Inspector 暴露，方便微调。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class RedHazardCapsule : MonoBehaviour
    {
        [Header("伤害配置")]
        [Tooltip("每次碰撞扣减玩家生命值")]
        [SerializeField] private float damageAmount = 20.0f;

        [Tooltip("伤害触发冷却间隔（秒）")]
        [SerializeField] private float damageInterval = 0.4f;

        [Header("受击反馈")]
        [Tooltip("碰击时给玩家轻微后退击退力")]
        [SerializeField] private float knockbackForce = 2.5f;

        private float _lastDamageTime = -999f;
        private MeshRenderer _renderer;
        private Color _baseColor = new Color(0.95f, 0.15f, 0.15f, 1f);

        private void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            EnsureVisuals();
        }

        private void OnEnable()
        {
            EnsureVisuals();
        }

        private void EnsureVisuals()
        {
            if (_renderer != null && _renderer.sharedMaterial == null)
            {
                Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Universal Render Pipeline/Lit");
                if (unlitShader != null)
                {
                    Material mat = new Material(unlitShader) { name = "M_RedHazard_Mat" };
                    mat.SetColor("_BaseColor", _baseColor);
                    _renderer.sharedMaterial = mat;
                }
            }
        }

        private void OnTriggerStay(Collider other)
        {
            TryDamagePlayer(other.gameObject);
        }

        private void OnCollisionStay(Collision collision)
        {
            TryDamagePlayer(collision.gameObject);
        }

        private void TryDamagePlayer(GameObject targetObj)
        {
            if (Time.time - _lastDamageTime < damageInterval) return;

            PlayerController player = targetObj.GetComponentInParent<PlayerController>();
            if (player == null) return;

            _lastDamageTime = Time.time;

            // 1. 扣减玩家生命
            if (PlayerStatusUI.Instance != null)
            {
                PlayerStatusUI.Instance.ModifyHealth(-damageAmount);
            }

            // 2. 给予微小击退反冲力
            Vector3 pushDir = (player.transform.position - transform.position);
            pushDir.y = 0f;
            if (pushDir.sqrMagnitude > 0.001f)
            {
                player.PerformDash(pushDir.normalized, knockbackForce, 0.12f);
            }

            Debug.Log($"<color=#FF3333>[RedHazardCapsule] 触碰玩家！造成 {damageAmount} 点伤害（间隔 {damageInterval}s）。</color>");
        }
    }
}
