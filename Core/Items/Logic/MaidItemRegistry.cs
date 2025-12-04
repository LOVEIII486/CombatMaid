using UnityEngine;
using FastModdingLib;
using ItemStatsSystem;
using Duckov.Utilities;
using CombatMaid.Core.Items.Data;
using CombatMaid.Core.Items.Components;
using System.IO;     
using Duckov.ItemBuilders; 
using System.Collections.Generic;
using Duckov.ItemUsage; 
using CombatMaid.Localization;

namespace CombatMaid.Core.Items.Logic
{
    public static class MaidItemRegistry
    {
        private const string MOD_ID = "CombatMaidMod";

        // ==================== 初始化流程 ====================

        public static void Initialize(string modPath)
        {
            CMDebug.Log($"开始初始化物品系统... {modPath}");

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
                    CMDebug.LogError($"物品 {info.itemId}: {ex.Message}");
                }
            }

            CMDebug.Log($"初始化完成。成功注册 {successCount}/{items.Count} 个物品。");
        }

        public static void Cleanup()
        {
            try { ItemUtils.UnregisterAllItem(MOD_ID); } catch {}
        }

        public static void RefreshLocalizations()
        {
            CMDebug.Log("正在刷新物品本地化文本...");
            foreach (var info in MaidItemDefs.GetDefinitions())
            {
                RegisterLocalization(info);
            }
        }

        // ==================== 核心注册逻辑 ====================

        private static void RegisterSingleItemSafe(string modPath, MaidItemInfo info)
        {
            // 1. 本地化
            RegisterLocalization(info);

            // 2. 构建基础物品
            var itemComponent = BuildItemWithIcon(modPath, info);

            // 3. 设置基础属性并注册到 FML
            Object.DontDestroyOnLoad(itemComponent);
            ItemUtils.SetItemProperties(itemComponent, info);
            ItemUtils.RegisterItem(itemComponent, MOD_ID);

            // 4. 注册后的逻辑扩展
            var registeredPrefab = ItemAssetsCollection.GetPrefab(info.itemId);
            if (registeredPrefab != null)
            {
                ApplyExtendedLogic(registeredPrefab, info);
            }

            // 5. 商店注入
            TryInjectToShop(info);
        }

        // ==================== 内部功能模块 ====================

        /// <summary>
        /// 构建物品并处理图标
        /// </summary>
        private static Item BuildItemWithIcon(string modPath, MaidItemInfo info)
        {
            var builder = ItemBuilder.New()
                .TypeID(info.itemId)
                .EnableStacking(info.maxStackCount, 1);

            bool iconSet = false;

            // 加载自定义图标
            if (!string.IsNullOrEmpty(info.spritePath))
            {
                string modFolderName = Path.GetFileName(modPath);
                string dllPath = Path.Combine(modPath, modFolderName + ".dll");
                
                var sprite = ItemUtils.LoadEmbeddedSprite(dllPath, info.spritePath, info.itemId);
                
                if (sprite != null)
                {
                    builder.Icon(sprite);
                    iconSet = true;
                    CMDebug.LogWarning($"{info.itemId}: 自定义图标加载成功");
                }
                else
                {
                    CMDebug.LogWarning($"{info.itemId}: 图标文件未找到 -> {info.spritePath}");
                }
            }

            // 借用参考物品图标
            if (!iconSet && info.VisualReferenceId > 0)
            {
                var refItem = ItemAssetsCollection.GetPrefab(info.VisualReferenceId);
                if (refItem != null)
                {
                    builder.Icon(refItem.Icon);
                    iconSet = true;
                    CMDebug.LogWarning($"{info.itemId}: 已借用 ID {info.VisualReferenceId} 的图标");
                }
            }

            return builder.Instantiate();
        }

        /// <summary>
        /// 应用模组的高级逻辑
        /// </summary>
        private static void ApplyExtendedLogic(Item prefab, MaidItemInfo info)
        {
            // 1. 视觉克隆
            if (info.VisualReferenceId > 0)
            {
                MaidVisualHelper.CloneVisuals(prefab, info.VisualReferenceId);
            }

            // 2. 挂载通用使用行为
            if (info.usages != null)
            {
                if (prefab.UsageUtilities.behaviors == null) 
                    prefab.UsageUtilities.behaviors = new List<UsageBehavior>();
                if (prefab.GetComponent<SimpleUseBehavior>() == null)
                {
                    var behavior = prefab.gameObject.AddComponent<SimpleUseBehavior>();
                    prefab.UsageUtilities.behaviors.Add(behavior);
                }
            }

            // 3. 挂载自定义逻辑组件
            if (info.CustomComponentType != null)
            {
                if (typeof(MonoBehaviour).IsAssignableFrom(info.CustomComponentType))
                    prefab.gameObject.AddComponent(info.CustomComponentType);
                else
                    CMDebug.LogError($"{info.itemId} 的组件类型无效，必须继承 MonoBehaviour");
            }

            // 4. 注入常量
            if (info.CustomConstants != null)
            {
                foreach (var kvp in info.CustomConstants)
                {
                    AddConstantSafe(prefab, kvp.Key, kvp.Value);
                }
            }
        }

        /// <summary>
        /// 类型安全的常量添加
        /// </summary>
        private static void AddConstantSafe(Item prefab, string key, object value)
        {
            switch (value)
            {
                case bool b: prefab.Constants.Add(new CustomData(key, b)); break;
                case float f: prefab.Constants.Add(new CustomData(key, f)); break;
                case int i: prefab.Constants.Add(new CustomData(key, (float)i)); break;
                case string s: prefab.Constants.Add(new CustomData(key, s)); break;
                default:
                    CMDebug.LogWarning($"不支持的常量类型: Key={key}, Type={value?.GetType()}");
                    break;
            }
        }

        /// <summary>
        /// 注册本地化文本
        /// </summary>
        private static void RegisterLocalization(MaidItemInfo info)
        {
            var dict = SodaCraft.Localizations.LocalizationManager.overrideTexts;

            if (!string.IsNullOrEmpty(info.localizationKey))
                dict[info.localizationKey] = LocalizationManager.GetText(info.localizationKey, $"[{info.localizationKey}]");

            if (!string.IsNullOrEmpty(info.localizationDesc))
                dict[info.localizationDesc] = LocalizationManager.GetText(info.localizationDesc, $"[{info.localizationDesc}]");
        }
        

        private static void TryInjectToShop(MaidItemInfo info)
        {
            if (string.IsNullOrEmpty(info.ShopMerchantId)) return;

            ShopUtils.AddGoods(new ShopGoodsData
            {
                merchantProfileID = info.ShopMerchantId,
                typeID = info.itemId,
                maxStock = info.ShopMaxStock,          // 使用配置的库存
                priceFactor = info.ShopPriceFactor,    // 使用配置的价格倍率
                possibility = info.ShopPossibility,    // 使用配置的概率
                forceUnlock = info.ShopForceUnlock     // 使用配置的解锁状态
            });
            CMDebug.Log($"[商店] 已添加物品 {info.itemId} 到商人 {info.ShopMerchantId}");
        }
    }
}