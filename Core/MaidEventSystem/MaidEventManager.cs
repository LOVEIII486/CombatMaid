using System.Collections.Generic;
using CombatMaid.Core.MaidEventSystem.Events;

namespace CombatMaid.Core.MaidEventSystem
{
    public static class MaidEventManager
    {
        private static readonly List<IMaidEvent> AllEvents = new List<IMaidEvent>
        {
            new ChristmasGiftEvent()
        };

        // 当前日期生效的事件列表
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
            // 仅遍历当前日期有效的事件
            foreach (var evt in _activeEvents)
            {
                evt.OnMaidRegistered(maid, player);
            }
        }
    }
}