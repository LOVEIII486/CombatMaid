using UnityEngine;
using ItemStatsSystem;
using CombatMaid.Core;

namespace CombatMaid.Core.Items.Components
{
    /// <summary>
    /// 女仆召回铃铛组件：使用时解散所有女仆
    /// </summary>
    public class Component_MaidRecallBell : MonoBehaviour
    {
        private Item _item;

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
            var player = user as CharacterMainControl;
            if (player == null) return;

            // 检查管理器是否存在
            if (MaidManager.Instance != null)
            {
                MaidManager.Instance.DespawnTeam();
                player.PopText("全员撤退！");
            }
        }

        private void OnDestroy()
        {
            if (_item != null) _item.onUse -= OnUseItem;
        }
    }
}