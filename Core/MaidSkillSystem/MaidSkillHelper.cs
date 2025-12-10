using System.Collections.Generic;
using UnityEngine;
using Duckov;
using Duckov.Scenes;
using ItemStatsSystem;
using ItemStatsSystem.Items;

namespace CombatMaid.Core.MaidSkillSystem
{
    /// <summary>
    /// 女仆技能辅助工具
    /// </summary>
    public static class MaidSkillHelper
    {
        /// <summary>
        /// 让指定角色向目标位置投掷手雷
        /// </summary>
        public static void LaunchGrenade(CharacterMainControl attacker, int itemId, Vector3 targetPos, float delay = 2.0f, bool canHurtSelf = false)
        {
            if (attacker == null) return;
            
            Item item = ItemAssetsCollection.InstantiateSync(itemId);
            if (item == null)
            {
                CMDebug.LogWarning($"无效的物品 ID: {itemId}");
                return;
            }
            
            Skill_Grenade skill = item.GetComponent<Skill_Grenade>();
            if (skill == null)
            {
                CMDebug.LogWarning($"物品 {item.DisplayName} (ID:{itemId}) 不包含 Skill_Grenade 组件");
                Object.Destroy(item.gameObject);
                return;
            }
            
            skill.canHurtSelf = canHurtSelf;
            skill.delay = delay;
            
            SkillReleaseContext context = new SkillReleaseContext
            {
                releasePoint = targetPos
            };
            
            skill.ReleaseSkill(context, attacker);
        }
        
    }
}