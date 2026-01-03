using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CombatMaid.Core.CustomModel
{
    /// <summary>
    /// 自定义模型前置反射桥接器 - 协作增强版
    /// 逻辑：1. 注入白名单(允许接管) -> 2. 检测配置是否存在 -> 3. 若无配置则初始化默认模型
    /// </summary>
    public static class CustomModelBridge
    {
        private const string GameModulesAsm = "DuckovCustomModel.GameModules";
        private const string CoreAsm = "DuckovCustomModel.Core";

        private static MethodInfo _setAiModelMethod;
        private static MethodInfo _addWhitelistMethod;
        private static PropertyInfo _usingModelProp;
        private static PropertyInfo _targetDictProp;

        private static bool _initialized;

        public static bool IsAvailable()
        {
            if (!_initialized) Initialize();
            return _setAiModelMethod != null && _addWhitelistMethod != null;
        }

        private static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                var allAsms = AppDomain.CurrentDomain.GetAssemblies();

                // 1. 获取模型设置接口
                var modulesAsm = allAsms.FirstOrDefault(a => a.GetName().Name == GameModulesAsm);
                if (modulesAsm != null)
                {
                    var managerType = modulesAsm.GetType("DuckovCustomModel.Managers.ModelListManager");
                    _setAiModelMethod = managerType?.GetMethod("SetModelInConfigForAICharacter",
                        BindingFlags.Public | BindingFlags.Static, null,
                        new[] { typeof(string), typeof(string), typeof(bool) }, null);

                    // 获取配置访问入口: ModEntry.UsingModel
                    var entryType = modulesAsm.GetType("DuckovCustomModel.ModEntry");
                    _usingModelProp = entryType?.GetProperty("UsingModel", BindingFlags.Public | BindingFlags.Static);
                    
                    if (_usingModelProp != null)
                    {
                        // 获取字典属性: UsingModel.TargetTypeModelIDs
                        var configType = _usingModelProp.PropertyType;
                        _targetDictProp = configType.GetProperty("TargetTypeModelIDs", BindingFlags.Public | BindingFlags.Instance);
                    }
                }

                // 2. 获取白名单注入接口
                var coreAsm = allAsms.FirstOrDefault(a => a.GetName().Name == CoreAsm);
                if (coreAsm != null)
                {
                    var aiCharsType = coreAsm.GetType("DuckovCustomModel.Core.Data.AICharacters");
                    _addWhitelistMethod = aiCharsType?.GetMethod("AddAICharacters", BindingFlags.Public | BindingFlags.Static);
                }

                if (_setAiModelMethod != null && _addWhitelistMethod != null && _targetDictProp != null)
                    CMDebug.Log("[CustomModelBridge] DCM 协作接口已就绪（支持配置保护）。");
            }
            catch (Exception ex)
            {
                CMDebug.LogError($"[CustomModelBridge] 初始化异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 智能注册：如果玩家还没设置过模型，则应用默认值并保存；如果已设置，则仅注入白名单确保生效。
        /// </summary>
        public static void SmartRegisterMaid(string nameKey, string defaultModelID)
        {
            if (!IsAvailable()) return;

            try
            {
                // 1. 无论如何，先注入白名单，否则 DCM 补丁会无视这个角色实例
                _addWhitelistMethod.Invoke(null, new object[] { new List<string> { nameKey } });

                // 2. 检查配置中是否已存在该角色的模型设置
                if (IsConfigExists(nameKey))
                {
                    CMDebug.Log($"[CustomModelBridge] 检测到 [{nameKey}] 已有配置，跳过初始化以保护玩家设置。");
                    return;
                }

                // 3. 如果是全新角色，应用默认模型并持久化到 UsingModel.json
                _setAiModelMethod.Invoke(null, new object[] { nameKey, defaultModelID, true });
                CMDebug.Log($"[CustomModelBridge] 已为新女仆 [{nameKey}] 初始化默认模型: {defaultModelID} (已自动保存)");
            }
            catch (Exception ex)
            {
                CMDebug.LogError($"[CustomModelBridge] 智能注册失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 内部辅助：检查 DCM 的字典里是否已有该 Key
        /// </summary>
        private static bool IsConfigExists(string nameKey)
        {
            try
            {
                var usingModel = _usingModelProp.GetValue(null);
                if (usingModel == null) return false;

                var dict = _targetDictProp.GetValue(usingModel) as IDictionary;
                if (dict == null) return false;

                // DCM 的 TargetID 内部构造规则
                string targetId = "built-in:AICharacter_" + nameKey;
                return dict.Contains(targetId);
            }
            catch
            {
                return false;
            }
        }
    }
}