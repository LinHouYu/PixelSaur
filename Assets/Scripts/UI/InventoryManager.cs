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
        Skin
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

        public float inGameScaleMultiplier = 1.0f;
        public float moveSpeedMultiplier = 1.0f;
        public float attackMultiplier = 1.0f;

        public bool defaultUnlocked;
        public bool unlocked;

        public string GetCategoryLabel()
        {
            switch (category)
            {
                case InventoryCategory.Dino: return "恐龙";
                case InventoryCategory.Skin: return "皮肤";
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
    /// 恐龙与皮肤仓库本地数据管理器（大厅与游戏内共用）：
    /// 1. 集中保存玩家在商店一次性购买解锁的【恐龙】与【皮肤】；
    /// 2. 支持在大厅以及游戏运行过程中实时切换出战恐龙与穿戴皮肤，同步影响角色体型、移速、攻击力与外观着色；
    /// 3. 采用 JSON 文件（persistentDataPath）+ PlayerPrefs 双备份进行本地持久化读写。
    /// </summary>
    public static class InventoryManager
    {
        public const string DefaultDinoId = "dino_trex";
        public const string DefaultSkinId = "skin_default";
        private const string SaveFileName = "pixelsaur_warehouse_v2.json";
        private const string PrefsBackupKey = "PIXELSaur_WAREHOUSE_JSON_V2";

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

            // 1. 恐龙（默认拥有 1 只基础霸王龙，其余均需在商店一次性购买解锁并进入仓库）
            AddDino("dino_trex", InventoryRarity.Common,
                "成年霸王龙 · T-Rex", "初始主战恐龙 · 免费赠送",
                "体型 1.00x  |  攻击 100%  |  移速 100%",
                "像素侏罗纪岛屿的基础主战霸王龙，属性均衡全面，擅长近战撕咬、潜行、狂暴冲刺与震慑吼叫，出征前无需解锁即可直接出战。",
                "dino_sneak", new Color(0.24f, 0.72f, 0.36f, 1f), Color.white,
                previewScale: 0.78f, inGameScaleMult: 1.00f, speedMult: 1.00f, atkMult: 1.00f, defaultUnlocked: true);

            AddDino("dino_rex_juvenile", InventoryRarity.Rare,
                "迅捷亚成体霸王龙", "商店限定 · 灵巧机动型",
                "体型 0.82x  |  攻击 85%  |  移速 +18%",
                "体型更为精悍灵活的年轻霸王龙，具备更快的移动与追击节奏，切换使用后角色身形缩小且奔跑速率明显提升。",
                "dino_jump", new Color(0.32f, 0.80f, 0.56f, 1f), new Color(0.88f, 1.04f, 0.90f, 1f),
                previewScale: 0.64f, inGameScaleMult: 0.82f, speedMult: 1.18f, atkMult: 0.85f, defaultUnlocked: false);

            AddDino("dino_rex_alpha", InventoryRarity.Epic,
                "狂暴领主霸王龙", "商店限定 · 高攻重装型",
                "体型 1.12x  |  攻击 +28%  |  移速 +6%",
                "经历数场领地争夺洗礼的成年领主霸王，体型更加魁梧雄壮，切换出战后撕咬与冲刺攻击力提升 28%。",
                "dino_bite", new Color(0.92f, 0.42f, 0.24f, 1f), new Color(1.06f, 0.86f, 0.76f, 1f),
                previewScale: 0.86f, inGameScaleMult: 1.12f, speedMult: 1.06f, atkMult: 1.28f, defaultUnlocked: false);

            AddDino("dino_raptor_swift", InventoryRarity.Epic,
                "迅猛疾风掠食龙", "商店限定 · 极速突袭型",
                "体型 0.90x  |  攻击 +12%  |  移速 +28%",
                "擅长岛屿长距离奔袭的高速掠食恐龙，切换上场后可大幅提升移动追猎速度并兼具强劲咬击威力。",
                "pterosaur", new Color(0.24f, 0.86f, 0.64f, 1f), new Color(0.76f, 1.02f, 0.88f, 1f),
                previewScale: 0.70f, inGameScaleMult: 0.90f, speedMult: 1.28f, atkMult: 1.12f, defaultUnlocked: false);

            AddDino("dino_spino", InventoryRarity.Epic,
                "浅海棘背龙猎手", "商店限定 · 水岸伏击型",
                "体型 1.06x  |  攻击 +10%  |  移速 +10%",
                "活跃于热带礁岩与海岸浅水区的强悍掠食者，兼具均衡攻防与海蓝磷甲视觉表现，购买后永久入库切换。",
                "water", new Color(0.20f, 0.68f, 0.82f, 1f), new Color(0.74f, 0.95f, 1.08f, 1f),
                previewScale: 0.81f, inGameScaleMult: 1.06f, speedMult: 1.10f, atkMult: 1.10f, defaultUnlocked: false);

            AddDino("dino_obsidian_rex", InventoryRarity.Legendary,
                "黑曜炎狱霸王龙", "商店限定 · SSS 级深渊霸主",
                "体型 1.16x  |  攻击 +45%  |  移速 +8%",
                "传说级黑曜重甲霸王龙，具备压倒性的撕咬伤害（+45%）与暗紫霸主气场，游戏内切换立即巨化并全面强化火力。",
                "dino_skill1", new Color(0.68f, 0.44f, 0.98f, 1f), new Color(0.68f, 0.56f, 0.96f, 1f),
                previewScale: 0.88f, inGameScaleMult: 1.16f, speedMult: 1.08f, atkMult: 1.45f, defaultUnlocked: false);

            // 2. 皮肤（默认拥有 1 套原生涂装，其余在商店一次性购买解锁并进入仓库切换）
            AddSkin("skin_default", InventoryRarity.Common,
                "原生丛林绿涂装", "默认标准外观涂装",
                "外观加成：原始像素森林迷彩",
                "霸王龙最经典的像素迷彩表皮，可在翠绿茂密的岛屿草丛中保持自然保护色，卸下定制皮肤后自动还原此涂装。",
                "dino_sneak", new Color(0.38f, 0.76f, 0.36f, 1f), Color.white, defaultUnlocked: true);

            AddSkin("skin_golden", InventoryRarity.Rare,
                "远古黄金琥珀甲", "商店限定 · 鎏金纪念涂装",
                "外观加成：耀眼琥珀鎏金表皮",
                "封印过远古树脂精华的金色像素皮肤，阳光照射下散发温暖明亮的金属质感，购买后永久入库随心穿戴。",
                "dino_skill2", new Color(0.98f, 0.78f, 0.24f, 1f), new Color(1.22f, 1.08f, 0.56f, 1f), defaultUnlocked: false);

            AddSkin("skin_frost", InventoryRarity.Rare,
                "霜雪冰原战纹", "商店限定 · 极寒质感涂装",
                "外观加成：冷冽银白冰晶表皮",
                "取自史前冰川冻土的银白像素护甲，在正午海风下呈现清爽冷冽的浅蓝高光，大厅与岛屿实战同步染色。",
                "dino_jump", new Color(0.68f, 0.88f, 1.0f, 1f), new Color(0.94f, 1.10f, 1.24f, 1f), defaultUnlocked: false);

            AddSkin("skin_emerald", InventoryRarity.Epic,
                "翡翠晶鳞限定涂装", "商店限定 · 碧玉晶鳞涂装",
                "外观加成：流光翡翠绿晶鳞光效",
                "商店专属解锁的珍稀晶鳞外观，穿戴后无论是大厅预览还是进入游戏均呈现出浓郁的翡翠碧玉质感。",
                "dino_sneak", new Color(0.18f, 0.86f, 0.50f, 1f), new Color(0.46f, 1.20f, 0.74f, 1f), defaultUnlocked: false);

            AddSkin("skin_abyss", InventoryRarity.Epic,
                "深海魅影冰蓝鳞", "商店限定 · 海域迷彩皮肤",
                "外观加成：幽邃深海蓝光涂装",
                "模仿深海海沟荧光矿物打造的冷色调皮肤，于碧蓝海面及浅水区涉水时格外协调，冷静肃杀。",
                "water", new Color(0.22f, 0.76f, 0.98f, 1f), new Color(0.58f, 0.88f, 1.24f, 1f), defaultUnlocked: false);

            AddSkin("skin_volcano", InventoryRarity.Legendary,
                "赤焰火山熔岩纹", "商店限定 · 传说动态战纹",
                "外观加成：炽热火山红橙战甲光效",
                "熔岩裂谷淬炼而成的红橙像素甲壳皮肤，穿戴后霸王龙全身披上深红烈焰战纹色泽，尽显掠食者锋芒。",
                "dino_skill1", new Color(0.98f, 0.40f, 0.22f, 1f), new Color(1.26f, 0.64f, 0.46f, 1f), defaultUnlocked: false);
        }

        private static void AddDino(string id, InventoryRarity rarity,
            string name, string subtitle, string statsSummary, string description, string iconKey,
            Color accentColor, Color previewTintColor, float previewScale,
            float inGameScaleMult, float speedMult, float atkMult, bool defaultUnlocked)
        {
            Items.Add(new InventoryItemData
            {
                id = id,
                category = InventoryCategory.Dino,
                rarity = rarity,
                name = name,
                subtitle = subtitle,
                statsSummary = statsSummary,
                description = description,
                iconKey = iconKey,
                accentColor = accentColor,
                previewTintColor = previewTintColor,
                previewScale = previewScale,
                inGameScaleMultiplier = inGameScaleMult,
                moveSpeedMultiplier = speedMult,
                attackMultiplier = atkMult,
                defaultUnlocked = defaultUnlocked,
                unlocked = defaultUnlocked
            });
        }

        private static void AddSkin(string id, InventoryRarity rarity,
            string name, string subtitle, string statsSummary, string description, string iconKey,
            Color accentColor, Color previewTintColor, bool defaultUnlocked)
        {
            Items.Add(new InventoryItemData
            {
                id = id,
                category = InventoryCategory.Skin,
                rarity = rarity,
                name = name,
                subtitle = subtitle,
                statsSummary = statsSummary,
                description = description,
                iconKey = iconKey,
                accentColor = accentColor,
                previewTintColor = previewTintColor,
                previewScale = 0.78f,
                inGameScaleMultiplier = 1.0f,
                moveSpeedMultiplier = 1.0f,
                attackMultiplier = 1.0f,
                defaultUnlocked = defaultUnlocked,
                unlocked = defaultUnlocked
            });
        }

        private static void LoadFromDisk()
        {
            string json = null;
            try
            {
                if (File.Exists(SaveFilePath))
                {
                    json = File.ReadAllText(SaveFilePath);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[InventoryManager] 读取本地仓库 JSON 失败，尝试回退 PlayerPrefs：{e.Message}");
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
                            def.unlocked = def.defaultUnlocked || entry.unlocked;
                        }
                    }
                }

                InventoryItemData eqDino = FindInternal(save.equippedDinoId);
                InventoryItemData eqSkin = FindInternal(save.equippedSkinId);
                _equippedDinoId = (eqDino != null && eqDino.unlocked && eqDino.category == InventoryCategory.Dino) ? eqDino.id : DefaultDinoId;
                _equippedSkinId = (eqSkin != null && eqSkin.unlocked && eqSkin.category == InventoryCategory.Skin) ? eqSkin.id : DefaultSkinId;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[InventoryManager] 解析仓库存档失败，恢复默认配置：{e.Message}");
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
                Debug.LogWarning($"[InventoryManager] 写入本地仓库 JSON 失败（已写入 PlayerPrefs）：{e.Message}");
            }
        }

        public static List<InventoryItemData> GetItemsByCategory(InventoryCategory category)
        {
            EnsureInitialized();
            var result = new List<InventoryItemData>();
            for (int i = 0; i < Items.Count; i++)
            {
                if (!Items[i].unlocked) continue;
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

        public static bool IsItemUnlocked(string itemId)
        {
            EnsureInitialized();
            InventoryItemData item = FindInternal(itemId);
            return item != null && item.unlocked;
        }

        public static bool UnlockPurchasedItem(string itemId)
        {
            EnsureInitialized();
            InventoryItemData item = FindInternal(itemId);
            if (item == null || item.unlocked) return false;
            item.unlocked = true;
            SaveToDisk();
            OnInventoryChanged?.Invoke();
            return true;
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

        public static float GetEffectiveInGameScaleMultiplier()
        {
            InventoryItemData dino = GetEquippedDino();
            return dino != null && dino.inGameScaleMultiplier > 0.1f ? dino.inGameScaleMultiplier : 1.0f;
        }

        public static float GetEffectiveMoveSpeedMultiplier()
        {
            InventoryItemData dino = GetEquippedDino();
            return dino != null && dino.moveSpeedMultiplier > 0.1f ? dino.moveSpeedMultiplier : 1.0f;
        }

        public static float GetEffectiveAttackMultiplier()
        {
            InventoryItemData dino = GetEquippedDino();
            return dino != null && dino.attackMultiplier > 0.1f ? dino.attackMultiplier : 1.0f;
        }

        private static readonly Dictionary<string, Texture2D> SkinTextureCache = new Dictionary<string, Texture2D>();

        public static string GetEffectiveSkinTextureResourcePath()
        {
            EnsureInitialized();
            if (!string.IsNullOrEmpty(_equippedSkinId) && _equippedSkinId != DefaultSkinId)
            {
                return "DinoSkins/" + _equippedSkinId;
            }
            if (!string.IsNullOrEmpty(_equippedDinoId) && _equippedDinoId != DefaultDinoId)
            {
                return "DinoSkins/" + _equippedDinoId;
            }
            return null;
        }

        public static Texture2D GetEffectiveSkinTexture()
        {
            string resPath = GetEffectiveSkinTextureResourcePath();
            if (string.IsNullOrEmpty(resPath)) return null;

            if (SkinTextureCache.TryGetValue(resPath, out Texture2D cached) && cached != null)
            {
                return cached;
            }

            Texture2D tex = Resources.Load<Texture2D>(resPath);
            if (tex != null)
            {
                tex.filterMode = FilterMode.Point;
                SkinTextureCache[resPath] = tex;
            }
            return tex;
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
            if (item == null || !item.unlocked)
            {
                feedbackMessage = "尚未在商店购买解锁该物品。";
                return false;
            }

            if (item.category == InventoryCategory.Dino)
            {
                if (_equippedDinoId == item.id)
                {
                    feedbackMessage = $"【{item.name}】当前已在游戏中出战";
                    return false;
                }

                _equippedDinoId = item.id;
                SaveToDisk();
                OnInventoryChanged?.Invoke();
                feedbackMessage = $"✓ 已切换出战恐龙：【{item.name}】";
                return true;
            }

            // 皮肤穿戴 / 卸下逻辑
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

        public static void ResetInventoryData()
        {
            _initialized = false;
            EnsureInitialized();
            _equippedDinoId = DefaultDinoId;
            _equippedSkinId = DefaultSkinId;
            for (int i = 0; i < Items.Count; i++)
            {
                Items[i].unlocked = Items[i].defaultUnlocked;
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
