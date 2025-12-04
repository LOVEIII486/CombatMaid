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
                new ItemData
                {
                    itemId = ID_MAID_CONTRACT,
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
                        // [修改] 清空 behaviors，我们将在代码里手动挂载上面的 SimpleUseBehavior
                        behaviors = new List<UsageBehaviorData>() 
                    }
                }
            };
        }
    }
}