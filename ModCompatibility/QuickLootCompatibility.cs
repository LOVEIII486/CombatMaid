using System;
using HarmonyLib;
using UnityEngine;

namespace CombatMaid.ModCompatibility
{
    public static class QuickLootCompatibility
    {
        // 根据源码确认的精确类名
        private const string TargetClassName = "QuickLoot.ModBehaviour";

        private static MonoBehaviour _targetModInstance;
        private static bool _hasChecked = false;

        /// <summary>
        /// 尝试禁用 QuickLoot 模组
        /// </summary>
        public static void DisableQuickLoot()
        {
            try
            {
                // 1. 获取实例（只在第一次调用时查找，缓存结果）
                if (!_hasChecked)
                {
                    // 使用 Harmony 的 AccessTools 查找类型，非常方便
                    Type targetType = AccessTools.TypeByName(TargetClassName);

                    if (targetType != null)
                    {
                        // 在场景中查找该类型的活动对象
                        _targetModInstance = UnityEngine.Object.FindObjectOfType(targetType) as MonoBehaviour;

                        if (_targetModInstance != null)
                        {
                            CMDebug.Log($"[兼容性] 已找到冲突模组: {TargetClassName}");
                        }
                    }

                    _hasChecked = true;
                }

                // 2. 执行禁用逻辑
                if (_targetModInstance != null && _targetModInstance.enabled)
                {
                    _targetModInstance.enabled = false;
                    CMDebug.LogWarning("[兼容性] 为防止冲突，已临时禁用 QuickLoot (自动拾取)。");
                }
            }
            catch (Exception ex)
            {
                CMDebug.LogWarning($"[兼容性] 尝试禁用 QuickLoot 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 恢复 QuickLoot 模组
        /// </summary>
        public static void RestoreQuickLoot()
        {
            try
            {
                // 只有当我们之前确实找到了实例，且目前处于禁用状态时才恢复
                if (_targetModInstance != null && !_targetModInstance.enabled)
                {
                    _targetModInstance.enabled = true;
                    CMDebug.Log("[兼容性] 已恢复 QuickLoot 功能。");
                }
            }
            catch (Exception ex)
            {
                CMDebug.LogWarning($"[兼容性] 恢复 QuickLoot 失败: {ex.Message}");
            }
        }
    }
}