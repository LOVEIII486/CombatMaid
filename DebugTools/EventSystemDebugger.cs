using System;
using CombatMaid.Core;
using UnityEngine;
using CombatMaid.Core.MaidEventSystem;
using CombatMaid.Core.MaidEventSystem.Events;

namespace CombatMaid.DebugTools
{
    public class EventSystemDebugger : MonoBehaviour
    {
        private bool _showGui;
        private Rect _windowRect = new Rect(100, 100, 400, 500); // 窗口初始位置和大小
        private int _windowId = 8848; // 确保 ID 唯一
        private string _statusInfo = "等待操作...";
        private Vector2 _scrollPos;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F10)) _showGui = !_showGui;
        }

        private void OnGUI()
        {
            if (!_showGui) return;

            // 使用 GUI.Window 替换 GUI.Box，使其具备窗口属性
            _windowRect = GUI.Window(_windowId, _windowRect, DrawWindowContents, "战斗女仆事件调试工具 (F10)");
        }

        // 窗口绘制函数
        private void DrawWindowContents(int id)
        {
            // 允许拖动窗口顶部区域（标题栏高度约20像素）
            GUI.DragWindow(new Rect(0, 0, 400, 20));

            var player = CharacterMainControl.Main;
            if (player == null)
            {
                GUI.Label(new Rect(20, 40, 360, 20), "<color=red>错误: 无法获取玩家 (CharacterMainControl.Main 为空)</color>");
                return;
            }

            // 1. 全局清理区
            if (GUI.Button(new Rect(20, 40, 360, 30), "清理当前年份所有标记 (Reset Tags)"))
            {
                ClearAllEventTags(player);
            }

            // 2. 事件选择区
            GUI.Label(new Rect(20, 80, 360, 20), "<b>已注册事件 (点击执行 OnMaidRegistered):</b>");
            
            // 使用滚动视图处理多个事件
            _scrollPos = GUI.BeginScrollView(new Rect(20, 100, 360, 280), _scrollPos, 
                new Rect(0, 0, 340, MaidEventManager.AllEvents.Count * 50));

            for (int i = 0; i < MaidEventManager.AllEvents.Count; i++)
            {
                var evt = MaidEventManager.AllEvents[i];
                string key = GetKeyByEvent(evt);
                bool isClaimed = player.CharacterItem.GetInt(key, 0) == 1;
                bool isDateActive = evt.IsDateActive();

                Rect btnRect = new Rect(0, i * 50, 240, 40);
                string dateColor = isDateActive ? "green" : "gray";
                string btnLabel = $"[{i}] {evt.EventName}\n<color={dateColor}>日期有效: {isDateActive}</color>";

                if (GUI.Button(btnRect, btnLabel))
                {
                    ExecuteEventDebug(evt, player);
                }

                // 标记状态显示
                GUI.Label(new Rect(250, i * 50 + 10, 80, 20), isClaimed ? "<color=orange>已领取</color>" : "<color=cyan>待触发</color>");
            }
            GUI.EndScrollView();

            // 3. 状态反馈区
            GUI.Box(new Rect(20, 390, 360, 90), "<b>操作反馈</b>");
            GUI.Label(new Rect(30, 410, 340, 60), _statusInfo);
        }

        private void ExecuteEventDebug(IMaidEvent evt, CharacterMainControl player)
        {
            var maid = FindObjectOfType<MaidController>();
            if (maid == null)
            {
                _statusInfo = "<color=red>触发失败</color>: 场景中未找到任何女仆 (MaidController)。";
                return;
            }

            // 检查存档标记
            string key = GetKeyByEvent(evt);
            if (player.CharacterItem.GetInt(key, 0) == 1)
            {
                _statusInfo = $"<color=yellow>拦截</color>: 事件 [{evt.EventName}] 存档已标记为已领。\n请先执行清理标记。";
                return;
            }

            // 强制触发执行
            try {
                _statusInfo = $"<color=green>指令已发送</color>: [{evt.EventName}]\n正在调用接口逻辑...";
                evt.OnMaidRegistered(maid, player);
            } catch (Exception e) {
                _statusInfo = $"<color=red>运行时崩溃</color>: {e.Message}";
            }
        }

        private void ClearAllEventTags(CharacterMainControl player)
        {
            int year = DateTime.Now.Year;
            player.CharacterItem.SetInt($"MaidEvent_XmasGift_{year}", 0);
            player.CharacterItem.SetInt($"MaidEvent_SpringFestival_{year}", 0);
            _statusInfo = $"<color=white>标记已重置</color>: {year} 年度的事件标记已归零。";
        }

        private string GetKeyByEvent(IMaidEvent evt)
        {
            int year = DateTime.Now.Year;
            if (evt is ChristmasGiftEvent) return $"MaidEvent_XmasGift_{year}";
            if (evt is SpringFestivalEvent) return $"MaidEvent_SpringFestival_{year}";
            return "UnknownKey";
        }
    }
}