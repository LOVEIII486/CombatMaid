using UnityEngine;
using ItemStatsSystem;
using System.Linq;
using CombatMaid.Core;

namespace CombatMaid.Core.Items.Components
{
    public class Component_MaidVial : MonoBehaviour
    {
        private Item _item;
        
        private const string DefaultProfile = "VialMaid_I"; 

        private void Awake()
        {
            _item = GetComponent<Item>();
            if (_item != null) _item.onUse += OnUseVial;
        }

        private void OnUseVial(Item item, object user)
        {
            var player = user as CharacterMainControl;
            if (player == null) return;
            
            string targetProfile = GetProfileFromItem(item);
            
            Vector3 spawnPos = player.transform.position + player.transform.forward * 2.0f + Vector3.up * 0.5f;
            
            if (MaidManager.Instance != null)
            {
                Core.MaidSpawner.Instance.SpawnMaidByProfile(targetProfile, spawnPos);
                player.PopText($"瓶中女仆 [{targetProfile}] 已就绪！");
            }
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
            return DefaultProfile;
        }

        private void OnDestroy()
        {
            if (_item != null) _item.onUse -= OnUseVial;
        }
    }
}