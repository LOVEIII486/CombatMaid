using HarmonyLib;
using NodeCanvas.Tasks.Actions; 
using UnityEngine;
using CombatMaid.Core.MaidBehaviors;

namespace CombatMaid.Core
{
    [HarmonyPatch]
    public static class HarmonyPatches
    {
        // 辅助方法：使用字典查表，速度极快 O(1)
        private static bool IsInPeaceMode(AICharacterController ai)
        {
            var controller = MaidController.GetMaid(ai); // 优化点
            return controller != null && controller.IsPeaceMode;
        }

        private static bool ShouldBlockNativeAI(AICharacterController ai)
        {
            var controller = MaidController.GetMaid(ai); // 优化点
            // 如果字典里查不到，说明这就不是个女仆，直接返回 false，开销极低
            return controller != null && (controller.IsOverrideActive || controller.IsPeaceMode);
        }

        // --- 寻路拦截 (保持逻辑不变，底层已优化) ---
        [HarmonyPatch(typeof(TraceTarget), "OnExecute")]
        [HarmonyPrefix]
        public static bool TraceTargetExecutePrefix(TraceTarget __instance) => !ShouldBlockNativeAI(__instance.agent);

        [HarmonyPatch(typeof(TraceTarget), "OnUpdate")]
        [HarmonyPrefix]
        public static bool TraceTargetUpdatePrefix(TraceTarget __instance) => !ShouldBlockNativeAI(__instance.agent);
        
        [HarmonyPatch(typeof(TraceTarget), "OnStop")]
        [HarmonyPrefix]
        public static bool TraceTargetStopPrefix(TraceTarget __instance) => !ShouldBlockNativeAI(__instance.agent);

        [HarmonyPatch(typeof(StopMoving), "OnExecute")]
        [HarmonyPrefix]
        public static bool StopMovingExecutePrefix(StopMoving __instance) => !ShouldBlockNativeAI(__instance.agent);

        // --- 索敌抑制 ---

        [HarmonyPatch(typeof(AICharacterController), "Update")]
        [HarmonyPostfix]
        public static void AIUpdatePostfix(AICharacterController __instance)
        {
            // 这里现在非常高效，全图几百个敌人调用也没压力
            if (IsInPeaceMode(__instance))
            {
                __instance.searchedEnemy = null;
                __instance.aimTarget = null;
                __instance.alert = false;
                __instance.noticed = false;
            }
        }

        [HarmonyPatch(typeof(AICharacterController), "SetNoticedToTarget")]
        [HarmonyPrefix]
        public static bool SetNoticedToTargetPrefix(AICharacterController __instance)
        {
            return !IsInPeaceMode(__instance);
        }
        
        [HarmonyPatch(typeof(AICharacterController), "TakeOutWeapon")]
        [HarmonyPrefix]
        public static bool TakeOutWeaponPrefix(AICharacterController __instance)
        {
            return !IsInPeaceMode(__instance);
        }
    }
}