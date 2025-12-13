using UnityEngine;
using System.Collections.Generic;
using CombatMaid.Core.AttributeModifiers;
using ItemStatsSystem.Stats;

namespace CombatMaid.Core.MaidFSM.States
{
    /// <summary>
    /// 战术移动模式：响应 G 键指令
    /// 优化：强制面朝移动方向，防止倒退滑步
    /// </summary>
    public class State_TacticalMove : MaidStateBase
    {
        public Vector3 TargetPosition { get; set; }
        
        private float _timeoutTimer;
        private const float MaxDuration = 10.0f;
        
        // 战术移动的移速倍率
        private const float TacticalSpeedMultiplier = 1.3f;
        private List<Modifier> _speedBuffs = new List<Modifier>();

        public override void Enter()
        {
            // 1. 暂停原生AI决策
            SetNativeBrainActive(false);

            if (Controller.AI != null)
            {
                Controller.AI.searchedEnemy = null;
                //Controller.AI.aimTarget = null;
                Controller.AI.SetTarget(null); // 这是一个显式清除目标的方法

                // 2. 执行移动指令
                Controller.AI.StopMove();
                Controller.AI.MoveToPos(TargetPosition);
                
                Controller.MaidCharacter?.PopText("战术机动...");
            }
            
            // 3. 应用战术移速加成
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

            if (Controller.MaidCharacter != null)
            {
                // 抬高视线高度
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
                // 15米以上脱离
                if (dist > 15.0f)
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
      
            float distToTarget = Vector3.Distance(ai.transform.position, TargetPosition);
            if (distToTarget < 1.5f) return true;

            if ((!ai.IsMoving() && ai.HasPath()) || ai.ReachedEndOfPath()) return true;
            
            return false;
        }
    }
}