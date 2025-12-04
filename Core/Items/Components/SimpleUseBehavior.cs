using UnityEngine;
using ItemStatsSystem;
using Duckov.ItemUsage; 

namespace CombatMaid.Core.Items.Components
{
    /// <summary>
    /// 一个没有任何限制条件的通用使用行为
    /// 用于激活“使用”按钮，具体逻辑由 OnUse 事件接管
    /// </summary>
    public class SimpleUseBehavior : UsageBehavior
    {
        public override bool CanBeUsed(Item item, object user)
        {
            return true; 
        }

        // 必须是 protected override void
        protected override void OnUse(Item item, object user)
        {
            // 留空
        }
    }
}