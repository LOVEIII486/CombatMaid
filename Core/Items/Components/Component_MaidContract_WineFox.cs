using UnityEngine;
using ItemStatsSystem;
using CombatMaid.Core;
using CombatMaid.Core.WineFox; // [关键] 引用酒狐组件命名空间以进行唯一性检查

namespace CombatMaid.Core.Items.Components
{
    public class Component_MaidContract_WineFox : MonoBehaviour
    {
        private Item _item;

        // [全局CD] 静态变量，所有酒狐契约物品共享此计时器
        private static float _nextSummonTime = 0f;
        private const float GlobalCooldown = 10f; // 5分钟冷却

        private void Awake()
        {
            _item = GetComponent<Item>();
            if (_item != null)
            {
                _item.onUse += OnUseItem;
            }
        }

        private void OnUseItem(Item item, object user)
        {
            var player = user as CharacterMainControl;
            if (player == null) return;

            // 1. [检查] 全局冷却
            if (Time.time < _nextSummonTime)
            {
                float remaining = _nextSummonTime - Time.time;
                player.PopText($"契约冷却中... {remaining:F0}秒");
                return; // 直接返回，不执行召唤
            }

            // 2. [检查] 唯一性 (检查场上是否有挂载了同步组件的酒狐)
            if (FindObjectOfType<WineFoxDataSync>() != null)
            {
                player.PopText("酒狐已在场，无法重复召唤！");
                return; // 直接返回
            }
            
            // 3. [执行] 召唤逻辑
            Vector3 spawnPos = player.transform.position + player.transform.forward * 1.5f;
            
            if (MaidManager.Instance != null)
            {
                MaidManager.Instance.SpawnWineFox(spawnPos);
                
                // 设置 CD
                _nextSummonTime = Time.time + GlobalCooldown;
                
                player.PopText("酒狐契约响应中...");
            }
        }

        private void OnDestroy()
        {
            if (_item != null) _item.onUse -= OnUseItem;
        }
    }
}