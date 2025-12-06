using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using Duckov.Modding;
using CombatMaid.Core.MaidConfigs;
using CombatMaid.Core.WineFox;
using CombatMaid.Core.AttributeModifiers;
using Newtonsoft.Json;

namespace CombatMaid.Core
{
    public class MaidManager : MonoBehaviour
    {
        public static MaidManager Instance { get; private set; }

        // Key = ProfileName (仅存储普通女仆的只读配置)
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
            else
            {
                Destroy(this);
            }
        }

        private void Update()
        {
            HandleDebugInput();
            UpdateFocusTarget();
        }

        public void OnLevelStart(string sceneName)
        {
            CMDebug.Log($"场景加载: {sceneName}");
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

            // H 强制回血
            if (Input.GetKeyDown(KeyCode.H)) CommandForceHealTeam();

            // F9 调试输出
            if (Input.GetKeyDown(KeyCode.F9))
            {
                if (MaidSpawner.Instance != null)
                {
                    MaidSpawner.Instance.DebugListAllKeys();
                    MaidSpawner.Instance.DebugExportReferenceStats();
                }
            }
        }

        // ==================== JSON 加载逻辑 (普通女仆) ====================

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
        /// [入口A] 根据 ProfileName 字符串生成女仆
        /// 包含特殊逻辑分流：如果是酒狐，则从存档读取
        /// </summary>
        public void SpawnMaidAt(string profileName, Vector3 targetPos)
        {
            if (MaidSpawner.Instance == null)
            {
                CMDebug.LogError("生成失败：MaidSpawner 未初始化！");
                return;
            }

            if (LevelManager.Instance?.MainCharacter == null) return;

            // --- 数据源分流 ---
            MaidProfileData finalData = null;
            bool isWineFox = (profileName == "RoyalMaid_WineFox");

            if (isWineFox)
            {
                // 分支A：酒狐 - 走持久化逻辑 (存档 + 技能树)
                finalData = WineFoxDataManager.LoadOrInit();
                if (finalData == null)
                {
                    CMDebug.LogError($"生成失败：酒狐数据初始化严重错误！");
                    return;
                }
            }
            else
            {
                // 分支B：普通女仆 - 走内存字典
                if (!_maidProfiles.TryGetValue(profileName, out finalData))
                {
                    CMDebug.LogError($"生成失败：找不到名为 '{profileName}' 的女仆配置！");
                    return;
                }
            }

            // --- 调用核心重载 ---
            SpawnMaidAt(targetPos, finalData, (controller) =>
            {
                // 如果是酒狐，额外挂载同步组件
                if (isWineFox && controller != null)
                {
                    var sync = controller.gameObject.AddComponent<WineFoxDataSync>();
                    sync.Initialize(controller, finalData);
                    CMDebug.Log("已挂载酒狐数据同步组件");
                }
            });
        }

        /// <summary>
        /// [入口B] 专用于酒狐生成的快捷方法 (给外部契约调用)
        /// </summary>
        public void SpawnWineFox(Vector3 position)
        {
            // 复用 SpawnMaidAt 的字符串入口逻辑，它会自动处理分流
            SpawnMaidAt("RoyalMaid_WineFox", position);
        }

        /// <summary>
        /// [核心重载] 直接根据 Data 对象生成实体
        /// 所有的生成逻辑最终都汇总到这里
        /// </summary>
        public void SpawnMaidAt(Vector3 targetPos, MaidProfileData profileData, System.Action<MaidController> onComplete = null)
        {
            if (profileData == null) return;

            var spawnConfig = profileData.PresetConfig;
            var extraData = profileData.ExtraData;
            
            string baseKey = extraData != null && !string.IsNullOrEmpty(extraData.BasePresetKey)
                ? extraData.BasePresetKey
                : "Cname_Usec";

            CMDebug.Log($"正在生成 [{profileData.ProfileName}] (Base: {baseKey})...");

            MaidSpawner.Instance.SpawnMaid(
                baseKey,
                targetPos,
                LevelManager.Instance.MainCharacter,
                spawnConfig,
                profileData.ProfileName,
                (ai) =>
                {
                    // 1. 通用初始化
                    var controller = OnMaidSpawnedCallback(ai.CharacterMainControl, profileData);
                    
                    // 2. 触发回调 (用于外部挂载额外组件)
                    onComplete?.Invoke(controller);
                }
            );
        }

        /// <summary>
        /// 生成后的通用初始化步骤
        /// </summary>
        private MaidController OnMaidSpawnedCallback(CharacterMainControl character, MaidProfileData profileData)
        {
            if (character == null) return null;

            // 1. 挂载核心控制器
            var controller = character.gameObject.AddComponent<MaidController>();
            controller.Initialize(profileData, LevelManager.Instance.MainCharacter);

            // 2. 应用自定义模型 
            if (profileData.ExtraData != null && !string.IsNullOrEmpty(profileData.ExtraData.CustomModelID))
            {
                StartCoroutine(CombatMaid.Core.CustomModel.CustomModelBridge.ApplyModelByIDAsync(
                    character, 
                    profileData.ExtraData.CustomModelID
                ));
            }
    
            // 3. 注册到列表
            if (!_activeMaids.Contains(controller)) _activeMaids.Add(controller);
    
            return controller;
        }

        private void SpawnSpecificMaid(string profileName)
        {
            Vector3 mousePos = GetMousePosition();
            if (mousePos != Vector3.zero)
            {
                SpawnMaidAt(profileName, mousePos);
            }
        }
        
        
        public void ApplyGlobalSkillEffect(CombatMaid.Core.SkillTreeSystem.MaidSkillGrantBehaviour effectData)
        {
            if (effectData == null) return;
            if (_activeMaids.Count == 0) return;

            CMDebug.Log($"[SkillTree] 收到强化通知，正在扫描场上的酒狐...");

            int count = 0;
            foreach (var maid in _activeMaids)
            {
                if (maid == null || maid.MaidCharacter == null) continue;

                // 1. [核心过滤] 只对挂载了同步组件的“酒狐”生效
                // 需确保引用命名空间: using CombatMaid.Core.WineFox;
                var wineFoxSync = maid.GetComponent<CombatMaid.Core.WineFox.WineFoxDataSync>();
                if (wineFoxSync == null) continue;

                count++;
                
                // 2. 应用属性加成 (MaidStatModifiers)
                if (effectData.MaidStatModifiers != null)
                {
                    foreach (var kvp in effectData.MaidStatModifiers)
                    {
                        // 调用你现有的属性修改工具
                        // 假设技能树给的是直接数值加成 (isMultiplier = false)
                        // 如果你的技能树设计是百分比(如 0.1 代表 10%)，请将 isMultiplier 改为 true
                        CombatMaid.Core.AttributeModifiers.AttributeModifier.Modify(
                            maid.MaidCharacter,
                            kvp.Key,
                            kvp.Value,
                            isMultiplier: false 
                        );
                        
                        maid.MaidCharacter.PopText($"{kvp.Key} UP!");
                    }
                }

                // 3. 应用新技能解锁 (UnlockAbilityID)
                if (!string.IsNullOrEmpty(effectData.UnlockAbilityID))
                {
                    // 检查是否已经拥有该技能，防止重复添加
                    // 这里假设 SkillSystem 有个 HasSkill 方法，或者我们直接加，Factory通常会处理
                    // 构建一个临时的配置对象
                    var skillConfig = new MaidSkillConfig 
                    { 
                        SkillID = effectData.UnlockAbilityID 
                        // 如果技能树支持传参，可以在这里扩展
                    };

                    // 使用工厂创建技能
                    var newSkill = CombatMaid.Core.MaidSkillSystem.MaidSkillFactory.CreateSkill(skillConfig);
                    
                    if (newSkill != null)
                    {
                        maid.SkillSystem.AddSkill(newSkill);
                        maid.MaidCharacter.PopText($"习得技能: {newSkill.SkillName}!");
                        CMDebug.Log($"酒狐 {maid.name} 实时习得了 {newSkill.SkillName}");
                    }
                }
            }
            
            if (count > 0)
            {
                CMDebug.Log($"[SkillTree] 已实时强化 {count} 只酒狐实例。");
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
                }
            }

            if (Input.GetMouseButton(0)) DetectPlayerTarget();

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
                    Vector3 offset = new Vector3(Random.Range(-0.5f, 0.5f), 0, Random.Range(-0.5f, 0.5f));
                    maid.ForceMoveTo(targetPos + offset);
                    maid.MaidCharacter.PopText("战术移动！");
                }
            }
        }

        private void CommandForceHealTeam()
        {
            CMDebug.Log("[指令] 强制全队尝试使用医疗包 (H)");
            for (int i = _activeMaids.Count - 1; i >= 0; i--)
            {
                var maid = _activeMaids[i];
                maid.ForceHeal(); 
                maid.MaidCharacter.PopText("手动治疗！");
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
        public string TacticalMode = "Standard";

        [Header("通用技能配置")] 
        public List<MaidSkillConfig> Skills = new List<MaidSkillConfig>();
    }

    [System.Serializable]
    public class MaidSkillConfig
    {
        public string SkillID; // 例如 "Grenade", "AutoHeal"
        public Dictionary<string, object> Params = new Dictionary<string, object>();
    }
}