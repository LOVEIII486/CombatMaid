using UnityEngine;

namespace CombatMaid.Core.MaidFSM.States
{
    /// <summary>
    /// 强制跟随模式：防卡死、传送
    /// </summary>
    public class State_ForceFollow : MaidStateBase
    {
        private float _checkTimer;
        private float _stuckTimer;
        
        public override void Enter()
        {
            // 暂停 AI，专心跑路
            SetNativeBrainActive(false);
            
            // 清除战斗目标
            if (Controller.AI != null)
            {
                Controller.AI.searchedEnemy = null;
                Controller.AI.aimTarget = null;
                Controller.AI.StopMove();
            }
            
            Controller.MaidCharacter?.PopText("归队中...");
            _stuckTimer = 0f;
            
            // 立即触发一次移动
            MoveToOwner();
        }

        public override void Update()
        {
            if (Controller.MainOwner == null) return;

            // 1. 检查是否需要传送
            float dist = Vector3.Distance(Controller.transform.position, Controller.MainOwner.transform.position);
            
            // 如果在强制跟随状态下还这么远，或者时间太久，就传送
            if (dist > Controller.TeleportDistance || _stuckTimer > Controller.TeleportTimeout)
            {
                Teleport();
                Machine.ChangeState<State_Autonomous>();
                return;
            }

            // 2. 检查是否已经回到了安全距离
            if (dist < Controller.SafeDistanceToResumeCombat)
            {
                Controller.MaidCharacter?.PopText("归队完成");
                Machine.ChangeState<State_Autonomous>();
                return;
            }

            // 3. 驱动移动
            _checkTimer += Time.deltaTime;
            _stuckTimer += Time.deltaTime;
            
            if (_checkTimer > 1.0f)
            {
                MoveToOwner();
                _checkTimer = 0f;
            }
        }

        private void MoveToOwner()
        {
            if (Controller.AI != null)
            {
                Controller.AI.MoveToPos(Controller.MainOwner.transform.position);
            }
        }

        private void Teleport()
        {
            Controller.transform.position = Controller.MainOwner.transform.position;
            // 处理可能的父子级位移问题
            if (Controller.AI != null && Controller.AI.transform.parent != Controller.transform)
            {
                Controller.AI.transform.position = Controller.MainOwner.transform.position;
            }
            Controller.MaidCharacter?.PopText("强制传送");
        }
    }
}