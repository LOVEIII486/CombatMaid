using Duckov.Buffs;
using CombatMaid.Core.AttributeModifiers;
using ItemStatsSystem.Stats;
using CombatMaid.Localization;

namespace CombatMaid.Core.BuffsSystem.Effects
{
    /// <summary>
    /// 酒狐的心意曲奇效果：增加 50 点生命上限
    /// </summary>
    public class MaidCookieEffect : IMaidBuffEffect
    {
        public string BuffName => "MaidBuff_WineFoxCookie";
        public int BuffID => 888003;

        private const float ExtraHP = 50f;

        public void OnBuffSetup(Buff buff, CharacterMainControl target)
        {
            if (target == null || target.CharacterItem == null) return;

            var modifier = StatModifier.AddModifier(target, StatModifier.Attributes.MaxHealth, ExtraHP, ModifierType.Add, buff);

            if (modifier != null && target?.CharacterItem != null)
            {
                var stat = target.CharacterItem.GetStat(StatModifier.Attributes.MaxHealth);
                MaidBuffModifierManager.Instance.TrackModifier(buff.GetInstanceID(), stat, modifier);
            }

            string popTemplate = LocalizationManager.GetText("Buff_WineFoxCookie_Pop");
            string finalMsg = string.Format(popTemplate, ExtraHP);
            target.PopText(finalMsg);
            
            // 仅回复提升的生命，而不是直接回满血
            target.AddHealth(ExtraHP);
        }

        public void OnBuffDestroy(Buff buff, CharacterMainControl target)
        {
            //MaidBuffPatches.cs 逻辑会在执行此回调后自动调用 CleanupModifiers
            //CMDebug.Log($"[{BuffName}] 效果结束：生命上限已还原");
        }
    }
}