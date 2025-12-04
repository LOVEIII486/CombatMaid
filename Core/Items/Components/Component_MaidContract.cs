using UnityEngine;
using ItemStatsSystem;
using CombatMaid.Core; // 引用 MaidManager

namespace CombatMaid.Core.Items.Components
{
    public class Component_MaidContract : MonoBehaviour
    {
        private Item _item;

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
            // 1. 确定生成位置 (玩家前方 1.5 米)
            var player = user as CharacterMainControl;
            if (player == null) return;

            Vector3 spawnPos = player.transform.position + player.transform.forward * 1.5f;

            // 2. 调用现有的 API 生成女仆
            // 使用 SpawnMaidAt 会自动处理控制器挂载和模型加载
            MaidManager.Instance.SpawnMaidAt(spawnPos);

            // 3. 销毁物品 (消耗逻辑)
            // 因为不可堆叠，直接销毁整棵物品树
            item.DestroyTree();
            
            CMDebug.Log("契约已使用，女仆召唤中...");
        }

        private void OnDestroy()
        {
            if (_item != null)
            {
                _item.onUse -= OnUseItem;
            }
        }
    }
}