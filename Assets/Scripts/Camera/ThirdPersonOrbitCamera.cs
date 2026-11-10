using UnityEngine;
using PixelSaur.Player;

namespace PixelSaur.Cam
{
    /// <summary>
    /// 第三人称轨道跟随摄像机：以人物为中心居中对齐，支持根据输入绕人物自由旋转 Yaw 与 Pitch，并自带防穿模碰撞检测。
    /// </summary>
    public class ThirdPersonOrbitCamera : MonoBehaviour
    {
        [Header("跟随目标")]
        [Tooltip("跟随的角色 Transform")]
        [SerializeField] private Transform target;

        [Tooltip("目标中心点垂直偏移（确保角色居中显示在屏幕中央）")]
        [SerializeField] private Vector3 targetOffset = new Vector3(0f, 0.35f, 0f);

        [Header("距离与角度")]
        [Tooltip("相机距离目标的默认距离")]
        [Range(2f, 15f)]
        [SerializeField] private float defaultDistance = 4.2f;

        [Tooltip("相机最小距离（碰撞缩进极限）")]
        [Range(0.5f, 3f)]
        [SerializeField] private float minDistance = 1.0f;

        [Tooltip("俯仰角下限（防止镜头钻入地面以下）")]
        [Range(5f, 25f)]
        [SerializeField] private float minPitch = 10f;

        [Tooltip("俯仰角上限（俯视地面限制）")]
        [Range(30f, 85f)]
        [SerializeField] private float maxPitch = 68f;

        [Header("平滑与灵敏度")]
        [Tooltip("旋转平滑速度")]
        [Range(5f, 50f)]
        [SerializeField] private float rotationSmoothSpeed = 25f;

        [Tooltip("跟随移动平滑时间")]
        [Range(0f, 0.5f)]
        [SerializeField] private float positionSmoothTime = 0.04f;

        [Header("防穿模遮挡检测")]
        [Tooltip("碰撞检测层级（如地面、障碍物）")]
        [SerializeField] private LayerMask collisionLayers = ~0;

        [Tooltip("碰撞球体半径")]
        [Range(0.05f, 0.5f)]
        [SerializeField] private float collisionRadius = 0.18f;

        private float _currentYaw = 0f;
        private float _currentPitch = 26f;
        private float _targetYaw = 0f;
        private float _targetPitch = 26f;
        private float _currentDistance;
        private Vector3 _smoothVelocity;

        public Transform Target => target;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            SnapToTargetImmediate();
        }

        public void SnapToTargetImmediate()
        {
            if (target == null) return;
            _currentYaw = _targetYaw = 0f;
            _currentPitch = _targetPitch = 26f;
            _currentDistance = defaultDistance;
            targetOffset = new Vector3(0f, 0.35f, 0f);

            Quaternion rot = Quaternion.Euler(_currentPitch, _currentYaw, 0f);
            Vector3 pivot = target.position + targetOffset;
            transform.position = pivot + (rot * -Vector3.forward) * _currentDistance;
            transform.rotation = rot;
        }

        private void Start()
        {
            if (target == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) target = p.transform;
            }
            _currentDistance = defaultDistance;
            _targetPitch = Mathf.Clamp(_targetPitch, minPitch, maxPitch);
            _currentPitch = _targetPitch;
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
            _currentYaw = Mathf.LerpAngle(_currentYaw, _targetYaw, Time.deltaTime * rotationSmoothSpeed);
            _currentPitch = Mathf.Lerp(_currentPitch, _targetPitch, Time.deltaTime * rotationSmoothSpeed);

            Quaternion rotation = Quaternion.Euler(_currentPitch, _currentYaw, 0f);
            Vector3 targetPivot = target.position + targetOffset;

            Vector3 desiredDirection = rotation * -Vector3.forward;
            float targetDistance = defaultDistance;

            // 遮挡检测（排除角色自身碰撞体）
            RaycastHit[] hits = Physics.SphereCastAll(targetPivot, collisionRadius, desiredDirection, defaultDistance, collisionLayers, QueryTriggerInteraction.Ignore);
            float closestDist = defaultDistance;
            foreach (RaycastHit hit in hits)
            {
                if (hit.transform == target || hit.transform.IsChildOf(target)) continue;
                if (hit.distance < closestDist)
                {
                    closestDist = hit.distance;
                }
            }
            if (closestDist < defaultDistance)
            {
                targetDistance = Mathf.Clamp(closestDist - 0.1f, minDistance, defaultDistance);
            }

            _currentDistance = Mathf.Lerp(_currentDistance, targetDistance, Time.deltaTime * 15f);

            Vector3 finalPosition = targetPivot + desiredDirection * _currentDistance;
            transform.position = Vector3.SmoothDamp(transform.position, finalPosition, ref _smoothVelocity, positionSmoothTime);
            transform.rotation = rotation;
        }
    }
}
