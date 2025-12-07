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
        public override float Cooldown => 0.1f; // 检测频率
        public override bool RespectGlobalCooldown => false;

        protected override bool CheckTriggerCondition()
        {
            // 1. 基础检查
            if (Controller == null || Controller.AI == null) return false;
    
            // 2. 状态检查
            if (Controller.StateMachine.CurrentState is State_PassiveFollow || 
                Controller.StateMachine.CurrentState is State_ForceFollow)
            {
                return false;
            }

            // 3. 检查是否有集火指令
            var focusTarget = MaidManager.Instance.FocusTarget;
            if (focusTarget == null) 
            {
                return false;
            }

            // 4. 检查是否需要修正
            // A: AI 当前没目标 -> 需要执行
            if (Controller.AI.searchedEnemy == null) 
            {
                CMDebug.Log($"[{SkillName}] 触发: 当前无目标 -> 响应集火");
                return true;
            }

            // B: AI 有目标，但不是集火目标 -> 需要执行
            if (Controller.AI.searchedEnemy != focusTarget) 
            {
                CMDebug.Log($"[{SkillName}] 触发: 当前目标({Controller.AI.searchedEnemy.name}) != 集火目标({focusTarget.name}) -> 纠正");
                return true;
            }
    
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