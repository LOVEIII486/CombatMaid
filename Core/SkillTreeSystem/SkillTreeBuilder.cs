using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using Duckov.PerkTrees;
using Duckov.PerkTrees.Behaviours;
using Duckov.PerkTrees.Interactable;
using Duckov.Economy;
using NodeCanvas.Framework; // [新增] 引用 NodeCanvas 基础框架以支持 Graph 操作

namespace CombatMaid.Core.SkillTreeSystem
{
    public static class SkillTreeBuilder
    {
        /// <summary>
        /// 创建一个新的空技能树实例
        /// </summary>
        public static PerkTree CreateEmptyTree(string treeId, string treeName)
        {
            // 1. 获取原版 "Skills" 树作为模板
            PerkTree template = PerkTreeManager.GetPerkTree("Skills");
            if (template == null)
            {
                Debug.LogError("[SkillTreeSystem] 无法找到原版技能树模板！");
                return null;
            }

            // 2. 实例化副本并清理子物体
            GameObject treeObj = Object.Instantiate(template.gameObject);
            treeObj.name = $"CustomSkillTree_{treeId}";
            treeObj.SetActive(false); // 先隐藏进行修改

            // 清理原有的所有节点子物体
            foreach (Transform child in treeObj.transform)
            {
                Object.Destroy(child.gameObject);
            }

            // 3. 重置 PerkTree 组件数据
            PerkTree perkTree = treeObj.GetComponent<PerkTree>();
            Traverse tTree = Traverse.Create(perkTree);

            tTree.Field("perkTreeID").SetValue(treeId);
            tTree.Field("perks").SetValue(new List<Perk>()); // 清空引用列表

            // 4. 清理关系图 (Graph)
            if (perkTree.RelationGraphOwner != null && perkTree.RelationGraphOwner.graph is PerkRelationGraph graph)
            {
                graph.allNodes.Clear();
                graph.GetGraphSource().connections.Clear();
                graph.UpdateGraph();
            }

            // 注册到全局管理器 (防止被GC或无法查找)
            if (!PerkTreeManager.Instance.perkTrees.Contains(perkTree))
            {
                PerkTreeManager.Instance.perkTrees.Add(perkTree);
            }

            treeObj.SetActive(true);
            return perkTree;
        }

        /// <summary>
        /// 向技能树添加一个节点
        /// </summary>
        /// <summary>
        /// 向技能树添加一个节点（完整修复版）
        /// </summary>
        public static Perk AddNodeToTree(PerkTree tree, SkillNodeDef def)
        {
            if (tree == null || def == null) return null;

            // 1. 创建节点物体
            GameObject nodeObj = new GameObject($"Perk_{def.ID}");
            nodeObj.transform.SetParent(tree.transform);
            nodeObj.transform.localPosition = Vector3.zero;

            // ============================================================
            // 2. 挂载自动存档组件 (所有节点必备)
            // ============================================================
            nodeObj.AddComponent<PerkAutoSaveBehaviour>();

            // ============================================================
            // 3. 挂载玩家属性加成 (如果有配置)
            // ============================================================
            if (def.PlayerStatModifiers != null && def.PlayerStatModifiers.Count > 0)
            {
                var statsComp = nodeObj.AddComponent<SilentModifyCharacterStats>();
                var entries = new List<ModifyCharacterStatsBase.Entry>();
                foreach (var kvp in def.PlayerStatModifiers)
                {
                    entries.Add(new ModifyCharacterStatsBase.Entry
                        { key = kvp.Key, value = kvp.Value, percentage = false });
                }

                // 使用反射写入 private 字段
                Traverse.Create(statsComp).Field("entries").SetValue(entries);
            }

            // ============================================================
            // 4. 挂载女仆技能逻辑 (如果有配置)
            // ============================================================
            if ((def.MaidStatModifiers != null && def.MaidStatModifiers.Count > 0) ||
                !string.IsNullOrEmpty(def.MaidAbilityID))
            {
                var maidBeh = nodeObj.AddComponent<MaidSkillGrantBehaviour>();
                // 注入数据
                maidBeh.SkillID = def.ID;
                maidBeh.MaidStatModifiers = def.MaidStatModifiers;
                maidBeh.UnlockAbilityID = def.MaidAbilityID;
            }

            // ============================================================
            // 5. 标准 Perk 初始化逻辑
            // ============================================================
            Perk perk = nodeObj.AddComponent<Perk>();
            Traverse tPerk = Traverse.Create(perk);

            tPerk.Field("displayName").SetValue(def.DisplayName);
            tPerk.Field("master").SetValue(tree);
            tPerk.Field("icon").SetValue(def.Icon);

            // 设置需求 (Cost)
            PerkRequirement req = new PerkRequirement();
            req.level = def.RequiredLevel;
            req.cost = new Cost { money = def.CostMoney };

            if (def.CostItems != null && def.CostItems.Count > 0)
            {
                req.cost.items = def.CostItems.Select(x => new Cost.ItemEntry { id = x.Key, amount = x.Value })
                    .ToArray();
            }
            else
            {
                req.cost.items = new Cost.ItemEntry[0];
            }

            tPerk.Field("requirement").SetValue(req);

            // ============================================================
            // [核心修复] 强制刷新 Perk 与 Behaviours 的连接
            // 解决 "OnUnlocked" 不触发导致无法自动保存的问题
            // ============================================================
            // 1. 获取该节点上挂载的所有行为组件
            var allBehaviours = nodeObj.GetComponents<PerkBehaviour>().ToList();

            // 2. 利用反射强制将这些行为注入到 Perk 的私有列表 'behaviours' 中
            tPerk.Field("behaviours").SetValue(allBehaviours);

            // 3. (双向绑定) 确保每个行为都知道自己的主人是这个 perk
            foreach (var beh in allBehaviours)
            {
                Traverse.Create(beh).Field("perk").SetValue(perk);
            }

            // ============================================================
            // 6. 将节点注册到树的列表和图数据中
            // ============================================================
            Traverse.Create(tree).Field("perks").GetValue<List<Perk>>().Add(perk);

            // 更新图节点位置
            if (tree.RelationGraphOwner.graph is PerkRelationGraph graph)
            {
                PerkRelationNode graphNode = new PerkRelationNode();
                graphNode.relatedNode = perk;
                graphNode.cachedPosition = def.Position;
                graph.allNodes.Add(graphNode);
            }

            return perk;
        }

        /// <summary>
        /// 重建图连接 (必须在所有节点添加完毕后调用)
        /// [已修复] 使用 graph.GetRelatedNode 替代手动遍历，解决编译错误
        /// </summary>
        public static void RebuildGraphConnections(PerkTree tree, List<SkillNodeDef> allDefs,
            Dictionary<string, Perk> createdPerks)
        {
            if (tree.RelationGraphOwner.graph is PerkRelationGraph graph)
            {
                foreach (var def in allDefs)
                {
                    if (def.PrerequisiteIDs == null) continue;
                    if (!createdPerks.ContainsKey(def.ID)) continue;

                    // [修复点] 直接使用官方提供的 GetRelatedNode 方法
                    // 这避免了 graph.allNodes 返回基础 Node 类型无法访问 relatedNode 的问题
                    var targetNode = graph.GetRelatedNode(createdPerks[def.ID]);

                    if (targetNode == null) continue;

                    foreach (var preId in def.PrerequisiteIDs)
                    {
                        if (createdPerks.ContainsKey(preId))
                        {
                            var sourceNode = graph.GetRelatedNode(createdPerks[preId]);

                            // 确保两个节点都有效，且尚未连接
                            if (sourceNode != null && sourceNode != targetNode)
                            {
                                // 检查是否已经存在连接，避免重复连线
                                bool alreadyConnected = false;
                                foreach (var conn in sourceNode.outConnections)
                                {
                                    if (conn.targetNode == targetNode)
                                    {
                                        alreadyConnected = true;
                                        break;
                                    }
                                }

                                if (!alreadyConnected)
                                {
                                    graph.ConnectNodes(sourceNode, targetNode);
                                }
                            }
                        }
                    }
                }

                graph.UpdateGraph();
            }
        }

        /// <summary>
        /// 在建筑上添加交互点 (终极修复版：位置重合 + 移除碰撞)
        /// </summary>
        public static void RegisterInteraction(GameObject buildingObj, string treeId, string interactionLabel)
        {
            var existingInvoker = buildingObj.GetComponentInChildren<PerkTreeUIInvoker>();
            if (existingInvoker == null)
            {
                CMDebug.LogError("[SkillTreeSystem] 目标建筑没有 PerkTreeUIInvoker，无法挂载交互。");
                return;
            }

            // 1. 克隆现有的交互点
            // 使用现有点的父级作为父级，这样 Instantiate 会自动保持相对位置一致
            GameObject interactObj = Object.Instantiate(existingInvoker.gameObject, existingInvoker.transform.parent);
            interactObj.name = $"Interact_{treeId}";

            // [修复 1] 显式对齐位置和旋转 (虽然 Instantiate 默认会保持，但为了保险起见)
            interactObj.transform.localPosition = existingInvoker.transform.localPosition;
            interactObj.transform.localRotation = existingInvoker.transform.localRotation;
            interactObj.transform.localScale = existingInvoker.transform.localScale;

            // [修复 2] 移除克隆体上的碰撞体 (Collider)
            // 这样玩家就无法直接对着这个新点按 F，只能通过原版点的菜单访问它
            // 避免了“出现两个互动点”的尴尬
            var collider = interactObj.GetComponent<Collider>();
            if (collider != null)
            {
                Object.Destroy(collider);
            }

            // 2. 配置组件
            PerkTreeUIInvoker newInvoker = interactObj.GetComponent<PerkTreeUIInvoker>();
            newInvoker.InteractName = interactionLabel;
            newInvoker.perkTreeID = treeId;
            newInvoker.MarkerActive = false; // 不显示头顶图标

            // 3. 清理克隆体的组列表 (防止嵌套死循环)
            var newInvokerGroupList = Traverse.Create(newInvoker).Field("otherInterablesInGroup")
                .GetValue<List<InteractableBase>>();
            if (newInvokerGroupList != null)
            {
                newInvokerGroupList.Clear();
            }

            // 4. 将新点注册到【原版】交互点的组里
            var mainGroupList = Traverse.Create(existingInvoker).Field("otherInterablesInGroup")
                .GetValue<List<InteractableBase>>();
            if (mainGroupList != null)
            {
                mainGroupList.Add(newInvoker);
            }

            // 5. 刷新原版交互列表 UI
            existingInvoker.GetInteractableList();

            CMDebug.Log($"[SkillTreeSystem] 交互点已完美挂载: {interactionLabel}");
        }
    }
}