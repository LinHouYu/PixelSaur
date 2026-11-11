using UnityEngine;
using PixelSaur.Player;
using PixelSaur.UI;
using PixelSaur.Cam;
using PixelSaur.Creatures;

namespace PixelSaur.Combat
{
    /// <summary>
    /// 恐龙战斗控制器：
    /// 1. 基础咬击攻击：攻击间隔 0.8 秒，在攻击范围内造成伤害；
    /// 2. 冲击技能（双击攻击按键触发）：
    ///    - 扣除 25 饱食度（Food）；
    ///    - 角色向前疾速冲撞；
    ///    - 触发摄像机微抖动与真机震动；
    /// 3. 所有伤害、冷却、距离与消耗数值均在 Inspector 暴露，方便微调。
    /// </summary>
    public class DinoCombatController : MonoBehaviour
    {
        public static DinoCombatController Instance { get; private set; }

        [Header("基础咬击配置")]
        [Tooltip("基础撕咬伤害")]
        [SerializeField] private float biteDamage = 50.0f;

        [Tooltip("攻击冷却间隔（秒，规范固定为 0.8s）")]
        [SerializeField] private float attackCooldown = 0.8f;

        [Header("冲击技能配置 (双击攻击键触发)")]
        [Tooltip("释放冲击技能消耗的饱食度数值（规范固定为 25）")]
        [SerializeField] private float dashFoodCost = 25.0f;

        [Tooltip("冲击技能速度（米/秒）")]
        [SerializeField] private float dashSpeed = 16.0f;

        [Tooltip("冲击技能位移持续时间（秒）")]
        [SerializeField] private float dashDuration = 0.22f;

        [Tooltip("冲击撞击敌人造成的伤害")]
        [SerializeField] private float dashDamage = 60.0f;

        [Tooltip("冲击技能冷却间隔（秒）")]
        [SerializeField] private float dashCooldown = 1.2f;

        [Header("打击反馈")]
        [Tooltip("冲击镜头微抖动强度")]
        [SerializeField] private float shakeIntensity = 0.18f;

        [Tooltip("冲击镜头微抖动时长")]
        [SerializeField] private float shakeDuration = 0.25f;

        private float _lastAttackTime = -999f;
        private float _lastDashTime = -999f;
        private PlayerController _playerController;
        private DinoAttackIndicator _indicator;

        public float AttackCooldown => attackCooldown;
        public float RemainingAttackCooldown => Mathf.Max(0f, attackCooldown - (Time.time - _lastAttackTime));
        public float RemainingDashCooldown => Mathf.Max(0f, dashCooldown - (Time.time - _lastDashTime));

        private void Awake()
        {
            Instance = this;
            _playerController = GetComponent<PlayerController>();
            _indicator = GetComponentInChildren<DinoAttackIndicator>();
        }

        private void Update()
        {
            // PC 键盘辅助按键：J键普通攻击，K键冲击技能
            if (Input.GetKeyDown(KeyCode.J))
            {
                TryBiteAttack();
            }
            if (Input.GetKeyDown(KeyCode.K))
            {
                TryChargeDashSkill();
            }
        }

        /// <summary>
        /// 尝试执行基础单发咬击攻击（冷却 0.8 秒）
        /// </summary>
        public bool TryBiteAttack()
        {
            if (Time.time - _lastAttackTime < attackCooldown)
            {
                return false;
            }

            _lastAttackTime = Time.time;

            float radius = _indicator != null ? _indicator.AttackRadius : 1.85f;
            Vector3 center = transform.position;
            Collider[] hits = Physics.OverlapSphere(center, radius);

            bool hitAny = false;
            foreach (var hit in hits)
            {
                if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;

                var prey = hit.GetComponentInParent<GreenPreyCapsule>();
                if (prey != null && !prey.IsDead)
                {
                    prey.TakeDamage(biteDamage);
                    hitAny = true;
                    Debug.Log($"<color=#FF5555>[DinoCombat] 咬中猎物！造成 {biteDamage} 伤害，剩余生命 {prey.Health}。</color>");
                }
            }

            // 轻微镜头点震增强咬击手感
            if (hitAny)
            {
                ThirdPersonOrbitCamera cam = Camera.main != null ? Camera.main.GetComponent<ThirdPersonOrbitCamera>() : null;
                cam?.TriggerShake(0.08f, 0.12f);
            }

            return true;
        }

        /// <summary>
        /// 尝试释放冲击技能（双击攻击按钮触发，消耗 25 饱食度）
        /// </summary>
        public bool TryChargeDashSkill()
        {
            if (Time.time - _lastDashTime < dashCooldown)
            {
                Debug.Log("[DinoCombat] 冲击技能冷却中...");
                return false;
            }

            // 1. 检查饱食度
            if (PlayerStatusUI.Instance != null && PlayerStatusUI.Instance.Food < dashFoodCost)
            {
                Debug.Log($"<color=#FFAA33>[DinoCombat] 饱食度不足！需要 {dashFoodCost}，当前只有 {PlayerStatusUI.Instance.Food:0}。</color>");
                return false;
            }

            _lastDashTime = Time.time;

            // 2. 扣除 25 饱食度
            if (PlayerStatusUI.Instance != null)
            {
                PlayerStatusUI.Instance.ModifyFood(-dashFoodCost);
            }

            // 3. 恐龙猛冲位移
            Vector3 dashDirection = transform.forward;
            if (_playerController != null)
            {
                _playerController.PerformDash(dashDirection, dashSpeed, dashDuration);
            }

            // 4. 镜头微抖动
            ThirdPersonOrbitCamera cam = Camera.main != null ? Camera.main.GetComponent<ThirdPersonOrbitCamera>() : null;
            cam?.TriggerShake(shakeIntensity, shakeDuration);

            // 5. 手机震动
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                Handheld.Vibrate();
            }
            catch { }
#endif

            // 6. 冲击范围内目标造成碰撞伤害与推力
            float radius = _indicator != null ? _indicator.AttackRadius * 1.1f : 2.0f;
            Collider[] hits = Physics.OverlapSphere(transform.position + dashDirection * 1.2f, radius);
            foreach (var hit in hits)
            {
                if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;

                var prey = hit.GetComponentInParent<GreenPreyCapsule>();
                if (prey != null && !prey.IsDead)
                {
                    prey.TakeDamage(dashDamage);
                }
            }

            Debug.Log($"<color=#33FFFF>[DinoCombat] 释放恐龙冲击技能！消耗饱食度 {dashFoodCost}，向前猛冲并震屏！</color>");
            return true;
        }
    }
}
