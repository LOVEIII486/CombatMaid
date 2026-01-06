using ItemStatsSystem.Stats;
using System.Collections.Generic;

namespace CombatMaid.Core.AttributeModifiers
{
    /// <summary>
    /// 统一属性修改器
    /// </summary>
    public static class AttributeModifier
    {
        // AI 字段白名单
        private static readonly HashSet<string> AI_FIELDS = new HashSet<string>
        {
            AIFieldModifier.Fields.ReactionTime,
            AIFieldModifier.Fields.BaseReactionTime,
            AIFieldModifier.Fields.UpdateValueTimer,
            AIFieldModifier.Fields.PatrolTurnSpeed,
            AIFieldModifier.Fields.CombatTurnSpeed,
            AIFieldModifier.Fields.ShootDelay,
            AIFieldModifier.Fields.ShootCanMove,
            AIFieldModifier.Fields.SightDistance,
            AIFieldModifier.Fields.SightAngle,
            AIFieldModifier.Fields.HearingAbility,
            AIFieldModifier.Fields.CanDash,
            AIFieldModifier.Fields.PatrolRange,
            AIFieldModifier.Fields.CombatMoveRange,
            AIFieldModifier.Fields.ForgetTime,
            AIFieldModifier.Fields.HasSkill,
            AIFieldModifier.Fields.SkillChance,
            AIFieldModifier.Fields.DefaultWeaponOut
        };

        /// <summary>
        /// 标准属性 Key 映射
        /// </summary>
        public static class StandardAttributes
        {
            // Stat 系统
            public const string MaxHealth = StatModifier.Attributes.MaxHealth;
            public const string HeadArmor = StatModifier.Attributes.HeadArmor;
            public const string BodyArmor = StatModifier.Attributes.BodyArmor;
            public const string GunDamage = StatModifier.Attributes.GunDamageMultiplier;
            public const string MeleeDamage = StatModifier.Attributes.MeleeDamageMultiplier;
            public const string WalkSpeed = StatModifier.Attributes.WalkSpeed;
            public const string RunSpeed = StatModifier.Attributes.RunSpeed;

            // AI 系统
            public const string AI_ReactionTime = AIFieldModifier.Fields.ReactionTime;
            public const string AI_SkillChance = AIFieldModifier.Fields.SkillChance;
            public const string AI_ShootDelay = AIFieldModifier.Fields.ShootDelay;
        }

        public static class Quick
        {
            public static void ModifyDamage(CharacterMainControl character, float multiplier, object source)
            {
                ModifyStat(character, StandardAttributes.GunDamage, multiplier, true, source);
                ModifyStat(character, StandardAttributes.MeleeDamage, multiplier, true, source);
            }

            public static void ModifySpeed(CharacterMainControl character, float multiplier, object source)
            {
                ModifyStat(character, StandardAttributes.WalkSpeed, multiplier, true, source);
                ModifyStat(character, StandardAttributes.RunSpeed, multiplier, true, source);
            }
        }


        /// <summary>
        /// 智能修改入口
        /// </summary>
        public static object Modify(CharacterMainControl character, string attributeName, float value, bool isMultiplier, object source = null)
        {
            if (character == null) return null;

            if (AI_FIELDS.Contains(attributeName) || attributeName.Contains("."))
            {
                ModifyAI(character, attributeName, value, isMultiplier);
                return null;
            }

            return ModifyStat(character, attributeName, value, isMultiplier, source);
        }

        /// <summary>
        /// 直接操作 Stat 系统
        /// </summary>
        public static Modifier ModifyStat(CharacterMainControl character, string statKey, float value, bool isMultiplier, object source = null)
        {
            if (character == null) return null;

            ModifierType type = isMultiplier ? ModifierType.PercentageMultiply : ModifierType.Add;
            // 适配 ItemStatsSystem 的倍率算法: 1.2倍 需转换为 +0.2 增量
            float finalValue = isMultiplier ? (value - 1f) : value;

            return StatModifier.AddModifier(character, statKey, finalValue, type, source);
        }

        /// <summary>
        /// 直接操作 AI 系统
        /// </summary>
        public static void ModifyAI(CharacterMainControl character, string fieldPath, float value, bool isMultiplier)
        {
            AIFieldModifier.ModifyDelayed(character, fieldPath, value, isMultiplier);
        }

        /// <summary>
        /// 统一清理方法
        /// </summary>
        public static void ClearAll(CharacterMainControl character, object source)
        {
            if (character == null || source == null) return;
            StatModifier.RemoveAllModifiersFromSource(character, source);
        }

        /// <summary>
        /// 属性名是否应该被视为百分比类型 (用于配置解析)
        /// </summary>
        public static bool IsPercentageType(string attributeName)
        {
            return attributeName.Contains("Multiplier") || attributeName.Contains("Speed") || 
                   attributeName.Contains("Rate") || attributeName.Contains("Gain") ||
                   attributeName.Contains("Chance") || attributeName == AIFieldModifier.Fields.ReactionTime;
        }
    }
}