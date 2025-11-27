using HarmonyLib;
using NodeCanvas.Tasks.Actions; // 注意：这是游戏原有 AI 逻辑的命名空间
using UnityEngine;
using CombatMaid.MaidBehaviors;

namespace CombatMaid.Core
{
    [HarmonyPatch]
    public static class HarmonyPatches
    {
        // 辅助方法：检查该 AI 是否被我们的 Mod 接管且正处于强制执行状态
        private static bool ShouldBlockNativeAI(AICharacterController ai)
        {
            if (ai == null) return false;

            // 获取我们挂载的控制器
            var controller = ai.GetComponent<MaidController>();
            
            // 如果控制器存在，且正处于 Override 状态，则返回 true (阻断)
            return controller != null && controller.IsOverrideActive;
        }

        // 1. 拦截“追踪目标”逻辑 (TraceTarget)
        // 原理：如果我们在强制移动，不要让 AI 自动检测“离玩家太远”并跑回来
        [HarmonyPatch(typeof(TraceTarget), "OnUpdate")]
        [HarmonyPrefix]
        public static bool TraceTargetUpdatePrefix(TraceTarget __instance)
        {
            // 返回 false 表示“跳过原方法”，即阻断
            return !ShouldBlockNativeAI(__instance.agent);
        }

        // 2. 拦截“停止移动”逻辑 (StopMoving)
        // 原理：防止原生 AI 在我们移动时突然下达停车指令
        [HarmonyPatch(typeof(StopMoving), "OnExecute")]
        [HarmonyPrefix]
        public static bool StopMovingExecutePrefix(StopMoving __instance)
        {
            return !ShouldBlockNativeAI(__instance.agent);
        }
    }
}