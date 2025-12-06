using UnityEngine;
using Duckov.PerkTrees.Behaviours;
using Duckov.Economy;
using Duckov.PerkTrees;

namespace CombatMaid.Core.SkillTreeSystem
{
    /// <summary>
    /// 当技能解锁时，自动解锁对应的商店商品
    /// </summary>
    public class PerkUnlockStockShop : PerkBehaviour
    {
        [SerializeField] public int unlockItem; // 商品 ID

        protected override void OnUnlocked()
        {
            // 直接调用游戏底层的经济系统解锁
            // 确保你引用了正确的 EconomyManager 命名空间
            EconomyManager.Unlock(unlockItem);
            CMDebug.Log($"[Perk] 技能解锁 -> 商店商品已解锁: {unlockItem}");
        }
    }
}