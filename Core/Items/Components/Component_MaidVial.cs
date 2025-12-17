using System;
using UnityEngine;
using ItemStatsSystem;
using System.Linq;
using CombatMaid.Localization;

namespace CombatMaid.Core.Items.Components
{
    /// <summary>
    /// 瓶中女仆：使用后生成指定配置的女仆
    /// </summary>
    public class Component_MaidVial : UsageBehavior
    {
        private const string DefaultProfile = "VialMaid_I";

        private readonly Lazy<string> _txtOnUse =
            new Lazy<string>(() => LocalizationManager.GetText("Item_MaidVial_OnUse"));

        public override bool CanBeUsed(Item item, object user)
        {
            if (!(user is CharacterMainControl)) return false;
            
            if (MaidManager.Instance == null) return false;

            // if (MaidManager.Instance.IsSquadFull()) return false;

            return true;
        }

        protected override void OnUse(Item item, object user)
        {
            var player = user as CharacterMainControl;
            if (player == null) return;

            // 读取配置 (I型/II型/III型)
            string targetProfile = GetProfileFromItem(item);

            Vector3 spawnPos = player.transform.position + player.transform.forward * 1.5f;
            Core.MaidSpawner.Instance.SpawnMaidByProfile(targetProfile, spawnPos);

            player.PopText(string.Format(_txtOnUse.Value, targetProfile));
        }

        /// <summary>
        /// 从物品常量中读取对应的女仆ID
        /// </summary>
        private string GetProfileFromItem(Item item)
        {
            if (item.Constants != null)
            {
                var data = item.Constants.FirstOrDefault(x => x.Key == "MaidProfileID");
                if (data != null && !string.IsNullOrEmpty(data.GetString()))
                {
                    return data.GetString();
                }
            }
            
            CMDebug.LogWarning($"[{item.DisplayName}] 物品数据异常，未找到 MaidProfileID，使用默认值。");
            return DefaultProfile;
        }
    }
}