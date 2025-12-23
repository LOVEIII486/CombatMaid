using HarmonyLib;
using Duckov.Buffs;
using CombatMaid.Localization;

namespace CombatMaid.Core.BuffsSystem
{
    [HarmonyPatch(typeof(Health), "Hurt")]
    public static class HealthHurtPatch_JadeBlessing
    {
        private const int BlessingBuffID = 888004;
        private const int InvincibleBuffID = 888005;

        [HarmonyPrefix]
        public static bool Prefix(Health __instance, DamageInfo damageInfo)
        {
            if (__instance.IsDead || !__instance.IsMainCharacterHealth) return true;
            if (damageInfo.damageValue < __instance.CurrentHealth) return true;

            // 检查是否有祝福 Buff
            var character = __instance.TryGetCharacter();
            if (character == null || !character.HasBuff(BlessingBuffID)) return true;

            __instance.SetHealth(1f);
            // 移除祝福，施加 5秒 无敌
            character.RemoveBuff(BlessingBuffID, false);
            var invincConfig = new MaidBuffFactory.BuffConfig("MaidBuff_JadeInvincible", InvincibleBuffID, 5f);
            var buffPfb = MaidBuffFactory.GetOrCreateSharedBuff(invincConfig);
            if (buffPfb != null) character.AddBuff(buffPfb, character);

            string triggerMsg = LocalizationManager.GetText("Buff_JadeBlessing_Trigger_Pop");
            character.PopText(string.Format(triggerMsg, 5)); 

            //CMDebug.Log($"[衔玉之护] 为 {character.name} 抵挡了致命伤害。");
            return false;
        }
    }
}