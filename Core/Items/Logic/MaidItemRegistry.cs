using UnityEngine;
using FastModdingLib;
using ItemStatsSystem;
using Duckov.Utilities;
using CombatMaid.Core.Items.Data;
using CombatMaid.Core.Items.Components; // 引用组件
using System.IO;     
using Duckov.ItemBuilders; 
using Duckov.ItemUsage; // 引用 UsageBehavior

namespace CombatMaid.Core.Items.Logic
{
    public static class MaidItemRegistry
    {
        private const string MOD_ID = "CombatMaidMod";
        private const int REF_VISUAL_ID_CONTRACT = 73; // 借用信件外观

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
            // 1. 构建
            var builder = ItemBuilder.New()
                .TypeID(data.itemId)
                .EnableStacking(data.maxStackCount, 1);

            // 2. 图标 (安全回退逻辑)
            bool iconLoaded = false;
            // (此处保留之前的 LoadEmbeddedSprite 逻辑，如果契约直接用借用图标，这段其实不执行)
            // ...

            // 契约特殊处理：借用 ID 73 图标
            if (data.itemId == MaidItemDefs.ID_MAID_CONTRACT)
            {
                var refItem = ItemAssetsCollection.GetPrefab(REF_VISUAL_ID_CONTRACT);
                if (refItem != null) builder.Icon(refItem.Icon);
            }

            // 3. 注册
            Item component = builder.Instantiate();
            Object.DontDestroyOnLoad(component);
            ItemUtils.SetItemProperties(component, data);
            ItemUtils.RegisterItem(component, MOD_ID);

            // 4. 后处理
            var prefab = ItemAssetsCollection.GetPrefab(data.itemId);
            if (prefab == null) return;

            if (data.itemId == MaidItemDefs.ID_MAID_CONTRACT)
            {
                MaidVisualHelper.CloneVisuals(prefab, REF_VISUAL_ID_CONTRACT);
            }

            ApplyCustomLogic(prefab, data.itemId);
            InjectToShop(data.itemId, MerchantIds.Mud);
        }

        private static void ApplyCustomLogic(Item prefab, int id)
        {
            if (id == MaidItemDefs.ID_MAID_CONTRACT)
            {
                // 1. 挂载业务逻辑 (召唤女仆)
                prefab.gameObject.AddComponent<Component_MaidContract>();
                
                // 2. [关键修复] 挂载万能行为，激活“使用”按钮
                // 必须把 behaviors 列表转换一下才能添加
                var simpleBehavior = prefab.gameObject.AddComponent<SimpleUseBehavior>();
                
                // 检查列表是否为空（防止空引用）
                if (prefab.UsageUtilities.behaviors == null) 
                    prefab.UsageUtilities.behaviors = new System.Collections.Generic.List<UsageBehavior>();
                
                prefab.UsageUtilities.behaviors.Add(simpleBehavior);

                // 3. 写入消耗标记
                prefab.Constants.Add(new CustomData("ConsumeOnUse", true));
            }
        }

        private static void InjectToShop(int itemId, string merchantId)
        {
            ShopUtils.AddGoods(new ShopGoodsData
            {
                merchantProfileID = merchantId, 
                typeID = itemId,
                maxStock = 1,
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