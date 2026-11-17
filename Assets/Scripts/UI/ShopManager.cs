using System;
using System.Collections.Generic;
using UnityEngine;

namespace PixelSaur.UI
{
    public enum ShopTab
    {
        Items,
        Dinos,
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
        public int grantQuantity;
        public bool isUniqueUnlock;

        public bool isRechargePack;
        public int rechargeGold;
        public int rechargeAmber;
    }

    /// <summary>
    /// 岛屿商店数据管理器：
    /// 1. 维护【道具商店 / 恐龙商店 / 琥珀充值】三类商品列表；
    /// 2. 执行购买结算时真实扣除【恐龙化石】或【琥珀】，并将对应物品数量或限定恐龙/皮肤写入 InventoryManager 本地仓库存档；
    /// 3. 琥珀充值页提供点击即加钱的测试充值包，与顶部货币栏和背包库存实时互通。
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

            // 1. 道具商店（消耗恐龙化石或琥珀，购买后直接累加对应道具数量至背包仓库）
            AddProduct("shop_meat_pack", ShopTab.Items, ShopCurrency.Fossil, 120,
                "鲜嫩恐龙肉排 x3", "补给礼包 · 写入背包",
                "购买后立即向恐龙背包写入 3 份【鲜嫩恐龙肉排】，使用每份可兑换 80 资源补给。",
                "普通", new Color(0.46f, 0.56f, 0.68f, 1f), new Color(0.92f, 0.56f, 0.26f, 1f), "food",
                "item_meat", 3, false);

            AddProduct("shop_herb_pack", ShopTab.Items, ShopCurrency.Fossil, 150,
                "热带治疗草药 x5", "恢复补给 · 写入背包",
                "购买后立即向恐龙背包写入 5 份【热带治疗草药】，性价比极高的日常消耗物资。",
                "普通", new Color(0.46f, 0.56f, 0.68f, 1f), new Color(0.32f, 0.84f, 0.46f, 1f), "heart",
                "item_herb", 5, false);

            AddProduct("shop_potion_pack", ShopTab.Items, ShopCurrency.Fossil, 200,
                "史前活力药剂 x3", "强化礼包 · 写入背包",
                "购买后向背包存入 3 瓶【史前活力药剂】，方便在大厅与生存冒险中随时调度使用。",
                "稀有", new Color(0.24f, 0.66f, 0.98f, 1f), new Color(0.94f, 0.34f, 0.42f, 1f), "heart",
                "item_potion", 3, false);

            AddProduct("shop_beacon_pack", ShopTab.Items, ShopCurrency.Fossil, 260,
                "恐龙信号信标 x2", "战术物资 · 写入背包",
                "购买后获得 2 个【恐龙信号信标】并存入背包，使用后可提炼额外远古琥珀。",
                "稀有", new Color(0.24f, 0.66f, 0.98f, 1f), new Color(0.42f, 0.74f, 0.98f, 1f), "dino_skill1",
                "item_beacon", 2, false);

            AddProduct("shop_amber_shard", ShopTab.Items, ShopCurrency.Fossil, 380,
                "精纯琥珀结晶 x2", "珍稀矿物 · 化石兑换",
                "消耗 380 恐龙化石即可购入 2 颗【精纯琥珀结晶】存入仓库，适合化石充裕的驯龙者。",
                "史诗", new Color(0.68f, 0.42f, 0.96f, 1f), new Color(0.32f, 0.80f, 1.0f, 1f), "water",
                "item_amber_shard", 2, false);

            AddProduct("shop_supply_box", ShopTab.Items, ShopCurrency.Amber, 20,
                "岛屿探索补给箱 x1", "琥珀甄选 · 惊喜礼盒",
                "花费 20 琥珀即可获得 1 个【岛屿探索补给箱】写入仓库，随时开启获取双份资源回馈。",
                "传说", new Color(0.95f, 0.68f, 0.20f, 1f), new Color(0.98f, 0.72f, 0.22f, 1f), "dino_skill2",
                "item_supply_box", 1, false);

            // 2. 恐龙 & 限定皮肤商店（购买后永久解锁并写入 InventoryManager 仓库，可前往背包穿戴出战）
            AddProduct("shop_dino_raptor", ShopTab.Dinos, ShopCurrency.Fossil, 480,
                "迅猛疾风掠食龙", "商店限定恐龙形态 · 解锁出战",
                "消耗 480 恐龙化石永久解锁该高速掠食者。购买成功后自动放入【恐龙背包 -> 恐龙】页签可一键切换出战。",
                "史诗", new Color(0.68f, 0.42f, 0.96f, 1f), new Color(0.24f, 0.86f, 0.64f, 1f), "dino_jump",
                "dino_raptor_swift", 1, true);

            AddProduct("shop_skin_emerald", ShopTab.Dinos, ShopCurrency.Fossil, 360,
                "翡翠晶鳞限定涂装", "商店限定皮肤 · 解锁穿戴",
                "消耗 360 恐龙化石永久解锁碧玉晶鳞皮肤。购买后可在【恐龙背包 -> 皮肤】页签直接穿戴并实时渲染大厅霸王龙。",
                "史诗", new Color(0.68f, 0.42f, 0.96f, 1f), new Color(0.18f, 0.86f, 0.50f, 1f), "dino_sneak",
                "skin_emerald", 1, true);

            AddProduct("shop_dino_obsidian", ShopTab.Dinos, ShopCurrency.Amber, 45,
                "黑曜炎狱霸王龙", "传说级霸主形态 · 琥珀专享",
                "消耗 45 远古琥珀解锁深渊黑曜重甲霸王龙，生命与咬击属性显著超越常规形态，写入背包后即可出战。",
                "传说", new Color(0.95f, 0.68f, 0.20f, 1f), new Color(0.68f, 0.44f, 0.98f, 1f), "dino_skill1",
                "dino_obsidian_rex", 1, true);

            AddProduct("shop_skin_neon", ShopTab.Dinos, ShopCurrency.Amber, 35,
                "霓虹雷暴战纹", "传说级幻彩皮肤 · 琥珀专享",
                "消耗 35 远古琥珀解锁紫电幻彩战纹涂装，购买后立即写入恐龙背包并可穿戴呈现炫彩霸王龙外观。",
                "传说", new Color(0.95f, 0.68f, 0.20f, 1f), new Color(0.62f, 0.52f, 1.0f, 1f), "dino_skill2",
                "skin_neon_storm", 1, true);

            // 3. 琥珀充值页（点击即加钱的测试充值按钮，同时提供恐龙化石测试补给）
            AddRecharge("recharge_amber_60", "+60 远古琥珀（测试充值）",
                "测试环境直充档位 · 免支付点击即到账",
                "点击右侧【立即充值】按钮直接向当前账号发放 60 颗远古琥珀并保存至本地。",
                "常用测试", new Color(0.24f, 0.66f, 0.98f, 1f), new Color(0.28f, 0.78f, 1f, 1f), "water",
                goldGain: 0, amberGain: 60);

            AddRecharge("recharge_amber_300", "+300 远古琥珀（测试大礼包）",
                "推荐测试档位 · 足够解锁商店传说恐龙与皮肤",
                "点击【立即充值】立即获得 300 远古琥珀，可快速测试商店购买、弹窗结算与背包闭环流程。",
                "热门测试", new Color(0.68f, 0.42f, 0.96f, 1f), new Color(0.32f, 0.84f, 1f, 1f), "water",
                goldGain: 0, amberGain: 300);

            AddRecharge("recharge_amber_980", "+980 远古琥珀（测试尊享包）",
                "超大额测试充值 · 点击瞬时累加",
                "点击直接到账 980 颗远古琥珀，并实时同步更新大厅顶部琥珀栏及本地存档。",
                "至尊测试", new Color(0.95f, 0.68f, 0.20f, 1f), new Color(0.46f, 0.88f, 1f, 1f), "dino_skill2",
                goldGain: 0, amberGain: 980);

            AddRecharge("recharge_fossil_600", "+600 恐龙化石（测试补给）",
                "辅助测试档位 · 点击即送恐龙化石",
                "点击立即发放 600 恐龙化石，方便在道具商店与恐龙商店体验化石扣款与库存写入。",
                "化石测试", new Color(0.95f, 0.68f, 0.20f, 1f), new Color(0.94f, 0.58f, 0.24f, 1f), "food",
                goldGain: 600, amberGain: 0);
        }

        private static void AddProduct(string id, ShopTab tab, ShopCurrency currency, int price,
            string title, string subtitle, string desc, string rarityLabel, Color rarityColor, Color accentColor,
            string iconKey, string targetInventoryId, int grantQuantity, bool isUniqueUnlock)
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
                grantQuantity = grantQuantity,
                isUniqueUnlock = isUniqueUnlock,
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
            if (product == null || !product.isUniqueUnlock) return false;
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
                    feedbackMessage = $"✓ 测试补给成功：+{product.rechargeGold} 恐龙化石已到账！";
                }
                return true;
            }

            if (IsOwned(product))
            {
                feedbackMessage = $"您已拥有【{product.title}】，可直接前往恐龙背包穿戴使用！";
                return false;
            }

            QuestRewardType rewardType = product.currency == ShopCurrency.Fossil ? QuestRewardType.Gold : QuestRewardType.Amber;
            if (!QuestManager.TrySpendCurrency(rewardType, product.price))
            {
                string unit = GetCurrencyName(product.currency);
                feedbackMessage = $"{unit}余额不足（需要 {product.price} {unit}），可先点击【琥珀充值】或完成任务获取！";
                return false;
            }

            if (product.isUniqueUnlock)
            {
                InventoryManager.UnlockSpecialItem(product.targetInventoryId);
            }
            else
            {
                InventoryManager.GrantItemQuantity(product.targetInventoryId, Mathf.Max(1, product.grantQuantity));
            }

            OnShopUpdated?.Invoke();
            feedbackMessage = $"✓ 购买成功：【{product.title}】已写入恐龙背包！";
            return true;
        }
    }
}
