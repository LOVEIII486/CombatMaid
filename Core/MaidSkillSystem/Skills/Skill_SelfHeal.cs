using System.Collections.Generic;
using UnityEngine;
using Duckov.ItemUsage;

namespace CombatMaid.Core.MaidSkillSystem.Skills
{
    public class Skill_SelfHeal : MaidSkillBase
    {
        public override string SkillName => "SelfHeal";
        public override float Cooldown => 5.0f;

        private const float HealthThreshold = 0.8f; // 80% 血以下触发
        private readonly HashSet<int> _medIds = new HashSet<int> { 10, 20, 17, 3, 15, 16 };

        protected override bool CheckTriggerCondition()
        {
            if (Owner == null || Owner.Health == null) return false;

            // 只有血量低于阈值才自动触发
            if (Owner.Health.CurrentHealth / Owner.Health.MaxHealth >= HealthThreshold) 
                return false;
            
            return true;
        }

        protected override bool TryExecute()
        {
            return ExecuteHealLogic();
        }

        /// <summary>
        /// 强制触发接口，忽略血量阈值和冷却
        /// </summary>
        public void ForceActivate()
        {
            if (Owner == null || Owner.Health.IsDead) return;

            CMDebug.Log($"[{SkillName}] 收到强制治疗指令");
            
            ExecuteHealLogic(isForce: true);
        }

        /// <summary>
        /// 核心逻辑：遍历背包 -> 找药 -> 使用
        /// </summary>
        private bool ExecuteHealLogic(bool isForce = false)
        {
            var inventory = Owner.CharacterItem.Inventory;
            if (inventory == null) return false;

            foreach (var item in inventory)
            {
                if (item == null || item.StackCount <= 0) continue;
                
                bool isDrug = item.GetComponent<Drug>() != null || _medIds.Contains(item.TypeID);
                
                if (isDrug)
                {
                    Owner.UseItem(item);
                    Owner.PopText($"使用药品: {item.DisplayName}");
                    return true;
                }
            }

            // 没药了
            Owner.PopText("缺药!"); 
            return false;
        }
    }
}