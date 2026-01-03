using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using FMOD.Studio;

namespace CombatMaid.Core.CustomModel
{
    /// <summary>
    /// 专门负责接管 DCM (DuckovCustomModel) 声音系统并应用自定义音量的补丁。
    /// 遵循安全反射规范，缺失前置时静默不报错。
    /// </summary>
    public static class CustomModelAudioPatcher
    {
        private const string TargetAssemblyName = "DuckovCustomModel";
        private const string TargetTypeName = "DuckovCustomModel.MonoBehaviours.ModelHandler";
        private const string TargetMethodName = "PlaySound";

        private static Harmony _harmony;
        private static bool _isPatched;
        private static PropertyInfo _characterMainControlProp;

        /// <summary>
        /// 全局女仆音量倍率 (0.0 ~ 1.0)
        /// </summary>
        public static float GlobalMaidVolume { get; set; } = 1.0f;

        public static void Initialize()
        {
            if (_isPatched) return;

            try
            {
                // 1. 动态定位程序集和类型
                var assembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == TargetAssemblyName);
                
                if (assembly == null) return; // 缺失前置，安全退出

                var modelHandlerType = assembly.GetType(TargetTypeName);
                if (modelHandlerType == null) return;

                // 2. 获取关键成员
                _characterMainControlProp = modelHandlerType.GetProperty("CharacterMainControl", 
                    BindingFlags.Public | BindingFlags.Instance);
                
                var playSoundMethod = modelHandlerType.GetMethod(TargetMethodName, 
                    BindingFlags.Public | BindingFlags.Instance);

                if (_characterMainControlProp == null || playSoundMethod == null)
                {
                    CMDebug.LogWarning("[AudioPatch] 无法定位 DCM 关键接口，音量控制失效。");
                    return;
                }

                // 3. 应用 Harmony 补丁
                _harmony = new Harmony("com.combatmaid.audiopatch");
                var postfix = typeof(CustomModelAudioPatcher).GetMethod(nameof(PlaySoundPostfix), 
                    BindingFlags.Static | BindingFlags.NonPublic);
                
                _harmony.Patch(playSoundMethod, postfix: new HarmonyMethod(postfix));
                
                _isPatched = true;
                CMDebug.Log("[AudioPatch] 成功挂载女仆音量拦截器。");
            }
            catch (Exception ex)
            {
                CMDebug.LogError($"[AudioPatch] 初始化异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 拦截 DCM 的 PlaySound 返回值，应用音量
        /// </summary>
        private static void PlaySoundPostfix(object __result, MonoBehaviour __instance)
        {
            // __result 是 EventInstance? (Nullable<EventInstance>)
            if (__result == null) return;

            if (!IsMyMaid(__instance)) return;

            try
            {
                // 通过反射或直接拆箱获取 FMOD 实例
                // 由于我们不引用 DLL，这里 __result 表现为 object，需要动态处理
                var resultType = __result.GetType();
                var hasValueProp = resultType.GetProperty("HasValue");
                
                if (hasValueProp != null && (bool)hasValueProp.GetValue(__result))
                {
                    var valueProp = resultType.GetProperty("Value");
                    var instance = (EventInstance)valueProp.GetValue(__result);

                    if (instance.isValid())
                    {
                        instance.setVolume(GlobalMaidVolume);
                        // 仅在高调试模式下开启此日志，避免刷屏
                        // CMDebug.Log($"[AudioPatch] 调整音频实例音量 -> {GlobalMaidVolume}"); 
                    }
                }
            }
            catch (Exception ex)
            {
                // 静默处理 FMOD 实例可能的失效异常
                CMDebug.LogWarning($"[AudioPatch] 设置音量失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 判断当前的 ModelHandler 是否属于战斗女仆
        /// </summary>
        private static bool IsMyMaid(MonoBehaviour handler)
        {
            if (handler == null || _characterMainControlProp == null) return false;

            try
            {
                // 从 ModelHandler.CharacterMainControl 获取引用
                var charCtrl = _characterMainControlProp.GetValue(handler) as Component;
                if (charCtrl == null) return false;

                // 核心逻辑：检查该对象是否挂载了女仆控制组件
                // GetComponent 是经过 Unity 优化的，在非 Update 的事件驱动中调用开销极低
                return charCtrl.GetComponent<MaidController>() != null;
            }
            catch
            {
                return false;
            }
        }

        public static void Unpatch()
        {
            if (!_isPatched) return;
            _harmony?.UnpatchAll("com.combatmaid.audiopatch");
            _isPatched = false;
            CMDebug.Log("[AudioPatch] 音量拦截器已卸载。");
        }
    }
}