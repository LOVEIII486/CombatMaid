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
        // 1. 判定方法：必须是 public override bool
        // 对应错误 CS0534 (未实现) 和 CS0115 (找不到方法)
        public override bool CanBeUsed(Item item, object user)
        {
            return true; 
        }

        // 2. 执行方法：必须是 protected override void
        // 对应错误 CS0507 (无法更改访问修饰符)
        protected override void OnUse(Item item, object user)
        {
            // 这里留空即可
            // 实际的召唤逻辑在 Component_MaidContract 的 item.onUse 事件中执行
        }
    }
}