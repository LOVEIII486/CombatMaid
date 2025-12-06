using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using Duckov.PerkTrees;
using Duckov.PerkTrees.Behaviours;
using Duckov.PerkTrees.Interactable;
using Duckov.Economy;
using NodeCanvas.Framework;

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
            treeObj.SetActive(false);

            foreach (Transform child in treeObj.transform)
            {
                Object.Destroy(child.gameObject);
            }

            // 3. 重置 PerkTree 组件数据
            PerkTree perkTree = treeObj.GetComponent<PerkTree>();
            Traverse tTree = Traverse.Create(perkTree);

            tTree.Field("perkTreeID").SetValue(treeId);
            tTree.Field("perks").SetValue(new List<Perk>());

            // 4. 清理关系图
            if (perkTree.RelationGraphOwner != null && perkTree.RelationGraphOwner.graph is PerkRelationGraph graph)
            {
                graph.allNodes.Clear();
                graph.GetGraphSource().connections.Clear();
                graph.UpdateGraph();
            }

            if (!PerkTreeManager.Instance.perkTrees.Contains(perkTree))
            {
                PerkTreeManager.Instance.perkTrees.Add(perkTree);
            }

            treeObj.SetActive(true);
            return perkTree;
        }

        /// <summary>
        /// 向技能树添加一个节点（修复版：正确的组件注册顺序）
        /// </summary>
        public static Perk AddNodeToTree(PerkTree tree, SkillNodeDef def)
        {
            if (tree == null || def == null) return null;

            // 1. 创建节点物体
            GameObject nodeObj = new GameObject($"Perk_{def.ID}");
            nodeObj.transform.SetParent(tree.transform);
            nodeObj.transform.localPosition = Vector3.zero;

            // ============================================================
            // 2. [关键修复] 必须先添加 Perk，再添加任何 Behaviour
            //    这样游戏引擎才能正确建立 Perk -> Behaviour 的连接
            // ============================================================
            Perk perk = nodeObj.AddComponent<Perk>();
            perk.name = $"Perk_{def.ID}"; // 设置名称
            
            Traverse tPerk = Traverse.Create(perk);
            tPerk.Field("displayName").SetValue(def.DisplayName);
            tPerk.Field("master").SetValue(tree);
            tPerk.Field("icon").SetValue(def.Icon);

            // 设置需求
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
            // 3. Perk 配置完成后，才开始添加 Behaviour 组件
            // ============================================================
            
            // 自动存档组件（所有节点必备）
            nodeObj.AddComponent<PerkAutoSaveBehaviour>();

            // 玩家属性加成（如果有配置）
            if (def.PlayerStatModifiers != null && def.PlayerStatModifiers.Count > 0)
            {
                var statsComp = nodeObj.AddComponent<ModifyPlayerCharacterStats>();
                var entries = new List<ModifyCharacterStatsBase.Entry>();
                foreach (var kvp in def.PlayerStatModifiers)
                {
                    entries.Add(new ModifyCharacterStatsBase.Entry
                        { key = kvp.Key, value = kvp.Value, percentage = false });
                }

                Traverse.Create(statsComp).Field("entries").SetValue(entries);
            }

            // 女仆技能逻辑（如果有配置）
            if ((def.MaidStatModifiers != null && def.MaidStatModifiers.Count > 0) ||
                !string.IsNullOrEmpty(def.MaidAbilityID))
            {
                var maidBeh = nodeObj.AddComponent<MaidSkillGrantBehaviour>();
                maidBeh.SkillID = def.ID;
                maidBeh.MaidStatModifiers = def.MaidStatModifiers;
                maidBeh.UnlockAbilityID = def.MaidAbilityID;
            }

            // ============================================================
            // 4. 验证 Behaviour 注册情况（调试用）
            // ============================================================
            var allBehaviours = nodeObj.GetComponents<PerkBehaviour>().ToList();
            CMDebug.Log($"[SkillTreeBuilder] 节点 {def.ID} 已注册 {allBehaviours.Count} 个 Behaviour");

            // ============================================================
            // 5. 将节点注册到树的列表和图数据中
            // ============================================================
            Traverse.Create(tree).Field("perks").GetValue<List<Perk>>().Add(perk);

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
        /// 重建图连接（修复版）
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

                    var targetNode = graph.GetRelatedNode(createdPerks[def.ID]);
                    if (targetNode == null) continue;

                    foreach (var preId in def.PrerequisiteIDs)
                    {
                        if (createdPerks.ContainsKey(preId))
                        {
                            var sourceNode = graph.GetRelatedNode(createdPerks[preId]);

                            if (sourceNode != null && sourceNode != targetNode)
                            {
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
        /// 在建筑上添加交互点（终极修复版）
        /// </summary>
        public static void RegisterInteraction(GameObject buildingObj, string treeId, string interactionLabel)
        {
            var existingInvoker = buildingObj.GetComponentInChildren<PerkTreeUIInvoker>();
            if (existingInvoker == null)
            {
                CMDebug.LogError("[SkillTreeSystem] 目标建筑没有 PerkTreeUIInvoker，无法挂载交互。");
                return;
            }

            GameObject interactObj = Object.Instantiate(existingInvoker.gameObject, existingInvoker.transform.parent);
            interactObj.name = $"Interact_{treeId}";

            interactObj.transform.localPosition = existingInvoker.transform.localPosition;
            interactObj.transform.localRotation = existingInvoker.transform.localRotation;
            interactObj.transform.localScale = existingInvoker.transform.localScale;

            var collider = interactObj.GetComponent<Collider>();
            if (collider != null)
            {
                Object.Destroy(collider);
            }

            PerkTreeUIInvoker newInvoker = interactObj.GetComponent<PerkTreeUIInvoker>();
            newInvoker.InteractName = interactionLabel;
            newInvoker.perkTreeID = treeId;
            newInvoker.MarkerActive = false;

            var newInvokerGroupList = Traverse.Create(newInvoker).Field("otherInterablesInGroup")
                .GetValue<List<InteractableBase>>();
            if (newInvokerGroupList != null)
            {
                newInvokerGroupList.Clear();
            }

            var mainGroupList = Traverse.Create(existingInvoker).Field("otherInterablesInGroup")
                .GetValue<List<InteractableBase>>();
            if (mainGroupList != null)
            {
                mainGroupList.Add(newInvoker);
            }

            existingInvoker.GetInteractableList();

            CMDebug.Log($"[SkillTreeSystem] 交互点已完美挂载: {interactionLabel}");
        }
    }
}