using System.Collections.Generic;
using UnityEngine;
using Duckov.Modding;

namespace CombatMaid.Core
{
    public class MaidManager : MonoBehaviour
    {
        private const string LogTag = "[CombatMaid.MaidManager]";
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
                CustomItemIDs = new List<int> { 254, 15, 594 } 
            }
        };

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                Debug.Log($"{LogTag} 初始化完成。");
            }
            else { Destroy(this); }
        }

        private void Update()
        {
            // F6: 生成测试
            if (Input.GetKeyDown(KeyCode.F6)) SpawnSpecificMaid("Cname_Usec"); 
            
            // F7: 清除队伍
            if (Input.GetKeyDown(KeyCode.F7)) DespawnTeam();

            // G: 战术移动指令 (原 T 键已修改)
            if (Input.GetKeyDown(KeyCode.G))
            {
                CommandMoveTeamToMouse();
            }
        }

        public void OnLevelStart(string sceneName)
        {
            Debug.Log($"{LogTag} 场景就绪: {sceneName}");
        }

        public void OnLevelEnd()
        {
            DespawnTeam();
        }

        // ==========================================
        //  逻辑区域
        // ==========================================

        private void CommandMoveTeamToMouse()
        {
            if (MaidSpawner.Instance == null) return;
            Vector3 targetPos = MaidSpawner.Instance.GetMousePosition();

            if (targetPos == Vector3.zero)
            {
                Debug.LogWarning($"{LogTag} 指令无效：请指向地面。");
                return;
            }

            if (_activeMaids.Count == 0) return;

            Debug.Log($"{LogTag} 全队移动指令(G) -> {targetPos}");

            for (int i = _activeMaids.Count - 1; i >= 0; i--)
            {
                var maid = _activeMaids[i];
                if (maid != null)
                {
                    Vector3 offset = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f));
                    maid.ForceMoveTo(targetPos + offset);
                }
            }
        }
        
        private void SpawnSpecificMaid(string targetKey)
        {
            if (LevelManager.Instance?.MainCharacter == null || MaidSpawner.Instance == null)
            {
                Debug.LogError($"{LogTag} 核心组件缺失，无法生成。");
                return;
            }

            Vector3 mousePos = MaidSpawner.Instance.GetMousePosition();
            if (mousePos == Vector3.zero) return;

            var spawnConfig = new MaidConfig()
            {
                CustomName = _defaultProfile.Config.CustomName,
                Health = _defaultProfile.Config.Health,
                IsBossIcon = _defaultProfile.Config.IsBossIcon,
                CustomItemIDs = new List<int>(_defaultProfile.Config.CustomItemIDs)
            };

            // 使用回调添加控制器
            MaidSpawner.Instance.SpawnMaid(targetKey, mousePos, LevelManager.Instance.MainCharacter, spawnConfig, (ai) => 
            {
                var controller = ai.gameObject.AddComponent<MaidController>();
                var profile = new MaidProfile { Name = "Elite", Config = spawnConfig };
                
                controller.Initialize(profile, LevelManager.Instance.MainCharacter);
                
                if (!_activeMaids.Contains(controller))
                {
                    _activeMaids.Add(controller);
                }
                
                if (ai.CharacterMainControl != null)
                {
                    ai.CharacterMainControl.PopText("指定召唤成功！");
                }
            });
        }

        public void DespawnTeam()
        {
            for (int i = _activeMaids.Count - 1; i >= 0; i--)
            {
                var maid = _activeMaids[i];
                if (maid != null)
                {
                    // 1. 优先尝试销毁角色的“身体” (根物体)
                    if (maid.MaidCharacter != null)
                    {
                        Debug.Log($"{LogTag} 销毁角色: {maid.MaidCharacter.name}");
                        Destroy(maid.MaidCharacter.gameObject);
                    }
                    // 2. 如果找不到身体（比如初始化未完成），则保底销毁控制器自身
                    else if (maid.gameObject != null)
                    {
                        Debug.LogWarning($"{LogTag} 找不到角色根物体，仅销毁 AI 控制器: {maid.name}");
                        Destroy(maid.gameObject);
                    }
                }
            }
            _activeMaids.Clear();
            Debug.Log($"{LogTag} 队伍已清理");
        }
    }
    
    [System.Serializable]
    public class MaidProfile 
    { 
        public string Name; 
        public MaidConfig Config; 
    }
}