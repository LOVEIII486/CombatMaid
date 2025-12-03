using UnityEngine;

namespace CombatMaid.Core.MaidFSM.States
{
    /// <summary>
    /// 驻守/哨戒模式：
    /// 原地待命，允许主动战斗（找掩体/开火），但不跟随玩家。
    /// 只有当玩家距离过远时，才会打破驻守状态强制归队。
    /// </summary>
    public class State_HoldPosition : MaidStateBase
    {
        private Vector3 _holdPoint;
        private float _checkTimer = 0f;

        public override void Enter()
        {
            // 1. 开启大脑，保持战斗力
            SetNativeBrainActive(true);

            // 2. 钉住当前位置
            _holdPoint = Controller.transform.position;
            
            if (Controller.AI != null)
            {
                Controller.AI.patrolPosition = _holdPoint;
                // 缩小巡逻范围，让她只在驻守点附近找掩体，不要跑太远去追人
                Controller.AI.patrolRange = 5.0f; 
            }

            Controller.MaidCharacter?.PopText("正在驻守");
        }

        public override void Update()
        {
            // 1. 持续锁定巡逻点 (防止其他逻辑意外修改)
            if (Controller.AI != null)
            {
                Controller.AI.patrolPosition = _holdPoint;
            }

            // 2. 距离检查 (低频检测节省性能)
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

            // 使用更大的宽容距离 (HoldMaxDistance)
            if (dist > Controller.HoldMaxDistance)
            {
                Controller.MaidCharacter?.PopText("距离过远-放弃驻守");
                // 切换到强制跟随模式
                Machine.ChangeState<State_ForceFollow>();
            }
        }

        public override void Exit()
        {
            // 退出时恢复正常的巡逻范围 (原版默认通常是较大的范围或者由 Controller 动态控制)
            if (Controller.AI != null)
            {
                Controller.AI.patrolRange = 100.0f; 
            }
        }
    }
}