using UnityEngine;
using UnityEngine.SceneManagement;
using ItemStatsSystem;
using CombatMaid.Core;
using CombatMaid.Core.WineFox;

namespace CombatMaid.Core.Items.Components
{
    public class Component_MaidContract_WineFox : MonoBehaviour
    {
        private Item _item;

        // 全局冷却时间戳
        private static float _nextSummonTime = 0f;
        
        // [新增] 记录上一次所在的场景索引，初始化为 -1 确保第一次进入游戏必定重置
        private static int _lastSceneIndex = -1;
        
        private const float GlobalCooldown = 120f;

        private void Awake()
        {
            _item = GetComponent<Item>();
            if (_item != null)
            {
                _item.onUse += OnUseItem;
            }

            // [新增] 场景切换检测逻辑
            CheckSceneChangeAndResetCD();
        }

        /// <summary>
        /// 检测场景是否发生变化，如果是则重置冷却。
        /// 这种方式安全，因为它能区分“切换地图”和“原地扔装备”。
        /// </summary>
        private void CheckSceneChangeAndResetCD()
        {
            int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;

            // 如果当前的场景索引与上次记录的不同（说明发生了场景跳转）
            if (currentSceneIndex != _lastSceneIndex)
            {
                _lastSceneIndex = currentSceneIndex;

                // 只有当前处于冷却中才执行重置，避免不必要的日志
                if (Time.time < _nextSummonTime)
                {
                    _nextSummonTime = 0f;
                    CMDebug.Log("[WineFoxContract] 检测到场景切换(Base<->Raid)，契约冷却已自动重置。");
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

            // 1. CD 检查
            if (Time.time < _nextSummonTime)
            {
                float remaining = _nextSummonTime - Time.time;
                player.PopText($"契约冷却中... {remaining:F0}秒");
                return; 
            }

            // 2. 首次召唤，如果存在则重新召唤
            MaidController existingFox = MaidManager.Instance.GetActiveWineFox();

            if (existingFox != null)
            {
                CMDebug.Log("[WineFoxContract] 重新构筑酒狐实体...");
                player.PopText("正在重铸酒狐躯体...");
                Destroy(existingFox.gameObject);
            }
            else
            {
                player.PopText("酒狐契约响应中...");
            }
            
            // 3. 召唤
            Vector3 spawnPos = player.transform.position + player.transform.forward * 1.5f;
            Core.MaidSpawner.Instance.SpawnWineFox(spawnPos);
            
            // 4. 重置冷却
            _nextSummonTime = Time.time + GlobalCooldown;
        }

        private void OnDestroy()
        {
            if (_item != null) _item.onUse -= OnUseItem;
        }
    }
}