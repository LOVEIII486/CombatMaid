using System;
using System.Linq;
using System.Reflection;
using ItemStatsSystem;

namespace CombatMaid.ModCompatibility
{
    public static class VTModifierApplier
    {
        private static bool _isModInstalled = false;
        private static MethodInfo _calcMethod;
        private static string _modifierVarKey = "VT_MODIFIER"; // 默认 Key

        public static bool IsEnabled => _isModInstalled;

        public static void Init()
        {
            try
            {
                Type coreType = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType("VTModifiers.VTLib.VTModifiersCoreV2"))
                    .FirstOrDefault(t => t != null);

                if (coreType != null)
                {
                    // 获取计算方法：CalcItemModifiers(Item item)
                    _calcMethod = coreType.GetMethod("CalcItemModifiers", new[] { typeof(Item) });
                    
                    // 动态获取词缀标记变量名 (VT_MODIFIER)
                    var field = coreType.GetField("VariableVtModifierHashCode", BindingFlags.Public | BindingFlags.Static);
                    if (field != null)
                    {
                        _modifierVarKey = (string)field.GetValue(null);
                    }

                    // MethodInfo initDataMethod = coreType.GetMethod("InitData", BindingFlags.Public | BindingFlags.Static);
                    // initDataMethod?.Invoke(null, null);

                    if (_calcMethod != null)
                    {
                        _isModInstalled = true;
                        CMDebug.Log($"[Compatibility] VTModifiers 核心类匹配成功，标识 Key: {_modifierVarKey}");
                    }
                }
                else
                {
                    CMDebug.Log("[Compatibility] 未检测到 VTModifiers 模组。");
                }
            }
            catch (Exception ex)
            {
                _isModInstalled = false;
                CMDebug.LogWarning($"[Compatibility] 反射过程异常: {ex.Message}");
            }
        }

        public static void TryApplyModifiers(Item item)
        {
            if (!_isModInstalled || item == null) return;

            try
            {
                // 检查物品是否有词缀 ID
                string modifierId = item.GetString(_modifierVarKey, null);
                if (!string.IsNullOrEmpty(modifierId))
                {
                    // 执行词缀效果注入逻辑
                    _calcMethod.Invoke(null, new object[] { item });
                }
            }
            catch (Exception ex)
            {
                CMDebug.LogWarning($"[Compatibility] 应用词缀失败: {item.DisplayName} - {ex.Message}");
            }
        }
    }
}