using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using Duckov.PerkTrees;
using Duckov.PerkTrees.Behaviours;
using Duckov.PerkTrees.Interactable;
using Duckov.Economy;
using NodeCanvas.Framework;
using CombatMaid.Localization; // [引用] 模组本地化
using SodaCraft.Localizations; // [引用] 游戏原生本地化

namespace CombatMaid.Core.SkillTreeSystem
{
    public static class SkillTreeBuilder
    {
        /// <summary>
        /// 创建一个新的空技能树实例
        /// 参数 treeNameKey: 本地化 Key (例如 "SkillTree_Maid_Name")
        /// </summary>
        public static PerkTree CreateEmptyTree(string treeId, string treeNameKey)
        {
            PerkTree template = PerkTreeManager.GetPerkTree("Skills");
            if (template == null) return null;

            GameObject treeObj = Object.Instantiate(template.gameObject);
            treeObj.name = $"CustomSkillTree_{treeId}"; // 这里是 GameObject 名，不需要本地化
            treeObj.SetActive(false);

            foreach (Transform child in treeObj.transform) Object.Destroy(child.gameObject);

            PerkTree perkTree = treeObj.GetComponent<PerkTree>();
            Traverse tTree = Traverse.Create(perkTree);

            tTree.Field("perkTreeID").SetValue(treeId); // 内部 ID
            tTree.Field("perks").SetValue(new List<Perk>());

            // [新增] 注入技能树显示名称 (修复了之前 unused parameter 的问题)
            if (!string.IsNullOrEmpty(treeNameKey))
            {
                // 1. 获取翻译文本 (例如从 "SkillTree_Maid_Name" 获取 "女仆战术强化")
                string nameText = CombatMaid.Localization.LocalizationManager.GetText(treeNameKey, "未命名技能树");
        
                if (SodaCraft.Localizations.LocalizationManager.overrideTexts != null)
                {
                    // 2. [常规注入] 注入配置中指定的 Key (防守性编程)
                    SodaCraft.Localizations.LocalizationManager.overrideTexts[treeNameKey] = nameText;

                    // 3. [关键修复] 注入游戏强制要求的 Key: PerkTree_{ID}
                    // 这样当游戏 UI 请求 "PerkTree_MaidCombatSkills" 时，也能拿到正确的中文
                    string forcedKey = $"PerkTree_{treeId}";
                    SodaCraft.Localizations.LocalizationManager.overrideTexts[forcedKey] = nameText;
            
                    CMDebug.Log($"[SkillTreeSystem] 已注入强制标题 Key: {forcedKey} -> {nameText}");
                }

                // 4. 还是设置一下字段，以防万一
                tTree.Field("displayName").SetValue(treeNameKey);
            }

            // ... (清理图逻辑保持不变)
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
        /// 向技能树添加一个节点
        /// </summary>
        public static Perk AddNodeToTree(PerkTree tree, SkillNodeDef def)
        {
            if (tree == null || def == null) return null;

            // 1. 创建节点物体
            GameObject nodeObj = new GameObject($"Perk_{def.ID}");
            nodeObj.transform.SetParent(tree.transform);
            nodeObj.transform.localPosition = Vector3.zero;

            // ============================================================
            // 2. 配置 Perk 基础属性
            // ============================================================
            Perk perk = nodeObj.AddComponent<Perk>();
            perk.name = $"Perk_{def.ID}"; 
            
            Traverse tPerk = Traverse.Create(perk);
            
            tPerk.Field("displayName").SetValue(def.DisplayName);
            string constructedDescKey = def.DisplayName + "_Desc";
            
            string lookupKey = !string.IsNullOrEmpty(def.Description) ? def.Description : constructedDescKey;
            string finalDescText = CombatMaid.Localization.LocalizationManager.GetText(lookupKey, "暂无描述");

            // 注入到游戏原生本地化字典中
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
            var unlockableItems = CombatMaid.Core.Items.Logic.MaidItemRegistry.GetItemsUnlockedByNode(def.ID);
            if (unlockableItems != null && unlockableItems.Count > 0)
            {
                foreach (var itemId in unlockableItems)
                {
                    var unlocker = nodeObj.AddComponent<PerkUnlockStockShop>();
                    unlocker.unlockItem = itemId;
                    CMDebug.Log($"[SkillTreeBuilder] 节点 {def.ID} 绑定了解锁物品: {itemId}");
                }
            }

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
            // 4. 将节点注册到树的列表和图数据中
            // ============================================================
            Traverse.Create(tree).Field("perks").GetValue<List<Perk>>().Add(perk);

            if (tree.RelationGraphOwner.graph is PerkRelationGraph graph)
            {
                PerkRelationNode graphNode = new PerkRelationNode();
                graphNode.relatedNode = perk;
                graphNode.cachedPosition = def.Position;
                graph.allNodes.Add(graphNode);
            }
            
            // 调试日志
            CMDebug.Log($"[SkillTreeBuilder] 节点 {def.ID} 创建成功 (Key: {def.DisplayName})");

            return perk;
        }

        /// <summary>
        /// 重建图连接
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
        /// 在建筑上添加交互点 (统一修正版)
        /// 参数 interactionKey: 本地化 Key
        /// </summary>
        public static void RegisterInteraction(GameObject buildingObj, string treeId, string interactionKey, string defaultText = "交互")
        {
            var existingInvoker = buildingObj.GetComponentInChildren<PerkTreeUIInvoker>();
            if (existingInvoker == null)
            {
                CMDebug.LogError("[SkillTreeSystem] 目标建筑缺少 PerkTreeUIInvoker");
                return;
            }

            // [新增] 统一的本地化注入逻辑
            string finalInteractText = CombatMaid.Localization.LocalizationManager.GetText(interactionKey, defaultText);
            
            if (SodaCraft.Localizations.LocalizationManager.overrideTexts != null)
            {
                // 告诉游戏：当 UI 遇到 interactionKey 时，请显示 finalInteractText
                SodaCraft.Localizations.LocalizationManager.overrideTexts[interactionKey] = finalInteractText;
            }

            GameObject interactObj = Object.Instantiate(existingInvoker.gameObject, existingInvoker.transform.parent);
            interactObj.name = $"Interact_{treeId}"; // GameObject 名字，仅供调试，无需本地化

            // ... (Transform 和 Collider 清理逻辑保持不变) ...
            interactObj.transform.localPosition = existingInvoker.transform.localPosition;
            interactObj.transform.localRotation = existingInvoker.transform.localRotation;
            interactObj.transform.localScale = existingInvoker.transform.localScale;
            var collider = interactObj.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);

            PerkTreeUIInvoker newInvoker = interactObj.GetComponent<PerkTreeUIInvoker>();
            
            // [关键] 这里赋值 Key，游戏 UI 会去 overrideTexts 里查这个 Key
            newInvoker.InteractName = interactionKey; 
            newInvoker.perkTreeID = treeId;
            newInvoker.MarkerActive = false;

            // ... (Group 处理逻辑保持不变) ...
            var newInvokerGroupList = Traverse.Create(newInvoker).Field("otherInterablesInGroup").GetValue<List<InteractableBase>>();
            if (newInvokerGroupList != null) newInvokerGroupList.Clear();
            var mainGroupList = Traverse.Create(existingInvoker).Field("otherInterablesInGroup").GetValue<List<InteractableBase>>();
            if (mainGroupList != null) mainGroupList.Add(newInvoker);
            existingInvoker.GetInteractableList();

            CMDebug.Log($"[SkillTreeSystem] 交互点已挂载。Key: {interactionKey}, 文本: {finalInteractText}");
        }
    }
}