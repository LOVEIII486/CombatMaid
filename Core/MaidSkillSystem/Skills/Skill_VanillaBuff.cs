using System;
using System.Collections.Generic;
using System.Linq;
using CombatMaid.Core.BuffsSystem;
using CombatMaid.Localization;
using CombatMaid.Settings;
using Random = UnityEngine.Random;

namespace CombatMaid.Core.MaidSkillSystem.Skills
{
    /// <summary>
    /// 随机增益技能
    /// </summary>
    public class Skill_VanillaBuff : MaidSkillBase
    {
        public override string SkillName => "VanillaBuff";
        public override float Cooldown => 100.0f;
        
        public override bool RespectGlobalCooldown => true;
        public override float TriggerGCDDuration => 1f;

        private readonly List<int> _buffPool;
        private const int HappyBuffId = 1101; // 高兴 Buff
        
        private readonly Lazy<string> _txtHappy = new Lazy<string>(() => 
            LocalizationManager.GetText("Skill_VanillaBuff_Happy"));
        private readonly Lazy<string> _txtExtraBuff = new Lazy<string>(() => 
            LocalizationManager.GetText("Skill_VanillaBuff_ExtraBuff"));

        public Skill_VanillaBuff(List<int> buffIds)
        {
            _buffPool = buffIds ?? new List<int>();
        }

        protected override bool CheckTriggerCondition()
        {
            return Controller.MainOwner != null && !Controller.MainOwner.Health.IsDead;
        }

        protected override bool TryExecute()
        {
            bool anySuccess = false;
            string randomBuffName = "";

            if (MaidBuffUtils.ApplyBuffByID(Controller.MainOwner, HappyBuffId, out _, Owner))
            {
                anySuccess = true;
            }

            if (_buffPool.Count > 0)
            {
                var validBuffs = _buffPool
                    .Where(id => !CombatMaidConfig.BlockedBuffIDs.Contains(id))
                    .ToList();
                if (validBuffs.Count > 0)
                {
                    int index = Random.Range(0, validBuffs.Count);
                    int randomBuffId = validBuffs[index]; // 使用过滤后的列表
            
                    if (MaidBuffUtils.ApplyBuffByID(Controller.MainOwner, randomBuffId, out string name, Owner))
                    {
                        randomBuffName = name;
                        anySuccess = true;
                    }
                    else
                    {
                        CMDebug.LogWarning($"随机 Buff 施加失败 (ID: {randomBuffId})");
                    }
                }
                else
                {
                    CMDebug.LogWarning($"所有随机 Buff 均被黑名单禁用或池为空。");
                }
            }

            if (anySuccess)
            {
                string popText = _txtHappy.Value;
                if (!string.IsNullOrEmpty(randomBuffName))
                {
                    popText += string.Format(_txtExtraBuff.Value, randomBuffName);
                }

                Owner.PopText(popText);
                return true;
            }
    
            return false;
        }
    }
}