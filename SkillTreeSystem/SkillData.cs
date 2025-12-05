using System.Collections.Generic;
using UnityEngine;

namespace CombatMaid.Core.SkillTreeSystem
{
    /// <summary>
    /// 简化版技能节点数据
    /// </summary>
    public class SkillNodeDef
    {
        public string ID;               // 内部唯一ID
        public string DisplayName;      // 显示名称
        public string Description;      // 描述文本
        public Sprite Icon;             // 图标
        public Vector2 Position;        // 在技能树面板上的坐标 (0,0 是中心)
        
        // 消耗与需求
        public int CostMoney = 0;
        public int RequiredLevel = 1;
        public Dictionary<int, int> CostItems = new Dictionary<int, int>(); // 物品ID -> 数量

        // 属性加成 (Key参考 ItemStatsSystem.Stat)
        // 例如: "health", "move_speed", "attack_damage"
        public Dictionary<string, float> StatModifiers = new Dictionary<string, float>();

        // 前置技能ID列表
        public List<string> PrerequisiteIDs = new List<string>();
    }
}