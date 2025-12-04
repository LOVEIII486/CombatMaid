using System;
using System.Collections.Generic;
using FastModdingLib;

namespace CombatMaid.Core.Items.Data
{
    /// <summary>
    /// 扩展后的物品定义类
    /// 包含了 FastModdingLib 的基础数据，以及战斗女仆模组所需的额外配置
    /// </summary>
    public class MaidItemInfo : ItemData
    {
        // --- 模组扩展属性 ---

        /// <summary>
        /// 该物品在哪个商人处出售？(使用 MerchantIds 常量)
        /// 默认为空，表示不注入商店
        /// </summary>
        public string ShopMerchantId = MerchantIds.Mud;

        /// <summary>
        /// 当资源缺失或为了偷懒时，借用哪个原版物品的模型/图标/Agent配置？
        /// 0 表示不借用
        /// </summary>
        public int VisualReferenceId = 0;

        /// <summary>
        /// 需要挂载的自定义逻辑脚本类型 (例如 typeof(Component_MaidContract))
        /// </summary>
        public Type CustomComponentType = null;

        /// <summary>
        /// 需要写入 item.Constants 的额外数据 (例如 ConsumeOnUse)
        /// </summary>
        public Dictionary<string, object> CustomConstants = new Dictionary<string, object>();
    }
}