using UnityEngine;

namespace PixelSaur.UI
{
    /// <summary>
    /// 全局 UI 音效管理器：为设置入口齿轮、主设置面板及二级设置页面的所有按钮、开关、滑块、弹窗提供统一反馈音效。
    /// 支持编辑器自动关联 Assets/Audio/UI/*.wav，同时内置运行时程序化兜底合成音防止缺音。
    /// </summary>
    [ExecuteAlways]
    public class UISoundManager : MonoBehaviour
    {
        public static UISoundManager Instance { get; private set; }

        [Header("UI 音效资源")]
        [SerializeField] private AudioClip openClip;
        [SerializeField] private AudioClip closeClip;
        [SerializeField] private AudioClip clickClip;
        [SerializeField] private AudioClip toggleOnClip;
        [SerializeField] private AudioClip toggleOffClip;
        [SerializeField] private AudioClip saveClip;
        [SerializeField] private AudioClip alertClip;

        [Header("动作按钮音效资源")]
        [SerializeField] private AudioClip jumpClip;
        [SerializeField] private AudioClip sneakOnClip;
        [SerializeField] private AudioClip sneakOffClip;

        [Header("音量与节流配置")]
        [Range(0f, 1f)]
        [SerializeField] private float masterVolume = 0.75f;
        [SerializeField] private float sliderTickInterval = 0.06f;

        private AudioSource _audioSource;
        private float _lastSliderTickTime = -999f;

        private void Awake()
        {
            if (Application.isPlaying)
            {
                if (Instance == null) Instance = this;
                else if (Instance != this) { Destroy(this); return; }
            }
            else if (Instance == null)
            {
                Instance = this;
            }

            LoadDefaultClips();
            EnsureAudioSource();
        }

        private void OnEnable()
        {
            if (Instance == null) Instance = this;
            LoadDefaultClips();
            EnsureAudioSource();
        }

        public void LoadDefaultClips()
        {
#if UNITY_EDITOR
            if (openClip == null) openClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/UI/ui_settings_open.wav");
            if (closeClip == null) closeClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/UI/ui_settings_close.wav");
            if (clickClip == null) clickClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/UI/ui_click.wav");
            if (toggleOnClip == null) toggleOnClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/UI/ui_toggle_on.wav");
            if (toggleOffClip == null) toggleOffClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/UI/ui_toggle_off.wav");
            if (saveClip == null) saveClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/UI/ui_save_success.wav");
            if (alertClip == null) alertClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/UI/ui_popup_alert.wav");
            if (jumpClip == null) jumpClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/UI/ui_jump.wav");
            if (sneakOnClip == null) sneakOnClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/UI/ui_sneak_on.wav");
            if (sneakOffClip == null) sneakOffClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/UI/ui_sneak_off.wav");
#endif
        }

        private void EnsureAudioSource()
        {
            if (_audioSource == null)
            {
                _audioSource = GetComponent<AudioSource>();
                if (_audioSource == null)
                {
                    _audioSource = gameObject.AddComponent<AudioSource>();
                }
            }
            _audioSource.playOnAwake = false;
            _audioSource.loop = false;
            _audioSource.spatialBlend = 0f;
            _audioSource.ignoreListenerPause = true;
        }

        private static UISoundManager EnsureInstance()
        {
            if (Instance != null) return Instance;
            Instance = FindAnyObjectByType<UISoundManager>(FindObjectsInactive.Include);
            if (Instance != null) return Instance;

            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas != null)
            {
                Instance = canvas.gameObject.AddComponent<UISoundManager>();
            }
            else
            {
                GameObject go = new GameObject("UISoundManager");
                Instance = go.AddComponent<UISoundManager>();
            }
            return Instance;
        }

        private void PlayClip(AudioClip clip, float volumeMultiplier = 1f, float fallbackFreq0 = 800f, float fallbackFreq1 = 500f, float fallbackDur = 0.06f)
        {
            if (!Application.isPlaying) return;
            EnsureAudioSource();
            if (_audioSource == null) return;

            if (clip == null)
            {
                clip = CreateFallbackChirp(fallbackFreq0, fallbackFreq1, fallbackDur);
            }
            if (clip != null)
            {
                _audioSource.PlayOneShot(clip, Mathf.Clamp01(masterVolume * volumeMultiplier));
            }
        }

        private static AudioClip CreateFallbackChirp(float f0, float f1, float dur)
        {
            const int sr = 44100;
            int n = Mathf.Max(1, Mathf.RoundToInt(sr * dur));
            float[] data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float p = t / dur;
                float freq = Mathf.Lerp(f0, f1, p);
                phase += 2f * Mathf.PI * freq / sr;
                float env = Mathf.Clamp01(t / 0.005f) * Mathf.Clamp01((dur - t) / (dur * 0.6f));
                data[i] = 0.45f * env * Mathf.Sin(phase);
            }
            AudioClip clip = AudioClip.Create("UI_Fallback_Chirp", n, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static void PlayOpen()
        {
            var mgr = EnsureInstance();
            mgr?.PlayClip(mgr.openClip, 0.9f, 780f, 1175f, 0.12f);
        }

        public static void PlayClose()
        {
            var mgr = EnsureInstance();
            mgr?.PlayClip(mgr.closeClip, 0.85f, 980f, 580f, 0.10f);
        }

        public static void PlayClick()
        {
            var mgr = EnsureInstance();
            mgr?.PlayClip(mgr.clickClip, 0.85f, 900f, 380f, 0.055f);
        }

        public static void PlayToggle(bool isOn)
        {
            var mgr = EnsureInstance();
            if (mgr == null) return;
            if (isOn) mgr.PlayClip(mgr.toggleOnClip, 0.85f, 520f, 940f, 0.07f);
            else mgr.PlayClip(mgr.toggleOffClip, 0.85f, 760f, 410f, 0.07f);
        }

        public static void PlaySave()
        {
            var mgr = EnsureInstance();
            mgr?.PlayClip(mgr.saveClip, 0.95f, 523f, 1046f, 0.18f);
        }

        public static void PlayAlert()
        {
            var mgr = EnsureInstance();
            mgr?.PlayClip(mgr.alertClip, 0.9f, 660f, 520f, 0.14f);
        }

        public static void PlaySliderTick()
        {
            var mgr = EnsureInstance();
            if (mgr == null || !Application.isPlaying) return;
            float now = Time.unscaledTime;
            if (now - mgr._lastSliderTickTime < mgr.sliderTickInterval) return;
            mgr._lastSliderTickTime = now;
            mgr.PlayClip(mgr.clickClip, 0.45f, 980f, 640f, 0.03f);
        }

        public static void PlayJump()
        {
            var mgr = EnsureInstance();
            mgr?.PlayClip(mgr.jumpClip, 0.90f, 320f, 920f, 0.13f);
        }

        public static void PlaySneak(bool isSneaking)
        {
            var mgr = EnsureInstance();
            if (mgr == null) return;
            if (isSneaking) mgr.PlayClip(mgr.sneakOnClip, 0.90f, 280f, 130f, 0.11f);
            else mgr.PlayClip(mgr.sneakOffClip, 0.85f, 165f, 330f, 0.10f);
        }
    }
}
