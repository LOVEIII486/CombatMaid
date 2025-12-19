using System;
using System.Collections.Generic;
using Duckov.Buffs;
using HarmonyLib;
using UnityEngine;
using CombatMaid.Localization;
using ItemStatsSystem;

namespace CombatMaid.Core.BuffsSystem
{
    [HarmonyPatch(typeof(Buff), "Setup")]
    public static class MaidBuffSetupPatch
    {
        // 前缀：清空原生特效
        [HarmonyPrefix]
        public static void Prefix(Buff __instance, ref List<Effect> ___effects)
        {
            if (IsMaidBuff(__instance.name))
            {
                ___effects.Clear(); // 移除所有原生 Effect
            }
        }

        // 后缀：执行自定义逻辑
        [HarmonyPostfix]
        public static void Postfix(Buff __instance, ref string ___displayName, CharacterBuffManager manager)
        {
            string buffName = ExtractBuffName(__instance.name);
            if (buffName == null) return;

            var effect = MaidBuffRegistry.Instance.GetEffect(buffName);
            if (effect == null) return;

            var target = manager?.Master;
            if (target == null) return;

            try
            {
                // 执行自定义逻辑
                effect.OnBuffSetup(__instance, target);
            }
            catch (Exception ex)
            {
                CMDebug.LogError($"Buff Setup Error ({buffName}): {ex}");
            }
        }

        // 断是否是本模组的 Buff
        private static bool IsMaidBuff(string name) => name != null && name.StartsWith("MaidBuff_");

        private static string ExtractBuffName(string fullName)
        {
            if (fullName == null) return null;
            string cleanName = fullName.Replace("(Clone)", "").Trim();
            return MaidBuffRegistry.Instance.IsRegistered(cleanName) ? cleanName : null;
        }
    }

    // 拦截 Buff.OnDestroy
    [HarmonyPatch(typeof(Buff), "OnDestroy")]
    public static class MaidBuffDestroyPatch
    {
        [HarmonyPrefix]
        public static void Prefix(Buff __instance)
        {
            string buffName = ExtractBuffName(__instance.name);
            if (buffName == null) return;

            var effect = MaidBuffRegistry.Instance.GetEffect(buffName);
            if (effect != null)
            {
                try
                {
                    effect.OnBuffDestroy(__instance, __instance.Character);
                }
                catch (Exception ex)
                {
                    CMDebug.LogError($"Buff Destroy Error ({buffName}): {ex}");
                }
            }
            
            // 自动清理该 Buff 注册的所有数值修改器
            MaidBuffModifierManager.Instance.CleanupModifiers(__instance.GetInstanceID());
        }

        private static string ExtractBuffName(string fullName)
        {
            if (fullName == null) return null;
            string cleanName = fullName.Replace("(Clone)", "").Trim();
            return MaidBuffRegistry.Instance.IsRegistered(cleanName) ? cleanName : null;
        }
    }
}