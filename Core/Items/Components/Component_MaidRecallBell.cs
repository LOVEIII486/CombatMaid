using System;
using ItemStatsSystem;
using CombatMaid.Localization;

namespace CombatMaid.Core.Items.Components
{
    /// <summary>
    /// 女仆召回铃：使用时解散所有女仆
    /// </summary>
    public class Component_MaidRecallBell : UsageBehavior
    {
        private readonly Lazy<string> _txtOnUse = new Lazy<string>(() => 
            LocalizationManager.GetText("Item_MaidRecallBell_OnUse"));

        public override bool CanBeUsed(Item item, object user)
        {
            if (!(user is CharacterMainControl)) return false;
            
            if (MaidManager.Instance == null) return false;

            if (MaidManager.Instance.ActiveMaidCount <= 0) return false;
            
            return true;
        }

        protected override void OnUse(Item item, object user)
        {
            var player = user as CharacterMainControl;
            if (player == null) return;

            if (MaidManager.Instance != null)
            {
                // 执行解散逻辑
                MaidManager.Instance.DespawnTeam();
                player.PopText(_txtOnUse.Value);
            }
        }
    }
}