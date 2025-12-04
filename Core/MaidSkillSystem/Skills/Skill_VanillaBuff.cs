using UnityEngine;
using Duckov.Buffs;

namespace CombatMaid.Core.MaidSkillSystem.Skills
{
    /// <summary>
    /// 施加原版 Buff 的技能
    /// </summary>
    public class Skill_VanillaBuff : MaidSkillBase
    {
        public override string SkillName => "VanillaBuff";
        public override float Cooldown => 30.0f;

        private string _buffResourcePath;

        public Skill_VanillaBuff(string buffPath)
        {
            _buffResourcePath = buffPath;
        }

        protected override bool CheckTriggerCondition()
        {
            return Controller.MainOwner != null && !Controller.MainOwner.Health.IsDead;
        }

        protected override bool TryExecute()
        {
            Buff originalBuff = Resources.Load<Buff>(_buffResourcePath);
            
            if (originalBuff == null)
            {
                CMDebug.LogWarning($"未找到原版 Buff: {_buffResourcePath}");
                return false;
            }

            Controller.MainOwner.AddBuff(originalBuff, Owner, 1);
            
            Owner.PopText("施加原版Buff");
            return true;
        }
    }
}