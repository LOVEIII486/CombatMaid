using System.Collections.Generic;
using FastModdingLib;
using CombatMaid.Core.Items.Components; // 引用组件

namespace CombatMaid.Core.Items.Data
{
    public static class MaidItemDefs
    {
        public const int ID_MAID_CONTRACT = 88888;

        // 返回类型改为 List<MaidItemInfo>
        public static List<MaidItemInfo> GetDefinitions()
        {
            return new List<MaidItemInfo>
            {
                // === 物品 1: 女仆契约 ===
                new MaidItemInfo
                {
                    // [基础 ItemData 属性]
                    itemId = ID_MAID_CONTRACT,
                    spritePath = "MaidContract_icon_512.png",
                    localizationKey = "Item_MaidContract_Name",
                    localizationDesc = "Item_MaidContract_Desc",
                    value = 8888,
                    maxStackCount = 1,
                    weight = 0.1f,
                    tags = new List<string> { "General" },
                    
                    usages = new UsageData
                    {
                        useTime = 3.0f,
                        useSound = "Paper", 
                        behaviors = new List<UsageBehaviorData>() // 空行为，由 SimpleUseBehavior 接管
                    },

                    // [新增 模组扩展属性]
                    // 1. 指定外观借用 ID (73 = 信件)
                    VisualReferenceId = 73,
                    
                    // 2. 指定出售商人 (神秘商人)
                    ShopMerchantId = MerchantIds.Myst,
                    
                    // 3. 指定逻辑脚本 (挂载召唤逻辑)
                    CustomComponentType = typeof(Component_MaidContract),
                    
                    // 4. 指定额外参数
                    CustomConstants = new Dictionary<string, object>
                    {
                        { "ConsumeOnUse", true }
                    }
                }
                
                // 未来添加新物品直接在这里 new 一个 MaidItemInfo 即可
                // 比如: new MaidItemInfo { itemId = 88999, VisualReferenceId = 123, ... }
            };
        }
    }
}