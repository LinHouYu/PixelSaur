using System;
using System.Collections.Generic;
using UnityEngine;

namespace PixelSaur.UI
{
    public enum ShopTab
    {
        Dinos,
        Skins,
        Recharge
    }

    public enum ShopCurrency
    {
        Fossil,
        Amber
    }

    [Serializable]
    public class ShopProductData
    {
        public string productId;
        public ShopTab tab;
        public ShopCurrency currency;
        public int price;
        public string title;
        public string subtitle;
        public string description;
        public string rarityLabel;
        public Color rarityColor;
        public Color accentColor;
        public string iconKey;

        public string targetInventoryId;
        public bool isRechargePack;
        public int rechargeGold;
        public int rechargeAmber;
    }

    /// <summary>
    /// 岛屿恐龙商店管理器：
    /// 1. 主打【恐龙】与【皮肤】两大核心商品页签，均为一次性购买解锁；
    /// 2. 购买时扣除【恐龙化石】或【琥珀】，立即写入 InventoryManager 本地仓库；
    /// 3. 购买后的恐龙与皮肤状态锁定为“已拥有”，不可重复购买，且可直接在游戏与大厅仓库中切换使用。
    /// </summary>
    public static class ShopManager
    {
        public static event Action OnShopUpdated;

        private static bool _initialized;
        private static readonly List<ShopProductData> Products = new List<ShopProductData>();

        public static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;
            BuildProducts();
        }

        private static void BuildProducts()
        {
            Products.Clear();

            // 1. 恐龙（一次性购买解锁，进入游戏仓库后可切换出战）
            AddProduct("shop_dino_trex", ShopTab.Dinos, ShopCurrency.Fossil, 0,
                "成年霸王龙 · T-Rex", "初始主战恐龙 · 免费入库",
                "基础均衡型陆地霸主（1.00x 体型 / 100% 攻速）。所有驯龙者初始即已在仓库中拥有。",
                "普通", new Color(0.46f, 0.56f, 0.68f, 1f), new Color(0.24f, 0.72f, 0.36f, 1f), "dino_sneak",
                "dino_trex");

            AddProduct("shop_dino_juvenile", ShopTab.Dinos, ShopCurrency.Fossil, 240,
                "迅捷亚成体霸王龙", "一次性购买 · 移速 +18%",
                "身形精悍（0.82x）的敏捷霸王龙，奔跑速度提升 18%。买后永久存入游戏仓库并支持局内切换。",
                "稀有", new Color(0.24f, 0.66f, 0.98f, 1f), new Color(0.32f, 0.80f, 0.56f, 1f), "dino_jump",
                "dino_rex_juvenile");

            AddProduct("shop_dino_alpha", ShopTab.Dinos, ShopCurrency.Fossil, 380,
                "狂暴领主霸王龙", "一次性购买 · 攻击 +28%",
                "体型魁梧（1.12x）的重装领主，咬击与冲刺伤害提高 28%。一次性解锁并同步进入仓库。",
                "史诗", new Color(0.68f, 0.42f, 0.96f, 1f), new Color(0.92f, 0.42f, 0.24f, 1f), "dino_bite",
                "dino_rex_alpha");

            AddProduct("shop_dino_raptor", ShopTab.Dinos, ShopCurrency.Fossil, 480,
                "迅猛疾风掠食龙", "一次性购买 · 移速 +28%",
                "极速岛屿猎手，移速高达 128% 且攻击提升 12%。购买即写入仓库，大厅与局内随时出战。",
                "史诗", new Color(0.68f, 0.42f, 0.96f, 1f), new Color(0.24f, 0.86f, 0.64f, 1f), "pterosaur",
                "dino_raptor_swift");

            AddProduct("shop_dino_spino", ShopTab.Dinos, ShopCurrency.Amber, 30,
                "浅海棘背龙猎手", "一次性购买 · 琥珀专享",
                "海蓝涂装两栖掠食恐龙（体型 1.06x / 攻击 +10% / 移速 +10%），一次性解锁永久入库。",
                "史诗", new Color(0.68f, 0.42f, 0.96f, 1f), new Color(0.20f, 0.68f, 0.82f, 1f), "water",
                "dino_spino");

            AddProduct("shop_dino_obsidian", ShopTab.Dinos, ShopCurrency.Amber, 55,
                "黑曜炎狱霸王龙", "一次性购买 · 传说霸主",
                "暗紫重甲终极霸王龙（1.16x 巨型体型 / 攻击 +45%）。购买后永久进入仓库并在游戏中强力出战。",
                "传说", new Color(0.95f, 0.68f, 0.20f, 1f), new Color(0.68f, 0.44f, 0.98f, 1f), "dino_skill1",
                "dino_obsidian_rex");

            // 2. 皮肤（一次性购买解锁，进入游戏仓库自由穿戴与卸下）
            AddProduct("shop_skin_default", ShopTab.Skins, ShopCurrency.Fossil, 0,
                "原生丛林绿涂装", "默认经典涂装 · 免费入库",
                "经典像素森林迷彩表皮，游戏初始默认已穿戴于仓库。",
                "普通", new Color(0.46f, 0.56f, 0.68f, 1f), new Color(0.38f, 0.76f, 0.36f, 1f), "dino_sneak",
                "skin_default");

            AddProduct("shop_skin_golden", ShopTab.Skins, ShopCurrency.Fossil, 220,
                "远古黄金琥珀甲", "一次性购买 · 鎏金皮肤",
                "闪耀温润金属光泽的纪念鎏金涂装，购买后永久入库，大厅与游戏中皆可实时穿戴生效。",
                "稀有", new Color(0.24f, 0.66f, 0.98f, 1f), new Color(0.98f, 0.78f, 0.24f, 1f), "dino_skill2",
                "skin_golden");

            AddProduct("shop_skin_frost", ShopTab.Skins, ShopCurrency.Fossil, 280,
                "霜雪冰原战纹", "一次性购买 · 极寒皮肤",
                "极地冰川风格浅蓝银白甲壳涂装，购买即存入仓库随时切换外观。",
                "稀有", new Color(0.24f, 0.66f, 0.98f, 1f), new Color(0.68f, 0.88f, 1.0f, 1f), "dino_jump",
                "skin_frost");

            AddProduct("shop_skin_emerald", ShopTab.Skins, ShopCurrency.Fossil, 360,
                "翡翠晶鳞限定涂装", "一次性购买 · 碧玉皮肤",
                "浓郁翡翠碧玉晶体鳞片纹理，草地潜行隐蔽感十足，永久写入仓库。",
                "史诗", new Color(0.68f, 0.42f, 0.96f, 1f), new Color(0.18f, 0.86f, 0.50f, 1f), "dino_sneak",
                "skin_emerald");

            AddProduct("shop_skin_abyss", ShopTab.Skins, ShopCurrency.Amber, 25,
                "深海魅影冰蓝鳞", "一次性购买 · 琥珀专享",
                "深海冰蓝冷光迷彩涂装，搭配浅水波纹质感极佳，一次购买终身拥有。",
                "史诗", new Color(0.68f, 0.42f, 0.96f, 1f), new Color(0.22f, 0.76f, 0.98f, 1f), "water",
                "skin_abyss");

            AddProduct("shop_skin_volcano", ShopTab.Skins, ShopCurrency.Amber, 40,
                "赤焰火山熔岩纹", "一次性购买 · 传说皮肤",
                "烈焰红橙熔岩战纹皮肤，大厅与游戏局内同步显现炽热外观，永久入库。",
                "传说", new Color(0.95f, 0.68f, 0.20f, 1f), new Color(0.98f, 0.40f, 0.22f, 1f), "dino_skill1",
                "skin_volcano");

            // 3. 测试充值页（便于一键获取测试货币购买恐龙与皮肤）
            AddRecharge("recharge_amber_60", "+60 远古琥珀（测试充值）",
                "免支付点击即到账 · 立即累加琥珀",
                "点击【立即充值】直接获得 60 远古琥珀并保存至本地货币栏。",
                "测试充值", new Color(0.24f, 0.66f, 0.98f, 1f), new Color(0.28f, 0.78f, 1f, 1f), "water",
                goldGain: 0, amberGain: 60);

            AddRecharge("recharge_amber_300", "+300 远古琥珀（测试大礼包）",
                "免支付点击即到账 · 畅买传说恐龙与皮肤",
                "点击【立即充值】直接到账 300 远古琥珀，足够解锁所有琥珀限定恐龙与皮肤。",
                "推荐测试", new Color(0.68f, 0.42f, 0.96f, 1f), new Color(0.32f, 0.84f, 1f, 1f), "water",
                goldGain: 0, amberGain: 300);

            AddRecharge("recharge_fossil_500", "+500 恐龙化石（测试补给）",
                "免支付点击即到账 · 畅买化石恐龙与皮肤",
                "点击【立即充值】立即获得 500 恐龙化石，随时用于购买仓库恐龙与皮肤。",
                "化石补给", new Color(0.95f, 0.68f, 0.20f, 1f), new Color(0.94f, 0.58f, 0.24f, 1f), "food",
                goldGain: 500, amberGain: 0);

            AddRecharge("recharge_fossil_1500", "+1,500 恐龙化石（至尊测试包）",
                "大额测试补给 · 点击瞬时累加",
                "点击【立即充值】立即增加 1,500 恐龙化石，同步刷新顶部货币余额。",
                "尊享测试", new Color(0.95f, 0.68f, 0.20f, 1f), new Color(0.98f, 0.72f, 0.22f, 1f), "dino_skill2",
                goldGain: 1500, amberGain: 0);
        }

        private static void AddProduct(string id, ShopTab tab, ShopCurrency currency, int price,
            string title, string subtitle, string desc, string rarityLabel, Color rarityColor, Color accentColor,
            string iconKey, string targetInventoryId)
        {
            Products.Add(new ShopProductData
            {
                productId = id,
                tab = tab,
                currency = currency,
                price = price,
                title = title,
                subtitle = subtitle,
                description = desc,
                rarityLabel = rarityLabel,
                rarityColor = rarityColor,
                accentColor = accentColor,
                iconKey = iconKey,
                targetInventoryId = targetInventoryId,
                isRechargePack = false
            });
        }

        private static void AddRecharge(string id, string title, string subtitle, string desc,
            string rarityLabel, Color rarityColor, Color accentColor, string iconKey, int goldGain, int amberGain)
        {
            Products.Add(new ShopProductData
            {
                productId = id,
                tab = ShopTab.Recharge,
                currency = amberGain > 0 ? ShopCurrency.Amber : ShopCurrency.Fossil,
                price = 0,
                title = title,
                subtitle = subtitle,
                description = desc,
                rarityLabel = rarityLabel,
                rarityColor = rarityColor,
                accentColor = accentColor,
                iconKey = iconKey,
                isRechargePack = true,
                rechargeGold = goldGain,
                rechargeAmber = amberGain
            });
        }

        public static List<ShopProductData> GetProductsByTab(ShopTab tab)
        {
            EnsureInitialized();
            var list = new List<ShopProductData>();
            for (int i = 0; i < Products.Count; i++)
            {
                if (Products[i].tab == tab) list.Add(Products[i]);
            }
            return list;
        }

        public static ShopProductData GetProductById(string productId)
        {
            EnsureInitialized();
            for (int i = 0; i < Products.Count; i++)
            {
                if (Products[i].productId == productId) return Products[i];
            }
            return null;
        }

        public static string GetCurrencyName(ShopCurrency currency)
        {
            return currency == ShopCurrency.Fossil ? "恐龙化石" : "琥珀";
        }

        public static int GetCurrencyBalance(ShopCurrency currency)
        {
            QuestManager.EnsureInitialized();
            return currency == ShopCurrency.Fossil ? QuestManager.Gold : QuestManager.Amber;
        }

        public static bool IsOwned(ShopProductData product)
        {
            if (product == null || product.isRechargePack) return false;
            return InventoryManager.IsItemUnlocked(product.targetInventoryId);
        }

        public static bool CanAfford(ShopProductData product)
        {
            if (product == null) return false;
            if (product.isRechargePack) return true;
            return GetCurrencyBalance(product.currency) >= product.price;
        }

        public static bool TryPurchaseProduct(string productId, out string feedbackMessage)
        {
            EnsureInitialized();
            InventoryManager.EnsureInitialized();
            QuestManager.EnsureInitialized();

            ShopProductData product = GetProductById(productId);
            if (product == null)
            {
                feedbackMessage = "未找到对应商品。";
                return false;
            }

            if (product.isRechargePack)
            {
                QuestManager.AddCurrency(product.rechargeGold, product.rechargeAmber);
                OnShopUpdated?.Invoke();

                if (product.rechargeAmber > 0 && product.rechargeGold > 0)
                {
                    feedbackMessage = $"✓ 测试充值成功：+{product.rechargeGold} 恐龙化石，+{product.rechargeAmber} 琥珀！";
                }
                else if (product.rechargeAmber > 0)
                {
                    feedbackMessage = $"✓ 测试充值成功：+{product.rechargeAmber} 琥珀已到账！";
                }
                else
                {
                    feedbackMessage = $"✓ 测试充值成功：+{product.rechargeGold} 恐龙化石已到账！";
                }
                return true;
            }

            // 恐龙与皮肤严格一次性购买，已拥有不可重复购买
            if (IsOwned(product))
            {
                feedbackMessage = $"【{product.title}】已拥有（一次性解锁），请前往恐龙仓库使用！";
                return false;
            }

            QuestRewardType rewardType = product.currency == ShopCurrency.Fossil ? QuestRewardType.Gold : QuestRewardType.Amber;
            if (!QuestManager.TrySpendCurrency(rewardType, product.price))
            {
                string unit = GetCurrencyName(product.currency);
                feedbackMessage = $"{unit}余额不足（需要 {product.price} {unit}），可先点击【充值测试】补充货币！";
                return false;
            }

            InventoryManager.UnlockPurchasedItem(product.targetInventoryId);
            OnShopUpdated?.Invoke();
            feedbackMessage = $"✓ 购买成功：【{product.title}】已永久解锁并放入恐龙仓库！";
            return true;
        }
    }
}
