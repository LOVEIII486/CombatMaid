using System;
using UnityEngine;
using CombatMaid.Core.BuffsSystem;
using CombatMaid.Localization;

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

        private CharacterMainControl _targetToHeal;
        
        private readonly Lazy<string> _txtExecute = new Lazy<string>(() => 
            LocalizationManager.GetText("Skill_EmergencyHeal_Execute"));
        
        public Skill_EmergencyHeal(string buffName, int buffId, float duration = 10f)
        {
            _buffName = buffName;
            _buffId = buffId;
            _duration = duration;
        }

        protected override bool CheckTriggerCondition()
        {
            _targetToHeal = null;

            // 1. 优先检查玩家血量 < 30%
            if (Controller.MainOwner != null && !Controller.MainOwner.Health.IsDead)
            {
                float playerHpRate = Controller.MainOwner.Health.CurrentHealth / Controller.MainOwner.Health.MaxHealth;
                if (playerHpRate < 0.3f)
                {
                    _targetToHeal = Controller.MainOwner;
                    return true;
                }
            }

            // 2. 其次检查女仆自己血量 < 30%
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

            var config = new MaidBuffFactory.BuffConfig(_buffName, _buffId, _duration);
            var template = MaidBuffFactory.GetOrCreateSharedBuff(config);
            
            if (MaidBuffFactory.TryAddBuff(_targetToHeal, template, Owner))
            {
                Owner.PopText(_txtExecute.Value);
                return true;
            }
            
            return false;
        }
    }
}