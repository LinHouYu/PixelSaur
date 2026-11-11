using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using PixelSaur.Combat;

namespace PixelSaur.UI
{
    /// <summary>
    /// 移动端恐龙攻击与冲击技能按钮：
    /// 1. 单击：触发 0.8s 间隔的基础撕咬攻击；
    /// 2. 双击（0.35s 内连击）：触发恐龙冲击猛冲技能（消耗 25 饱食度并震屏）；
    /// 3. 外观保持正圆形深色半透明底盘，内嵌 32x32 像素恐龙利齿撕咬图标。
    /// </summary>
    [ExecuteAlways]
    public class DinoAttackButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        [Header("双击阈值配置")]
        [Tooltip("判定双击技能的最大时间间隔（秒）")]
        [Range(0.2f, 0.5f)]
        [SerializeField] private float doubleClickThreshold = 0.35f;

        [Header("按压反馈效果")]
        [SerializeField] private float pressScale = 0.92f;
        [SerializeField] private Color normalColor = new Color(0.12f, 0.16f, 0.22f, 0.75f);
        [SerializeField] private Color pressColor = new Color(0.22f, 0.28f, 0.36f, 0.92f);

        [Header("图标资产")]
        [SerializeField] private Sprite biteIconSprite;

        private Image _bgImage;
        private Image _iconImage;
        private float _lastClickTime = -999f;
        private Vector3 _originalScale = Vector3.one;

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
            if (biteIconSprite == null)
            {
                biteIconSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_dino_bite_32x32.png");
            }
#endif
            RectTransform rt = GetComponent<RectTransform>();
            if (rt != null)
            {
                _originalScale = Vector3.one;
            }

            _bgImage = GetComponent<Image>();
            if (_bgImage != null)
            {
                _bgImage.color = normalColor;
            }

            Transform iconTr = transform.Find("Icon");
            if (iconTr != null)
            {
                _iconImage = iconTr.GetComponent<Image>();
                if (_iconImage != null)
                {
                    if (biteIconSprite != null) _iconImage.sprite = biteIconSprite;
                    _iconImage.preserveAspect = true;
                    _iconImage.raycastTarget = false;
                }
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            transform.localScale = _originalScale * pressScale;
            if (_bgImage != null) _bgImage.color = pressColor;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            transform.localScale = _originalScale;
            if (_bgImage != null) _bgImage.color = normalColor;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            float timeSinceLastClick = Time.time - _lastClickTime;
            _lastClickTime = Time.time;

            if (timeSinceLastClick <= doubleClickThreshold)
            {
                // 双击判定成功：触发冲击技能！
                Debug.Log("[DinoAttackButton] 判定为双击！触发恐龙冲击技能！");
                if (DinoCombatController.Instance != null)
                {
                    DinoCombatController.Instance.TryChargeDashSkill();
                }
                // 重置时间，防止三击再次连续触发
                _lastClickTime = -999f;
            }
            else
            {
                // 单击判定：触发基础咬击攻击
                if (DinoCombatController.Instance != null)
                {
                    DinoCombatController.Instance.TryBiteAttack();
                }
            }
        }
    }
}
