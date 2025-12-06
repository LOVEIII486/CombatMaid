using System;
using System.Collections.Generic;
using Duckov.PerkTrees;
using Duckov.PerkTrees.Behaviours;
using UnityEngine;

namespace CombatMaid.Core.SkillTreeSystem
{
    /// <summary>
    /// 自动存档组件
    /// </summary>
    public class PerkAutoSaveBehaviour : PerkBehaviour
    {
        protected override void OnUnlocked()
        {
            CMDebug.LogInfo($"[PerkAutoSave] ✓✓✓ 技能已解锁: {gameObject.name}");
            
            if (SkillTreeManager.Instance != null)
            {
                SkillTreeManager.Instance.SaveProgress();
                CMDebug.LogInfo($"[PerkAutoSave] ✓ 存档请求已发送");
            }
            else
            {
                CMDebug.LogError($"[PerkAutoSave] ✗ SkillTreeManager.Instance 为 null！");
            }
        }
    }

    /// <summary>
    /// 玩家属性修改器
    /// </summary>
    public class ModifyPlayerCharacterStats : ModifyCharacterStatsBase
    {
        // 隐藏原版的描述文本
        public override string Description => string.Empty;
    }

    /// <summary>
    /// 女仆技能授权组件
    /// </summary>
    public class MaidSkillGrantBehaviour : PerkBehaviour
    {
        // 这些数据在 Builder 中注入
        public string SkillID; 
        public Dictionary<string, float> MaidStatModifiers;
        public string UnlockAbilityID;

        protected override void OnUnlocked()
        {
            if (MaidManager.Instance == null) return;
            CMDebug.Log($"[MaidSkill] 技能 {SkillID} 解锁");
        }
    }
}