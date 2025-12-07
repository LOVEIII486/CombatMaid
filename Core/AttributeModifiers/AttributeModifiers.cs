using System.Collections.Generic;
using ItemStatsSystem.Stats;

namespace CombatMaid.Core.AttributeModifiers
{
    /// <summary>
    /// 统一属性修改器
    /// 自动分发 Stat 和 AI 字段的修改请求
    /// </summary>
    public static class AttributeModifier
    {
        // ========== 快捷修改常用属性 ==========
        public static class Quick
        {
            /// <summary>
            /// 修改血量上限 (可选回满血)
            /// </summary>
            public static void ModifyHealth(CharacterMainControl character, float multiplier, bool healToFull = false)
            {
                float val = multiplier - 1f; // StatModifier 接受的是增量 (1.5倍 -> 增加0.5)
                StatModifier.AddModifier(character, StatModifier.Attributes.MaxHealth, val, ModifierType.PercentageMultiply);
                
                if (healToFull && character?.Health != null)
                {
                    character.Health.SetHealth(character.Health.MaxHealth);
                }
            }

            /// <summary>
            /// 修改全局伤害 (枪械+近战)
            /// </summary>
            public static void ModifyDamage(CharacterMainControl character, float multiplier)
            {
                float val = multiplier - 1f;
                StatModifier.AddModifier(character, StatModifier.Attributes.GunDamageMultiplier, val, ModifierType.PercentageMultiply);
                StatModifier.AddModifier(character, StatModifier.Attributes.MeleeDamageMultiplier, val, ModifierType.PercentageMultiply);
            }

            /// <summary>
            /// 修改移动能力 (走、跑、加速度)
            /// </summary>
            public static List<Modifier> ModifySpeed(CharacterMainControl character, float multiplier)
            {
                var modifiers = new List<Modifier>();
                float val = multiplier - 1f;

                modifiers.Add(StatModifier.AddModifier(character, StatModifier.Attributes.WalkSpeed, val, ModifierType.PercentageMultiply));
                modifiers.Add(StatModifier.AddModifier(character, StatModifier.Attributes.RunSpeed, val, ModifierType.PercentageMultiply));
                modifiers.Add(StatModifier.AddModifier(character, StatModifier.Attributes.WalkAcc, val, ModifierType.PercentageMultiply));
                modifiers.Add(StatModifier.AddModifier(character, StatModifier.Attributes.RunAcc, val, ModifierType.PercentageMultiply));

                return modifiers;
            }

            /// <summary>
            /// 修改护甲 (头+身)
            /// </summary>
            public static void ModifyDefense(CharacterMainControl character, float multiplier)
            {
                float val = multiplier - 1f;
                StatModifier.AddModifier(character, StatModifier.Attributes.HeadArmor, val, ModifierType.PercentageMultiply);
                StatModifier.AddModifier(character, StatModifier.Attributes.BodyArmor, val, ModifierType.PercentageMultiply);
            }
        }

        // ========== 标准属性名称定义 ==========
        public static class StandardAttributes
        {
            // Stat 基础
            public const string MaxHealth = StatModifier.Attributes.MaxHealth;
            public const string MoveSpeed = StatModifier.Attributes.WalkSpeed;
            public const string Damage = StatModifier.Attributes.GunDamageMultiplier;
            
            // Stat 防御
            public const string HeadArmor = StatModifier.Attributes.HeadArmor;
            public const string BodyArmor = StatModifier.Attributes.BodyArmor;

            // Stat 感知 
            public const string SightDistance = StatModifier.Attributes.ViewDistance;
            public const string SightAngle = StatModifier.Attributes.ViewAngle;
            public const string HearingAbility = StatModifier.Attributes.HearingAbility;
            
            // AI 行为 (走 AIFieldModifier 的反射)
            public const string PatrolRange = AIFieldModifier.Fields.PatrolRange;
            public const string CombatMoveRange = AIFieldModifier.Fields.CombatMoveRange;
            public const string ForgetTime = AIFieldModifier.Fields.ForgetTime;
            public const string CanDash = AIFieldModifier.Fields.CanDash;
            public const string ReactionTime = AIFieldModifier.Fields.ReactionTime; // 新增
        }
        
        private static readonly HashSet<string> PercentageAttributes = new HashSet<string>
        {
            // 速度类通常是倍率
            "WalkSpeed", "RunSpeed", "WalkAcc", "RunAcc", "TurnSpeed", 
            // 战斗类倍率
            "GunDamageMultiplier", "MeleeDamageMultiplier", 
            "GunDistanceMultiplier", "BulletSpeedMultiplier",
            "GunScatterMultiplier", "RecoilControl", "ReloadSpeedGain",
            // 暴击通常是率 (0.1 = 10%)
            "GunCritRateGain", "MeleeCritRateGain", "GunCritDamageGain", "MeleeCritDamageGain",
            // 元素抗性系数
            "ElementFactor_Physics", "ElementFactor_Fire", "ElementFactor_Poison",
            "ElementFactor_Electricity", "ElementFactor_Space", "ElementFactor_Ghost",
            
            // [新增] AI 属性倍率支持
            AIFieldModifier.Fields.ReactionTime // 允许对反应时间使用百分比修正 (e.g. -0.2 => x0.8)
        };

        // ========== 核心分发逻辑 =========

        /// <summary>
        /// 修改属性通用入口
        /// </summary>
        /// <returns>如果是 Stat 修改，返回 Modifier 对象（可用于后续移除）；如果是 AI 修改，返回 null</returns>
        public static object Modify(CharacterMainControl character, string attributeName, float value, bool isMultiplier)
        {
            // 1. 优先尝试 Stat 修改 (游戏原生属性系统)
            if (StatModifier.CanModify(attributeName))
            {
                var type = isMultiplier ? ModifierType.PercentageMultiply : ModifierType.Add;
                // 如果是倍率模式，假设传入的是最终倍率(如1.5)，转换为增量(0.5)适配 Modifier
                float finalValue = isMultiplier ? (value - 1f) : value;
                
                return StatModifier.AddModifier(character, attributeName, finalValue, type);
            }

            // 2. 其次尝试 AI 字段修改 (反射修改私有字段)
            if (AIFieldModifier.CanModify(attributeName))
            {
                // 使用立即修改，假设调用时机通常是技能触发，AI 应该已经存在
                AIFieldModifier.ModifyImmediate(character, attributeName, value, isMultiplier);
                return null;
            }

            return null;
        }
        
        /// <summary>
        /// [新增] 专门处理来自 JSON 的增量数值
        /// </summary>
        /// <param name="deltaValue">增量值 (例如 0.1 代表 +10%, 50 代表 +50点)</param>
        public static object ModifyByDelta(CharacterMainControl character, string attributeName, float deltaValue)
        {
            // 1. 自动判断修改类型
            bool isPercent = IsPercentageType(attributeName);
            
            // 2. 尝试 Stat 修改
            if (StatModifier.CanModify(attributeName))
            {
                var type = isPercent ? ModifierType.PercentageMultiply : ModifierType.Add;
                return StatModifier.AddModifier(character, attributeName, deltaValue, type);
            }

            // 3. 尝试 AI 字段修改
            if (AIFieldModifier.CanModify(attributeName))
            {
                // 如果是百分比属性，执行乘法逻辑 (current * (1 + delta))
                // 例如 delta = -0.2 (减少20%), final = 0.8
                float finalAIValue = deltaValue;
                if (isPercent)
                {
                    finalAIValue = 1.0f + deltaValue; 
                }
                
                AIFieldModifier.ModifyImmediate(character, attributeName, finalAIValue, multiply: isPercent);
                return null;
            }

            CMDebug.LogWarning($"[AttributeModifier] 无法处理属性: {attributeName}");
            return null;
        }
        
        /// <summary>
        /// 判断属性是否应该使用百分比修改
        /// </summary>
        public static bool IsPercentageType(string attributeName)
        {
            return PercentageAttributes.Contains(attributeName);
        }
    }
}