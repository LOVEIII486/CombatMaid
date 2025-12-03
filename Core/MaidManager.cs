using System.Collections.Generic;
using UnityEngine;
using Duckov.Modding;
using CombatMaid.Core.MaidConfigs; // 引用配置命名空间

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
                // 254 格力克，40 行军背包max，15 大医疗箱，594 S-生锈弹, 442 弹挂
                CustomItemIDs = new List<int> { 254, 40, 15, 594, 442 },
                CustomModelID = "10004", 
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

            // G: 战术移动指令
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

        // [新增] 获取鼠标射线点击位置
        private Vector3 GetMousePosition()
        {
            if (Camera.main == null) return Vector3.zero;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            // 射线检测层级：Default(0), Ground(可能会有), Terrain
            // 确保你的层级名称是正确的，Duckov 通常用地形层或默认层
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, LayerMask.GetMask("Default", "Ground", "Terrain")))
            {
                return hit.point;
            }
            return Vector3.zero;
        }

        private void CommandMoveTeamToMouse()
        {
            // [修改] 直接调用本地方法，不再依赖 MaidSpawner
            Vector3 targetPos = GetMousePosition();

            if (targetPos == Vector3.zero)
            {
                // Debug.LogWarning($"{LogTag} 指令无效：请指向地面。");
                return;
            }

            if (_activeMaids.Count == 0) return;

            Debug.Log($"{LogTag} 全队移动指令(G) -> {targetPos}");

            for (int i = _activeMaids.Count - 1; i >= 0; i--)
            {
                var maid = _activeMaids[i];
                if (maid != null)
                {
                    // 给每个女仆一点随机偏移，避免叠在一起
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

            // [修改] 直接调用本地方法
            Vector3 mousePos = GetMousePosition();
            if (mousePos == Vector3.zero) return;

            var spawnConfig = new MaidConfig()
            {
                CustomName = _defaultProfile.Config.CustomName,
                Health = _defaultProfile.Config.Health,
                IsBossIcon = _defaultProfile.Config.IsBossIcon,
                CustomItemIDs = new List<int>(_defaultProfile.Config.CustomItemIDs),
                CustomModelID = _defaultProfile.Config.CustomModelID,
            };
            
            MaidSpawner.Instance.SpawnMaid(targetKey, mousePos, LevelManager.Instance.MainCharacter, spawnConfig, (ai) => 
            {
                var controller = ai.gameObject.AddComponent<MaidController>();
                var profile = new MaidProfile { Name = "Elite", Config = spawnConfig };
                
                controller.Initialize(profile, LevelManager.Instance.MainCharacter);
                
                // 应用自定义模型
                if (!string.IsNullOrEmpty(spawnConfig.CustomModelID))
                {
                    this.StartCoroutine(
                        CombatMaid.Core.CustomModel.CustomModelBridge.ApplyModelByIDAsync(
                            ai.CharacterMainControl, 
                            spawnConfig.CustomModelID
                        )
                    );
                }
                
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
                    if (maid.MaidCharacter != null)
                    {
                        // Debug.Log($"{LogTag} 销毁角色: {maid.MaidCharacter.name}");
                        Destroy(maid.MaidCharacter.gameObject);
                    }
                    else if (maid.gameObject != null)
                    {
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