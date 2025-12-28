using System;
using CombatMaid.Localization;
using UnityEngine;

namespace CombatMaid.Core.MaidFSM.States
{
    /// <summary>
    /// 驻守模式
    /// </summary>
    public class State_HoldPosition : MaidStateBase
    {
        private Vector3 _holdPoint;
        private float _checkTimer = 0f;
        
        // 缓存原始AI参数
        private float _originPatrolRange;
        private float _originCombatMoveRange;
        
        // 驻守允许的最大活动半径
        private const float MaxWanderRadius = 2.5f; 
        
        private readonly Lazy<string> _txtHoldStart = new Lazy<string>(() => 
            LocalizationManager.GetText("Msg_Maid_HoldStart"));
        private readonly Lazy<string> _txtHoldFarAway = new Lazy<string>(() => 
            LocalizationManager.GetText("Msg_Maid_HoldFarAway"));
        private readonly Lazy<string> _txtHoldStop = new Lazy<string>(() => 
            LocalizationManager.GetText("Msg_Maid_HoldStop"));

        public override void Enter()
        {
            SetNativeBrainActive(true);

            _holdPoint = Controller.transform.position;
            
            if (Controller.AI != null)
            {
                _originPatrolRange = Controller.AI.patrolRange;
                _originCombatMoveRange = Controller.AI.combatMoveRange;
                
                Controller.AI.patrolPosition = _holdPoint;
                Controller.AI.patrolRange = 2.0f;       
                Controller.AI.combatMoveRange = 2.0f;
            }

            Controller.MaidCharacter?.PopText(_txtHoldStart.Value);
            //CMDebug.Log($"[Hold] 开始驻守，锚点坐标: {_holdPoint}");
        }

        public override void Update()
        {
            if (Controller.AI == null) return;

            Controller.AI.patrolPosition = _holdPoint;

            float currentDist = Vector3.Distance(Controller.transform.position, _holdPoint);
            
            if (currentDist > MaxWanderRadius)
            {
                Controller.AI.MoveToPos(_holdPoint);
            }

            _checkTimer += Time.deltaTime;
            if (_checkTimer > 0.5f)
            {
                _checkTimer = 0f;
                CheckDistanceToOwner();
            }
        }

        private void CheckDistanceToOwner()
        {
            if (Controller.MainOwner == null) return;

            float dist = Vector3.Distance(Controller.transform.position, Controller.MainOwner.transform.position);

            if (dist > Controller.HoldMaxDistance)
            {
                Controller.MaidCharacter?.PopText(_txtHoldFarAway.Value);
                Machine.ChangeState<State_ForceFollow>();
            }
        }

        public override void Exit()
        {
            if (Controller.AI != null)
            {
                Controller.AI.patrolRange = _originPatrolRange;
                Controller.AI.combatMoveRange = _originCombatMoveRange;
                Controller.AI.StopMove(); 
            }
            
            Controller.MaidCharacter?.PopText(_txtHoldStop.Value);
        }
    }
}