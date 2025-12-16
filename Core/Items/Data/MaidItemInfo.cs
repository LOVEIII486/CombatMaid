using System;
using System.Collections.Generic;
using FastModdingLib;

namespace CombatMaid.Core.Items.Data
{
    /// <summary>
    /// 扩展后的物品定义类
    /// <para>=== 父类 ItemData 可用字段备忘 ===</para>
    /// <para>itemId (int), localizationKey (string), localizationDesc (string)</para>
    /// <para>value (int), weight (float), maxStackCount (int)</para>
    /// <para>quality (int), displayQuality (DisplayQuality), spritePath (string)</para>
    /// <para>tags (List string), usages (UsageData)</para>
    /// </summary>
    public class MaidItemInfo : ItemData
    {
        public int VisualReferenceId = 0;
        public Type CustomComponentType = null;
        //需要写入 item.Constants 的额外数据
        public Dictionary<string, object> CustomConstants = new Dictionary<string, object>();
        
        public string ShopMerchantId = MerchantIds.Mud;
        public int ShopMaxStock = 5;
        public float ShopPriceFactor = 1.0f;
        public float ShopPossibility = 1.0f;
        public bool ShopForceUnlock = true;
    }
}