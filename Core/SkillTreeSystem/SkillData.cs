using System.Collections.Generic;
using UnityEngine;

namespace CombatMaid.Core.SkillTreeSystem
{
    /// <summary>
    /// 技能节点定义
    /// </summary>
    public class SkillNodeDef
    {
        public string ID;               
        public string DisplayName;      
        public string Description;      

        // 默认 "default_icon.png"
        public string IconFileName; 

        // 不需要手动配置
        public Sprite Icon;             
        
        public Vector2 Position;        
        public int CostMoney = 0;
        public int RequiredLevel = 1;
        public Dictionary<int, int> CostItems = new Dictionary<int, int>(); 
        public List<string> PrerequisiteIDs = new List<string>();
        
        // 玩家获得的属性
        public Dictionary<string, float> PlayerStatModifiers = new Dictionary<string, float>();
        
        // === 新格式：女仆修改器列表 ===
        public List<SkillTreeModifier> MaidModifiers = new List<SkillTreeModifier>();
    }
}