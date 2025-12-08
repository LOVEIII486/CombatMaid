using System.Collections.Generic;
using UnityEngine;
using CombatMaid.Core.BuffsSystem; // 引用 MaidBuffUtils 所在的命名空间

namespace CombatMaid.Core.MaidSkillSystem.Skills
{
    /// <summary>
    /// 施加原版 Buff 的技能 (支持通过 ID 批量施加)
    /// </summary>
    public class Skill_VanillaBuff : MaidSkillBase
    {
        public override string SkillName => "VanillaBuff";
        public override float Cooldown => 10.0f;
        public override bool RespectGlobalCooldown => true;
        public override float TriggerGCDDuration => 0.5f;

        // 存储 Buff ID 列表
        private readonly List<int> _buffIds;

        public Skill_VanillaBuff(List<int> buffIds)
        {
            _buffIds = buffIds ?? new List<int>();
        }

        protected override bool CheckTriggerCondition()
        {
            // 确保主人存活
            return Controller.MainOwner != null && !Controller.MainOwner.Health.IsDead;
        }

        protected override bool TryExecute()
        {
            if (_buffIds.Count == 0) return false;

            int successCount = 0;

            foreach (int buffId in _buffIds)
            {
                // 使用 MaidBuffUtils 工具通过 ID 施加 Buff
                // 第三个参数 Owner 是施法者（女仆自己），用于记录 Buff 来源
                bool result = MaidBuffUtils.ApplyBuffByID(Controller.MainOwner, buffId, Owner);
                
                if (result)
                {
                    successCount++;
                }
                else
                {
                    CMDebug.LogWarning($"[{SkillName}] 施加 Buff 失败，无效的 ID: {buffId}");
                }
            }

            if (successCount > 0)
            {
                // 只有至少成功施加了一个 Buff 才算技能释放成功
                Owner.PopText($"Buff x{successCount}!");
                return true;
            }
            
            return false;
        }
    }
}