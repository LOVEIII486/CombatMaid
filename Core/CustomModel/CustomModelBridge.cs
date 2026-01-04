using System;
using System.Linq;
using System.Reflection;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CombatMaid.Core.CustomModel
{
    public static class CustomModelBridge
    {
        private static MethodInfo _registerMethod;
        private static MethodInfo _setConfigMethod;
        private static MethodInfo _initHandlerMethod;
        private static MethodInfo _updatePriorityMethod;
        private static Type _handlerType;
        private static bool _initialized;

        public static bool IsAvailable()
        {
            if (!_initialized) Initialize();
            return _registerMethod != null && _setConfigMethod != null;
        }

        private static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                var allAsms = AppDomain.CurrentDomain.GetAssemblies();
                foreach (var asm in allAsms)
                {
                    // 1. 注册表方法
                    if (_registerMethod == null) {
                        var t = asm.GetType("DuckovCustomModel.Core.Managers.ModelTargetTypeRegistry");
                        if (t != null) _registerMethod = t.GetMethod("RegisterTargetType", new[] { typeof(string), typeof(string[]), typeof(Func<SystemLanguage, string>) });
                    }
                    // 2. 设置方法
                    if (_setConfigMethod == null) {
                        var t = asm.GetType("DuckovCustomModel.Managers.ModelListManager");
                        if (t != null) _setConfigMethod = t.GetMethod("SetModelInConfig", new[] { typeof(string), typeof(string), typeof(bool) });
                    }
                    // 3. Handler 类及其方法
                    if (_handlerType == null) {
                        var t = asm.GetType("DuckovCustomModel.MonoBehaviours.ModelHandler");
                        if (t != null) {
                            _handlerType = t;
                            _initHandlerMethod = t.GetMethod("Initialize", new[] { typeof(CharacterMainControl), typeof(string) });
                            _updatePriorityMethod = t.GetMethod("UpdateModelPriorityList");
                        }
                    }
                }
                CMDebug.Log($"[DCM-Bridge] 接口绑定: Register={_registerMethod!=null}, SetConfig={_setConfigMethod!=null}, Handler={_handlerType!=null}");
            }
            catch (Exception ex) { CMDebug.LogError($"[DCM-Bridge] 初始化异常: {ex.Message}"); }
        }

        public static void RegisterMaid(string profileName, string displayName, string defaultModelID)
        {
            if (!IsAvailable()) return;

            try
            {
                string rawId = "CombatMaid_" + profileName;
                // 兼容类型设置为 built-in:Character (开发者建议)
                string[] compatibles = new[] { "built-in:Character" };
                Func<SystemLanguage, string> nameGetter = (lang) => displayName;

                _registerMethod.Invoke(null, new object[] { rawId, compatibles, nameGetter });

                string fullTargetId = "extension:" + rawId;
                if (!IsTargetIdConfigured(fullTargetId))
                {
                    _setConfigMethod.Invoke(null, new object[] { fullTargetId, defaultModelID, true });
                    CMDebug.Log($"[DCM-Bridge] 注册并绑定模型: {fullTargetId} -> {defaultModelID}");
                }
            }
            catch (Exception ex) { CMDebug.LogError($"[DCM-Bridge] 注册失败: {ex.Message}"); }
        }

        public static void ActivateModel(Component handler, CharacterMainControl charCtrl, string profileName)
        {
            if (handler == null || charCtrl == null || _initHandlerMethod == null) return;
            try
            {
                string extensionId = "extension:CombatMaid_" + profileName;
                // 1. 调用官方 Initialize 建立连接
                _initHandlerMethod.Invoke(handler, new object[] { charCtrl, extensionId });
                // 2. 调用官方 UpdateModelPriorityList 触发模型替换，经测试这一步是必须的，否则模型不会生效！！！
                _updatePriorityMethod.Invoke(handler, null);
                CMDebug.Log($"[DCM-Bridge] 激活成功: {extensionId}");
            }
            catch (Exception ex) { CMDebug.LogWarning($"[DCM-Bridge] 激活 Handler 异常: {ex.Message}"); }
        }

        public static Type GetModelHandlerType() => _handlerType;

        private static bool IsTargetIdConfigured(string fullTargetId)
        {
            try {
                string path = Path.Combine(Directory.GetCurrentDirectory(), "ModConfigs", "DuckovCustomModel", "UsingModel.json");
                if (!File.Exists(path)) return false;
                JObject root = JObject.Parse(File.ReadAllText(path));
                return root["TargetTypeModelIDs"] is JObject dict && dict.ContainsKey(fullTargetId);
            } catch { return true; }
        }
    }
}