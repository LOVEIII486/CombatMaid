using HarmonyLib;
using NodeCanvas.Tasks.Actions; 
using UnityEngine;
using CombatMaid.MaidBehaviors;

namespace CombatMaid.Core
{
    [HarmonyPatch]
    public static class HarmonyPatches
    {
        // 核心判断逻辑保持不变：依然是读取实例的 Controller 状态
        private static bool ShouldBlockNativeAI(AICharacterController ai)
        {
            if (ai == null) return false;
            var controller = ai.GetComponent<MaidController>();
            // 桥接到了 MaidMovement
            return controller != null && controller.IsOverrideActive;
        }

        // ========================================================
        // 1. 全面拦截 TraceTarget (追踪/跟随)
        // 学习自 spawn.cs，拦截生命周期的所有阶段，防止“偷跑”
        // ========================================================

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
        
        // 某些情况下，强行结束当前帧的逻辑可能有助于稳定性
        [HarmonyPatch(typeof(TraceTarget), "OnStop")]
        [HarmonyPrefix]
        public static bool TraceTargetStopPrefix(TraceTarget __instance)
        {
            return !ShouldBlockNativeAI(__instance.agent);
        }

        // ========================================================
        // 2. 拦截 StopMoving (防止原生逻辑突然刹车)
        // ========================================================

        [HarmonyPatch(typeof(StopMoving), "OnExecute")]
        [HarmonyPrefix]
        public static bool StopMovingExecutePrefix(StopMoving __instance)
        {
            return !ShouldBlockNativeAI(__instance.agent);
        }
    }
}