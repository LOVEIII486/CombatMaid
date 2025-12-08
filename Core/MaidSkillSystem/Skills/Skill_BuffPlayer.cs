using System.Collections.Generic;
using UnityEngine;
using CombatMaid.Core.BuffsSystem;

namespace CombatMaid.Core.MaidSkillSystem.Skills
{
    public class Skill_BuffPlayer : MaidSkillBase
    {
        public override string SkillName => "BuffPlayer";
        public override float Cooldown => 10.0f;
        
        public override bool RespectGlobalCooldown => true;
        public override float TriggerGCDDuration => 0.5f;

        private readonly List<(string Name, int Id)> _buffPool;

        public Skill_BuffPlayer(List<(string, int)> buffPool)
        {
            _buffPool = buffPool ?? new List<(string, int)>();
        }

        protected override bool CheckTriggerCondition()
        {
            return Controller.MainOwner != null && !Controller.MainOwner.Health.IsDead;
        }

        protected override bool TryExecute()
        {
            if (_buffPool.Count == 0)
            {
                CMDebug.LogWarning($"[{SkillName}] Buff池为空，无法施放技能");
                return false;
            }

            // 1. 随机抽取一个 Buff 配置
            var selection = _buffPool[Random.Range(0, _buffPool.Count)];
            string buffName = selection.Name;
            int buffId = selection.Id;

            // 2. 获取或创建模板
            var config = new MaidBuffFactory.BuffConfig(buffName, buffId, 30f); // 持续30秒
            var template = MaidBuffFactory.GetOrCreateSharedBuff(config);
            
            // 3. 施加给玩家
            if (MaidBuffFactory.TryAddBuff(Controller.MainOwner, template, Owner))
            {
                // 可以把 Buff 名也打出来方便调试
                Owner.PopText($"支援: {buffName}!");
                return true;
            }
            return false;
        }
    }
}