using System.Collections.Generic;
using FastModdingLib;

namespace CombatMaid.Core.Items.Data
{
    public static class MaidItemDefs
    {
        // 统一管理 ID 常量，防止魔法数字
        public const int ID_MAID_CONTRACT = 88888;
        public const int ID_MAID_TEA = 88902;

        public static List<ItemData> GetDefinitions()
        {
            return new List<ItemData>
            {
                // === 物品 1: 女仆契约 ===
                new ItemData
                {
                    itemId = ID_MAID_CONTRACT,
                    localizationKey = "Item_MaidContract_Name", // 确保CSV里有这个Key
                    localizationDesc = "Item_MaidContract_Desc",
                    value = 5000,
                    maxStackCount = 10,
                    weight = 0.5f,
                    tags = new List<string> { "General" },
                    
                    usages = new UsageData
                    {
                        useTime = 2.0f,
                        useSound = "Eat", 
                        behaviors = new List<UsageBehaviorData>
                        {
                            // 可以在这里添加简单的 Buff，复杂逻辑由 Registry 处理
                        }
                    }
                },

                // === 物品 2: 女仆红茶 ===
                new ItemData
                {
                    itemId = ID_MAID_TEA,
                    localizationKey = "Maid_Food_Tea",
                    localizationDesc = "Maid_Food_Tea_Desc",
                    value = 80,
                    maxStackCount = 5,
                    weight = 0.2f,
                    spritePath = "icon_maid_tea.png", // 需放在 assets/textures/
                    tags = new List<string> { "Food" },
                    
                    usages = new UsageData
                    {
                        useTime = 3.0f,
                        useSound = "Drink",
                        behaviors = new List<UsageBehaviorData>
                        {
                            new FoodData { energyValue = 15f, waterValue = 20f }
                        }
                    }
                }
            };
        }
    }
}