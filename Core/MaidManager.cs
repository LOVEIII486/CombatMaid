using System.Collections.Generic;
using UnityEngine;
using Duckov.Modding;
using CombatMaid.MaidBehaviors;

namespace CombatMaid.Core
{
    public class MaidManager : MonoBehaviour
    {
        public static MaidManager Instance { get; private set; }

        private List<MaidController> _activeMaids = new List<MaidController>();

        private MaidProfile _defaultProfile = new MaidProfile
        {
            Name = "MyMaid",
            Config = new MaidConfig()
            {
                CustomName = "皇家女仆·贝拉",
                Health = 500f,
                IsBossIcon = true,
                CustomItemIDs = new List<int> { 254, 15, 594 } // 枪, 药, 弹药
            }
        };

        private void Awake()
        {
            if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
            else { Destroy(this); }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F6))
            {
                SpawnSpecificMaid("Cname_Usec"); 
            }
    
            if (Input.GetKeyDown(KeyCode.F7)) DespawnTeam(); // 顺便改一下清除键
        }

        // ==========================================
        //  接口区域：供 ModBehaviour 调用，不再自己监听
        // ==========================================

        /// <summary>
        /// 场景加载完毕时调用
        /// </summary>
        public void OnLevelStart(string sceneName)
        {
            Debug.Log($"[MaidManager] 就绪。当前场景: {sceneName}。");
        }

        /// <summary>
        /// 场景卸载时调用
        /// </summary>
        public void OnLevelEnd()
        {
            DespawnTeam(); // 确保切换场景时清理干净
        }

        // ==========================================
        //  逻辑区域
        // ==========================================
        private void SpawnSpecificMaid(string targetKey)
        {
            if (LevelManager.Instance?.MainCharacter == null) return;
            Vector3 mousePos = MaidSpawner.Instance.GetMousePosition();
            if (mousePos == Vector3.zero) return;

            // 配置：给她一个特殊的名字和装备
            var eliteConfig = _defaultProfile.Config;

            // 调用新方法
            var ai = MaidSpawner.Instance.SpawnMaid(targetKey, mousePos, LevelManager.Instance.MainCharacter, eliteConfig);
    
            if (ai != null)
            {
                var controller = ai.gameObject.AddComponent<MaidController>();
                // 注意：这里需要构造一个临时的 profile 传进去
                var profile = new MaidProfile { Name = "Elite", Config = eliteConfig };
                controller.Initialize(profile, LevelManager.Instance.MainCharacter);
                _activeMaids.Add(controller);
        
                ai.CharacterMainControl.PopText("指定召唤成功！", 2f);
            }
        }

        public void DespawnTeam()
        {
            foreach (var maid in _activeMaids)
            {
                if (maid != null && maid.gameObject != null) Destroy(maid.gameObject);
            }
            _activeMaids.Clear();
            Debug.Log("[MaidManager] 队伍已清理");
        }
    }
    
    [System.Serializable]
    public class MaidProfile 
    { 
        public string Name; 
        public MaidConfig Config; 
    }
}