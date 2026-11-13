using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PixelSaur.Player
{
    /// <summary>
    /// 统一输入代理层：负责汇聚并分发移动端虚拟摇杆、触控视角以及 PC 键盘/鼠标输入。
    /// 严格遵循 AGENTS.md 规范，对外输出标准归一化方向向量。
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class PlayerInputProxy : MonoBehaviour
    {
        public static PlayerInputProxy Instance { get; private set; }

        [Header("PC 输入灵敏度")]
        [Tooltip("PC 键盘移动平滑阻尼")]
        [Range(1f, 30f)]
        [SerializeField] private float pcMoveSmoothness = 15f;

        [Tooltip("PC 鼠标旋转灵敏度")]
        [Range(0.1f, 5f)]
        [SerializeField] private float mouseLookSensitivity = 1.5f;

        [Header("移动端触控灵敏度")]
        [Tooltip("移动端右屏触控视角灵敏度")]
        [Range(0.01f, 2f)]
        [SerializeField] private float touchLookSensitivity = 0.2f;

        // 对外暴露的输入属性
        public Vector2 MoveInput { get; private set; }
        public Vector2 LookInput { get; private set; }
        public bool JumpRequested { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsSneaking { get; private set; }

        private Vector2 _joystickInput;
        private Vector2 _touchLookDelta;
        private Vector2 _smoothedKeyboardInput;
        private bool _jumpFlag;
        private bool _joystickSprint;

        public void SetSprint(bool sprinting)
        {
            _joystickSprint = sprinting;
            if (sprinting) IsSneaking = false;
        }

        /// <summary>
        /// 设置蹲下/潜行状态
        /// </summary>
        public void SetSneak(bool sneaking)
        {
            IsSneaking = sneaking;
        }

        /// <summary>
        /// 切换蹲下/潜行状态并返回切换后的结果
        /// </summary>
        public bool ToggleSneak()
        {
            IsSneaking = !IsSneaking;
            return IsSneaking;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Update()
        {
            UpdateMoveInput();
            UpdateLookInput();

            JumpRequested = _jumpFlag;
            _jumpFlag = false;
        }

        private void LateUpdate()
        {
            // 每帧消费完毕触控增量后重置
            _touchLookDelta = Vector2.zero;
        }

        /// <summary>
        /// 由虚拟摇杆（UI）调用的输入注入接口
        /// </summary>
        public void SetJoystickInput(Vector2 input)
        {
            _joystickInput = input;
        }

        /// <summary>
        /// 由右半屏触控区域（UI）调用的视角旋转增量注入接口
        /// </summary>
        public void SetTouchLookDelta(Vector2 delta)
        {
            _touchLookDelta += delta * touchLookSensitivity;
        }

        /// <summary>
        /// 触发跳跃
        /// </summary>
        public void TriggerJump()
        {
            _jumpFlag = true;
        }

        private void UpdateMoveInput()
        {
            Vector2 rawKeyboard = Vector2.zero;

            // 1. 读取 PC 键盘输入 (支持新旧 Input System)
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) rawKeyboard.y += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) rawKeyboard.y -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) rawKeyboard.x += 1f;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) rawKeyboard.x -= 1f;

                if (Keyboard.current.spaceKey.wasPressedThisFrame)
                {
                    _jumpFlag = true;
                }
                if (Keyboard.current.cKey.wasPressedThisFrame || Keyboard.current.leftCtrlKey.wasPressedThisFrame)
                {
                    ToggleSneak();
                    PixelSaur.UI.UISoundManager.PlaySneak(IsSneaking);
                }
            }
#else
            rawKeyboard.x = Input.GetAxisRaw("Horizontal");
            rawKeyboard.y = Input.GetAxisRaw("Vertical");
            if (Input.GetButtonDown("Jump"))
            {
                _jumpFlag = true;
            }
            if (Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.LeftControl))
            {
                ToggleSneak();
                PixelSaur.UI.UISoundManager.PlaySneak(IsSneaking);
            }
#endif

            // 平滑键盘输入
            _smoothedKeyboardInput = Vector2.MoveTowards(_smoothedKeyboardInput, rawKeyboard, Time.deltaTime * pcMoveSmoothness);

            // 2. 结合虚拟摇杆输入 (若虚拟摇杆有值则优先或叠加)
            Vector2 combined = _joystickInput.sqrMagnitude > 0.01f ? _joystickInput : _smoothedKeyboardInput;

            // 3. 严格归一化 (限制最大模长为 1.0)
            MoveInput = Vector2.ClampMagnitude(combined, 1f);

            // 4. 判定奔跑状态 (移动端上推摇杆锁定 或 PC 左Shift)
            bool pcSprint = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed) pcSprint = true;
#else
            if (Input.GetKey(KeyCode.LeftShift)) pcSprint = true;
#endif
            if (pcSprint && IsSneaking)
            {
                IsSneaking = false;
            }
            IsSprinting = !IsSneaking && (_joystickSprint || pcSprint);
        }

        private void UpdateLookInput()
        {
            Vector2 finalLook = Vector2.zero;

            // 1. 若有移动端触控输入，优先使用
            if (_touchLookDelta.sqrMagnitude > 0.0001f)
            {
                finalLook = _touchLookDelta;
            }
            else
            {
                // 2. PC 鼠标输入作为备用支持（右键按住或直接移动）
#if ENABLE_INPUT_SYSTEM
                if (Mouse.current != null)
                {
                    // 支持按住鼠标右键拖动或自由移动控制视角
                    bool isDragging = Mouse.current.rightButton.isPressed || Mouse.current.leftButton.isPressed;
                    if (isDragging)
                    {
                        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
                        finalLook = mouseDelta * mouseLookSensitivity * 0.1f;
                    }
                }
#else
                if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
                {
                    finalLook = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * mouseLookSensitivity;
                }
#endif
            }

            LookInput = finalLook;
        }
    }
}
