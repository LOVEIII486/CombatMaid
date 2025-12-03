using UnityEngine;
using CombatMaid.Core; // 引用 Manager
using CombatMaid.Core.MaidFSM.States; // 引用状态

namespace CombatMaid.Core.MaidSkillSystem.Skills
{
    /// <summary>
    /// 小队协同被动技能
    /// 核心功能：
    /// 1. 解决呆滞：AI 没目标时，强塞玩家正在打的目标给它。
    /// 2. 集火：强制所有女仆攻击玩家标记的目标。
    /// </summary>
    public class Skill_SquadCoordination : MaidSkillBase
    {
        public override string SkillName => "SquadCoordination";
        public override float Cooldown => 0.5f; // 检测频率高一点，保证响应速度 (0.5秒)

        protected override bool CheckTriggerCondition()
        {
            // 1. 基础检查
            if (Controller == null || Controller.AI == null) return false;
            
            // 2. 如果女仆正在执行“和平跟随”或“强制跟随”，不要打扰她
            // 我们只在“自主战斗”或“驻守”模式下修正仇恨
            if (Controller.StateMachine.CurrentState is State_PassiveFollow || 
                Controller.StateMachine.CurrentState is State_ForceFollow)
            {
                return false;
            }

            // 3. 检查是否有集火指令
            var focusTarget = MaidManager.Instance.FocusTarget;
            if (focusTarget == null) return false;

            // 4. 检查是否需要修正
            // 情况 A: AI 当前没目标 (发呆中) -> 需要修正
            if (Controller.AI.searchedEnemy == null) return true;

            // 情况 B: AI 有目标，但不是指挥官标记的目标 -> 需要转火
            if (Controller.AI.searchedEnemy != focusTarget) return true;

            return false;
        }

        protected override bool TryExecute()
        {
            var target = MaidManager.Instance.FocusTarget;
            if (target == null) return false;

            var ai = Controller.AI;

            // --- 核心修正逻辑 ---

            // 1. 强制赋予仇恨目标
            ai.searchedEnemy = target.mainDamageReceiver;
            
            // 2. 强制赋予瞄准目标 (让枪口转过去)
            ai.aimTarget = target.transform;

            // 3. 唤醒 AI (解决“无法被激活”的关键)
            if (!ai.alert)
            {
                ai.alert = true;
                ai.noticed = true;
                // 有些 AI 系统需要调用 OnFoundEnemy 来触发状态机切换
                // 如果有这个公开方法最好，没有的话设置 alert 通常足够
                // ai.OnFoundEnemy(target); 
            }

            // 4. 视觉反馈 (可选，调试用)
            Owner.PopText("收到集火指令!");

            return true;
        }
    }
}