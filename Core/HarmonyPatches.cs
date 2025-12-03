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
    }
}