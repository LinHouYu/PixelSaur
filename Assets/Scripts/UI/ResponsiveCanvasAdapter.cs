using UnityEngine;
using UnityEngine.UI;

namespace PixelSaur.UI
{
    /// <summary>
    /// 多分辨率 Canvas 自适应与刘海屏 SafeArea 适配组件：
    /// 自动根据当前屏幕纵横比动态平滑 CanvasScaler 的 MatchWidthOrHeight，
    /// 并可为指定 SafeArea 容器同步应用 Screen.safeArea 边距。
    /// </summary>
    [ExecuteAlways]
    public class ResponsiveCanvasAdapter : MonoBehaviour
    {
        [Header("CanvasScaler 自适应参数")]
        [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
        [SerializeField] private CanvasScaler targetScaler;

        [Header("刘海屏安全区容器 (可选)")]
        [SerializeField] private RectTransform safeAreaTarget;

        private int _lastWidth = -1;
        private int _lastHeight = -1;
        private Rect _lastSafeArea = Rect.zero;

        private void Awake()
        {
            ApplyAdaptation();
        }

        private void OnEnable()
        {
            ApplyAdaptation();
        }

        private void Update()
        {
            if (Screen.width != _lastWidth || Screen.height != _lastHeight || Screen.safeArea != _lastSafeArea)
            {
                ApplyAdaptation();
            }
        }

        public void Configure(CanvasScaler scaler, RectTransform safeArea)
        {
            targetScaler = scaler;
            safeAreaTarget = safeArea;
            ApplyAdaptation();
        }

        public void ApplyAdaptation()
        {
            int w = Mathf.Max(1, Screen.width);
            int h = Mathf.Max(1, Screen.height);
            _lastWidth = w;
            _lastHeight = h;

            if (targetScaler == null)
            {
                targetScaler = GetComponent<CanvasScaler>();
            }

            if (targetScaler != null)
            {
                targetScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                targetScaler.referenceResolution = referenceResolution;
                targetScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

                float refAspect = referenceResolution.x / Mathf.Max(1f, referenceResolution.y);
                float curAspect = (float)w / h;
                // 宽屏手机适配高度 (1.0)，窄屏/平板适配宽度 (0.0)，标准 16:9 居中 (0.5)
                if (curAspect > refAspect + 0.05f)
                {
                    targetScaler.matchWidthOrHeight = 1f;
                }
                else if (curAspect < refAspect - 0.05f)
                {
                    targetScaler.matchWidthOrHeight = 0f;
                }
                else
                {
                    targetScaler.matchWidthOrHeight = 0.5f;
                }
            }

            Rect sa = Screen.safeArea;
            _lastSafeArea = sa;
            if (safeAreaTarget != null && w > 0 && h > 0)
            {
                Vector2 anchorMin = new Vector2(sa.x / w, sa.y / h);
                Vector2 anchorMax = new Vector2((sa.x + sa.width) / w, (sa.y + sa.height) / h);
                safeAreaTarget.anchorMin = anchorMin;
                safeAreaTarget.anchorMax = anchorMax;
                safeAreaTarget.offsetMin = Vector2.zero;
                safeAreaTarget.offsetMax = Vector2.zero;
            }
        }
    }
}
