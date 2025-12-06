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
        private const string DEFAULT_CONFIG_FILE = "SkillTree_Combat.json";

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

        // 响应任何场景加载
        private void OnAnySceneLoaded(UnityEngine.SceneManagement.Scene scene,
            UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (scene.name == "Base" || scene.name == "Base_SceneV2")
            {
                CMDebug.Log($"[SkillTreeManager] 检测到基地场景: {scene.name}");
                StartCoroutine(InitSkillTreeRoutine());
            }
        }

        private IEnumerator InitSkillTreeRoutine()
        {
            // 防止协程重复执行
            if (_isInitializing)
            {
                CMDebug.Log("[SkillTreeManager] 初始化正在进行中，跳过");
                yield break;
            }

            _isInitializing = true;

            try
            {
                yield return new WaitForSeconds(0.5f); // 等待场景完全加载

                GameObject skillBuilding = FindSkillMachine();
                if (skillBuilding == null)
                {
                    CMDebug.LogWarning("[SkillTreeManager] 未找到 SkillMachine 建筑，跳过加载。");
                    yield break;
                }

                CMDebug.Log($"[SkillTreeManager] 找到建筑: {skillBuilding.name}");

                // 🔧 核心修复：检查技能树是否仍然有效，无效则重建
                bool needsRebuild = false;
                
                if (_customTree == null)
                {
                    needsRebuild = true;
                    CMDebug.Log("[SkillTreeManager] 技能树对象为 null，需要重建");
                }
                else
                {
                    // 检查技能树是否仍在 PerkTreeManager 中注册
                    var registeredTree = PerkTreeManager.GetPerkTree(TREE_ID);
                    if (registeredTree == null || registeredTree != _customTree)
                    {
                        needsRebuild = true;
                        CMDebug.LogWarning("[SkillTreeManager] 技能树未在 PerkTreeManager 中注册或引用不一致，需要重建");
                        
                        // 销毁旧对象
                        if (_customTree != null && _customTree.gameObject != null)
                        {
                            Destroy(_customTree.gameObject);
                            _customTree = null;
                        }
                    }
                    else
                    {
                        CMDebug.Log("[SkillTreeManager] 技能树状态正常，跳过重建");
                    }
                }

                // 如果需要重建
                if (needsRebuild)
                {
                    CMDebug.Log("[SkillTreeManager] 开始重建技能树");

                    try
                    {
                        // 1. 清理旧数据
                        _isTreeBuilt = false;
                        _runtimePerks.Clear();
                        _nodeDefsMap.Clear();

                        // 2. 加载存档
                        _saveData = SkillTreePersistence.Load();

                        // 3. 构建技能树
                        BuildSkillTree();

                        // 4. 验证构建结果
                        if (_customTree == null)
                        {
                            CMDebug.LogError("[SkillTreeManager] ✗ BuildSkillTree 失败：_customTree 仍为 null");
                            yield break;
                        }

                        // 5. 恢复已购买状态
                        RestorePurchasedState();

                        _isTreeBuilt = true;
                        CMDebug.LogInfo("[SkillTreeManager] ✓ 技能树重建成功");
                    }
                    catch (System.Exception ex)
                    {
                        CMDebug.LogError($"[SkillTreeManager] 重建技能树时发生异常: {ex.Message}\n{ex.StackTrace}");
                        _isTreeBuilt = false;
                        _customTree = null;
                        yield break;
                    }
                }

                // 每次进入场景都重新注册交互点
                if (_customTree != null)
                {
                    CMDebug.Log($"[SkillTreeManager] 准备注册交互点到建筑: {skillBuilding.name}");
                    SkillTreeBuilder.RegisterInteraction(skillBuilding, TREE_ID, INTERACT_KEY, "战斗女仆: 战术技能");
                    CMDebug.Log("[SkillTreeManager] ✓ 交互点已重新注册");
                }
                else
                {
                    CMDebug.LogError("[SkillTreeManager] ✗ _customTree 为 null，无法注册交互点！");
                }
            }
            finally
            {
                _isInitializing = false;
            }
        }

        /// <summary>
        /// [已删除] ReregisterTreeToPerkTreeManager 方法（不再需要）
        /// 改为直接重建技能树以确保状态完全一致
        /// </summary>

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
            CMDebug.Log("[SkillTreeManager] 开始构建技能树...");
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
                
                // 🔧 修复：将技能树设置为 SkillTreeManager 的子对象，确保跟随 DontDestroyOnLoad
                _customTree.transform.SetParent(this.transform);
                
                // 🔧 修复：确保技能树对象本身也设置 DontDestroyOnLoad（冗余保险）
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


        public void ApplyPassiveEffectsToMaid(MaidController maid)
        {
            if (_saveData == null || maid == null) return;

            CMDebug.Log($"[SkillTree] 正在为 {maid.name} 应用技能树加成...");

            foreach (var kvp in _runtimePerks)
            {
                string id = kvp.Key;
                Perk perk = kvp.Value;

                // 检查是否解锁
                bool isUnlocked = perk != null && perk.Unlocked;

                if (isUnlocked && _nodeDefsMap.TryGetValue(id, out SkillNodeDef def))
                {
                    // 1. 应用属性加成 (Stat Modifiers)
                    // Stat系统自带去重/堆叠处理，只要我们不修改存档里的BaseValue，这里重复Add是安全的(AddModifier是临时的)
                    if (def.MaidStatModifiers != null)
                    {
                        foreach (var statKvp in def.MaidStatModifiers)
                        {
                            CombatMaid.Core.AttributeModifiers.AttributeModifier.ModifyByDelta(
                                maid.MaidCharacter,
                                statKvp.Key,
                                statKvp.Value
                            );
                            
                        }
                    }

                    // 2. 应用技能 (Abilities)
                    if (!string.IsNullOrEmpty(def.MaidAbilityID))
                    {
                        string skillId = def.MaidAbilityID;
                        var skillSystem = maid.SkillSystem;

                        // [安全检查] 防止重复添加技能
                        // 假设 SkillSystem 没有公开的 HasSkill 方法，我们通过反射或者遍历检查
                        // 既然你在 MaidSkillComponent 里有 List<IMaidSkill> _skills
                        // 我们最好在 MaidSkillComponent 加一个 HasSkill 方法，或者在这里做一个简单的判断

                        // 为了简化，这里假设 Factory 创建技能是轻量级的
                        // 我们构建配置，尝试添加
                        var skillConfig = new MaidSkillConfig { SkillID = skillId };
                        var newSkill = CombatMaid.Core.MaidSkillSystem.MaidSkillFactory.CreateSkill(skillConfig);

                        if (newSkill != null)
                        {
                            // 你需要修改 MaidSkillComponent.AddSkill 内部增加 if(HasSkill) return; 
                            // 或者在这里依赖 SkillSystem 自身的健壮性
                            skillSystem.AddSkill(newSkill);
                            CMDebug.Log($" -> 激活技能: {skillId}");
                        }
                    }
                }
            }
            
            maid.MaidCharacter.Health.SetHealth(maid.MaidCharacter.Health.MaxHealth);
        }

        /// <summary>
        /// 查询指定 ID 的技能节点是否已解锁
        /// </summary>
        public bool IsSkillUnlocked(string nodeID)
        {
            if (string.IsNullOrEmpty(nodeID)) return false;

            // 1. 优先检查运行时 Perk 对象 (最准确，包含当前会话刚解锁但未保存的状态)
            if (_runtimePerks != null && _runtimePerks.TryGetValue(nodeID, out Perk perk))
            {
                if (perk != null && perk.Unlocked) return true;
            }

            // 2. 回退检查存档数据
            // (适用于技能树 UI 尚未构建，但数据已加载的情况，例如商店初始化时)
            if (_saveData != null && _saveData.UnlockedNodeIDs != null)
            {
                if (_saveData.UnlockedNodeIDs.Contains(nodeID)) return true;
            }

            // 3. 如果以上都无法确认（例如数据尚未初始化），尝试临时加载防止逻辑错误
            // 注意：这取决于你的加载流程，如果 MaidItemRegistry 运行极早，可能需要这一步
            if (_saveData == null)
            {
                var tempList = SkillTreePersistence.Load();
                if (tempList != null && tempList.UnlockedNodeIDs.Contains(nodeID))
                {
                    // 顺便缓存一下，避免频繁 IO
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
    }
}