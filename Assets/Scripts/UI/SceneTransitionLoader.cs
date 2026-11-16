using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PixelSaur.UI
{
    /// <summary>
    /// 跨场景异步加载与过渡控制器：
    /// 点击【开始游戏】后淡入加载遮罩，执行 SceneManager.LoadSceneAsync 异步切换至 3D 岛屿场景，
    /// 实时更新进度条与百分比，场景就绪后同步全套用户设置并平滑淡出。
    /// </summary>
    public class SceneTransitionLoader : MonoBehaviour
    {
        public static SceneTransitionLoader Instance { get; private set; }

        [Header("过渡动画参数")]
        [SerializeField] private float fadeDuration = 0.30f;
        [SerializeField] private float minLoadingDisplaySeconds = 1.15f;

        private Canvas _overlayCanvas;
        private CanvasGroup _canvasGroup;
        private Slider _progressSlider;
        private Text _percentText;
        private Text _statusText;
        private bool _isLoading;

        public static void LoadSceneWithTransition(string sceneName, string statusHint = "正在进入 3D 恐龙岛屿...")
        {
            SceneTransitionLoader loader = EnsureInstance();
            loader.BeginLoad(sceneName, statusHint);
        }

        private static SceneTransitionLoader EnsureInstance()
        {
            if (Instance != null) return Instance;

            GameObject go = new GameObject("SceneTransitionLoader");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<SceneTransitionLoader>();
            Instance.BuildOverlayUI();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            BuildOverlayUI();
        }

        private void BeginLoad(string sceneName, string statusHint)
        {
            if (_isLoading) return;
            BuildOverlayUI();
            StartCoroutine(LoadRoutine(sceneName, statusHint));
        }

        private IEnumerator LoadRoutine(string sceneName, string statusHint)
        {
            _isLoading = true;
            _overlayCanvas.gameObject.SetActive(true);
            if (_statusText != null) _statusText.text = statusHint;
            SetProgress(0f);

            // 1. 淡入遮罩
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Clamp01(t / Mathf.Max(0.01f, fadeDuration));
                yield return null;
            }
            _canvasGroup.alpha = 1f;

            // 2. 异步加载目标场景
            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
            if (op == null)
            {
                Debug.LogError($"[SceneTransitionLoader] 无法加载场景: {sceneName}，请检查 Build Settings。");
                _canvasGroup.alpha = 0f;
                _overlayCanvas.gameObject.SetActive(false);
                _isLoading = false;
                yield break;
            }

            op.allowSceneActivation = false;
            float elapsed = 0f;

            while (!op.isDone)
            {
                elapsed += Time.unscaledDeltaTime;
                float rawProgress = Mathf.Clamp01(op.progress / 0.9f);
                float minTimeProgress = Mathf.Clamp01(elapsed / Mathf.Max(0.2f, minLoadingDisplaySeconds));
                float shownProgress = Mathf.Min(rawProgress, minTimeProgress);
                SetProgress(shownProgress);

                if (rawProgress >= 0.999f && elapsed >= minLoadingDisplaySeconds)
                {
                    SetProgress(1f);
                    op.allowSceneActivation = true;
                }
                yield return null;
            }

            // 3. 新场景首帧后同步全部设置（画质/帧率/灵敏度/音量）
            yield return null;
            SubSettingsPageHUD.ApplyAllSavedSettingsAtStartup();
            UISoundManager.ApplySavedAudioVolumes();

            // 4. 淡出遮罩
            t = 0f;
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                _canvasGroup.alpha = 1f - Mathf.Clamp01(t / Mathf.Max(0.01f, fadeDuration));
                yield return null;
            }
            _canvasGroup.alpha = 0f;
            _overlayCanvas.gameObject.SetActive(false);
            _isLoading = false;
        }

        private void SetProgress(float value01)
        {
            float clamped = Mathf.Clamp01(value01);
            if (_progressSlider != null) _progressSlider.value = clamped;
            if (_percentText != null) _percentText.text = $"{Mathf.RoundToInt(clamped * 100f)}%";
        }

        private void BuildOverlayUI()
        {
            if (_overlayCanvas != null) return;

            GameObject canvasGo = new GameObject("LoadingTransitionCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            canvasGo.transform.SetParent(transform, false);

            _overlayCanvas = canvasGo.GetComponent<Canvas>();
            _overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _overlayCanvas.sortingOrder = 9999;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            _canvasGroup = canvasGo.GetComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = true;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 背景遮罩
            GameObject bgGo = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(canvasGo.transform, false);
            RectTransform bgRT = bgGo.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;
            bgGo.GetComponent<Image>().color = new Color(0.04f, 0.06f, 0.09f, 1f);

            // 居中标题
            GameObject titleGo = new GameObject("LoadingTitle", typeof(RectTransform), typeof(Text));
            titleGo.transform.SetParent(bgGo.transform, false);
            RectTransform titRT = titleGo.GetComponent<RectTransform>();
            titRT.anchorMin = new Vector2(0.5f, 0.5f);
            titRT.anchorMax = new Vector2(0.5f, 0.5f);
            titRT.sizeDelta = new Vector2(800f, 90f);
            titRT.anchoredPosition = new Vector2(0f, 90f);
            Text titTxt = titleGo.GetComponent<Text>();
            titTxt.font = font;
            titTxt.fontSize = 62;
            titTxt.fontStyle = FontStyle.Bold;
            titTxt.alignment = TextAnchor.MiddleCenter;
            titTxt.color = Color.white;
            titTxt.text = "PIXELSaur";

            // 状态文本
            GameObject statusGo = new GameObject("LoadingStatus", typeof(RectTransform), typeof(Text));
            statusGo.transform.SetParent(bgGo.transform, false);
            RectTransform stRT = statusGo.GetComponent<RectTransform>();
            stRT.anchorMin = new Vector2(0.5f, 0.5f);
            stRT.anchorMax = new Vector2(0.5f, 0.5f);
            stRT.sizeDelta = new Vector2(800f, 44f);
            stRT.anchoredPosition = new Vector2(0f, 16f);
            _statusText = statusGo.GetComponent<Text>();
            _statusText.font = font;
            _statusText.fontSize = 26;
            _statusText.fontStyle = FontStyle.Bold;
            _statusText.alignment = TextAnchor.MiddleCenter;
            _statusText.color = new Color(0.72f, 0.84f, 0.96f, 0.95f);
            _statusText.text = "正在进入 3D 恐龙岛屿...";

            // 进度条 Slider
            GameObject sliderGo = new GameObject("LoadingProgressBar", typeof(RectTransform), typeof(Image), typeof(Slider));
            sliderGo.transform.SetParent(bgGo.transform, false);
            RectTransform slRT = sliderGo.GetComponent<RectTransform>();
            slRT.anchorMin = new Vector2(0.5f, 0.5f);
            slRT.anchorMax = new Vector2(0.5f, 0.5f);
            slRT.sizeDelta = new Vector2(720f, 34f);
            slRT.anchoredPosition = new Vector2(0f, -52f);
            sliderGo.GetComponent<Image>().color = new Color(0.14f, 0.19f, 0.27f, 1f);

            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderGo.transform, false);
            RectTransform faRT = fillArea.GetComponent<RectTransform>();
            faRT.anchorMin = Vector2.zero;
            faRT.anchorMax = Vector2.one;
            faRT.offsetMin = new Vector2(4f, 4f);
            faRT.offsetMax = new Vector2(-4f, -4f);

            GameObject fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(fillArea.transform, false);
            RectTransform fRT = fillGo.GetComponent<RectTransform>();
            fRT.anchorMin = Vector2.zero;
            fRT.anchorMax = Vector2.one;
            fRT.offsetMin = Vector2.zero;
            fRT.offsetMax = Vector2.zero;
            fillGo.GetComponent<Image>().color = new Color(0.24f, 0.80f, 0.36f, 1f);

            _progressSlider = sliderGo.GetComponent<Slider>();
            _progressSlider.interactable = false;
            _progressSlider.transition = Selectable.Transition.None;
            _progressSlider.fillRect = fRT;
            _progressSlider.minValue = 0f;
            _progressSlider.maxValue = 1f;
            _progressSlider.value = 0f;

            // 百分比文本
            GameObject pctGo = new GameObject("PercentText", typeof(RectTransform), typeof(Text));
            pctGo.transform.SetParent(sliderGo.transform, false);
            RectTransform pctRT = pctGo.GetComponent<RectTransform>();
            pctRT.anchorMin = Vector2.zero;
            pctRT.anchorMax = Vector2.one;
            pctRT.offsetMin = Vector2.zero;
            pctRT.offsetMax = Vector2.zero;
            _percentText = pctGo.GetComponent<Text>();
            _percentText.font = font;
            _percentText.fontSize = 22;
            _percentText.fontStyle = FontStyle.Bold;
            _percentText.alignment = TextAnchor.MiddleCenter;
            _percentText.color = Color.white;
            _percentText.text = "0%";

            canvasGo.SetActive(false);
        }
    }
}
