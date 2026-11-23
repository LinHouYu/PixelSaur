using System;
using System.Collections;
using UnityEngine;
using PixelSaur.Cam;
using PixelSaur.UI;

namespace PixelSaur.Player
{
    [Serializable]
    public struct EvolutionStageConfig
    {
        [Tooltip("阶段显示名称")]
        public string stageName;

        [Tooltip("HUD 阶段名称与进化特效主题颜色")]
        public Color stageColor;

        [Tooltip("该阶段恐龙目标体型倍率（相对初始幼年期 1.0）")]
        public float targetScale;

        [Tooltip("升至下一阶段所需经验（满级阶段填 0）")]
        public float requiredExpToNext;
    }

    /// <summary>
    /// 恐龙进化系统总控制器（手动触发 + 3秒原地高燃特效 + 程序合成音效，不存档，每次重开从幼年期开始）：
    /// 1. Inspector 使用结构体数组暴露 4 个阶段（幼年期 1.0/50、亚成年期 1.5/100、成年期 2.2/150、完全体 3.5/MAX）；
    /// 2. 吃掉 Creature_GreenPrey 增加成长经验（默认 +25），经验攒满后不自动进化，而是触发可升级提示与叮咚音效；
    /// 3. 玩家在进化弹窗手动点击【进化】后，立即关闭弹窗并在当前场景原地播放约 3 秒高燃进化演出：
    ///    模型纯白发光 + 目标阶段主题色冲天光柱 + 旋转神秘符号环 + 底部向上喷射粒子 + 白光峰值平滑放大体型（防卡地）+ 摄像机微调拉远。
    /// </summary>
    public class DinoEvolutionController : MonoBehaviour
    {
        public static DinoEvolutionController Instance { get; private set; }

        [Header("四阶段进化配置（结构体数组，Inspector 随时可调）")]
        [SerializeField] private EvolutionStageConfig[] stages =
        {
            new EvolutionStageConfig { stageName = "幼年期", stageColor = Color.white, targetScale = 1.0f, requiredExpToNext = 50f },
            new EvolutionStageConfig { stageName = "亚成年期", stageColor = new Color(0.32f, 0.94f, 0.38f, 1f), targetScale = 1.5f, requiredExpToNext = 100f },
            new EvolutionStageConfig { stageName = "成年期", stageColor = new Color(0.28f, 0.68f, 1.00f, 1f), targetScale = 2.2f, requiredExpToNext = 150f },
            new EvolutionStageConfig { stageName = "完全体", stageColor = new Color(0.76f, 0.42f, 1.00f, 1f), targetScale = 3.5f, requiredExpToNext = 0f }
        };

        [Header("成长与手动进化演出参数")]
        [Tooltip("每次进食 Creature_GreenPrey 增加的进化经验")]
        [SerializeField] private float growthExpPerFeed = 25f;

        [Tooltip("满经验后继续进食是否保留溢出经验（关闭则封顶当前阶段所需经验）")]
        [SerializeField] private bool keepOverflowExp = true;

        [Tooltip("原地进化高燃演出总时长（秒，默认 3.0 秒）")]
        [Range(1.5f, 6.0f)]
        [SerializeField] private float evolutionSequenceDuration = 3.0f;

        [Header("可选摄像机视距微调（防止 3.5 倍完全体挡住视野）")]
        [SerializeField] private bool adjustCameraOnEvolve = true;

        [Tooltip("每增加 1.0 倍体型额外拉远的摄像机距离（米）")]
        [Range(0f, 6f)]
        [SerializeField] private float cameraDistanceBonusPerScale = 2.4f;

        [Tooltip("每增加 1.0 倍体型额外抬高的摄像机注视点高度（米）")]
        [Range(0f, 2f)]
        [SerializeField] private float cameraTargetYBonusPerScale = 0.60f;

        [Header("运行时状态（只读观察）")]
        [SerializeField] private int currentStageIndex = 0;
        [SerializeField] private float currentExp = 0f;
        [SerializeField] private float currentScaleMultiplier = 1.0f;
        [SerializeField] private bool isEvolving = false;

        public event Action OnEvolutionStateChanged;
        public event Action OnEvolutionReadyTriggered;

        private PlayerController _playerController;
        private DinoEvolutionVfx _vfx;
        private Coroutine _evolveCoroutine;
        private bool _hasNotifiedReadyForCurrentStage = false;

        public int CurrentStageIndex => currentStageIndex;
        public float CurrentExp => currentExp;
        public float CurrentScaleMultiplier => currentScaleMultiplier;
        public float GrowthExpPerFeed => growthExpPerFeed;
        public bool IsEvolving => isEvolving;
        public bool IsMaxStage => stages == null || stages.Length == 0 || currentStageIndex >= stages.Length - 1;

        public EvolutionStageConfig CurrentStage =>
            (stages != null && stages.Length > 0)
                ? stages[Mathf.Clamp(currentStageIndex, 0, stages.Length - 1)]
                : default;

        public bool HasNextStage => stages != null && currentStageIndex + 1 < stages.Length;
        public EvolutionStageConfig NextStage =>
            HasNextStage ? stages[currentStageIndex + 1] : CurrentStage;

        public float CurrentStageRequiredExp => CurrentStage.requiredExpToNext;

        public bool CanEvolve =>
            !isEvolving &&
            !IsMaxStage &&
            CurrentStageRequiredExp > 0.001f &&
            currentExp >= CurrentStageRequiredExp;

        private void Awake()
        {
            Instance = this;
            _playerController = GetComponent<PlayerController>();
            _vfx = GetComponent<DinoEvolutionVfx>();
            if (_vfx == null) _vfx = gameObject.AddComponent<DinoEvolutionVfx>();

            EnsureDefaultStages();
            ResetToJuvenileImmediate();
        }

        private void Start()
        {
            // 每次重启游戏 / 重新进入 Game 场景均从幼年期（Scale 1.0，经验 0）开始
            ResetToJuvenileImmediate();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void ResetStagesToDefaultConfig()
        {
            stages = new[]
            {
                new EvolutionStageConfig { stageName = "幼年期", stageColor = Color.white, targetScale = 1.0f, requiredExpToNext = 50f },
                new EvolutionStageConfig { stageName = "亚成年期", stageColor = new Color(0.32f, 0.94f, 0.38f, 1f), targetScale = 1.5f, requiredExpToNext = 100f },
                new EvolutionStageConfig { stageName = "成年期", stageColor = new Color(0.28f, 0.68f, 1.00f, 1f), targetScale = 2.2f, requiredExpToNext = 150f },
                new EvolutionStageConfig { stageName = "完全体", stageColor = new Color(0.76f, 0.42f, 1.00f, 1f), targetScale = 3.5f, requiredExpToNext = 0f }
            };
        }

        private void EnsureDefaultStages()
        {
            if (stages == null || stages.Length < 4)
            {
                ResetStagesToDefaultConfig();
            }
        }

        /// <summary>
        /// 重置为幼年期（Scale 1.0，经验 0）
        /// </summary>
        public void ResetToJuvenileImmediate()
        {
            EnsureDefaultStages();
            if (_evolveCoroutine != null)
            {
                StopCoroutine(_evolveCoroutine);
                _evolveCoroutine = null;
            }

            if (_vfx != null) _vfx.EndSpectacle();
            isEvolving = false;
            _hasNotifiedReadyForCurrentStage = false;
            currentStageIndex = 0;
            currentExp = 0f;
            currentScaleMultiplier = stages[0].targetScale;

            if (_playerController == null) _playerController = GetComponent<PlayerController>();
            if (_playerController != null) _playerController.MovementLocked = false;

            ApplyScaleToPlayerAndCamera(currentScaleMultiplier);
            OnEvolutionStateChanged?.Invoke();
        }

        /// <summary>
        /// 由 Creature_GreenPrey 进食成功时调用，按默认 growthExpPerFeed 增加进化经验
        /// </summary>
        public void OnPreyFed(float customExpOverride = -1f)
        {
            float gain = customExpOverride > 0f ? customExpOverride : growthExpPerFeed;
            AddGrowthExp(gain);
        }

        /// <summary>
        /// 增加成长经验（攒满后不自动进化，等待玩家手动点击进化，并在刚满时播放一次叮咚提示音）
        /// </summary>
        public void AddGrowthExp(float amount)
        {
            if (amount <= 0f || stages == null || stages.Length == 0) return;

            if (IsMaxStage)
            {
                OnEvolutionStateChanged?.Invoke();
                return;
            }

            bool wasReady = CanEvolve;
            float req = CurrentStageRequiredExp;

            currentExp += amount;
            if (!keepOverflowExp && req > 0.001f)
            {
                currentExp = Mathf.Min(currentExp, req);
            }

            bool nowReady = CanEvolve;
            if (!wasReady && nowReady && !_hasNotifiedReadyForCurrentStage)
            {
                _hasNotifiedReadyForCurrentStage = true;
                UISoundManager.PlayEvolutionReadyChime();
                OnEvolutionReadyTriggered?.Invoke();
                Debug.Log($"<color=#FFEE55>[DinoEvolution] 进化经验已满 ({currentExp:0}/{req:0})！请点击主界面【升级系统】手动进化至【{NextStage.stageName}】。</color>");
            }

            OnEvolutionStateChanged?.Invoke();
        }

        /// <summary>
        /// 玩家在进化弹窗手动点击【进化】后触发：立即原地执行约 3 秒的高燃进化演出
        /// </summary>
        public bool TryStartManualEvolution()
        {
            if (!CanEvolve) return false;

            float req = CurrentStageRequiredExp;
            currentExp = keepOverflowExp ? Mathf.Max(0f, currentExp - req) : 0f;
            currentStageIndex++;
            if (IsMaxStage)
            {
                currentExp = 0f;
            }

            _hasNotifiedReadyForCurrentStage = false;
            isEvolving = true;

            EvolutionStageConfig targetStage = CurrentStage;
            Debug.Log($"<color=#CC88FF>[DinoEvolution] 手动触发进化演出！目标阶段：【{targetStage.stageName}】，目标体型 Scale={targetStage.targetScale:0.0}x！</color>");

            UISoundManager.PlayEvolutionSequenceSfx();
            OnEvolutionStateChanged?.Invoke();

            if (_evolveCoroutine != null) StopCoroutine(_evolveCoroutine);
            _evolveCoroutine = StartCoroutine(EvolutionSpectacleRoutine(targetStage));
            return true;
        }

        private IEnumerator EvolutionSpectacleRoutine(EvolutionStageConfig targetStage)
        {
            if (_playerController == null) _playerController = GetComponent<PlayerController>();
            if (_vfx == null) _vfx = GetComponent<DinoEvolutionVfx>() ?? gameObject.AddComponent<DinoEvolutionVfx>();

            bool prevLocked = _playerController != null && _playerController.MovementLocked;
            if (_playerController != null) _playerController.MovementLocked = true;

            float startScale = currentScaleMultiplier;
            float targetScale = targetStage.targetScale;
            float totalDur = Mathf.Max(1.0f, evolutionSequenceDuration);
            float elapsed = 0f;
            bool burstShakeTriggered = false;

            _vfx.BeginSpectacle(transform, targetStage.stageColor, startScale);
            DinoAnimatorController.Instance?.TriggerRoar();

            while (elapsed < totalDur)
            {
                float dt = Time.deltaTime;
                elapsed += dt;
                float normT = Mathf.Clamp01(elapsed / totalDur);

                // 在白光最亮窗口 (normT: 0.42 ~ 0.72) 平滑插值放大恐龙模型与摄像机视距
                if (normT >= 0.42f)
                {
                    if (!burstShakeTriggered)
                    {
                        burstShakeTriggered = true;
                        ThirdPersonOrbitCamera.Instance?.TriggerShake(0.22f, 0.45f);
                    }

                    float scaleProgress = Mathf.Clamp01((normT - 0.42f) / 0.30f);
                    float smoothS = Mathf.SmoothStep(0f, 1f, scaleProgress);
                    currentScaleMultiplier = Mathf.Lerp(startScale, targetScale, smoothS);
                    ApplyScaleToPlayerAndCamera(currentScaleMultiplier);
                }

                _vfx.UpdateSpectacle(transform, normT, currentScaleMultiplier, dt);
                yield return null;
            }

            currentScaleMultiplier = targetScale;
            ApplyScaleToPlayerAndCamera(currentScaleMultiplier);
            _vfx.EndSpectacle();

            isEvolving = false;
            if (_playerController != null) _playerController.MovementLocked = prevLocked;
            _evolveCoroutine = null;

            // 若保留的溢出经验已经满足下一阶段阈值，给出一次新可升级叮咚提示
            if (CanEvolve && !_hasNotifiedReadyForCurrentStage)
            {
                _hasNotifiedReadyForCurrentStage = true;
                UISoundManager.PlayEvolutionReadyChime();
                OnEvolutionReadyTriggered?.Invoke();
            }

            OnEvolutionStateChanged?.Invoke();
        }

        private void ApplyScaleToPlayerAndCamera(float scaleMult)
        {
            if (_playerController == null) _playerController = GetComponent<PlayerController>();
            if (_playerController != null)
            {
                _playerController.SetEvolutionScaleMultiplier(scaleMult);
            }

            if (adjustCameraOnEvolve && ThirdPersonOrbitCamera.Instance != null)
            {
                float extraScale = Mathf.Max(0f, scaleMult - 1.0f);
                ThirdPersonOrbitCamera.Instance.SetEvolutionViewBonus(
                    extraScale * cameraDistanceBonusPerScale,
                    extraScale * cameraTargetYBonusPerScale);
            }
        }
    }
}
