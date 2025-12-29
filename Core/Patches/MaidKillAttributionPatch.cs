using CombatMaid.Settings;
using HarmonyLib;

namespace CombatMaid.Core.Patches;

[HarmonyPatch(typeof(Health), nameof(Health.Hurt))]
public static class MaidKillAttributionPatch
{
    static void Prefix(ref DamageInfo damageInfo)
    {
        if (!CombatMaidConfig.TransferKillToOwner) return;

        if (damageInfo.fromCharacter == null) return;

        var maid = MaidController.GetMaidByCharacter(damageInfo.fromCharacter);

        if (maid != null && maid.MainOwner != null)
        {
            damageInfo.fromCharacter = maid.MainOwner;
        }
    }
}