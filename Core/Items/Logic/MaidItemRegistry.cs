using UnityEngine;
using FastModdingLib;
using ItemStatsSystem;
using Duckov.Utilities; // 可能需要引用
using CombatMaid.Core.Items.Data;

namespace CombatMaid.Core.Items.Logic
{
    public static class MaidItemRegistry
    {
        private const string MOD_ID = "CombatMaidMod";
        // 借用外观的物品ID (例如 451 是某个文件夹/文件模型，根据你的实际喜好修改)
        private const int REF_VISUAL_ID_CONTRACT = 451; 

        public static void Initialize(string modPath)
        {
            CMDebug.Log("[MaidItemRegistry] 开始注册物品...");

            var items = MaidItemDefs.GetDefinitions();
            foreach (var data in items)
            {
                RegisterSingleItem(modPath, data);
            }

            CMDebug.Log($"[MaidItemRegistry] 成功注册 {items.Count} 个物品。");
        }

        private static void RegisterSingleItem(string modPath, ItemData data)
        {
            // 1. FML 创建基础数据
            ItemUtils.CreateCustomItem(modPath, data, MOD_ID);

            // 2. 获取 Prefab 进行后处理
            var prefab = ItemAssetsCollection.GetPrefab(data.itemId);
            if (prefab == null) return;

            // 3. 视觉修正
            // 如果是契约，借用文件夹模型；如果是红茶，可能自带Sprite但没有3D模型，这里可以加判断
            if (data.itemId == MaidItemDefs.ID_MAID_CONTRACT)
            {
                MaidVisualHelper.CloneVisuals(prefab, REF_VISUAL_ID_CONTRACT);
            }

            // 4. 应用特殊逻辑 (Future Proofing)
            ApplyCustomLogic(prefab, data.itemId);

            // 5. 注入商店
            InjectToShop(data.itemId);
        }

        private static void ApplyCustomLogic(Item prefab, int id)
        {
            switch (id)
            {
                case MaidItemDefs.ID_MAID_CONTRACT:
                    // 写入女仆生成所需的自定义数据
                    // 这里对应旧代码的 MaidProfileKey
                    prefab.Constants.Add(new CustomData("MaidProfileKey", "Cname_Usec"));
                    prefab.Constants.Add(new CustomData("ConsumeOnUse", true));
                    break;
            }
        }

        private static void InjectToShop(int itemId)
        {
            // 注入到所有商人的示例，也可以指定 merchantProfileID
            ShopUtils.AddGoods(new ShopGoodsData
            {
                merchantProfileID = "Mud", // 假设是 Mud 商人
                typeID = itemId,
                maxStock = 5,
                priceFactor = 1.0f,
                forceUnlock = true,
                possibility = 1.0f
            });
        }

        public static void Cleanup()
        {
            ItemUtils.UnregisterAllItem(MOD_ID);
        }
    }
}