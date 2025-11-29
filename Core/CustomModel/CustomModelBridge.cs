using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace CombatMaid.Core.CustomModel
{
    /// <summary>
    /// 用于与 DuckovCustomModel 模组进行反射交互的桥接类
    /// (支持日志输出列表 + 指定ID应用)
    /// </summary>
    public static class CustomModelBridge
    {
        private const string LogTag = "[CombatMaid.CustomModelBridge]";
        
        // 状态标记
        private static bool _isInitialized = false;
        private static bool _isModAvailable = false;

        // 反射缓存：类型
        private static Type _managerType;      // ModelManager
        private static Type _modelHandlerType; // ModelHandler
        private static Type _targetEnumType;   // ModelTarget
        private static Type _bundleType;       // ModelBundleInfo
        private static Type _modelInfoType;    // ModelInfo
        
        // 反射缓存：字段/属性
        private static FieldInfo _bundlesField;     // ModelManager.ModelBundles
        private static PropertyInfo _bundleNameProp; // ModelBundleInfo.BundleName
        private static PropertyInfo _modelsProp;     // ModelBundleInfo.Models
        private static PropertyInfo _modelIdProp;    // ModelInfo.ModelID (通常是属性)
        private static FieldInfo _modelIdField;      // ModelInfo.ModelID (后备字段)

        // 反射缓存：方法
        private static MethodInfo _findMethod;   // ModelManager.FindModelByID
        private static MethodInfo _initMethod;   // ModelHandler.Initialize
        private static MethodInfo _loadMethod;   // ModelHandler.InitializeCustomModel
        private static MethodInfo _changeMethod; // ModelHandler.ChangeToCustomModel
        
        // 反射缓存：枚举值
        private static object _aiTargetEnumValue; // ModelTarget.AICharacter

        /// <summary>
        /// 【第一步】输出所有可用的模型 ID 到日志，供您查阅
        /// </summary>
        public static void LogAvailableModels()
        {
            if (!_isInitialized) InitializeReflection();
            if (!_isModAvailable) return;

            try
            {
                var bundles = _bundlesField.GetValue(null) as IList;
                if (bundles == null || bundles.Count == 0)
                {
                    Debug.LogWarning($"{LogTag} 模型库为空，未找到任何已加载的模型包。");
                    return;
                }

                Debug.Log($"{LogTag} === 开始列出所有可用自定义模型 ===");
                
                foreach (object bundle in bundles)
                {
                    string bundleName = GetPropString(bundle, _bundleNameProp) ?? "UnknownBundle";
                    IList models = GetList(bundle, _modelsProp);

                    if (models != null)
                    {
                        foreach (object model in models)
                        {
                            string modelId = GetModelID(model);
                            Debug.Log($">>> 包名: [{bundleName}]  |  模型ID: [{modelId}]");
                        }
                    }
                }
                
                Debug.Log($"{LogTag} === 列表结束 (请复制想要的 模型ID 使用) ===");
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 列出模型失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 【第二步】应用指定的模型 ID 给角色
        /// </summary>
        /// <param name="target">目标角色</param>
        /// <param name="modelId">从日志中查到的模型 ID</param>
        /// <returns>是否成功</returns>
        public static IEnumerator ApplyModelByIDAsync(CharacterMainControl target, string modelId)
        {
            if (!_isInitialized) InitializeReflection();
            if (!_isModAvailable || target == null || string.IsNullOrEmpty(modelId)) yield break;

            object bundle = null;
            object model = null;
            bool found = false;

            try
            {
                // 1. 查找模型 (同步操作，通常很快)
                object[] args = new object[] { modelId, null, null };
                found = (bool)_findMethod.Invoke(null, args);
                if (found)
                {
                    bundle = args[1];
                    model = args[2];
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogTag} 查找模型失败: {ex.Message}");
            }

            if (!found)
            {
                Debug.LogWarning($"{LogTag} 未找到 ID '{modelId}'");
                yield break;
            }

            // [关键优化] 暂停一帧。让游戏先完成女仆的生成动画和逻辑，避免生成瞬间卡顿
            yield return null; 

            // 2. 应用模型 (耗时操作)
            ApplyModelInternal(target, bundle, model);
        }

        /// <summary>
        /// 内部应用逻辑
        /// </summary>
        private static void ApplyModelInternal(CharacterMainControl target, object bundle, object model)
        {
            try 
            {
                Component handler = target.GetComponent(_modelHandlerType);
                if (handler == null) handler = target.gameObject.AddComponent(_modelHandlerType);

                // [尝试修复报错] 尝试通过反射清除模型数据中的 DeathLootBox 引用
                // 这可以防止 DuckovMod 尝试加载不存在的 prefab
                SanitizeDeathLootBox(model);

                // 执行初始化链
                _initMethod.Invoke(handler, new object[] { target, _aiTargetEnumValue });
                _loadMethod.Invoke(handler, new object[] { bundle, model });
                _changeMethod.Invoke(handler, null);

                HideOriginalEquipment(target);
                Debug.Log($"{LogTag} 模型应用完成: {target.name}");
            }
            catch(Exception ex)
            {
                Debug.LogError($"{LogTag} 应用过程异常: {ex.Message}");
            }
        }
        
        private static void SanitizeDeathLootBox(object modelInfo)
        {
            try
            {
                // 盲猜 Duckov 模型数据中可能包含 "DeathLootBox" 相关的字段/属性
                // 如果将其置空，模组可能会跳过加载，从而消除报错
                var type = modelInfo.GetType();
                var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                foreach (var p in props)
                {
                    if (p.Name.Contains("DeathLootBox") && p.PropertyType == typeof(string) && p.CanWrite)
                    {
                        p.SetValue(modelInfo, null); // 设为空，欺骗模组不要加载它
                    }
                }
            }
            catch { /* 忽略清洗失败，这只是尝试修复 */ }
        }

        // ==================== 初始化与辅助 ====================

        private static void InitializeReflection()
        {
            _isInitialized = true;
            try
            {
                // 1. 加载类型
                _managerType = Type.GetType("DuckovCustomModel.Managers.ModelManager, DuckovCustomModel.GameModules");
                _modelHandlerType = Type.GetType("DuckovCustomModel.MonoBehaviours.ModelHandler, DuckovCustomModel.GameModules");
                _bundleType = Type.GetType("DuckovCustomModel.Core.Data.ModelBundleInfo, DuckovCustomModel.Core");
                _modelInfoType = Type.GetType("DuckovCustomModel.Core.Data.ModelInfo, DuckovCustomModel.Core");
                _targetEnumType = Type.GetType("DuckovCustomModel.Core.Data.ModelTarget, DuckovCustomModel.Core");

                if (_managerType == null || _modelHandlerType == null || _bundleType == null || _targetEnumType == null)
                {
                    Debug.LogWarning($"{LogTag} 未找到 DuckovCustomModel 模组，跳过功能。");
                    _isModAvailable = false;
                    return;
                }

                // 2. 获取数据源字段/属性
                _bundlesField = _managerType.GetField("ModelBundles", BindingFlags.Public | BindingFlags.Static);
                _bundleNameProp = _bundleType.GetProperty("BundleName");
                _modelsProp = _bundleType.GetProperty("Models"); // 列表属性
                
                // ModelInfo.ModelID 可能是属性也可能是字段
                _modelIdProp = _modelInfoType.GetProperty("ModelID");
                if (_modelIdProp == null) _modelIdField = _modelInfoType.GetField("ModelID");

                // 3. 获取方法
                _findMethod = _managerType.GetMethod("FindModelByID", BindingFlags.Public | BindingFlags.Static);
                _initMethod = _modelHandlerType.GetMethod("Initialize", new Type[] { typeof(CharacterMainControl), _targetEnumType });
                _loadMethod = _modelHandlerType.GetMethod("InitializeCustomModel", new Type[] { _bundleType, _modelInfoType });
                _changeMethod = _modelHandlerType.GetMethod("ChangeToCustomModel");

                // 4. 枚举
                _aiTargetEnumValue = Enum.Parse(_targetEnumType, "AICharacter");

                if (_bundlesField != null && _findMethod != null && _initMethod != null)
                {
                    _isModAvailable = true;
                    Debug.Log($"{LogTag} 反射初始化成功。");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 初始化失败: {ex.Message}");
                _isModAvailable = false;
            }
        }

        private static string GetModelID(object modelObj)
        {
            if (modelObj == null) return "null";
            if (_modelIdProp != null) return _modelIdProp.GetValue(modelObj)?.ToString();
            if (_modelIdField != null) return _modelIdField.GetValue(modelObj)?.ToString();
            return "UnknownID";
        }

        private static string GetPropString(object obj, PropertyInfo prop) => prop?.GetValue(obj)?.ToString();
        private static IList GetList(object obj, PropertyInfo prop) => prop?.GetValue(obj) as IList;

        private static void HideOriginalEquipment(CharacterMainControl character)
        {
            if (character == null || character.characterModel == null) return;
            Transform[] sockets = { character.characterModel.HelmatSocket, character.characterModel.ArmorSocket };
            foreach (var socket in sockets)
            {
                if (socket == null) continue;
                foreach (var r in socket.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            }
        }
    }
}