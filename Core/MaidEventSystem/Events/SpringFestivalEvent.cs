using System;
using System.Globalization;
using CombatMaid.Core.WineFox;
using ItemStatsSystem;
using Cysharp.Threading.Tasks;
using CombatMaid.Localization;

namespace CombatMaid.Core.MaidEventSystem.Events
{
    public class SpringFestivalEvent : IMaidEvent
    {
        public string EventName => $"春节红包事件 ({DateTime.Now.Year})";
        
        private const int RED_ENVELOPE_ID = 444; // 红包物品ID
        private const int REWARD_COUNT = 10;    // 发放数量
        private static readonly ChineseLunisolarCalendar LunarCalendar = new ChineseLunisolarCalendar();

        public bool IsDateActive()
        {
            DateTime now = DateTime.Now;
            try
            {
                // 获取当前公历年份对应的农历正月初一
                DateTime cnyDate = LunarCalendar.ToDateTime(now.Year, 1, 1, 0, 0, 0, 0);
                
                // 活动区间：除夕（初一前1天）至 正月十五（初一后14天）
                DateTime start = cnyDate.AddDays(-1);
                DateTime end = cnyDate.AddDays(14);
                return now >= start && now <= end;
            }
            catch (Exception e)
            {
                CMDebug.LogWarning($"[SpringFestival] 历法计算异常: {e.Message}");
                return false;
            }
        }

        public void OnMaidRegistered(MaidController maid, CharacterMainControl player)
        {
            if (maid.GetComponent<WineFoxDataSync>() == null) return;
            
            string recordKey = $"MaidEvent_SpringFestival_{DateTime.Now.Year}";
            
            // 检查存档标记，确保每年只领取一次
            if (player.CharacterItem != null && player.CharacterItem.GetInt(recordKey, 0) == 0)
            {
                ExecuteDelayedGift(player, recordKey).Forget();
            }
        }

        private async UniTaskVoid ExecuteDelayedGift(CharacterMainControl player, string recordKey)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(3f));
            if (player?.CharacterItem == null) return;

            int successCount = 0;
            for (int i = 0; i < REWARD_COUNT; i++)
            {
                Item gift = ItemAssetsCollection.InstantiateSync(RED_ENVELOPE_ID);
                if (gift != null)
                {
                    ItemUtilities.SendToPlayer(gift);
                    successCount++;
                }
            }

            if (successCount > 0)
            {
                player.CharacterItem.SetInt(recordKey, 1);
                player.PopText(LocalizationManager.GetText("Event_SpringFestival_Received_Msg"));
                CMDebug.Log($"[SpringFestival] 已成功为玩家发放 {successCount} 个春节红包奖励。");
            }
            else
            {
                CMDebug.LogError($"[SpringFestival] 奖励发放失败：无法实例化物品 ID {RED_ENVELOPE_ID}");
            }
        }
    }
}