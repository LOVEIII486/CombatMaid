using System;
using UnityEngine;
using Duckov.ItemUsage; 
using ItemStatsSystem;   
using CombatMaid.Localization;

namespace CombatMaid.Core.Items.Components
{
    /// <summary>
    /// 酒狐的魔力抛光剂：按照百分比修复已装备的防具
    /// </summary>
    public class Component_ArmorRepairPotion : UsageBehavior
    {
        private const float RepairPercent = 0.25f; 

        private static readonly int _headArmorHash = "HeadArmor".GetHashCode();
        private static readonly int _bodyArmorHash = "BodyArmor".GetHashCode();
        private static readonly int _helmatHash = "Helmat".GetHashCode();
        private static readonly int _armorHash = "Armor".GetHashCode();

        private readonly Lazy<string> _txtRepair = new Lazy<string>(() => 
            LocalizationManager.GetText("Item_ArmorRepairPotion_Used"));
        private readonly Lazy<string> _txtNoTarget = new Lazy<string>(() => 
            LocalizationManager.GetText("Item_ArmorRepairPotion_NoTarget"));

        public override bool CanBeUsed(Item item, object user)
        {
            if (!(user is CharacterMainControl character)) return false;
            if (character.Health.IsDead) return false;

            return HasDamagedArmor(character);
        }

        protected override void OnUse(Item item, object user)
        {
            var character = user as CharacterMainControl;
            if (character == null) return;

            int repairedCount = RepairEquippedArmor(character);

            if (repairedCount > 0)
            {
                string percentText = (RepairPercent * 100).ToString("F0");
                character.PopText($"<color=#00FFFF>{_txtRepair.Value} *{repairedCount} (+{percentText}%)</color>");
            }
            else
            {
                character.PopText(_txtNoTarget.Value);
            }
        }

        /// <summary>
        /// 检查角色身上是否有受损的防具
        /// </summary>
        private bool HasDamagedArmor(CharacterMainControl character)
        {
            var rootItem = character.CharacterItem;
            if (rootItem == null || rootItem.Slots == null) return false;

            foreach (var slot in rootItem.Slots)
            {
                if (slot == null || slot.Content == null) continue;
                var equipment = slot.Content;

                if (IsArmor(equipment) && equipment.Durability < equipment.MaxDurabilityWithLoss)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 修复所有已装备的防具
        /// </summary>
        private int RepairEquippedArmor(CharacterMainControl character)
        {
            var rootItem = character.CharacterItem;
            if (rootItem == null || rootItem.Slots == null) return 0;

            int count = 0;
            foreach (var slot in rootItem.Slots)
            {
                if (slot == null || slot.Content == null) continue;
                var equipment = slot.Content;

                if (IsArmor(equipment) && equipment.Durability < equipment.MaxDurabilityWithLoss)
                {
                    float repairValue = equipment.MaxDurabilityWithLoss * RepairPercent;
                    
                    // 至少修复 1 点耐久
                    if (repairValue < 1f) repairValue = 1f;

                    // 不超过磨损上限
                    float targetDurability = Mathf.Min(equipment.Durability + repairValue, equipment.MaxDurabilityWithLoss);
                    
                    equipment.Durability = targetDurability;
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// 判断是否为防具
        /// </summary>
        private bool IsArmor(Item item)
        {
            if (item == null) return false;
            
            // 排除武器
            if (item.Tags.Contains("Weapon")) return false;

            // 再次注意是Helmat！！！
            if (item.Tags.Contains("Armor") || item.Tags.Contains("BodyArmor") ||
                item.Tags.Contains("Helmat") || item.Tags.Contains("HeadArmor")) 
            {
                return true;
            }

            // 属性检查
            if (item.GetStatValue(_headArmorHash) > 0f || 
                item.GetStatValue(_bodyArmorHash) > 0f || 
                item.GetStatValue(_helmatHash) > 0f || 
                item.GetStatValue(_armorHash) > 0f)
            {
                return true;
            }

            return false;
        }
    }
}