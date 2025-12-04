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

            // 在玩家前方生成
            Vector3 spawnPos = player.transform.position + player.transform.forward * 1.5f;
            MaidManager.Instance.SpawnMaidAt("RoyalMaid_Bella", spawnPos);

            player.PopText("契约成立！");

            //似乎不需要
            //item.DestroyTree();
        }

        private void OnDestroy()
        {
            if (_item != null) _item.onUse -= OnUseItem;
        }
    }
}