using UnityEngine;
using Duckov;           // 游戏原生命名空间
using Duckov.Scenes;    // 游戏原生命名空间
using ItemStatsSystem;  // 游戏原生命名空间
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
            
            // 1. 实例化物品
            Item item = ItemAssetsCollection.InstantiateSync(itemId);
            if (item == null)
            {
                CMDebug.LogWarning($"无效的物品 ID: {itemId}");
                return;
            }
            
            // 2. 获取技能组件
            Skill_Grenade skill = item.GetComponent<Skill_Grenade>();
            if (skill == null)
            {
                CMDebug.LogWarning($"物品 {item.DisplayName} (ID:{itemId}) 不包含 Skill_Grenade 组件");
                Object.Destroy(item.gameObject);
                return;
            }
            
            // 3. 配置参数
            skill.canHurtSelf = canHurtSelf;
            skill.delay = delay;
            
            // 4. 构建释放上下文
            SkillReleaseContext context = new SkillReleaseContext
            {
                releasePoint = targetPos
            };
            
            // 5. 释放技能
            skill.ReleaseSkill(context, attacker);
        }

        /// <summary>
        /// 直接向玩家当前位置发射
        /// </summary>
        public static void LaunchGrenadeAtPlayer(CharacterMainControl attacker, int itemId, float delay = 2.0f)
        {
            if (LevelManager.Instance?.MainCharacter == null) return;
            Vector3 playerPos = LevelManager.Instance.MainCharacter.transform.position;
            LaunchGrenade(attacker, itemId, playerPos, delay);
        }
    }
}