using System.Collections.Generic;
using CombatMaid.Core.BuffsSystem;
using CombatMaid.Localization;
using UnityEngine;
using System.Diagnostics;

namespace CombatMaid.Core.MaidSkillSystem.Skills
{
    public class Skill_FreezeNova : MaidSkillBase
    {
        public override string SkillName => "FreezeNova";
        public override float Cooldown => 60.0f;

        private const float FreezeRadius = 15.0f;
        private const int FreezeBuffID = 1127;
        
        private const float ScanInterval = 1.0f;
        private float _lastScanTime = 0f;
        private bool _cachedScanResult = false;

        protected override bool CheckTriggerCondition()
        {
            if (Owner == null || Owner.Health.IsDead) return false;

            if (Controller.AI.searchedEnemy == null || Controller.AI.searchedEnemy.health.IsDead)
            {
                _cachedScanResult = false;
                return false;
            }

            if (Time.time < _lastScanTime + ScanInterval)
            {
                return _cachedScanResult;
            }

            _lastScanTime = Time.time;
            
            _cachedScanResult = GetTargetsInRange(false).Count > 0;
            return _cachedScanResult;
        }

        protected override bool TryExecute()
        {
            List<CharacterMainControl> targets = GetTargetsInRange(true); 
            if (targets.Count == 0) return false;

            int frozenCount = 0;
            foreach (var target in targets)
            {
                if (MaidBuffUtils.ApplyBuffByID(target, FreezeBuffID, out _, Owner))
                {
                    frozenCount++;
                }
            }

            if (frozenCount > 0)
            {
                string msg = LocalizationManager.GetText("Skill_FreezeNova_Success") ?? "Freeze Nova: {0}!";
                Owner.PopText(string.Format(msg, frozenCount));
                
                _cachedScanResult = false;
                return true;
            }
            return false;
        }

        private List<CharacterMainControl> GetTargetsInRange(bool logPerformance)
        {
            Stopwatch sw = null;
            if (logPerformance) sw = Stopwatch.StartNew();

            List<CharacterMainControl> validTargets = new List<CharacterMainControl>();
            Vector3 myPos = Owner.transform.position;
            float sqrRadius = FreezeRadius * FreezeRadius;

            CharacterMainControl[] allEntities = Object.FindObjectsOfType<CharacterMainControl>();

            foreach (var entity in allEntities)
            {
                if (entity == null || entity.Health.IsDead || entity.Team == Owner.Team)
                    continue;
                if ((entity.transform.position - myPos).sqrMagnitude <= sqrRadius)
                {
                    validTargets.Add(entity);
                }
            }

            if (logPerformance && sw != null)
            {
                sw.Stop();
                //CMDebug.Log($"[FreezeNova] 性能监测: 耗时 {sw.Elapsed.TotalMilliseconds:F3}ms | 场景总数: {allEntities.Length} | 目标数: {validTargets.Count}");
            }

            return validTargets;
        }
    }
}