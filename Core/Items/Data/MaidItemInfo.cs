using System;
using System.Collections.Generic;
using FastModdingLib;

namespace CombatMaid.Core.Items.Data
{
    /// <summary>
    /// 扩展后的物品定义类
    /// </summary>
    public class MaidItemInfo : ItemData
    {
        // ==================== 模组核心扩展 ====================

        /// <summary>
        /// [视觉回退] 当资源缺失借用哪个原版物品的模型/图标/Agent配置？
        /// <para>0 表示不借用，使用模组自带资源或保底</para>
        /// </summary>
        public int VisualReferenceId = 0;

        /// <summary>
        /// [逻辑扩展] 需要挂载的自定义逻辑脚本类型
        /// <para>必须继承自 MonoBehaviour，例如 typeof(Component_MaidContract)</para>
        /// </summary>
        public Type CustomComponentType = null;

        /// <summary>
        /// [参数注入] 需要写入 item.Constants 的额外数据
        /// <para>例如 { "ConsumeOnUse", true }</para>
        /// </summary>
        public Dictionary<string, object> CustomConstants = new Dictionary<string, object>();

        // ==================== 商店销售配置 ====================

        /// <summary>
        /// [商人ID] 该物品在哪个商人处出售？
        /// <para>使用 MerchantIds 常量。设为 null 则不注入商店。</para>
        /// </summary>
        public string ShopMerchantId = MerchantIds.Mud;

        /// <summary>
        /// [库存] 商人每次刷新时的最大持有量 (默认: 5)
        /// </summary>
        public int ShopMaxStock = 5;

        /// <summary>
        /// [价格] 价格倍率 (基于物品基础 Value 计算)
        /// </summary>
        public float ShopPriceFactor = 1.0f;

        /// <summary>
        /// [概率] 物品在商店刷新的概率 (0.0 ~ 1.0)
        /// </summary>
        public float ShopPossibility = 1.0f;

        /// <summary>
        /// [解锁] 是否强制解锁
        /// </summary>
        public bool ShopForceUnlock = true;
    }
}