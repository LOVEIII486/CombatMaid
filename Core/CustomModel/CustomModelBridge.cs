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

                CMDebug.Log($"=== 可用模型列表 ===");
                foreach (object bundle in bundles)
                {
                    string bundleName = GetPropString(bundle, _bundleNameProp) ?? "Unknown";
                    IList models = GetList(bundle, _modelsProp);

                    if (models != null)
                    {
                        foreach (object model in models)
                        {
                            CMDebug.Log($">>> 包名: [{bundleName}] | ID: [{GetModelID(model)}]");
                        }
                    }
                }
                CMDebug.Log($"======================");
            }
            catch (Exception ex) { CMDebug.LogError($"列出模型失败: {ex.Message}"); }
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
            catch (Exception ex) { CMDebug.LogWarning($"查找模型失败: {ex.Message}"); }

            if (!found)
            {
                CMDebug.LogWarning($"未找到模型 ID: {modelId}");
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

                // [优化] 挂载高性能装备隐藏器
                var hider = target.GetComponent<MaidEquipmentHider>();
                if (hider == null) hider = target.gameObject.AddComponent<MaidEquipmentHider>();
                
                if (target.characterModel != null)
                {
                    hider.Initialize(target.characterModel);
                }

                CMDebug.Log($"模型应用成功: {GetModelID(model)}");
            }
            catch(Exception ex)
            {
                CMDebug.LogError($"应用异常: {ex.Message}");
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
                    CMDebug.Log($"模组连接成功");
                    CustomModelAudioPatcher.Initialize(); 
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

        private static string GetModelID(object obj)
        {
            if (obj == null) return "null";
            return (_modelIdProp?.GetValue(obj) ?? _modelIdField?.GetValue(obj))?.ToString() ?? "Unknown";
        }

        private static string GetPropString(object obj, PropertyInfo prop) => prop?.GetValue(obj)?.ToString();
        private static IList GetList(object obj, PropertyInfo prop) => prop?.GetValue(obj) as IList;
    }

    // ===================================================================================
    //  高性能装备隐藏组件 (Watcher 模式)
    // ===================================================================================

    /// <summary>
    /// 挂载在角色身上，持续监测并强制隐藏原版装备（头盔、护甲、面罩、背包）
    /// 采用缓存比对机制，极大降低每帧开销
    /// </summary>
    public class MaidEquipmentHider : MonoBehaviour
    {
        // 内部类：负责监控单个插槽
        // 内部类：负责监控单个插槽
        private class SocketWatcher
        {
            private Transform _socket;
            
            // 缓存状态用于检测变化
            private int _lastChildCount = -1;
            private Transform _lastFirstChild = null;
            
            // 缓存渲染器列表
            private Renderer[] _cachedRenderers;

            public SocketWatcher(Transform socket)
            {
                _socket = socket;
            }

            public void Update()
            {
                if (_socket == null) return;

                int currentCount = _socket.childCount;

                // 1. 如果插槽为空，清理缓存
                if (currentCount == 0)
                {
                    _cachedRenderers = null;
                    _lastChildCount = 0;
                    _lastFirstChild = null;
                    return;
                }

                Transform currentFirst = _socket.GetChild(0);

                // 2. 检测变化 (核心修复点)
                // 只要子物体数量变了，或者第一个子物体换了，就视为装备发生了变动
                // 这比之前只盯着 GetChild(0) 更稳健
                if (currentCount != _lastChildCount || currentFirst != _lastFirstChild)
                {
                    _lastChildCount = currentCount;
                    _lastFirstChild = currentFirst;

                    // [关键修改] 直接从 _socket 本身获取所有层级的渲染器
                    // 这样无论头盔由几个物体组成，或者模型在第几个子级，都能被抓取到
                    _cachedRenderers = _socket.GetComponentsInChildren<Renderer>(true);
                    
                    // CMDebug.Log($"[{_socket.name}] 装备更新，重新缓存 {(_cachedRenderers?.Length ?? 0)} 个渲染器");
                }

                // 3. 强制关闭渲染器 (极低开销)
                if (_cachedRenderers != null)
                {
                    for (int i = 0; i < _cachedRenderers.Length; i++)
                    {
                        var r = _cachedRenderers[i];
                        // 必须判空，因为切换场景时 Renderer 可能被销毁但缓存还在
                        if (r != null && r.enabled)
                        {
                            r.enabled = false;
                        }
                    }
                }
            }
        }

        private SocketWatcher[] _watchers;

        public void Initialize(CharacterModel model)
        {
            if (model == null) return;
            
            // 为每个需要隐藏的部位创建一个观察者
            _watchers = new SocketWatcher[] 
            {
                new SocketWatcher(model.HelmatSocket),      // 头盔
                new SocketWatcher(model.ArmorSocket),       // 护甲
                new SocketWatcher(model.FaceMaskSocket),    // 面罩
                new SocketWatcher(model.BackpackSocket)     // 背包
            };
        }

        private void LateUpdate()
        {
            if (_watchers == null) return;

            // 轮询所有观察者
            for (int i = 0; i < _watchers.Length; i++)
            {
                _watchers[i].Update();
            }
        }
    }
}