using UnityEngine;
using CombatMaid.Core.BuffsSystem;

namespace CombatMaid.Core.MaidSkillSystem.Skills
{
    /// <summary>
    /// 紧急治疗技能：当主人或自己血量低于30%时触发
    /// </summary>
    public class Skill_EmergencyHeal : MaidSkillBase
    {
        public override string SkillName => "EmergencyHeal";
        public override float Cooldown => 120.0f;
        
        public override bool RespectGlobalCooldown => false;

        private string _buffName;
        private int _buffId;
        private float _duration;

        // 缓存当前需要治疗的目标，以便在 TryExecute 中使用
        private CharacterMainControl _targetToHeal;

        /// <summary>
        /// 构造函数传入具体的 Buff 配置
        /// </summary>
        public Skill_EmergencyHeal(string buffName, int buffId, float duration = 10f)
        {
            _buffName = buffName;
            _buffId = buffId;
            _duration = duration;
        }

        protected override bool CheckTriggerCondition()
        {
            // 每次检查前重置目标
            _targetToHeal = null;

            // 1. 优先检查玩家血量 (< 30%)
            if (Controller.MainOwner != null && !Controller.MainOwner.Health.IsDead)
            {
                float playerHpRate = Controller.MainOwner.Health.CurrentHealth / Controller.MainOwner.Health.MaxHealth;
                if (playerHpRate < 0.3f)
                {
                    _targetToHeal = Controller.MainOwner;
                    return true;
                }
            }

            // 2. 其次检查女仆自己血量 (< 30%)
            if (Owner != null && !Owner.Health.IsDead)
            {
                float maidHpRate = Owner.Health.CurrentHealth / Owner.Health.MaxHealth;
                if (maidHpRate < 0.3f)
                {
                    _targetToHeal = Owner;
                    return true;
                }
            }

            return false;
        }

        protected override bool TryExecute()
        {
            if (_targetToHeal == null) return false;

            // 1. 获取或创建 Buff 模板 (复用 MaidBuffFactory)
            var config = new MaidBuffFactory.BuffConfig(_buffName, _buffId, _duration);
            var template = MaidBuffFactory.GetOrCreateSharedBuff(config);
            
            // 2. 施加给选定的目标 (玩家或女仆)
            if (MaidBuffFactory.TryAddBuff(_targetToHeal, template, Owner))
            {
                string targetDesc = (_targetToHeal == Controller.MainOwner) ? "主人" : "自己";
                Owner.PopText($"紧急治疗: {targetDesc}!");
                return true;
            }
            
            return false;
        }
    }
}