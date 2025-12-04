using UnityEngine;

namespace CombatMaid.Core.MaidFSM.States
{
    /// <summary>
    /// 和平跟随/潜行模式：
    /// 1. 暂停大脑，不主动攻击或找掩体。
    /// 2. 紧贴玩家移动。
    /// </summary>
    public class State_PassiveFollow : MaidStateBase
    {
        private const float FollowDistance = 2.5f; // 贴身距离
        private const float RepathInterval = 0.5f; // 路径更新频率
        
        private float _repathTimer = 0f;

        public override void Enter()
        {
            // 1. 关停大脑
            SetNativeBrainActive(false);

            // 2. 物理清除仇恨
            if (Controller.AI != null)
            {
                Controller.AI.searchedEnemy = null;
                Controller.AI.aimTarget = null;
                Controller.AI.StopMove();
                
                // 强制关闭警觉状态
                Controller.AI.alert = false; 
                Controller.AI.noticed = false;
            }

            Controller.MaidCharacter?.PopText("和平跟随");
            
            // 立即执行一次移动
            UpdateMove();
        }

        public override void Update()
        {
            if (Controller.MainOwner == null) return;

            // 1. 周期性更新移动目标
            _repathTimer += Time.deltaTime;
            if (_repathTimer >= RepathInterval)
            {
                _repathTimer = 0f;
                UpdateMove();
            }

            // 2. 极端情况防丢检测
            float dist = Vector3.Distance(Controller.transform.position, Controller.MainOwner.transform.position);
            if (dist > Controller.TeleportDistance)
            {
                TeleportToOwner();
            }
        }

        private void UpdateMove()
        {
            if (Controller.AI == null) return;

            float dist = Vector3.Distance(Controller.transform.position, Controller.MainOwner.transform.position);

            // 只有距离大于阈值才移动
            if (dist > FollowDistance)
            {
                Controller.AI.MoveToPos(Controller.MainOwner.transform.position);
            }
        }

        private void TeleportToOwner()
        {
            Controller.transform.position = Controller.MainOwner.transform.position;
            if (Controller.AI.transform.parent != Controller.transform)
            {
                Controller.AI.transform.position = Controller.MainOwner.transform.position;
            }
            Controller.MaidCharacter?.PopText("跟丢传送");
        }
        
        public override void Exit()
        {
            // 退出时不做特殊处理，交由下一个状态去决定是否开启大脑
            if (Controller.AI != null)
            {
                Controller.AI.StopMove();
            }
        }
    }
}