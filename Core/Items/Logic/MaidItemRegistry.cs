using UnityEngine;
using FastModdingLib;
using ItemStatsSystem;
using Duckov.Utilities;
using CombatMaid.Core.Items.Data;
using CombatMaid.Core.Items.Components;
using System.IO;     
using Duckov.ItemBuilders; 
using System.Collections.Generic;
using CombatMaid.Localization;

namespace CombatMaid.Core.Items.Logic
{
    public static class MaidItemRegistry
    {
        private const string MOD_ID = "CombatMaidMod";

        public static void Initialize(string modPath)
        {
            var items = MaidItemDefs.GetDefinitions();
            int successCount = 0;

            foreach (var info in items)
            {
                try
                {
                    RegisterItem(modPath, info);
                    successCount++;
                }
                catch (System.Exception ex)
                {
                    CMDebug.LogError($"[注册异常] 物品 {info.itemId}: {ex.Message}");
                }
            }

            CMDebug.LogInfo($"战斗女仆物品初始化完成！成功注册 {successCount}/{items.Count} 个物品。");
            // CombatMaid.DebugTools.TagPrinter.PrintAllTags();
        }

        public static void Cleanup()
        {
            ItemUtils.UnregisterAllItem(MOD_ID);
        }

        public static void RefreshLocalizations()
        {
            CMDebug.Log("正在刷新战斗女仆物品本地化文本...");
            foreach (var info in MaidItemDefs.GetDefinitions())
            {
                RegisterLocalization(info);
            }
        }

        #region 注册物品并添加到商店
        
        private static void RegisterItem(string modPath, MaidItemInfo info)
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
            InjectToShop(info);
        }

        /// <summary>
        /// 将物品注入商店
        /// </summary>
        private static void InjectToShop(MaidItemInfo info)
        {
            if (string.IsNullOrEmpty(info.ShopMerchantId)) return;

            // 检查该物品是否需要技能树前置
            string requiredNodeID = GetRequiredSkillNode(info.itemId);
            bool hasRequirement = !string.IsNullOrEmpty(requiredNodeID);
            bool finalUnlockState = !hasRequirement;

            ShopUtils.AddGoods(new ShopGoodsData
            {
                merchantProfileID = info.ShopMerchantId,
                typeID = info.itemId,
                maxStock = info.ShopMaxStock,
                priceFactor = info.ShopPriceFactor,
                possibility = info.ShopPossibility,
                forceUnlock = finalUnlockState 
            });

            string statusLog = finalUnlockState ? "默认解锁" : $"初始锁定 (等待技能 {requiredNodeID} 解锁)";
            CMDebug.Log($"战斗女仆商人物品注册 {info.itemId} -> {statusLog}");
        }
        
        #endregion
        
        #region 内部注册物品函数
        
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
                }
            }

            return builder.Instantiate();
        }

        private static void ApplyExtendedLogic(Item prefab, MaidItemInfo info)
        {
            // 1. 视觉克隆
            if (info.VisualReferenceId > 0)
            {
                MaidVisualHelper.CloneVisuals(prefab, info.VisualReferenceId);
            }

            // 确保行为列表初始化
            if (prefab.UsageUtilities.behaviors == null) 
                prefab.UsageUtilities.behaviors = new List<UsageBehavior>();

            bool hasCustomUsageBehavior = false;

            // 2. 挂载自定义逻辑组件
            if (info.CustomComponentType != null)
            {
                var component = prefab.gameObject.AddComponent(info.CustomComponentType);
                if (component is UsageBehavior customBehavior)
                {
                    // 如果是UsageBehavior必须加入到底层列表
                    prefab.UsageUtilities.behaviors.Add(customBehavior);
                    hasCustomUsageBehavior = true;
                    // CMDebug.LogInfo($"[{info.itemId}] 挂载了原生行为: {info.CustomComponentType.Name}");
                }
                else if (component is MonoBehaviour)
                {
                    // 如果是 MonoBehaviour，什么都不用做
                }
                else
                {
                    CMDebug.LogError($"{info.itemId} 的组件类型无效");
                }
            }

            // 3. 挂载通用使用行为 (SimpleUseBehavior)
            if (info.usages != null && !hasCustomUsageBehavior)
            {
                // 避免重复挂载
                if (prefab.GetComponent<SimpleUseBehavior>() == null)
                {
                    var behavior = prefab.gameObject.AddComponent<SimpleUseBehavior>();
                    prefab.UsageUtilities.behaviors.Add(behavior);
                }
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

        private static void RegisterLocalization(MaidItemInfo info)
        {
            var dict = SodaCraft.Localizations.LocalizationManager.overrideTexts;

            if (!string.IsNullOrEmpty(info.localizationKey))
                dict[info.localizationKey] = LocalizationManager.GetText(info.localizationKey, $"[{info.localizationKey}]");

            if (!string.IsNullOrEmpty(info.localizationDesc))
                dict[info.localizationDesc] = LocalizationManager.GetText(info.localizationDesc, $"[{info.localizationDesc}]");
        }
        
        #endregion

        #region 技能树映射，解锁配置也在这里！
        /// <summary>
        /// 获取指定技能节点ID解锁的所有物品ID列表
        /// 供 SkillTreeBuilder 使用，用于给节点挂载 PerkUnlockStockShop 组件
        /// </summary>
        public static List<int> GetItemsUnlockedByNode(string nodeId)
        {
            var list = new List<int>();
            foreach (var item in MaidItemDefs.GetDefinitions())
            {
                if (GetRequiredSkillNode(item.itemId) == nodeId)
                {
                    list.Add(item.itemId);
                }
            }
            return list;
        }

        /// <summary>
        /// 配置表
        /// </summary>
        private static string GetRequiredSkillNode(int itemId)
        {
            switch (itemId)
            {
                // 契约
                case 88000: // 酒狐契约
                    return "maid_core_license";

                // 瓶中女仆
                case 88001: return "maid_special_bottle_1"; // Lv1
                case 88002: return "maid_special_bottle_2"; // Lv2
                case 88003: return "maid_special_bottle_3"; // Lv3
                
                // 女仆物品
                case 88101: return "maid_skill_selfheal"; // 治疗药
                case 88103: return "maid_skill_repair";
                
                case 88108: 
                    return "maid_special_recycle_bottle"; // 空白契约瓶

                default: 
                    return null;
            }
        }
        
        #endregion
    }
}