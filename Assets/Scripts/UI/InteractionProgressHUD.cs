using UnityEngine;
using UnityEngine.UI;
using PixelSaur.Combat;

namespace PixelSaur.UI
{
    /// <summary>
    /// 顶部居中进食/饮水进度条 HUD：
    /// 长按进食或饮水时在屏幕顶部居中显示进度 Slider，填充颜色与底部状态条一致，并在居中显示剩余秒数（保留 1 位小数）。
    /// </summary>
    public class InteractionProgressHUD : MonoBehaviour
    {
        private static readonly Color FoodColor = new Color(0.90f, 0.48f, 0.12f, 1f);
        private static readonly Color ThirstColor = new Color(0.18f, 0.65f, 0.95f, 1f);
        private static readonly Color BgColor = new Color(0.08f, 0.10f, 0.14f, 0.88f);

        private GameObject _container;
        private Slider _slider;
        private Image _fillImage;
        private Text _timerText;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttachToHudCanvas()
        {
            GameObject canvasObj = GameObject.Find("HUD_Canvas");
            if (canvasObj == null) return;
            if (canvasObj.GetComponent<InteractionProgressHUD>() == null)
            {
                canvasObj.AddComponent<InteractionProgressHUD>();
            }
        }

        private void Awake()
        {
            BuildUI();
            if (_container != null)
            {
                _container.SetActive(false);
            }
        }

        private void Update()
        {
            DinoInteractionController interaction = DinoInteractionController.Instance;
            if (interaction == null || !interaction.IsInteracting || interaction.CurrentActionDuration <= 0.001f)
            {
                if (_container != null && _container.activeSelf)
                {
                    _container.SetActive(false);
                }
                return;
            }

            if (_container != null && !_container.activeSelf)
            {
                _container.SetActive(true);
            }

            float duration = interaction.CurrentActionDuration;
            float elapsed = Mathf.Clamp(interaction.CurrentActionElapsed, 0f, duration);
            float remaining = Mathf.Max(0f, interaction.CurrentActionRemaining);

            if (_slider != null)
            {
                _slider.value = elapsed / duration;
            }

            if (_fillImage != null)
            {
                _fillImage.color = interaction.CurrentInteraction == InteractionType.EatingPrey
                    ? FoodColor
                    : ThirstColor;
            }

            if (_timerText != null)
            {
                _timerText.text = $"{remaining:F1}s";
            }
        }

        private void BuildUI()
        {
            Transform existing = transform.Find("InteractionProgressBar");
            if (existing != null)
            {
                _container = existing.gameObject;
                _slider = _container.GetComponent<Slider>();
                _fillImage = _container.transform.Find("Fill Area/Fill")?.GetComponent<Image>();
                _timerText = _container.transform.Find("TimerText")?.GetComponent<Text>();
                return;
            }

            _container = new GameObject("InteractionProgressBar", typeof(RectTransform), typeof(Image), typeof(Slider));
            _container.transform.SetParent(transform, false);

            RectTransform rt = _container.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -24f);
            rt.sizeDelta = new Vector2(360f, 36f);

            Image bgImage = _container.GetComponent<Image>();
            bgImage.color = BgColor;
            bgImage.raycastTarget = false;

            Outline bgOutline = _container.AddComponent<Outline>();
            bgOutline.effectColor = new Color(0f, 0f, 0f, 0.90f);
            bgOutline.effectDistance = new Vector2(2f, -2f);

            // Fill Area
            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(_container.transform, false);
            RectTransform fillAreaRT = fillArea.GetComponent<RectTransform>();
            fillAreaRT.anchorMin = Vector2.zero;
            fillAreaRT.anchorMax = Vector2.one;
            fillAreaRT.offsetMin = new Vector2(3f, 3f);
            fillAreaRT.offsetMax = new Vector2(-3f, -3f);

            // Fill
            GameObject fillObj = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillObj.transform.SetParent(fillArea.transform, false);
            RectTransform fillRT = fillObj.GetComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;

            _fillImage = fillObj.GetComponent<Image>();
            _fillImage.color = FoodColor;
            _fillImage.raycastTarget = false;

            _slider = _container.GetComponent<Slider>();
            _slider.interactable = false;
            _slider.transition = Selectable.Transition.None;
            _slider.fillRect = fillRT;
            _slider.minValue = 0f;
            _slider.maxValue = 1f;
            _slider.value = 0f;

            // Centered Timer Text
            GameObject textObj = new GameObject("TimerText", typeof(RectTransform), typeof(Text));
            textObj.transform.SetParent(_container.transform, false);
            RectTransform textRT = textObj.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;

            _timerText = textObj.GetComponent<Text>();
            _timerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _timerText.fontSize = 22;
            _timerText.fontStyle = FontStyle.Bold;
            _timerText.alignment = TextAnchor.MiddleCenter;
            _timerText.color = Color.white;
            _timerText.raycastTarget = false;
            _timerText.text = "0.0s";

            Outline textOutline = textObj.AddComponent<Outline>();
            textOutline.effectColor = new Color(0f, 0f, 0f, 0.95f);
            textOutline.effectDistance = new Vector2(1.5f, -1.5f);
        }
    }
}
