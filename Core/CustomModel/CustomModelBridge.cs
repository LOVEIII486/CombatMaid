using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CombatMaid.Core.CustomModel
{
    public static class CustomModelBridge
    {
        private const string GameModulesAsm = "DuckovCustomModel.GameModules";
        private const string CoreAsm = "DuckovCustomModel.Core";

        private static MethodInfo _setAiModelMethod;
        private static MethodInfo _addWhitelistMethod;
        private static PropertyInfo _usingModelProp;
        private static PropertyInfo _targetDictProp;
        private static PropertyInfo _aiCharsListProp;
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
                var modulesAsm = allAsms.FirstOrDefault(a => a.GetName().Name == GameModulesAsm);
                if (modulesAsm != null)
                {
                    var managerType = modulesAsm.GetType("DuckovCustomModel.Managers.ModelListManager");
                    _setAiModelMethod = managerType?.GetMethod("SetModelInConfigForAICharacter", BindingFlags.Public | BindingFlags.Static);

                    var entryType = modulesAsm.GetType("DuckovCustomModel.ModEntry");
                    _usingModelProp = entryType?.GetProperty("UsingModel", BindingFlags.Public | BindingFlags.Static);
                    
                    if (_usingModelProp != null)
                    {
                        var configType = _usingModelProp.PropertyType;
                        _targetDictProp = configType.GetProperty("TargetTypeModelIDs", BindingFlags.Public | BindingFlags.Instance);
                    }
                }

                var coreAsm = allAsms.FirstOrDefault(a => a.GetName().Name == CoreAsm);
                if (coreAsm != null)
                {
                    var aiCharsType = coreAsm.GetType("DuckovCustomModel.Core.Data.AICharacters");
                    _addWhitelistMethod = aiCharsType?.GetMethod("AddAICharacters", BindingFlags.Public | BindingFlags.Static);
                    _aiCharsListProp = aiCharsType?.GetProperty("characterNameKeys", BindingFlags.Public | BindingFlags.Static);
                }
            }
            catch (Exception ex) { CMDebug.LogError($"[CustomModelBridge] 初始化失败: {ex.Message}"); }
        }

        /// <summary>
        /// 仅注入白名单和本地化。不涉及磁盘操作，可立即调用。
        /// </summary>
        public static void OnlyRegisterWhitelist(string nameKey)
        {
            if (!IsAvailable()) return;
            try
            {
                _addWhitelistMethod.Invoke(null, new object[] { new List<string> { nameKey } });
                // CMDebug.Log($"[CustomModelBridge] 身份白名单注入成功: {nameKey}");
            }
            catch (Exception ex) { CMDebug.LogError($"[CustomModelBridge] 白名单注入异常: {ex.Message}"); }
        }

        /// <summary>
        /// 仅尝试写入默认模型（带保护逻辑）。必须在 IsDcmConfigReady 为 true 时调用。
        /// </summary>
        public static void OnlyTrySetDefaultModel(string nameKey, string defaultModelID)
        {
            if (!IsAvailable()) return;
            try
            {
                if (IsConfigExists(nameKey)) return;

                // 写入并保存配置
                _setAiModelMethod.Invoke(null, new object[] { nameKey, defaultModelID, true });
                CMDebug.Log($"[CustomModelBridge] 首次运行：已为 [{nameKey}] 设置默认模型 [{defaultModelID}]");
            }
            catch (Exception ex) { CMDebug.LogError($"[CustomModelBridge] 默认模型设置异常: {ex.Message}"); }
        }

        public static bool IsDcmConfigReady()
        {
            if (!IsAvailable()) return false;
            try
            {
                var usingModel = _usingModelProp.GetValue(null);
                if (usingModel == null) return false;

                var keys = _aiCharsListProp?.GetValue(null) as ICollection;
                if (keys == null || keys.Count == 0) return false;

                var dict = _targetDictProp.GetValue(usingModel) as IDictionary;
                return dict != null;
            }
            catch { return false; }
        }

        private static bool IsConfigExists(string nameKey)
        {
            try
            {
                var usingModel = _usingModelProp.GetValue(null);
                var dict = _targetDictProp.GetValue(usingModel) as IDictionary;
                if (dict == null) return true;

                string targetId = "built-in:AICharacter_" + nameKey;
                return dict.Contains(targetId);
            }
            catch { return true; }
        }
    }
}