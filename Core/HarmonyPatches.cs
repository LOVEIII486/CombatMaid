using HarmonyLib;
using NodeCanvas.Tasks.Actions; 
using UnityEngine;
using CombatMaid.Core.MaidBehaviors;

namespace CombatMaid.Core
{
    /// <summary>
    /// Harmony 补丁 - 极简版
    /// 
    /// 核心策略：
    /// 1. 不再使用和平模式
    /// 2. 只拦截手动移动（G键）期间的寻路
    /// 3. 让 AI 的战斗系统完全正常工作
    /// 4. 依赖官方的 leader 和 patrolPosition 系统实现跟随
    /// </summary>
    [HarmonyPatch]
    public static class HarmonyPatches
    {
        // ==================== 核心判断方法 ====================
        
        /// <summary>
        /// 只在手动移动时拦截
        /// </summary>
        private static bool ShouldBlockNativeAI(AICharacterController ai)
        {
            var controller = MaidController.GetMaid(ai);
            if (controller == null) return false;
            
            // 只拦截手动移动
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