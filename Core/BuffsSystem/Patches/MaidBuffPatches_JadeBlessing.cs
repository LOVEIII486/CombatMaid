using HarmonyLib;
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
            if (__instance.IsDead || __instance.Invincible) return true;

            // 致死判定
            if (damageInfo.damageValue < __instance.CurrentHealth) return true;
            
            var character = __instance.TryGetCharacter();
            // 检查该角色是否携带了指定的触发 Buff
            if (character == null || !character.HasBuff(BlessingBuffID)) return true;
            
            // 恢复至最大生命值的 50%
            float recoveryAmount = __instance.MaxHealth * 0.5f;
            __instance.SetHealth(recoveryAmount);

            // 移除消耗性 Buff，并施加 5秒 无敌 Buff
            character.RemoveBuff(BlessingBuffID, false);
            
            var invincConfig = new MaidBuffFactory.BuffConfig("MaidBuff_JadeInvincible", InvincibleBuffID, 5f);
            var buffPfb = MaidBuffFactory.GetOrCreateSharedBuff(invincConfig);
            if (buffPfb != null)
            {
                character.AddBuff(buffPfb, character);
            }

            string triggerMsg = LocalizationManager.GetText("Buff_JadeBlessing_Trigger_Pop");
            character.PopText(string.Format(triggerMsg, 5)); 
                
            // 玩家触发时没有preset，无法输出displayname，只能用name
            // CMDebug.Log($"衔玉无敌在 {character.name} 上激活。恢复血量: {recoveryAmount}，施加5秒无敌。");
            
            return false;
        }
    }
}