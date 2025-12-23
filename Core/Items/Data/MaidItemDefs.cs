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
                new MaidItemInfo // 酒狐女仆契约 88000
                {
                    itemId = 88000,
                    spritePath = "MaidContract_WineFox.png",
                    localizationKey = "Item_MaidContractWineFox",
                    localizationDesc = "Item_MaidContractWineFox_Desc",
                    value = 10000,
                    maxStackCount = 1,
                    maxDurability = 100f,
                    quality = 6,
                    displayQuality = DisplayQuality.Red,
                    weight = 0.01f,
                    tags = new List<string> { },
                    usages = new UsageData
                    {
                        useTime = 1.0f,
                        useSound = "",
                        actionSound = "",
                        useDurability = true,
                        durabilityUsage = 0,
                        behaviors = new List<UsageBehaviorData>()
                    },
                    VisualReferenceId = 73, // 信件
                    CustomComponentType = typeof(Component_MaidContract_WineFox),
                    CustomConstants = new Dictionary<string, object>
                    {
                        { "ConsumeOnUse", false }
                    },
                    ShopMerchantId = MerchantIds.Mud,
                    ShopMaxStock = 2,
                    ShopPriceFactor = 1f,
                    ShopPossibility = 1f,
                    ShopForceUnlock = false
                },
                new MaidItemInfo // 瓶中女仆·I型 88001
                {
                    itemId = 88001,
                    localizationKey = "Item_VialMaid_I",
                    localizationDesc = "Item_VialMaid_I_Desc",
                    spritePath = "VialMaid_I.png",

                    value = 5000,
                    weight = 1f,
                    maxStackCount = 3,
                    maxDurability = 0f,

                    order = 0,
                    tags = new List<string> { },

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
                    ShopPriceFactor = 1f,
                    ShopPossibility = 1.0f,
                    ShopForceUnlock = false
                },
                new MaidItemInfo // 瓶中女仆·II型 88002
                {
                    itemId = 88002,
                    localizationKey = "Item_VialMaid_II",
                    localizationDesc = "Item_VialMaid_II_Desc",
                    spritePath = "VialMaid_II.png",

                    value = 10000,
                    weight = 1.2f,
                    maxStackCount = 2,
                    maxDurability = 0f,

                    order = 1,
                    tags = new List<string> { },

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
                    ShopMaxStock = 2,
                    ShopPriceFactor = 1f,
                    ShopPossibility = 1f,
                    ShopForceUnlock = false
                },
                new MaidItemInfo // 瓶中女仆·III型 88003
                {
                    itemId = 88003,
                    localizationKey = "Item_VialMaid_III",
                    localizationDesc = "Item_VialMaid_III_Desc",
                    spritePath = "VialMaid_III.png",

                    value = 20000,
                    weight = 1.5f,
                    maxStackCount = 1,
                    maxDurability = 0f,

                    order = 2,
                    tags = new List<string> { },

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
                    ShopPriceFactor = 1f,
                    ShopPossibility = 1f,
                    ShopForceUnlock = false
                },
                new MaidItemInfo // 回复药 88101
                {
                    itemId = 88101,
                    spritePath = "RecoveryPotion.png",
                    localizationKey = "Item_RecoveryPotion",
                    localizationDesc = "Item_RecoveryPotion_Desc",
                    value = 2000,
                    weight = 0.5f,
                    maxStackCount = 1,
                    maxDurability = 6f,
                    quality = 4,
                    displayQuality = DisplayQuality.Purple,
                    tags = new List<string> { "Medic", "Healing"},
                    usages = new UsageData
                    {
                        useTime = 1.0f,
                        useSound = string.Empty,
                        actionSound = string.Empty,
                        useDurability = true,
                        durabilityUsage = 1,
                        behaviors = new List<UsageBehaviorData>()
                    },
                    VisualReferenceId = 15,
                    CustomComponentType = typeof(Component_RecoveryPotion),
                    CustomConstants = new Dictionary<string, object>
                    {
                        { "ConsumeOnUse", false }
                    },
                    ShopMerchantId = MerchantIds.Fo,
                    ShopMaxStock = 6,
                    ShopPriceFactor = 1.0f,
                    ShopPossibility = 1.0f,
                    ShopForceUnlock = false
                },
                new MaidItemInfo // 女仆召回铃铛 88102
                {
                    itemId = 88102,
                    localizationKey = "Item_MaidRecallBell",
                    localizationDesc = "Item_MaidRecallBell_Desc",
                    spritePath = "MaidRecallBell.png",
                    value = 1000,
                    weight = 0.3f,
                    maxStackCount = 1,
                    maxDurability = 100f,
                    tags = new List<string> { "Tool" },
                    quality = 4,
                    displayQuality = DisplayQuality.Purple,
                    usages = new UsageData
                    {
                        useTime = 1.0f,
                        useSound = "",
                        actionSound = "",
                        useDurability = false,
                        durabilityUsage = 0,
                        behaviors = new List<UsageBehaviorData>()
                    },
                    VisualReferenceId = 1266, 
                    CustomComponentType = typeof(Component_MaidRecallBell),
                    CustomConstants = new Dictionary<string, object>
                    {
                        { "ConsumeOnUse", false }
                    },
                    ShopMerchantId = MerchantIds.Mud,
                    ShopMaxStock = 1,
                    ShopPriceFactor = 1.0f,
                    ShopPossibility = 1.0f,
                    ShopForceUnlock = true
                },
                new MaidItemInfo // 魔力抛光剂 88103
                {
                    itemId = 88103,
                    spritePath = "ArmorRepairPotion.png",
                    localizationKey = "Item_ArmorRepairPotion",
                    localizationDesc = "Item_ArmorRepairPotion_Desc",
                    value = 3000,
                    weight = 0.5f,
                    maxStackCount = 1,
                    maxDurability = 4f,
                    quality = 4,
                    displayQuality = DisplayQuality.Purple,
                    tags = new List<string> { "Tool" },
                    usages = new UsageData
                    {
                        useTime = 2.0f,
                        useSound = "",
                        actionSound = "",
                        useDurability = true,
                        durabilityUsage = 1,
                        behaviors = new List<UsageBehaviorData>()
                    },
                    VisualReferenceId = 30, 
                    CustomComponentType = typeof(Component_ArmorRepairPotion),
                    CustomConstants = new Dictionary<string, object>
                    {
                        { "ConsumeOnUse", false }
                    },
                    ShopMerchantId = MerchantIds.Fo,
                    ShopMaxStock = 5,
                    ShopPriceFactor = 1.0f,
                    ShopPossibility = 1.0f,
                    ShopForceUnlock = false
                },
                new MaidItemInfo  // 体检套装 88104
                {
                    itemId = 88104,
                    localizationKey = "Item_MaidScanner", 
                    localizationDesc = "Item_MaidScanner_Desc",
                    spritePath = "MaidScanner.png",
                    value = 200,
                    weight = 0.1f,
                    maxStackCount = 1,
                    maxDurability = 10f,
                    quality = 3,
                    displayQuality = DisplayQuality.Blue,
                    usages = new UsageData
                    {
                        useTime = 0.3f,
                        useSound = "",
                        actionSound = "",
                        useDurability = true,
                        durabilityUsage = 1,
                        behaviors = new List<UsageBehaviorData>()
                    },
                    VisualReferenceId = 73,
                    CustomComponentType = typeof(Component_MaidStatusChecker),
                    CustomConstants = new Dictionary<string, object>
                    {
                        { "ConsumeOnUse", false }
                    },
                    ShopMerchantId = MerchantIds.Fo,
                    ShopMaxStock = 10,
                    ShopPriceFactor = 1.0f,
                    ShopPossibility = 1.0f,
                    ShopForceUnlock = true
                },
                new MaidItemInfo // 酒狐的心意曲奇 88105
                {
                    itemId = 88105,
                    spritePath = "WineFoxCookie.png",
                    localizationKey = "Item_WineFoxCookie",
                    localizationDesc = "Item_WineFoxCookie_Desc",
                    value = 520,
                    weight = 0.1f,
                    maxStackCount = 1,
                    maxDurability = 6f,
                    quality = 4,
                    displayQuality = DisplayQuality.Purple,
                    tags = new List<string> { "Food" },
                    usages = new UsageData
                    {
                        useTime = 0.6f,
                        useSound = string.Empty,
                        actionSound = string.Empty,
                        useDurability = true,
                        durabilityUsage = 1,
                        behaviors = new List<UsageBehaviorData>()
                    }, 
                    VisualReferenceId = 15,
                    CustomComponentType = typeof(Component_WineFoxCookie),
                    CustomConstants = new Dictionary<string, object>
                    {
                        { "ConsumeOnUse", false }
                    },
                    ShopMerchantId = null,
                    ShopForceUnlock = true
                },
            };
        }
    }
}