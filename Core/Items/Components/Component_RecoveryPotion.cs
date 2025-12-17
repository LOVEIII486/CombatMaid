using UnityEngine;
using ItemStatsSystem;

namespace CombatMaid.Core.Items.Components
{
    /// <summary>
    /// 强力回复药：百分比回血 + 解除异常状态
    /// </summary>
    public class Component_RecoveryPotion : UsageBehavior
    {
        private const float HealPercent = 0.4f; // 40%
        private const float MinHealValue = 30f; // 最低回复量
        
        public override bool CanBeUsed(Item item, object user)
        {
            if (!(user is CharacterMainControl character)) return false;

            if (character.Health.IsDead) return false;
            
            // 满血用于解除buff
            //if (character.Health.CurrentHealth >= character.Health.MaxHealth) return false;

            return true;
        }
        
        protected override void OnUse(Item item, object user)
        {
            var character = user as CharacterMainControl;
            if (character == null) return;

            float maxHp = character.Health.MaxHealth;
            float healAmount = Mathf.Max(maxHp * HealPercent, MinHealValue);

            character.AddHealth(healAmount);

            // 移除负面 Buff
            character.RemoveBuff(1001, false);
            character.RemoveBuff(1002, false);
            character.RemoveBuff(1003, false);
            character.RemoveBuff(1004, false);
            
            string healText = $"+{healAmount:F0}";
            character.PopText($"<color=#00FF00>{healText}</color>");
        }
    }
}