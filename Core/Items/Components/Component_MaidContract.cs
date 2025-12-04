using UnityEngine;
using ItemStatsSystem;
using CombatMaid.Core; 

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
            var player = user as CharacterMainControl;
            if (player == null) return;

            // 1. 在玩家前方生成
            Vector3 spawnPos = player.transform.position + player.transform.forward * 1.5f;
            MaidManager.Instance.SpawnMaidAt(spawnPos);

            // 2. 播放提示并销毁物品
            player.PopText("契约成立！");
            
            // 因为在 ItemData 里配置了 behaviors，底层可能会尝试消耗耐久
            // 但为了保险，我们直接销毁整个物品对象（因为是不可堆叠的一次性道具）
            item.DestroyTree();
        }

        private void OnDestroy()
        {
            if (_item != null) _item.onUse -= OnUseItem;
        }
    }
}