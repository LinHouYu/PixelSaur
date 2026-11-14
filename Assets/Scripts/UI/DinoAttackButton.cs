using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using PixelSaur.Combat;

namespace PixelSaur.UI
{
    /// <summary>
    /// 绉诲姩绔亹榫欐敾鍑讳笌鍐插嚮鎶€鑳芥寜閽細
    /// 1. 淇濇寔姝ｅ渾褰㈠瑙傦紙鍚岃櫄鎷熸憞鏉嗛噰鐢ㄥ钩婊戞姉閿娇姝ｅ渾褰㈠崐閫忔槑纾ㄧ爞搴曠洏锛夛紱
    /// 2. 鐭寜鍗曞嚮锛氳Е鍙?0.8s 闂撮殧鐨勫熀纭€鎾曞挰鏀诲嚮锛?
    /// 3. 鐭寜鍙屽嚮锛?.35s 鍐呰繛鍑伙級锛氳Е鍙戞亹榫欏啿鍑荤寷鍐叉妧鑳斤紙MOBA澶ц寖鍥寸洿鎺ラ攣瀹氭挒鍑伙紝娑堣€?25 楗遍搴﹀苟闇囧睆锛夛紱
    /// 4. 澶勪簬姘村煙鎴栫寧鐗╁案浣撴椂闀挎寜 0.4 绉掞細寮€濮嬪枬姘存垨杩涢锛岃鑹查攣瀹氱Щ鍔紝鏉炬墜锛堟澗寮€锛夌珛鍗虫墦鏂€€鍑恒€?
    /// </summary>
    [ExecuteAlways]
    public class DinoAttackButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [Header("鎸夊帇鍙嶉鏁堟灉")]
        [SerializeField] private float pressScale = 0.92f;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color pressColor = new Color(0.76f, 0.76f, 0.76f, 1f);

        [Header("鍥炬爣璧勪骇")]
        [SerializeField] private Sprite biteIconSprite;

        private Image _bgImage;
        private Image _iconImage;
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
                if (biteIconSprite != null)
                {
                    _bgImage.sprite = biteIconSprite;
                }
                else
                {
                    if (_cachedCircleSprite == null) _cachedCircleSprite = CreateAntiAliasedCircleSprite(256, true);
                    _bgImage.sprite = _cachedCircleSprite;
                }
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
                    // 鍥炬爣鏈韩宸叉槸瀹屾暣鍦嗗舰鎸夐挳璐村浘锛岄殣钘忔棫鐨勫彔鍔犲瓙鑺傜偣閬垮厤閲嶅奖
                    _iconImage.enabled = false;
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
                        // 澶栧湀甯﹂珮浜弿杈圭幆锛屽唴閮ㄥ崐閫忔槑纾ㄧ爞鍦嗙洏锛堝悓铏氭嫙鎽囨潌锛?
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

            // 閫氱煡浜や簰绯荤粺锛氭寜閿凡鎸変笅锛堥暱鎸?.4绉掕Е鍙戝枬姘存垨杩涢锛?
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

            // 鑻ユ湰娆℃寜鍘嬫槸闀挎寜涓旀鍦ㄥ枬姘存垨杩涢锛屾澗鎵嬩粎浣滀负鎵撴柇閫€鍑猴紝涓嶈Е鍙戞挄鍜敾鍑?
            if (wasHoldingAction || pressDuration >= 0.4f)
            {
                return;
            }

            // 鐭寜锛氬垽瀹氬崟鍙戝挰鍑昏繕鏄弻鍑荤寷鍐?
            HandleTapClick();
        }

        private void HandleTapClick()
        {
            if (DinoCombatController.Instance != null)
            {
                DinoCombatController.Instance.TryBiteAttack();
            }
        }
    }
}
