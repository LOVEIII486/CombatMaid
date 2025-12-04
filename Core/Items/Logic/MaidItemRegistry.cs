using UnityEngine;
using FastModdingLib;
// using FastModdingLib.Shop; // [删除] 这是一个错误的引用，ShopUtils 直接在 FastModdingLib 下
using ItemStatsSystem;
using Duckov.Utilities;
using CombatMaid.Core.Items.Data;
using CombatMaid.Core.Items.Components;
using System.IO;     
using Duckov.ItemBuilders; 
using System.Collections.Generic;
using Duckov.ItemUsage; 

namespace CombatMaid.Core.Items.Logic
{
    public static class MaidItemRegistry
    {
        private const string MOD_ID = "CombatMaidMod";
        private const int FALLBACK_ICON_ID = 254; 

        public static void Initialize(string modPath)
        {
            CMDebug.Log("[MaidItemRegistry] 开始初始化物品系统..." + modPath);

            var items = MaidItemDefs.GetDefinitions();
            int successCount = 0;

            foreach (var info in items)
            {
                try
                {
                    RegisterSingleItemSafe(modPath, info);
                    successCount++;
                }
                catch (System.Exception ex)
                {
                    CMDebug.LogError($"[注册失败] 物品 {info.itemId}: {ex.Message}");
                }
            }

            CMDebug.Log($"[MaidItemRegistry] 初始化完成。成功注册 {successCount}/{items.Count} 个物品。");
        }

        private static void RegisterSingleItemSafe(string modPath, MaidItemInfo info)
        {
            // 1. 构建物品
            var builder = ItemBuilder.New()
                .TypeID(info.itemId)
                .EnableStacking(info.maxStackCount, 1);

            // 2. 图标处理
            bool iconLoaded = false;
            
            CMDebug.LogWarning($"[Icon-L] 物品 {info.itemId}: 尝试加载自定义图标: {info.spritePath}");

            if (!string.IsNullOrEmpty(info.spritePath))
            {
                // [关键修复] 路径欺骗的升级版
                // 强制将 modPath 转换为 DLL 路径格式，以确保 Path.GetDirectoryName() 返回正确的文件夹。
                // 假设 Mod 的 DLL 名就是 Mod 文件夹名 + ".dll"
                string modFolderName = Path.GetFileName(modPath); // 获取 "CombatMaid"
                string pathForFML = Path.Combine(modPath, modFolderName + ".dll");
                
                // 调用 FastModdingLib 的加载函数，使用欺骗路径
                var sprite = ItemUtils.LoadEmbeddedSprite(pathForFML, info.spritePath, info.itemId);
                
                if (sprite != null)
                {
                    builder.Icon(sprite);
                    iconLoaded = true;
                    CMDebug.LogWarning($"[Icon-L] 物品 {info.itemId}: 成功加载自定义图标！");
                }
                else
                {
                    // 打印正确的期望路径
                    string expectedPath = Path.Combine(modPath, "assets/textures/", info.spritePath);
                    CMDebug.LogWarning($"[Icon-F] 物品 {info.itemId}: 文件加载失败。请检查文件是否存在于: {expectedPath}");
                }
            }

            // B. 借用 VisualReferenceId 的图标 (如果文件加载失败)
            if (!iconLoaded && info.VisualReferenceId > 0)
            {
                var refItem = ItemAssetsCollection.GetPrefab(info.VisualReferenceId);
                if (refItem != null)
                {
                    builder.Icon(refItem.Icon);
                    iconLoaded = true;
                    // [新增调试日志] 记录借用事件
                    CMDebug.LogWarning($"[Icon-F] 物品 {info.itemId}: 启动回退机制，借用 ID {info.VisualReferenceId} 图标。");
                }
            }

            if (!iconLoaded)
            {
                CMDebug.LogWarning($"[资源缺失] 物品 {info.itemId} 无图标，使用默认保底。");
                var fallbackItem = ItemAssetsCollection.GetPrefab(FALLBACK_ICON_ID);
                if (fallbackItem != null) builder.Icon(fallbackItem.Icon);
            }

            // 3. 注册
            Item component = builder.Instantiate();
            Object.DontDestroyOnLoad(component);
            ItemUtils.SetItemProperties(component, info); 
            ItemUtils.RegisterItem(component, MOD_ID);

            // 4. 后处理
            var prefab = ItemAssetsCollection.GetPrefab(info.itemId);
            if (prefab == null) return;

            // 4a. 视觉克隆
            if (info.VisualReferenceId > 0)
            {
                MaidVisualHelper.CloneVisuals(prefab, info.VisualReferenceId);
            }

            // 4b. 挂载万能行为 (修正类型引用错误)
            if (info.usages != null)
            {
                var simpleBehavior = prefab.gameObject.AddComponent<SimpleUseBehavior>();
                if (prefab.UsageUtilities.behaviors == null) 
                    // [修正] 这里不需要 Duckov.ItemUsage 前缀，或者应该是 ItemStatsSystem.UsageBehavior
                    // 由于开头引用了 ItemStatsSystem，直接用 UsageBehavior 即可
                    prefab.UsageUtilities.behaviors = new List<UsageBehavior>();
                
                prefab.UsageUtilities.behaviors.Add(simpleBehavior);
            }

            // 4c. 挂载自定义脚本
            if (info.CustomComponentType != null)
            {
                if (typeof(MonoBehaviour).IsAssignableFrom(info.CustomComponentType))
                {
                    prefab.gameObject.AddComponent(info.CustomComponentType);
                }
                else
                {
                    CMDebug.LogError($"物品 {info.itemId} 的 CustomComponentType 必须继承自 MonoBehaviour");
                }
            }

            // 4d. 写入自定义参数 (修正 object 转 float 错误)
            if (info.CustomConstants != null)
            {
                foreach (var kvp in info.CustomConstants)
                {
                    // [修正] 显式类型检查和转换，因为 CustomData 构造函数不支持 object
                    if (kvp.Value is bool bVal)
                    {
                        prefab.Constants.Add(new CustomData(kvp.Key, bVal));
                    }
                    else if (kvp.Value is float fVal)
                    {
                        prefab.Constants.Add(new CustomData(kvp.Key, fVal));
                    }
                    else if (kvp.Value is int iVal)
                    {
                        // int 自动转 float
                        prefab.Constants.Add(new CustomData(kvp.Key, (float)iVal));
                    }
                    else if (kvp.Value is string sVal)
                    {
                        prefab.Constants.Add(new CustomData(kvp.Key, sVal));
                    }
                    else
                    {
                        CMDebug.LogWarning($"[警告] 物品 {info.itemId} 的常量 {kvp.Key} 类型不支持: {kvp.Value?.GetType()}");
                    }
                }
            }

            // 4e. 注入商店
            if (!string.IsNullOrEmpty(info.ShopMerchantId))
            {
                InjectToShop(info.itemId, info.ShopMerchantId);
            }
        }

        private static void InjectToShop(int itemId, string merchantId)
        {
            ShopUtils.AddGoods(new ShopGoodsData
            {
                merchantProfileID = merchantId, 
                typeID = itemId,
                maxStock = 5,
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