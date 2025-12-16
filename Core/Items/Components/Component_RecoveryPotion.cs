using UnityEngine;
using ItemStatsSystem;

namespace CombatMaid.Core.Items.Components
{
    public class Component_RecoveryPotion : MonoBehaviour
    {
        private Item _item;

        private const float HealPercent = 0.4f;
        private const float MinHealValue = 30f; // 最低30

        private void Awake()
        {
            _item = GetComponent<Item>();
            if (_item != null)
            {
                _item.onUse += OnUseItem;
            }
        }

        private void OnUseItem(Item item, object user)
        {
            if (item == null) 
            {
                return;
            }

            var character = user as CharacterMainControl;
            if (character == null)
            {
                return; 
            }

            if (character.Health == null || character.Health.IsDead)
            {
                return;
            }
            
            float maxHp = character.Health.MaxHealth;
            float healAmount = Mathf.Max(maxHp * HealPercent, MinHealValue);
            character.AddHealth(healAmount);
            character.RemoveBuff(1001,false);
            character.RemoveBuff(1002,false);
            character.RemoveBuff(1003,false);
            character.RemoveBuff(1004,false);
            // string healText = $"+{healAmount:F0}";
            // character.PopText($"<color=#00FF00>{healText}</color>");
        }

        private void OnDestroy()
        {
            if (_item != null) _item.onUse -= OnUseItem;
        }
    }
}