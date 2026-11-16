using System;
using System.Collections.Generic;
using UnityEngine;

namespace PixelSaur.UI
{
    public enum QuestCategory
    {
        Daily,
        Main
    }

    public enum QuestRewardType
    {
        Gold,
        Amber
    }

    public enum QuestButtonState
    {
        InProgress,
        Claimable,
        Claimed
    }

    [Serializable]
    public class QuestItemData
    {
        public string id;
        public QuestCategory category;
        public string title;
        public string desc;
        public string triggerTag;
        public int targetCount;
        public int defaultProgress;
        public bool defaultClaimed;
        public QuestRewardType rewardType;
        public int rewardAmount;

        public int currentProgress;
        public bool isClaimed;

        public QuestButtonState GetState()
        {
            if (isClaimed) return QuestButtonState.Claimed;
            return currentProgress >= targetCount ? QuestButtonState.Claimable : QuestButtonState.InProgress;
        }
    }

    /// <summary>
    /// 任务与货币本地管理器（支持跨场景进度累计与 PlayerPrefs 持久化）：
    /// 管理每日/主线两类任务状态（进行中 / 可领取 / 已领取）及领奖后金币/琥珀增加。
    /// </summary>
    public static class QuestManager
    {
        private const string KeyGold = "PLAYER_CURRENCY_GOLD";
        private const string KeyAmber = "PLAYER_CURRENCY_AMBER";
        private const string KeyProgPrefix = "QUEST_PROG_";
        private const string KeyClaimPrefix = "QUEST_CLAIMED_";

        public static event Action OnDataChanged;

        private static bool _initialized;
        private static readonly List<QuestItemData> Quests = new List<QuestItemData>();

        public static int Gold
        {
            get => PlayerPrefs.GetInt(KeyGold, 1280);
            private set
            {
                PlayerPrefs.SetInt(KeyGold, Mathf.Max(0, value));
                PlayerPrefs.Save();
            }
        }

        public static int Amber
        {
            get => PlayerPrefs.GetInt(KeyAmber, 60);
            private set
            {
                PlayerPrefs.SetInt(KeyAmber, Mathf.Max(0, value));
                PlayerPrefs.Save();
            }
        }

        public static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;
            Quests.Clear();

            // 每日任务（含可领取 / 进行中 / 已领取三种初始演示状态，亦可在游戏中真实推进）
            AddDef("daily_roar", QuestCategory.Daily, "霸王龙威慑：完成 1 次吼叫", "使用技能 2 触发霸王龙抬头吼叫动画", "roar", 1, 1, false, QuestRewardType.Gold, 150);
            AddDef("daily_bite", QuestCategory.Daily, "岛屿狩猎：撕咬命中 5 次", "点击右下角攻击键或 J 键命中目标", "bite", 5, 0, false, QuestRewardType.Gold, 200);
            AddDef("daily_drink", QuestCategory.Daily, "补充水分：水边饮水 3 次", "靠近水面并长按攻击键完成饮水恢复", "drink", 3, 1, false, QuestRewardType.Amber, 15);
            AddDef("daily_dash", QuestCategory.Daily, "狂暴冲刺：释放冲刺技能 3 次", "使用技能 1 或 K 键向前冲撞突袭", "dash", 3, 0, false, QuestRewardType.Gold, 180);
            AddDef("daily_checkin", QuestCategory.Daily, "每日登录岛屿签到", "每日首次启动游戏即可完成", "checkin", 1, 1, true, QuestRewardType.Gold, 100);

            // 主线任务
            AddDef("main_arrive", QuestCategory.Main, "初入史前岛屿：进入游戏大厅", "开启 PixelSaur 3D 像素恐龙生存之旅", "arrive", 1, 1, false, QuestRewardType.Gold, 300);
            AddDef("main_hunter", QuestCategory.Main, "顶级掠食者：累计咬击命中 10 次", "不断狩猎并磨练恐龙战斗技巧", "bite", 10, 2, false, QuestRewardType.Amber, 30);
            AddDef("main_sneak", QuestCategory.Main, "潜行专家：进入潜行姿态 5 次", "使用潜行按钮或 C 键隐蔽接近猎物", "sneak", 5, 0, false, QuestRewardType.Gold, 250);
            AddDef("main_roar_master", QuestCategory.Main, "领域宣誓：累计吼叫 5 次", "通过吼叫宣示霸王龙的岛屿主权", "roar", 5, 1, false, QuestRewardType.Amber, 20);
            AddDef("main_first_settings", QuestCategory.Main, "定制专属体验：打开大厅指南", "熟悉基础设置与岛屿操作方式", "guide", 1, 1, true, QuestRewardType.Amber, 10);
        }

        private static void AddDef(string id, QuestCategory cat, string title, string desc, string triggerTag,
            int target, int defProg, bool defClaimed, QuestRewardType rType, int rAmount)
        {
            int savedProg = PlayerPrefs.GetInt(KeyProgPrefix + id, defProg);
            bool savedClaimed = PlayerPrefs.GetInt(KeyClaimPrefix + id, defClaimed ? 1 : 0) == 1;

            Quests.Add(new QuestItemData
            {
                id = id,
                category = cat,
                title = title,
                desc = desc,
                triggerTag = triggerTag,
                targetCount = Mathf.Max(1, target),
                defaultProgress = defProg,
                defaultClaimed = defClaimed,
                rewardType = rType,
                rewardAmount = rAmount,
                currentProgress = Mathf.Clamp(savedProg, 0, Mathf.Max(1, target)),
                isClaimed = savedClaimed
            });
        }

        public static List<QuestItemData> GetQuestsByCategory(QuestCategory category)
        {
            EnsureInitialized();
            var list = new List<QuestItemData>();
            for (int i = 0; i < Quests.Count; i++)
            {
                if (Quests[i].category == category)
                {
                    list.Add(Quests[i]);
                }
            }

            // 排序：可领取置顶 -> 进行中居中 -> 已领取沉底
            list.Sort((a, b) =>
            {
                int sa = StateSortPriority(a.GetState());
                int sb = StateSortPriority(b.GetState());
                return sa.CompareTo(sb);
            });
            return list;
        }

        private static int StateSortPriority(QuestButtonState s)
        {
            switch (s)
            {
                case QuestButtonState.Claimable: return 0;
                case QuestButtonState.InProgress: return 1;
                default: return 2;
            }
        }

        public static int GetClaimableCount(QuestCategory category)
        {
            EnsureInitialized();
            int count = 0;
            for (int i = 0; i < Quests.Count; i++)
            {
                if (Quests[i].category == category && Quests[i].GetState() == QuestButtonState.Claimable)
                {
                    count++;
                }
            }
            return count;
        }

        public static void ReportProgress(string triggerTag, int delta = 1)
        {
            if (string.IsNullOrEmpty(triggerTag) || delta <= 0) return;
            EnsureInitialized();

            bool changed = false;
            for (int i = 0; i < Quests.Count; i++)
            {
                QuestItemData q = Quests[i];
                if (q.isClaimed || q.triggerTag != triggerTag) continue;
                if (q.currentProgress < q.targetCount)
                {
                    q.currentProgress = Mathf.Min(q.targetCount, q.currentProgress + delta);
                    PlayerPrefs.SetInt(KeyProgPrefix + q.id, q.currentProgress);
                    changed = true;
                }
            }

            if (changed)
            {
                PlayerPrefs.Save();
                OnDataChanged?.Invoke();
            }
        }

        public static bool TryClaimReward(string questId, out QuestRewardType rewardType, out int rewardAmount)
        {
            EnsureInitialized();
            rewardType = QuestRewardType.Gold;
            rewardAmount = 0;

            for (int i = 0; i < Quests.Count; i++)
            {
                QuestItemData q = Quests[i];
                if (q.id == questId && q.GetState() == QuestButtonState.Claimable)
                {
                    q.isClaimed = true;
                    PlayerPrefs.SetInt(KeyClaimPrefix + q.id, 1);

                    rewardType = q.rewardType;
                    rewardAmount = q.rewardAmount;
                    if (q.rewardType == QuestRewardType.Gold) Gold += q.rewardAmount;
                    else Amber += q.rewardAmount;

                    PlayerPrefs.Save();
                    OnDataChanged?.Invoke();
                    return true;
                }
            }

            return false;
        }

        public static void ResetAllQuestsAndCurrency()
        {
            _initialized = false;
            EnsureInitialized();
            PlayerPrefs.SetInt(KeyGold, 1280);
            PlayerPrefs.SetInt(KeyAmber, 60);
            for (int i = 0; i < Quests.Count; i++)
            {
                QuestItemData q = Quests[i];
                q.currentProgress = q.defaultProgress;
                q.isClaimed = q.defaultClaimed;
                PlayerPrefs.SetInt(KeyProgPrefix + q.id, q.defaultProgress);
                PlayerPrefs.SetInt(KeyClaimPrefix + q.id, q.defaultClaimed ? 1 : 0);
            }
            PlayerPrefs.Save();
            OnDataChanged?.Invoke();
        }
    }
}
