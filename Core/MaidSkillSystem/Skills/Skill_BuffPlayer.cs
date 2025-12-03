using UnityEngine;
using CombatMaid.Core.BuffsSystem; // 引用移植好的 Buff 系统

namespace CombatMaid.Core.MaidSkillSystem.Skills
{
    public class Skill_BuffPlayer : MaidSkillBase
    {
        public override string SkillName => "BuffPlayer";
        public override float Cooldown => 60.0f; // 60秒一次大招

        private string _buffName; // 注册在 MaidBuffRegistry 里的名字
        private int _buffId;      // 注册在 MaidBuffFactory 里的ID

        public Skill_BuffPlayer(string buffName, int buffId)
        {
            _buffName = buffName;
            _buffId = buffId;
        }

        protected override bool CheckTriggerCondition()
        {
            // 只有当跟随时，且玩家正在战斗或者受伤时才加
            // 这里简单写：只要冷却好了就给玩家续杯
            return Controller.MainOwner != null && !Controller.MainOwner.Health.IsDead;
        }

        protected override bool TryExecute()
        {
            // 1. 获取模板
            var config = new MaidBuffFactory.BuffConfig(_buffName, _buffId, 30f); // 持续30秒
            var template = MaidBuffFactory.GetOrCreateSharedBuff(config);
            
            // 2. 施加给玩家
            if (MaidBuffFactory.TryAddBuff(Controller.MainOwner, template, Owner))
            {
                Owner.PopText("支援Buff!");
                return true;
            }
            return false;
        }
    }
}