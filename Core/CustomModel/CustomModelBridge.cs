using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using System.Collections.Generic;

namespace CombatMaid.Core.CustomModel
{
    public static class CustomModelBridge
    {
        private static bool _isInitialized = false;
        private static bool _isModAvailable = false;
        private static bool _hasWarnedMissing = false;

        private static Type _modelHandlerType; 
        private static Type _bundleType;
        private static Type _modelInfoType;
        
        private static FieldInfo _bundlesField;
        private static PropertyInfo _modelsProp;
        private static PropertyInfo _bundleNameProp;
        private static PropertyInfo _modelIdProp;
        private static FieldInfo _modelIdField;      
        
        private static MethodInfo _findMethod;
        private static MethodInfo _initMethod;
        private static MethodInfo _loadMethod;
        private static MethodInfo _changeMethod;
        
        private static object _aiTargetEnumValue;

        public static bool IsAvailable()
        {
            if (!_isInitialized) InitializeReflection();
            return _isModAvailable;
        }

        public static void LogAvailableModels()
        {
            if (!IsAvailable()) 
            {
                WarnMissingMod();
                return;
            }

            try
            {
                var bundles = _bundlesField.GetValue(null) as IList;
                if (bundles == null || bundles.Count == 0) return;

                CMDebug.Log($"=== 可用模型列表 ===");
                foreach (object bundle in bundles)
                {
                    if (bundle == null) continue;
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
            }
            catch (Exception ex) { CMDebug.LogWarning($"[CustomModel] 列出模型时发生非致命异常: {ex.Message}"); }
        }

        public static IEnumerator ApplyModelByIDAsync(CharacterMainControl target, string modelId)
        {
            if (!IsAvailable())
            {
                WarnMissingMod();
                yield break;
            }

            if (target == null || target.gameObject == null) yield break;

            object bundle = null;
            object model = null;
            bool found = false;

            try
            {
                object[] args = new object[] { modelId, null, null };
                found = (bool)_findMethod.Invoke(null, args);
                if (found)
                {
                    bundle = args[1];
                    model = args[2];
                }
            }
            catch (Exception ex) { CMDebug.LogError($"[CustomModel] 查找模型 ID [{modelId}] 失败: {ex.Message}"); }

            if (!found)
            {
                CMDebug.LogWarning($"[CustomModel] 库中未找到模型 ID: {modelId}");
                yield break;
            }

            yield return null; 

            if (target == null || target.gameObject == null) yield break;

            ApplyModelInternal(target, bundle, model);
        }

        private static void ApplyModelInternal(CharacterMainControl target, object bundle, object model)
        {
            try 
            {
                if (_modelHandlerType == null) return;

                Component handler = target.GetComponent(_modelHandlerType);
                if (handler == null) 
                {
                    handler = target.gameObject.AddComponent(_modelHandlerType);
                }

                if (handler == null) throw new Exception("无法在角色对象上创建 ModelHandler 组件");

                // 尝试清洗导致报错的 LootBox 字段
                try { SanitizeDeathLootBox(model); } catch { }

                _initMethod.Invoke(handler, new object[] { target, _aiTargetEnumValue });
                _loadMethod.Invoke(handler, new object[] { bundle, model });
                _changeMethod.Invoke(handler, null);

                // 只有成功更换模型后，才挂载隐藏器
                var hider = target.GetComponent<MaidEquipmentHider>() ?? target.gameObject.AddComponent<MaidEquipmentHider>();
                
                if (target.characterModel != null)
                {
                    hider.Initialize(target.characterModel);
                }

                CMDebug.Log($"[CustomModel] 模型应用成功: {GetModelID(model)}");
            }
            catch(Exception ex)
            {
                CMDebug.LogError($"[CustomModel] 应用模型逻辑崩溃: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static void InitializeReflection()
        {
            if (_isInitialized) return;
            _isInitialized = true;

            try
            {
                Type managerType = Type.GetType("DuckovCustomModel.Managers.ModelManager, DuckovCustomModel.GameModules");
                _modelHandlerType = Type.GetType("DuckovCustomModel.MonoBehaviours.ModelHandler, DuckovCustomModel.GameModules");
                _bundleType = Type.GetType("DuckovCustomModel.Core.Data.ModelBundleInfo, DuckovCustomModel.Core");
                _modelInfoType = Type.GetType("DuckovCustomModel.Core.Data.ModelInfo, DuckovCustomModel.Core");
                Type targetEnum = Type.GetType("DuckovCustomModel.Core.Data.ModelTarget, DuckovCustomModel.Core");

                if (managerType == null || _modelHandlerType == null || targetEnum == null) return;

                _bundlesField = managerType.GetField("ModelBundles", BindingFlags.Public | BindingFlags.Static);
                _findMethod = managerType.GetMethod("FindModelByID", BindingFlags.Public | BindingFlags.Static);
                
                _bundleNameProp = _bundleType?.GetProperty("BundleName");
                _modelsProp = _bundleType?.GetProperty("Models");

                _modelIdProp = _modelInfoType?.GetProperty("ModelID");
                _modelIdField = _modelInfoType?.GetField("ModelID");

                _initMethod = _modelHandlerType.GetMethod("Initialize", new Type[] { typeof(CharacterMainControl), targetEnum });
                _loadMethod = _modelHandlerType.GetMethod("InitializeCustomModel", new Type[] { _bundleType, _modelInfoType });
                _changeMethod = _modelHandlerType.GetMethod("ChangeToCustomModel");

                try { _aiTargetEnumValue = Enum.Parse(targetEnum, "AICharacter"); }
                catch { _aiTargetEnumValue = 0; }
                
                if (_findMethod != null && _initMethod != null && _loadMethod != null)
                {
                    _isModAvailable = true;
                    CMDebug.Log($"[CustomModel] DuckovCustomModel 桥接初始化成功。");
                    CustomModelAudioPatcher.Initialize(); 
                }
            }
            catch (Exception ex)
            {
                CMDebug.LogWarning($"[CustomModel] 反射初始化期间发生错误 (可能是模组版本不兼容): {ex.Message}");
            }
        }

        private static void WarnMissingMod()
        {
            if (!_hasWarnedMissing)
            {
                CMDebug.LogWarning("[CustomModel] 桥接器未激活：未检测到 DuckovCustomModel 模组或版本不匹配。自定义外观功能已禁用。");
                _hasWarnedMissing = true;
            }
        }

        private static void SanitizeDeathLootBox(object modelInfo)
        {
            if (modelInfo == null) return;
            var props = modelInfo.GetType().GetProperties();
            foreach (var p in props)
            {
                if (p.Name != null && p.Name.Contains("DeathLootBox") && p.PropertyType == typeof(string) && p.CanWrite)
                {
                    p.SetValue(modelInfo, null);
                }
            }
        }

        private static string GetModelID(object obj)
        {
            if (obj == null) return "null";
            try
            {
                return (_modelIdProp?.GetValue(obj) ?? _modelIdField?.GetValue(obj))?.ToString() ?? "Unknown";
            }
            catch { return "Error"; }
        }

        private static string GetPropString(object obj, PropertyInfo prop) => prop?.GetValue(obj)?.ToString();
        private static IList GetList(object obj, PropertyInfo prop) => prop?.GetValue(obj) as IList;
    }

    public class MaidEquipmentHider : MonoBehaviour
    {
        private class SocketWatcher
        {
            private Transform _socket;
            private int _lastChildCount = -1;
            private Transform _lastFirstChild = null;
            private Renderer[] _cachedRenderers;

            public SocketWatcher(Transform socket)
            {
                _socket = socket;
            }

            public void Update()
            {
                if (_socket == null) return;

                int currentCount = _socket.childCount;
                if (currentCount == 0)
                {
                    if (_lastChildCount != 0)
                    {
                        _cachedRenderers = null;
                        _lastChildCount = 0;
                        _lastFirstChild = null;
                    }
                    return;
                }

                Transform currentFirst = _socket.GetChild(0);

                if (currentCount != _lastChildCount || currentFirst != _lastFirstChild)
                {
                    _lastChildCount = currentCount;
                    _lastFirstChild = currentFirst;
                    _cachedRenderers = _socket.GetComponentsInChildren<Renderer>(true);
                }

                if (_cachedRenderers != null)
                {
                    for (int i = 0; i < _cachedRenderers.Length; i++)
                    {
                        var r = _cachedRenderers[i];
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
            
            var tempWatchers = new List<SocketWatcher>();
            
            if (model.HelmatSocket != null) tempWatchers.Add(new SocketWatcher(model.HelmatSocket));
            if (model.ArmorSocket != null) tempWatchers.Add(new SocketWatcher(model.ArmorSocket));
            if (model.FaceMaskSocket != null) tempWatchers.Add(new SocketWatcher(model.FaceMaskSocket));
            if (model.BackpackSocket != null) tempWatchers.Add(new SocketWatcher(model.BackpackSocket));

            _watchers = tempWatchers.ToArray();
        }

        private void LateUpdate()
        {
            if (_watchers == null) return;

            for (int i = 0; i < _watchers.Length; i++)
            {
                _watchers[i].Update();
            }
        }
    }
}