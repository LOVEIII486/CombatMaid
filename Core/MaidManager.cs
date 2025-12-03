using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using Duckov.Modding;
using CombatMaid.Core.MaidConfigs; // 引用配置命名空间
using Newtonsoft.Json;

namespace CombatMaid.Core
{
    public class MaidManager : MonoBehaviour
    {
        private const string LogTag = "[CombatMaid.MaidManager]";
        public static MaidManager Instance { get; private set; }

        private List<MaidController> _activeMaids = new List<MaidController>();

        // 当前加载的默认配置
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
                // 默认不需要 FaceCode，使用基底预设的脸
            },
            ExtraData = new MaidExtraInfo()
            {
                Description = "代码默认配置",
                BasePresetKey = "Cname_Usec", // [新增] 默认基底
                CustomModelID = "10004", 
                EnableAutoHeal = true,
                TacticalMode = "Assault"
            }
        };

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                
                // 初始化时尝试加载 JSON
                LoadDefaultPreset();
                
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
            
            // F8: 重载配置 (方便调试，不用重启游戏)
            if (Input.GetKeyDown(KeyCode.F8)) LoadDefaultPreset();

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

        // ==================== JSON 加载逻辑 ====================

        /// <summary>
        /// 从 Mod 目录加载默认女仆配置
        /// </summary>
        private void LoadDefaultPreset()
        {
            string modAssemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string presetPath = Path.Combine(modAssemblyDir, "MaidPreset", "default_maid.json");

            Debug.Log($"{LogTag} 尝试加载配置: {presetPath}");

            if (File.Exists(presetPath))
            {
                try
                {
                    string jsonContent = File.ReadAllText(presetPath);
                    
                    // [修改] 改用 JsonConvert (Newtonsoft)
                    // 它能更好地处理嵌套对象、列表和容错
                    _currentProfileData = JsonConvert.DeserializeObject<MaidProfileData>(jsonContent);
                    
                    if (_currentProfileData != null)
                    {
                        // [修复] 安全检查，防止崩溃
                        if (_currentProfileData.PresetConfig != null)
                        {
                            Debug.Log($"{LogTag} 配置加载成功！名称: {_currentProfileData.PresetConfig.CustomName}");
                        }
                        else
                        {
                            Debug.LogError($"{LogTag} JSON 读取成功，但 PresetConfig 节点为空！请检查 JSON 结构是否包含 'PresetConfig'。");
                            // 如果关键数据为空，强制使用默认值，防止后续逻辑报错
                            _currentProfileData = _fallbackProfile;
                        }
                        return;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"{LogTag} 配置解析严重失败: {ex.Message}");
                }
            }
            else
            {
                Debug.LogWarning($"{LogTag} 配置文件未找到，将创建默认文件模板。");
                EnsureDirectoryExists(Path.GetDirectoryName(presetPath));
                WriteDefaultJson(presetPath);
            }

            // 兜底逻辑
            _currentProfileData = _fallbackProfile;
            Debug.LogWarning($"{LogTag} 已启用内置回退配置。");
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
                // [修改] 使用 JsonConvert 写入，格式更标准
                string json = JsonConvert.SerializeObject(_fallbackProfile, Formatting.Indented);
                File.WriteAllText(path, json);
                Debug.Log($"{LogTag} 已生成默认配置文件模板。");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"{LogTag} 无法写入默认配置: {ex.Message}");
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
        
        private void SpawnSpecificMaid(string debugKeyOverride = null)
        {
            if (LevelManager.Instance?.MainCharacter == null || MaidSpawner.Instance == null) return;

            Vector3 mousePos = GetMousePosition();
            if (mousePos == Vector3.zero) return;

            if (_currentProfileData == null) LoadDefaultPreset();

            var spawnConfig = _currentProfileData.PresetConfig;
            var extraData = _currentProfileData.ExtraData;

            // [修改] 优先使用 ExtraData 中的 BasePresetKey，如果没有则使用传入的 debugKey (如F6测试用)
            string targetKey = !string.IsNullOrEmpty(extraData.BasePresetKey) ? extraData.BasePresetKey : debugKeyOverride;
            
            // 如果 JSON 里没写，fallback 里也没写，给个兜底
            if (string.IsNullOrEmpty(targetKey)) targetKey = "Cname_Usec";

            Debug.Log($"{LogTag} 正在基于预设 [{targetKey}] 生成女仆...");

            MaidSpawner.Instance.SpawnMaid(targetKey, mousePos, LevelManager.Instance.MainCharacter, 
                spawnConfig, _currentProfileData.ProfileName, (ai) =>
            {
                var controller = ai.gameObject.AddComponent<MaidController>();
                controller.Initialize(_currentProfileData, LevelManager.Instance.MainCharacter);
                
                // 应用自定义模型 (DuckovCustomModel)
                if (extraData != null && !string.IsNullOrEmpty(extraData.CustomModelID))
                {
                    this.StartCoroutine(
                        CombatMaid.Core.CustomModel.CustomModelBridge.ApplyModelByIDAsync(
                            ai.CharacterMainControl, 
                            extraData.CustomModelID
                        )
                    );
                }
                
                if (!_activeMaids.Contains(controller)) _activeMaids.Add(controller);
                if (ai.CharacterMainControl != null) ai.CharacterMainControl.PopText("女仆就绪！");
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
    }
}