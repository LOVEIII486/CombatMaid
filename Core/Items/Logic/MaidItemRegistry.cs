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
        
        public static void RefreshShopAvailability()
        {
            // 清理旧的商店数据 (如果 FML 支持的话，或者依靠游戏重载)
            // 重新遍历定义，只针对商店部分进行注入
            var items = MaidItemDefs.GetDefinitions();
            foreach (var info in items)
            {
                // 重新尝试注入（TryInjectToShop 内部会再次检查 IsItemUnlocked）
                TryInjectToShop(info);
            }
            CMDebug.Log("[MaidItemRegistry] 商店列表已根据技能树状态刷新。");
        }

        private static void TryInjectToShop(MaidItemInfo info)
        {
            if (string.IsNullOrEmpty(info.ShopMerchantId)) return;

            // [新增] 核心拦截：检测技能树解锁状态
            if (!IsItemUnlocked(info))
            {
                // 如果未解锁，仅在调试模式下打印，避免刷屏
                // CMDebug.Log($"[商店] 物品 {info.itemId} 未解锁 (节点条件未满足)，跳过注册。");
                return;
            }

            ShopUtils.AddGoods(new ShopGoodsData
            {
                merchantProfileID = info.ShopMerchantId,
                typeID = info.itemId,
                maxStock = info.ShopMaxStock,
                priceFactor = info.ShopPriceFactor,
                possibility = info.ShopPossibility,
                forceUnlock = info.ShopForceUnlock
            });
            
            CMDebug.Log($"[商店] 已上架物品 {info.itemId} (商人: {info.ShopMerchantId})");
        }
        
        private static bool IsItemUnlocked(MaidItemInfo info)
        {
            // 1. 获取所需的技能节点 ID
            string requiredNodeID = GetRequiredSkillNode(info.itemId);

            // 2. 如果返回 null 或空，说明该物品没有门槛，默认解锁
            if (string.IsNullOrEmpty(requiredNodeID)) return true;

            // 3. 检查技能树管理器是否存在 (防止游戏刚启动时报错)
            if (SkillTreeManager.Instance == null)
            {
                // 注意：如果 Initialize 在读档前运行，这里可能为空。
                // 建议策略：如果没有加载存档，视为未解锁，防止未授权物品泄露
                return false; 
            }

            // 4. 查询节点是否已点亮
            return SkillTreeManager.Instance.IsSkillUnlocked(requiredNodeID);
        }

        /// <summary>
        /// 配置表：定义哪些物品需要哪些技能节点
        /// 未来添加新物品只需修改这里
        /// </summary>
        private static string GetRequiredSkillNode(int itemId)
        {
            switch (itemId)
            {
                // === 契约类 ===
                case 88888: // 贝拉契约 (MaidItemDefs.ID_MAID_CONTRACT)
                case 88000: // 酒狐契约
                    return "maid_core_license"; // 需要核心授权

                // === 瓶中女仆类 (假设ID) ===
                // 请替换为你实际定义的瓶中女仆物品 ID
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