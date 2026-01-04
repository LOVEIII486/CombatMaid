using System.Collections.Generic;
using CombatMaid.Core.MaidEventSystem.Events;

namespace CombatMaid.Core.MaidEventSystem
{
    public static class MaidEventManager
    {
        public static readonly List<IMaidEvent> AllEvents = new List<IMaidEvent>
        {
            new ChristmasGiftEvent(),
            new SpringFestivalEvent()
        };

        private static List<IMaidEvent> _activeEvents = new List<IMaidEvent>();

        static MaidEventManager()
        {
            RefreshActiveEvents();
        }
        
        public static void RefreshActiveEvents()
        {
            _activeEvents.Clear();
            foreach (var evt in AllEvents)
            {
                if (evt.IsDateActive())
                {
                    _activeEvents.Add(evt);
                }
            }
            CMDebug.Log($"活跃事件已刷新，当前有效事件数: {_activeEvents.Count}");
        }

        public static void OnMaidJoinTeam(MaidController maid)
        {
            if (_activeEvents.Count == 0) return;

            var player = CharacterMainControl.Main;
            if (maid == null || player == null) return;
            foreach (var evt in _activeEvents)
            {
                evt.OnMaidRegistered(maid, player);
            }
        }
        
        public static void Debug_ForceTriggerAll(MaidController maid)
        {
            var player = CharacterMainControl.Main;
            if (maid == null || player == null) return;

            CMDebug.Log("[Debug] 正在强制触发所有已注册事件（忽略日期判断）...");
            foreach (var evt in AllEvents)
            {
                evt.OnMaidRegistered(maid, player);
            }
        }
    }
}