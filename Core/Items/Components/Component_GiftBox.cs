using System.Collections.Generic;
using CombatMaid.Localization;
using ItemStatsSystem;

namespace CombatMaid.Core.Items.Components
{
    /// <summary>
    /// 酒狐的圣诞礼物：打开获得礼物，并由酒狐送上祝福
    /// </summary>
    public class Component_GiftBox : UsageBehavior
    {
        private static readonly List<int> GiftContentIds = new List<int> { 88105, 88106, 135 };

        public override bool CanBeUsed(Item item, object user)
        {
            if (!(user is CharacterMainControl character)) return false;
            return !character.Health.IsDead;
        }

        protected override void OnUse(Item item, object user)
        {
            if (!(user is CharacterMainControl player)) return;

            foreach (int id in GiftContentIds)
            {
                Item giftItem = ItemAssetsCollection.InstantiateSync(id);
                
                if (giftItem != null)
                {
                    ItemUtilities.SendToPlayer(giftItem, false);
                }
                else
                {
                    CMDebug.LogWarning($"圣诞礼物 {id} 生成失败");
                }
            }
            HandleMaidGreeting(player);
        }

        private void HandleMaidGreeting(CharacterMainControl player)
        {
            string greeting = LocalizationManager.GetText("Item_GiftBox_Open_Pop");
            var wineFox = MaidManager.Instance.GetActiveWineFox();

            // 如果酒狐在场
            if (wineFox != null && wineFox.MaidCharacter != null)
            {
                wineFox.MaidCharacter.PopText(greeting);
            }
            else
            {
                player.PopText(greeting);
            }
        }
    }
}