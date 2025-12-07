using UnityEngine;
using CombatMaid.Core.MaidSkillSystem;

namespace CombatMaid.Core.MaidSkillSystem.Skills
{
    public class Skill_GrenadeThrower : MaidSkillBase
    {
        public override string SkillName => "GrenadeThrow";
        public override float Cooldown => 15.0f;
        
        public override bool RespectGlobalCooldown => true;
        public override float TriggerGCDDuration => 1.0f;

        private int _grenadeItemId;
        private float _throwRange = 25.0f;
        private float _minRange = 5.0f;

        public Skill_GrenadeThrower(int grenadeId) 
        {
            _grenadeItemId = grenadeId;
        }

        protected override bool CheckTriggerCondition()
        {
            // 1. 必须有 AI 且活着
            if (Controller == null || Controller.AI == null) return false;
            
            // 2. 必须有仇恨目标
            var target = Controller.AI.searchedEnemy;
            if (target == null || target.health.IsDead) return false;

            // 3. 距离检查
            float dist = Vector3.Distance(Owner.transform.position, target.transform.position);
            
            // 只有在射程内，且不会炸到自己的距离才扔
            return dist >= _minRange && dist <= _throwRange;
        }

        protected override bool TryExecute()
        {
            var target = Controller.AI.searchedEnemy;
            if (target == null) return false;

            MaidSkillHelper.LaunchGrenade(
                Owner, 
                _grenadeItemId, 
                target.transform.position, 
                delay: 2.0f, 
                canHurtSelf: false
            );

            Owner.PopText("投掷手雷!");
            return true;
        }
    }
}