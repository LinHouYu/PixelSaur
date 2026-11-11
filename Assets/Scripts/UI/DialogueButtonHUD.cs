using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace PixelSaur.UI
{
    /// <summary>
    /// 左下角对话按钮（DialogueButton）：
    /// 1. 采用与虚拟摇杆统一的深色圆盘与像素对话风格；
    /// 2. 搭载 32x32 像素对话图标（icon_dialogue_32x32.png）；
    /// 3. 支持触控点击高亮/按压反馈与外部事件派发；
    /// 4. 提供位置与尺寸的 Inspector 开放微调，绝不因更新而恢复出厂设置。
    /// </summary>
    [ExecuteAlways]
    public class DialogueButtonHUD : MonoBehaviour
    {
        public static DialogueButtonHUD Instance { get; private set; }

        [Header("资源与组件引用")]
        [Tooltip("32*32 像素对话图标（圆底+气泡）")]
        [SerializeField] private Sprite dialogueIconSprite;
        [SerializeField] private Image buttonImage;
        [SerializeField] private Button button;

        [Header("位置与尺寸 (可在 Inspector 中自由微调)")]
        [Tooltip("左下角锚点偏移位置 (X, Y)")]
        [SerializeField] private Vector2 buttonPosition = new Vector2(75f, 75f);

        [Tooltip("按钮尺寸（宽, 高）")]
        [SerializeField] private Vector2 buttonSize = new Vector2(68f, 68f);

        [Header("交互事件")]
        [Tooltip("点击对话按钮时触发的 Unity 事件")]
        public UnityEvent onDialogueClicked;

        private RectTransform _rectTransform;

        public Vector2 ButtonPosition => buttonPosition;
        public Vector2 ButtonSize => buttonSize;

        private void Awake()
        {
            if (Application.isPlaying)
            {
                if (Instance == null) Instance = this;
                else if (Instance != this) { Destroy(gameObject); return; }
            }

            LoadDefaultSprite();
            EnsureUIHierarchy();
        }

        private void OnEnable()
        {
            LoadDefaultSprite();
            EnsureUIHierarchy();
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClick);
                button.onClick.AddListener(HandleClick);
            }
        }

        private void OnDisable()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClick);
            }
        }

        private void LoadDefaultSprite()
        {
#if UNITY_EDITOR
            if (dialogueIconSprite == null)
            {
                dialogueIconSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_dialogue_32x32.png");
            }
#endif
        }

        /// <summary>
        /// 确保 UI 节点层级与 RectTransform 约束完好
        /// </summary>
        public void EnsureUIHierarchy()
        {
            _rectTransform = GetComponent<RectTransform>();
            if (_rectTransform == null) _rectTransform = gameObject.AddComponent<RectTransform>();

            // 1. 左下角锚点对齐 (AnchorMin = 0,0; AnchorMax = 0,0)
            _rectTransform.anchorMin = new Vector2(0f, 0f);
            _rectTransform.anchorMax = new Vector2(0f, 0f);
            _rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _rectTransform.anchoredPosition = buttonPosition;
            _rectTransform.sizeDelta = buttonSize;

            // 2. 图像渲染配置
            buttonImage = GetComponent<Image>();
            if (buttonImage == null) buttonImage = gameObject.AddComponent<Image>();
            if (dialogueIconSprite != null)
            {
                buttonImage.sprite = dialogueIconSprite;
            }
            buttonImage.color = Color.white;
            buttonImage.preserveAspect = true;
            buttonImage.raycastTarget = true;

            // 3. 按钮交互配置
            button = GetComponent<Button>();
            if (button == null) button = gameObject.AddComponent<Button>();
            button.targetGraphic = buttonImage;

            ColorBlock cb = button.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(0.92f, 0.95f, 1.0f, 1f);
            cb.pressedColor = new Color(0.70f, 0.80f, 0.95f, 1f);
            cb.selectedColor = Color.white;
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.1f;
            button.colors = cb;
        }

        private void HandleClick()
        {
            Debug.Log("<color=#66CCFF>[DialogueButtonHUD] 对话按钮被点击！触发对话系统交互。</color>");
            onDialogueClicked?.Invoke();
        }

        /// <summary>
        /// 外部公共接口：动态调整左下角位置（代码或编辑器微调）
        /// </summary>
        public void SetPosition(Vector2 newPos)
        {
            buttonPosition = newPos;
            if (_rectTransform != null) _rectTransform.anchoredPosition = buttonPosition;
        }

        /// <summary>
        /// 外部公共接口：动态调整按钮尺寸
        /// </summary>
        public void SetSize(Vector2 newSize)
        {
            buttonSize = newSize;
            if (_rectTransform != null) _rectTransform.sizeDelta = buttonSize;
        }
    }
}
