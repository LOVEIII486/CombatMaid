using System.Collections.Generic;
using FastModdingLib;
using CombatMaid.Core.Items.Components;
using ItemStatsSystem;

namespace CombatMaid.Core.Items.Data
{
    public static class MaidItemDefs
    {
        public static List<MaidItemInfo> GetDefinitions()
        {
            return new List<MaidItemInfo>
            {
                // === 物品 1: 女仆契约 ===
                new MaidItemInfo
                {
                    itemId = 88888,
                    spritePath = "MaidContract_icon_512.png",
                    localizationKey = "Item_MaidContract",
                    localizationDesc = "Item_MaidContract_Desc",
                    value = 5000,
                    maxStackCount = 1,
                    weight = 0.01f,
                    tags = new List<string> { "Maid" },
                    usages = new UsageData
                    {
                        useTime = 1.0f,
                        // useSound = "Paper", 
                        behaviors = new List<UsageBehaviorData>() // 空行为，由 SimpleUseBehavior 接管
                    },
                    VisualReferenceId = 73, // 信件
                    CustomComponentType = typeof(Component_MaidContract),
                    CustomConstants = new Dictionary<string, object>
                    {
                        { "ConsumeOnUse", true }
                    },
                    ShopMerchantId = MerchantIds.Mud,
                    ShopMaxStock = 5,
                    ShopPriceFactor = 1.2f,
                    ShopPossibility = 1f,
                    ShopForceUnlock = true
                },
                new MaidItemInfo// 瓶中女仆·I型
                {
                    itemId = 88001,
                    localizationKey = "Item_VialMaid_I",
                    localizationDesc = "Item_VialMaid_I_Desc",
                    spritePath = "VialMaid_I.png",

                    value = 2000,
                    weight = 1f,
                    maxStackCount = 3,
                    maxDurability = 0f,

                    order = 0,
                    tags = new List<string> { "Maid", "Consumable" },

                    quality = 4,
                    displayQuality = DisplayQuality.Purple,

                    usages = new UsageData
                    {
                        useTime = 1f,
                        useSound = "",
                        actionSound = "",
                        useDurability = false,
                        durabilityUsage = 0,
                        behaviors = new List<UsageBehaviorData>()
                    },

                    VisualReferenceId = 429,

                    CustomComponentType = typeof(Component_MaidVial),

                    CustomConstants = new Dictionary<string, object>
                    {
                        { "MaidProfileID", "VialMaid_I" },
                        { "ConsumeOnUse", true },
                    },
                    ShopMerchantId = MerchantIds.Mud,
                    ShopMaxStock = 6,
                    ShopPriceFactor = 10f,
                    ShopPossibility = 1.0f,
                    ShopForceUnlock = true
                },
                new MaidItemInfo// 瓶中女仆·II型
                {
                    itemId = 88002,
                    localizationKey = "Item_VialMaid_II",
                    localizationDesc = "Item_VialMaid_II_Desc",
                    spritePath = "VialMaid_II.png",

                    value = 5000,
                    weight = 1.2f,
                    maxStackCount = 3,
                    maxDurability = 0f,

                    order = 1,
                    tags = new List<string> { "Maid", "Consumable" },

                    quality = 5,
                    displayQuality = DisplayQuality.Orange,

                    usages = new UsageData
                    {
                        useTime = 1f,
                        useSound = "",
                        actionSound = "",
                        useDurability = false,
                        durabilityUsage = 0,
                        behaviors = new List<UsageBehaviorData>()
                    },

                    VisualReferenceId = 429,

                    CustomComponentType = typeof(Component_MaidVial),

                    CustomConstants = new Dictionary<string, object>
                    {
                        { "MaidProfileID", "VialMaid_II" },
                        { "ConsumeOnUse", true },
                    },
                    ShopMerchantId = MerchantIds.Mud,
                    ShopMaxStock = 3,
                    ShopPriceFactor = 10f,
                    ShopPossibility = 1f,
                    ShopForceUnlock = true
                },
                new MaidItemInfo // 瓶中女仆·III型
                {
                    itemId = 88003,
                    localizationKey = "Item_VialMaid_III",
                    localizationDesc = "Item_VialMaid_III_Desc",
                    spritePath = "VialMaid_III.png",

                    value = 12000,
                    weight = 1.5f,
                    maxStackCount = 2,
                    maxDurability = 0f,

                    order = 2,
                    tags = new List<string> { "Maid", "Consumable" },

                    quality = 6,
                    displayQuality = DisplayQuality.Red,

                    usages = new UsageData
                    {
                        useTime = 1f,
                        useSound = "",
                        actionSound = "",
                        useDurability = false,
                        durabilityUsage = 0,
                        behaviors = new List<UsageBehaviorData>()
                    },

                    VisualReferenceId = 429,

                    CustomComponentType = typeof(Component_MaidVial),

                    CustomConstants = new Dictionary<string, object>
                    {
                        { "MaidProfileID", "VialMaid_III" },
                        { "ConsumeOnUse", true },
                    },
                    ShopMerchantId = MerchantIds.Mud,
                    ShopMaxStock = 1,
                    ShopPriceFactor = 10f,
                    ShopPossibility = 1f,
                    ShopForceUnlock = true
                }
            };
        }
    }
}


//
// // ==================== 新建物品填空模板 ====================
// new MaidItemInfo
// {
//     // --- [1. 必填核心] (来自 ItemData) ---
//     itemId = 0,                      // [必填] 唯一数字ID (不要重复)
//     localizationKey = "Item_Key",    // [必填] 名称文本 Key (对应 CSV)
//     localizationDesc = "Item_Desc",  // [必填] 描述文本 Key (对应 CSV)
//     spritePath = "Icon_Name.png",    // [必填] 图标文件名 (放入 assets/textures)
//
//     // --- [2. 基础数值] (来自 ItemData) ---
//     value = 100,                     // 基础价格
//     maxStackCount = 1,               // 最大堆叠数
//     weight = 0.1f,                   // 重量 (kg)
//     quality = 0,                     // 品质数值 (影响排序等)
//     displayQuality = DisplayQuality.None, // 稀有度边框: None, Common, Rare, Epic, Legendary
//
//     // --- [3. 分类与排序] (来自 ItemData) ---
//     tags = new List<string> { "General", "Maid" }, // 标签
//     order = 0,                       // 排序优先级 (越小越前)
//
//     // --- [4. 使用行为] (来自 ItemData) ---
//     // 不需要使用功能则删掉此段
//     usages = new UsageData
//     {
//         useTime = 1.0f,              // 使用读条时间 (秒)
//         useSound = "Paper",          // 音效 Key
//         // behaviors = ...           // 通常由模组脚本接管，这里留空
//     },
//
//     // ==========================================
//     // --- [5. 模组扩展设置] (MaidItemInfo) ---
//     // ==========================================
//
//     // --- 视觉借用 ---
//     VisualReferenceId = 0,           // 借用原版物品 ID (0 = 使用自己的 spritePath)
//
//     // --- 逻辑绑定 ---
//     CustomComponentType = null,      // 挂载脚本类型: typeof(Component_MaidVial)
//     CustomConstants = new Dictionary<string, object>
//     {
//         { "Key", "Value" }           // 自定义参数 (如 VialTier, MaidProfileID)
//     },
//
//     // --- 商店设置 ---
//     ShopMerchantId = MerchantIds.Mud,// 商人 ID (设为 null 不上架)
//     ShopMaxStock = 5,                // 每次刷新库存
//     ShopPriceFactor = 1.0f,          // 价格倍率
//     ShopPossibility = 1.0f,          // 出现概率 (0.0 - 1.0)
//     ShopForceUnlock = true           // 是否无视好感度强制解锁
// }
//     