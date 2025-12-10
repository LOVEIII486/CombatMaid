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
                new MaidItemInfo // 酒狐女仆契约
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
                    ShopPriceFactor = 1.2f,
                    ShopPossibility = 1f,
                    ShopForceUnlock = false
                },
                new MaidItemInfo // 瓶中女仆·I型
                {
                    itemId = 88001,
                    localizationKey = "Item_VialMaid_I",
                    localizationDesc = "Item_VialMaid_I_Desc",
                    spritePath = "VialMaid_I.png",

                    value = 3000,
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
                    ShopPriceFactor = 10f,
                    ShopPossibility = 1.0f,
                    ShopForceUnlock = false
                },
                new MaidItemInfo // 瓶中女仆·II型
                {
                    itemId = 88002,
                    localizationKey = "Item_VialMaid_II",
                    localizationDesc = "Item_VialMaid_II_Desc",
                    spritePath = "VialMaid_II.png",

                    value = 6000,
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
                    ShopPriceFactor = 10f,
                    ShopPossibility = 1f,
                    ShopForceUnlock = false
                },
                new MaidItemInfo // 瓶中女仆·III型
                {
                    itemId = 88003,
                    localizationKey = "Item_VialMaid_III",
                    localizationDesc = "Item_VialMaid_III_Desc",
                    spritePath = "VialMaid_III.png",

                    value = 10000,
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
                    ShopPriceFactor = 10f,
                    ShopPossibility = 1f,
                    ShopForceUnlock = false
                },
                new MaidItemInfo
                {
                    itemId = 88101,
                    spritePath = "RecoveryPotion.png",
                    localizationKey = "Item_RecoveryPotion",
                    localizationDesc = "Item_RecoveryPotion_Desc",
                    value = 2500,
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
                        actionSound = "SFX/Item/use_drink",
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
                    ShopMaxStock = 5,
                    ShopPriceFactor = 2.0f,
                    ShopPossibility = 1.0f,
                    ShopForceUnlock = false
                }
            };
        }
    }
}