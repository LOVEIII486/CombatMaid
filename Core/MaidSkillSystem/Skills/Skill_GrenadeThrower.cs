using System;
using System.Collections.Generic;
using UnityEngine;
using CombatMaid.Localization;
using Random = UnityEngine.Random;

namespace CombatMaid.Core.MaidSkillSystem.Skills
{
    public class Skill_GrenadeThrower : MaidSkillBase
    {
        private static int _headLayer = -1;
        private static int _damageReceiverLayer = 3;
        private static int _visualOcclusionLayer = 10;

        public override string SkillName => "GrenadeThrow";
        public override float Cooldown => 20.0f;
        
        public override bool RespectGlobalCooldown => true;
        public override float TriggerGCDDuration => 1.5f;

        private readonly List<int> _grenadePool;
        private float _throwRange = 25.0f;
        private float _minRange = 5.0f;

        private readonly float _minRangeSqr;
        private readonly float _throwRangeSqr;
        
        private readonly HashSet<int> _elementalGrenadeIds = new HashSet<int> { 933, 941, 942 };
        
        private readonly Lazy<string> _txtExecute = new Lazy<string>(() => 
            LocalizationManager.GetText("Skill_GrenadeThrow_Execute"));

        public Skill_GrenadeThrower(List<int> grenadeIds) 
        {
            _grenadePool = grenadeIds ?? new List<int>();
            
            _minRangeSqr = _minRange * _minRange;
            _throwRangeSqr = _throwRange * _throwRange;

            // 只在第一次初始化时查找层级
            if (_headLayer == -1) _headLayer = LayerMask.NameToLayer("HeadCollider");
        }

        protected override bool CheckTriggerCondition()
        {
            if (Controller == null || Controller.AI == null) return false;
            
            var target = Controller.AI.searchedEnemy;
            if (target == null || target.health.IsDead) return false;

            Vector3 ownerPos = Owner.transform.position;
            Vector3 targetPos = target.transform.position;

            float distSqr = (targetPos - ownerPos).sqrMagnitude;
            if (distSqr < _minRangeSqr || distSqr > _throwRangeSqr) return false;

            // 排除：女仆自身、敌人主层、受击层、视线层、头部碰撞层
            int ignoreMask = (1 << Owner.gameObject.layer) | (1 << target.gameObject.layer) | 
                             (1 << _damageReceiverLayer) | (1 << _visualOcclusionLayer);
            
            if (_headLayer != -1) ignoreMask |= (1 << _headLayer);
            
            int obstacleMask = ~ignoreMask;

            Vector3 rayStart = ownerPos + Vector3.up * 1.6f;
            Vector3 rayEnd = targetPos + Vector3.up * 0.8f;

            RaycastHit hit;
            if (Physics.Linecast(rayStart, rayEnd, out hit, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(Owner.transform) || hit.transform.IsChildOf(target.transform))
                {
                    return true;
                }
                return false;
            }

            return true;
        }

        protected override bool TryExecute()
        {
            var target = Controller.AI.searchedEnemy;
            if (target == null) return false;

            List<int> validGrenades = GetValidGrenades();
            if (validGrenades.Count == 0) return false; 
    
            int selectedItemId = validGrenades[Random.Range(0, validGrenades.Count)];

            MaidSkillHelper.LaunchGrenade(
                Owner, 
                selectedItemId, 
                target.transform.position + Vector3.up * 0.2f, 
                delay: 1.3f, 
                canHurtSelf: false
            );

            Owner.PopText(_txtExecute.Value);
            return true;
        }

        private List<int> GetValidGrenades()
        {
            if (CombatMaid.Settings.CombatMaidConfig.EnableElementalGrenades)
                return _grenadePool;

            var list = new List<int>();
            foreach (var id in _grenadePool)
            {
                if (!_elementalGrenadeIds.Contains(id)) list.Add(id);
            }
            return list;
        }
    }
}