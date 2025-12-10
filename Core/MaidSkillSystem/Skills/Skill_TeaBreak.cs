using UnityEngine;
using System.Collections.Generic;
using CombatMaid.Core;

namespace CombatMaid.Core.MaidSkillSystem.Skills
{
    /// <summary>
    /// 休憩饮食支援：非战斗状态下为主人恢复水分与饥饿值
    /// </summary>
    public class Skill_TeaBreak : MaidSkillBase
    {
        public override string SkillName => "TeaBreak";
        public override float Cooldown => 120.0f;
        
        public override bool RespectGlobalCooldown => true;
        public override float TriggerGCDDuration => 1.0f;

        private float _recoverWaterRatio = 0.20f;
        private float _recoverEnergyRatio = 0.10f;

        private const float NeedWaterThreshold = 0.6f;
        private const float NeedEnergyThreshold = 0.6f;

        private readonly string[] _teaWords = new string[]
        {
            "主人，请用红茶~",
            "战斗辛苦了，吃点点心吧。",
            "休息一下，补充水分~",
            "这是刚烤好的饼干哦！",
            "女仆特制活力饮料！"
        };
        private int _lastTeaWordIndex = -1;

        protected override bool CheckTriggerCondition()
        {
            if (Controller == null || Owner == null || Owner.Health.IsDead) return false;
            if (Controller.MainOwner == null || Controller.MainOwner.Health.IsDead) return false;

            var ai = Controller.AI;
            if (ai == null) return false;
            
            if (ai.searchedEnemy != null || ai.alert) return false;

            float dist = Vector3.Distance(Owner.transform.position, Controller.MainOwner.transform.position);
            if (dist > 10.0f) return false;

            var player = Controller.MainOwner;

            bool needWater = player.CurrentWater < player.MaxWater * NeedWaterThreshold;
            bool needEnergy = player.CurrentEnergy < player.MaxEnergy * NeedEnergyThreshold;

            return needWater || needEnergy;
        }

        protected override bool TryExecute()
        {
            var player = Controller.MainOwner;
            if (player == null) return false;

            // 计算缺口
            float waterMissing = player.MaxWater - player.CurrentWater;
            float energyMissing = player.MaxEnergy - player.CurrentEnergy;

            if (waterMissing <= 0f && energyMissing <= 0f)
                return false;

            float waterToAdd = Mathf.Min(player.MaxWater * _recoverWaterRatio, waterMissing);
            float energyToAdd = Mathf.Min(player.MaxEnergy * _recoverEnergyRatio, energyMissing);

            if (waterToAdd > 0f)
                player.AddWater(waterToAdd);

            if (energyToAdd > 0f)
                player.AddEnergy(energyToAdd);

            int index;
            if (_teaWords.Length <= 1)
            {
                index = 0;
            }
            else
            {
                do
                {
                    index = Random.Range(0, _teaWords.Length);
                }
                while (index == _lastTeaWordIndex);
            }

            _lastTeaWordIndex = index;
            string word = _teaWords[index];
            Owner.PopText(word);

            //CMDebug.Log($"[{SkillName}] 为主人提供了茶点支援（水分+{waterToAdd:F1}，能量+{energyToAdd:F1}）");
            return true;
        }
    }
}
