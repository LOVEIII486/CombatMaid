using UnityEngine;
using ItemStatsSystem;
using System.Reflection;
using CombatMaid; 

namespace CombatMaid.Core.Items.Logic
{
    public static class MaidVisualHelper
    {
        /// <summary>
        /// 全面克隆源物品的外观、物理和代理配置
        /// </summary>
        public static void CloneVisuals(Item targetItem, int sourceId)
        {
            var sourceItem = ItemAssetsCollection.GetPrefab(sourceId);
            if (sourceItem == null) return;

            // 1. 图标与模型
            if (targetItem.Icon == null) targetItem.Icon = sourceItem.Icon;
            CloneMeshAndMaterial(targetItem.gameObject, sourceItem.gameObject);
            ClonePhysics(targetItem.gameObject, sourceItem.gameObject);

            // 2. [关键] 克隆 Agent 配置 (解决丢弃崩溃)
            CloneAgentUtilities(targetItem, sourceItem);
        }

        private static void CloneMeshAndMaterial(GameObject tgt, GameObject src)
        {
            var srcFilter = src.GetComponent<MeshFilter>();
            if (srcFilter != null)
            {
                var tgtFilter = tgt.GetComponent<MeshFilter>() ?? tgt.AddComponent<MeshFilter>();
                tgtFilter.sharedMesh = srcFilter.sharedMesh;
            }

            var srcRenderer = src.GetComponent<MeshRenderer>();
            if (srcRenderer != null)
            {
                var tgtRenderer = tgt.GetComponent<MeshRenderer>() ?? tgt.AddComponent<MeshRenderer>();
                tgtRenderer.sharedMaterials = srcRenderer.sharedMaterials;
            }
            
            tgt.layer = src.layer;
        }

        private static void ClonePhysics(GameObject tgt, GameObject src)
        {
            var srcCollider = src.GetComponent<BoxCollider>();
            if (srcCollider != null)
            {
                var tgtCollider = tgt.GetComponent<BoxCollider>() ?? tgt.AddComponent<BoxCollider>();
                tgtCollider.center = srcCollider.center;
                tgtCollider.size = srcCollider.size;
                tgtCollider.isTrigger = srcCollider.isTrigger;
            }

            if (tgt.GetComponent<Rigidbody>() == null)
            {
                var rb = tgt.AddComponent<Rigidbody>();
                rb.mass = 0.5f;
            }
        }

        private static void CloneAgentUtilities(Item target, Item source)
        {
            // 通过反射将源物品的 Agent配置 (掉落物/手持物定义) 复制给新物品
            try 
            {
                var field = typeof(Item).GetField("agentUtilities", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (field != null)
                {
                    var sourceAgents = field.GetValue(source);
                    if (sourceAgents != null)
                    {
                        field.SetValue(target, sourceAgents);
                    }
                }
            }
            catch (System.Exception ex)
            {
                CMDebug.LogError($"[MaidVisualHelper] Agent 克隆失败: {ex.Message}");
            }
        }
    }
}