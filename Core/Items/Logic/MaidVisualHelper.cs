using UnityEngine;
using ItemStatsSystem;
using CombatMaid; // 用于访问 CMDebug

namespace CombatMaid.Core.Items.Logic
{
    public static class MaidVisualHelper
    {
        /// <summary>
        /// 从现有的原版物品借用模型外观（Mesh, Material, Collider）
        /// </summary>
        /// <param name="targetItem">我们创建的新物品</param>
        /// <param name="sourceId">被借用的原版物品ID</param>
        public static void CloneVisuals(Item targetItem, int sourceId)
        {
            var sourceItem = ItemAssetsCollection.GetPrefab(sourceId);
            if (sourceItem == null)
            {
                CMDebug.LogWarning($"[MaidVisualHelper] 无法找到源物品 ID: {sourceId}，视觉克隆失败。");
                return;
            }

            // 1. 借用图标 (如果 ItemData 没配图片，就用原版的)
            if (targetItem.Icon == null)
            {
                targetItem.Icon = sourceItem.Icon;
            }

            GameObject srcGo = sourceItem.gameObject;
            GameObject tgtGo = targetItem.gameObject;

            // 2. 克隆网格 (Mesh)
            var srcFilter = srcGo.GetComponent<MeshFilter>();
            if (srcFilter != null)
            {
                var tgtFilter = tgtGo.GetComponent<MeshFilter>() ?? tgtGo.AddComponent<MeshFilter>();
                tgtFilter.sharedMesh = srcFilter.sharedMesh;
            }

            // 3. 克隆材质 (Renderer)
            var srcRenderer = srcGo.GetComponent<MeshRenderer>();
            if (srcRenderer != null)
            {
                var tgtRenderer = tgtGo.GetComponent<MeshRenderer>() ?? tgtGo.AddComponent<MeshRenderer>();
                tgtRenderer.sharedMaterials = srcRenderer.sharedMaterials;
            }

            // 4. 克隆碰撞体 (解决丢地上穿模问题)
            var srcCollider = srcGo.GetComponent<BoxCollider>();
            if (srcCollider != null)
            {
                var tgtCollider = tgtGo.GetComponent<BoxCollider>() ?? tgtGo.AddComponent<BoxCollider>();
                tgtCollider.center = srcCollider.center;
                tgtCollider.size = srcCollider.size;
                tgtCollider.isTrigger = srcCollider.isTrigger;
            }
            
            // 保持层级一致 (例如 Item 层)
            tgtGo.layer = srcGo.layer;

            // 5. 确保有刚体
            if (tgtGo.GetComponent<Rigidbody>() == null)
            {
                var rb = tgtGo.AddComponent<Rigidbody>();
                rb.mass = 0.5f; 
            }
        }
    }
}