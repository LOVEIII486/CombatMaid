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
            EconomyManager.Unlock(unlockItem);
            CMDebug.Log($"[Perk] 技能解锁 -> 商店商品已解锁: {unlockItem}");
        }
    }
}