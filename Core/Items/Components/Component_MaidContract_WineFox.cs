using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using ItemStatsSystem;
using CombatMaid.Localization;

namespace CombatMaid.Core.Items.Components
{
    public class Component_MaidContract_WineFox : UsageBehavior
    {
        private static float _nextSummonTime = 0f;
        private static int _lastSceneIndex = -1;
        
        private const float GlobalCooldown = 120f;

        private readonly Lazy<string> _txtCooldown = new Lazy<string>(() => 
            LocalizationManager.GetText("Item_MaidContractWineFox_Cooldown"));
        private readonly Lazy<string> _txtRebuild = new Lazy<string>(() => 
            LocalizationManager.GetText("Item_MaidContractWineFox_Rebuild"));
        private readonly Lazy<string> _txtRespond = new Lazy<string>(() => 
            LocalizationManager.GetText("Item_MaidContractWineFox_Respond"));
        
        public override bool CanBeUsed(Item item, object user)
        {
            if (!(user is CharacterMainControl)) return false;
            if (MaidManager.Instance == null) return false;

            // 维护冷却状态
            CheckSceneAndResetCD();

            // 即使在冷却中，也返回 true。
            return true;
        }
        
        
        protected override void OnUse(Item item, object user)
        {
            var player = user as CharacterMainControl;
            if (player == null) return;
            
            if (Time.time < _nextSummonTime)
            {
                float remaining = _nextSummonTime - Time.time;
                // 显示CD
                player.PopText(string.Format(_txtCooldown.Value, remaining.ToString("F0")));
                return; 
            }
            HandleSummon(player);
        }

        private void HandleSummon(CharacterMainControl player)
        {
            MaidController existingFox = MaidManager.Instance.GetActiveWineFox();

            if (existingFox != null)
            {
                player.PopText(_txtRebuild.Value);
                Destroy(existingFox.gameObject);
            }
            else
            {
                player.PopText(_txtRespond.Value);
            }
            
            Vector3 spawnPos = player.transform.position + player.transform.forward * 1.5f;
            MaidSpawner.Instance.SpawnWineFox(spawnPos);

            _nextSummonTime = Time.time + GlobalCooldown;
        }

        private void CheckSceneAndResetCD()
        {
            int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
            if (currentSceneIndex != _lastSceneIndex)
            {
                _lastSceneIndex = currentSceneIndex;
                if (Time.time < _nextSummonTime)
                {
                    _nextSummonTime = 0f;
                    // CMDebug.Log("[WineFox] 场景切换，冷却已重置");
                }
            }
        }
    }
}