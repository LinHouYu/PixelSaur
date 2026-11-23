using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PixelSaur.Cam;

namespace PixelSaur.Player
{
    [Serializable]
    public class EvolutionStageConfig
    {
        [Tooltip("阶段显示名称")]
        public string stageName = "幼年期";

        [Tooltip("HUD 阶段名称与进度条颜色")]
        public Color stageColor = Color.white;

        [Tooltip("该阶段恐龙目标体型倍率（相对初始幼年期 1.0）")]
        public float targetScale = 1.0f;

        [Tooltip("升至下一阶段所需成长值（满级阶段填 0）")]
        public float requiredExpToNext = 50f;
    }

    /// <summary>
    /// 恐龙进化系统控制器（测试版，不存档，每次进入 Game 场景重置为幼年期 Scale=1.0 / 经验 0）：
    /// 1. 在 Inspector 中配置四个进化阶段（幼年期/亚成年期/成年期/完全体）；
    /// 2. 吃掉 Creature_GreenPrey 后增加成长值，达到阈值自动原地进化并保留溢出经验；
    /// 3. 进化时通过约 0.5 秒平滑插值放大模型并同步抬升脚底防止穿入地面，同时可选微调摄像机视距防遮挡；
    /// 4. 仅放大模型，不改变血量与攻击力等其他数值。
    /// </summary>
    public class DinoEvolutionController : MonoBehaviour
    {
        public static DinoEvolutionController Instance { get; private set; }

        [Header("进化阶段配置（可在 Inspector 随时微调）")]
        [SerializeField] private List<EvolutionStageConfig> stages = new List<EvolutionStageConfig>
        {
            new EvolutionStageConfig { stageName = "幼年期", stageColor = Color.white, targetScale = 1.0f, requiredExpToNext = 50f },
            new EvolutionStageConfig { stageName = "亚成年期", stageColor = new Color(0.32f, 0.94f, 0.38f, 1f), targetScale = 1.5f, requiredExpToNext = 100f },
            new EvolutionStageConfig { stageName = "成年期", stageColor = new Color(0.28f, 0.68f, 1.00f, 1f), targetScale = 2.0f, requiredExpToNext = 150f },
            new EvolutionStageConfig { stageName = "完全体", stageColor = new Color(0.76f, 0.42f, 1.00f, 1f), targetScale = 2.5f, requiredExpToNext = 0f }
        };

        [Header("成长与进化参数")]
        [Tooltip("每次进食 Creature_GreenPrey 增加的成长值")]
        [SerializeField] private float growthExpPerFeed = 25f;

        [Tooltip("触发进化时体型平滑放大插值时长（秒）")]
        [Range(0.1f, 2.0f)]
        [SerializeField] private float evolveScaleDuration = 0.5f;

        [Tooltip("升级时是否保留溢出经验（关闭则清零）")]
        [SerializeField] private bool keepOverflowExp = true;

        [Header("可选摄像机视距微调（防止 2.5 倍体型挡住视野）")]
        [SerializeField] private bool adjustCameraOnEvolve = true;

        [Tooltip("每增加 1.0 倍体型额外拉远的摄像机距离（米）")]
        [Range(0f, 6f)]
        [SerializeField] private float cameraDistanceBonusPerScale = 2.6f;

        [Tooltip("每增加 1.0 倍体型额外抬高的摄像机注视点高度（米）")]
        [Range(0f, 2f)]
        [SerializeField] private float cameraTargetYBonusPerScale = 0.55f;

        [Header("运行时状态（只读观察）")]
        [SerializeField] private int currentStageIndex = 0;
        [SerializeField] private float currentExp = 0f;
        [SerializeField] private float currentScaleMultiplier = 1.0f;

        public event Action OnEvolutionStateChanged;

        private PlayerController _playerController;
        private Coroutine _scaleCoroutine;

        public int CurrentStageIndex => currentStageIndex;
        public float CurrentExp => currentExp;
        public float CurrentScaleMultiplier => currentScaleMultiplier;
        public float GrowthExpPerFeed => growthExpPerFeed;
        public bool IsMaxStage => stages == null || stages.Count == 0 || currentStageIndex >= stages.Count - 1;
        public EvolutionStageConfig CurrentStage =>
            (stages != null && stages.Count > 0)
                ? stages[Mathf.Clamp(currentStageIndex, 0, stages.Count - 1)]
                : null;

        public float CurrentStageRequiredExp => CurrentStage != null ? CurrentStage.requiredExpToNext : 0f;

        private void Awake()
        {
            Instance = this;
            _playerController = GetComponent<PlayerController>();
            EnsureDefaultStagesIfEmpty();
            ResetToJuvenileImmediate();
        }

        private void Start()
        {
            // 每次进入 Game 场景确保从幼年期 (Scale 1.0, Exp 0) 开始，不读档
            ResetToJuvenileImmediate();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void EnsureDefaultStagesIfEmpty()
        {
            if (stages != null && stages.Count > 0) return;
            stages = new List<EvolutionStageConfig>
            {
                new EvolutionStageConfig { stageName = "幼年期", stageColor = Color.white, targetScale = 1.0f, requiredExpToNext = 50f },
                new EvolutionStageConfig { stageName = "亚成年期", stageColor = new Color(0.32f, 0.94f, 0.38f, 1f), targetScale = 1.5f, requiredExpToNext = 100f },
                new EvolutionStageConfig { stageName = "成年期", stageColor = new Color(0.28f, 0.68f, 1.00f, 1f), targetScale = 2.0f, requiredExpToNext = 150f },
                new EvolutionStageConfig { stageName = "完全体", stageColor = new Color(0.76f, 0.42f, 1.00f, 1f), targetScale = 2.5f, requiredExpToNext = 0f }
            };
        }

        /// <summary>
        /// 重置为幼年期（Scale 1.0，经验 0）
        /// </summary>
        public void ResetToJuvenileImmediate()
        {
            EnsureDefaultStagesIfEmpty();
            if (_scaleCoroutine != null)
            {
                StopCoroutine(_scaleCoroutine);
                _scaleCoroutine = null;
            }

            currentStageIndex = 0;
            currentExp = 0f;
            currentScaleMultiplier = stages[0].targetScale;

            ApplyScaleToPlayerAndCamera(currentScaleMultiplier);
            OnEvolutionStateChanged?.Invoke();
        }

        /// <summary>
        /// 由 Creature_GreenPrey 进食成功时调用，按默认 growthExpPerFeed 增加成长值
        /// </summary>
        public void OnPreyFed(float customExpOverride = -1f)
        {
            float gain = customExpOverride > 0f ? customExpOverride : growthExpPerFeed;
            AddGrowthExp(gain);
        }

        /// <summary>
        /// 增加成长值并在达到当前阶段阈值时自动原地进化
        /// </summary>
        public void AddGrowthExp(float amount)
        {
            if (amount <= 0f || stages == null || stages.Count == 0) return;

            if (IsMaxStage)
            {
                OnEvolutionStateChanged?.Invoke();
                return;
            }

            currentExp += amount;
            int prevStage = currentStageIndex;

            while (!IsMaxStage)
            {
                float threshold = stages[currentStageIndex].requiredExpToNext;
                if (threshold <= 0.001f || currentExp < threshold)
                {
                    break;
                }

                currentExp = keepOverflowExp ? (currentExp - threshold) : 0f;
                currentStageIndex++;
            }

            if (IsMaxStage)
            {
                currentExp = 0f;
            }

            if (currentStageIndex != prevStage)
            {
                EvolutionStageConfig newStage = CurrentStage;
                Debug.Log($"<color=#CC88FF>[DinoEvolution] 恐龙进化！进入【{newStage.stageName}】，目标体型 Scale={newStage.targetScale:F1}x！</color>");

                if (_scaleCoroutine != null) StopCoroutine(_scaleCoroutine);
                if (gameObject.activeInHierarchy)
                {
                    _scaleCoroutine = StartCoroutine(SmoothEvolveScaleRoutine(newStage.targetScale));
                }
                else
                {
                    currentScaleMultiplier = newStage.targetScale;
                    ApplyScaleToPlayerAndCamera(currentScaleMultiplier);
                }
            }

            OnEvolutionStateChanged?.Invoke();
        }

        private IEnumerator SmoothEvolveScaleRoutine(float targetScale)
        {
            float startScale = currentScaleMultiplier;
            float duration = Mathf.Max(0.05f, evolveScaleDuration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                currentScaleMultiplier = Mathf.Lerp(startScale, targetScale, t);
                ApplyScaleToPlayerAndCamera(currentScaleMultiplier);
                yield return null;
            }

            currentScaleMultiplier = targetScale;
            ApplyScaleToPlayerAndCamera(currentScaleMultiplier);
            _scaleCoroutine = null;
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
