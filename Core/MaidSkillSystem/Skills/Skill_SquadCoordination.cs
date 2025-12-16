using System;
using CombatMaid.Core.MaidFSM.States;
using CombatMaid.Localization;

namespace CombatMaid.Core.MaidSkillSystem.Skills
{
    public class Skill_SquadCoordination : MaidSkillBase
    {
        public override string SkillName => "SquadCoordination";
        public override float Cooldown => 1f; // 检测频率
        
        private readonly Lazy<string> _txtExecute = new Lazy<string>(() => 
            LocalizationManager.GetText("Skill_SquadCoordination_Execute"));

        protected override bool CheckTriggerCondition()
        {
            if (Controller == null || Controller.AI == null) return false;
            
            // 处于特殊状态时不响应
            if (Controller.StateMachine.CurrentState is State_PassiveFollow || 
                Controller.StateMachine.CurrentState is State_ForceFollow)
            {
                return false;
            }

            var focusTarget = MaidManager.Instance.FocusTarget;
            if (focusTarget == null || focusTarget.Health.IsDead) return false;
            
            if (Controller.AI.searchedEnemy != focusTarget.mainDamageReceiver) return true;
            if (!Controller.AI.alert) return true;

            return false;
        }

        protected override bool TryExecute()
        {
            var target = MaidManager.Instance.FocusTarget;
            if (target == null) return false;

            var ai = Controller.AI;

            ai.searchedEnemy = target.mainDamageReceiver;
            ai.aimTarget = target.transform;
            
            if (!ai.alert || !ai.noticed)
            {
                ai.alert = true;
                ai.noticed = true;
                Owner.PopText(_txtExecute.Value);
            }
            
            // CMDebug.Log($"[{Owner.name}] 执行集火 -> {target.name}");

            return true;
        }
    }
}