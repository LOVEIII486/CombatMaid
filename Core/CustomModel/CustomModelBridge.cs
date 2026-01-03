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
                    // 仅获取设置模型的方法
                    _setAiModelMethod = type?.GetMethod("SetModelInConfigForAICharacter", BindingFlags.Public | BindingFlags.Static);
                }

                var coreAsm = allAsms.FirstOrDefault(a => a.GetName().Name == CoreAsm);
                if (coreAsm != null)
                {
                    var type = coreAsm.GetType("DuckovCustomModel.Core.Data.AICharacters");
                    _addWhitelistMethod = type?.GetMethod("AddAICharacters", BindingFlags.Public | BindingFlags.Static);
                }
                
                if (IsAvailable()) CMDebug.Log("[CustomModelBridge] DCM 接口反射绑定成功。");
            }
            catch (Exception ex) { CMDebug.LogError($"[CustomModelBridge] 初始化异常: {ex.Message}"); }
        }

        /// <summary>
        /// 智能注册：检查磁盘，若无配置则应用默认模型。盔甲显示逻辑已移除，交给玩家自行管理。
        /// </summary>
        public static void RegisterMaid(string nameKey, string modelID)
        {
            if (!IsAvailable()) return;

            // 1. 注入白名单（使 AI 在 DCM 中合法可见）
            _addWhitelistMethod.Invoke(null, new object[] { new List<string> { nameKey } });

            // 2. 磁盘检测：如果已经配置过，则跳过默认设置以保护玩家自定义
            if (IsMaidConfiguredInFile(nameKey)) return;

            // 3. 应用默认模型
            try
            {
                if (!string.IsNullOrEmpty(modelID))
                {
                    // 参数: (string nameKey, string modelID, bool saveConfig)
                    // 直接设置为 true 触发保存
                    _setAiModelMethod.Invoke(null, new object[] { nameKey, modelID, true });
                    CMDebug.Log($"[CustomModelBridge] 检测到 [{nameKey}] 为新角色，已应用默认预设: {modelID}");
                }
            }
            catch (Exception ex)
            {
                CMDebug.LogError($"[CustomModelBridge] 应用配置失败: {ex.Message}");
            }
        }

        private static bool IsMaidConfiguredInFile(string nameKey)
        {
            try
            {
                string baseDir = Directory.GetCurrentDirectory();
                
                // 使用修正后的路径
                string path = Path.Combine(baseDir, "ModConfigs", "DuckovCustomModel", "UsingModel.json");
                
                // 兼容性路径修正
                if (!File.Exists(path))
                {
                    path = Path.Combine(baseDir, "..", "ModConfigs", "DuckovCustomModel", "UsingModel.json");
                }

                if (!File.Exists(path))
                {
                    // CMDebug.Log($"[DCM-Check] 未找到配置文件: {path}");
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
                    CMDebug.Log($"[DCM-Check] 命中配置: [{targetId}] 已存在，跳过初始化。");
                    return true;
                }
                
                return false;
            }
            catch { return true; } // 报错时默认保护，不进行覆盖
        }
    }
}