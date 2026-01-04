using System;
using System.Reflection;
using ItemStatsSystem;

namespace CombatMaid.ModCompatibility
{
    /// <summary>
    /// 词缀模组兼容性助手 - 负责处理与 VTModifiers 模组的可选依赖逻辑
    /// </summary>
    public static class VTModifierApplier
    {
        private static bool _isModInstalled = false;
        private static MethodInfo _calcMethod;
        private static string _modifierVarKey;

        /// <summary>
        /// 外部接口：判断词缀模组是否已启用且兼容
        /// </summary>
        public static bool IsEnabled => _isModInstalled;

        /// <summary>
        /// 初始化反射逻辑。应在 Mod 启动时（如 Awake/Start）调用一次。
        /// </summary>
        public static void Init()
        {
            try
            {
                // 尝试定位目标程序集中的核心类
                Type coreType = Type.GetType("VTModifiers.VTLib.VTModifiersCoreV2, VTModifiers");
                
                if (coreType != null)
                {
                    // 获取初始化数据和计算词缀的方法
                    MethodInfo initDataMethod = coreType.GetMethod("InitData", BindingFlags.Public | BindingFlags.Static);
                    _calcMethod = coreType.GetMethod("CalcItemModifiers", new[] { typeof(Item) });
                    
                    // 获取标记变量 Key: VT_MODIFIER
                    var field = coreType.GetField("VariableVtModifierHashCode", BindingFlags.Public | BindingFlags.Static);
                    _modifierVarKey = field?.GetValue(null) as string ?? "VT_MODIFIER";

                    if (initDataMethod != null && _calcMethod != null)
                    {
                        // 预热词缀模组的数据加载
                        initDataMethod.Invoke(null, null);
                        _isModInstalled = true;
                        CMDebug.LogInfo("[Compatibility] 检测到 VTModifiers，已激活词缀自动恢复支持。");
                    }
                }
            }
            catch (Exception ex)
            {
                _isModInstalled = false;
                CMDebug.LogWarning($"[Compatibility] 词缀模组反射初始化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 尝试为物品应用词缀效果。调用前应先检查 IsEnabled。
        /// </summary>
        public static void TryApplyModifiers(Item item)
        {
            // 基础安全检查
            if (!_isModInstalled || item == null) return;

            try
            {
                // 检查物品是否有词缀标记变量
                string modifierId = item.GetString(_modifierVarKey, null);
                if (!string.IsNullOrEmpty(modifierId))
                {
                    _calcMethod.Invoke(null, new object[] { item });
                }
            }
            catch (Exception ex)
            {
                CMDebug.LogWarning($"[Compatibility] 应用词缀效果失败 ({item.DisplayName}): {ex.Message}");
            }
        }
    }
}