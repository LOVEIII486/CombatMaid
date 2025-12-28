using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CombatMaid.Localization;
using CombatMaid.Core.WineFox;
using ItemStatsSystem;

namespace CombatMaid.Core.Items.Components
{
    public class Component_MaidRecycleBottle : UsageBehavior
    {
        private readonly Lazy<string> _txtNoTarget = new Lazy<string>(() => 
            LocalizationManager.GetText("Item_MaidRecycleBottle_NoTarget"));
            
        private readonly Lazy<string> _txtSuccess = new Lazy<string>(() => 
            LocalizationManager.GetText("Item_MaidRecycleBottle_Success"));

        private static readonly Dictionary<string, int> ProfileToVialIdMap = new Dictionary<string, int>
        {
            { "VialMaid_I", 88001 },
            { "VialMaid_II", 88002 },
            { "VialMaid_III", 88003 }
        };

        public override bool CanBeUsed(Item item, object user)
        {
            if (!(user is CharacterMainControl player) || player.Health.IsDead) return false;
            return GetRecyclableMaids().Any();
        }

        protected override void OnUse(Item item, object user)
        {
            var player = user as CharacterMainControl;
            if (player == null) return;

            var targets = GetRecyclableMaids();
            if (targets.Count == 0)
            {
                player.PopText(_txtNoTarget.Value);
                return;
            }

            foreach (var maid in targets)
            {
                RecycleMaidToItem(maid);
            }

            player.PopText(_txtSuccess.Value);
            CMDebug.Log($"玩家使用了回收瓶，尝试封印 {targets.Count} 名女仆");
        }

        private List<MaidController> GetRecyclableMaids()
        {
            if (MaidManager.Instance == null) return new List<MaidController>();

            return MaidManager.Instance.GetAllActiveMaids()
                .Where(m => m != null && m.MaidCharacter != null)
                .Where(m => m.GetComponent<WineFoxDataSync>() == null) 
                .ToList();
        }

        private void RecycleMaidToItem(MaidController maid)
        {
            if (maid == null || maid.MaidCharacter == null) return;

            string profileId = maid.ProfileID; 

            if (ProfileToVialIdMap.TryGetValue(profileId, out int itemId))
            {
                Item vial = ItemAssetsCollection.InstantiateSync(itemId);
                if (vial != null)
                {
                    CMDebug.Log($"正在为 {profileId} 生成回收瓶 (ID: {itemId})");
                    vial.Drop(maid.transform.position, true, Vector3.up * 0.1f, 0f);
                }
                else
                {
                    CMDebug.LogError($"物品实例化失败: ID {itemId}");
                }
            }
            else
            {
                CMDebug.LogWarning($"无法识别的女仆配置: {profileId}，请检查映射表。");
            }
            Destroy(maid.MaidCharacter.gameObject);
        }
    }
}