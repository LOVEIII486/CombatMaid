using HarmonyLib;
using UnityEngine;

namespace CombatMaid.Core.Patches
{
    [HarmonyPatch(typeof(Projectile), "Init", typeof(ProjectileContext))]
    public static class MaidBulletOptimizedPatch
    {
        [HarmonyPostfix]
        static void Postfix(Projectile __instance, ProjectileContext _context)
        {
            if (_context.fromCharacter == null) return;
            var maid = _context.fromCharacter.GetComponentInParent<MaidController>();
            if (maid == null) return;

            var hitLayersField = Traverse.Create(__instance).Field<LayerMask>("hitLayers");
            int maskValue = hitLayersField.Value.value;
        
            // 互动物品
            maskValue &= ~(1 << 8);
            // 部分障碍物：玻璃墙、木架子
            maskValue &= ~(1 << 6);

            hitLayersField.Value = (LayerMask)maskValue;

            __instance.context.ignoreHalfObsticle = true;
            
            //__instance.context.penetrate = 5;
        }
    }
}