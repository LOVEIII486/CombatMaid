using UnityEngine;
using ItemStatsSystem; // 引用物品系统
using Duckov;          // 引用游戏核心 (LevelManager, CharacterMainControl)
using CombatMaid.Core.Items.Data; // 引用你的数据定义

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
            CMDebug.Log("[Debug] 按下 F4，开始生成测试物品...");

            // 1. 获取本地玩家控制器 (基于源码 CharacterMainControl.Main)
            var player = CharacterMainControl.Main;
            if (player == null)
            {
                CMDebug.LogError("[Debug] 未找到 CharacterMainControl.Main，无法添加物品。");
                return;
            }

            // 2. 尝试添加物品
            // 添加 1 个契约
            AddItemToPlayer(player, MaidItemDefs.ID_MAID_CONTRACT, 1);
            
            // 添加 2 个红茶
            AddItemToPlayer(player, MaidItemDefs.ID_MAID_TEA, 2);
        }

        /// <summary>
        /// 基于源码逻辑的正确添加物品方法
        /// </summary>
        private void AddItemToPlayer(CharacterMainControl player, int itemId, int count)
        {
            // 检查玩家背包组件是否存在
            // 源码逻辑: player.CharacterItem.Inventory
            if (player.CharacterItem == null || player.CharacterItem.Inventory == null)
            {
                CMDebug.LogError("[Debug] 玩家背包未初始化。");
                return;
            }

            var inventory = player.CharacterItem.Inventory;

            // 循环添加指定数量 (虽然有 StackCount，但为了安全通常先创建再堆叠)
            // 如果物品是可堆叠的，AddAndMerge 会自动处理
            for (int i = 0; i < count; i++)
            {
                // A. 获取物品预制体
                var prefab = ItemAssetsCollection.GetPrefab(itemId);
                if (prefab == null)
                {
                    CMDebug.LogError($"[Debug] 找不到物品 ID: {itemId} 的预制体。");
                    break;
                }

                // B. 实例化物品 (使用源码中的 CreateInstance 方法，它会自动调用 Initialize)
                // 源码路径: ItemStatsSystem.Item.CreateInstance()
                Item newItem = prefab.CreateInstance();
                
                // C. 添加到背包
                // 源码路径: CharacterMainControl.cs 中使用了 inventory.AddAndMerge(obj)
                bool success = inventory.AddAndMerge(newItem);

                if (success)
                {
                    CMDebug.Log($"[Debug] 成功添加物品: {newItem.DisplayName} (ID: {itemId})");
                }
                else
                {
                    CMDebug.LogWarning($"[Debug] 背包已满，添加失败: {itemId}");
                    // 如果添加失败，销毁生成的浮空物体，防止内存泄漏或掉落在场景原点
                    Destroy(newItem.gameObject);
                    break; 
                }
            }
        }
    }
}