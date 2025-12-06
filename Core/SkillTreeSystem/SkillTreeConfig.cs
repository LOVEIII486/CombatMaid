using System;
using System.Collections.Generic;
using UnityEngine;

namespace CombatMaid.Core.SkillTreeSystem
{
    /// <summary>
    /// 技能树配置文件根对象
    /// </summary>
    [Serializable]
    public class SkillTreeConfig
    {
        public string TreeID;
        public string TreeName;
        public string Description;
        public string Version;
        public List<SkillNodeConfig> Nodes;
    }

    /// <summary>
    /// 单个技能节点配置（新格式 - 已移除兼容）
    /// </summary>
    [Serializable]
    public class SkillNodeConfig
    {
        public string ID;                          // 唯一标识符
        public string DisplayName;                 // 显示名称
        public string Description;                 // 描述文本
        public string IconFileName;                // 图标文件名
        
        // 成本配置
        public int CostMoney = 0;                  // 金钱成本
        public int RequiredLevel = 1;              // 等级需求
        public Dictionary<int, int> CostItems;     // 物品成本 {物品ID: 数量}
        
        // 位置和前置
        public Vector2 Position;                   // UI位置
        public List<string> PrerequisiteIDs;       // 前置技能ID列表
        
        // 属性修改器
        public Dictionary<string, float> PlayerStatModifiers;  // 玩家属性修改
        
        // === 新格式：女仆修改器 ===
        public List<MaidModifierConfig> MaidModifiers;  // 女仆修改器列表
    }

    /// <summary>
    /// 女仆修改器配置（新格式）
    /// </summary>
    [Serializable]
    public class MaidModifierConfig
    {
        public string Type;  // "AddAttribute", "MultiplyAttribute", "AddSkill", "Custom"
        
        // 用于属性修改
        public string Attribute;
        public float Value;
        
        // 用于技能添加
        public string SkillID;
        public Dictionary<string, object> SkillParams;
        
        // 用于自定义逻辑
        public string CustomAction;
    }
}