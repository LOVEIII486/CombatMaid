using System.Collections.Generic;
using CombatMaid.Core.MaidEventSystem.Events;

namespace CombatMaid.Core.MaidEventSystem
{
    public static class MaidEventManager
    {
        private static readonly List<IMaidEvent> RegisteredEvents = new List<IMaidEvent>
        {
            new ChristmasGiftEvent() // 注册圣诞事件
        };

        public static void OnMaidJoinTeam(MaidController maid)
        {
            var player = CharacterMainControl.Main;
            if (maid == null || player == null) return;

            foreach (var evt in RegisteredEvents)
            {
                evt.OnMaidRegistered(maid, player);
            }
        }
    }
}