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
}