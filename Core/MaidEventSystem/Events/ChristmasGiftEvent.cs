using System;
using CombatMaid.Core.WineFox;
using ItemStatsSystem;
using Cysharp.Threading.Tasks;
using CombatMaid.Localization;

namespace CombatMaid.Core.MaidEventSystem.Events
{
    public class ChristmasGiftEvent : IMaidEvent
    {
        private const int GIFT_BOX_ID = 88107;

        public void OnMaidRegistered(MaidController maid, CharacterMainControl player)
        {
            if (maid.GetComponent<WineFoxDataSync>() == null || player?.CharacterItem == null) return;
           
            // 12月24日 - 26日
            DateTime now = DateTime.Now;
            bool isChristmas = (now.Month == 12 && now.Day >= 24 && now.Day <= 26);
            if (!isChristmas) return;

            // 存档记录检查
            string recordKey = $"MaidEvent_XmasGift_{now.Year}";
            if (player.CharacterItem.GetInt(recordKey, 0) != 0) return;

            ExecuteDelayedGift(player, recordKey, now.Year).Forget();
        }

        private async UniTaskVoid ExecuteDelayedGift(CharacterMainControl player, string recordKey, int year)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(3f));

            if (player == null || player.CharacterItem == null) return;

            Item gift = ItemAssetsCollection.InstantiateSync(GIFT_BOX_ID);
            if (gift != null)
            {
                // 参数：item, dontMerge=false, sendToStorage=true
                ItemUtilities.SendToPlayer(gift);
                
                player.CharacterItem.SetInt(recordKey, 1);
                
                string msg = LocalizationManager.GetText("Item_GiftBox_Received_Msg");
                player.PopText(msg);
                CMDebug.LogInfo($"{year}年圣诞礼物已发放。");
            }
            else
            {
                CMDebug.LogWarning($"礼盒 ID {GIFT_BOX_ID} 实例化失败。");
            }
        }
    }
}