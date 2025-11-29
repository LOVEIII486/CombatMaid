using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Random = UnityEngine.Random;

namespace CombatMaid.Core.CustomModel
{
    public static class CustomModelSelector
    {
        private const string LogTag = "[CombatMaid.Selector]";

        // 缓存
        private static bool _isInitialized = false;
        private static Type _managerType;
        private static FieldInfo _bundlesField;
        private static FieldInfo _modelsField; // ModelBundleInfo 中的 Models 列表字段

        private static void Initialize()
        {
            if (_isInitialized) return;
            _isInitialized = true;

            try
            {
                // 1. 获取管理器类型
                _managerType = Type.GetType("DuckovCustomModel.Managers.ModelManager, DuckovCustomModel.GameModules");
                
                // 2. 获取数据结构类型 (为了反射字段)
                Type bundleType = Type.GetType("DuckovCustomModel.Core.Data.ModelBundleInfo, DuckovCustomModel.Core");

                if (_managerType != null && bundleType != null)
                {
                    // 3. 获取 ModelBundles 静态列表
                    _bundlesField = _managerType.GetField("ModelBundles", BindingFlags.Public | BindingFlags.Static);
                    
                    // 4. 获取 ModelBundleInfo 内部的 Models 列表字段
                    // 注意：源码中是 public List<ModelInfo> Models { get; set; } 或者是字段
                    // 通常自动属性的背得字段是 <Models>k__BackingField，或者是属性本身
                    _modelsField = bundleType.GetField("Models", BindingFlags.Public | BindingFlags.Instance);
                    if (_modelsField == null)
                    {
                        // 如果是属性，需要用 GetProperty，这里简化逻辑，假设源码中 Models 是字段或属性
                        // 根据你提供的文件，Models 在 LoadFromDirectory 里被赋值，通常是公共属性
                        PropertyInfo prop = bundleType.GetProperty("Models", BindingFlags.Public | BindingFlags.Instance);
                        if (prop != null)
                        {
                            // 为了后续方便，这里做一个简单的包装，实际代码中可能需要分别处理
                            // 为保持简洁，这里我们假设能取到。如果取不到，下文 GetModelsFromBundle 会处理
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 初始化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 随机获取一个可用的模型数据
        /// </summary>
        /// <param name="bundleInfo">输出：模型包数据</param>
        /// <param name="modelInfo">输出：模型具体数据</param>
        /// <returns>是否获取成功</returns>
        public static bool TryGetRandomModel(out object bundleInfo, out object modelInfo)
        {
            bundleInfo = null;
            modelInfo = null;
            
            Initialize();
            if (_managerType == null || _bundlesField == null) return false;

            try
            {
                // 1. 获取所有 Bundles 列表
                var bundlesList = _bundlesField.GetValue(null) as IList;
                if (bundlesList == null || bundlesList.Count == 0) return false;

                // 2. 随机选择一个 Bundle
                // 考虑到有的 Bundle 可能没模型，多试几次或者先扁平化列表（这里简单随机）
                for (int i = 0; i < 5; i++) 
                {
                    object randomBundle = bundlesList[Random.Range(0, bundlesList.Count)];
                    if (randomBundle == null) continue;

                    // 3. 获取该 Bundle 下的 Models 列表
                    IList modelsList = GetModelsFromBundle(randomBundle);
                    if (modelsList != null && modelsList.Count > 0)
                    {
                        // 4. 随机选择一个 Model
                        object randomModel = modelsList[Random.Range(0, modelsList.Count)];
                        
                        bundleInfo = randomBundle;
                        modelInfo = randomModel;
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogTag} 获取随机模型失败: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// 通过 ID 获取指定模型
        /// </summary>
        public static bool TryGetModelByID(string targetID, out object bundleInfo, out object modelInfo)
        {
            bundleInfo = null;
            modelInfo = null;
            Initialize();

            if (_managerType == null) return false;

            try
            {
                // ModelManager.FindModelByID 是静态方法，可以直接调用
                // 方法签名: public static bool FindModelByID(string modelID, out ModelBundleInfo? foundModel, out ModelInfo? foundModelInfo)
                
                MethodInfo findMethod = _managerType.GetMethod("FindModelByID", BindingFlags.Public | BindingFlags.Static);
                if (findMethod != null)
                {
                    object[] parameters = new object[] { targetID, null, null };
                    bool result = (bool)findMethod.Invoke(null, parameters);

                    if (result)
                    {
                        bundleInfo = parameters[1];
                        modelInfo = parameters[2];
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogTag} 查找模型 ID '{targetID}' 失败: {ex.Message}");
            }
            return false;
        }

        // 辅助：从 Bundle 对象中反射获取 Models 列表
        private static IList GetModelsFromBundle(object bundleObj)
        {
            if (bundleObj == null) return null;
            Type type = bundleObj.GetType();

            // 尝试获取字段
            FieldInfo field = type.GetField("Models", BindingFlags.Public | BindingFlags.Instance);
            if (field != null) return field.GetValue(bundleObj) as IList;

            // 尝试获取属性
            PropertyInfo prop = type.GetProperty("Models", BindingFlags.Public | BindingFlags.Instance);
            if (prop != null) return prop.GetValue(bundleObj) as IList;

            return null;
        }
    }
}