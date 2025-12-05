using System.Collections.Generic;
using UnityEngine;

namespace CombatMaid.Core.SkillTreeSystem
{
    public class SkillNodeDef
    {
        public string ID;               
        public string DisplayName;      
        public string Description;      
        
        // [新增] 图标文件名配置
        // 如果为空，逻辑层会自动使用 "default_icon.png"
        public string IconFileName; 

        // 运行时加载后的 Sprite 对象 (不需要手动配置，由管理器填充)
        public Sprite Icon;             
        
        public Vector2 Position;        
        public int CostMoney = 0;
        public int RequiredLevel = 1;
        public Dictionary<int, int> CostItems = new Dictionary<int, int>(); 
        public Dictionary<string, float> StatModifiers = new Dictionary<string, float>();
        public List<string> PrerequisiteIDs = new List<string>();
        
        // --- 玩家相关 ---
        // 玩家获得的属性 (使用 SilentModifyCharacterStats)
        public Dictionary<string, float> PlayerStatModifiers = new Dictionary<string, float>();

        // --- 女仆相关 [新增] ---
        // 女仆获得的属性 (如 "MaxHealth": 50)
        public Dictionary<string, float> MaidStatModifiers = new Dictionary<string, float>();
        
        // 女仆获得的特殊能力ID (如 "GrenadeThrow")
        public string MaidAbilityID;
    }
}