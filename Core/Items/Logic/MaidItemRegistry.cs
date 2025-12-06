using UnityEngine;
using FastModdingLib;
using ItemStatsSystem;
using Duckov.Utilities;
using CombatMaid.Core.Items.Data;
using CombatMaid.Core.Items.Components;
using System.IO;     
using Duckov.ItemBuilders; 
using System.Collections.Generic;
using CombatMaid.Core.SkillTreeSystem;
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
            CMDebug.Log($"[MaidItemRegistry] 开始初始化物品系统... {modPath}");

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
                    CMDebug.LogError($"[注册异常] 物品 {info.itemId}: {ex.Message}");
                }
            }

            CMDebug.Log($"[MaidItemRegistry] 初始化完成。成功注册 {successCount}/{items.Count} 个物品。");
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

            // 5. 商店注入 (关键修改：直接注入，由参数控制锁定状态)
            InjectToShopWithLockState(info);
        }

        /// <summary>
        /// 将物品注入商店，并根据技能树要求决定初始锁定状态
        /// </summary>
        private static void InjectToShopWithLockState(MaidItemInfo info)
        {
            if (string.IsNullOrEmpty(info.ShopMerchantId)) return;

            // 检查该物品是否需要技能树前置
            string requiredNodeID = GetRequiredSkillNode(info.itemId);
            bool hasRequirement = !string.IsNullOrEmpty(requiredNodeID);

            // 逻辑核心：
            // - 无前置要求 -> forceUnlock = true (默认解锁，直接可买)
            // - 有前置要求 -> forceUnlock = false (初始锁定，不可见，等待 EconomyManager.Unlock 解锁)
            // 注意：这里覆盖了 MaidItemInfo 中配置的 ShopForceUnlock 默认值
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
            // CMDebug.Log($"[商店] 注册 {info.itemId} -> {statusLog}");
        }

        // ==================== 内部功能模块 ====================

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
                    CMDebug.LogWarning($"[MaidItemRegistry] {info.itemId}: 图标文件未找到 -> {info.spritePath}");
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

            // 2. 挂载通用使用行为
            if (info.usages != null)
            {
                if (prefab.UsageUtilities.behaviors == null) 
                    prefab.UsageUtilities.behaviors = new List<UsageBehavior>();
                
                // 避免重复挂载
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
                    CMDebug.LogError($"[MaidItemRegistry] {info.itemId} 的组件类型无效，必须继承 MonoBehaviour");
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
                    CMDebug.LogWarning($"[MaidItemRegistry] 不支持的常量类型: Key={key}, Type={value?.GetType()}");
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

        // ==================== 技能树映射逻辑 ====================

        /// <summary>
        /// 获取指定技能节点ID解锁的所有物品ID列表
        /// 供 SkillTreeBuilder 使用，用于给节点挂载 PerkUnlockStockShop 组件
        /// </summary>
        public static List<int> GetItemsUnlockedByNode(string nodeId)
        {
            var list = new List<int>();
            foreach (var item in MaidItemDefs.GetDefinitions())
            {
                // 如果该物品的前置节点正是传入的 nodeId，则加入列表
                if (GetRequiredSkillNode(item.itemId) == nodeId)
                {
                    list.Add(item.itemId);
                }
            }
            return list;
        }

        /// <summary>
        /// 配置表：定义哪些物品需要哪些技能节点
        /// </summary>
        private static string GetRequiredSkillNode(int itemId)
        {
            switch (itemId)
            {
                // === 契约类 ===
                // case 88888: // 贝拉契约
                case 88000: // 酒狐契约
                    return "maid_core_license"; // 需要核心授权

                // === 瓶中女仆类 ===
                case 88001: return "maid_special_bottle_1"; // Lv1
                case 88002: return "maid_special_bottle_2"; // Lv2
                case 88003: return "maid_special_bottle_3"; // Lv3

                // === 默认类 ===
                default: 
                    return null; // 无特殊要求
            }
        }
    }
}