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

        public override void Enter()
        {
            // 1. 开启大脑
            SetNativeBrainActive(true);

            // 2. 钉住当前位置
            _holdPoint = Controller.transform.position;

            Controller.AI.patrolPosition = _holdPoint;
            // 缩小巡逻范围
            Controller.AI.patrolRange = 2.0f;
            Controller.MaidCharacter?.PopText("正在驻守");
        }

        public override void Update()
        {
            // 1. 持续锁定巡逻点
            Controller.AI.patrolPosition = _holdPoint;
            // 2. 距离检查
            _checkTimer += Time.deltaTime;
            if (_checkTimer > 0.5f)
            {
                _checkTimer = 0f;
                CheckDistance();
            }
        }

        private void CheckDistance()
        {
            if (Controller.MainOwner == null) return;

            float dist = Vector3.Distance(Controller.transform.position, Controller.MainOwner.transform.position);

            // 使用更大的宽容距离
            if (dist > Controller.HoldMaxDistance)
            {
                Controller.MaidCharacter?.PopText("距离过远-放弃驻守");
                // 切换到强制跟随模式
                Machine.ChangeState<State_ForceFollow>();
            }
        }

        public override void Exit()
        {
            Controller.AI.patrolRange = 10.0f;
            Controller.MaidCharacter?.PopText("停止驻守");
        }
    }
}