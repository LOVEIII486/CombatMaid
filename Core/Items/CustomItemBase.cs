using UnityEngine;
using Duckov.ItemBuilders;
using ItemStatsSystem;

namespace CombatMaid.Core.Items
{
    /// <summary>
    /// 自定义物品基类
    /// </summary>
    public abstract class CustomItemBase
    {
        public abstract int ItemID { get; }
        public abstract string NameKey { get; } // 本地化Key
        public abstract int Price { get; }
        public virtual int MaxStock => 5;
        
        // 返回一个图标路径或直接返回Sprite，这里简化为复用游戏内图标ID
        // 你可以通过 Resources.Load<Sprite>("路径") 来加载
        public abstract Sprite GetIcon();

        /// <summary>
        /// 使用 ItemBuilder 构建物品实例
        /// </summary>
        public virtual Item Build()
        {
            var builder = ItemBuilder.New()
                .TypeID(ItemID)
                .Icon(GetIcon())
                .EnableStacking(10, 1) // 默认可堆叠
                .SetConstant("CM_CustomItem", true, false) // 标记这是我们的物品
                .SetConstant("CM_Price", Price, true);

            // 子类可以在这里扩展更多属性
            OnBuild(builder);

            return builder.Instantiate();
        }

        protected virtual void OnBuild(ItemBuilder builder) { }

        /// <summary>
        /// 物品被使用时的逻辑
        /// </summary>
        public abstract bool OnUsed(CharacterMainControl user);
    }
}