using UnityEngine;

namespace CombatMaid.Core.MaidSkillSystem.Skills
{
    /// <summary>
    /// 酒狐的温暖
    /// </summary>
    public class Skill_WarmthOfFox : MaidSkillBase
    {
        public override string SkillName => "WarmthOfFox";
        public override float Cooldown => 8.0f;
        public override bool RespectGlobalCooldown => false;

        private readonly int[] _coldBuffIds = { 2101, 2102, 2201 }; 
        private readonly int _resistColdBuffId = 2301; 
        private const float RangeThreshold = 1.5f;
        
        private float _lastPopTextTime = -999f; 
        private const float TextPopInterval = 16.0f;

        protected override bool CheckTriggerCondition()
        {
            if (Controller.MainOwner == null || Controller.MainOwner.Health.IsDead) return false;

            float distance = Vector3.Distance(Owner.transform.position, Controller.MainOwner.transform.position);
            if (distance > RangeThreshold) return false;

            var playerBuffManager = Controller.MainOwner.GetBuffManager();
            if (playerBuffManager == null) return false;

            foreach (int id in _coldBuffIds)
            {
                if (playerBuffManager.HasBuff(id)) return true;
            }

            return false;
        }

        protected override bool TryExecute()
        {
            var playerBuffManager = Controller.MainOwner.GetBuffManager();
            if (playerBuffManager == null) return false;

            bool removedAny = false;
            foreach (int id in _coldBuffIds)
            {
                if (playerBuffManager.HasBuff(id))
                {
                    playerBuffManager.RemoveBuff(id, false);
                    //MaidBuffUtils.ApplyBuffByID(Controller.MainOwner, _resistColdBuffId, out string name, Owner);
                    removedAny = true;
                }
            }

            return removedAny;
        }

        protected override void OnSkillExecuted()
        {
            if (Time.time - _lastPopTextTime >= TextPopInterval)
            {
                Controller.MainOwner.PopText("<color=#FFA500>感到一阵温暖，寒意消失了...</color>");
                Controller.MaidCharacter.PopText("<color=#FFA500>呼~ 呼~ 给主人取暖！寒气全跑掉啦~</color>");
                _lastPopTextTime = Time.time;
            }
            //CMDebug.Log($"[{SkillName}] 驱散寒冷状态");
        }
    }
}