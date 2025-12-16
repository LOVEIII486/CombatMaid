using System;
using System.Collections.Generic;
using UnityEngine;
using CombatMaid.Localization;
using Random = UnityEngine.Random;

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
        
        private readonly HashSet<int> _elementalGrenadeIds = new HashSet<int> { 933, 941, 942 };
        
        private readonly Lazy<string> _txtExecute = new Lazy<string>(() => 
            LocalizationManager.GetText("Skill_GrenadeThrow_Execute"));

        public Skill_GrenadeThrower(List<int> grenadeIds) 
        {
            _grenadePool = grenadeIds ?? new List<int>();
        }

        protected override bool CheckTriggerCondition()
        {
            if (Controller == null || Controller.AI == null) return false;
            
            var target = Controller.AI.searchedEnemy;
            if (target == null || target.health.IsDead) return false;

            float dist = Vector3.Distance(Owner.transform.position, target.transform.position);
            
            // 只有在射程内，且不会炸到自己的距离才扔
            return dist >= _minRange && dist <= _throwRange;
        }

        protected override bool TryExecute()
        {
            var target = Controller.AI.searchedEnemy;
            if (target == null) return false;

            // 池子是否为空
            if (_grenadePool == null || _grenadePool.Count == 0)
            {
                CMDebug.LogWarning($"[{SkillName}] 手雷配置列表为空，无法执行技能");
                return false;
            }
    
            // 1. 根据配置生成有效列表
            List<int> validGrenades;
            if (CombatMaid.Settings.CombatMaidConfig.EnableElementalGrenades)
            {
                validGrenades = _grenadePool;
            }
            else
            {
                validGrenades = new List<int>();
                foreach (var id in _grenadePool)
                {
                    if (!_elementalGrenadeIds.Contains(id))
                    {
                        validGrenades.Add(id);
                    }
                }
            }
    
            // 2. 检查有效列表
            if (validGrenades.Count == 0)
            {
                // CMDebug.Log($"[{SkillName}] 没有可用的非元素手雷，跳过执行。");
                return false; 
            }
    
            // 3. 从 validGrenades 中随机
            int index = Random.Range(0, validGrenades.Count);
            int selectedItemId = validGrenades[index];

            MaidSkillHelper.LaunchGrenade(
                Owner, 
                selectedItemId, 
                target.transform.position, 
                delay: 1.3f, 
                canHurtSelf: false
            );

            //CMDebug.Log($"[{SkillName}] 随机投掷手雷 (ID: {selectedItemId})");
            Owner.PopText(_txtExecute.Value);
            return true;
        }
    }
}