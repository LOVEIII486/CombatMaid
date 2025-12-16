using System;
using UnityEngine;
using ItemStatsSystem;
using System.Linq;
using CombatMaid.Localization;

namespace CombatMaid.Core.Items.Components
{
    public class Component_MaidVial : MonoBehaviour
    {
        private Item _item;

        private const string DefaultProfile = "VialMaid_I";

        private readonly Lazy<string> _txtOnUse =
            new Lazy<string>(() => LocalizationManager.GetText("Item_MaidVial_OnUse"));

        private void Awake()
        {
            _item = GetComponent<Item>();
            if (_item != null) _item.onUse += OnUseVial;
        }

        private void OnUseVial(Item item, object user)
        {
            var player = user as CharacterMainControl;
            if (player == null) return;

            if (MaidManager.Instance == null)
            {
                CMDebug.LogError("MaidManager 未初始化");
                return;
            }

            string targetProfile = GetProfileFromItem(item);

            Vector3 spawnPos = player.transform.position + player.transform.forward * 1.5f;
            Core.MaidSpawner.Instance.SpawnMaidByProfile(targetProfile, spawnPos);
            
            player.PopText(string.Format(_txtOnUse.Value, targetProfile));
        }

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
            CMDebug.LogWarning("物品缺少对应的女仆预设名称！");
            return DefaultProfile;
        }

        private void OnDestroy()
        {
            if (_item != null) _item.onUse -= OnUseVial;
        }
    }
}