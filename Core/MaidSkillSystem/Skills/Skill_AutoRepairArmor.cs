using System;
using System.Linq;
using UnityEngine;
using ItemStatsSystem;
using CombatMaid.Localization;
using CombatMaid.Core.Items.Components;

namespace CombatMaid.Core.MaidSkillSystem.Skills
{
    public class Skill_AutoRepairArmor : MaidSkillBase
    {
        public override string SkillName => "AutoRepairArmor";
        public override float Cooldown => 5.0f; 
        public override bool RespectGlobalCooldown => true;
        public override float TriggerGCDDuration => 3.0f;

        private const float DurabilityThreshold = 0.25f;
        private const float RepairSafeWindow = 6.0f; 
        
        private readonly Lazy<string> _txtUseRepair = new Lazy<string>(() => 
            LocalizationManager.GetText("Skill_AutoRepair_Use", "正在维护装备..."));

        protected override bool CheckTriggerCondition()
        {
            // 基础检查
            if (Controller == null || Controller.AI == null) return false;
            if (Owner == null || Owner.Health == null || Owner.Health.IsDead) return false;
            
            // 1. 手部忙碌检查 (修装备需要双手空闲)
            if (!Owner.CanUseHand()) return false;

            // 2. 严格的非战斗判定
            // 如果有锁定的敌人 或 处于警戒状态，绝对不修
            if (Controller.AI.searchedEnemy != null || Controller.AI.alert) return false;
            if (Controller.AI.aimTarget != null) return false;

            // 3. 受击/战斗保护时间检查
            // 必须距离上次受伤或开火超过10秒
            bool recentlyActive = Time.time < Controller.AI.hurtTimeMarker + RepairSafeWindow;
            // 如果AI有记录攻击时间，最好也加上攻击时间的判断，这里暂用受击时间
            if (recentlyActive) return false;

            // 4. 检查是否有需要修的装备
            if (!HasLowDurabilityArmor()) return false;

            return true;
        }

        protected override bool TryExecute()
        {
            return ExecuteRepairLogic();
        }

        private bool ExecuteRepairLogic()
        {
            var inventory = Owner.CharacterItem.Inventory;
            if (inventory == null) return false;

            // 寻找维修物品 (优先找 Component_ArmorRepairPotion)
            var repairItem = inventory
                .Where(item => item != null && item.StackCount > 0)
                .FirstOrDefault(item => IsRepairItem(item) && IsItemUsable(item));

            if (repairItem != null)
            {
                // 执行使用
                Owner.UseItem(repairItem);
                
                // 气泡提示 (可选)
                Owner.PopText(string.Format(_txtUseRepair.Value, repairItem.DisplayName));
                
                // 触发公CD
                if (RespectGlobalCooldown)
                {
                    Controller.SkillSystem.TriggerGlobalCooldown(TriggerGCDDuration);
                }
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// 检查全身装备是否有耐久过低的
        /// </summary>
        private bool HasLowDurabilityArmor()
        {
            if (Owner.CharacterItem == null || Owner.CharacterItem.Slots == null) return false;

            foreach (var slot in Owner.CharacterItem.Slots)
            {
                if (slot == null || slot.Content == null) continue;
                var item = slot.Content;

                // 必须是防具 (复用之前的判断逻辑，或者简单判断Tag)
                if (IsArmor(item))
                {
                    // 使用 MaxDurabilityWithLoss (磨损后的上限) 作为基准
                    if (item.MaxDurabilityWithLoss <= 0) continue;

                    float ratio = item.Durability / item.MaxDurabilityWithLoss;
                    if (ratio < DurabilityThreshold)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 判断物品是否为指定的维修药剂
        /// </summary>
        private bool IsRepairItem(Item item)
        {
            // 方法A: 既然我们写了专用组件，直接找组件最准确
            if (item.GetComponent<Component_ArmorRepairPotion>() != null) return true;

            // 方法B: 如果你想兼容ID (例如 88103)
            // if (item.TypeID == 88103) return true;

            return false;
        }
        
        private bool IsArmor(Item item)
        {
            if (item.Tags.Contains("Weapon")) return false;
            return item.Tags.Contains("Armor") || item.Tags.Contains("Helmet") || item.Tags.Contains("BodyArmor");
        }

        private bool IsItemUsable(Item item)
        {
            // 再次确认该物品的 UsageBehavior 是否允许当前使用
            // 这会调用 Component_ArmorRepairPotion.CanBeUsed 进行二次验证(是否有受损装备)
            var usageLogic = item.GetComponent<UsageBehavior>();
            return usageLogic != null && usageLogic.CanBeUsed(item, Owner);
        }
    }
}