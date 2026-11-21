using UnityEngine;
using UnityEngine.UI;

namespace PixelSaur.UI
{
    /// <summary>
    /// MOBA 风格圆形灰色技能冷却 CD 遮罩球与倒计时数字组件：
    /// 自动贴合所在技能按键的圆形区域，按剩余冷却比例执行 Radial360 扇形旋转冷却并居中显示剩余秒数。
    /// 随按钮 sizeDelta 改变自动自适应字号，且不影响射线检测与 localScale。
    /// </summary>
    public class SkillCooldownOverlay : MonoBehaviour
    {
        private static Sprite _cachedDiscSprite;

        private GameObject _overlayRoot;
        private Image _cdDiscImage;
        private Text _cdText;
        private RectTransform _parentRT;
        private float _lastHeight = -1f;

        public static SkillCooldownOverlay EnsureOn(GameObject targetButton)
        {
            if (targetButton == null) return null;
            SkillCooldownOverlay comp = targetButton.GetComponent<SkillCooldownOverlay>();
            if (comp == null) comp = targetButton.AddComponent<SkillCooldownOverlay>();
            comp.EnsureHierarchy();
            return comp;
        }

        private void Awake()
        {
            EnsureHierarchy();
            HideImmediate();
        }

        private void EnsureHierarchy()
        {
            _parentRT = GetComponent<RectTransform>();
            if (_cachedDiscSprite == null)
            {
                _cachedDiscSprite = CreateSolidCircleSprite(128);
            }

            Transform existing = transform.Find("CooldownOverlay");
            if (existing != null)
            {
                _overlayRoot = existing.gameObject;
                _cdDiscImage = existing.Find("CDFill")?.GetComponent<Image>();
                _cdText = existing.Find("CDText")?.GetComponent<Text>();
            }

            if (_overlayRoot == null)
            {
                _overlayRoot = new GameObject("CooldownOverlay", typeof(RectTransform));
                _overlayRoot.transform.SetParent(transform, false);
                RectTransform rootRT = _overlayRoot.GetComponent<RectTransform>();
                rootRT.anchorMin = new Vector2(0.04f, 0.04f);
                rootRT.anchorMax = new Vector2(0.96f, 0.96f);
                rootRT.offsetMin = Vector2.zero;
                rootRT.offsetMax = Vector2.zero;
                rootRT.localScale = Vector3.one;

                // 1. 圆形灰色扇形冷却遮罩球
                GameObject fillGo = new GameObject("CDFill", typeof(RectTransform), typeof(Image));
                fillGo.transform.SetParent(_overlayRoot.transform, false);
                RectTransform fillRT = fillGo.GetComponent<RectTransform>();
                fillRT.anchorMin = Vector2.zero;
                fillRT.anchorMax = Vector2.one;
                fillRT.offsetMin = Vector2.zero;
                fillRT.offsetMax = Vector2.zero;
                fillRT.localScale = Vector3.one;

                _cdDiscImage = fillGo.GetComponent<Image>();
                _cdDiscImage.sprite = _cachedDiscSprite;
                _cdDiscImage.color = new Color(0.10f, 0.12f, 0.16f, 0.76f);
                _cdDiscImage.type = Image.Type.Filled;
                _cdDiscImage.fillMethod = Image.FillMethod.Radial360;
                _cdDiscImage.fillOrigin = (int)Image.Origin360.Top;
                _cdDiscImage.fillClockwise = false;
                _cdDiscImage.fillAmount = 1f;
                _cdDiscImage.preserveAspect = true;
                _cdDiscImage.raycastTarget = false;

                // 2. 居中冷却剩余秒数数字
                GameObject txtGo = new GameObject("CDText", typeof(RectTransform), typeof(Text), typeof(Outline));
                txtGo.transform.SetParent(_overlayRoot.transform, false);
                RectTransform txtRT = txtGo.GetComponent<RectTransform>();
                txtRT.anchorMin = Vector2.zero;
                txtRT.anchorMax = Vector2.one;
                txtRT.offsetMin = Vector2.zero;
                txtRT.offsetMax = Vector2.zero;
                txtRT.localScale = Vector3.one;

                _cdText = txtGo.GetComponent<Text>();
                Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                _cdText.font = font;
                _cdText.fontSize = 44;
                _cdText.fontStyle = FontStyle.Bold;
                _cdText.alignment = TextAnchor.MiddleCenter;
                _cdText.color = Color.white;
                _cdText.raycastTarget = false;

                Outline outline = txtGo.GetComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
                outline.effectDistance = new Vector2(2.0f, -2.0f);
            }

            // 置于按钮内部最顶层（确保盖在图标上但处于外圆环内侧）
            _overlayRoot.transform.SetAsLastSibling();
        }

        public void SetCooldown(float remainingSeconds, float totalDuration)
        {
            if (_overlayRoot == null) EnsureHierarchy();
            if (_overlayRoot == null) return;

            bool active = remainingSeconds > 0.01f && totalDuration > 0.01f;
            if (!active)
            {
                if (_overlayRoot.activeSelf) _overlayRoot.SetActive(false);
                return;
            }

            if (!_overlayRoot.activeSelf)
            {
                _overlayRoot.SetActive(true);
                _overlayRoot.transform.SetAsLastSibling();
            }

            if (_cdDiscImage != null)
            {
                _cdDiscImage.fillAmount = Mathf.Clamp01(remainingSeconds / totalDuration);
            }

            if (_cdText != null)
            {
                if (_parentRT != null)
                {
                    float h = _parentRT.rect.height;
                    if (h > 10f && Mathf.Abs(h - _lastHeight) > 1f)
                    {
                        _lastHeight = h;
                        _cdText.fontSize = Mathf.Clamp(Mathf.RoundToInt(h * 0.34f), 20, 76);
                    }
                }

                _cdText.text = remainingSeconds >= 10f
                    ? Mathf.CeilToInt(remainingSeconds).ToString()
                    : remainingSeconds.ToString("0.0");
            }
        }

        public void HideImmediate()
        {
            if (_overlayRoot != null && _overlayRoot.activeSelf)
            {
                _overlayRoot.SetActive(false);
            }
        }

        private static Sprite CreateSolidCircleSprite(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "SkillCooldownDiscSprite",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color[] pixels = new Color[size * size];
            float c = (size - 1) * 0.5f;
            float r = size * 0.485f;
            float aa = 1.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
                    float a = Mathf.Clamp01((r - d) / aa);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            Sprite sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            sp.name = tex.name;
            sp.hideFlags = HideFlags.HideAndDontSave;
            return sp;
        }
    }
}
