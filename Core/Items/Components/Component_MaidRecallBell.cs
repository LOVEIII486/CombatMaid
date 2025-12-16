using System;
using CombatMaid.Localization;
using UnityEngine;
using ItemStatsSystem;

namespace CombatMaid.Core.Items.Components
{
    /// <summary>
    /// 女仆召回铃：使用时解散所有女仆
    /// </summary>
    public class Component_MaidRecallBell : MonoBehaviour
    {
        private Item _item;
        
        private readonly Lazy<string> _txtOnUse =  new Lazy<string>(() =>LocalizationManager.GetText("Item_MaidRecallBell_OnUse"));

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
            
            if (MaidManager.Instance != null)
            {
                MaidManager.Instance.DespawnTeam();
                player.PopText(_txtOnUse.Value);
            }
        }

        private void OnDestroy()
        {
            if (_item != null) _item.onUse -= OnUseItem;
        }
    }
}