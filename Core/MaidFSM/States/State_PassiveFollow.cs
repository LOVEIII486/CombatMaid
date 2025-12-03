using UnityEngine;

namespace CombatMaid.Core.MaidFSM.States
{
    /// <summary>
    /// 和平跟随/潜行模式：
    /// 1. 暂停大脑，绝对不主动攻击或找掩体。
    /// 2. 紧贴玩家移动 (像宠物一样)。
    /// 3. 适合跑图、撤离或潜行。
    /// </summary>
    public class State_PassiveFollow : MaidStateBase
    {
        private const float FollowDistance = 2.5f; // 贴身距离
        private const float RepathInterval = 0.5f; // 路径更新频率
        
        private float _repathTimer = 0f;

        public override void Enter()
        {
            // 1. 关停大脑，停止一切自主决策
            SetNativeBrainActive(false);

            // 2. 物理清除仇恨，防止“身在曹营心在汉”
            // (即：身体跟着你跑，头却扭过去锁着敌人)
            if (Controller.AI != null)
            {
                Controller.AI.searchedEnemy = null;
                Controller.AI.aimTarget = null;
                Controller.AI.StopMove();
                
                // 强制关闭警觉状态，让她把枪放下（如果游戏逻辑支持的话）
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
            // 即使在和平跟随，如果玩家用修改器飞走了，或者跳崖了，
            // 超过最大传送距离 (TeleportDistance) 依然需要强制传送来兜底。
            float dist = Vector3.Distance(Controller.transform.position, Controller.MainOwner.transform.position);
            if (dist > Controller.TeleportDistance)
            {
                // 这里可以直接调用 Controller 的瞬移逻辑，或者切到 ForceFollow 让它处理
                // 为了流畅性，直接瞬移并保持当前状态
                TeleportToOwner();
            }
        }

        private void UpdateMove()
        {
            if (Controller.AI == null) return;

            float dist = Vector3.Distance(Controller.transform.position, Controller.MainOwner.transform.position);

            // 只有距离大于阈值才移动，避免贴在一起鬼畜抖动
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
            // 退出时不做特殊处理，交由下一个状态（如 Autonomous）去决定是否开启大脑
            // 但为了保险，停止当前的强行移动指令
            if (Controller.AI != null)
            {
                Controller.AI.StopMove();
            }
        }
    }
}