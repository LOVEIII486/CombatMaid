using UnityEngine;
using CombatMaid.Core.AttributeModifiers;

namespace CombatMaid.Core.MaidFSM.States
{
    /// <summary>
    /// 战术移动状态：利用加速度和反应速度提升实现“瞬移”感
    /// </summary>
    public class State_TacticalMove : MaidStateBase
    {
        public Vector3 TargetPosition { get; set; }
        
        private float _timeoutTimer;
        private const float MaxDuration = 5.0f;
        
        private const float SpeedMultiplier = 1.35f;  // 移速提升 35%
        private const float AccMultiplier = 6.0f;    // 加速度提升 500%

        public override void Enter()
        {
            SetNativeBrainActive(false);

            if (Controller.AI != null)
            {
                Controller.AI.StopMove();
                Controller.AI.MoveToPos(TargetPosition);
                Controller.MaidCharacter?.PopText("<color=#00FFFF>执行战术机动</color>");
            }
            
            ApplyTacticalBuffs();
            _timeoutTimer = MaxDuration;
        }

        private void ApplyTacticalBuffs()
        {
            var character = Controller.MaidCharacter;
            if (character == null) return;
            
            StatModifier.RemoveAllModifiersFromSource(Controller.MaidCharacter, this);
            
            AttributeModifier.Modify(character, StatModifier.Attributes.WalkSpeed, SpeedMultiplier, true, this);
            AttributeModifier.Modify(character, StatModifier.Attributes.RunSpeed, SpeedMultiplier, true, this);
            
            AttributeModifier.Modify(character, StatModifier.Attributes.WalkAcc, AccMultiplier, true, this);
            AttributeModifier.Modify(character, StatModifier.Attributes.RunAcc, AccMultiplier, true, this);
        }

        public override void Update()
        {
            _timeoutTimer -= Time.deltaTime;

            // 集火检查逻辑：检测玩家通过 MaidManager 标记的目标
            var focusTarget = MaidManager.Instance.FocusTarget;
            if (focusTarget != null && !focusTarget.Health.IsDead)
            {
                CMDebug.Log("[TacticalMove] 检测到集火指令，中止机动。");
                Machine.ChangeState<State_Autonomous>();
                return;
            }

            // 移动中注视目标点上方
            if (Controller.MaidCharacter != null)
            {
                Controller.MaidCharacter.SetAimPoint(TargetPosition + Vector3.up * 1.5f); //
            }

            if (HasArrived() || _timeoutTimer <= 0)
            {
                HandleArrivalLogic();
                Machine.ChangeState<State_Autonomous>();
            }
        }

        public override void Exit()
        {
            SetNativeBrainActive(true); 

            if (Controller.MaidCharacter != null)
            {
                AttributeModifier.ClearAll(Controller.MaidCharacter, this);
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
                // 如果机动结束离敌人较远，则清除仇恨，实现“脱战”效果
                float dist = Vector3.Distance(ai.transform.position, ai.searchedEnemy.transform.position);
                if (dist > 20.0f)
                {
                    ai.searchedEnemy = null;
                    ai.noticed = false;
                    Controller.MaidCharacter?.PopText("目标已摆脱");
                }
            }
        }

        private bool HasArrived()
        {
            var ai = Controller.AI;
            if (ai == null || ai.WaitingForPathResult()) return false;
            
            // 平面距离检查
            float distToTarget = Vector2.Distance(
                new Vector2(ai.transform.position.x, ai.transform.position.z), 
                new Vector2(TargetPosition.x, TargetPosition.z)
            );

            return distToTarget < 1.5f || ai.ReachedEndOfPath();
        }
    }
}