using UnityEngine;
using ItemStatsSystem;
using CombatMaid.Core;

namespace CombatMaid.Core.Items.Components
{
    public class Component_RecoveryPotion : MonoBehaviour
    {
        private Item _item;

        // 配置参数
        private const float HealPercent = 0.4f; // 40%
        private const float MinHealValue = 30f; // 最低30点

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
            // === 1. 安全检查 (防止 NPE 报错) ===
            if (item == null) 
            {
                CMDebug.LogWarning("[MaidMedkit] Item 引用为空！");
                return;
            }

            var character = user as CharacterMainControl;
            if (character == null)
            {
                // 可能是非角色对象使用了物品，直接忽略
                return; 
            }

            if (character.Health == null || character.Health.IsDead)
            {
                // 角色已死或无血条组件
                return;
            }

            // === 2. 执行治疗逻辑 ===
            float maxHp = character.Health.MaxHealth;
            float healAmount = Mathf.Max(maxHp * HealPercent, MinHealValue);

            character.AddHealth(healAmount);

            // 视觉反馈
            // string healText = $"+{healAmount:F0}";
            // character.PopText($"<color=#00FF00>{healText}</color>");
        }

        private void OnDestroy()
        {
            if (_item != null) _item.onUse -= OnUseItem;
        }
    }
}