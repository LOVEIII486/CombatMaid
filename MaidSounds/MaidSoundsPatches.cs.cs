using HarmonyLib;
using System;
using Duckov;
using FMOD.Studio;

namespace CombatMaid.MaidSounds
{
    /// <summary>
    /// 拦截 AudioObject.PostQuak 的补丁
    /// </summary>
    internal static class MaidSoundsPatches
    {
        [HarmonyPatch(typeof(AudioObject))]
        public static class AudioObject_PostQuak_Patch
        {
            [HarmonyPatch("PostQuak", new Type[] { typeof(string) })]
            [HarmonyPostfix]
            private static void Postfix(AudioObject __instance, string soundKey, ref EventInstance? __result)
            {
                // 1. 基础检查
                if (__instance == null || string.IsNullOrEmpty(soundKey)) return;

                var go = __instance.gameObject;
                if (go == null) return;

                // 2. 委托给 Manager 判断是否需要替换 (检查 Key 和 MaidController)
                if (MaidSoundManager.ShouldReplace(go, soundKey))
                {
                    // 3. 尝试播放自定义语音
                    if (MaidSoundManager.TryPlayMaidSound(go, soundKey))
                    {
                        // 4. 如果替换成功，静音原版事件
                        if (__result.HasValue && __result.Value.isValid())
                        {
                            __result.Value.setVolume(0f);
                        }
                    }
                }
            }
        }
    }
}