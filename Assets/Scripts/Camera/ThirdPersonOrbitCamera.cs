using UnityEngine;
using PixelSaur.Player;

namespace PixelSaur.Cam
{
    /// <summary>
    /// 第三人称轨道跟随摄像机：以人物为中心，支持根据输入绕人物自由旋转 Yaw 与 Pitch，并自带防穿模碰撞检测。
    /// </summary>
    public class ThirdPersonOrbitCamera : MonoBehaviour
    {
        [Header("跟随目标")]
        [Tooltip("跟随的角色 Transform")]
        [SerializeField] private Transform target;

        [Tooltip("目标中心点垂直/局部偏移（确保角色居中）")]
        [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.3f, 0f);

        [Header("距离与角度")]
        [Tooltip("相机距离目标的默认距离")]
        [Range(2f, 15f)]
        [SerializeField] private float defaultDistance = 5f;

        [Tooltip("相机最小距离（碰撞缩进极限）")]
        [Range(0.5f, 3f)]
        [SerializeField] private float minDistance = 0.8f;

        [Tooltip("俯仰角下限（仰视地面限制）")]
        [Range(-40f, 10f)]
        [SerializeField] private float minPitch = -20f;

        [Tooltip("俯仰角上限（俯视地面限制）")]
        [Range(30f, 85f)]
        [SerializeField] private float maxPitch = 70f;

        [Header("平滑与灵敏度")]
        [Tooltip("旋转平滑速度")]
        [Range(5f, 50f)]
        [SerializeField] private float rotationSmoothSpeed = 25f;

        [Tooltip("跟随移动平滑时间")]
        [Range(0f, 0.5f)]
        [SerializeField] private float positionSmoothTime = 0.05f;

        [Header("防穿模遮挡检测")]
        [Tooltip("碰撞检测层级（如地面、障碍物）")]
        [SerializeField] private LayerMask collisionLayers = ~0;

        [Tooltip("碰撞球体半径")]
        [Range(0.05f, 0.5f)]
        [SerializeField] private float collisionRadius = 0.2f;

        private float _currentYaw;
        private float _currentPitch = 15f;
        private float _targetYaw;
        private float _targetPitch = 15f;
        private float _currentDistance;
        private Vector3 _smoothVelocity;

        public Transform Target => target;
        public void SetTarget(Transform newTarget) => target = newTarget;

        private void Start()
        {
            Vector3 angles = transform.eulerAngles;
            _currentYaw = _targetYaw = angles.y;
            _currentPitch = _targetPitch = angles.x;
            _currentDistance = defaultDistance;
        }

        private void LateUpdate()
        {
            if (target == null) return;

            HandleInput();
            CalculatePositionAndRotation();
        }

        private void HandleInput()
        {
            if (PlayerInputProxy.Instance != null)
            {
                Vector2 lookInput = PlayerInputProxy.Instance.LookInput;
                _targetYaw += lookInput.x;
                _targetPitch -= lookInput.y;
                _targetPitch = Mathf.Clamp(_targetPitch, minPitch, maxPitch);
            }
        }

        private void CalculatePositionAndRotation()
        {
            // 角度平滑插值
            _currentYaw = Mathf.Lerp(_currentYaw, _targetYaw, Time.deltaTime * rotationSmoothSpeed);
            _currentPitch = Mathf.Lerp(_currentPitch, _targetPitch, Time.deltaTime * rotationSmoothSpeed);

            Quaternion rotation = Quaternion.Euler(_currentPitch, _currentYaw, 0f);
            Vector3 targetPivot = target.position + targetOffset;

            // 基础射线方向与目标位置
            Vector3 desiredDirection = rotation * -Vector3.forward;
            float targetDistance = defaultDistance;

            // 遮挡检测（SphereCast 防止穿墙与贴地穿插）
            if (Physics.SphereCast(targetPivot, collisionRadius, desiredDirection, out RaycastHit hit, defaultDistance, collisionLayers, QueryTriggerInteraction.Ignore))
            {
                targetDistance = Mathf.Clamp(hit.distance - 0.1f, minDistance, defaultDistance);
            }

            _currentDistance = Mathf.Lerp(_currentDistance, targetDistance, Time.deltaTime * 15f);

            Vector3 finalPosition = targetPivot + desiredDirection * _currentDistance;

            // 平滑移动到最终位置
            transform.position = Vector3.SmoothDamp(transform.position, finalPosition, ref _smoothVelocity, positionSmoothTime);
            transform.rotation = rotation;
        }
    }
}
