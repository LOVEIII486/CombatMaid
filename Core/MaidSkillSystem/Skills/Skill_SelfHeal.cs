using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Duckov.ItemUsage;
using ItemStatsSystem;
using CombatMaid.Core;

namespace CombatMaid.Core.MaidSkillSystem.Skills
{
    public class Skill_SelfHeal : MaidSkillBase
    {
        // === 1. 基础配置 ===
        public override string SkillName => "SelfHeal";
        public override float Cooldown => 3.0f;
        
        public override bool RespectGlobalCooldown => true;
        public override float TriggerGCDDuration => 1.0f;

        private const float HealthThreshold = 0.7f; // 70% 血以下触发
        
        // 普通药品 ID 集合
        private readonly HashSet<int> _normalMedIds = new HashSet<int> { 88101, 15, 16, 17, 20, 10, 875 };
        // 优先级列表：越靠前越优先使用
        private readonly List<int> _priorityList = new List<int> { 88101, 15, 16, 17, 20, 10, 875 };

        protected override bool CheckTriggerCondition()
        {
            if (Owner == null || Owner.Health == null) return false;
            if (Controller.AI == null) return false;

            float hpPercent = Owner.Health.CurrentHealth / Owner.Health.MaxHealth;
            if (hpPercent >= HealthThreshold) return false;
            
            // 紧急情况：血量低于 30%，无视状态直接吃药保命
            if (hpPercent < 0.3f) return true;

            // 非紧急情况（30%~70%）：如果有攻击目标则暂不吃药
            if (Controller.AI.aimTarget != null) return false;

            return true;
        }

        protected override bool TryExecute()
        {
            return ExecuteHealLogic();
        }

        /// <summary>
        /// 强制触发接口
        /// </summary>
        public void ForceActivate()
        {
            if (Owner == null || Owner.Health.IsDead) return;
            // CMDebug.Log($"[{SkillName}] 收到强制治疗指令");
            ExecuteHealLogic(isForce: true);
        }

        /// <summary>
        /// 核心治疗逻辑：筛选 -> 排序 -> 使用
        /// </summary>
        private bool ExecuteHealLogic(bool isForce = false)
        {
            var inventory = Owner.CharacterItem.Inventory;
            if (inventory == null) return false;

            var bestDrug = inventory
                // 1. 基础过滤
                .Where(item => item != null && item.StackCount > 0)
                // 2. 类型过滤
                .Where(item => IsMedicItem(item))
                // 3. 优先级排序
                .OrderBy(item => 
                {
                    int index = _priorityList.IndexOf(item.TypeID);
                    return index == -1 ? 1000 : index;
                })
                // 4. 取第一个
                .FirstOrDefault();

            if (bestDrug != null)
            {
                Owner.UseItem(bestDrug);
                Owner.PopText($"使用: {bestDrug.DisplayName}");
                
                if (isForce)
                {
                    Controller.SkillSystem.TriggerGlobalCooldown(TriggerGCDDuration);
                }
                return true;
            }
            
            Owner.PopText("主人我没有药了!"); 
            return false;
        }

        /// <summary>
        /// 辅助判断药
        /// </summary>
        private bool IsMedicItem(Item item)
        {
            // 优先级列表
            if (_priorityList.Contains(item.TypeID)) return true;
            
            // 普通药品列表
            if (_normalMedIds.Contains(item.TypeID)) return true;

            // 带有 Drug 组件
            if (item.GetComponent<Drug>() != null) return true;

            return false;
        }
    }
}