using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using Duckov.Modding;
using CombatMaid.Core.MaidConfigs;
using CombatMaid.Core.MaidFSM.States;
using Duckov.ItemBuilders; // 引用配置命名空间
using Newtonsoft.Json;

namespace CombatMaid.Core
{
    public class MaidManager : MonoBehaviour
    {
        public static MaidManager Instance { get; private set; }

        private List<MaidController> _activeMaids = new List<MaidController>();
        private MaidProfileData _currentProfileData;

        // 默认回退配置
        private MaidProfileData _fallbackProfile = new MaidProfileData
        {
            ProfileName = "DefaultMaid",
            PresetConfig = new MaidConfig()
            {
                CustomName = "皇家女仆·贝拉",
                Health = 500f,
                IsBossIcon = true,
                CustomItemIDs = new List<int> { 254, 40, 15, 594, 442 },
            },
            ExtraData = new MaidExtraInfo()
            {
                Description = "代码默认配置",
                BasePresetKey = "Cname_Usec",
                CustomModelID = "10004", 
                EnableAutoHeal = true,
                TacticalMode = "Assault"
            }
        };
        
        public CharacterMainControl FocusTarget { get; private set; } // 当前集火目标
        private float _focusExpireTimer = 0f; // 集火指令过期倒计时
        private const float FocusDuration = 5.0f; // 玩家停火后，集火指令维持 5 秒
        private const float RaycastDistance = 20f; // 标记距离
        private int _enemyLayerMask;
        
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                
                _enemyLayerMask = LayerMask.GetMask("Default", "Character", "Hitbox", "Enemy");
                
                // 初始化时尝试加载 JSON
                LoadDefaultPreset();
                
                CMDebug.Log($"初始化完成。");
            }
            else { Destroy(this); }
        }

        private void Update()
        {
            HandleDebugInput();
            
            UpdateFocusTarget();
        }

        public void OnLevelStart(string sceneName)
        {
            CMDebug.Log($"场景就绪: {sceneName}");
        }

        public void OnLevelEnd()
        {
            DespawnTeam();
        }
        
        private void HandleDebugInput()
        {
            if (Input.GetKeyDown(KeyCode.F5)) SpawnSpecificMaid(); 
            if (Input.GetKeyDown(KeyCode.F6)) DespawnTeam();
            if (Input.GetKeyDown(KeyCode.F8)) LoadDefaultPreset();
            if (Input.GetKeyDown(KeyCode.G)) CommandMoveTeamToMouse();
            if (Input.GetKeyDown(KeyCode.F1)) { /* 驻守逻辑 */ }
            if (Input.GetKeyDown(KeyCode.F2)) { /* 召回逻辑 */ }
        }
        
        private void UpdateFocusTarget()
        {
            // 1. 倒计时逻辑
            if (_focusExpireTimer > 0)
            {
                _focusExpireTimer -= Time.deltaTime;
                if (_focusExpireTimer <= 0)
                {
                    FocusTarget = null; // 指令过期
                    CMDebug.Log($"集火指令已结束");
                }
            }

            // 2. 只有当玩家按下攻击键时才尝试更新目标
            // 这样避免玩家只是看一眼就把女仆仇恨拉过去了
            if (Input.GetMouseButton(0))
            {
                DetectPlayerTarget();
            }

            // 3. 目标有效性检查
            if (FocusTarget != null && (FocusTarget.Health == null || FocusTarget.Health.IsDead))
            {
                FocusTarget = null;
            }
        }
        
        private void DetectPlayerTarget()
        {
            if (Camera.main == null) return;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, RaycastDistance, _enemyLayerMask))
            {
                var target = hit.collider.GetComponentInParent<CharacterMainControl>();
                
                if (target != null && !target.Health.IsDead)
                {
                    // 排除自己和队友
                    if (target.Team != Teams.player) 
                    {
                        // 更新目标
                        if (FocusTarget != target)
                        {
                            FocusTarget = target;
                            CMDebug.Log($"标记集火目标: {target.name}");
                        }
                        _focusExpireTimer = FocusDuration;
                    }
                }
            }
        }

        // ==================== JSON 加载逻辑 ====================

        /// <summary>
        /// 从 Mod 目录加载默认女仆配置
        /// </summary>
        private void LoadDefaultPreset()
        {
            string modAssemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string presetPath = Path.Combine(modAssemblyDir, "MaidPreset", "default_maid.json");

            CMDebug.Log($"尝试加载配置: {presetPath}");

            if (File.Exists(presetPath))
            {
                try
                {
                    string jsonContent = File.ReadAllText(presetPath);
                    
                    _currentProfileData = JsonConvert.DeserializeObject<MaidProfileData>(jsonContent);
                    
                    if (_currentProfileData != null)
                    {
                        if (_currentProfileData.PresetConfig != null)
                        {
                            CMDebug.Log($"配置加载成功！名称: {_currentProfileData.PresetConfig.CustomName}");
                        }
                        else
                        {
                            CMDebug.LogError($"JSON 读取成功，但 PresetConfig 节点为空！请检查 JSON 结构是否包含 'PresetConfig'。");
                            _currentProfileData = _fallbackProfile;
                        }
                        return;
                    }
                }
                catch (System.Exception ex)
                {
                    CMDebug.LogError($"配置解析严重失败: {ex.Message}");
                }
            }
            else
            {
                CMDebug.LogWarning($"配置文件未找到，将创建默认文件模板。");
                EnsureDirectoryExists(Path.GetDirectoryName(presetPath));
                WriteDefaultJson(presetPath);
            }
            
            _currentProfileData = _fallbackProfile;
            CMDebug.LogWarning($"已启用内置回退配置。");
        }

        private void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        private void WriteDefaultJson(string path)
        {
            try
            {
                string json = JsonConvert.SerializeObject(_fallbackProfile, Formatting.Indented);
                File.WriteAllText(path, json);
                CMDebug.Log($"已生成默认配置文件模板。");
            }
            catch (System.Exception ex)
            {
                CMDebug.LogError($"无法写入默认配置: {ex.Message}");
            }
        }

        // ==================== 核心功能 ====================

        private Vector3 GetMousePosition()
        {
            if (Camera.main == null) return Vector3.zero;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, LayerMask.GetMask("Default", "Ground", "Terrain")))
            {
                return hit.point;
            }
            return Vector3.zero;
        }

        private void CommandMoveTeamToMouse()
        {
            Vector3 targetPos = GetMousePosition();

            if (targetPos == Vector3.zero) return;

            CMDebug.Log($"全队移动指令(G) -> {targetPos}");

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
        
        /// <summary>
        /// [新增公共接口] 在指定位置生成配置好的女仆
        /// </summary>
        /// <param name="targetPos">生成坐标</param>
        /// <param name="presetKeyOverride">可选：覆盖用的基底预设Key</param>
        public void SpawnMaidAt(Vector3 targetPos, string presetKeyOverride = null)
        {
            if (LevelManager.Instance?.MainCharacter == null || MaidSpawner.Instance == null) return;

            // 1. 确保配置已加载
            if (_currentProfileData == null) LoadDefaultPreset();

            var spawnConfig = _currentProfileData.PresetConfig;
            var extraData = _currentProfileData.ExtraData;
            
            // 2. 确定基底预设 (优先使用 Config 中的定义)
            string targetKey = !string.IsNullOrEmpty(extraData.BasePresetKey) ? extraData.BasePresetKey : presetKeyOverride;
            if (string.IsNullOrEmpty(targetKey)) targetKey = "Cname_Usec"; // 保底

            CMDebug.Log($"正在基于预设 [{targetKey}] 生成女仆...");

            // 3. 调用 Spawner 生成物理实体
            MaidSpawner.Instance.SpawnMaid(targetKey, targetPos, LevelManager.Instance.MainCharacter, 
                spawnConfig, _currentProfileData.ProfileName, (ai) =>
            {
                // 4. [关键] 挂载控制器并初始化
                var controller = ai.gameObject.AddComponent<MaidController>();
                controller.Initialize(_currentProfileData, LevelManager.Instance.MainCharacter);
                
                // 5. [关键] 应用自定义模型 (如果有)
                if (extraData != null && !string.IsNullOrEmpty(extraData.CustomModelID))
                {
                    this.StartCoroutine(
                        CombatMaid.Core.CustomModel.CustomModelBridge.ApplyModelByIDAsync(
                            ai.CharacterMainControl, 
                            extraData.CustomModelID
                        )
                    );
                }
                
                // 6. 注册到管理列表
                if (!_activeMaids.Contains(controller)) _activeMaids.Add(controller);
                
                if (ai.CharacterMainControl != null) ai.CharacterMainControl.PopText("女仆就绪！");
            });
        }
        
        private void SpawnSpecificMaid(string debugKeyOverride = null)
        {
            Vector3 mousePos = GetMousePosition();
            if (mousePos == Vector3.zero) return;

            // 调用公共方法
            SpawnMaidAt(mousePos, debugKeyOverride);
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
                        Destroy(maid.MaidCharacter.gameObject);
                    }
                    else if (maid.gameObject != null)
                    {
                        Destroy(maid.gameObject);
                    }
                }
            }
            _activeMaids.Clear();
            CMDebug.Log($"队伍已清理");
        }
    }
    
    // ==================== JSON 数据结构 ====================

    [System.Serializable]
    public class MaidProfileData
    {
        public string ProfileName;
        public MaidConfig PresetConfig; 
        public MaidExtraInfo ExtraData; 
    }

    [System.Serializable]
    public class MaidExtraInfo
    {
        public string Description;
        
        [Header("生成基底")]
        public string BasePresetKey = "Cname_Usec";

        [Header("外观模型")]
        public string CustomModelID = ""; 
        
        [Header("Mod行为")]
        public bool EnableAutoHeal = true;
        public string TacticalMode = "Standard";
        
        [Header("技能配置")]
        public bool EnableGrenade = false;
        public int GrenadeItemID = 67;
        
        public string BuffSkillName = "";
        public int BuffSkillID = 0;
    }
}