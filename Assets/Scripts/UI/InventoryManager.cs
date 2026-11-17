using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PixelSaur.UI
{
    public enum InventoryCategory
    {
        All,
        Dino,
        Skin,
        Item
    }

    public enum InventoryRarity
    {
        Common,
        Rare,
        Epic,
        Legendary
    }

    [Serializable]
    public class InventoryItemData
    {
        public string id;
        public InventoryCategory category;
        public InventoryRarity rarity;
        public string name;
        public string subtitle;
        public string statsSummary;
        public string description;
        public string iconKey;
        public Color accentColor;
        public Color previewTintColor;
        public float previewScale;

        public int defaultQuantity;
        public bool isStackable;
        public int rewardGold;
        public int rewardAmber;

        public int quantity;
        public bool unlocked;

        public string GetCategoryLabel()
        {
            switch (category)
            {
                case InventoryCategory.Dino: return "恐龙";
                case InventoryCategory.Skin: return "皮肤";
                case InventoryCategory.Item: return "道具";
                default: return "全部";
            }
        }

        public string GetRarityLabel()
        {
            switch (rarity)
            {
                case InventoryRarity.Legendary: return "传说";
                case InventoryRarity.Epic: return "史诗";
                case InventoryRarity.Rare: return "稀有";
                default: return "普通";
            }
        }

        public Color GetRarityColor()
        {
            switch (rarity)
            {
                case InventoryRarity.Legendary: return new Color(0.95f, 0.68f, 0.20f, 1f);
                case InventoryRarity.Epic: return new Color(0.68f, 0.42f, 0.96f, 1f);
                case InventoryRarity.Rare: return new Color(0.24f, 0.66f, 0.98f, 1f);
                default: return new Color(0.46f, 0.56f, 0.68f, 1f);
            }
        }
    }

    [Serializable]
    internal class InventoryItemSaveEntry
    {
        public string id;
        public int quantity;
        public bool unlocked;
    }

    [Serializable]
    internal class InventorySaveData
    {
        public string equippedDinoId = "dino_trex";
        public string equippedSkinId = "skin_default";
        public List<InventoryItemSaveEntry> items = new List<InventoryItemSaveEntry>();
    }

    /// <summary>
    /// 开始场景本地背包与仓库数据管理器：
    /// 1. 维护恐龙 / 皮肤 / 道具三类物品定义、穿戴/出战状态与道具库存；
    /// 2. 使用 JSON 文件（persistentDataPath）+ PlayerPrefs 双备份进行本地读写与持久化，重启不丢失。
    /// </summary>
    public static class InventoryManager
    {
        public const string DefaultDinoId = "dino_trex";
        public const string DefaultSkinId = "skin_default";
        private const string SaveFileName = "pixelsaur_inventory_v1.json";
        private const string PrefsBackupKey = "PIXELSaur_INVENTORY_JSON_V1";

        public static event Action OnInventoryChanged;

        private static bool _initialized;
        private static readonly List<InventoryItemData> Items = new List<InventoryItemData>();
        private static string _equippedDinoId = DefaultDinoId;
        private static string _equippedSkinId = DefaultSkinId;

        public static string EquippedDinoId
        {
            get
            {
                EnsureInitialized();
                return _equippedDinoId;
            }
        }

        public static string EquippedSkinId
        {
            get
            {
                EnsureInitialized();
                return _equippedSkinId;
            }
        }

        public static string SaveFilePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        public static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;
            BuildDefinitions();
            LoadFromDisk();
        }

        public static void ReloadFromDisk()
        {
            _initialized = false;
            EnsureInitialized();
            OnInventoryChanged?.Invoke();
        }

        private static void BuildDefinitions()
        {
            Items.Clear();

            // 1. 恐龙（出战个体切换）
            AddDef("dino_trex", InventoryCategory.Dino, InventoryRarity.Legendary,
                "成年霸王龙 · T-Rex", "SS 级陆地顶级掠食者",
                "生命 100  |  攻击 25  |  移速 6.5",
                "主战像素霸王龙形态，兼具强劲下颌撕咬、潜行接近、狂暴冲刺与震慑吼叫四项岛屿核心技能，适应陆地与近岸浅海追猎。",
                "dino_sneak", new Color(0.24f, 0.72f, 0.36f, 1f), Color.white, 0.78f,
                isStackable: false, defaultQty: 1, rewardGold: 0, rewardAmber: 0);

            AddDef("dino_rex_alpha", InventoryCategory.Dino, InventoryRarity.Legendary,
                "狂暴领主霸王龙", "SSS 级岛屿霸主个体",
                "生命 135  |  攻击 32  |  移速 7.0",
                "经历数场领地争夺洗礼的成年霸王，体型更加魁梧雄壮。穿戴出战后，大厅展示霸王龙将切换为更硕大的领主体型，气场全面提升。",
                "dino_bite", new Color(0.92f, 0.42f, 0.24f, 1f), new Color(1.06f, 0.86f, 0.76f, 1f), 0.86f,
                isStackable: false, defaultQty: 1, rewardGold: 0, rewardAmber: 0);

            AddDef("dino_rex_juvenile", InventoryCategory.Dino, InventoryRarity.Epic,
                "迅捷亚成体霸王龙", "A 级灵巧突袭猎手",
                "生命 78  |  攻击 19  |  移速 7.8",
                "身形轻盈矫健的年轻霸王龙，擅长在密林草丛间高速穿行，降低遭遇伏击时的转向延迟，适合灵活机动打法。",
                "dino_jump", new Color(0.32f, 0.80f, 0.56f, 1f), new Color(0.88f, 1.04f, 0.90f, 1f), 0.64f,
                isStackable: false, defaultQty: 1, rewardGold: 0, rewardAmber: 0);

            AddDef("dino_pterosaur", InventoryCategory.Dino, InventoryRarity.Epic,
                "苍穹翼龙侦察形态", "S 级海岸巡航物种",
                "生命 85  |  攻击 16  |  移速 8.4",
                "活跃于热带礁岩与海岸线上空的翼龙品种，视野开阔、身姿轻盈，能够在岛屿浅水海域附近快速巡航搜寻猎物。",
                "pterosaur", new Color(0.26f, 0.74f, 0.96f, 1f), new Color(0.84f, 0.98f, 1.06f, 1f), 0.72f,
                isStackable: false, defaultQty: 1, rewardGold: 0, rewardAmber: 0);

            AddDef("dino_spino", InventoryCategory.Dino, InventoryRarity.Epic,
                "浅海棘背龙猎手", "S 级水陆两栖掠食者",
                "生命 115  |  攻击 24  |  移速 6.9",
                "背部长有标志性帆状棘刺的两栖恐龙，在水域及沙滩地形移动流畅，擅长伏击海岸附近的游荡猎物。",
                "water", new Color(0.20f, 0.68f, 0.82f, 1f), new Color(0.76f, 0.94f, 1.05f, 1f), 0.81f,
                isStackable: false, defaultQty: 1, rewardGold: 0, rewardAmber: 0);

            // 2. 皮肤（外观穿戴与卸下切换）
            AddDef("skin_default", InventoryCategory.Skin, InventoryRarity.Common,
                "原生丛林绿涂装", "经典原始像素涂装",
                "外观加成：标准森林隐蔽色",
                "霸王龙最经典的像素迷彩表皮，能够在翠绿茂密的岛屿草丛中提供自然保护色。卸下其他定制皮肤后将自动还原为此涂装。",
                "dino_sneak", new Color(0.38f, 0.76f, 0.36f, 1f), Color.white, 0.78f,
                isStackable: false, defaultQty: 1, rewardGold: 0, rewardAmber: 0);

            AddDef("skin_volcano", InventoryCategory.Skin, InventoryRarity.Legendary,
                "赤焰火山熔岩纹", "传说级动态战纹皮肤",
                "外观加成：炽热火山红橙战甲光效",
                "熔岩裂谷淬炼而成的红橙像素甲壳皮肤。穿戴后大厅展示的霸王龙表面将披上深红烈焰战纹色泽，尽显掠食者锋芒。",
                "dino_skill1", new Color(0.98f, 0.40f, 0.22f, 1f), new Color(1.26f, 0.64f, 0.46f, 1f), 0.78f,
                isStackable: false, defaultQty: 1, rewardGold: 0, rewardAmber: 0);

            AddDef("skin_abyss", InventoryCategory.Skin, InventoryRarity.Epic,
                "深海魅影冰蓝鳞", "史诗级海域迷彩皮肤",
                "外观加成：幽邃海洋冰蓝反光",
                "模仿深海海沟荧光矿物打造的冷色调皮肤，于碧蓝海面及浅水区涉水时格外协调，自带冷静肃杀的猎手质感。",
                "water", new Color(0.22f, 0.76f, 0.98f, 1f), new Color(0.58f, 0.88f, 1.24f, 1f), 0.78f,
                isStackable: false, defaultQty: 1, rewardGold: 0, rewardAmber: 0);

            AddDef("skin_golden", InventoryCategory.Skin, InventoryRarity.Rare,
                "远古黄金琥珀甲", "稀有纪念鎏金皮肤",
                "外观加成：耀眼琥珀鎏金表皮",
                "封印过远古树脂精华的金色像素皮肤，阳光照射下散发温暖明亮的金属质感，象征尊贵的驯龙先行者荣耀。",
                "dino_skill2", new Color(0.98f, 0.78f, 0.24f, 1f), new Color(1.22f, 1.08f, 0.56f, 1f), 0.78f,
                isStackable: false, defaultQty: 1, rewardGold: 0, rewardAmber: 0);

            AddDef("skin_frost", InventoryCategory.Skin, InventoryRarity.Rare,
                "霜雪冰原战纹", "稀有极寒质感涂装",
                "外观加成：冷冽银白冰晶表皮",
                "取自史前冰川冻土的银白像素护甲，在正午海风下呈现清爽冷冽的浅蓝高光，适合喜欢极简冷色系搭配的驯龙者。",
                "dino_jump", new Color(0.68f, 0.88f, 1.0f, 1f), new Color(0.96f, 1.10f, 1.22f, 1f), 0.78f,
                isStackable: false, defaultQty: 1, rewardGold: 0, rewardAmber: 0);

            // 3. 道具（可使用消耗并累加货币/即时反馈）
            AddDef("item_meat", InventoryCategory.Item, InventoryRarity.Common,
                "鲜嫩恐龙肉排", "消耗品 · 能量补给",
                "使用效果：每次消耗 1 份，兑换 +80 龙肉金币",
                "岛屿狩猎收集的高蛋白炙烤肉排。在背包中点击使用后可立即转化为补给资金，同步累加至顶部金币栏并自动保存。",
                "food", new Color(0.92f, 0.56f, 0.26f, 1f), Color.white, 0.78f,
                isStackable: true, defaultQty: 5, rewardGold: 80, rewardAmber: 0);

            AddDef("item_amber_shard", InventoryCategory.Item, InventoryRarity.Epic,
                "精纯琥珀结晶", "消耗品 · 珍稀矿物",
                "使用效果：每次消耗 1 颗，提炼 +10 远古琥珀",
                "蕴含史前蚊虫与纯正松脂能量的半透明蓝珀原石。点击使用即可提取其中的纯净琥珀，立即增加大厅琥珀库存。",
                "water", new Color(0.32f, 0.80f, 1.0f, 1f), Color.white, 0.78f,
                isStackable: true, defaultQty: 3, rewardGold: 0, rewardAmber: 10);

            AddDef("item_potion", InventoryCategory.Item, InventoryRarity.Rare,
                "史前活力药剂", "消耗品 · 战斗激励",
                "使用效果：获得 +50 金币并激发霸王龙活力",
                "由岛屿热带草药浓缩而成的生命活力浓缩剂，使用后可激发霸王龙兴奋状态并提供少量探险经费补贴。",
                "heart", new Color(0.94f, 0.34f, 0.42f, 1f), Color.white, 0.78f,
                isStackable: true, defaultQty: 4, rewardGold: 50, rewardAmber: 0);

            AddDef("item_supply_box", InventoryCategory.Item, InventoryRarity.Legendary,
                "岛屿探索补给箱", "消耗品 · 惊喜补给礼盒",
                "使用效果：开启获得 +150 金币 与 +5 琥珀",
                "空投至像素岛屿海岸线的特种木箱，内部封装有珍贵的生存货币与琥珀样本，消耗开启后双项资源同步到账。",
                "dino_skill2", new Color(0.98f, 0.72f, 0.22f, 1f), Color.white, 0.78f,
                isStackable: true, defaultQty: 2, rewardGold: 150, rewardAmber: 5);

            AddDef("item_herb", InventoryCategory.Item, InventoryRarity.Common,
                "热带治疗草药", "消耗品 · 伤口舒缓",
                "使用效果：每次消耗 1 份，兑换 +40 龙肉金币",
                "生长在岛屿林荫间的天然止血草束，常用于战斗后的快速休整，也可在集市中直接兑换探索经费。",
                "heart", new Color(0.32f, 0.84f, 0.46f, 1f), Color.white, 0.78f,
                isStackable: true, defaultQty: 6, rewardGold: 40, rewardAmber: 0);

            AddDef("item_beacon", InventoryCategory.Item, InventoryRarity.Rare,
                "恐龙信号信标", "消耗品 · 支援召唤道具",
                "使用效果：每次消耗 1 个，兑换 +8 远古琥珀",
                "能够在岛屿制高点发送高频声波脉冲的定位装置，使用后可回收能量核心提炼远古琥珀资源。",
                "dino_skill1", new Color(0.42f, 0.74f, 0.98f, 1f), Color.white, 0.78f,
                isStackable: true, defaultQty: 2, rewardGold: 0, rewardAmber: 8);
        }

        private static void AddDef(string id, InventoryCategory category, InventoryRarity rarity,
            string name, string subtitle, string statsSummary, string description, string iconKey,
            Color accentColor, Color previewTintColor, float previewScale,
            bool isStackable, int defaultQty, int rewardGold, int rewardAmber)
        {
            Items.Add(new InventoryItemData
            {
                id = id,
                category = category,
                rarity = rarity,
                name = name,
                subtitle = subtitle,
                statsSummary = statsSummary,
                description = description,
                iconKey = iconKey,
                accentColor = accentColor,
                previewTintColor = previewTintColor,
                previewScale = previewScale,
                isStackable = isStackable,
                defaultQuantity = defaultQty,
                rewardGold = rewardGold,
                rewardAmber = rewardAmber,
                quantity = defaultQty,
                unlocked = true
            });
        }

        private static void LoadFromDisk()
        {
            string json = null;
            try
            {
                string path = SaveFilePath;
                if (File.Exists(path))
                {
                    json = File.ReadAllText(path);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[InventoryManager] 读取本地背包 JSON 文件失败，将尝试回退 PlayerPrefs：{e.Message}");
            }

            if (string.IsNullOrEmpty(json) && PlayerPrefs.HasKey(PrefsBackupKey))
            {
                json = PlayerPrefs.GetString(PrefsBackupKey, string.Empty);
            }

            if (string.IsNullOrEmpty(json))
            {
                _equippedDinoId = DefaultDinoId;
                _equippedSkinId = DefaultSkinId;
                SaveToDisk();
                return;
            }

            try
            {
                InventorySaveData save = JsonUtility.FromJson<InventorySaveData>(json);
                if (save == null)
                {
                    _equippedDinoId = DefaultDinoId;
                    _equippedSkinId = DefaultSkinId;
                    SaveToDisk();
                    return;
                }

                if (save.items != null)
                {
                    for (int i = 0; i < save.items.Count; i++)
                    {
                        InventoryItemSaveEntry entry = save.items[i];
                        InventoryItemData def = FindInternal(entry.id);
                        if (def != null)
                        {
                            def.quantity = Mathf.Max(0, entry.quantity);
                            def.unlocked = entry.unlocked;
                        }
                    }
                }

                _equippedDinoId = FindInternal(save.equippedDinoId) != null ? save.equippedDinoId : DefaultDinoId;
                _equippedSkinId = FindInternal(save.equippedSkinId) != null ? save.equippedSkinId : DefaultSkinId;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[InventoryManager] 解析背包存档失败，使用默认背包配置：{e.Message}");
                _equippedDinoId = DefaultDinoId;
                _equippedSkinId = DefaultSkinId;
                SaveToDisk();
            }
        }

        public static void SaveToDisk()
        {
            EnsureInitialized();
            var save = new InventorySaveData
            {
                equippedDinoId = _equippedDinoId,
                equippedSkinId = _equippedSkinId,
                items = new List<InventoryItemSaveEntry>(Items.Count)
            };

            for (int i = 0; i < Items.Count; i++)
            {
                save.items.Add(new InventoryItemSaveEntry
                {
                    id = Items[i].id,
                    quantity = Items[i].quantity,
                    unlocked = Items[i].unlocked
                });
            }

            string json = JsonUtility.ToJson(save, true);
            PlayerPrefs.SetString(PrefsBackupKey, json);
            PlayerPrefs.Save();

            try
            {
                string path = SaveFilePath;
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(path, json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[InventoryManager] 写入本地背包 JSON 文件失败（已保底写入 PlayerPrefs）：{e.Message}");
            }
        }

        public static List<InventoryItemData> GetItemsByCategory(InventoryCategory category)
        {
            EnsureInitialized();
            var result = new List<InventoryItemData>();
            for (int i = 0; i < Items.Count; i++)
            {
                if (category == InventoryCategory.All || Items[i].category == category)
                {
                    result.Add(Items[i]);
                }
            }
            return result;
        }

        public static InventoryItemData GetItemById(string itemId)
        {
            EnsureInitialized();
            return FindInternal(itemId);
        }

        public static InventoryItemData GetEquippedDino()
        {
            EnsureInitialized();
            return FindInternal(_equippedDinoId) ?? FindInternal(DefaultDinoId);
        }

        public static InventoryItemData GetEquippedSkin()
        {
            EnsureInitialized();
            return FindInternal(_equippedSkinId) ?? FindInternal(DefaultSkinId);
        }

        public static Color GetEffectivePreviewTint()
        {
            InventoryItemData skin = GetEquippedSkin();
            InventoryItemData dino = GetEquippedDino();
            if (skin != null && skin.id != DefaultSkinId)
            {
                return skin.previewTintColor;
            }
            return dino != null ? dino.previewTintColor : Color.white;
        }

        public static float GetEffectivePreviewScale()
        {
            InventoryItemData dino = GetEquippedDino();
            return dino != null && dino.previewScale > 0.1f ? dino.previewScale : 0.78f;
        }

        public static bool IsItemEquipped(InventoryItemData item)
        {
            if (item == null) return false;
            EnsureInitialized();
            if (item.category == InventoryCategory.Dino) return item.id == _equippedDinoId;
            if (item.category == InventoryCategory.Skin) return item.id == _equippedSkinId;
            return false;
        }

        public static bool EquipOrUseItem(string itemId, out string feedbackMessage)
        {
            EnsureInitialized();
            InventoryItemData item = FindInternal(itemId);
            if (item == null)
            {
                feedbackMessage = "未找到对应物品。";
                return false;
            }

            if (item.category == InventoryCategory.Dino)
            {
                if (_equippedDinoId == item.id)
                {
                    feedbackMessage = $"【{item.name}】当前已在出战中";
                    return false;
                }

                _equippedDinoId = item.id;
                SaveToDisk();
                OnInventoryChanged?.Invoke();
                feedbackMessage = $"✓ 已切换出战恐龙：【{item.name}】";
                return true;
            }

            if (item.category == InventoryCategory.Skin)
            {
                if (_equippedSkinId == item.id)
                {
                    if (item.id == DefaultSkinId)
                    {
                        feedbackMessage = "当前已穿戴原生默认涂装";
                        return false;
                    }

                    _equippedSkinId = DefaultSkinId;
                    SaveToDisk();
                    OnInventoryChanged?.Invoke();
                    feedbackMessage = $"✓ 已卸下【{item.name}】，恢复原生丛林绿涂装";
                    return true;
                }

                _equippedSkinId = item.id;
                SaveToDisk();
                OnInventoryChanged?.Invoke();
                feedbackMessage = $"✓ 已穿戴皮肤：【{item.name}】";
                return true;
            }

            // 消耗道具逻辑
            if (item.quantity <= 0)
            {
                feedbackMessage = $"【{item.name}】库存不足，无法继续使用";
                return false;
            }

            item.quantity = Mathf.Max(0, item.quantity - 1);
            if (item.rewardGold > 0 || item.rewardAmber > 0)
            {
                QuestManager.AddCurrency(item.rewardGold, item.rewardAmber);
            }

            SaveToDisk();
            OnInventoryChanged?.Invoke();

            if (item.rewardGold > 0 && item.rewardAmber > 0)
            {
                feedbackMessage = $"✓ 使用【{item.name}】：+{item.rewardGold} 金币，+{item.rewardAmber} 琥珀（余 {item.quantity}）";
            }
            else if (item.rewardGold > 0)
            {
                feedbackMessage = $"✓ 使用【{item.name}】：+{item.rewardGold} 金币（余 {item.quantity}）";
            }
            else if (item.rewardAmber > 0)
            {
                feedbackMessage = $"✓ 使用【{item.name}】：+{item.rewardAmber} 琥珀（余 {item.quantity}）";
            }
            else
            {
                feedbackMessage = $"✓ 已使用【{item.name}】（余 {item.quantity}）";
            }

            return true;
        }

        public static void ResetInventoryData()
        {
            _initialized = false;
            EnsureInitialized();
            _equippedDinoId = DefaultDinoId;
            _equippedSkinId = DefaultSkinId;
            for (int i = 0; i < Items.Count; i++)
            {
                Items[i].quantity = Items[i].defaultQuantity;
                Items[i].unlocked = true;
            }
            SaveToDisk();
            OnInventoryChanged?.Invoke();
        }

        private static InventoryItemData FindInternal(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < Items.Count; i++)
            {
                if (Items[i].id == id) return Items[i];
            }
            return null;
        }
    }
}
