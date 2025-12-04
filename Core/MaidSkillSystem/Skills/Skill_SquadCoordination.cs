using UnityEngine;
using CombatMaid.Core;
using CombatMaid.Core.MaidFSM.States;

namespace CombatMaid.Core.MaidSkillSystem.Skills
{
    /// <summary>
    /// 小队协同被动技能
    /// </summary>
    public class Skill_SquadCoordination : MaidSkillBase
    {
        public override string SkillName => "SquadCoordination";
        public override float Cooldown => 0.5f; // 检测频率

        protected override bool CheckTriggerCondition()
        {
            // 1. 基础检查
            if (Controller == null || Controller.AI == null) return false;
            
            // 2. 如果女仆正在执行“和平跟随”或“强制跟随”
            if (Controller.StateMachine.CurrentState is State_PassiveFollow || 
                Controller.StateMachine.CurrentState is State_ForceFollow)
            {
                return false;
            }

            // 3. 检查是否有集火指令
            var focusTarget = MaidManager.Instance.FocusTarget;
            if (focusTarget == null) return false;

            // 4. 检查是否需要修正
            // A: AI 当前没目标
            if (Controller.AI.searchedEnemy == null) return true;
            // B: AI 有目标，但不是指挥官标记的目标
            if (Controller.AI.searchedEnemy != focusTarget) return true;

            return false;
        }

        protected override bool TryExecute()
        {
            var target = MaidManager.Instance.FocusTarget;
            if (target == null) return false;

            var ai = Controller.AI;

            // 1. 强制赋予仇恨目标
            ai.searchedEnemy = target.mainDamageReceiver;
            
            // 2. 强制赋予瞄准目标
            ai.aimTarget = target.transform;

            // 3. 唤醒 AI
            if (!ai.alert)
            {
                ai.alert = true;
                ai.noticed = true;
            }
            Owner.PopText("收到集火指令!");

            return true;
        }
    }
}