using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using Duckov.PerkTrees;
using Duckov.PerkTrees.Behaviours;
using Duckov.PerkTrees.Interactable;
using Duckov.Economy;
using NodeCanvas.Framework;
using CombatMaid.Localization;
using SodaCraft.Localizations;

namespace CombatMaid.Core.SkillTreeSystem
{
    public static class SkillTreeBuilder
    {
        /// <summary>
        /// 创建一个新的空技能树实例
        /// </summary>
        public static PerkTree CreateEmptyTree(string treeId, string treeNameKey)
        {
            PerkTree template = PerkTreeManager.GetPerkTree("Skills");
            if (template == null)
            {
                CMDebug.LogError("无法找到原版 'Skills' 技能树作为模板！");
                return null;
            }

            // 1. 临时禁用原版树的所有子节点
            for (int i = 0; i < template.transform.childCount; i++)
            {
                template.transform.GetChild(i).gameObject.SetActive(false);
            }

            // 2. 临时禁用原版树本身
            bool wasTemplateActive = template.gameObject.activeSelf;
            template.gameObject.SetActive(false);

            // 3. 克隆原版技能树（此时是禁用状态）
            GameObject treeObj = Object.Instantiate(template.gameObject);
            treeObj.name = $"CustomSkillTree_{treeId}";

            // 4. 立即恢复原版树的状态
            template.gameObject.SetActive(wasTemplateActive);
            for (int j = 0; j < template.transform.childCount; j++)
            {
                template.transform.GetChild(j).gameObject.SetActive(true);
            }

            // 5. 清除克隆体的所有子节点
            var children = treeObj.transform.Cast<Transform>().ToList();
            foreach (var child in children)
            {
                Object.DestroyImmediate(child.gameObject);
            }

            // 6. 重置 PerkTree 组件核心数据
            PerkTree perkTree = treeObj.GetComponent<PerkTree>();
            Traverse tTree = Traverse.Create(perkTree);

            tTree.Field("perkTreeID").SetValue(treeId);
            tTree.Field("perks").SetValue(new List<Perk>());

            // 7. 注入本地化名称
            if (!string.IsNullOrEmpty(treeNameKey))
            {
                string nameText = CombatMaid.Localization.LocalizationManager.GetText(treeNameKey, "未命名技能树");

                if (SodaCraft.Localizations.LocalizationManager.overrideTexts != null)
                {
                    SodaCraft.Localizations.LocalizationManager.overrideTexts[treeNameKey] = nameText;
                    string forcedKey = $"PerkTree_{treeId}";
                    SodaCraft.Localizations.LocalizationManager.overrideTexts[forcedKey] = nameText;
                    CMDebug.Log($"[SkillTreeSystem] 标题注入: {forcedKey} -> {nameText}");
                }

                tTree.Field("displayName").SetValue(treeNameKey);
            }

            // 🔧 核心修复：创建独立的 Graph 实例，避免与原版技能树共享
            if (perkTree.RelationGraphOwner != null)
            {
                var oldGraph = perkTree.RelationGraphOwner.graph;

                if (oldGraph != null)
                {
                    CMDebug.Log($"[SkillTreeBuilder] 原版 Graph 实例 ID: {oldGraph.GetInstanceID()}");

                    // 创建一个全新的 Graph 实例
                    var graphType = oldGraph.GetType();
                    Graph newGraph = ScriptableObject.CreateInstance(graphType) as Graph;

                    if (newGraph != null)
                    {
                        newGraph.name = $"MaidSkillGraph_{treeId}";

                        // 使用反射替换 GraphOwner 的 _graph 字段
                        var graphOwnerTraverse = Traverse.Create(perkTree.RelationGraphOwner);
                        graphOwnerTraverse.Field("_graph").SetValue(newGraph);

                        CMDebug.Log($"[SkillTreeBuilder] ✓ 已创建独立 Graph: {newGraph.GetInstanceID()}");

                        // 如果是 PerkRelationGraph，初始化空数据
                        if (newGraph is PerkRelationGraph perkGraph)
                        {
                            perkGraph.allNodes.Clear();
                            perkGraph.UpdateGraph();
                        }
                    }
                    else
                    {
                        CMDebug.LogError("[SkillTreeBuilder] ✗ 创建 Graph 实例失败！");
                    }
                }
            }
            else
            {
                CMDebug.LogWarning("[SkillTreeBuilder] RelationGraphOwner 为 null");
            }

            // 9. 注册到管理器
            if (!PerkTreeManager.Instance.perkTrees.Contains(perkTree))
            {
                PerkTreeManager.Instance.perkTrees.Add(perkTree);
            }

            // 10. 激活克隆体
            treeObj.SetActive(true);
            return perkTree;
        }

        /// <summary>
        /// 向技能树添加一个节点
        /// </summary>
        public static Perk AddNodeToTree(PerkTree tree, SkillNodeDef def)
        {
            if (tree == null || def == null) return null;

            // 1. 创建节点物体
            GameObject nodeObj = new GameObject($"Perk_{def.ID}");
            nodeObj.transform.SetParent(tree.transform);
            nodeObj.transform.localPosition = Vector3.zero;

            // 2. 配置 Perk 基础属性
            Perk perk = nodeObj.AddComponent<Perk>();
            perk.name = $"Perk_{def.ID}";

            Traverse tPerk = Traverse.Create(perk);

            tPerk.Field("displayName").SetValue(def.DisplayName);
            string constructedDescKey = def.DisplayName + "_Desc";
            string lookupKey = !string.IsNullOrEmpty(def.Description) ? def.Description : constructedDescKey;
            string finalDescText = CombatMaid.Localization.LocalizationManager.GetText(lookupKey, "暂无描述");

            // 注入描述
            if (SodaCraft.Localizations.LocalizationManager.overrideTexts != null)
            {
                SodaCraft.Localizations.LocalizationManager.overrideTexts[constructedDescKey] = finalDescText;
                string nameText = CombatMaid.Localization.LocalizationManager.GetText(def.DisplayName, def.DisplayName);
                SodaCraft.Localizations.LocalizationManager.overrideTexts[def.DisplayName] = nameText;
            }

            tPerk.Field("hasDescription").SetValue(true);
            tPerk.Field("master").SetValue(tree);
            tPerk.Field("icon").SetValue(def.Icon);

            // 设置需求
            PerkRequirement req = new PerkRequirement();
            req.level = def.RequiredLevel;
            req.cost = new Cost { money = def.CostMoney };
            
            if (def.UnlockTime > 0)
            {
                // 将秒转为 TimeSpan，再提取 Ticks 赋值给字段
                req.requireTime = System.TimeSpan.FromSeconds(def.UnlockTime).Ticks;
            }
            else
            {
                req.requireTime = 0;
            }
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

            // 3. 添加组件行为
            // A. 自动存档组件
            nodeObj.AddComponent<PerkAutoSaveBehaviour>();

            // B. 商店解锁自动绑定
            var unlockableItems = CombatMaid.Core.Items.Logic.MaidItemRegistry.GetItemsUnlockedByNode(def.ID);
            if (unlockableItems != null && unlockableItems.Count > 0)
            {
                foreach (var itemId in unlockableItems)
                {
                    var unlocker = nodeObj.AddComponent<PerkUnlockStockShop>();
                    unlocker.unlockItem = itemId;
                    CMDebug.Log($"[SkillTreeBuilder] 节点 {def.ID} 绑定解锁物品: {itemId}");
                }
            }

            // C. 玩家属性加成
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

            // 🔧 D. 女仆数据修改器（新系统 - 直接使用 MaidModifiers）
            if (def.MaidModifiers != null && def.MaidModifiers.Count > 0)
            {
                var modifyBehaviour = nodeObj.AddComponent<ModifyWineFoxDataBehaviour>();
                modifyBehaviour.NodeID = def.ID;
                modifyBehaviour.Modifiers = def.MaidModifiers; // 直接赋值，无需转换

                CMDebug.Log($"[SkillTreeBuilder] 节点 {def.ID} 配置了 {def.MaidModifiers.Count} 个修改器");
            }

            // 4. 注册到数据结构
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

        public static void RebuildGraphConnections(PerkTree tree, List<SkillNodeDef> allDefs,
            Dictionary<string, Perk> createdPerks)
        {
            if (tree.RelationGraphOwner.graph is PerkRelationGraph graph)
            {
                // 🔧 添加调试日志和验证
                CMDebug.Log($"[RebuildGraph] Graph 实例 ID: {graph.GetInstanceID()}");
                CMDebug.Log($"[RebuildGraph] 当前 allNodes 数量: {graph.allNodes.Count}");
                CMDebug.Log($"[RebuildGraph] 预期节点数量: {createdPerks.Count}");

                // 🔧 修复：如果节点数量不匹配，说明 Graph 被污染，需要重建
                if (graph.allNodes.Count != createdPerks.Count)
                {
                    CMDebug.LogWarning($"[RebuildGraph] ⚠ 检测到节点数量不匹配，重建节点列表");

                    // 清空并重新添加所有节点
                    graph.allNodes.Clear();

                    foreach (var def in allDefs)
                    {
                        if (createdPerks.TryGetValue(def.ID, out Perk perk))
                        {
                            PerkRelationNode graphNode = new PerkRelationNode();
                            graphNode.relatedNode = perk;
                            graphNode.cachedPosition = def.Position;
                            graph.allNodes.Add(graphNode);
                        }
                    }

                    CMDebug.Log($"[RebuildGraph] ✓ 节点列表已重建，当前数量: {graph.allNodes.Count}");
                }

                // 清空旧连接
                graph.GetGraphSource().connections.Clear();

                // 重建连接
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

                                if (!alreadyConnected) graph.ConnectNodes(sourceNode, targetNode);
                            }
                        }
                    }
                }

                graph.UpdateGraph();

                CMDebug.Log($"[RebuildGraph] ✓ 连接重建完成，共 {graph.GetGraphSource().connections.Count} 条连接");
            }
        }

        public static void RegisterInteraction(GameObject buildingObj, string treeId, string interactionKey,
            string defaultText = "交互")
        {
            var existingInvoker = buildingObj.GetComponentInChildren<PerkTreeUIInvoker>();
            if (existingInvoker == null) return;

            string targetName = $"Interact_{treeId}";
            Transform parentTransform = existingInvoker.transform.parent;

            // ================= [修复核心] 清理旧交互点 =================
            // 1. 在同级目录下查找是否已存在同名物体
            Transform oldObj = parentTransform.Find(targetName);

            if (oldObj != null)
            {
                // 2. 从主交互器的分组列表中移除对旧物体的引用 (防止空引用报错)
                var groupList = Traverse.Create(existingInvoker).Field("otherInterablesInGroup")
                    .GetValue<List<InteractableBase>>();
                var oldInvoker = oldObj.GetComponent<PerkTreeUIInvoker>();

                if (groupList != null && oldInvoker != null)
                {
                    groupList.Remove(oldInvoker);
                }

                // 3. 销毁旧物体
                Object.DestroyImmediate(oldObj.gameObject);
                CMDebug.Log($"[SkillTreeBuilder] 检测到重载，已清理旧交互点: {targetName}");
            }
            // ==========================================================

            string finalInteractText = CombatMaid.Localization.LocalizationManager.GetText(interactionKey, defaultText);
            if (SodaCraft.Localizations.LocalizationManager.overrideTexts != null)
            {
                SodaCraft.Localizations.LocalizationManager.overrideTexts[interactionKey] = finalInteractText;
            }

            GameObject interactObj = Object.Instantiate(existingInvoker.gameObject, existingInvoker.transform.parent);
            interactObj.name = targetName; // 使用我们刚才定义的变量

            interactObj.transform.localPosition = existingInvoker.transform.localPosition;
            interactObj.transform.localRotation = existingInvoker.transform.localRotation;
            interactObj.transform.localScale = existingInvoker.transform.localScale;
            var collider = interactObj.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);

            PerkTreeUIInvoker newInvoker = interactObj.GetComponent<PerkTreeUIInvoker>();
            newInvoker.InteractName = interactionKey;
            newInvoker.perkTreeID = treeId;
            newInvoker.MarkerActive = false;

            var newInvokerGroupList = Traverse.Create(newInvoker).Field("otherInterablesInGroup")
                .GetValue<List<InteractableBase>>();
            if (newInvokerGroupList != null) newInvokerGroupList.Clear();

            var mainGroupList = Traverse.Create(existingInvoker).Field("otherInterablesInGroup")
                .GetValue<List<InteractableBase>>();
            if (mainGroupList != null) mainGroupList.Add(newInvoker);
            existingInvoker.GetInteractableList();

            CMDebug.Log($"[SkillTreeSystem] 交互点已挂载: {finalInteractText}");
        }
    }
}