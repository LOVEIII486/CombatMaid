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
        public string IconFileName; 
        public Sprite Icon;             
        public Vector2 Position;        
        public int CostMoney = 0;
        public int RequiredLevel = 1;
        public Dictionary<int, int> CostItems = new Dictionary<int, int>(); 
        public List<string> PrerequisiteIDs = new List<string>();
        public float UnlockTime = 3f;
        // 玩家获得的属性
        public Dictionary<string, float> PlayerStatModifiers = new Dictionary<string, float>();
        // 女仆修改器列表
        public List<SkillTreeModifier> MaidModifiers = new List<SkillTreeModifier>();
    }
}