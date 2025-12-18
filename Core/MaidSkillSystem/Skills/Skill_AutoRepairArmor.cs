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

        private const float StartRepairThreshold = 0.55f;
        private const float StopRepairThreshold = 0.95f;
        
        private const float RepairSafeWindow = 6.0f; 

        // 标记当前是否处于“正在连续维修”的状态
        private bool _isUnderMaintenance = false;
        
        private readonly Lazy<string> _txtUseRepair = new Lazy<string>(() => 
            LocalizationManager.GetText("Skill_AutoRepair_Use", "正在维护装备..."));

        protected override bool CheckTriggerCondition()
        {
            // --- 1. 基础安全性检查 ---
            if (Controller == null || Controller.AI == null) return false;
            if (Owner == null || Owner.Health == null || Owner.Health.IsDead) return false;
            if (!Owner.CanUseHand()) return false;

            // --- 2. 战斗环境判定 (一旦进入战斗，强制打断维修状态) ---
            if (Controller.AI.searchedEnemy != null || Controller.AI.alert || Controller.AI.aimTarget != null) 
            {
                _isUnderMaintenance = false;
                return false;
            }

            // 受击保护
            bool recentlyActive = Time.time < Controller.AI.hurtTimeMarker + RepairSafeWindow;
            if (recentlyActive)
            {
                _isUnderMaintenance = false;
                return false;
            }

            // --- 3. 物品检查 (没药了就别修了) ---
            if (!HasRepairItem())
            {
                _isUnderMaintenance = false;
                return false;
            }

            // --- 4. 核心逻辑：双阈值判定 ---
            float lowestRatio = GetLowestDurabilityRatio();

            if (_isUnderMaintenance)
            {
                // 如果已经在维修模式中，直到修满 (>=95%) 才停止
                if (lowestRatio >= StopRepairThreshold)
                {
                    _isUnderMaintenance = false; // 任务完成
                    return false;
                }
                return true; // 继续修！
            }
            else
            {
                // 如果不在维修模式，只有低于 25% 才触发
                if (lowestRatio < StartRepairThreshold && lowestRatio > 0) // >0 排除没穿装备的情况
                {
                    _isUnderMaintenance = true; // 启动维修模式
                    return true;
                }
                return false;
            }
        }

        protected override bool TryExecute()
        {
            return ExecuteRepairLogic();
        }

        private bool ExecuteRepairLogic()
        {
            var inventory = Owner.CharacterItem.Inventory;
            if (inventory == null) return false;

            // 寻找维修物品
            var repairItem = inventory
                .Where(item => item != null && item.StackCount > 0)
                .FirstOrDefault(item => IsRepairItem(item) && IsItemUsable(item));

            if (repairItem != null)
            {
                Owner.UseItem(repairItem);
                Owner.PopText(string.Format(_txtUseRepair.Value, repairItem.DisplayName));
                
                if (RespectGlobalCooldown)
                {
                    Controller.SkillSystem.TriggerGlobalCooldown(TriggerGCDDuration);
                }
                return true;
            }
            
            // 如果执行时突然发现没物品了，重置状态
            _isUnderMaintenance = false;
            return false;
        }

        /// <summary>
        /// 获取全身装备中耐久度最低的比例 (0.0 - 1.0)
        /// 如果没穿装备，返回 1.0 (视为满状态)
        /// </summary>
        private float GetLowestDurabilityRatio()
        {
            if (Owner.CharacterItem == null || Owner.CharacterItem.Slots == null) return 1.0f;

            float minRatio = 1.0f;
            bool hasArmor = false;

            foreach (var slot in Owner.CharacterItem.Slots)
            {
                if (slot == null || slot.Content == null) continue;
                var item = slot.Content;

                if (IsArmor(item))
                {
                    if (item.MaxDurabilityWithLoss <= 0) continue;

                    float ratio = item.Durability / item.MaxDurabilityWithLoss;
                    if (ratio < minRatio) minRatio = ratio;
                    hasArmor = true;
                }
            }

            return hasArmor ? minRatio : 1.0f;
        }

        /// <summary>
        /// 判断是否为维修药剂
        /// </summary>
        private bool IsRepairItem(Item item)
        {   
            //if (item.TypeID == 88103) return true;
            return item.GetComponent<Component_ArmorRepairPotion>() != null;
        }
        
        /// <summary>
        /// 检查背包里有没有能用的维修包
        /// </summary>
        private bool HasRepairItem()
        {
             var inventory = Owner.CharacterItem?.Inventory;
             if (inventory == null) return false;
             return inventory.Any(item => item != null && item.StackCount > 0 && IsRepairItem(item));
        }
        
        private bool IsArmor(Item item)
        {
            if (item.Tags.Contains("Weapon")) return false;
            
            // 游戏底层使用的是 Helmat
            return item.Tags.Contains("Armor") || 
                   item.Tags.Contains("BodyArmor") || 
                   item.Tags.Contains("Helmet") || 
                   item.Tags.Contains("Helmat") || 
                   item.Tags.Contains("HeadArmor") ||
                   item.Tags.Contains("Vest");
        }

        private bool IsItemUsable(Item item)
        {
            var usageLogic = item.GetComponent<UsageBehavior>();
            return usageLogic != null && usageLogic.CanBeUsed(item, Owner);
        }
    }
}