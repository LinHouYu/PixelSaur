using UnityEngine;

namespace PixelSaur.Player
{
    /// <summary>
    /// 胶囊体角色移动控制器：使用 CharacterController 执行移动，
    /// 输入严格基于摄像机视线在水平面的投影方向进行移动，实现流畅的第三人称操控。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("移动参数配置")]
        [Tooltip("常态走路速度（米/秒）")]
        [Range(1f, 15f)]
        [SerializeField] private float walkSpeed = 4.2f;

        [Tooltip("奔跑移动速度（米/秒）")]
        [Range(2f, 25f)]
        [SerializeField] private float runSpeed = 7.6f;

        [Tooltip("奔跑扣水时间间隔（秒）")]
        [SerializeField] private float sprintThirstInterval = 0.5f;

        [Tooltip("奔跑每次消耗口渴度数值")]
        [SerializeField] private float sprintThirstCost = 2.0f;

        [Tooltip("转向目标方向的平滑角速度")]
        [Range(360f, 1440f)]
        [SerializeField] private float rotationSpeed = 720f;

        [Header("跳跃与重力")]
        [Tooltip("跳跃高度（米）")]
        [Range(0.5f, 5f)]
        [SerializeField] private float jumpHeight = 1.2f;

        [Tooltip("重力加速度系数")]
        [Range(0.5f, 3f)]
        [SerializeField] private float gravityMultiplier = 1.5f;

        [Header("摄像机参考")]
        [Tooltip("主相机 Transform（用于计算摄像机视口相对移动），为空时自动查找主相机")]
        [SerializeField] private Transform cameraTransform;

        private CharacterController _characterController;
        private float _verticalVelocity;
        private readonly float _baseGravity = -9.81f;
        private float _sprintThirstTimer = 0f;

        // 冲击技能瞬时位移
        private Vector3 _dashVelocity = Vector3.zero;
        private float _dashTimeRemaining = 0f;

        /// <summary>
        /// 动作硬直位移锁定：进食、喝水时不可移动
        /// </summary>
        public bool MovementLocked { get; set; } = false;

        public CharacterController Controller => _characterController;
        public bool IsGrounded => _characterController != null && _characterController.isGrounded;

        /// <summary>
        /// 触发冲击技能向前冲撞位移
        /// </summary>
        public void PerformDash(Vector3 direction, float speed, float duration)
        {
            _dashVelocity = direction.normalized * speed;
            _dashTimeRemaining = duration;
        }

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            if (_characterController != null)
            {
                // 确保 CharacterController 碰撞中心与 Unity 标准 Capsule 网格中心 (0,0,0) 完全对齐，杜绝半截身体穿入地下
                _characterController.center = Vector3.zero;
                _characterController.height = 2.0f;
                _characterController.radius = 0.5f;
                _characterController.stepOffset = 0.35f;
                _characterController.skinWidth = 0.03f;
            }
            if (cameraTransform == null && UnityEngine.Camera.main != null)
            {
                cameraTransform = UnityEngine.Camera.main.transform;
            }
        }

        private void Update()
        {
            if (cameraTransform == null && UnityEngine.Camera.main != null)
            {
                cameraTransform = UnityEngine.Camera.main.transform;
            }

            HandleMovement();
        }

        private void HandleMovement()
        {
            Vector2 input = Vector2.zero;
            if (!MovementLocked && PlayerInputProxy.Instance != null)
            {
                input = PlayerInputProxy.Instance.MoveInput;
            }

            // 1. 获取摄像机前向与右向在水平面 (XZ) 的投影
            Vector3 camForward = Vector3.forward;
            Vector3 camRight = Vector3.right;

            if (cameraTransform != null)
            {
                camForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
                camRight = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
            }

            Vector3 moveDirection = (camForward * input.y + camRight * input.x);
            if (moveDirection.sqrMagnitude > 1f)
            {
                moveDirection.Normalize();
            }

            // 2. 角色面朝移动方向平滑旋转
            if (moveDirection.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }

            // 3. 奔跑判定与每0.5秒扣2点口渴度
            bool isSprinting = PlayerInputProxy.Instance != null && PlayerInputProxy.Instance.IsSprinting && !MovementLocked;
            if (isSprinting && PixelSaur.UI.PlayerStatusUI.Instance != null && PixelSaur.UI.PlayerStatusUI.Instance.Thirst <= 0.01f)
            {
                isSprinting = false; // 缺水无法奔跑
            }

            float currentSpeed = isSprinting ? runSpeed : walkSpeed;

            if (isSprinting && input.sqrMagnitude > 0.01f)
            {
                _sprintThirstTimer += Time.deltaTime;
                if (_sprintThirstTimer >= sprintThirstInterval)
                {
                    _sprintThirstTimer = 0f;
                    PixelSaur.UI.PlayerStatusUI.Instance?.ModifyThirst(-sprintThirstCost);
                }
            }
            else
            {
                _sprintThirstTimer = 0f;
            }

            // 4. 处理重力与跳跃
            if (_characterController.isGrounded)
            {
                if (_verticalVelocity < 0f)
                {
                    _verticalVelocity = -2f; // 贴地缓冲力
                }

                if (!MovementLocked && PlayerInputProxy.Instance != null && PlayerInputProxy.Instance.JumpRequested)
                {
                    _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * (_baseGravity * gravityMultiplier));
                }
            }
            else
            {
                _verticalVelocity += _baseGravity * gravityMultiplier * Time.deltaTime;
            }

            // 5. 组合最终位移向量 (包含冲击位移)
            Vector3 horizontalMotion = moveDirection * currentSpeed;
            if (_dashTimeRemaining > 0f)
            {
                _dashTimeRemaining -= Time.deltaTime;
                horizontalMotion += _dashVelocity;
            }

            Vector3 motion = horizontalMotion + (Vector3.up * _verticalVelocity);
            _characterController.Move(motion * Time.deltaTime);
        }
    }
}
