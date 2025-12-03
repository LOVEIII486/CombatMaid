using System;
using System.Collections.Generic;
using Duckov.Buffs;
using HarmonyLib;
using UnityEngine;
using CombatMaid.Localization;
using ItemStatsSystem; // 确保引用了本地化管理器

namespace CombatMaid.Core.BuffsSystem
{
    // 拦截 Buff.Setup
    [HarmonyPatch(typeof(Buff), "Setup")]
    public static class MaidBuffSetupPatch
    {
        // 前缀：清空原生特效
        [HarmonyPrefix]
        public static void Prefix(Buff __instance, ref List<Effect> ___effects)
        {
            if (IsMaidBuff(__instance.name))
            {
                ___effects.Clear(); // 核心：移除所有原生 Effect
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
                // 1. 执行自定义逻辑
                effect.OnBuffSetup(__instance, target);
                
                // 2. 尝试设置本地化名称 (Key 示例: "Buff_MaidBuff_Berserk_Name")
                string locKey = $"Buff_{buffName}_Name";
                string localizedName = LocalizationManager.GetText(locKey, buffName);
                ___displayName = localizedName;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CombatMaid] Buff Setup Error ({buffName}): {ex}");
            }
        }

        // 辅助方法：判断是否是本模组的 Buff
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
                    Debug.LogError($"[CombatMaid] Buff Destroy Error ({buffName}): {ex}");
                }
            }
            
            // 自动清理该 Buff 注册的所有数值修改器
            // 只要你是通过 MaidBuffModifierManager 注册的 Modifier，这里都会自动移除
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