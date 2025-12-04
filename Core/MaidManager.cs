using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using Duckov.Modding;
using CombatMaid.Core.MaidConfigs;
using Newtonsoft.Json;

namespace CombatMaid.Core
{
    public class MaidManager : MonoBehaviour
    {
        public static MaidManager Instance { get; private set; }

        // Key = ProfileName
        private Dictionary<string, MaidProfileData> _maidProfiles = new Dictionary<string, MaidProfileData>();
        
        private List<MaidController> _activeMaids = new List<MaidController>();
        
        // 集火系统变量
        public CharacterMainControl FocusTarget { get; private set; } 
        private float _focusExpireTimer = 0f;
        private const float FocusDuration = 5.0f;
        private const float RaycastDistance = 25f;
        private int _enemyLayerMask;
        
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                
                _enemyLayerMask = LayerMask.GetMask("Default", "Character", "Hitbox", "Enemy");
                
                LoadAllPresets();
                
                CMDebug.Log("MaidManager 初始化完成。");
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
            CMDebug.Log($"场景加载: {sceneName}");
            // 之后可以在这里重新加载配置以便热更
            // LoadAllPresets(); 
        }

        public void OnLevelEnd()
        {
            DespawnTeam();
        }
        
        private void HandleDebugInput()
        {
            // F5 测试生成默认的贝拉
            if (Input.GetKeyDown(KeyCode.F5)) SpawnSpecificMaid("RoyalMaid_Bella"); 
            
            // F6 清除
            if (Input.GetKeyDown(KeyCode.F6)) DespawnTeam();
            
            // F8 重载配置
            if (Input.GetKeyDown(KeyCode.F8)) LoadAllPresets();
            
            // G 移动指令
            if (Input.GetKeyDown(KeyCode.G)) CommandMoveTeamToMouse();
            
            if (Input.GetKeyDown(KeyCode.F9))
            {
                if (MaidSpawner.Instance != null)
                {
                    MaidSpawner.Instance.DebugListAllKeys();
                    MaidSpawner.Instance.DebugExportReferenceStats();
                }
            }
        }

        // ==================== JSON 加载逻辑 ====================

        public void LoadAllPresets()
        {
            _maidProfiles.Clear();
            
            string modAssemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string presetDir = Path.Combine(modAssemblyDir, "MaidPreset");

            if (!Directory.Exists(presetDir))
            {
                Directory.CreateDirectory(presetDir);
                CMDebug.LogWarning($"创建了配置文件夹: {presetDir}");
                return;
            }

            string[] files = Directory.GetFiles(presetDir, "*.json");
            CMDebug.Log($"找到 {files.Length} 个配置文件，开始加载...");

            foreach (string file in files)
            {
                try
                {
                    string jsonContent = File.ReadAllText(file);
                    var data = JsonConvert.DeserializeObject<MaidProfileData>(jsonContent);

                    if (data != null && !string.IsNullOrEmpty(data.ProfileName))
                    {
                        if (_maidProfiles.ContainsKey(data.ProfileName))
                        {
                            CMDebug.LogWarning($"检测到重复的 ProfileName: {data.ProfileName}，将覆盖旧配置。");
                        }
                        
                        _maidProfiles[data.ProfileName] = data;
                        CMDebug.Log($"已加载预设: {data.ProfileName} ({data.PresetConfig?.CustomName})");
                    }
                    else
                    {
                        CMDebug.LogWarning($"文件 {Path.GetFileName(file)} 解析失败或 ProfileName 为空。");
                    }
                }
                catch (System.Exception ex)
                {
                    CMDebug.LogError($"加载配置文件失败 {Path.GetFileName(file)}: {ex.Message}");
                }
            }
        }

        // ==================== 核心生成逻辑 ====================

        /// <summary>
        /// 根据 ProfileName 在指定位置生成女仆
        /// </summary>
        /// <param name="profileName">JSON中定义的唯一ID (例如 "RoyalMaid_Bella")</param>
        /// <param name="targetPos">生成位置</param>
        public void SpawnMaidAt(string profileName, Vector3 targetPos)
        {
            // 1. 基础检查
            if (MaidSpawner.Instance == null)
            {
                CMDebug.LogError("生成失败：MaidSpawner 未初始化！");
                return;
            }
            
            if (LevelManager.Instance?.MainCharacter == null) return;

            // 2. 查找配置
            if (!_maidProfiles.TryGetValue(profileName, out var profileData))
            {
                CMDebug.LogError($"生成失败：找不到名为 '{profileName}' 的女仆配置！请检查 JSON 文件。");
                return;
            }

            // 3. 准备数据
            var spawnConfig = profileData.PresetConfig;
            var extraData = profileData.ExtraData;
            
            // 确定基底
            string baseKey = extraData != null && !string.IsNullOrEmpty(extraData.BasePresetKey) 
                ? extraData.BasePresetKey 
                : "Cname_Usec";

            CMDebug.Log($"正在生成 [{profileName}] (Base: {baseKey})...");

            // 4. 执行生成
            MaidSpawner.Instance.SpawnMaid(
                baseKey, 
                targetPos, 
                LevelManager.Instance.MainCharacter, 
                spawnConfig, 
                profileName, 
                (ai) => OnMaidSpawnedCallback(ai.CharacterMainControl, profileData)
            );
        }

        /// <summary>
        /// 生成后的初始化回调
        /// </summary>
        private void OnMaidSpawnedCallback(CharacterMainControl ai, MaidProfileData profileData)
        {
            if (ai == null) return;

            // 1. 挂载控制器
            var controller = ai.gameObject.AddComponent<MaidController>();
            controller.Initialize(profileData, LevelManager.Instance.MainCharacter);
            
            // 2. 应用自定义模型
            if (profileData.ExtraData != null && !string.IsNullOrEmpty(profileData.ExtraData.CustomModelID))
            {
                StartCoroutine(CombatMaid.Core.CustomModel.CustomModelBridge.ApplyModelByIDAsync(
                    ai, 
                    profileData.ExtraData.CustomModelID
                ));
            }
            
            // 3. 加入管理列表
            if (!_activeMaids.Contains(controller)) 
            {
                _activeMaids.Add(controller);
            }
            
            ai.PopText(profileData.PresetConfig?.CustomName + " 参上！");
        }
        
        private void SpawnSpecificMaid(string profileName)
        {
            Vector3 mousePos = GetMousePosition();
            if (mousePos != Vector3.zero)
            {
                SpawnMaidAt(profileName, mousePos);
            }
        }

        // ==================== 队伍控制 & 集火逻辑 ====================

        private void UpdateFocusTarget()
        {
            if (_focusExpireTimer > 0)
            {
                _focusExpireTimer -= Time.deltaTime;
                if (_focusExpireTimer <= 0)
                {
                    FocusTarget = null;
                    // CMDebug.Log("集火指令结束");
                }
            }

            if (Input.GetMouseButton(0)) DetectPlayerTarget();

            // 目标死亡检测
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
                if (target != null && !target.Health.IsDead && target.Team != Teams.player)
                {
                    if (FocusTarget != target)
                    {
                        FocusTarget = target;
                        CMDebug.Log($"[指令] 集火目标: {target.name}");
                    }
                    _focusExpireTimer = FocusDuration;
                }
            }
        }

        private Vector3 GetMousePosition()
        {
            if (Camera.main == null) return Vector3.zero;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, LayerMask.GetMask("Default", "Ground", "Terrain")))
                return hit.point;
            return Vector3.zero;
        }

        private void CommandMoveTeamToMouse()
        {
            Vector3 targetPos = GetMousePosition();
            if (targetPos == Vector3.zero) return;

            for (int i = _activeMaids.Count - 1; i >= 0; i--)
            {
                var maid = _activeMaids[i];
                if (maid != null)
                {
                    // 稍微分散一点移动，避免重叠
                    Vector3 offset = new Vector3(Random.Range(-0.5f, 0.5f), 0, Random.Range(-0.5f, 0.5f));
                    maid.ForceMoveTo(targetPos + offset);
                }
            }
        }

        public void DespawnTeam()
        {
            for (int i = _activeMaids.Count - 1; i >= 0; i--)
            {
                var maid = _activeMaids[i];
                if (maid != null)
                {
                    if (maid.MaidCharacter != null) Destroy(maid.MaidCharacter.gameObject);
                    else if (maid.gameObject != null) Destroy(maid.gameObject);
                }
            }
            _activeMaids.Clear();
            CMDebug.Log("女仆队伍已解散");
        }
    }
    
    // ==================== 数据结构定义 ====================

    [System.Serializable]
    public class MaidProfileData
    {
        // 如 "RoyalMaid_Bella"
        public string ProfileName; 
        
        // 基础数值配置
        public MaidConfig PresetConfig; 
        
        // 模组特有的行为配置
        public MaidExtraInfo ExtraData; 
    }

    [System.Serializable]
    public class MaidExtraInfo
    {
        public string Description;
        
        [Header("基底预设")]
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