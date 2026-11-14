using System.Collections.Generic;
using UnityEngine;
using PixelSaur.Combat;
using PixelSaur.UI;

namespace PixelSaur.Player
{
    /// <summary>
    /// 霸王龙模型动作与受伤贴图总控制器：
    /// 驱动 11 套骨骼动画（idle/walk/run/sprint/sneak/sneak_walk/attack/jump/eat/drink/roar），
    /// 实现喝水/进食按住循环、松手立即抬头回位，以及残血（<=30%）动态切换流血贴图。
    /// </summary>
    public class DinoAnimatorController : MonoBehaviour
    {
        public static DinoAnimatorController Instance { get; private set; }

        public const string StateIdle = "idle";
        public const string StateWalk = "walk";
        public const string StateRun = "run";
        public const string StateSprint = "sprint";
        public const string StateSneak = "sneak";
        public const string StateSneakWalk = "sneak_walk";
        public const string StateAttack = "attack";
        public const string StateJump = "jump";
        public const string StateEat = "eat";
        public const string StateDrink = "drink";
        public const string StateRoar = "roar";

        // 进食/饮水局部循环区间（归一化时间）
        private const float DrinkLoopStartNorm = 0.60f / 3.20f;
        private const float DrinkLoopEndNorm = 1.40f / 3.20f;
        private const float EatLoopStartNorm = 0.80f / 2.40f;
        private const float EatLoopEndNorm = 1.50f / 2.40f;
        private const float JumpTakeoffNorm = 0.20f / 1.20f;

        [Header("引用配置")]
        [SerializeField] private Animator modelAnimator;
        [SerializeField] private Texture2D healthyTexture;
        [SerializeField] private Texture2D injuredTexture;

        [Header("残血换肤阈值")]
        [Range(0.05f, 0.9f)]
        [SerializeField] private float injuredHealthRatio = 0.30f;

        private enum OverrideAction
        {
            None,
            Attack,
            Jump,
            Roar,
            EatHold,
            EatRelease,
            DrinkHold,
            DrinkRelease
        }

        private OverrideAction _overrideAction = OverrideAction.None;
        private string _currentLocomotion = string.Empty;
        private readonly List<Material> _runtimeMaterials = new List<Material>();
        private bool _isCurrentlyInjured = false;
        private float _overrideStartTime = 0f;

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        private void Awake()
        {
            Instance = this;
            FindAnimatorIfMissing();
            if (modelAnimator != null)
            {
                modelAnimator.applyRootMotion = false;
            }
            CollectRuntimeMaterials();
            ApplyTexture(false, force: true);
        }

        private void Update()
        {
            if (modelAnimator == null) return;

            UpdateInjuredTexture();
            UpdateAnimationState();
        }

        public void TriggerAttack()
        {
            if (modelAnimator == null) return;
            _overrideAction = OverrideAction.Attack;
            _overrideStartTime = Time.time;
            _currentLocomotion = string.Empty;
            modelAnimator.CrossFadeInFixedTime(StateAttack, 0.08f, 0, 0f);
        }

        public void TriggerJump()
        {
            if (modelAnimator == null) return;
            _overrideAction = OverrideAction.Jump;
            _overrideStartTime = Time.time;
            _currentLocomotion = string.Empty;
            modelAnimator.CrossFadeInFixedTime(StateJump, 0.08f, 0, JumpTakeoffNorm * 1.20f);
        }

        public void TriggerRoar()
        {
            if (modelAnimator == null) return;
            _overrideAction = OverrideAction.Roar;
            _overrideStartTime = Time.time;
            _currentLocomotion = string.Empty;
            modelAnimator.CrossFadeInFixedTime(StateRoar, 0.10f, 0, 0f);
        }

        public void BeginInteraction(InteractionType type)
        {
            if (modelAnimator == null) return;
            _overrideStartTime = Time.time;
            _currentLocomotion = string.Empty;

            if (type == InteractionType.DrinkingWater)
            {
                _overrideAction = OverrideAction.DrinkHold;
                modelAnimator.CrossFadeInFixedTime(StateDrink, 0.12f, 0, 0f);
            }
            else if (type == InteractionType.EatingPrey)
            {
                _overrideAction = OverrideAction.EatHold;
                modelAnimator.CrossFadeInFixedTime(StateEat, 0.12f, 0, 0f);
            }
        }

        public void EndInteraction(InteractionType type)
        {
            if (modelAnimator == null) return;

            if (type == InteractionType.DrinkingWater && _overrideAction == OverrideAction.DrinkHold)
            {
                _overrideAction = OverrideAction.DrinkRelease;
                _overrideStartTime = Time.time;
                // 立即跳至抬颈仰头咽水阶段并回正
                modelAnimator.Play(StateDrink, 0, DrinkLoopEndNorm);
            }
            else if (type == InteractionType.EatingPrey && _overrideAction == OverrideAction.EatHold)
            {
                _overrideAction = OverrideAction.EatRelease;
                _overrideStartTime = Time.time;
                // 立即跳至微抬头咀嚼吞咽阶段并回正
                modelAnimator.Play(StateEat, 0, EatLoopEndNorm);
            }
        }

        private void UpdateAnimationState()
        {
            bool hasMoveInput = PlayerInputProxy.Instance != null && PlayerInputProxy.Instance.MoveInput.sqrMagnitude > 0.02f;

            if (_overrideAction != OverrideAction.None)
            {
                AnimatorStateInfo info = modelAnimator.GetCurrentAnimatorStateInfo(0);
                float elapsed = Time.time - _overrideStartTime;

                switch (_overrideAction)
                {
                    case OverrideAction.DrinkHold:
                        if (info.IsName(StateDrink) && info.normalizedTime >= DrinkLoopEndNorm)
                        {
                            modelAnimator.Play(StateDrink, 0, DrinkLoopStartNorm);
                        }
                        return;

                    case OverrideAction.DrinkRelease:
                        if (hasMoveInput || (info.IsName(StateDrink) && info.normalizedTime >= 0.96f) || elapsed > 2.0f)
                        {
                            _overrideAction = OverrideAction.None;
                        }
                        else return;
                        break;

                    case OverrideAction.EatHold:
                        if (info.IsName(StateEat) && info.normalizedTime >= EatLoopEndNorm)
                        {
                            modelAnimator.Play(StateEat, 0, EatLoopStartNorm);
                        }
                        return;

                    case OverrideAction.EatRelease:
                        if (hasMoveInput || (info.IsName(StateEat) && info.normalizedTime >= 0.96f) || elapsed > 1.2f)
                        {
                            _overrideAction = OverrideAction.None;
                        }
                        else return;
                        break;

                    case OverrideAction.Attack:
                        if ((info.IsName(StateAttack) && info.normalizedTime >= 0.95f) || elapsed > 1.05f)
                        {
                            _overrideAction = OverrideAction.None;
                        }
                        else return;
                        break;

                    case OverrideAction.Roar:
                        if ((hasMoveInput && elapsed > 0.4f) || (info.IsName(StateRoar) && info.normalizedTime >= 0.96f) || elapsed > 2.45f)
                        {
                            _overrideAction = OverrideAction.None;
                        }
                        else return;
                        break;

                    case OverrideAction.Jump:
                        bool grounded = PlayerController.Instance != null && PlayerController.Instance.IsGrounded;
                        if ((grounded && elapsed > 0.28f) || (info.IsName(StateJump) && info.normalizedTime >= 0.96f) || elapsed > 1.3f)
                        {
                            _overrideAction = OverrideAction.None;
                        }
                        else return;
                        break;
                }
            }

            // 常规移动状态机
            string targetState = ResolveLocomotionState();
            if (targetState != _currentLocomotion)
            {
                _currentLocomotion = targetState;
                modelAnimator.CrossFadeInFixedTime(targetState, 0.15f, 0);
            }
        }

        private string ResolveLocomotionState()
        {
            PlayerController pc = PlayerController.Instance;
            PlayerInputProxy input = PlayerInputProxy.Instance;

            bool isDashing = pc != null && pc.IsDashing;
            if (isDashing) return StateSprint;

            bool isSneaking = pc != null && pc.IsSneaking;
            bool isMoving = input != null && input.MoveInput.sqrMagnitude > 0.01f;
            bool isSprinting = !isSneaking && input != null && input.IsSprinting && isMoving
                               && (PlayerStatusUI.Instance == null || PlayerStatusUI.Instance.Thirst > 0.01f);

            if (isSneaking)
            {
                return isMoving ? StateSneakWalk : StateSneak;
            }
            if (!isMoving)
            {
                return StateIdle;
            }
            return isSprinting ? StateRun : StateWalk;
        }

        private void UpdateInjuredTexture()
        {
            if (PlayerStatusUI.Instance == null) return;
            bool shouldBeInjured = (PlayerStatusUI.Instance.Health / 100f) <= injuredHealthRatio;
            if (shouldBeInjured != _isCurrentlyInjured)
            {
                ApplyTexture(shouldBeInjured, force: false);
            }
        }

        private void ApplyTexture(bool injured, bool force)
        {
            if (!force && injured == _isCurrentlyInjured) return;
            _isCurrentlyInjured = injured;

            Texture2D targetTex = injured ? injuredTexture : healthyTexture;
            if (targetTex == null) return;

            for (int i = 0; i < _runtimeMaterials.Count; i++)
            {
                Material mat = _runtimeMaterials[i];
                if (mat == null) continue;
                if (mat.HasProperty(BaseMapId)) mat.SetTexture(BaseMapId, targetTex);
                if (mat.HasProperty(MainTexId)) mat.SetTexture(MainTexId, targetTex);
            }
        }

        private void FindAnimatorIfMissing()
        {
            if (modelAnimator == null)
            {
                modelAnimator = GetComponentInChildren<Animator>();
            }
        }

        private void CollectRuntimeMaterials()
        {
            _runtimeMaterials.Clear();
            if (modelAnimator == null) return;

            Renderer[] renderers = modelAnimator.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                Material[] mats = Application.isPlaying ? r.materials : r.sharedMaterials;
                if (mats == null) continue;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null && !_runtimeMaterials.Contains(mats[i]))
                    {
                        _runtimeMaterials.Add(mats[i]);
                    }
                }
            }
        }
    }
}
