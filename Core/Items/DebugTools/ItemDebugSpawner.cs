using UnityEngine;
using ItemStatsSystem;

namespace CombatMaid.Core.Items.DebugTools
{
    public class ItemDebugSpawner : MonoBehaviour
    {
        private void Update()
        {
            // 按下 F4 生成测试物品
            if (Input.GetKeyDown(KeyCode.F4))
            {
                SpawnTestItems();
            }
        }

        private void SpawnTestItems()
        {
            CMDebug.Log("按下 F4，开始生成测试物品...");
            
            var player = CharacterMainControl.Main;
            if (player == null)
            {
                CMDebug.LogError("未找到 CharacterMainControl.Main，无法添加物品。");
                return;
            }

            AddItemToPlayer(player, 88888, 1);
        }

        /// <summary>
        /// 添加物品方法
        /// </summary>
        private void AddItemToPlayer(CharacterMainControl player, int itemId, int count)
        {
            if (player.CharacterItem == null || player.CharacterItem.Inventory == null)
            {
                CMDebug.LogError("玩家背包未初始化。");
                return;
            }

            var inventory = player.CharacterItem.Inventory;
            
            for (int i = 0; i < count; i++)
            {
                var prefab = ItemAssetsCollection.GetPrefab(itemId);
                if (prefab == null)
                {
                    CMDebug.LogError($"找不到物品 ID: {itemId} 的预制体。");
                    break;
                }
                
                Item newItem = prefab.CreateInstance();
                bool success = inventory.AddAndMerge(newItem);

                if (success)
                {
                    CMDebug.Log($"成功添加物品: {newItem.DisplayName} (ID: {itemId})");
                }
                else
                {
                    CMDebug.LogWarning($"背包已满，添加失败: {itemId}");
                    Destroy(newItem.gameObject);
                    break; 
                }
            }
        }
    }
}