using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace CombatMaid.Core.CustomModel
{
    public static class CustomModelBridge
    {
        private const string GameModulesAsm = "DuckovCustomModel.GameModules";
        private const string CoreAsm = "DuckovCustomModel.Core";

        private static MethodInfo _setAiModelMethod;
        private static MethodInfo _addWhitelistMethod;
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
                    var type = modulesAsm.GetType("DuckovCustomModel.Managers.ModelListManager");
                    // 新版设置模型接口
                    _setAiModelMethod = type?.GetMethod("SetModelInConfigForAICharacter", BindingFlags.Public | BindingFlags.Static);
                }

                var coreAsm = allAsms.FirstOrDefault(a => a.GetName().Name == CoreAsm);
                if (coreAsm != null)
                {
                    var type = coreAsm.GetType("DuckovCustomModel.Core.Data.AICharacters");
                    // 必须添加到白名单以便 DCM 识别 自定义预设的 AI 角色
                    _addWhitelistMethod = type?.GetMethod("AddAICharacters", BindingFlags.Public | BindingFlags.Static);
                }
                
                if (IsAvailable()) CMDebug.Log("DCM 接口反射绑定成功。");
            }
            catch (Exception ex) { CMDebug.LogError($"桥接器初始化异常: {ex.Message}"); }
        }

        /// <summary>
        /// 为指定角色注册默认模型
        /// </summary>
        public static void RegisterMaid(string nameKey, string modelID)
        {
            if (!IsAvailable()) return;

            // 注入白名单
            _addWhitelistMethod.Invoke(null, new object[] { new List<string> { nameKey } });

            // 检测是否已有配置，避免覆盖
            if (IsMaidConfiguredInFile(nameKey)) return;

            // 应用默认模型
            try
            {
                if (!string.IsNullOrEmpty(modelID))
                {
                    _setAiModelMethod.Invoke(null, new object[] { nameKey, modelID, true });
                    CMDebug.Log($"检测到 [{nameKey}] 为新角色，已应用默认预设: {modelID}");
                }
            }
            catch (Exception ex)
            {
                CMDebug.LogError($"应用配置失败: {ex.Message}");
            }
        }

        private static bool IsMaidConfiguredInFile(string nameKey)
        {
            try
            {
                string baseDir = Directory.GetCurrentDirectory();
                string path = Path.Combine(baseDir, "ModConfigs", "DuckovCustomModel", "UsingModel.json");
                
                // if (!File.Exists(path))
                // {
                //     path = Path.Combine(baseDir, "..", "ModConfigs", "DuckovCustomModel", "UsingModel.json");
                // }

                if (!File.Exists(path))
                {
                    CMDebug.LogWarning($"未找到DCM配置文件: {path}");
                    return false;
                }

                string jsonContent = File.ReadAllText(path);
                if (string.IsNullOrEmpty(jsonContent)) return false;

                JObject root = JObject.Parse(jsonContent);
                JObject targetDict = root["TargetTypeModelIDs"] as JObject;
                
                if (targetDict == null) return false;
                
                string targetId = "built-in:AICharacter_" + nameKey;
                if (targetDict.ContainsKey(targetId))
                {
                    CMDebug.Log($"DCM配置: [{targetId}] 已存在，跳过初始化。");
                    return true;
                }
                
                return false;
            }
            catch { return true; }
        }
    }
}