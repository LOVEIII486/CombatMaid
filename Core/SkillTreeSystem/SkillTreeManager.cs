using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Duckov.Scenes;
using Duckov.PerkTrees; // 引用 Perk 基础类
using HarmonyLib; // 用于反射读取 Perk 状态

namespace CombatMaid.Core.SkillTreeSystem
{
    public class SkillTreeManager : MonoBehaviour
    {
        public static SkillTreeManager Instance { get; private set; }

        private const string TREE_ID = "MaidCombatSkills";
        private bool _isInitialized = false;

        // 运行时状态缓存
        private SkillTreeSaveData _saveData;
        private Dictionary<string, Perk> _runtimePerks = new Dictionary<string, Perk>();
        
        // [修复 1] 新增缺失的字典定义：用于 ID -> SkillNodeDef 的快速查找
        private Dictionary<string, SkillNodeDef> _nodeDefsMap = new Dictionary<string, SkillNodeDef>();

        private void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            LevelManager.OnAfterLevelInitialized += OnLevelLoaded;
        }

        private void OnDestroy()
        {
            LevelManager.OnAfterLevelInitialized -= OnLevelLoaded;
            SaveProgress(); // 销毁前强制保存
        }

        private void OnLevelLoaded()
        {
            if (MultiSceneCore.MainSceneID != "Base" || _isInitialized) return;

            StartCoroutine(InitSkillTreeRoutine());
        }

        private IEnumerator InitSkillTreeRoutine()
        {
            yield return null; // 等待一帧

            GameObject skillBuilding = FindSkillMachine();
            if (skillBuilding == null)
            {
                CMDebug.LogWarning("[SkillTreeManager] 未找到 SkillMachine 建筑，跳过加载。");
                yield break;
            }

            // 1. 加载存档
            _saveData = SkillTreePersistence.Load();

            // 2. 构建技能树
            BuildSkillTree(skillBuilding);

            // 3. 恢复已购买状态
            RestorePurchasedState();

            // 4. 启动状态监视器 (用于自动保存)
            StartCoroutine(StateWatcherRoutine());

            _isInitialized = true;
        }

        private GameObject FindSkillMachine()
        {
            // 1. 尝试全局查找
            GameObject obj = GameObject.Find("SkillMachine");
            if (obj != null) return obj;

            // 2. 尝试在当前子场景中查找 (修复 IsCreated -> HasValue)
            if (MultiSceneCore.ActiveSubScene.HasValue)
            {
                // 获取可空类型的实际值 (.Value)
                var scene = MultiSceneCore.ActiveSubScene.Value;

                // 确保场景有效再遍历
                if (scene.IsValid())
                {
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        // Duckov 的建筑通常挂在名为 "Buildings" 的根节点下
                        if (root.name == "Buildings")
                            return root.transform.Find("SkillMachine")?.gameObject;
                    }
                }
            }

            return null;
        }

        private void BuildSkillTree(GameObject building)
        {
            CMDebug.Log("[SkillTreeManager] 开始构建技能树...");
            _runtimePerks.Clear();
            _nodeDefsMap.Clear(); // [修复 2] 清理旧数据

            var tree = SkillTreeBuilder.CreateEmptyTree(TREE_ID, "女仆战术");
            var nodes = GetNodeDefinitions();

            // 构建实体
            foreach (var nodeDef in nodes)
            {
                // [修复 3] 填充字典
                if (!_nodeDefsMap.ContainsKey(nodeDef.ID))
                {
                    _nodeDefsMap.Add(nodeDef.ID, nodeDef);
                }

                // 图标加载逻辑
                string iconName = !string.IsNullOrEmpty(nodeDef.IconFileName) 
                    ? nodeDef.IconFileName 
                    : "default_icon.png";
                nodeDef.Icon = SkillIconLoader.LoadIcon(iconName);

                var perk = SkillTreeBuilder.AddNodeToTree(tree, nodeDef);
                if (perk != null)
                {
                    _runtimePerks.Add(nodeDef.ID, perk);
                }
            }

            SkillTreeBuilder.RebuildGraphConnections(tree, nodes, _runtimePerks);
            SkillTreeBuilder.RegisterInteraction(building, TREE_ID, "战斗女仆模组: 战术技能");
        }

        private List<SkillNodeDef> GetNodeDefinitions()
        {
            // 这里建议未来移到独立的 Config 文件或类中
            return new List<SkillNodeDef>
            {
                new SkillNodeDef
                {
                    ID = "maid_basic_train",
                    DisplayName = "女仆基础训练",
                    Description = "增加 50 点最大生命值。",
                    Position = new Vector2(0, 0),
                    CostMoney = 100,
                    StatModifiers = new Dictionary<string, float> { { "health", 50f } }
                },
                new SkillNodeDef
                {
                    ID = "maid_reload",
                    DisplayName = "极速换弹",
                    Description = "换弹速度提升 20%。",
                    Position = new Vector2(150, 0),
                    CostMoney = 500,
                    RequiredLevel = 2,
                    StatModifiers = new Dictionary<string, float> { { "reload_speed", 0.2f } },
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
                
                // 检查是否解锁
                bool isUnlocked = _saveData.UnlockedNodeIDs.Contains(id); 

                if (isUnlocked)
                {
                    // 现在 _nodeDefsMap 已经存在并被填充了
                    if (_nodeDefsMap.TryGetValue(id, out SkillNodeDef def))
                    {
                        // 1. 应用属性
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

                        // 2. 应用技能 (如果有)
                        // 注意：你需要确保 MaidManager 有处理 AbilityID 的逻辑，或者在这里直接处理
                        if (!string.IsNullOrEmpty(def.MaidAbilityID)) 
                        {
                            // 示例逻辑：如果 abilityID 是 "Grenade"，则添加手雷技能
                            // 这里只是示例，具体取决于你的 AbilityID 怎么定义
                            // maid.SkillSystem.UnlockAbility(def.MaidAbilityID);
                        }
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
                    // 使用反射强制设置 unlocked 状态 (Perk 类的 unlocked 字段通常是 private/protected)
                    Traverse.Create(perk).Field("unlocked").SetValue(true);
                    // 如果原版逻辑需要 ApplyStats，这里可能需要手动触发一次，或者依赖原版加载逻辑
                    // 通常 PerkTree 只有在购买时才 Apply。如果是加载存档，你可能需要手动 Apply 属性加成。
                }
            }
        }

        private IEnumerator StateWatcherRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(1.0f); // 每秒检查一次
                CheckAndSave();
            }
        }

        private void CheckAndSave()
        {
            if (_saveData == null) return;

            bool isDirty = false;
            foreach (var kvp in _runtimePerks)
            {
                string id = kvp.Key;
                Perk perk = kvp.Value;

                // 检查是否已购买/解锁
                bool isUnlocked = Traverse.Create(perk).Field("unlocked").GetValue<bool>();

                if (isUnlocked && !_saveData.UnlockedNodeIDs.Contains(id))
                {
                    _saveData.UnlockedNodeIDs.Add(id);
                    isDirty = true;
                }
            }

            if (isDirty)
            {
                SaveProgress();
            }
        }

        public void SaveProgress()
        {
            if (_saveData != null)
            {
                SkillTreePersistence.Save(_saveData);
            }
        }
    }
}