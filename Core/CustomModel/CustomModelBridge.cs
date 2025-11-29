using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace CombatMaid.Core.CustomModel
{
    /// <summary>
    /// DuckovCustomModel 模组桥接器
    /// </summary>
    public static class CustomModelBridge
    {
        private const string LogTag = "[CombatMaid.Bridge]";
        private static bool _isInitialized = false;
        private static bool _isModAvailable = false;

        // 反射缓存
        private static Type _modelHandlerType; 
        private static Type _bundleType;
        private static Type _modelInfoType;
        
        private static FieldInfo _bundlesField;      // ModelBundles
        private static PropertyInfo _modelsProp;     // ModelBundleInfo.Models
        private static PropertyInfo _bundleNameProp; // BundleName
        private static PropertyInfo _modelIdProp;    // ModelInfo.ModelID
        private static FieldInfo _modelIdField;      
        
        private static MethodInfo _findMethod;   // FindModelByID
        private static MethodInfo _initMethod;   // Initialize
        private static MethodInfo _loadMethod;   // InitializeCustomModel
        private static MethodInfo _changeMethod; // ChangeToCustomModel
        
        private static object _aiTargetEnumValue;

        // ==================== 公共接口 ====================

        /// <summary>
        /// 输出所有可用模型 ID 到日志
        /// </summary>
        public static void LogAvailableModels()
        {
            if (!_isInitialized) InitializeReflection();
            if (!_isModAvailable) return;

            try
            {
                var bundles = _bundlesField.GetValue(null) as IList;
                if (bundles == null || bundles.Count == 0) return;

                Debug.Log($"{LogTag} === 可用模型列表 ===");
                foreach (object bundle in bundles)
                {
                    string bundleName = GetPropString(bundle, _bundleNameProp) ?? "Unknown";
                    IList models = GetList(bundle, _modelsProp);

                    if (models != null)
                    {
                        foreach (object model in models)
                        {
                            Debug.Log($">>> 包名: [{bundleName}] | ID: [{GetModelID(model)}]");
                        }
                    }
                }
                Debug.Log($"{LogTag} ======================");
            }
            catch (Exception ex) { Debug.LogError($"{LogTag} 列出模型失败: {ex.Message}"); }
        }

        /// <summary>
        /// 异步应用指定 ID 的模型
        /// </summary>
        public static IEnumerator ApplyModelByIDAsync(CharacterMainControl target, string modelId)
        {
            if (!_isInitialized) InitializeReflection();
            if (!_isModAvailable || target == null || string.IsNullOrEmpty(modelId)) yield break;

            object bundle = null;
            object model = null;
            bool found = false;

            try
            {
                // 调用 ModelManager.FindModelByID
                object[] args = new object[] { modelId, null, null };
                found = (bool)_findMethod.Invoke(null, args);
                if (found)
                {
                    bundle = args[1];
                    model = args[2];
                }
            }
            catch (Exception ex) { Debug.LogWarning($"{LogTag} 查找模型失败: {ex.Message}"); }

            if (!found)
            {
                Debug.LogWarning($"{LogTag} 未找到模型 ID: {modelId}");
                yield break;
            }

            yield return null; 

            ApplyModelInternal(target, bundle, model);
        }

        // ==================== 内部逻辑 ====================

        private static void ApplyModelInternal(CharacterMainControl target, object bundle, object model)
        {
            try 
            {
                Component handler = target.GetComponent(_modelHandlerType);
                if (handler == null) handler = target.gameObject.AddComponent(_modelHandlerType);

                // 尝试清洗导致报错的 LootBox 字段
                SanitizeDeathLootBox(model);

                // 执行初始化链
                _initMethod.Invoke(handler, new object[] { target, _aiTargetEnumValue });
                _loadMethod.Invoke(handler, new object[] { bundle, model });
                _changeMethod.Invoke(handler, null);

                HideOriginalEquipment(target);
                Debug.Log($"{LogTag} 模型应用成功: {GetModelID(model)}");
            }
            catch(Exception ex)
            {
                Debug.LogError($"{LogTag} 应用异常: {ex.Message}");
            }
        }

        private static void InitializeReflection()
        {
            _isInitialized = true;
            try
            {
                // 获取类型
                Type managerType = Type.GetType("DuckovCustomModel.Managers.ModelManager, DuckovCustomModel.GameModules");
                _modelHandlerType = Type.GetType("DuckovCustomModel.MonoBehaviours.ModelHandler, DuckovCustomModel.GameModules");
                _bundleType = Type.GetType("DuckovCustomModel.Core.Data.ModelBundleInfo, DuckovCustomModel.Core");
                _modelInfoType = Type.GetType("DuckovCustomModel.Core.Data.ModelInfo, DuckovCustomModel.Core");
                Type targetEnum = Type.GetType("DuckovCustomModel.Core.Data.ModelTarget, DuckovCustomModel.Core");

                if (managerType == null || _modelHandlerType == null) return;

                // 获取成员
                _bundlesField = managerType.GetField("ModelBundles", BindingFlags.Public | BindingFlags.Static);
                _findMethod = managerType.GetMethod("FindModelByID", BindingFlags.Public | BindingFlags.Static);
                
                _bundleNameProp = _bundleType?.GetProperty("BundleName");
                _modelsProp = _bundleType?.GetProperty("Models");

                _modelIdProp = _modelInfoType?.GetProperty("ModelID");
                _modelIdField = _modelInfoType?.GetField("ModelID");

                _initMethod = _modelHandlerType.GetMethod("Initialize", new Type[] { typeof(CharacterMainControl), targetEnum });
                _loadMethod = _modelHandlerType.GetMethod("InitializeCustomModel", new Type[] { _bundleType, _modelInfoType });
                _changeMethod = _modelHandlerType.GetMethod("ChangeToCustomModel");

                _aiTargetEnumValue = Enum.Parse(targetEnum, "AICharacter");

                if (_bundlesField != null && _findMethod != null && _initMethod != null)
                {
                    _isModAvailable = true;
                    Debug.Log($"{LogTag} 模组连接成功");
                }
            }
            catch { /* 忽略反射错误，视为未安装模组 */ }
        }

        private static void SanitizeDeathLootBox(object modelInfo)
        {
            try
            {
                var props = modelInfo.GetType().GetProperties();
                foreach (var p in props)
                {
                    if (p.Name.Contains("DeathLootBox") && p.PropertyType == typeof(string) && p.CanWrite)
                        p.SetValue(modelInfo, null);
                }
            }
            catch { }
        }

        private static void HideOriginalEquipment(CharacterMainControl c)
        {
            if (c?.characterModel == null) return;
            Transform[] sockets = { c.characterModel.HelmatSocket, c.characterModel.ArmorSocket };
            foreach (var s in sockets)
            {
                if (s == null) continue;
                foreach (var r in s.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            }
        }

        private static string GetModelID(object obj)
        {
            if (obj == null) return "null";
            return (_modelIdProp?.GetValue(obj) ?? _modelIdField?.GetValue(obj))?.ToString() ?? "Unknown";
        }

        private static string GetPropString(object obj, PropertyInfo prop) => prop?.GetValue(obj)?.ToString();
        private static IList GetList(object obj, PropertyInfo prop) => prop?.GetValue(obj) as IList;
    }
}