using System.Collections.Generic;
using HarmonyLib;

namespace CombatMaid.Core.Patches
{
    [HarmonyPatch]
    public static class MaidNeutralAIStrategy
    {
        private static readonly HashSet<int> _provokedEntityIds = new HashSet<int>();

        public static bool IsProvoked(DamageReceiver target)
        {
            if (target == null) return false;
            return _provokedEntityIds.Contains(target.GetInstanceID());
        }

        public static void MarkAsEnemy(DamageReceiver target)
        {
            if (target == null) return;
            int id = target.GetInstanceID();
            if (!_provokedEntityIds.Contains(id))
            {
                _provokedEntityIds.Add(id);
                CMDebug.Log($"目标 {target.name} (Team: {target.Team}) 已标记为敌对");
            }
        }

        public static void ClearProvocations() => _provokedEntityIds.Clear();

        #region Harmony Patches

        [HarmonyPatch(typeof(DamageReceiver), nameof(DamageReceiver.Hurt))]
        [HarmonyPrefix]
        private static void Prefix_Hurt(DamageReceiver __instance, DamageInfo damageInfo)
        {
            var fromChar = damageInfo.fromCharacter;
            if (fromChar == null) return;

            var attacker = fromChar.mainDamageReceiver;
            if (attacker == null) return;
            var maid = __instance.GetComponentInParent<MaidController>();
            
            if (__instance.IsMainCharacter || maid != null)
            {
                // 敌人打了玩家或女仆
                MarkAsEnemy(attacker);
            }
            else if (fromChar.IsMainCharacter)
            {
                // 玩家主动打了某个单位
                MarkAsEnemy(__instance);
            }
        }

        [HarmonyPatch(typeof(AICharacterController), "Update")]
        [HarmonyPostfix]
        private static void Postfix_Update(AICharacterController __instance)
        {
            if (__instance.searchedEnemy == null) return;

            var maid = __instance.GetComponentInParent<MaidController>();
            if (maid == null) return; 

            DamageReceiver target = __instance.searchedEnemy;
            
            // 黑商tmd是 team all！！！
            if ((target.Team == Teams.middle || target.Team == Teams.all) && !IsProvoked(target))
            {
                //CMDebug.Log($"[策略拦截] 成功拦截！目标 {target.name} (Team: {target.Team}) 未被挑衅，清除锁定。");
        
                __instance.searchedEnemy = null;
                __instance.noticed = false;
                __instance.alert = false;
                __instance.aimTarget = null;
            }
        }

        [HarmonyPatch(typeof(LevelManager), "Start")]
        [HarmonyPostfix]
        private static void Postfix_LevelManager_Start()
        {
            ClearProvocations();
            CMDebug.Log("场景开始，已重置敌对名单");
        }

        #endregion
    }
}