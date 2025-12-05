using System.Collections;
using System.Collections.Generic;
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
        private bool _isTreeBuilt = false;
        private PerkTree _customTree;
        private bool _isInitializing = false;  // [新增] 防止协程重复执行

        // 运行时状态缓存
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
            // [修复] 监听所有场景加载事件
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnAnySceneLoaded;
        }

        private void OnDestroy()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnAnySceneLoaded;
            SaveProgress();
        }

        // [新增] 响应任何场景加载
        private void OnAnySceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            // 只在 Base 或 Base_SceneV2 场景时尝试注册
            if (scene.name == "Base" || scene.name == "Base_SceneV2")
            {
                CMDebug.Log($"[SkillTreeManager] 检测到基地场景: {scene.name}");
                StartCoroutine(InitSkillTreeRoutine());
            }
        }

        private IEnumerator InitSkillTreeRoutine()
        {
            // [保护] 防止协程重复执行
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

                // [修复] 状态一致性检查
                if (!_isTreeBuilt || _customTree == null)
                {
                    CMDebug.Log("[SkillTreeManager] 开始构建技能树（首次或修复损坏状态）");
                    
                    try
                    {
                        // 1. 加载存档
                        _saveData = SkillTreePersistence.Load();

                        // 2. 构建技能树
                        BuildSkillTree();

                        // 3. 验证构建结果
                        if (_customTree == null)
                        {
                            CMDebug.LogError("[SkillTreeManager] ✗ BuildSkillTree 失败：_customTree 仍为 null");
                            _isTreeBuilt = false; // 重置状态，下次重试
                            yield break;
                        }

                        // 4. 恢复已购买状态
                        RestorePurchasedState();

                        _isTreeBuilt = true;
                        CMDebug.LogInfo("[SkillTreeManager] ✓ 技能树构建成功");
                    }
                    catch (System.Exception ex)
                    {
                        CMDebug.LogError($"[SkillTreeManager] 构建技能树时发生异常: {ex.Message}\n{ex.StackTrace}");
                        _isTreeBuilt = false; // 重置状态
                        _customTree = null;
                        yield break;
                    }
                }
                else
                {
                    CMDebug.Log("[SkillTreeManager] 技能树已存在，跳过构建");
                }

                // [关键修复] 每次进入场景都重新注册交互点
                if (_customTree != null)
                {
                    CMDebug.Log($"[SkillTreeManager] 准备注册交互点到建筑: {skillBuilding.name}");
                    SkillTreeBuilder.RegisterInteraction(skillBuilding, TREE_ID, "战斗女仆模组: 战术技能");
                    CMDebug.LogInfo("[SkillTreeManager] ✓ 交互点已重新注册");
                }
                else
                {
                    CMDebug.LogError("[SkillTreeManager] ✗ _customTree 为 null，无法注册交互点！");
                    CMDebug.LogError($"[SkillTreeManager] 状态异常：_isTreeBuilt={_isTreeBuilt}, _customTree={_customTree}");
                }
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
            CMDebug.Log("[SkillTreeManager] 开始构建技能树...");
            _runtimePerks.Clear();
            _nodeDefsMap.Clear();

            try
            {
                // 1. 创建树
                _customTree = SkillTreeBuilder.CreateEmptyTree(TREE_ID, "女仆战术");
                
                if (_customTree == null)
                {
                    CMDebug.LogError("[BuildSkillTree] CreateEmptyTree 返回 null！");
                    return;
                }
                
                CMDebug.Log($"[BuildSkillTree] 技能树已创建: {_customTree.name}");

                // 2. 获取节点定义
                var nodes = GetNodeDefinitions();
                CMDebug.Log($"[BuildSkillTree] 加载了 {nodes.Count} 个节点定义");

                // 3. 添加节点
                foreach (var nodeDef in nodes)
                {
                    if (!_nodeDefsMap.ContainsKey(nodeDef.ID))
                    {
                        _nodeDefsMap.Add(nodeDef.ID, nodeDef);
                    }

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

                // 4. 重建连接
                SkillTreeBuilder.RebuildGraphConnections(_customTree, nodes, _runtimePerks);
                
                CMDebug.LogInfo($"[BuildSkillTree] ✓ 技能树构建完成，共 {_runtimePerks.Count} 个节点");
            }
            catch (System.Exception ex)
            {
                CMDebug.LogError($"[BuildSkillTree] 构建过程中发生异常: {ex.Message}\n{ex.StackTrace}");
                _customTree = null; // 确保状态一致
                throw; // 重新抛出让上层处理
            }
        }

        private List<SkillNodeDef> GetNodeDefinitions()
        {
            return new List<SkillNodeDef>
            {
                new SkillNodeDef
                {
                    ID = "maid_basic_train",
                    DisplayName = "女仆基础训练",
                    Description = "增加 50 点最大生命值。",
                    Position = new Vector2(0, 0),
                    CostMoney = 100,
                    PlayerStatModifiers = new Dictionary<string, float> { { "MaxHealth", 50f } }
                },
                new SkillNodeDef
                {
                    ID = "maid_reload",
                    DisplayName = "极速换弹",
                    Description = "换弹速度提升 20%。",
                    Position = new Vector2(150, 0),
                    CostMoney = 500,
                    RequiredLevel = 2,
                    PlayerStatModifiers = new Dictionary<string, float> { { "ReloadSpeedGain", 0.2f } },
                    PrerequisiteIDs = new List<string> { "maid_basic_train" }
                }
            };
        }
        
        public void ApplyPassiveEffectsToMaid(MaidController maid)
        {
            if (_saveData == null) return;

            foreach (var kvp in _runtimePerks)
            {
                string id = kvp.Key;
                Perk perk = kvp.Value;
                
                // 使用公共属性而不是反射
                bool isUnlocked = perk != null && perk.Unlocked;

                if (isUnlocked && _nodeDefsMap.TryGetValue(id, out SkillNodeDef def))
                {
                    if (def.MaidStatModifiers != null && def.MaidStatModifiers.Count > 0) 
                    {
                        foreach (var statKvp in def.MaidStatModifiers)
                        {
                            CombatMaid.Core.AttributeModifiers.AttributeModifier.Modify(
                                maid.MaidCharacter, 
                                statKvp.Key, 
                                statKvp.Value, 
                                false
                            );
                        }
                    }

                    if (!string.IsNullOrEmpty(def.MaidAbilityID)) 
                    {
                        // 处理技能解锁逻辑
                    }
                }
            }
        }

        private void RestorePurchasedState()
        {
            if (_saveData == null) return;

            foreach (var id in _saveData.UnlockedNodeIDs)
            {
                if (_runtimePerks.TryGetValue(id, out Perk perk))
                {
                    // 使用反射强制设置 unlocked 状态
                    Traverse.Create(perk).Property("Unlocked").SetValue(true);
                    CMDebug.Log($"[RestorePurchasedState] 已恢复节点: {id}");
                }
            }
        }

        // ============================================================
        // [核心修复] 使用公共属性而不是私有字段
        // ============================================================
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

                    // [修复点] 使用 Perk.Unlocked 公共属性（首字母大写）
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