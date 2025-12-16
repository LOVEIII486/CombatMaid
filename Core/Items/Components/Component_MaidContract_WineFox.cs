using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using ItemStatsSystem;
using CombatMaid.Localization; 

namespace CombatMaid.Core.Items.Components
{
    public class Component_MaidContract_WineFox : MonoBehaviour
    {
        private Item _item;

        // 全局冷却时间戳
        private static float _nextSummonTime = 0f;
        
        // 记录上一次所在的场景索引，初始化为 -1
        private static int _lastSceneIndex = -1;
        
        private const float GlobalCooldown = 120f;

        private readonly Lazy<string> _txtCooldown = new Lazy<string>(() => 
            LocalizationManager.GetText("Item_MaidContractWineFox_Cooldown"));
        private readonly Lazy<string> _txtRebuild = new Lazy<string>(() => 
            LocalizationManager.GetText("Item_MaidContractWineFox_Rebuild"));
        private readonly Lazy<string> _txtRespond = new Lazy<string>(() => 
            LocalizationManager.GetText("Item_MaidContractWineFox_Respond"));
        

        private void Awake()
        {
            _item = GetComponent<Item>();
            if (_item != null)
            {
                _item.onUse += OnUseItem;
            }

            // 场景切换检测
            CheckSceneChangeAndResetCD();
        }

        private void CheckSceneChangeAndResetCD()
        {
            int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;

            if (currentSceneIndex != _lastSceneIndex)
            {
                _lastSceneIndex = currentSceneIndex;
                if (Time.time < _nextSummonTime)
                {
                    _nextSummonTime = 0f;
                    CMDebug.Log("场景切换，契约冷却重置。");
                }
            }
        }

        private void OnUseItem(Item item, object user)
        {
            var player = user as CharacterMainControl;
            if (player == null) return;
            
            if (MaidManager.Instance == null)
            {
                CMDebug.LogError("MaidManager 未初始化");
                return;
            }

            if (Time.time < _nextSummonTime)
            {
                float remaining = _nextSummonTime - Time.time;
                player.PopText(string.Format(_txtCooldown.Value, remaining.ToString("F0")));
                return; 
            }

            // 首次召唤，如果存在则重新召唤
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
            Core.MaidSpawner.Instance.SpawnWineFox(spawnPos);
            
            _nextSummonTime = Time.time + GlobalCooldown;
        }

        private void OnDestroy()
        {
            if (_item != null) _item.onUse -= OnUseItem;
        }
    }
}