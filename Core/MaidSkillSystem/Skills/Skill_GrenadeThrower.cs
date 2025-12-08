using System.Collections.Generic;
using UnityEngine;
using CombatMaid.Core.MaidSkillSystem;

namespace CombatMaid.Core.MaidSkillSystem.Skills
{
    public class Skill_GrenadeThrower : MaidSkillBase
    {
        public override string SkillName => "GrenadeThrow";
        public override float Cooldown => 15.0f;
        
        public override bool RespectGlobalCooldown => true;
        public override float TriggerGCDDuration => 1.0f;

        private readonly List<int> _grenadePool;
        private float _throwRange = 25.0f;
        private float _minRange = 5.0f;

        public Skill_GrenadeThrower(List<int> grenadeIds) 
        {
            _grenadePool = grenadeIds ?? new List<int>();
        }

        protected override bool CheckTriggerCondition()
        {
            // 1. 必须有 AI 且活着
            if (Controller == null || Controller.AI == null) return false;
            
            // 2. 必须有仇恨目标
            var target = Controller.AI.searchedEnemy;
            if (target == null || target.health.IsDead) return false;

            // 3. 距离检查
            float dist = Vector3.Distance(Owner.transform.position, target.transform.position);
            
            // 只有在射程内，且不会炸到自己的距离才扔
            return dist >= _minRange && dist <= _throwRange;
        }

        protected override bool TryExecute()
        {
            var target = Controller.AI.searchedEnemy;
            if (target == null) return false;

            // 安全检查：池子是否为空
            if (_grenadePool.Count == 0)
            {
                CMDebug.LogWarning($"[{SkillName}] 手雷配置列表为空，无法执行技能");
                return false;
            }

            // 随机选取一个手雷 ID
            int index = Random.Range(0, _grenadePool.Count);
            int selectedItemId = _grenadePool[index];

            MaidSkillHelper.LaunchGrenade(
                Owner, 
                selectedItemId, 
                target.transform.position, 
                delay: 2.0f, 
                canHurtSelf: false
            );

            // 可以在日志里打印具体扔了哪个，方便调试
            CMDebug.Log($"[{SkillName}] 随机投掷手雷 (ID: {selectedItemId})");
            Owner.PopText("投掷手雷!");
            return true;
        }
    }
}