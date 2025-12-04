using System.Collections.Generic;
using FastModdingLib;
using CombatMaid.Core.Items.Components; 

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
                }
            };
        }
    }
}