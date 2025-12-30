using System.Collections.Generic;
using CombatMaid.Core.BuffsSystem;

namespace CombatMaid.Core.MaidSkillSystem.Skills
{
    public class Skill_FoxGuard : MaidSkillBase
    {
        public override string SkillName => "FoxGuard";
        
        // 衔玉祝福的 Buff ID
        private const int BlessingBuffID = 888004;

        public Skill_FoxGuard(Dictionary<string, object> parameters)
        {
            // 暂时无需参数
        }

        public override void Initialize(MaidController controller)
        {
            base.Initialize(controller);
            ApplyBlessing();
        }

        private void ApplyBlessing()
        {
            if (Owner == null) return;

            var config = new MaidBuffFactory.BuffConfig("MaidBuff_JadeBlessing", BlessingBuffID, -1.0f);
            var buffPfb = MaidBuffFactory.GetOrCreateSharedBuff(config);
            
            if (buffPfb != null)
            {
                MaidBuffFactory.TryAddBuff(Owner, buffPfb, Owner);
            }
        }

        protected override bool CheckTriggerCondition() => false;
        protected override bool TryExecute() => false;
    }
}