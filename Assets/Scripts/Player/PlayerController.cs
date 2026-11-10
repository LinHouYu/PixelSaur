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
        [Tooltip("基础移动速度（米/秒）")]
        [Range(1f, 15f)]
        [SerializeField] private float moveSpeed = 5.5f;

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

        public CharacterController Controller => _characterController;
        public bool IsGrounded => _characterController != null && _characterController.isGrounded;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
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
            Vector2 input = PlayerInputProxy.Instance != null ? PlayerInputProxy.Instance.MoveInput : Vector2.zero;

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

            // 3. 处理重力与跳跃
            if (_characterController.isGrounded)
            {
                if (_verticalVelocity < 0f)
                {
                    _verticalVelocity = -2f; // 贴地缓冲力
                }

                if (PlayerInputProxy.Instance != null && PlayerInputProxy.Instance.JumpRequested)
                {
                    _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * (_baseGravity * gravityMultiplier));
                }
            }
            else
            {
                _verticalVelocity += _baseGravity * gravityMultiplier * Time.deltaTime;
            }

            // 4. 组合最终位移向量并驱动 CharacterController
            Vector3 motion = (moveDirection * moveSpeed) + (Vector3.up * _verticalVelocity);
            _characterController.Move(motion * Time.deltaTime);
        }
    }
}
