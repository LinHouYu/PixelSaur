using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using PixelSaur.Player;

namespace PixelSaur.UI
{
    /// <summary>
    /// 移动端跳跃虚拟按键：位于攻击按钮上方，按下触发角色跳跃并播放合成跳跃音效。
    /// </summary>
    [ExecuteAlways]
    public class DinoJumpButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [Header("按压反馈效果")]
        [SerializeField] private float pressScale = 0.90f;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color pressColor = new Color(0.76f, 0.76f, 0.76f, 1f);

        [Header("图标资产")]
        [SerializeField] private Sprite jumpIconSprite;

        private Image _bgImage;
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

        public void EnsureAppearance()
        {
#if UNITY_EDITOR
            if (jumpIconSprite == null)
            {
                jumpIconSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_dino_jump_32x32.png");
            }
#endif
            _bgImage = GetComponent<Image>();
            if (_bgImage != null)
            {
                if (jumpIconSprite != null) _bgImage.sprite = jumpIconSprite;
                _bgImage.type = Image.Type.Simple;
                _bgImage.preserveAspect = true;
                _bgImage.color = normalColor;
                _bgImage.raycastTarget = true;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            transform.localScale = _originalScale * pressScale;
            if (_bgImage != null) _bgImage.color = pressColor;

            if (Application.isPlaying)
            {
                UISoundManager.PlayJump();
                PlayerInputProxy.Instance?.TriggerJump();
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            transform.localScale = _originalScale;
            if (_bgImage != null) _bgImage.color = normalColor;
        }
    }
}
