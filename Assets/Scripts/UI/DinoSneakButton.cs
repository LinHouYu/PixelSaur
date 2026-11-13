using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using PixelSaur.Player;

namespace PixelSaur.UI
{
    /// <summary>
    /// 移动端蹲下潜行虚拟按键：位于攻击按钮左侧，点击切换角色蹲下/潜行状态并播放合成潜行音效。
    /// </summary>
    [ExecuteAlways]
    public class DinoSneakButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [Header("按压与潜行高亮反馈")]
        [SerializeField] private float pressScale = 0.90f;
        [SerializeField] private float activeScale = 0.95f;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color pressColor = new Color(0.76f, 0.76f, 0.76f, 1f);
        [SerializeField] private Color activeSneakColor = new Color(1.0f, 0.92f, 0.45f, 1f);

        [Header("图标资产")]
        [SerializeField] private Sprite sneakIconSprite;

        private Image _bgImage;
        private bool _isPointerDown = false;
        private bool _lastSyncedSneak = false;
        private readonly Vector3 _originalScale = Vector3.one;

        private void Awake()
        {
            EnsureAppearance();
        }

        private void OnEnable()
        {
            EnsureAppearance();
        }

        private void OnValidate()
        {
            EnsureAppearance();
        }

        private void Update()
        {
            if (!Application.isPlaying || _isPointerDown) return;
            bool sneaking = PlayerInputProxy.Instance != null && PlayerInputProxy.Instance.IsSneaking;
            if (sneaking != _lastSyncedSneak)
            {
                _lastSyncedSneak = sneaking;
                RefreshVisualState(sneaking);
            }
        }

        public void EnsureAppearance()
        {
#if UNITY_EDITOR
            if (sneakIconSprite == null)
            {
                sneakIconSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_dino_sneak_32x32.png");
            }
#endif
            _bgImage = GetComponent<Image>();
            if (_bgImage != null)
            {
                if (sneakIconSprite != null) _bgImage.sprite = sneakIconSprite;
                _bgImage.type = Image.Type.Simple;
                _bgImage.preserveAspect = true;
                _bgImage.raycastTarget = true;
                RefreshVisualState(Application.isPlaying && PlayerInputProxy.Instance != null && PlayerInputProxy.Instance.IsSneaking);
            }
        }

        private void RefreshVisualState(bool sneaking)
        {
            if (!_isPointerDown)
            {
                transform.localScale = sneaking ? _originalScale * activeScale : _originalScale;
            }
            if (_bgImage != null)
            {
                _bgImage.color = sneaking ? activeSneakColor : normalColor;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _isPointerDown = true;
            transform.localScale = _originalScale * pressScale;
            if (_bgImage != null) _bgImage.color = pressColor;

            if (Application.isPlaying && PlayerInputProxy.Instance != null)
            {
                bool nowSneaking = PlayerInputProxy.Instance.ToggleSneak();
                _lastSyncedSneak = nowSneaking;
                UISoundManager.PlaySneak(nowSneaking);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _isPointerDown = false;
            bool sneaking = Application.isPlaying && PlayerInputProxy.Instance != null && PlayerInputProxy.Instance.IsSneaking;
            _lastSyncedSneak = sneaking;
            RefreshVisualState(sneaking);
        }
    }
}
