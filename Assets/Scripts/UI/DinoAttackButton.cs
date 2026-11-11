using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using PixelSaur.Combat;

namespace PixelSaur.UI
{
    /// <summary>
    /// 移动端恐龙攻击与冲击技能按钮：
    /// 1. 保持正圆形外观（同虚拟摇杆采用平滑抗锯齿正圆形半透明磨砂底盘）；
    /// 2. 短按单击：触发 0.8s 间隔的基础撕咬攻击；
    /// 3. 短按双击（0.35s 内连击）：触发恐龙冲击猛冲技能（MOBA大范围直接锁定撞击，消耗 25 饱食度并震屏）；
    /// 4. 处于水域或猎物尸体时长按 0.4 秒：开始喝水或进食，角色锁定移动，松手（松开）立即打断退出。
    /// </summary>
    [ExecuteAlways]
    public class DinoAttackButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
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
        private float _pointerDownTime = -999f;
        private bool _isPointerDown = false;
        private Vector3 _originalScale = Vector3.one;
        private static Sprite _cachedCircleSprite;

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
                if (_cachedCircleSprite == null)
                {
                    _cachedCircleSprite = CreateAntiAliasedCircleSprite(256, true);
                }
                _bgImage.sprite = _cachedCircleSprite;
                _bgImage.type = Image.Type.Simple;
                _bgImage.preserveAspect = true;
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

        private static Sprite CreateAntiAliasedCircleSprite(int size, bool withOuterRing)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "ProceduralAttackButtonCircle",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color[] pixels = new Color[size * size];
            float center = (size - 1) * 0.5f;
            float maxRadius = size * 0.48f;
            float aaWidth = 2.0f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float outerAlpha = Mathf.Clamp01((maxRadius - dist) / aaWidth);

                    if (outerAlpha <= 0f)
                    {
                        pixels[y * size + x] = Color.clear;
                    }
                    else if (withOuterRing)
                    {
                        // 外圈带高亮描边环，内部半透明磨砂圆盘（同虚拟摇杆）
                        float ringInner = maxRadius - (size * 0.045f);
                        float ringFactor = Mathf.Clamp01((dist - ringInner) / aaWidth);
                        float brightness = Mathf.Lerp(0.25f, 0.95f, ringFactor);
                        float alpha = Mathf.Lerp(0.55f, 0.92f, ringFactor) * outerAlpha;
                        pixels[y * size + x] = new Color(brightness, brightness, brightness, alpha);
                    }
                    else
                    {
                        float radialT = Mathf.Clamp01(dist / maxRadius);
                        float shade = Mathf.Lerp(1.0f, 0.88f, radialT * radialT);
                        pixels[y * size + x] = new Color(shade, shade, shade, outerAlpha);
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            Sprite sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            sp.name = tex.name;
            sp.hideFlags = HideFlags.HideAndDontSave;
            return sp;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _isPointerDown = true;
            _pointerDownTime = Time.time;
            transform.localScale = _originalScale * pressScale;
            if (_bgImage != null) _bgImage.color = pressColor;

            // 通知交互系统：按键已按下（长按0.4秒触发喝水或进食）
            if (DinoInteractionController.Instance != null)
            {
                DinoInteractionController.Instance.SetButtonPressed(true);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            bool wasHoldingAction = false;
            float pressDuration = Time.time - _pointerDownTime;

            if (DinoInteractionController.Instance != null)
            {
                wasHoldingAction = DinoInteractionController.Instance.IsInteracting;
                DinoInteractionController.Instance.SetButtonPressed(false);
            }

            _isPointerDown = false;
            transform.localScale = _originalScale;
            if (_bgImage != null) _bgImage.color = normalColor;

            // 若本次按压是长按且正在喝水或进食，松手仅作为打断退出，不触发撕咬攻击
            if (wasHoldingAction || pressDuration >= 0.4f)
            {
                return;
            }

            // 短按：判定单发咬击还是双击猛冲
            HandleTapClick();
        }

        private void HandleTapClick()
        {
            float timeSinceLastClick = Time.time - _lastClickTime;
            _lastClickTime = Time.time;

            if (timeSinceLastClick <= doubleClickThreshold)
            {
                // 双击判定：触发 MOBA 锁定冲刺技能！
                if (DinoCombatController.Instance != null)
                {
                    DinoCombatController.Instance.TryChargeDashSkill();
                }
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
