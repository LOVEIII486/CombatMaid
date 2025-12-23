using System;
using CombatMaid.Core.WineFox;
using ItemStatsSystem;
using Cysharp.Threading.Tasks;

namespace CombatMaid.Core.MaidEventSystem.Events
{
    public class ChristmasGiftEvent : IMaidEvent
    {
        private const int GIFT_BOX_ID = 88107;

        public bool IsDateActive()
        {
            DateTime now = DateTime.Now;
            // 圣诞节判定范围：12.24 - 12.26
            return now.Month == 12 && now.Day >= 24 && now.Day <= 26;
        }

        public void OnMaidRegistered(MaidController maid, CharacterMainControl player)
        {
            if (maid.GetComponent<WineFoxDataSync>() == null) return;

            string recordKey = $"MaidEvent_XmasGift_{DateTime.Now.Year}";
            if (player.CharacterItem != null && player.CharacterItem.GetInt(recordKey, 0) == 0)
            {
                ExecuteDelayedGift(player, recordKey).Forget();
            }
        }

        private async UniTaskVoid ExecuteDelayedGift(CharacterMainControl player, string recordKey)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(5f));
            if (player?.CharacterItem == null) return;

            Item gift = ItemAssetsCollection.InstantiateSync(GIFT_BOX_ID);
            if (gift != null)
            {
                ItemUtilities.SendToPlayer(gift);
                player.CharacterItem.SetInt(recordKey, 1);
                player.PopText(CombatMaid.Localization.LocalizationManager.GetText("Item_GiftBox_Received_Msg"));
            }
        }
    }
}