using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PixelSaur.Combat;

namespace PixelSaur.UI
{
    /// <summary>
    /// 移动端技能虚拟按钮（编辑器与运行时均可直接微调位置与大小）：
    /// 技能 1 执行冲刺技能（ChargeDash），技能 2 执行恐龙吼叫动画技能（Roar）。
    /// </summary>
    [ExecuteAlways]
    public class DinoSkillButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public enum SkillSlot
        {
            ChargeDash = 1,
            Roar = 2
        }

        [Header("技能槽位与按压反馈")]
        [SerializeField] private SkillSlot skillSlot = SkillSlot.ChargeDash;
        [SerializeField] private float pressScale = 0.90f;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color pressColor = new Color(0.76f, 0.76f, 0.76f, 1f);

        [Header("图标资产")]
        [SerializeField] private Sprite skillIconSprite;

        private Image _iconImage;
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
            if (skillIconSprite == null)
            {
                string path = skillSlot == SkillSlot.Roar
                    ? "Assets/Textures/UI/skill1.png"
                    : "Assets/Textures/UI/skill3.png";
                skillIconSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
#endif
            _iconImage = GetComponent<Image>();
            if (_iconImage != null)
            {
                if (skillIconSprite != null) _iconImage.sprite = skillIconSprite;
                _iconImage.type = Image.Type.Simple;
                _iconImage.preserveAspect = true;
                _iconImage.color = normalColor;
                _iconImage.raycastTarget = true;
            }
        }

        public void SetIconSprite(Sprite sprite)
        {
            skillIconSprite = sprite;
            EnsureAppearance();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            transform.localScale = _originalScale * pressScale;
            if (_iconImage != null) _iconImage.color = pressColor;

            if (!Application.isPlaying || DinoCombatController.Instance == null) return;

            if (skillSlot == SkillSlot.Roar)
            {
                DinoCombatController.Instance.TryRoarSkill();
            }
            else
            {
                UISoundManager.PlayClick();
                DinoCombatController.Instance.TryChargeDashSkill();
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            transform.localScale = _originalScale;
            if (_iconImage != null) _iconImage.color = normalColor;
        }
    }
}
