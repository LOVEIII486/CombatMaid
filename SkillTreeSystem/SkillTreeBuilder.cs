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
        public static Perk AddNodeToTree(PerkTree tree, SkillNodeDef def)
        {
            if (tree == null || def == null) return null;

            // 1. 创建节点 GameObject
            GameObject nodeObj = new GameObject($"Perk_{def.ID}");
            nodeObj.transform.SetParent(tree.transform);
            nodeObj.transform.localPosition = Vector3.zero;

            // 2. 添加属性修改器 (ModifyCharacterStatsBase)
            var statsComp = nodeObj.AddComponent<ModifyCharacterStatsBase>();
            var entries = new List<ModifyCharacterStatsBase.Entry>();
            
            foreach (var kvp in def.StatModifiers)
            {
                entries.Add(new ModifyCharacterStatsBase.Entry
                {
                    key = kvp.Key,
                    value = kvp.Value,
                    percentage = false 
                });
            }
            Traverse.Create(statsComp).Field("entries").SetValue(entries);

            // 3. 配置 Perk 组件
            Perk perk = nodeObj.AddComponent<Perk>(); 
            Traverse tPerk = Traverse.Create(perk);
            
            tPerk.Field("displayName").SetValue(def.DisplayName);
            tPerk.Field("master").SetValue(tree);
            tPerk.Field("icon").SetValue(def.Icon);
            
            // 4. 设置需求 (Cost)
            PerkRequirement req = new PerkRequirement();
            req.level = def.RequiredLevel;
            req.cost = new Cost { money = def.CostMoney };
            
            if (def.CostItems.Count > 0)
            {
                req.cost.items = def.CostItems.Select(x => new Cost.ItemEntry { id = x.Key, amount = x.Value }).ToArray();
            }
            else
            {
                req.cost.items = new Cost.ItemEntry[0];
            }
            tPerk.Field("requirement").SetValue(req);

            // 5. 将节点注册到树的列表和图数据中
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
        public static void RebuildGraphConnections(PerkTree tree, List<SkillNodeDef> allDefs, Dictionary<string, Perk> createdPerks)
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
        /// 在建筑上添加交互点
        /// </summary>
        public static void RegisterInteraction(GameObject buildingObj, string treeId, string interactionLabel)
        {
            var existingInvoker = buildingObj.GetComponentInChildren<PerkTreeUIInvoker>();
            if (existingInvoker == null)
            {
                Debug.LogError("[SkillTreeSystem] 目标建筑没有 PerkTreeUIInvoker，无法挂载交互。");
                return;
            }

            GameObject interactObj = new GameObject($"Interact_{treeId}");
            interactObj.transform.SetParent(existingInvoker.transform.parent);
            interactObj.transform.localPosition = Vector3.zero;

            PerkTreeUIInvoker newInvoker = interactObj.AddComponent<PerkTreeUIInvoker>();
            newInvoker.InteractName = interactionLabel;
            newInvoker.perkTreeID = treeId;
            newInvoker.MarkerActive = false;

            var groupList = Traverse.Create(existingInvoker).Field("otherInterablesInGroup").GetValue<List<InteractableBase>>();
            if (groupList != null)
            {
                groupList.Add(newInvoker);
            }
            
            existingInvoker.GetInteractableList();
        }
    }
}