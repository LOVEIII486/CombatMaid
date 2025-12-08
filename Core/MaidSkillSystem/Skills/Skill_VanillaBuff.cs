using System.Collections.Generic;
using UnityEngine;
using CombatMaid.Core.BuffsSystem;
using Duckov.Buffs;

namespace CombatMaid.Core.MaidSkillSystem.Skills
{
    /// <summary>
    /// 随机增益技能
    /// </summary>
    public class Skill_VanillaBuff : MaidSkillBase
    {
        public override string SkillName => "VanillaBuff";
        public override float Cooldown => 10.0f;
        
        public override bool RespectGlobalCooldown => true;
        public override float TriggerGCDDuration => 0.5f;

        private readonly List<int> _buffPool;
        private const int HappyBuffId = 1101; // 高兴 Buff

        public Skill_VanillaBuff(List<int> buffIds)
        {
            _buffPool = buffIds ?? new List<int>();
        }

        protected override bool CheckTriggerCondition()
        {
            return Controller.MainOwner != null && !Controller.MainOwner.Health.IsDead;
        }

        protected override bool TryExecute()
        {
            bool anySuccess = false;
            string randomBuffName = "";

            if (MaidBuffUtils.ApplyBuffByID(Controller.MainOwner, HappyBuffId, out _, Owner))
            {
                anySuccess = true;
            }

            if (_buffPool.Count > 0)
            {
                int index = Random.Range(0, _buffPool.Count);
                int randomBuffId = _buffPool[index];
                if (MaidBuffUtils.ApplyBuffByID(Controller.MainOwner, randomBuffId, out string name, Owner))
                {
                    randomBuffName = name;
                    anySuccess = true;
                }
                else
                {
                    CMDebug.LogWarning($"[{SkillName}] 随机 Buff 施加失败 (ID: {randomBuffId})");
                }
            }

            if (anySuccess)
            {
                string popText = "主人要开心哦~ ";
                if (!string.IsNullOrEmpty(randomBuffName))
                {
                    popText += $"\n给主人buff了！ ({randomBuffName})";
                }
        
                Owner.PopText(popText);
                return true;
            }
    
            return false;
        }
    }
}