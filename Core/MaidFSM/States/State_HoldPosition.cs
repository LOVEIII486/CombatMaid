using UnityEngine;
using Duckov.Modding;

namespace CombatMaid.Core.MaidFSM.States
{
    /// <summary>
    /// 驻守模式：原地防守 + 电子围栏机制
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

        public override void Enter()
        {
            SetNativeBrainActive(true);

            _holdPoint = Controller.transform.position;
            
            if (Controller.AI != null)
            {
                _originPatrolRange = Controller.AI.patrolRange;
                _originCombatMoveRange = Controller.AI.combatMoveRange;
                
                // 1. 设置原生参数（虽然战斗时可能被忽略，但还是设置一下）
                Controller.AI.patrolPosition = _holdPoint;
                Controller.AI.patrolRange = 2.0f;       
                Controller.AI.combatMoveRange = 2.0f;
            }

            Controller.MaidCharacter?.PopText("正在驻守(坚守模式)");
            //CMDebug.Log($"[Hold] 开始驻守，锚点坐标: {_holdPoint}");
        }

        public override void Update()
        {
            if (Controller.AI == null) return;

            // 2. 围栏
            Controller.AI.patrolPosition = _holdPoint;

            float currentDist = Vector3.Distance(Controller.transform.position, _holdPoint);
            
            // 超出了驻守半径
            if (currentDist > MaxWanderRadius)
            {
                Controller.AI.MoveToPos(_holdPoint);
            }

            // 3. 防丢
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
                Controller.MaidCharacter?.PopText("距离过远-放弃驻守");
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
            
            Controller.MaidCharacter?.PopText("停止驻守");
        }
    }
}