using UnityEngine;
using System.Collections.Generic;
using CombatMaid.Core.AttributeModifiers;
using ItemStatsSystem.Stats;

namespace CombatMaid.Core.MaidFSM.States
{
    /// <summary>
    /// 战术移动模式：响应 G 键指令
    /// </summary>
    public class State_TacticalMove : MaidStateBase
    {
        public Vector3 TargetPosition { get; set; }
        
        private float _timeoutTimer;
        private const float MaxDuration = 5.0f;
        
        // 战术移动的移速倍率
        private const float TacticalSpeedMultiplier = 1.3f;
        private List<Modifier> _speedBuffs = new List<Modifier>();

        public override void Enter()
        {
            SetNativeBrainActive(false);

            if (Controller.AI != null)
            {
                //Controller.AI.searchedEnemy = null;
                //Controller.AI.aimTarget = null;
                //Controller.AI.SetTarget(null); // 这是一个显式清除目标的方法

                Controller.AI.StopMove();
                Controller.AI.MoveToPos(TargetPosition);
                
                Controller.MaidCharacter?.PopText("战术机动...");
            }
            
            if (Controller.MaidCharacter != null)
            {
                AttributeModifier.Quick.RevertSpeedModifiers(Controller.MaidCharacter, _speedBuffs);
                _speedBuffs = AttributeModifier.Quick.ModifySpeed(Controller.MaidCharacter, TacticalSpeedMultiplier);
            }

            _timeoutTimer = MaxDuration;
        }

        public override void Update()
        {
            _timeoutTimer -= Time.deltaTime;

            var focusTarget = MaidManager.Instance.FocusTarget;
            if (focusTarget != null && !focusTarget.Health.IsDead)
            {
                CMDebug.Log($"检测到集火目标: {focusTarget.name}，打断战术移动。");
                Controller.MaidCharacter?.PopText("<color=red>中止机动，集火目标！</color>");
                Machine.ChangeState<State_Autonomous>();
                return;
            }

            if (Controller.MaidCharacter != null)
            {
                Vector3 lookAtPos = TargetPosition + Vector3.up * 1.5f;
                Controller.MaidCharacter.SetAimPoint(lookAtPos);
            }

            if (HasArrived() || _timeoutTimer <= 0)
            {
                HandleArrivalLogic();
                Machine.ChangeState<State_Autonomous>();
            }
        }

        /// <summary>
        /// 退出状态清理 Buff
        /// </summary>
        public override void Exit()
        {
            SetNativeBrainActive(true); 

            if (Controller.MaidCharacter != null)
            {
                AttributeModifier.Quick.RevertSpeedModifiers(Controller.MaidCharacter, _speedBuffs);
            }
    
            if (Controller.AI != null)
            {
                Controller.AI.StopMove();
            }
        }

        private void HandleArrivalLogic()
        {
            var ai = Controller.AI;
            if (ai != null && ai.searchedEnemy != null)
            {
                float dist = Vector3.Distance(ai.transform.position, ai.searchedEnemy.transform.position);
                if (dist > 18.0f)
                {
                    ai.searchedEnemy = null;
                    ai.aimTarget = null;
                    ai.noticed = false;
                    Controller.MaidCharacter?.PopText("脱离接触");
                }
            }
        }

        private bool HasArrived()
        {
            var ai = Controller.AI;
            if (ai == null) return true;
    
            if (ai.WaitingForPathResult()) return false;

            Vector3 flatPos = new Vector3(ai.transform.position.x, 0, ai.transform.position.z);
            Vector3 flatTarget = new Vector3(TargetPosition.x, 0, TargetPosition.z);
            float distToTarget = Vector3.Distance(flatPos, flatTarget);

            if (distToTarget < 1.8f)
            {
                return true;
            }
            if ((!ai.IsMoving() && ai.HasPath()) || ai.ReachedEndOfPath()) return true;
    
            return false;
        }
    }
}