using UnityEngine;
using PixelSaur.Player;
using PixelSaur.UI;
using PixelSaur.Creatures;
using PixelSaur.Water;

namespace PixelSaur.Combat
{
    public enum InteractionType
    {
        None,
        DrinkingWater,
        EatingPrey
    }

    /// <summary>
    /// 恐龙进食与饮水交互控制器：
    /// 1. 停留在水边或猎物尸体攻击范围内 0.4 秒，自动触发饮水或进食；
    /// 2. 交互进行中锁定角色位移，任何移动或跳跃操作可立即打断；
    /// 3. 饮水：每 0.2 秒恢复 5 点口渴度，满 100 自动打断退出；
    /// 4. 进食：每 1.5 秒吃一口（最多 3 口），每口恢复 20 饱食度与 15 生命，饱食度满 100 自动打断；
    /// 5. 驱动 DinoAttackIndicator 实时变色（敌人红、饮水蓝、猎物黄）；
    /// 6. 全流程参数 Inspector 暴露，方便微调。
    /// </summary>
    public class DinoInteractionController : MonoBehaviour
    {
        [Header("触发与判定阈值")]
        [Tooltip("在攻击范围内停留多久后开始喝水或进食（秒）")]
        [SerializeField] private float triggerDelay = 0.4f;

        [Tooltip("水面绝对高度（参考 WaterSurface Y 轴）")]
        [SerializeField] private float waterLevelY = 0.038f;

        [Tooltip("涉水或贴近水面的判定高度阈值（米）")]
        [SerializeField] private float waterContactTolerance = 0.45f;

        [Header("饮水速率配置")]
        [Tooltip("饮水恢复间隔（秒）")]
        [SerializeField] private float drinkInterval = 0.2f;

        [Tooltip("每次饮水恢复口渴度数值")]
        [SerializeField] private float drinkRecoveryAmount = 5.0f;

        [Header("交互状态 (只读观察)")]
        [SerializeField] private InteractionType currentInteraction = InteractionType.None;
        [SerializeField] private float stayTimer = 0f;
        [SerializeField] private float actionTimer = 0f;

        private PlayerController _playerController;
        private DinoAttackIndicator _indicator;

        public static DinoInteractionController Instance { get; private set; }

        public InteractionType CurrentInteraction => currentInteraction;
        public bool IsInteracting => currentInteraction != InteractionType.None;

        private bool _mobileButtonPressed = false;
        private float _holdTimer = 0f;

        public bool CanDrinkWater { get; private set; }
        public bool CanEatPrey { get; private set; }
        public bool CanInteract => CanEatPrey || CanDrinkWater;

        public void SetButtonPressed(bool pressed)
        {
            _mobileButtonPressed = pressed;
            if (!pressed && currentInteraction != InteractionType.None)
            {
                InterruptInteraction("玩家按键松开，打断动作");
            }
        }

        private void Awake()
        {
            Instance = this;
            _playerController = GetComponent<PlayerController>();
            _indicator = GetComponentInChildren<DinoAttackIndicator>();
        }

        private void Update()
        {
            if (_playerController == null) _playerController = GetComponent<PlayerController>();
            if (_indicator == null) _indicator = GetComponentInChildren<DinoAttackIndicator>();

            DetectSurroundingsAndUpdateIndicator();
            HandleInteractionLogic();
        }

        private void DetectSurroundingsAndUpdateIndicator()
        {
            if (_indicator == null) return;

            float radius = _indicator.AttackRadius;
            Vector3 center = transform.position;

            Collider[] hits = Physics.OverlapSphere(center, radius);

            GreenPreyCapsule closestPrey = null;
            RedHazardCapsule closestHazard = null;
            float closestDist = float.MaxValue;

            foreach (var hit in hits)
            {
                if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;

                var prey = hit.GetComponentInParent<GreenPreyCapsule>();
                if (prey != null)
                {
                    float d = Vector3.Distance(center, prey.transform.position);
                    if (d < closestDist)
                    {
                        closestDist = d;
                        closestPrey = prey;
                    }
                }

                var hazard = hit.GetComponentInParent<RedHazardCapsule>();
                if (hazard != null) closestHazard = hazard;
            }

            // 更新猎物的受威胁范围判定
            if (closestPrey != null)
            {
                closestPrey.SetInAttackRange(true);
            }

            // 检测是否近水
            bool isNearWater = (transform.position.y <= waterLevelY + waterContactTolerance);

            // 变色优先级：
            // 1. 存活敌人/猎物 -> 红色
            // 2. 死亡可吃猎物 -> 黄色
            // 3. 附近有水 -> 蓝色
            // 4. 移动中 -> 正常移动指示
            // 5. 静止 -> 隐藏
            bool isMoving = PlayerInputProxy.Instance != null && PlayerInputProxy.Instance.MoveInput.sqrMagnitude > 0.01f;

            if (closestHazard != null || (closestPrey != null && !closestPrey.IsDead))
            {
                _indicator.SetIndicatorState(IndicatorState.AttackEnemy); // 红色
            }
            else if (closestPrey != null && closestPrey.IsDead && closestPrey.RemainingFeedCount > 0)
            {
                _indicator.SetIndicatorState(IndicatorState.FeedCorpse); // 黄色
            }
            else if (isNearWater)
            {
                _indicator.SetIndicatorState(IndicatorState.DrinkWater); // 蓝色
            }
            else if (isMoving)
            {
                _indicator.SetIndicatorState(IndicatorState.NormalMove);
            }
            else
            {
                _indicator.SetIndicatorState(IndicatorState.IdleHidden);
            }
        }

        private void HandleInteractionLogic()
        {
            // 1. 若玩家拖动摇杆主动位移，或者按下跳跃，立即打断退出任何交互
            if (PlayerInputProxy.Instance != null && (PlayerInputProxy.Instance.MoveInput.sqrMagnitude > 0.05f || PlayerInputProxy.Instance.JumpRequested))
            {
                InterruptInteraction("玩家移动输入，打断动作");
                return;
            }

            // 2. 检测当前可交互的目标
            float radius = _indicator != null ? _indicator.AttackRadius : 1.85f;
            Vector3 center = transform.position;

            GreenPreyCapsule ediblePrey = null;
            Collider[] hits = Physics.OverlapSphere(center, radius);
            foreach (var hit in hits)
            {
                var prey = hit.GetComponentInParent<GreenPreyCapsule>();
                if (prey != null && prey.IsDead && prey.RemainingFeedCount > 0)
                {
                    ediblePrey = prey;
                    break;
                }
            }

            bool isNearWater = (transform.position.y <= waterLevelY + waterContactTolerance);
            CanEatPrey = ediblePrey != null && (PlayerStatusUI.Instance == null || PlayerStatusUI.Instance.Food < 100f);
            CanDrinkWater = isNearWater && (PlayerStatusUI.Instance == null || PlayerStatusUI.Instance.Thirst < 100f);

            // 3. 判定长按状态 (移动端攻击按键按住 或 PC J/E键按住)
            bool pcHold = false;
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                pcHold = UnityEngine.InputSystem.Keyboard.current.jKey.isPressed || UnityEngine.InputSystem.Keyboard.current.eKey.isPressed;
            }
#else
            pcHold = Input.GetKey(KeyCode.J) || Input.GetKey(KeyCode.E);
#endif
            bool isHolding = _mobileButtonPressed || pcHold;

            // 4. 尚未进入交互时：必须长按按键满 0.4 秒才触发，杜绝直接走入水域自动喝水的Bug
            if (currentInteraction == InteractionType.None)
            {
                if (isHolding && CanInteract)
                {
                    _holdTimer += Time.deltaTime;
                    if (_holdTimer >= triggerDelay)
                    {
                        if (CanEatPrey)
                        {
                            StartInteraction(InteractionType.EatingPrey);
                        }
                        else if (CanDrinkWater)
                        {
                            StartInteraction(InteractionType.DrinkingWater);
                        }
                    }
                }
                else
                {
                    _holdTimer = 0f;
                }
            }
            // 5. 正在交互中：按键松开立即打断，或数值回满自动打断
            else
            {
                if (!isHolding)
                {
                    InterruptInteraction("按键松开，动作打断");
                    return;
                }

                if (currentInteraction == InteractionType.DrinkingWater)
                {
                    if (!isNearWater || (PlayerStatusUI.Instance != null && PlayerStatusUI.Instance.Thirst >= 100f))
                    {
                        InterruptInteraction("口渴已满或脱离水域，自动完成喝水");
                        return;
                    }

                    actionTimer += Time.deltaTime;
                    if (actionTimer >= drinkInterval)
                    {
                        actionTimer = 0f;
                        PlayerStatusUI.Instance?.ModifyThirst(drinkRecoveryAmount);
                        Debug.Log($"<color=#33CCFF>[DinoInteraction] 长按喝水中... 恢复 {drinkRecoveryAmount} 点水量。</color>");
                    }
                }
                else if (currentInteraction == InteractionType.EatingPrey)
                {
                    if (ediblePrey == null || ediblePrey.RemainingFeedCount <= 0 || (PlayerStatusUI.Instance != null && PlayerStatusUI.Instance.Food >= 100f))
                    {
                        InterruptInteraction("饱食度已满或猎物吃完，自动完成进食");
                        return;
                    }

                    actionTimer += Time.deltaTime;
                    if (actionTimer >= ediblePrey.FeedBiteDuration)
                    {
                        actionTimer = 0f;
                        bool success = ediblePrey.BiteFeed();
                        if (!success)
                        {
                            InterruptInteraction("进食结束");
                        }
                    }
                }
            }
        }

        private void StartInteraction(InteractionType type)
        {
            currentInteraction = type;
            actionTimer = 0f;
            stayTimer = 0f;

            // 进食/喝水时不可以移动！
            if (_playerController != null)
            {
                _playerController.MovementLocked = true;
            }

            Debug.Log($"<color=#FFFF33>[DinoInteraction] 开始执行动作: {type}，角色位移已锁定。</color>");
        }

        public void InterruptInteraction(string reason)
        {
            if (currentInteraction != InteractionType.None)
            {
                currentInteraction = InteractionType.None;
                actionTimer = 0f;
                stayTimer = 0f;

                if (_playerController != null)
                {
                    _playerController.MovementLocked = false;
                }

                Debug.Log($"[DinoInteraction] 退出动作 ({reason})，角色位移已解锁。");
            }
        }
    }
}
