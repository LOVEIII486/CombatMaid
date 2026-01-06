using System;
using UnityEngine;
using Duckov.Buffs;
using ItemStatsSystem.Stats;
using CombatMaid.Core.AttributeModifiers;
using CombatMaid.Localization;
using ItemStatsSystem;

namespace CombatMaid.Core.BuffsSystem.Effects
{
    /// <summary>
    /// 撕裂效果：削弱护甲。
    /// </summary>
    public class MaidTearEffect : IMaidBuffEffect
    {
        public string BuffName => "MaidBuff_Tear";
        public int BuffID => 888001;

        public void OnBuffSetup(Buff buff, CharacterMainControl target)
        {
            if (target == null) return;

            try
            {
                float reduction = UnityEngine.Random.Range(-0.4f, -0.1f);

                ApplyAndTrack(buff, target, StatModifier.Attributes.BodyArmor, reduction);
                ApplyAndTrack(buff, target, StatModifier.Attributes.HeadArmor, reduction);

                int pct = Mathf.RoundToInt(Mathf.Abs(reduction) * 100f);
                string fmt = LocalizationManager.GetText("Buff_Tear_Pop", "护甲撕裂 -{0}%");
                target.PopText(string.Format(fmt, pct));
                
                CMDebug.Log($"[{BuffName}] 生效: {target.name} 护甲削弱 {pct}% (InstanceID: {buff.GetInstanceID()})");
            }
            catch (Exception ex)
            {
                CMDebug.LogError($"[{BuffName}] Setup 异常: {ex}");
            }
        }
        
        public void OnBuffDestroy(Buff buff, CharacterMainControl target)
        {
            MaidBuffModifierManager.Instance.CleanupModifiers(buff.GetInstanceID());
        }

        private void ApplyAndTrack(Buff buff, CharacterMainControl target, string statKey, float value)
        {
            var modifier = StatModifier.AddModifier(target, statKey, value, ModifierType.PercentageMultiply, buff);

            if (modifier != null)
            {
                Stat stat = target.CharacterItem?.Stats.GetStat(statKey);
                if (stat == null) stat = target.GetComponent<StatCollection>()?.GetStat(statKey);

                if (stat != null)
                {
                    MaidBuffModifierManager.Instance.TrackModifier(buff.GetInstanceID(), stat, modifier);
                }
            }
        }
    }
}