using UnityEngine;
using CombatMaid.Core;
using CombatMaid.Core.MaidFSM.States;

namespace CombatMaid.Core.MaidSkillSystem.Skills
{
    public class Skill_SquadCoordination : MaidSkillBase
    {
        public override string SkillName => "SquadCoordination";
        public override float Cooldown => 0.2f; // 加快检测频率，让女仆反应更快

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

            // 逻辑优化：只要当前目标不是集火目标，或者当前没有处于攻击状态，就触发修正
            // 这样可以防止 AI "发呆"
            if (Controller.AI.searchedEnemy != focusTarget) return true;
            if (!Controller.AI.alert) return true;

            return false;
        }

        protected override bool TryExecute()
        {
            var target = MaidManager.Instance.FocusTarget;
            if (target == null) return false;

            var ai = Controller.AI;

            // 强制覆盖 AI 的仇恨列表
            ai.searchedEnemy = target.mainDamageReceiver; // 确保指向主受击体
            ai.aimTarget = target.transform;
            
            // 强制进入战斗状态
            if (!ai.alert || !ai.noticed)
            {
                ai.alert = true;
                ai.noticed = true;
                // 让女仆喊话，明确反馈她收到了指令
                Owner.PopText("收到！集火目标！");
            }
            
            // 调试日志
             CMDebug.Log($"[{Owner.name}] 执行集火 -> {target.name}");

            return true;
        }
    }
}