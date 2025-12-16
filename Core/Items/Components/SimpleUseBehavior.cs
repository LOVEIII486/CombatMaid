using UnityEngine;
using ItemStatsSystem;
using Duckov.ItemUsage; 

namespace CombatMaid.Core.Items.Components
{
    /// <summary>
    /// 仅用于激活“使用”按钮
    /// </summary>
    public class SimpleUseBehavior : UsageBehavior
    {
        public override bool CanBeUsed(Item item, object user)
        {
            return true; 
        }

        // 必须是 protected override void
        protected override void OnUse(Item item, object user) { }
    }
}