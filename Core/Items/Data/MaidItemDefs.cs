using System.Collections.Generic;
using FastModdingLib;

namespace CombatMaid.Core.Items.Data
{
    public static class MaidItemDefs
    {
        public const int ID_MAID_CONTRACT = 88888;

        public static List<ItemData> GetDefinitions()
        {
            return new List<ItemData>
            {
                // === 女仆契约 ===
                new ItemData
                {
                    itemId = ID_MAID_CONTRACT,
                    localizationKey = "Item_MaidContract_Name",
                    localizationDesc = "Item_MaidContract_Desc",
                    value = 8888,        // 基础价格
                    maxStackCount = 1,   // 不可堆叠
                    weight = 0.1f,
                    tags = new List<string> { "General" },
                    
                    // 必须配置 usages，否则游戏里不会显示“使用”按钮
                    usages = new UsageData
                    {
                        useTime = 3.0f,
                        useSound = "Paper", // 纸张声音 (如果游戏有)
                        // 空行为列表，具体逻辑由组件接管
                        behaviors = new List<UsageBehaviorData>() 
                    }
                }
            };
        }
    }
}