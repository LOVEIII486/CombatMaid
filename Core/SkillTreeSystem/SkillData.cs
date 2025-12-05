using System;
using System.Collections.Generic;

namespace CombatMaid.Core.SkillTreeSystem
{
    [Serializable]
    public class SkillTreeConfig
    {
        public string treeId = "Maid_Tech_Tree";
        public List<SkillNodeData> nodes = new List<SkillNodeData>();
    }

    [Serializable]
    public class SkillNodeData
    {
        public string id;           // 逻辑ID (例如 "Contract_Elite", "Stat_Atk_1")
        public string name;         // 显示名称
        public string description;  // 描述文本
        public string iconPath;     // 图标路径
        
        public float posX;          // UI X坐标
        public float posY;          // UI Y坐标

        public List<string> parentIDs = new List<string>(); // 前置父节点
        public List<CostData> costs = new List<CostData>(); // 解锁消耗
    }

    [Serializable]
    public class CostData
    {
        public int itemId;
        public int amount;
    }
    
    // 用于存档的简单容器
    [Serializable]
    public class SkillSaveData
    {
        public List<string> unlockedSkillIDs = new List<string>();
    }
}