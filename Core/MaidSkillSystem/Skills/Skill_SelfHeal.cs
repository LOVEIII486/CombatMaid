using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Duckov.ItemUsage;
using ItemStatsSystem;
using CombatMaid.Localization;

namespace CombatMaid.Core.MaidSkillSystem.Skills
{
    public class Skill_SelfHeal : MaidSkillBase
    {
        public override string SkillName => "SelfHeal";
        public override float Cooldown => 2.0f;

        public override bool RespectGlobalCooldown => true;
        public override float TriggerGCDDuration => 1.5f;

        private const float HealthThreshold = 0.8f;
        private const float CriticalThreshold = 0.2f;

        // 受击保护时间
        private const float HurtSafeWindow = 3.0f;

        private readonly HashSet<int> _normalMedIds = new HashSet<int>
            { 88101, 15, 16, 17, 20, 10, 875, 1245, 1244, 1243, 1246 };

        private readonly List<int> _priorityList = new List<int>
            { 88101, 15, 16, 1246, 1244, 17, 20, 1243, 1245, 10, 875 };

        private readonly Lazy<string> _txtUseItem = new Lazy<string>(() =>
            LocalizationManager.GetText("Skill_SelfHeal_UseItem"));

        private readonly Lazy<string> _txtNoDrug = new Lazy<string>(() =>
            LocalizationManager.GetText("Skill_SelfHeal_NoDrug"));

        protected override bool CheckTriggerCondition()
        {
            if (Controller == null || Controller.AI == null) return false;
            if (Owner == null || Owner.Health == null || Owner.Health.IsDead) return false;

            float hpPercent = Owner.Health.CurrentHealth / Owner.Health.MaxHealth;
            if (hpPercent >= HealthThreshold) return false;

            // 1. 受击保护
            bool recentlyHurt = Time.time < Controller.AI.hurtTimeMarker + HurtSafeWindow;
            if (recentlyHurt) return false;

            // 2. 战斗状态判定
            bool inCombat = Controller.AI.searchedEnemy != null && Controller.AI.alert;

            // 3. 紧急情况
            if (hpPercent < CriticalThreshold)
            {
                // 如果手上有动作不打断
                if (!Owner.CanUseHand()) return false;
                // 过了受击保护期
                return true;
            }

            // 4. 非紧急情况
            if (inCombat || Controller.AI.aimTarget != null) return false;

            return true;
        }

        protected override bool TryExecute()
        {
            return ExecuteHealLogic();
        }

        public void ForceActivate()
        {
            if (Owner == null || Owner.Health.IsDead) return;
            ExecuteHealLogic(isForce: true);
        }

        private bool ExecuteHealLogic(bool isForce = false)
        {
            var inventory = Owner.CharacterItem.Inventory;
            if (inventory == null) return false;

            if (!Owner.CanUseHand())
            {
                if (isForce) Owner.PopText("忙碌中");
                return false;
            }

            var bestDrug = inventory
                .Where(item => item != null && item.StackCount > 0)
                .Where(item => IsMedicItem(item))
                .Where(item => IsItemUsable(item))
                .OrderBy(item =>
                {
                    int index = _priorityList.IndexOf(item.TypeID);
                    return index == -1 ? 1000 : index;
                })
                .FirstOrDefault();

            if (bestDrug != null)
            {
                Owner.UseItem(bestDrug);
                Owner.PopText(string.Format(_txtUseItem.Value, bestDrug.DisplayName));

                if (isForce || RespectGlobalCooldown)
                {
                    Controller.SkillSystem.TriggerGlobalCooldown(TriggerGCDDuration);
                }

                return true;
            }

            if (isForce) Owner.PopText(_txtNoDrug.Value);
            return false;
        }

        private bool IsMedicItem(Item item)
        {
            if (_priorityList.Contains(item.TypeID)) return true;
            if (_normalMedIds.Contains(item.TypeID)) return true;
            if (item.GetComponent<Drug>() != null) return true;
            return false;
        }

        private bool IsItemUsable(Item item)
        {
            var usageLogic = item.GetComponent<UsageBehavior>();
            return usageLogic != null && usageLogic.CanBeUsed(item, Owner);
        }
    }
}