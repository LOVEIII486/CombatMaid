using UnityEngine;
using ItemStatsSystem;
using CombatMaid.Core;
using CombatMaid.Core.WineFox;

namespace CombatMaid.Core.Items.Components
{
    public class Component_MaidContract_WineFox : MonoBehaviour
    {
        private Item _item;

        // 全局冷却时间戳
        private static float _nextSummonTime = 0f;
        private const float GlobalCooldown = 5f;

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
            
            if (MaidManager.Instance == null)
            {
                CMDebug.LogError("MaidManager 未初始化");
                return;
            }

            // 1. CD 检查
            if (Time.time < _nextSummonTime)
            {
                float remaining = _nextSummonTime - Time.time;
                player.PopText($"契约冷却中... {remaining:F0}秒");
                return; 
            }

            // 2. 首次召唤，如果存在则重新召唤
            MaidController existingFox = MaidManager.Instance.GetActiveWineFox();

            if (existingFox != null)
            {
                CMDebug.Log("[WineFoxContract] 重新构筑酒狐实体...");
                player.PopText("正在重铸酒狐躯体...");
                Destroy(existingFox.gameObject);
            }
            else
            {
                player.PopText("酒狐契约响应中...");
            }
            
            // 3. 召唤
            Vector3 spawnPos = player.transform.position + player.transform.forward * 1.5f;
            Core.MaidSpawner.Instance.SpawnWineFox(spawnPos);
            
            // 4. 重置冷却
            _nextSummonTime = Time.time + GlobalCooldown;
        }

        private void OnDestroy()
        {
            if (_item != null) _item.onUse -= OnUseItem;
        }
    }
}