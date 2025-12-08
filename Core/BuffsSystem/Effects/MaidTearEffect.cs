using System;
using UnityEngine;
using Duckov.Buffs;
using ItemStatsSystem.Stats;
using CombatMaid.Core.AttributeModifiers;
using CombatMaid.Localization;

namespace CombatMaid.Core.BuffsSystem.Effects
{
    /// <summary>
    /// 撕裂效果：削弱护甲
    /// </summary>
    public class MaidTearEffect : IMaidBuffEffect
    {
        // Buff 配置的名称，必须以 MaidBuff_ 开头
        public string BuffName => "MaidBuff_Tear";
        
        public int BuffID => 888001;

        public void OnBuffSetup(Buff buff, CharacterMainControl target)
        {
            if (target == null || target.CharacterItem == null) return;

            try
            {
                // 1. 随机生成削弱幅度 (-10% ~ -40%)
                float reduction = UnityEngine.Random.Range(-0.4f, -0.1f);

                // 2. 应用并追踪修改器 (身体护甲 + 头部护甲)
                ApplyAndTrackModifier(buff, target, StatModifier.Attributes.BodyArmor, reduction);
                ApplyAndTrackModifier(buff, target, StatModifier.Attributes.HeadArmor, reduction);

                // 3. 飘字提示
                // 格式化数值 (例如 0.25 -> "25")
                string pctStr = Mathf.RoundToInt(Mathf.Abs(reduction) * 100f).ToString();
                
                // 尝试获取本地化文本 (Key: "Buff_Tear_Pop")，如果没有则使用默认文本
                // 建议在 CSV 中添加: Buff_Tear_Pop,护甲撕裂 -{0}%,...
                string fmt = LocalizationManager.GetText("Buff_Tear_Pop", "护甲撕裂 -{0}%");
                string finalMsg = string.Format(fmt, pctStr);
                
                target.PopText(finalMsg);
                
                CMDebug.Log($"[MaidTearEffect] 生效: {target.characterPreset.DisplayName} 护甲削弱 {reduction:P0}");
            }
            catch (Exception ex)
            {
                CMDebug.LogError($"[MaidTearEffect] Setup 异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 当 Buff 销毁时触发
        /// </summary>
        public void OnBuffDestroy(Buff buff, CharacterMainControl target)
        {
            // Core.BuffsSystem.MaidBuffDestroyPatch 会在 OnBuffDestroy 执行后，
            // 自动调用 MaidBuffModifierManager.Instance.CleanupModifiers(buff.GetInstanceID())。
        }

        /// <summary>
        /// 辅助方法：给属性施加 Buff 并注册到管理器以便自动清理
        /// </summary>
        private void ApplyAndTrackModifier(Buff buff, CharacterMainControl target, string statName, float value)
        {
            var modifier = StatModifier.AddModifier(
                target, 
                statName, 
                value, 
                ModifierType.PercentageMultiply
            );

            if (modifier != null)
            {
                var stat = target.CharacterItem.GetStat(statName);
                if (stat != null)
                {
                    MaidBuffModifierManager.Instance.TrackModifier(buff.GetInstanceID(), stat, modifier);
                }
            }
        }
    }
}