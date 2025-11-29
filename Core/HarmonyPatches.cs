using HarmonyLib;
using NodeCanvas.Tasks.Actions; 
using UnityEngine;
using CombatMaid.Core.MaidBehaviors;

namespace CombatMaid.Core
{
    [HarmonyPatch]
    public static class HarmonyPatches
    {
        private static bool ShouldBlockNativeAI(AICharacterController ai)
        {
            var controller = MaidController.GetMaid(ai);
            if (controller == null) return false;
            return controller.IsOverrideActive;
        }

        // ==================== 寻路拦截（仅手动移动时）====================
        
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
    }
}