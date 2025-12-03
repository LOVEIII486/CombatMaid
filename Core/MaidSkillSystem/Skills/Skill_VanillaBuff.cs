using UnityEngine;
using Duckov.Buffs; // 引用原版 Buff 系统

namespace CombatMaid.Core.MaidSkillSystem.Skills
{
    /// <summary>
    /// 施加原版 Buff 的技能
    /// (不需要走 MaidBuffFactory，直接用游戏原生的 Buff)
    /// </summary>
    public class Skill_VanillaBuff : MaidSkillBase
    {
        public override string SkillName => "VanillaBuff";
        public override float Cooldown => 30.0f;

        private string _buffResourcePath; // 原版 Buff 在 Resources 里的路径
        // 或者你可以用 ID 查找，取决于你如何获取原版 Buff 的引用

        public Skill_VanillaBuff(string buffPath)
        {
            _buffResourcePath = buffPath;
        }

        protected override bool CheckTriggerCondition()
        {
            // 简单的触发条件：跟随玩家且玩家活着
            return Controller.MainOwner != null && !Controller.MainOwner.Health.IsDead;
        }

        protected override bool TryExecute()
        {
            // 1. 加载原版 Buff (假设你知道路径，或者通过 GameplayDataSettings 获取)
            // 这里的加载方式取决于游戏怎么管理 Buff，通常是 Resources.Load 或单例列表
            Buff originalBuff = Resources.Load<Buff>(_buffResourcePath);
            
            if (originalBuff == null)
            {
                // 如果通过 Resources 找不到，可能需要去 GameplayDataSettings.Buffs 里找
                // 这里仅作示例，具体要看你想加哪个原版 Buff
                Debug.LogWarning($"[Skill] 未找到原版 Buff: {_buffResourcePath}");
                return false;
            }

            // 2. 直接施加给玩家
            // 这里的 stackCount 填 1，duration 填原版默认或者你想要的
            Controller.MainOwner.AddBuff(originalBuff, Owner, 1);
            
            Owner.PopText("施加原版Buff");
            return true;
        }
    }
}