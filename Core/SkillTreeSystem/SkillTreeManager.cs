using System.Collections;
using System.Collections.Generic;
using CombatMaid.Localization;
using UnityEngine;
using Duckov.Scenes;
using Duckov.PerkTrees;
using HarmonyLib;

namespace CombatMaid.Core.SkillTreeSystem
{
    public class SkillTreeManager : MonoBehaviour
    {
        public static SkillTreeManager Instance { get; private set; }

        private const string TREE_ID = "MaidCombatSkills";
        private const string INTERACT_KEY = "Interaction_MaidSkill_Label";
        private const string DEFAULT_CONFIG_FILE = "SkillTree_MaidTech.json";

        private bool _isTreeBuilt = false;
        private PerkTree _customTree;
        private bool _isInitializing = false;

        private SkillTreeConfig _currentConfig;

        private SkillTreeSaveData _saveData;
        private Dictionary<string, Perk> _runtimePerks = new Dictionary<string, Perk>();
        private Dictionary<string, SkillNodeDef> _nodeDefsMap = new Dictionary<string, SkillNodeDef>();

        private void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnAnySceneLoaded;
        }

        private void OnDestroy()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnAnySceneLoaded;
            SaveProgress();
        }

        private void OnAnySceneLoaded(UnityEngine.SceneManagement.Scene scene,
            UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (scene.name == "Base_SceneV2")
            {
                CMDebug.Log($"检测到基地场景: {scene.name}");
                StartCoroutine(InitSkillTreeRoutine());
            }
        }

        private IEnumerator InitSkillTreeRoutine()
        {
            if (_isInitializing) yield break;
            _isInitializing = true;

            // 使用轮询机制替代固定等待
            GameObject skillBuilding = null;
            float timeOut = 15f; // 设置最大等待时间
            float timer = 0f;
            float checkInterval = 0.5f;

            CMDebug.Log("开始寻找 SkillMachine...");

            while (skillBuilding == null && timer < timeOut)
            {
                skillBuilding = FindSkillMachine();

                if (skillBuilding != null)
                {
                    CMDebug.Log($"成功找到 SkillMachine (耗时: {timer:F1}s)");
                    break;
                }

                yield return new WaitForSeconds(checkInterval);
                timer += checkInterval;
            }

            if (skillBuilding == null)
            {
                CMDebug.LogWarning($"初始化失败：在 {timeOut} 秒内未找到 SkillMachine，跳过技能树构建。");
                _isInitializing = false; // 务必在退出前重置状态
                yield break;
            }

            try
            {
                if (_customTree != null)
                {
                    CMDebug.Log("检测到跨场景残留的技能树，正在清理...");
                    if (_customTree.gameObject != null) Destroy(_customTree.gameObject);
                    _customTree = null;
                }

                CMDebug.Log("开始构建新场景的技能树...");

                // 1. 清理旧数据
                _isTreeBuilt = false;
                _runtimePerks.Clear();
                _nodeDefsMap.Clear();

                // 2. 加载存档
                _saveData = SkillTreePersistence.Load();

                // 3. 构建
                BuildSkillTree();

                // 4. 恢复购买状态
                RestorePurchasedState();

                // 5. 验证
                if (_customTree != null)
                {
                    _isTreeBuilt = true;
                    string interactTitle = LocalizationManager.GetText("SkillTree_InteractTitle");
                    SkillTreeBuilder.RegisterInteraction(skillBuilding, TREE_ID, INTERACT_KEY, interactTitle);
                    CMDebug.LogInfo("技能树初始化完毕");
                }
            }
            catch (System.Exception ex)
            {
                CMDebug.LogError($"初始化异常: {ex}");
            }
            finally
            {
                _isInitializing = false;
            }
        }
        
        private GameObject FindSkillMachine()
        {
            GameObject obj = GameObject.Find("SkillMachine");
            if (obj != null) return obj;

            if (MultiSceneCore.ActiveSubScene.HasValue)
            {
                var scene = MultiSceneCore.ActiveSubScene.Value;
                if (scene.IsValid())
                {
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        if (root.name == "Buildings")
                            return root.transform.Find("SkillMachine")?.gameObject;
                    }
                }
            }

            return null;
        }

        private void BuildSkillTree()
        {
            CMDebug.Log("开始构建技能树...");
            _runtimePerks.Clear();
            _nodeDefsMap.Clear();

            try
            {
                // 1. 加载配置文件
                if (_currentConfig == null)
                {
                    string modPath = ModBehaviour.Instance?.ModRootPath;
                    if (string.IsNullOrEmpty(modPath))
                    {
                        CMDebug.LogError("[BuildSkillTree] 无法获取模组路径");
                        return;
                    }

                    _currentConfig = SkillTreeConfigLoader.LoadFromFile(modPath, DEFAULT_CONFIG_FILE);

                    if (_currentConfig == null)
                    {
                        CMDebug.LogError("[BuildSkillTree] 配置文件加载失败");
                        return;
                    }
                }

                // 2. 创建树
                string treeName = !string.IsNullOrEmpty(_currentConfig.TreeName)
                    ? _currentConfig.TreeName
                    : "女仆战术";

                _customTree = SkillTreeBuilder.CreateEmptyTree(TREE_ID, treeName);

                if (_customTree == null)
                {
                    CMDebug.LogError("[BuildSkillTree] CreateEmptyTree 返回 null！");
                    return;
                }

                CMDebug.Log($"[BuildSkillTree] 技能树已创建: {_customTree.name}");

                // 将技能树设置为 SkillTreeManager 的子对象，确保跟随 DontDestroyOnLoad
                _customTree.transform.SetParent(this.transform);
                // 确保技能树对象本身也设置 DontDestroyOnLoad
                DontDestroyOnLoad(_customTree.gameObject);

                // 3. 转换配置为节点定义
                var nodes = SkillTreeConfigLoader.ConvertToNodeDefs(_currentConfig);
                CMDebug.Log($"[BuildSkillTree] 转换了 {nodes.Count} 个节点定义");

                // 4. 添加节点
                foreach (var nodeDef in nodes)
                {
                    if (!_nodeDefsMap.ContainsKey(nodeDef.ID))
                    {
                        _nodeDefsMap.Add(nodeDef.ID, nodeDef);
                    }

                    // 加载图标
                    string iconName = !string.IsNullOrEmpty(nodeDef.IconFileName)
                        ? nodeDef.IconFileName
                        : "default_icon.png";
                    nodeDef.Icon = SkillIconLoader.LoadIcon(iconName);

                    var perk = SkillTreeBuilder.AddNodeToTree(_customTree, nodeDef);
                    if (perk != null)
                    {
                        _runtimePerks.Add(nodeDef.ID, perk);
                    }
                    else
                    {
                        CMDebug.LogWarning($"[BuildSkillTree] 节点 {nodeDef.ID} 添加失败");
                    }
                }

                // 5. 重建连接
                SkillTreeBuilder.RebuildGraphConnections(_customTree, nodes, _runtimePerks);

                CMDebug.LogInfo($"[BuildSkillTree] ✓ 技能树构建完成，共 {_runtimePerks.Count} 个节点");
            }
            catch (System.Exception ex)
            {
                CMDebug.LogError($"[BuildSkillTree] 构建过程中发生异常: {ex.Message}\n{ex.StackTrace}");
                _customTree = null;
                throw;
            }
        }

        /// <summary>
        /// 查询指定 ID 的技能节点是否已解锁
        /// </summary>
        public bool IsSkillUnlocked(string nodeID)
        {
            if (string.IsNullOrEmpty(nodeID)) return false;

            if (_runtimePerks != null && _runtimePerks.TryGetValue(nodeID, out Perk perk))
            {
                if (perk != null && perk.Unlocked) return true;
            }

            if (_saveData != null && _saveData.UnlockedNodeIDs != null)
            {
                if (_saveData.UnlockedNodeIDs.Contains(nodeID)) return true;
            }

            if (_saveData == null)
            {
                var tempList = SkillTreePersistence.Load();
                if (tempList != null && tempList.UnlockedNodeIDs.Contains(nodeID))
                {
                    _saveData = tempList;
                    return true;
                }
            }

            return false;
        }

        private void RestorePurchasedState()
        {
            if (_saveData == null) return;

            foreach (var id in _saveData.UnlockedNodeIDs)
            {
                if (_runtimePerks.TryGetValue(id, out Perk perk))
                {
                    Traverse.Create(perk).Property("Unlocked").SetValue(true);
                    CMDebug.Log($"[RestorePurchasedState] 已恢复节点: {id}");
                }
            }
        }

        public void SaveProgress()
        {
            if (_saveData == null || _runtimePerks == null) return;

            try
            {
                bool hasChanges = false;

                foreach (var kvp in _runtimePerks)
                {
                    string id = kvp.Key;
                    Perk perk = kvp.Value;

                    if (perk == null) continue;

                    bool isUnlocked = perk.Unlocked;

                    if (isUnlocked && !_saveData.UnlockedNodeIDs.Contains(id))
                    {
                        _saveData.UnlockedNodeIDs.Add(id);
                        hasChanges = true;
                        CMDebug.Log($"[SaveProgress] 新解锁节点: {id}");
                    }
                }

                if (hasChanges)
                {
                    SkillTreePersistence.Save(_saveData);
                }
                else
                {
                    CMDebug.Log($"[SaveProgress] 无变化，跳过保存");
                }
            }
            catch (System.Exception ex)
            {
                CMDebug.LogError($"[SaveProgress] 保存失败: {ex.Message}");
            }
        }


        public void ReloadTree()
        {
            CMDebug.LogWarning("开始执行热重载...");

            if (_customTree != null)
            {
                Destroy(_customTree.gameObject);
                _customTree = null;
            }

            _currentConfig = null;
            _isTreeBuilt = false;
            _runtimePerks.Clear();
            _nodeDefsMap.Clear();
            _isInitializing = false;

            StartCoroutine(InitSkillTreeRoutine());

            CMDebug.LogInfo("热重载请求已发送");
        }
    }
}