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

        [Header("背景音乐资源")]
        [SerializeField] private AudioClip bgmClip;

        [Header("音量与节流配置")]
        [Range(0f, 1f)]
        [SerializeField] private float masterVolume = 0.80f;
        [Range(0f, 1f)]
        [SerializeField] private float bgmVolume = 0.75f;
        [SerializeField] private float sliderTickInterval = 0.06f;

        private AudioSource _audioSource;
        private AudioSource _bgmSource;
        private float _lastSliderTickTime = -999f;
        private static AudioClip _cachedEvoReadyClip;
        private static AudioClip _cachedEvoSequenceClip;

        public float SfxVolume => masterVolume;
        public float BgmVolume => bgmVolume;

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
            if (Application.isPlaying)
            {
                ApplySavedAudioVolumes();
            }
        }

        private void OnEnable()
        {
            if (Instance == null) Instance = this;
            LoadDefaultClips();
            EnsureAudioSource();
            if (Application.isPlaying)
            {
                ApplySavedAudioVolumes();
            }
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

        private void EnsureBgmSource()
        {
            if (_bgmSource == null)
            {
                Transform bgmChild = transform.Find("BGM_AudioSource");
                if (bgmChild != null)
                {
                    _bgmSource = bgmChild.GetComponent<AudioSource>();
                }
                if (_bgmSource == null)
                {
                    GameObject go = new GameObject("BGM_AudioSource", typeof(AudioSource));
                    go.transform.SetParent(transform, false);
                    _bgmSource = go.GetComponent<AudioSource>();
                }
            }

            _bgmSource.playOnAwake = false;
            _bgmSource.loop = true;
            _bgmSource.spatialBlend = 0f;
            _bgmSource.ignoreListenerPause = true;
            _bgmSource.volume = bgmVolume;

            if (bgmClip != null && _bgmSource.clip != bgmClip)
            {
                _bgmSource.clip = bgmClip;
                if (Application.isPlaying && !_bgmSource.isPlaying)
                {
                    _bgmSource.Play();
                }
            }
        }

        public static void ApplySavedAudioVolumes()
        {
            int savedSfx = PlayerPrefs.GetInt("SETTING_AUDIO_SFX_VOLUME", 80);
            int savedBgm = PlayerPrefs.GetInt("SETTING_AUDIO_BGM_VOLUME", 75);
            SetSfxVolume(savedSfx / 100f);
            SetBgmVolume(savedBgm / 100f);
        }

        public static void SetSfxVolume(float volume01)
        {
            var mgr = EnsureInstance();
            if (mgr == null) return;
            mgr.masterVolume = Mathf.Clamp01(volume01);
        }

        public static void SetBgmVolume(float volume01)
        {
            var mgr = EnsureInstance();
            if (mgr == null) return;
            mgr.bgmVolume = Mathf.Clamp01(volume01);
            if (Application.isPlaying)
            {
                mgr.EnsureBgmSource();
                if (mgr._bgmSource != null)
                {
                    mgr._bgmSource.volume = mgr.bgmVolume;
                }
            }
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

        public static void PlayRoar()
        {
            var mgr = EnsureInstance();
            mgr?.PlayClip(null, 0.95f, 180f, 75f, 0.55f);
        }

        /// <summary>
        /// 程序合成：可升级提示叮咚音（经验刚攒满时播放）
        /// </summary>
        public static void PlayEvolutionReadyChime()
        {
            var mgr = EnsureInstance();
            if (mgr == null || !Application.isPlaying) return;
            if (_cachedEvoReadyClip == null)
            {
                _cachedEvoReadyClip = CreateEvolutionReadyChimeClip();
            }
            mgr.PlayClip(_cachedEvoReadyClip, 1.0f);
        }

        /// <summary>
        /// 程序合成：3秒进化演出音效（能量蓄力上升 + 白光爆发完成和弦）
        /// </summary>
        public static void PlayEvolutionSequenceSfx()
        {
            var mgr = EnsureInstance();
            if (mgr == null || !Application.isPlaying) return;
            if (_cachedEvoSequenceClip == null)
            {
                _cachedEvoSequenceClip = CreateEvolutionSequenceClip();
            }
            mgr.PlayClip(_cachedEvoSequenceClip, 1.0f);
        }

        private static AudioClip CreateEvolutionReadyChimeClip()
        {
            const int sr = 44100;
            const float dur = 0.52f;
            int n = Mathf.RoundToInt(sr * dur);
            float[] data = new float[n];

            // 两段清脆上扬叮咚音：E6 (1318.5Hz) -> B6 (1975.5Hz) + 柔和八度泛音
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float sample = 0f;

                // Note 1: 0.00s ~ 0.24s
                if (t < 0.26f)
                {
                    float env1 = Mathf.Clamp01(t / 0.008f) * Mathf.Exp(-t * 11f);
                    sample += env1 * (0.55f * Mathf.Sin(2f * Mathf.PI * 1318.5f * t)
                                    + 0.22f * Mathf.Sin(2f * Mathf.PI * 2637.0f * t));
                }

                // Note 2: 0.13s ~ 0.52s
                if (t >= 0.13f)
                {
                    float t2 = t - 0.13f;
                    float env2 = Mathf.Clamp01(t2 / 0.008f) * Mathf.Exp(-t2 * 7.5f);
                    sample += env2 * (0.65f * Mathf.Sin(2f * Mathf.PI * 1975.5f * t2)
                                    + 0.25f * Mathf.Sin(2f * Mathf.PI * 3951.0f * t2)
                                    + 0.14f * Mathf.Sin(2f * Mathf.PI * 987.8f * t2));
                }

                data[i] = Mathf.Clamp(sample * 0.65f, -0.95f, 0.95f);
            }

            AudioClip clip = AudioClip.Create("SFX_EvolutionReadyChime", n, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateEvolutionSequenceClip()
        {
            const int sr = 44100;
            const float dur = 3.0f;
            int n = Mathf.RoundToInt(sr * dur);
            float[] data = new float[n];

            float phaseBase = 0f;
            float phaseFifth = 0f;
            float phaseHigh = 0f;
            System.Random rng = new System.Random(2026);

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float sample = 0f;

                // 阶段 A (0.0s ~ 1.45s): 能量蓄力拉升 + 脉冲震颤
                if (t < 1.55f)
                {
                    float p = Mathf.Clamp01(t / 1.45f);
                    float curve = p * p;
                    float freq = Mathf.Lerp(150f, 740f, curve);
                    phaseBase += 2f * Mathf.PI * freq / sr;
                    phaseFifth += 2f * Mathf.PI * (freq * 1.5f) / sr;
                    phaseHigh += 2f * Mathf.PI * (freq * 2.0f) / sr;

                    float tremolo = 0.72f + 0.28f * Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(4f, 18f, p) * t);
                    float env = Mathf.Clamp01(t / 0.12f) * (1f - Mathf.Clamp01((t - 1.38f) / 0.17f));
                    float chargeWave = 0.45f * Mathf.Sin(phaseBase)
                                     + 0.26f * Mathf.Sin(phaseFifth)
                                     + 0.16f * Mathf.Sin(phaseHigh);
                    sample += chargeWave * tremolo * env * 0.55f;
                }

                // 阶段 B (1.38s ~ 3.00s): 爆发低频冲击 + 辉煌大三和弦升华余韵
                if (t >= 1.38f)
                {
                    float tb = t - 1.38f;
                    // 爆发瞬间温暖低音冲击 + 轻噪声光爆
                    float boomEnv = Mathf.Clamp01(tb / 0.015f) * Mathf.Exp(-tb * 4.2f);
                    float boomFreq = Mathf.Lerp(140f, 52f, Mathf.Clamp01(tb / 0.45f));
                    float noise = ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-tb * 9.0f) * 0.22f;
                    sample += boomEnv * (0.48f * Mathf.Sin(2f * Mathf.PI * boomFreq * tb) + noise);

                    // 进化成功明亮大三和弦 (C5 523.25 + E5 659.25 + G5 783.99 + C6 1046.5 + G6 1567.98)
                    float chordEnv = Mathf.Clamp01(tb / 0.03f) * Mathf.Exp(-tb * 1.95f);
                    float shimmer = 0.88f + 0.12f * Mathf.Sin(2f * Mathf.PI * 7.5f * tb);
                    float chord = 0.30f * Mathf.Sin(2f * Mathf.PI * 523.25f * tb)
                                + 0.24f * Mathf.Sin(2f * Mathf.PI * 659.25f * tb)
                                + 0.24f * Mathf.Sin(2f * Mathf.PI * 783.99f * tb)
                                + 0.22f * Mathf.Sin(2f * Mathf.PI * 1046.50f * tb)
                                + 0.12f * Mathf.Sin(2f * Mathf.PI * 1567.98f * tb);
                    sample += chord * chordEnv * shimmer * 0.68f;
                }

                data[i] = Mathf.Clamp(sample, -0.95f, 0.95f);
            }

            AudioClip clip = AudioClip.Create("SFX_EvolutionSequence", n, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
