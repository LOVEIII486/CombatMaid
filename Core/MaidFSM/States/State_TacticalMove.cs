using UnityEngine;

namespace CombatMaid.Core.MaidFSM.States
{
    /// <summary>
    /// 战术移动模式：响应 G 键指令
    /// </summary>
    public class State_TacticalMove : MaidStateBase
    {
        public Vector3 TargetPosition { get; set; }
        
        private float _timeoutTimer;
        private const float MaxDuration = 15.0f;

        public override void Enter()
        {
            // 1. 暂停原生AI
            SetNativeBrainActive(false);

            // 2. 清除瞄准锁定
            if (Controller.AI != null)
            {
                Controller.AI.aimTarget = null;
                Controller.AI.StopMove();
                Controller.AI.MoveToPos(TargetPosition);
                
                Controller.MaidCharacter?.PopText("战术机动...");
            }

            _timeoutTimer = MaxDuration;
        }

        public override void Update()
        {
            _timeoutTimer -= Time.deltaTime;

            if (HasArrived() || _timeoutTimer <= 0)
            {
                // 移动完成或超时，切回自主模式
                HandleArrivalLogic();
                Machine.ChangeState<State_Autonomous>();
            }
        }

        private void HandleArrivalLogic()
        {
            // 如果移动后离敌人太远，就清除仇恨
            var ai = Controller.AI;
            if (ai != null && ai.searchedEnemy != null)
            {
                float dist = Vector3.Distance(ai.transform.position, ai.searchedEnemy.transform.position);
                if (dist > 25.0f)
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
            // 如果不在移动且有路径，或者已经到达终点
            if ((!ai.IsMoving() && ai.HasPath()) || ai.ReachedEndOfPath()) return true;
            return false;
        }
    }
}