using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Duckov.Modding;
using CombatMaid.MaidBehaviors; // 引用行为命名空间

namespace CombatMaid.Core
{
    /// <summary>
    /// 女仆大管家
    /// 职责：监听场景变化、管理女仆队伍数据、控制生成与销毁
    /// </summary>
    public class MaidManager : MonoBehaviour
    {
        public static MaidManager Instance { get; private set; }

        // 队伍数据档案 (用于保存状态/背包)
        private List<MaidProfile> _teamProfiles = new List<MaidProfile>();
        
        // 当前场景中活跃的女仆控制器引用
        private List<MaidController> _activeMaids = new List<MaidController>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                
                // 【测试用】初始化默认队伍 (两名女仆)
                // 实际开发中，这里应该是从存档文件读取 _teamProfiles
                _teamProfiles.Add(new MaidProfile { Name = "Alpha", Config = new MaidConfig() });
                _teamProfiles.Add(new MaidProfile { Name = "Beta", Config = new MaidConfig() });
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnEnable()
        {
            // 订阅场景加载事件
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        #region 场景生命周期

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // 过滤掉主菜单等非战斗场景
            if (IsGameplayScene(scene.name))
            {
                Debug.Log($"[MaidManager] 进入战斗场景: {scene.name}，准备生成队伍...");
                StartCoroutine(SpawnTeamRoutine());
            }
        }

        private void OnSceneUnloaded(Scene scene)
        {
            // 场景卸载时，清空活跃引用 (物体会被Unity自动销毁，但我们需要清空列表)
            _activeMaids.Clear();
        }

        private bool IsGameplayScene(string sceneName)
        {
            // 根据你的游戏实际情况调整过滤逻辑
            return !sceneName.Contains("Menu") && !sceneName.Contains("Boot") && !sceneName.Contains("Loader");
        }

        #endregion

        #region 生成逻辑

        private IEnumerator SpawnTeamRoutine()
        {
            // 等待直到玩家角色初始化完成
            yield return new WaitUntil(() => LevelManager.Instance?.MainCharacter != null);
            
            var player = LevelManager.Instance.MainCharacter;
            
            foreach (var profile in _teamProfiles)
            {
                SpawnSingleMaid(profile, player);
                // 稍微错开生成时间，防止卡顿和重叠
                yield return new WaitForSeconds(0.1f);
            }
        }

        private void SpawnSingleMaid(MaidProfile profile, CharacterMainControl player)
        {
            // 计算生成位置：玩家身后 2米 + 随机偏移
            Vector3 spawnPos = player.transform.position - player.transform.forward * 2f;
            spawnPos += new Vector3(Random.Range(-1.5f, 1.5f), 0, Random.Range(-1.5f, 1.5f));

            // 调用 Spawner 生成实体
            var aiController = MaidSpawner.Instance.SpawnMaid(spawnPos, player, profile.Config);

            if (aiController != null)
            {
                // 【关键】挂载行为控制器 (Brain)
                var controller = aiController.gameObject.AddComponent<MaidController>();
                controller.Initialize(profile, player);
                
                _activeMaids.Add(controller);
                Debug.Log($"[MaidManager] 女仆 {profile.Name} 已入队");
            }
        }

        #endregion
        
        #region 队伍控制接口

        /// <summary>
        /// 全员强制移动到指定位置
        /// </summary>
        public void CommandMoveAllTo(Vector3 targetPosition)
        {
            foreach (var maid in _activeMaids)
            {
                if (maid != null)
                {
                    // 给每个女仆一个微小的随机目标偏移，防止挤在一起
                    Vector3 offset = new Vector3(Random.Range(-2f, 2f), 0, Random.Range(-2f, 2f));
                    maid.ForceMoveTo(targetPosition + offset);
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// 女仆数据档案 (数据层)
    /// </summary>
    [System.Serializable]
    public class MaidProfile
    {
        public string Name;
        public MaidConfig Config;
        // TODO: 在这里添加背包数据 List<ItemData> Inventory;
    }
}