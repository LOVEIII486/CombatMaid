using UnityEngine;
using Duckov.Modding;
using Duckov.ItemUsage; // 引用游戏原生道具命名空间
using System.Collections.Generic;

namespace CombatMaid.Core.MaidBehaviors
{
    /// <summary>
    /// 自动补血行为模块 (支持 H 键手动触发)
    /// </summary>
    public class MaidHeal : MonoBehaviour
    {
        private AICharacterController _ai;
        
        // 配置：检测频率与冷却
        private const float CheckInterval = 1.0f; 
        private const float HealCooldown = 5.0f;  
        private const float HealthThreshold = 0.8f; // 血量低于80%触发

        // 运行时计时器
        private float _nextCheckTime = 0f;
        private float _actionUnlockTime = 0f;

        // 备用医疗物品 TypeID
        private readonly HashSet<int> _medIds = new HashSet<int> { 10, 20, 17, 3, 15, 16 };

        /// <summary>
        /// 判断当前AI是否跟随玩家
        /// </summary>
        private bool IsPlayerMaid
        {
            get 
            {
                // 确保 AI 有领队，且领队是玩家主角 (CharacterMainControl.Main)
                return _ai != null && 
                       _ai.leader != null && 
                       CharacterMainControl.Main != null && 
                       _ai.leader == CharacterMainControl.Main;
            }
        }

        public void Initialize(AICharacterController ai)
        {
            _ai = ai;
        }

        public void OnUpdate()
        {
            // 基础检查：AI存活且有效
            if (_ai == null || _ai.CharacterMainControl == null || _ai.CharacterMainControl.Health.IsDead) return;

            // --- 新增逻辑：手动触发检查 ---
            // 只有跟随玩家的女仆才响应按键
            if (IsPlayerMaid && Input.GetKeyDown(KeyCode.H))
            {
                HandleManualHeal();
                return; // 如果触发了手动逻辑，跳过当帧的自动检测
            }

            // 冷却检查 (手动和自动共享同一个动作冷却)
            if (Time.time < _actionUnlockTime) return;

            // --- 原有逻辑：自动周期性检测 ---
            if (Time.time >= _nextCheckTime)
            {
                _nextCheckTime = Time.time + CheckInterval;
                CheckAndHeal();
            }
        }

        /// <summary>
        /// 处理手动按键治疗逻辑
        /// </summary>
        private void HandleManualHeal()
        {
            // 如果还在冷却中，提示玩家
            if (Time.time < _actionUnlockTime)
            {
                _ai.CharacterMainControl.PopText("动作冷却中...");
                return;
            }

            // 尝试强行治疗（忽略血量阈值）
            bool success = TryUseMed(force: true);
            
            if (!success)
            {
                _ai.CharacterMainControl.PopText("背包里没有药品!");
                _ai.CharacterMainControl.DropAllItems();
            }
        }

        private void CheckAndHeal()
        {
            var health = _ai.CharacterMainControl.Health;
            if (health.MaxHealth <= 0) return;

            float ratio = health.CurrentHealth / health.MaxHealth;
            
            // 自动模式下，严格遵守血量阈值
            if (ratio < HealthThreshold)
            {
                TryUseMed(force: false);
            }
        }

        /// <summary>
        /// 尝试使用药品
        /// </summary>
        /// <param name="force">是否为强制模式（主要用于区分日志或提示）</param>
        /// <returns>是否成功使用了物品</returns>
        private bool TryUseMed(bool force)
        {
            var inventory = _ai.CharacterMainControl.CharacterItem.Inventory;
            if (inventory == null) return false;

            foreach (var item in inventory)
            {
                if (item == null || item.StackCount <= 0) continue;

                // 优先检测 Drug 组件，兼容性更好
                bool isDrug = item.GetComponent<Drug>() != null || _medIds.Contains(item.TypeID);

                if (isDrug)
                {
                    // 使用物品
                    _ai.CharacterMainControl.UseItem(item);
                    
                    // 根据触发方式显示不同文本
                    string msg = force ? "收到指令: 使用药品" : "自动治疗...";
                    _ai.CharacterMainControl.PopText(msg);

                    // 重置冷却时间
                    _actionUnlockTime = Time.time + HealCooldown;
                    
                    Debug.Log($"[战斗女仆] {(force ? "强制" : "自动")}使用药品: {item.DisplayName}");
                    return true;
                }
            }

            return false;
        }
    }
}