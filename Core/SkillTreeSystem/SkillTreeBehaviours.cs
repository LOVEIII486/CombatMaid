using System.Collections.Generic;
using UnityEngine;
using Duckov.PerkTrees;
using Duckov.PerkTrees.Behaviours;
using CombatMaid.Core; // 引用 MaidManager

namespace CombatMaid.Core.SkillTreeSystem
{
    // =========================================================
    // 1. 自动存档组件 (直接使用你的逻辑)
    // =========================================================
    public class PerkAutoSaveBehaviour : PerkBehaviour
    {
        protected override void OnUnlocked()
        {
            // 调用管理器的保存方法
            SkillTreeManager.Instance?.SaveProgress();
            CMDebug.Log("[SkillTree] 技能解锁，触发自动保存。");
        }
    }

    // =========================================================
    // 2. 玩家属性静默修改器 (你的代码，用于玩家自身属性加成)
    // =========================================================
    public class SilentModifyCharacterStats : ModifyCharacterStatsBase
    {
        // 隐藏原来的属性描述，防止UI显示的太乱，仅生效数值
        public override string Description => string.Empty;
    }

    // =========================================================
    // 3. [新增] 女仆技能授权组件
    //    职责：当解锁时，通知 MaidManager 强化所有女仆
    // =========================================================
    public class MaidSkillGrantBehaviour : PerkBehaviour
    {
        // 这些数据会在 Builder 构建时注入进来
        public string SkillID; 
        public Dictionary<string, float> MaidStatModifiers; // 女仆属性加成
        public string UnlockAbilityID; // 如果有主动技能需要解锁

        protected override void OnUnlocked()
        {
            if (MaidManager.Instance == null) return;

            CMDebug.Log($"[MaidSkill] 技能 {SkillID} 解锁，正在应用女仆强化...");

            // 1. 通知管理器执行“全员强化”逻辑 (针对当前已生成的实体)
            MaidManager.Instance.ApplyGlobalSkillEffect(this);
            
            // 2. (可选) 播放个全局音效或提示
            // Duckov.Audio.AudioManager.Play...
        }
    }
}