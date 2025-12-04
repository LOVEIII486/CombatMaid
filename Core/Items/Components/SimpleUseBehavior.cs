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
        // 判定方法通常是 public 的，供 UI 调用检查
        public override bool CanBeUsed(Item item, object user)
        {
            return true; 
        }

        // [修正] 必须使用 protected，与父类保持一致
        // 核心：使用时不需要做任何底层逻辑，因为我们在 Component_MaidContract 里写了
        protected override void OnUse(Item item, object user)
        {
            // Do nothing
        }
    }
}