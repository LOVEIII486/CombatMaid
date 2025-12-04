using UnityEngine;
using FastModdingLib;
using ItemStatsSystem;
using Duckov.Utilities;
using CombatMaid.Core.Items.Data;
using CombatMaid.Core.Items.Components; // 引用组件命名空间
using System.IO;     
using Duckov.ItemBuilders; 

namespace CombatMaid.Core.Items.Logic
{
    public static class MaidItemRegistry
    {
        private const string MOD_ID = "CombatMaidMod";
        
        // 修改：借用 ID 73 (信件/文件) 的外观
        private const int REF_VISUAL_ID_CONTRACT = 73; 

        public static void Initialize(string modPath)
        {
            CMDebug.Log("[MaidItemRegistry] 开始初始化物品系统...");
            var items = MaidItemDefs.GetDefinitions();

            foreach (var data in items)
            {
                try
                {
                    RegisterSingleItemSafe(modPath, data);
                }
                catch (System.Exception ex)
                {
                    CMDebug.LogError($"[注册失败] 物品 {data.itemId}: {ex.Message}");
                }
            }
            CMDebug.Log($"[MaidItemRegistry] 初始化完成。");
        }

        private static void RegisterSingleItemSafe(string modPath, ItemData data)
        {
            // 1. 构建基础物品
            var builder = ItemBuilder.New()
                .TypeID(data.itemId)
                .EnableStacking(data.maxStackCount, 1);

            // 2. 图标处理：如果是契约，直接借用 ID 73 的图标
            if (data.itemId == MaidItemDefs.ID_MAID_CONTRACT)
            {
                var refItem = ItemAssetsCollection.GetPrefab(REF_VISUAL_ID_CONTRACT);
                if (refItem != null) builder.Icon(refItem.Icon);
            }
            else 
            {
                // 其他物品保留原来的加载逻辑 (如果有的话)
                // ... (LoadEmbeddedSprite 逻辑可以保留以防万一)
            }

            // 3. 实例化与注册
            Item component = builder.Instantiate();
            Object.DontDestroyOnLoad(component);
            ItemUtils.SetItemProperties(component, data);
            ItemUtils.RegisterItem(component, MOD_ID);

            // 4. 后处理
            var prefab = ItemAssetsCollection.GetPrefab(data.itemId);
            if (prefab == null) return;

            // 4a. 视觉克隆 (解决丢弃后的 3D 模型显示)
            if (data.itemId == MaidItemDefs.ID_MAID_CONTRACT)
            {
                MaidVisualHelper.CloneVisuals(prefab, REF_VISUAL_ID_CONTRACT);
            }

            // 4b. 挂载功能组件
            ApplyCustomLogic(prefab, data.itemId);

            // 4c. 注入商店 (神秘商人)
            InjectToShop(data.itemId, MerchantIds.Myst);
        }

        private static void ApplyCustomLogic(Item prefab, int id)
        {
            if (id == MaidItemDefs.ID_MAID_CONTRACT)
            {
                // 挂载我们写的召唤脚本
                prefab.gameObject.AddComponent<Component_MaidContract>();
                
                // 保险起见，写入消耗标记 (虽然组件里已经写了 DestroyTree)
                prefab.Constants.Add(new CustomData("ConsumeOnUse", true));
            }
        }

        private static void InjectToShop(int itemId, string merchantId)
        {
            ShopUtils.AddGoods(new ShopGoodsData
            {
                merchantProfileID = merchantId, 
                typeID = itemId,
                maxStock = 1,       // 契约比较珍贵，库存设为 1
                priceFactor = 1.0f,
                forceUnlock = true,
                possibility = 1.0f
            });
        }

        public static void Cleanup()
        {
            try { ItemUtils.UnregisterAllItem(MOD_ID); } catch {}
        }
    }
}