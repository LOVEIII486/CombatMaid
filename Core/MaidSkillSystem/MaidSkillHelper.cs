using UnityEngine;
using Duckov;           // 游戏原生命名空间
using Duckov.Scenes;    // 游戏原生命名空间
using ItemStatsSystem;  // 游戏原生命名空间
using ItemStatsSystem.Items;

namespace CombatMaid.Core.MaidSkillSystem
{
    /// <summary>
    /// 女仆技能辅助工具 (移植自 EliteBehaviorHelper)
    /// </summary>
    public static class MaidSkillHelper
    {
        private const string LogTag = "[CombatMaid.SkillHelper]";

        /// <summary>
        /// 让指定角色向目标位置投掷手雷/技能物品
        /// </summary>
        public static void LaunchGrenade(CharacterMainControl attacker, int itemId, Vector3 targetPos, float delay = 2.0f, bool canHurtSelf = false)
        {
            if (attacker == null) return;
            
            // 1. 实例化物品 (同步)
            Item item = ItemAssetsCollection.InstantiateSync(itemId);
            if (item == null)
            {
                Debug.LogWarning($"{LogTag} 无效的物品 ID: {itemId}");
                return;
            }
            
            // 2. 获取技能组件
            // 注意：Duckov 的手雷逻辑通常挂载了 Skill_Grenade 或类似的 SkillBase
            Skill_Grenade skill = item.GetComponent<Skill_Grenade>();
            if (skill == null)
            {
                Debug.LogWarning($"{LogTag} 物品 {item.DisplayName} (ID:{itemId}) 不包含 Skill_Grenade 组件");
                // 尝试销毁生成的无用物品防止内存泄漏
                Object.Destroy(item.gameObject);
                return;
            }
            
            // 3. 配置参数
            skill.canHurtSelf = canHurtSelf;
            skill.delay = delay;
            
            // 4. 构建释放上下文 (指定落点)
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