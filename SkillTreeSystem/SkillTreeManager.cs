using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Duckov.Scenes;

namespace CombatMaid.Core.SkillTreeSystem
{
    public class SkillTreeManager : MonoBehaviour
    {
        public static SkillTreeManager Instance { get; private set; }

        private bool _isInitialized = false;
        private const string TREE_ID = "MaidCombatSkills";

        private void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            // 监听关卡加载事件
            LevelManager.OnAfterLevelInitialized += OnLevelLoaded;
        }

        private void OnDestroy()
        {
            LevelManager.OnAfterLevelInitialized -= OnLevelLoaded;
        }

        private void OnLevelLoaded()
        {
            // 仅在主场景（Base）加载，且尚未初始化时执行
            if (MultiSceneCore.MainSceneID == "Base")
            {
                StartCoroutine(InitSkillTreeRoutine());
            }
        }

        private IEnumerator InitSkillTreeRoutine()
        {
            // 等待一帧以确保建筑已生成
            yield return null;

            // 1. 寻找技能机器建筑 (SkillMachine)
            GameObject skillBuilding = GameObject.Find("SkillMachine");
            if (skillBuilding == null)
            {
                // 尝试通过标签或层级查找的备用方案（如果名字不对）
                var rootObjs = MultiSceneCore.ActiveSubScene.Value.GetRootGameObjects();
                foreach (var root in rootObjs)
                {
                    if (root.name == "Buildings")
                    {
                        skillBuilding = root.transform.Find("SkillMachine")?.gameObject;
                        break;
                    }
                }
            }

            if (skillBuilding == null)
            {
                Debug.LogWarning("[SkillTreeManager] 未找到 SkillMachine 建筑，跳过技能树加载。");
                yield break;
            }

            CreateTestSkillTree(skillBuilding);
            _isInitialized = true;
        }

        private void CreateTestSkillTree(GameObject building)
        {
            Debug.Log("[SkillTreeManager] 开始构建女仆技能树...");

            // 1. 创建树
            var tree = SkillTreeBuilder.CreateEmptyTree(TREE_ID, "女仆战术");

            // 2. 定义节点
            var nodes = new List<SkillNodeDef>();

            // 节点 A: 基础训练 (根节点)
            var nodeRoot = new SkillNodeDef
            {
                ID = "maid_basic_train",
                DisplayName = "女仆基础训练",
                Description = "增加 50 点最大生命值。",
                Position = new Vector2(0, 0),
                CostMoney = 100,
                StatModifiers = new Dictionary<string, float> { { "health", 50f } }
            };
            nodes.Add(nodeRoot);

            // 节点 B: 战术换弹 (子节点)
            var nodeReload = new SkillNodeDef
            {
                ID = "maid_reload",
                DisplayName = "极速换弹",
                Description = "换弹速度提升 20%。",
                Position = new Vector2(150, 0), // 向右偏移
                CostMoney = 500,
                RequiredLevel = 2,
                StatModifiers = new Dictionary<string, float> { { "reload_speed", 0.2f } }, // 假设 stat key
                PrerequisiteIDs = new List<string> { "maid_basic_train" }
            };
            nodes.Add(nodeReload);

            // 3. 构建节点实体
            Dictionary<string, Duckov.PerkTrees.Perk> createdPerks = new Dictionary<string, Duckov.PerkTrees.Perk>();
            foreach (var nodeDef in nodes)
            {
                // 注意：这里 Icon 传了 null，实际使用时建议使用 AssetBundle 加载或引用现有 Sprite
                var perk = SkillTreeBuilder.AddNodeToTree(tree, nodeDef);
                if (perk != null)
                {
                    createdPerks.Add(nodeDef.ID, perk);
                }
            }

            // 4. 重建连接线
            SkillTreeBuilder.RebuildGraphConnections(tree, nodes, createdPerks);

            // 5. 挂载交互到建筑
            SkillTreeBuilder.RegisterInteraction(building, TREE_ID, "战斗女仆模组: 战术技能");

            Debug.Log("[SkillTreeManager] 技能树构建完成！");
        }
    }
}