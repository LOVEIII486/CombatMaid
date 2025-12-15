using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using FMOD.Studio;

namespace CombatMaid.Core.CustomModel
{
    /// <summary>
    /// 用于接管并控制 DCM 模组声音的补丁
    /// </summary>
    public static class CustomModelAudioPatcher
    {
        private static Harmony _harmony;
        private static bool _isPatched = false;

        private static PropertyInfo _characterMainControlProp;

        // 全局音量控制 
        public static float GlobalMaidVolume { get; set; } = 1.0f;

        public static void Initialize()
        {
            if (_isPatched) return;

            try
            {
                Type modelHandlerType = Type.GetType("DuckovCustomModel.MonoBehaviours.ModelHandler, DuckovCustomModel.GameModules");
                if (modelHandlerType == null)
                {
                    CMDebug.LogWarning("未找到 ModelHandler，音量控制模块跳过初始化。");
                    return;
                }

                _characterMainControlProp = modelHandlerType.GetProperty("CharacterMainControl", BindingFlags.Public | BindingFlags.Instance);

                // 获取 PlaySound 方法
                MethodInfo playSoundMethod = modelHandlerType.GetMethod("PlaySound", BindingFlags.Public | BindingFlags.Instance);
                
                if (playSoundMethod == null || _characterMainControlProp == null)
                {
                    CMDebug.LogError("无法获取 ModelHandler 的关键成员，音量补丁初始化失败。");
                    return;
                }

                _harmony = new Harmony("com.combatmaid.audiopatch");
                var postfix = typeof(CustomModelAudioPatcher).GetMethod(nameof(PlaySoundPostfix), BindingFlags.Static | BindingFlags.NonPublic);
                
                _harmony.Patch(playSoundMethod, postfix: new HarmonyMethod(postfix));
                _isPatched = true;
                
                CMDebug.Log("战斗女仆: 音量控制补丁已应用");
            }
            catch (Exception ex)
            {
                CMDebug.LogError($"应用音量补丁时发生异常: {ex}");
            }
        }

        public static void Unpatch()
        {
            if (!_isPatched) return;
            _harmony?.UnpatchAll("com.combatmaid.audiopatch");
            _isPatched = false;
            _characterMainControlProp = null;
        }

        /// <summary>
        /// 后置补丁：拦截声音实例并修改音量
        /// </summary>
        private static void PlaySoundPostfix(EventInstance? __result, MonoBehaviour __instance)
        {
            if (__result == null || !__result.Value.isValid()) return;

            if (!IsMyMaid(__instance)) return;

            try
            {
                __result.Value.setVolume(GlobalMaidVolume);
                CMDebug.Log($"[AudioPatch] 已调整女仆音量: {GlobalMaidVolume}");
            }
            catch { }
        }
        
        private static bool IsMyMaid(MonoBehaviour handler)
        {
            if (handler == null || _characterMainControlProp == null) return false;

            try
            {
                // 1. 通过反射从 ModelHandler 获取 CharacterMainControl
                var character = _characterMainControlProp.GetValue(handler) as CharacterMainControl;
                
                if (character == null) return false;

                // 2. 检查该角色身上是否有 CombatMaid 的核心组件
                // 只要角色挂载了 MaidController，就认定为我们的控制对象
                var maidController = character.GetComponent<MaidController>();
                return maidController != null;
            }
            catch (Exception)
            {
                // 反射取值出错时安全返回 false
                return false;
            }
        }
    }
}