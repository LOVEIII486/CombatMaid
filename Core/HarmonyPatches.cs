using HarmonyLib;
using NodeCanvas.Tasks.Actions; 
using UnityEngine;
using CombatMaid.Core.MaidBehaviors;

namespace CombatMaid.Core
{
    [HarmonyPatch]
    public static class HarmonyPatches
    {
        // 辅助方法：快速获取 MaidController 并判断是否处于和平模式
        private static bool IsInPeaceMode(AICharacterController ai)
        {
            if (ai == null) return false;
            // 尝试获取我们挂载的控制器
            var controller = ai.GetComponent<MaidController>();
            return controller != null && controller.IsPeaceMode;
        }

        // ==================== 原有的移动拦截逻辑 ====================
        private static bool ShouldBlockNativeAI(AICharacterController ai)
        {
            if (ai == null) return false;
            var controller = ai.GetComponent<MaidController>();
            // 如果 Movement 模块正在强制移动，或者处于和平模式，都应该屏蔽原生 AI 寻路逻辑
            return controller != null && (controller.IsOverrideActive || controller.IsPeaceMode);
        }

        [HarmonyPatch(typeof(TraceTarget), "OnExecute")]
        [HarmonyPrefix]
        public static bool TraceTargetExecutePrefix(TraceTarget __instance)
        {
            return !ShouldBlockNativeAI(__instance.agent);
        }

        [HarmonyPatch(typeof(TraceTarget), "OnUpdate")]
        [HarmonyPrefix]
        public static bool TraceTargetUpdatePrefix(TraceTarget __instance)
        {
            return !ShouldBlockNativeAI(__instance.agent);
        }
        
        [HarmonyPatch(typeof(TraceTarget), "OnStop")]
        [HarmonyPrefix]
        public static bool TraceTargetStopPrefix(TraceTarget __instance)
        {
            return !ShouldBlockNativeAI(__instance.agent);
        }

        [HarmonyPatch(typeof(StopMoving), "OnExecute")]
        [HarmonyPrefix]
        public static bool StopMovingExecutePrefix(StopMoving __instance)
        {
            return !ShouldBlockNativeAI(__instance.agent);
        }

        // ==================== 新增：索敌抑制逻辑 ====================

        // 1. Postfix 拦截 Update：在原生逻辑跑完后，强行清空索敌数据
        // 这样可以覆盖掉 Update 内部通过 leaderAI 或 forceTracePlayerDistance 重新赋值的 searchedEnemy
        [HarmonyPatch(typeof(AICharacterController), "Update")]
        [HarmonyPostfix]
        public static void AIUpdatePostfix(AICharacterController __instance)
        {
            if (IsInPeaceMode(__instance))
            {
                // 强行“洗脑”
                __instance.searchedEnemy = null;
                __instance.aimTarget = null;
                __instance.alert = false;
                __instance.noticed = false;
            }
        }

        // 2. Prefix 拦截 SetNoticedToTarget：彻底禁止进入“注意到”状态
        // 防止因为受伤(OnHurt)或听到声音(OnSound)而被动进入战斗
        [HarmonyPatch(typeof(AICharacterController), "SetNoticedToTarget")]
        [HarmonyPrefix]
        public static bool SetNoticedToTargetPrefix(AICharacterController __instance)
        {
            // 如果处于和平模式，返回 false (拦截原方法执行)
            return !IsInPeaceMode(__instance);
        }
        
        // 3. (可选) 拦截 TakeOutWeapon：和平模式下禁止自动拔枪
        [HarmonyPatch(typeof(AICharacterController), "TakeOutWeapon")]
        [HarmonyPrefix]
        public static bool TakeOutWeaponPrefix(AICharacterController __instance)
        {
            return !IsInPeaceMode(__instance);
        }
    }
}